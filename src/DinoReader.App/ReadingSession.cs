using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Shared;

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
/// <c>player.Start()</c>, possibly off it). A page with a parent's recording (Slice 111, Decision 115) plays its file instead, then the read goes on
/// with the next page; a generated read stops where the next recorded page begins.
/// </summary>
public sealed class ReadingSession
{
	// How long the session waits for the first audio after SpeakSegmentsAsync returns before it
	// stops showing the loading state (same grace as the Read The Stupid Text reader page).
	private static readonly TimeSpan FirstAudioGrace = TimeSpan.FromSeconds(8);

	private enum RecordedPlay
	{
		None,
		Playing,
		Paused,
	}

	private readonly ISpeechReader _reader;
	private readonly StoryVoicing _voicing;
	private readonly IRecordingPlayer _player;
	private readonly IStoryLibrary _library;
	private readonly ISettingsStore _settings;
	private readonly IVoiceModelService _model;
	private Story? _story;
	private string _reading = UiLanguage.English; // the language this screen reads in (the child's pick, Slice 173): passed to everything the read uses, never stored
	private IReadOnlyList<int> _pageStarts = [];
	private (int First, int Last)[] _pageSentences = [];
	private RecordedPlay _recorded;
	private readonly HashSet<string> _recastChecked = []; // books whose cast was checked against the narrator this session (Slice 144)
	private readonly HashSet<int> _unplayable = []; // pages whose recording failed to play this session: read in the generated voice
	private (int ReadId, int Sentence)? _continueRead; // a generated read cut short before a recorded page: where the read goes on once it ends
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

	public ReadingSession(ISpeechReader reader, StoryVoicing voicing, IRecordingPlayer player, IStoryLibrary library, ISettingsStore settings, IVoiceModelService model)
	{
		_reader = reader;
		_voicing = voicing;
		_player = player;
		_library = library;
		_settings = settings;
		_model = model;
		if (reader is CompositeSpeechReader composite)
		{
			composite.AppLanguage ??= () => LanguageChoice.Current(settings); // where the phone voice returns after a read in the child's pick (Slice 173)
		}
	}

	/// <summary>The language this screen reads in (Slice 173). It belongs to this session alone: the stored voice and app language are never changed or shadowed by it.</summary>
	public string ReadingLanguage => _reading;

	/// <summary>Reads in <paramref name="language"/> from the next read on.</summary>
	public void SetReadingLanguage(string language)
	{
		string resolved = UiLanguage.Resolve(language);
		bool changed = !string.Equals(_reading, resolved, StringComparison.Ordinal);
		_reading = resolved;
		if (changed)
		{
			_ = WarmUpLanguageAsync(resolved); // the model of a pick other than the stored language is built before its first read, not during it (Slice 177's warm-up)
		}
	}

