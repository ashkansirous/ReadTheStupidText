using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

public enum SessionState
{
	Idle,
	Loading,
	Playing,
	Paused,
}

/// <summary>
/// One story being read aloud: starts a read at a sentence, follows the reader's chunks to say which
/// sentence is being spoken, and keeps the loading state straight. The page only draws what this
/// reports. Events are raised on the main thread (Slice 60's <c>ChunkChanged</c> fires after
/// <c>player.Start()</c>, possibly off it).
/// </summary>
public sealed class ReadingSession
{
	// How long the session waits for the first audio after SpeakSegmentsAsync returns before it
	// stops showing the loading state (same grace as the Read The Stupid Text reader page).
	private static readonly TimeSpan FirstAudioGrace = TimeSpan.FromSeconds(8);

	private readonly ISpeechReader _reader;
	private readonly StoryVoicing _voicing;
	private Story? _story;
	private Func<Story, Task>? _saveCast;
	private IReadOnlyList<SentenceSpan> _sentences = [];
	private int _readSequence;
	private int _pendingReadId;
	private int _returnedReadId;

	// The read whose chunks and completion count: set when a read starts, cleared when it is
	// stopped, fails or completes. Chunk events carry no id, so they are tagged with this when raised
	// and dropped if the read they belong to is no longer the active one.
	private int _activeReadId;
	private int _playingReadId;
	private int _readOrigin;

	// Cancelled when the read is stopped or superseded, so a translation still running for it is abandoned.
	private CancellationTokenSource? _readCancellation;

	/// <summary>Raised (off the main thread) when a read turns out not to be translated, so the voice should speak the story's own language.</summary>
	public event EventHandler? TranslationSkipped;

	public ReadingSession(ISpeechReader reader, StoryVoicing voicing)
	{
		_reader = reader;
		_voicing = voicing;
	}

	/// <summary>The sentence being spoken, or the last one spoken; -1 before the first.</summary>
	public int CurrentSentence { get; private set; } = -1;

	/// <summary>The story index being spoken, inside <see cref="CurrentSentence"/> (a chunk of a long sentence
	/// starts mid-sentence); -1 before the first.</summary>
	public int CurrentPosition { get; private set; } = -1;

	/// <summary>True once a read reached its natural end, so Play starts the story again from the top.
	/// Moving the mark or swiping to another page (<see cref="ClearFinished"/>) means Play starts there instead.</summary>
	public bool Finished { get; private set; }

	public void ClearFinished() => Finished = false;

	public event EventHandler<int>? SentenceChanged;

	public event EventHandler? StateChanged;

	public event EventHandler? Failed;

	public SessionState State
	{
		get
		{
			if (_reader.State == PlaybackState.Playing)
			{
				return SessionState.Playing;
			}

			if (_pendingReadId != 0)
			{
				return SessionState.Loading;
			}

			return _reader.State == PlaybackState.Paused ? SessionState.Paused : SessionState.Idle;
		}
	}

	/// <param name="saveCast">Stores the story with a cast made for a language that had none (Slice 108); null keeps nothing.</param>
	public void Open(Story story, IReadOnlyList<SentenceSpan> sentences, Func<Story, Task>? saveCast = null)
	{
		_story = story;
		_sentences = sentences;
		_saveCast = saveCast;
	}

	public void Attach()
	{
		Detach();
		_reader.StateChanged += OnReaderStateChanged;
		_reader.ChunkChanged += OnChunkChanged;
		_reader.Completed += OnCompleted;
	}

	public void Detach()
	{
		_reader.StateChanged -= OnReaderStateChanged;
		_reader.ChunkChanged -= OnChunkChanged;
		_reader.Completed -= OnCompleted;
	}

	/// <summary>Moves the current sentence without reading (a skip while nothing plays).</summary>
	public void MoveTo(int sentence) => MoveTo(sentence, _sentences[sentence].Start);

	private void MoveTo(int sentence, int position)
	{
		Finished = false;
		CurrentSentence = sentence;
		CurrentPosition = position;
		SentenceChanged?.Invoke(this, sentence);
	}

	public void Pause()
	{
		if (_reader.State == PlaybackState.Playing)
		{
			_reader.Pause();
		}
	}

	public void Resume()
	{
		if (_reader.State == PlaybackState.Paused)
		{
			_reader.Resume();
		}
	}

	public void Stop()
	{
		CancelRead();
		_reader.Stop();
		_activeReadId = 0;
		_playingReadId = 0;
		ClearPending();
	}

	/// <summary>Reads from the start of <paramref name="sentence"/> on, in each character's voice.</summary>
	public async Task StartAtAsync(int sentence)
	{
		if (_story is not { } story || sentence < 0 || sentence >= _sentences.Count)
		{
			return;
		}

		_reader.Stop();
		CancellationToken cancellation = BeginReadCancellation();
		Finished = false;
		MoveTo(sentence);
		int id = ++_readSequence;
		_pendingReadId = id;
		_playingReadId = 0;
		Volatile.Write(ref _activeReadId, id);
		StateChanged?.Invoke(this, EventArgs.Empty);
		await SpeakAsync(story, _sentences[sentence].Start, id, cancellation);
	}

	private async Task SpeakAsync(Story story, int fromPosition, int id, CancellationToken cancellation)
	{
		if (await TryStartReadAsync(story, fromPosition, id, cancellation))
		{
			await WaitForFirstAudioAsync(id);
		}
	}