	private async Task WarmUpLanguageAsync(string language)
	{
		try
		{
			if (_reader is IReadScoped scoped && !IsStoredVoiceLanguage(language) && _model is IReadingModelResolver resolver && await resolver.ModelForAsync(language) is { } pack)
			{
				await scoped.WarmUpScopedAsync(pack);
			}
		}
		catch (Exception)
		{
			// Best-effort: the read resolves and builds the pack itself.
		}
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

	/// <summary>True while a read that had started waits for its next chunk of audio (Slice 148): the page shows "Still getting the voice ready".</summary>
	public bool Stalled { get; private set; }

	public event EventHandler<int>? SentenceChanged;

	public event EventHandler? StateChanged;

	public event EventHandler? Failed;

	public SessionState State
	{
		get
		{
			if (_recorded != RecordedPlay.None)
			{
				return _recorded == RecordedPlay.Playing ? SessionState.Playing : SessionState.Paused;
			}

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
		_pageStarts = StoryRecordings.PageStarts(story.Pages);
		_pageSentences = SentencesOfPages(story.Pages.Count);
		_unplayable.Clear();
	}

	/// <summary>Takes the story as it is now (the grown-up turned "Read with my voice" on or off); the next read follows it.</summary>
	public void UpdateStory(Story story) => _story = story;

	/// <summary>Turns "Read with my voice" on or off on the session's own story, leaving the rest of it (a cast made this session, say) as it is.</summary>
	public void SetUseRecordings(bool useRecordings)
	{
		if (_story is { } story)
		{
			_story = story with { UseRecordings = useRecordings };
		}
	}

	/// <summary>The first and last sentence of the page that holds <paramref name="sentence"/> when that page plays a parent's recording (so the screen marks
	/// the whole page softly), else null.</summary>
	public (int First, int Last)? RecordedRangeOf(int sentence)
	{
		if (_story is not { } story || sentence < 0 || sentence >= _sentences.Count)
		{
			return null;
		}

		int page = PageOf(sentence);
		return PlaysRecording(story, page) && _pageSentences[page].First >= 0 ? _pageSentences[page] : null;
	}

	/// <summary>How long the recording of the page holding <paramref name="sentence"/> runs, in seconds, when that page plays one; else null.</summary>
	public int? RecordedSecondsOf(int sentence)
	{
		if (RecordedRangeOf(sentence) is null || _story is not { } story)
		{
			return null;
		}

		return StoryRecordings.Of(story, PageOf(sentence))?.Seconds;
	}

	private bool PlaysRecording(Story story, int page) => StoryRecordings.Plays(story, page) && !_unplayable.Contains(page);

	private int PageOf(int sentence) => StoryRecordings.PageOfPosition(_pageStarts, _sentences[sentence].Start);

	// The sentences a page holds, by the sentence that starts on it; (-1, -1) for a page with none.
	private (int First, int Last)[] SentencesOfPages(int pageCount)
	{
		var pages = Enumerable.Repeat((First: -1, Last: -1), Math.Max(1, pageCount)).ToArray();
		for (int i = 0; i < _sentences.Count; i++)
		{
			int page = PageOf(i);
			pages[page] = (pages[page].First < 0 ? i : pages[page].First, i);
		}

		return pages;
	}

	public void Attach()
	{
		Detach();
		_reader.StateChanged += OnReaderStateChanged;
		_reader.ChunkChanged += OnChunkChanged;
		_reader.Completed += OnCompleted;
		_reader.StallChanged += OnReaderStallChanged;
	}

	public void Detach()
	{
		_reader.StateChanged -= OnReaderStateChanged;
		_reader.ChunkChanged -= OnChunkChanged;
		_reader.Completed -= OnCompleted;
		_reader.StallChanged -= OnReaderStallChanged;
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
		if (_recorded == RecordedPlay.Playing)
		{
			_player.Pause();
			_recorded = RecordedPlay.Paused;
			StateChanged?.Invoke(this, EventArgs.Empty);
			return;
		}

		if (_reader.State == PlaybackState.Playing)
		{
			_reader.Pause();
		}
	}

	public void Resume()
	{
		if (_recorded == RecordedPlay.Paused)
		{
			_player.Resume();
			_recorded = RecordedPlay.Playing;
			StateChanged?.Invoke(this, EventArgs.Empty);
			return;
		}

		if (_reader.State == PlaybackState.Paused)
		{
			_reader.Resume();
		}
	}

	public void Stop()
	{
		CancelRead();
		StopRecording();
		_reader.Stop();
		_activeReadId = 0;
		_playingReadId = 0;
		Stalled = false;
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
		StopRecording();
		CancellationToken cancellation = BeginReadCancellation();
		Finished = false;
		MoveTo(sentence);
		int id = ++_readSequence;
		_pendingReadId = id;
		_playingReadId = 0;
		Volatile.Write(ref _activeReadId, id);
		if (TryPlayRecording(story, sentence, id))
		{
			return;
		}

		StateChanged?.Invoke(this, EventArgs.Empty);
		await SpeakAsync(story, _sentences[sentence].Start, id, cancellation);
	}

	private void StopRecording()
	{
		_continueRead = null;
		if (_recorded != RecordedPlay.None)
		{
			_player.Stop();
			_recorded = RecordedPlay.None;
		}
	}

	// A recorded page plays its file (Slice 111); a file that is gone reads the page in the generated voice. It is playing as soon as the
	// player has the file, so there is no loading state.
	private bool TryPlayRecording(Story story, int sentence, int id)
	{
		int page = PageOf(sentence);
		string? path = PlaysRecording(story, page) ? _library.TryResolveFile(story.Id, StoryRecordings.FileNameOf(page)) : null;
		if (path is null || !File.Exists(path))
		{
			return false;
		}

		try
		{
			_player.Play(path, () => MainThread.BeginInvokeOnMainThread(() => ApplyRecordingEnded(id, page)),
				() => MainThread.BeginInvokeOnMainThread(() => ApplyRecordingFailed(id, page)));
		}
		catch (Exception)
		{
			_unplayable.Add(page);
			return false;
		}

		_recorded = RecordedPlay.Playing;
		_playingReadId = id;
		ClearPending();
		return true;
	}

	// The page's recording played to its end: the read goes on with the sentence after the page (a recorded or a generated page), or the story is finished.
	private void ApplyRecordingEnded(int id, int page)
	{
		if (id != _activeReadId || _recorded == RecordedPlay.None)
		{
			return;
		}

		_recorded = RecordedPlay.None;
		int next = _pageSentences[page].Last + 1;
		if (next >= _sentences.Count)
		{
			_activeReadId = 0;
			_playingReadId = 0;
			Finished = true;
			StateChanged?.Invoke(this, EventArgs.Empty);
			return;
		}

		_ = StartAtAsync(next);
	}

	// The recording cannot be played (a corrupt or empty file): this page is read in the generated voice instead, and the read goes on from its start.
	private void ApplyRecordingFailed(int id, int page)
	{
		if (id != _activeReadId || _recorded == RecordedPlay.None)
		{
			return;
		}

		_unplayable.Add(page);
		if (_recorded == RecordedPlay.Paused)
		{
			_player.Stop();
			_recorded = RecordedPlay.None;
			StateChanged?.Invoke(this, EventArgs.Empty);
			return; // paused by the user: the page reads in the generated voice when they resume playing, not on its own
		}

		_ = StartAtAsync(_pageSentences[page].First);
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
			return await StartVoicedReadAsync(story, fromPosition, id, cancellation);
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

	private async Task<bool> StartVoicedReadAsync(Story story, int fromPosition, int id, CancellationToken cancellation)
	{
		string reading = _reading; // this read keeps the language it started in, whatever the screen switches to meanwhile
		story = await RecastClashesAsync(story, reading, cancellation);
		story = await KeepNewCastAsync(story, reading);
		bool untranslated = false;
		IReadOnlyList<VoicedSegment> segments = await _voicing.VoiceAsync(story, reading, fromPosition, cancellation, () => untranslated = true);
		if (id != _pendingReadId)
		{
			return false;
		}

		segments = StopBeforeRecordedPage(story, segments, fromPosition, id);
		if (segments.Count == 0 && _continueRead is { } cut && cut.ReadId == id)
		{
			_continueRead = null;
			_ = StartAtAsync(cut.Sentence);
			return false;
		}

		_readOrigin = segments.Count > 0 ? segments[0].SourceStart : fromPosition;
		await SpeakAsync(segments, untranslated ? UiLanguage.Resolve(story.Language) : _voicing.ReadingLanguage(story, reading)); // a translation that was skipped leaves the story's own voices
		return true;
	}

	// The voices of the segments belong to the pack of the language they are spoken in. That is the stored voice language for most reads (the plain read, head start
	// and all); a read in another language names its pack for this read only (Slice 173), so the stored language, the installer and the active model stay as they were.
	// A language whose pack is not on the phone (or an engine that cannot take a named pack) is read by the phone voice in that language; nothing is ever fetched here.
	private async Task SpeakAsync(IReadOnlyList<VoicedSegment> segments, string spoken)
	{
		if (_reader is not IReadScoped scoped || IsStoredVoiceLanguage(spoken))
		{
			await _reader.SpeakSegmentsAsync(segments);
			return;
		}

		VoiceModelPaths? pack = _model is IReadingModelResolver resolver ? await resolver.ModelForAsync(spoken) : null;
		if (pack is null)
		{
			StoryLanguages.NoteWanted(spoken); // read by the phone voice for want of a pack: Settings > Story languages shows it as missing, the child sees nothing
		}

		await scoped.SpeakSegmentsScopedAsync(segments, new ReadVoiceScope(pack, pack is null, spoken));
	}

	private bool IsStoredVoiceLanguage(string language) =>
		string.Equals(VoiceAvailability.Primary(language), VoiceAvailability.Primary(LanguageChoice.Current(_settings)), StringComparison.OrdinalIgnoreCase);

	// A generated read ends where the next page with a parent's recording begins, and goes on there with the recording (Slice 111).
	private IReadOnlyList<VoicedSegment> StopBeforeRecordedPage(Story story, IReadOnlyList<VoicedSegment> segments, int fromPosition, int id)
	{
		int fromPage = StoryRecordings.PageOfPosition(_pageStarts, fromPosition);
		int next = StoryRecordings.RecordedPages(story).FirstOrDefault(p => p > fromPage && PlaysRecording(story, p) && _pageSentences[p].First >= 0, -1);
		if (next < 0)
		{
			return segments;
		}

		int boundary = _pageStarts[next];
		_continueRead = (id, _pageSentences[next].First);
		return segments.Where(s => s.SourceEnd <= boundary).ToList(); // a segment running over the break is left to the recording, so its words are not heard twice
	}

	// A stored cast that gives a character the narrator's voice (or a voice the language dropped) is re-cast and saved (Slice 144: Farsi's default narrator became mana).
	// A book already checked this session is not checked again: a cast that cannot move (the one woman's voice is both narrator and mum) would otherwise be rebuilt on every read.
	private async Task<Story> RecastClashesAsync(Story story, string reading, CancellationToken cancellation)
	{
		if (_saveCast is null || _recastChecked.Contains(story.Id))
		{
			return story;
		}

		Story? recast = await NarratorRecast.TryRecastAsync(_voicing, story, cancellation);
		if (recast is not null || _voicing.VoicesFor(_voicing.ReadingLanguage(story, reading)).Count > 0)
		{
			_recastChecked.Add(story.Id); // only once the language has voices: a pack still installing is checked again on the next read
		}

		return recast is null ? story : await PersistCastAsync(recast);
	}

	// Keeps the story with its new cast in memory and tries to store it. A failed save only costs that: the next successful save stores it.
	private async Task<Story> PersistCastAsync(Story withCast)
	{
		try
		{
			await _saveCast!(withCast);
		}
		catch (Exception)
		{
			// The cast stays in memory for this session, so every later read keeps the same voices.
		}

		_story = withCast;
		return withCast;
	}

	// The first read in a language with no cast stores the one it makes, so its voices stay put (Slice 108). A failed save only costs that.
	private async Task<Story> KeepNewCastAsync(Story story, string reading)
	{
		string language = _voicing.ReadingLanguage(story, reading);
		if (_saveCast is null || story.HasCastIn(language))
		{
			return story;
		}

		StoryVoicing.NewCast? made = await _voicing.NewCastForReadingAsync(story, reading);
		if (made is null)
		{
			return story; // no voices or characters yet: asked again on the next read, once a pack has installed
		}

		return await PersistCastAsync(story.WithCastIn(made.Language, made.Members));
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

	private void OnReaderStallChanged(object? sender, bool stalled)
	{
		int id = Volatile.Read(ref _activeReadId);
		MainThread.BeginInvokeOnMainThread(() => ApplyStall(stalled, id));
	}

	// Only the active generated read can stall; a stall of a replaced or stopped read, or one that ends, is dropped or cleared.
	private void ApplyStall(bool stalled, int id)
	{
		bool next = stalled && id != 0 && id == _activeReadId && _recorded == RecordedPlay.None;
		if (next == Stalled)
		{
			return;
		}

		Stalled = next;
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
		if (id == 0 || id != _activeReadId || id != _playingReadId || _recorded != RecordedPlay.None)
		{
			return; // a recording plays now: this is the stopped generated read's late Completed
		}

		_activeReadId = 0;
		_playingReadId = 0;
		if (_continueRead is { } cut && cut.ReadId == id)
		{
			_continueRead = null;
			_ = StartAtAsync(cut.Sentence);
			return;
		}

		Finished = true;
		StateChanged?.Invoke(this, EventArgs.Empty); // the page counts a finished book from this
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