	// A superseded or stopped read never alerts and never touches the current read's loading state.
	private async Task<bool> TryStartReadAsync(Story story, int fromPosition, int id, CancellationToken cancellation)
	{
		try
		{
			story = await KeepNewCastAsync(story);
			IReadOnlyList<VoicedSegment> segments = await _voicing.VoiceAsync(story, fromPosition, cancellation,
				() => TranslationSkipped?.Invoke(this, EventArgs.Empty));
			if (id != _pendingReadId)
			{
				return false;
			}

			_readOrigin = segments.Count > 0 ? segments[0].SourceStart : fromPosition;
			await _reader.SpeakSegmentsAsync(segments);
			return true;
		}
		catch (OperationCanceledException)
		{
			ClearPendingIfCurrent(id);
			return false;
		}
		catch (Exception)
		{
			FailRead(id);
			return false;
		}
	}

	// The first read in a language with no cast stores the one it makes, so its voices stay put (Slice 108). A failed save only costs that.
	private async Task<Story> KeepNewCastAsync(Story story)
	{
		string language = _voicing.ReadingLanguage(story);
		if (_saveCast is null || story.HasCastIn(language))
		{
			return story;
		}

		StoryVoicing.NewCast? made = await _voicing.NewCastForReadingAsync(story);
		if (made is null)
		{
			return story; // no voices or characters yet: asked again on the next read, once a pack has installed
		}

		Story withCast = story.WithCastIn(made.Language, made.Members);
		try
		{
			await _saveCast(withCast);
		}
		catch (Exception)
		{
			// The cast stays in memory for this session, so every later read keeps the same voices; the next successful save stores it.
		}

		_story = withCast;
		return withCast;
	}

	private CancellationToken BeginReadCancellation()
	{
		CancelRead();
		_readCancellation = new CancellationTokenSource();
		return _readCancellation.Token;
	}

	private void CancelRead()
	{
		_readCancellation?.Cancel();
		_readCancellation?.Dispose();
		_readCancellation = null;
	}

	private void FailRead(int id)
	{
		bool wasCurrent = id == _pendingReadId;
		if (wasCurrent)
		{
			_activeReadId = 0;
		}

		ClearPendingIfCurrent(id);
		if (wasCurrent)
		{
			Failed?.Invoke(this, EventArgs.Empty);
		}
	}

	// SpeakSegmentsAsync returned but nothing plays yet: keep loading until Playing, an Idle after
	// the return (ApplyReaderState), or the grace expires.
	private async Task WaitForFirstAudioAsync(int id)
	{
		if (id != _pendingReadId || _reader.State == PlaybackState.Playing)
		{
			ClearPendingIfCurrent(id);
			return;
		}

		_returnedReadId = id;
		await Task.Delay(FirstAudioGrace);
		ClearPendingIfCurrent(id);
	}

	private void ClearPendingIfCurrent(int id)
	{
		if (id == _pendingReadId && _reader.State != PlaybackState.Playing)
		{
			ClearPending();
		}
	}

	private void ClearPending()
	{
		_pendingReadId = 0;
		_returnedReadId = 0;
		StateChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnReaderStateChanged(object? sender, PlaybackState state) =>
		MainThread.BeginInvokeOnMainThread(() => ApplyReaderState(state));

	private void ApplyReaderState(PlaybackState state)
	{
		bool idleAfterReturn = state == PlaybackState.Idle
			&& _pendingReadId != 0 && _pendingReadId == _returnedReadId;
		if (state == PlaybackState.Playing)
		{
			_playingReadId = _activeReadId;
		}

		if (state == PlaybackState.Playing || idleAfterReturn)
		{
			ClearPending();
			return;
		}

		StateChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnCompleted(object? sender, EventArgs e)
	{
		int id = Volatile.Read(ref _activeReadId);
		MainThread.BeginInvokeOnMainThread(() => ApplyCompleted(id));
	}

	// Only the active read can finish the story, and only once its own audio has played: a stopped or
	// replaced read's late Completed (the event carries no id) arrives while the new read is still
	// loading, so it is ignored and neither sets Finished nor clears the new read's id.
	private void ApplyCompleted(int id)
	{
		if (id == 0 || id != _activeReadId || id != _playingReadId)
		{
			return;
		}

		_activeReadId = 0;
		_playingReadId = 0;
		Finished = true;
	}

	private void OnChunkChanged(object? sender, ReadChunk chunk)
	{
		int id = Volatile.Read(ref _activeReadId);
		MainThread.BeginInvokeOnMainThread(() => ApplyChunk(chunk, id));
	}

	// A chunk's range indexes the whole story; the sentence holding its start is the one being spoken.
	// The system-voice fallback speaks the joined segments as one utterance and reports the range
	// (0, length) of that text, which is not a story position: such a whole-read chunk is placed at
	// the read's own start, so the mark never jumps back to the first sentence.
	private void ApplyChunk(ReadChunk chunk, int id)
	{
		if (id == 0 || id != _activeReadId)
		{
			return;
		}

		int position = chunk.ChunkCount <= 1 ? _readOrigin : chunk.SourceStart;
		int sentence = StorySentences.IndexAt(_sentences, position);
		if (sentence >= 0)
		{
			MoveTo(sentence, position);
		}
	}
}
