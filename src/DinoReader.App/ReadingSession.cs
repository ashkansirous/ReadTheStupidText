using System.Diagnostics;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Mobile.Shared.Crashes;

namespace DinoReader;

public enum SessionState
{
	Idle,
	Loading,
	Playing,
	Paused,
}

/// <summary>
/// One story being read: starts a read at a sentence, follows the pre-made clips and recordings to say which sentence is being heard, and keeps the loading state straight.
/// The page only draws what this reports. Events are raised on the main thread. A page with a parent's recording (Slice 111, Decision 115) plays its file, then the read goes on
/// with the next page. Slice 278 part B: nothing here asks for a speech engine. A piece with no clip and no recording is not spoken: it is shown and highlighted for a short fixed beat
/// (<see cref="PlaybackSteps.BeatMilliseconds"/>), one fallback is reported for it, and the read goes on to the next piece as if it had played.
/// </summary>
public sealed class ReadingSession
{
	private enum RecordedPlay
	{
		None,
		Playing,
		Paused,
	}

	private readonly StoryVoicing _voicing;
	private readonly IRecordingPlayer _player;
	private readonly IStoryLibrary _library;
	private Story? _story;
	private string _reading = UiLanguage.English; // the language this screen reads in (the child's pick, Slice 173): passed to everything the read uses, never stored
	private IReadOnlyList<int> _pageStarts = [];
	private (int First, int Last)[] _pageSentences = [];
	private RecordedPlay _recorded;
	private IReadOnlySet<int>? _clipPages; // pages whose clips play while the book is translated; null when it is not
	private readonly HashSet<StoryClipKey> _unplayable = []; // clips of a recording that failed to play this session: read from the book's own clips (Slice 279: a clip is a run of a page's sentences)

	/// <summary>A parent's clip that plays: its key, the story's sentence numbers it covers (first and last) and its length in seconds.</summary>
	private readonly record struct ClipSpan(StoryClipKey Key, int First, int Last, int Seconds);
	private (int ReadId, int Sentence)? _continueRead; // a read cut short before a recorded page: where the read goes on once it ends
	// The read as a list of steps (a pre-made clip of one piece, an animal's recording, or one piece with no clip that is shown for a beat): one read id and the step being taken.
	private sealed class StepRead(int readId, List<PremadeAudioPlan.Step> steps, CancellationToken cancellation)
	{
		public int ReadId { get; } = readId;

		public List<PremadeAudioPlan.Step> Steps { get; } = steps;

		public CancellationToken Cancellation { get; } = cancellation;

		public int Index { get; set; }

		public HashSet<int> Reported { get; } = []; // the steps whose missing or unplayable clip was already reported, so a resumed step reports nothing twice
	}

	private StepRead? _stepRead;
	private StepRead? _restartStep; // a step paused by the user (a piece shown for a beat, or a clip that failed while paused): it starts again when they resume
	private CancellationTokenSource? _beat; // the timer of the beat being shown; null when none runs (a covered page or a person's pause stops it)
	private readonly SilentBeat _silent = new(); // the one clock of a silent piece: active, covered, held by the person; the timer runs only while all allow it (Slice 278 part B, reviews/s278b-design.md)
	private bool _pendingAdvance; // a step ended while the user had paused: Resume takes the next one
	private readonly ReadingPauseState _pauseState = new(); // a recorded page that ended while the user had paused: Resume goes on from it (Slice 271)
	private IReadOnlyList<SentenceSpan> _sentences = [];
	private int _readSequence;
	private int _pendingReadId;
	private string? _voiceCountedFor; // the book and reading language whose voice was counted: one read_voice count per book in this session, not one per mid-book start

	// The read whose steps count: set when a read starts, cleared when it is stopped, fails or completes. A callback tagged with another id is dropped.
	private int _activeReadId;

	// Slice 250: true between a counted read_started and its read_stopped, so every start gets exactly one stop.
	private bool _countedOpen;

	// Slice 259 item 1 (Debug trace): the time from a read's start to its first sound, once per read.
	private ReadStartTrace? _startTrace;

	// Cancelled when the read is stopped or superseded, so a translation still running for it is abandoned.
	private CancellationTokenSource? _readCancellation;

	private readonly ReadTheStupidText.Application.Usage.IFallbackSink _fallbacks; // Slice 273 part B

	public ReadingSession(StoryVoicing voicing, IRecordingPlayer player, IStoryLibrary library, ReadTheStupidText.Application.Usage.IFallbackSink? fallbacks = null)
	{
		_fallbacks = fallbacks ?? ReadTheStupidText.Application.Usage.NoFallbackSink.Instance;
		_voicing = voicing;
		_player = player;
		_library = library;
	}

	/// <summary>The language this screen reads in (Slice 173). It belongs to this session alone: the stored app language is never changed or shadowed by it.</summary>
	public string ReadingLanguage => _reading;

	/// <summary>Reads in <paramref name="language"/> from the next read on.</summary>
	public void SetReadingLanguage(string language) => _reading = UiLanguage.Resolve(language);

	/// <summary>The sentence being heard, or the last one; -1 before the first.</summary>
	public int CurrentSentence { get; private set; } = -1;

	/// <summary>The story index being heard, inside <see cref="CurrentSentence"/>; -1 before the first.</summary>
	public int CurrentPosition { get; private set; } = -1;

	/// <summary>True once a read reached its natural end, so Play starts the story again from the top.
	/// Moving the mark or swiping to another page (<see cref="ClearFinished"/>) means Play starts there instead.</summary>
	public bool Finished { get; private set; }

	public void ClearFinished() => Finished = false;

	public event EventHandler<int>? SentenceChanged;

	public event EventHandler? StateChanged;

	/// <summary>Slice 250: a read started, paused, resumed or stopped (with the closed reason for a stop; null for the other verbs). The page turns each into a usage count.</summary>
	public event Action<ReadingEventKind, ReadStopReason?>? Playback;

	/// <summary>Slice 267: this session's read really started; the story and the voice detail (<c>premade</c>, or <c>device:&lt;reason&gt;</c>, the reason the pieces without a clip had none). Raised on the session itself, so only the page that owns it counts the read.</summary>
	public event Action<Story, ReadVoiceUse>? VoiceUsed;

	/// <summary>The piece now shown has (true) or lacks (false) a clip. Raised on the main thread; the page shows the book's audio state while pieces lack one, so a read with no audio is never a silent slideshow with no word about it.</summary>
	public event Action<bool>? PieceAudio;

	public event EventHandler? Failed;

	public SessionState State
	{
		get
		{
			if (_recorded != RecordedPlay.None)
			{
				return _recorded == RecordedPlay.Playing ? SessionState.Playing : SessionState.Paused;
			}

			if (_restartStep is not null || (_silent.Active && _silent.HeldByPerson))
			{
				return SessionState.Paused;
			}

			if (_silent.Active)
			{
				return SessionState.Playing; // running, or only covered: the pending marker is already cleared, so never Loading
			}

			return _pendingReadId != 0 ? SessionState.Loading : SessionState.Idle;
		}
	}

	public void Open(Story story, IReadOnlyList<SentenceSpan> sentences)
	{
		_story = story;
		_sentences = sentences;
		_pageStarts = StoryRecordings.PageStarts(story.Pages);
		_pageSentences = SentencesOfPages(story.Pages.Count);
		_unplayable.Clear();
		_clipPages = null;
	}

	/// <summary>While the book reads in a translation, the pages whose clips still play (the pages fully covered by clips, Slice 279); a page with only some sentences recorded reads translated with the pre-made audio. Null when the book is not translated: every clip plays.</summary>
	public void SetClipPages(IReadOnlySet<int>? pages) => _clipPages = pages;

	private bool ClipStands(StoryClipKey key) => !_unplayable.Contains(key) && (_clipPages is null || _clipPages.Contains(key.Page));

	/// <summary>Takes the story as it is now (the grown-up turned "Read with my voice" on or off); the next read follows it.</summary>
	public void UpdateStory(Story story) => _story = story;

	/// <summary>Turns "Read with my voice" on or off on the session's own story, leaving the rest of it as it is.</summary>
	public void SetUseRecordings(bool useRecordings)
	{
		if (_story is { } story)
		{
			_story = story with { UseRecordings = useRecordings };
		}
	}

	/// <summary>The first and last sentence of the clip that covers <paramref name="sentence"/> when it plays a parent's recording (so the screen marks
	/// the whole run softly: a page, or the lines recorded as one clip), else null.</summary>
	public (int First, int Last)? RecordedRangeOf(int sentence) =>
		_story is { } story && ClipAt(story, sentence) is { } clip ? (clip.First, clip.Last) : null;

	/// <summary>How long the clip covering <paramref name="sentence"/> runs, in seconds, when one plays; else null.</summary>
	public int? RecordedSecondsOf(int sentence) =>
		_story is { } story && ClipAt(story, sentence) is { } clip ? clip.Seconds : null;

	// The playing clip that covers the sentence, in the story's sentence numbers; clips are counted from 0 on their page, so a clip is placed by the first sentence of its page.
	private ClipSpan? ClipAt(Story story, int sentence)
	{
		if (sentence < 0 || sentence >= _sentences.Count)
		{
			return null;
		}

		int page = PageOf(sentence);
		(int pageFirst, int pageLast) = _pageSentences[page];
		return pageFirst < 0 ? null : StoryRecordings.PlayingClips(story)
			.Where(key => key.Page == page && ClipStands(key))
			.Select(key => SpanOf(story, key, pageFirst, pageLast))
			.FirstOrDefault(span => span is { } s && s.First <= sentence && sentence <= s.Last);
	}

	private static ClipSpan? SpanOf(Story story, StoryClipKey key, int pageFirst, int pageLast)
	{
		long first = (long)pageFirst + key.First;
		if (!key.FitsPage(pageLast - pageFirst + 1) || StoryRecordings.ClipOf(story, key) is not { } recording)
		{
			return null; // the page has fewer sentences than the clip names: it was recorded for other words, so it neither plays nor counts as recorded (it stays in the story)
		}

		int last = (int)Math.Min((long)pageFirst + key.Last, pageLast);
		return new ClipSpan(key, (int)first, last, recording.Seconds);
	}

	// Every playing clip that begins after the position, the earliest first.
	private ClipSpan? NextClipAfter(Story story, int position)
	{
		ClipSpan? next = null;
		foreach (StoryClipKey key in StoryRecordings.PlayingClips(story).Where(k => ClipStands(k) && k.Page < _pageSentences.Length))
		{
			(int pageFirst, int pageLast) = _pageSentences[key.Page];
			if (pageFirst >= 0 && SpanOf(story, key, pageFirst, pageLast) is { } span && _sentences[span.First].Start > position && (next is null || span.First < next.Value.First))
			{
				next = span;
			}
		}

		return next;
	}

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

	public void Detach() => AppTrace.Reading(TraceLines.ReadingEvent.Detached, TraceLines.ReadingReason.PageLeft); // Slice 243 item 5

	/// <summary>Moves the current sentence without reading (a skip while nothing plays).</summary>
	public void MoveTo(int sentence) => MoveTo(sentence, _sentences[sentence].Start);

	private void MoveTo(int sentence, int position)
	{
		Finished = false;
		CurrentSentence = sentence;
		CurrentPosition = position;
		SentenceChanged?.Invoke(this, sentence);
	}

	/// <param name="reason">Why, one of <see cref="TraceLines.ReadingReason"/>; logged only when a read was playing.</param>
	/// <param name="counted">False for a pause the app makes itself (the language menu), which is not a count (Slice 250).</param>
	public void Pause(string reason, bool counted = true)
	{
		if (_recorded == RecordedPlay.Playing)
		{
			AppTrace.Reading(TraceLines.ReadingEvent.Paused, reason);
			_player.Pause();
			_recorded = RecordedPlay.Paused;
			RaiseIf(counted, ReadingEventKind.Paused);
			StateChanged?.Invoke(this, EventArgs.Empty);
			return;
		}

		if (_silent.Active && !_silent.HeldByPerson)
		{
			AppTrace.Reading(TraceLines.ReadingEvent.Paused, reason);
			RaiseIf(counted, ReadingEventKind.Paused);
			if (_silent.Pause())
			{
				StopBeat();
			}

			AudioPackLog.Write("silent piece paused, it is shown again on resume");
			StateChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	/// <summary>A piece shown for a beat has no sound, so while the page is covered (screen lock, another app, the grown-up gate) its timer stops; it never runs on unseen. Only the cover flips: a person's pause is untouched. A clip plays on, as audio does.</summary>
	public void PauseBeatWhenHidden()
	{
		if (_silent.Hide())
		{
			StopBeat();
		}
	}

	/// <summary>The page is shown again: the beat of a piece that is still wanted starts again, whole. A pause the person made themselves stays paused.</summary>
	public void ResumeBeatWhenShown()
	{
		if (_silent.Show())
		{
			StartBeatTimer();
		}
	}

	// The person (or the app's own pause, uncounted) resumes a held beat: logged as a user resume only when counted, and the whole beat starts at once, or when the page is shown if it is covered.
	private void ResumeBeat(bool counted)
	{
		AppTrace.Reading(TraceLines.ReadingEvent.Resumed, counted ? TraceLines.ReadingReason.User : TraceLines.ReadingReason.Other);
		RaiseIf(counted, ReadingEventKind.Resumed);
		if (_silent.Resume())
		{
			StartBeatTimer();
		}
		else if (_silent.Hidden)
		{
			AudioPackLog.Write("silent piece resumed while the page is covered, it is shown when the page is");
		}

		StateChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <param name="counted">False for a resume of a pause the app made itself (the language menu), which is not a count (Slice 250).</param>
	public void Resume(bool counted = true)
	{
		if (_silent.Active && _silent.HeldByPerson)
		{
			ResumeBeat(counted);
			return;
		}

		if (_restartStep is { } restart)
		{
			_restartStep = null;
			AppTrace.Reading(TraceLines.ReadingEvent.Resumed, TraceLines.ReadingReason.User);
			RaiseIf(counted, ReadingEventKind.Resumed);
			RunStep(restart);
			return;
		}

		if (_pendingAdvance)
		{
			RaiseIf(counted, ReadingEventKind.Resumed);
			ResumePendingAdvance();
			return;
		}

		if (_recorded == RecordedPlay.Paused)
		{
			AppTrace.Reading(TraceLines.ReadingEvent.Resumed, TraceLines.ReadingReason.User);
			RaiseIf(counted, ReadingEventKind.Resumed);
			_recorded = RecordedPlay.Playing;
			if (_pauseState.TakeHeld() is { } ended)
			{
				ApplyRecordingEnded(ended.Id, ended.Page, ended.Last); // (Page here is the clip's first sentence, Slice 279) the clip had already ended: go on instead of resuming a finished player
				return;
			}

			_player.Resume();
			StateChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private void RaiseIf(bool counted, ReadingEventKind kind)
	{
		if (counted)
		{
			Playback?.Invoke(kind, null);
		}
	}

	// Slice 250: closes the counted read if one is open, so a start never gets a second stop.
	private void RaiseStopped(ReadStopReason reason)
	{
		if (_countedOpen)
		{
			_countedOpen = false;
			Playback?.Invoke(ReadingEventKind.Stopped, reason);
		}
	}

	/// <param name="reason">Why (Slice 250: a closed <see cref="ReadStopReason"/>, written to the log as its <see cref="ReadStopTrace"/> text and counted by its usage detail); logged and counted only when a read was playing, paused or loading (a stop of an idle session changes nothing).</param>
	public void Stop(ReadStopReason reason)
	{
		if (State != SessionState.Idle || _activeReadId != 0)
		{
			AppTrace.Reading(TraceLines.ReadingEvent.Stopped, ReadStopTrace.ReasonOf(reason));
		}

		RaiseStopped(reason);

		CancelRead();
		StopRecording();
		_activeReadId = 0;
		ClearPending();
	}

	/// <summary>Reads from the start of <paramref name="sentence"/> on. A start asked for from outside counts as a started read (Slice 250); the read's own continuations do not.</summary>
	public Task StartAtAsync(int sentence)
	{
		if (_story is not null && sentence >= 0 && sentence < _sentences.Count)
		{
			RaiseStopped(ReadStopReason.Restarted); // a start that cuts a running read closes it first
			_countedOpen = true;
			Playback?.Invoke(ReadingEventKind.Started, null);
		}

		return ContinueAtAsync(sentence);
	}

	private async Task ContinueAtAsync(int sentence)
	{
		if (_story is not { } story || sentence < 0 || sentence >= _sentences.Count)
		{
			return;
		}

		StopRecording();
		AppTrace.Reading(TraceLines.ReadingEvent.Started);
		CancellationToken cancellation = BeginReadCancellation();
		Finished = false;
		MoveTo(sentence);
		int id = ++_readSequence;
		_pendingReadId = id;
		Volatile.Write(ref _activeReadId, id);
		Volatile.Write(ref _startTrace, ReadStartTrace.Begin(id, _sentences[sentence].Start));
		if (TryPlayRecording(story, sentence, id))
		{
			return;
		}

		StateChanged?.Invoke(this, EventArgs.Empty);
		await TryStartReadAsync(story, _sentences[sentence].Start, id, cancellation);
	}

	private void StopRecording()
	{
		_continueRead = null;
		_stepRead = null;
		_restartStep = null;
		_silent.End();
		_pendingAdvance = false;
		StopBeat();
		_pauseState.Clear(); // a stale flag must not fire on a later Resume
		if (_recorded != RecordedPlay.None)
		{
			_player.Stop();
			_recorded = RecordedPlay.None;
		}
	}

	private void StopBeat()
	{
		CancellationTokenSource? beat = Interlocked.Exchange(ref _beat, null);
		beat?.Cancel();
		beat?.Dispose();
	}

	private bool FileExists(string storyId, string name) => _library.TryResolveFile(storyId, name) is { } path && File.Exists(path);

	// A recorded clip plays its file (Slice 111, Slice 279: a page or a run of its sentences); a file that is gone reads those sentences from the book's own clips. It is playing as soon as the
	// player has the file, so there is no loading state.
	private bool TryPlayRecording(Story story, int sentence, int id)
	{
		ClipSpan? clip = ClipAt(story, sentence);
		string? path = clip is { } c ? _library.TryResolveFile(story.Id, StoryRecordings.FileOf(story, c.Key, name => FileExists(story.Id, name))) : null;
		if (clip is null || path is null || !File.Exists(path))
		{
			if (clip is not null)
			{
				ReportRecordingUnplayable(); // Slice 273 part B: the clip was to play a recording but its file is gone
				RecordedClipLog($"[RecordedClip] missing {clip.Value.Key}"); // the sentences are read from the book's own clips
			}

			return false;
		}

		int first = clip.Value.First;
		int last = clip.Value.Last;
		try
		{
			_player.Play(path, () => MainThread.BeginInvokeOnMainThread(() => ApplyRecordingEnded(id, first, last)),
				() => MainThread.BeginInvokeOnMainThread(() => ApplyRecordingFailed(id, first)));
		}
		catch (Exception)
		{
			_unplayable.Add(clip.Value.Key);
			ReportRecordingUnplayable();
			return false;
		}

		RecordedClipLog($"[RecordedClip] playing {clip.Value.Key} sentences={clip.Value.First}-{clip.Value.Last}");
		_recorded = RecordedPlay.Playing;
		_pauseState.Clear();
		RunOnMain(() => PieceAudio?.Invoke(true)); // sound plays: an earlier silent piece's status line goes
		ClearPending();
		return true;
	}

	// The page's recording played to its end: the read goes on with the sentence after the page (a recorded page or the book's clips), or the story is finished.
	private void ApplyRecordingEnded(int id, int clipFirst, int clipLast)
	{
		// Slice 270: Pause landed after the clip ended; the read holds here and Resume goes on. A stale id or no recording playing is ignored (Slice 271: ReadingPauseState, unit-tested).
		// The state keeps the clip's first and last sentence (Slice 279): the read goes on after the clip's own end, whatever the story plays by then.
		if (_pauseState.OnRecordingEnded(id, clipFirst, clipLast, activeReadId: _activeReadId, recordingActive: _recorded != RecordedPlay.None, paused: _recorded == RecordedPlay.Paused) != RecordingEndAction.Advance)
		{
			return;
		}

		_recorded = RecordedPlay.None;
		int next = clipLast + 1;
		if (next >= _sentences.Count)
		{
			_activeReadId = 0;
			Finished = true;
			RaiseStopped(ReadStopReason.Finished);
			StateChanged?.Invoke(this, EventArgs.Empty);
			return;
		}

		_ = ContinueAtAsync(next);
	}

	// The recording cannot be played (a corrupt or empty file): its sentences are read from the book's own clips instead, and the read goes on from their start.
	private void ApplyRecordingFailed(int id, int clipFirst)
	{
		if (id != _activeReadId || _recorded == RecordedPlay.None)
		{
			return;
		}

		if (_story is { } story && ClipAt(story, clipFirst) is { } clip)
		{
			_unplayable.Add(clip.Key);
			RecordedClipLog($"[RecordedClip] unplayable {clip.Key}");
		}

		ReportRecordingUnplayable();
		if (_recorded == RecordedPlay.Paused)
		{
			_player.Stop();
			_recorded = RecordedPlay.None;
			StateChanged?.Invoke(this, EventArgs.Empty);
			return; // paused by the user: the page reads from the book's clips when they resume playing, not on its own
		}

		_ = ContinueAtAsync(clipFirst);
	}

	[Conditional("DEBUG")] // which clip played or was missing, so a device check can tell a recording from the book's own audio (Slice 279)
	private static void RecordedClipLog(string line)
	{
		Debug.WriteLine(line);
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write(line);
#endif
	}

	// Slice 273 part B: a recording that cannot be played falls back to the book's own clips.
	private void ReportRecordingUnplayable() =>
		_fallbacks.Report(ReadTheStupidText.Application.Usage.FallbackArea.Speech, ReadTheStupidText.Application.Usage.FallbackReason.RecordingUnplayable, _reading);

	// A superseded or stopped read never alerts and never touches the current read's loading state.
	private async Task TryStartReadAsync(Story story, int fromPosition, int id, CancellationToken cancellation)
	{
		try
		{
			await StartVoicedReadAsync(story, fromPosition, id, cancellation);
		}
		catch (OperationCanceledException)
		{
			// no-fallback: the read was superseded or stopped
			ClearPendingIfCurrent(id);
		}
		catch (Exception)
		{
			// no-fallback: the read fails and the person is told (FailRead); nothing else is tried
			FailRead(id);
		}
	}

	private async Task StartVoicedReadAsync(Story story, int fromPosition, int id, CancellationToken cancellation)
	{
		string reading = _reading; // this read keeps the language it started in, whatever the screen switches to meanwhile
		bool untranslated = false;
		var why = new StoryVoicing.VoiceWhy();
		IReadOnlyList<VoicedSegment> segments = await _voicing.VoiceAsync(story, reading, fromPosition, cancellation, () => untranslated = true, why);
		_startTrace?.Voiced(id, segments.Count, untranslated);
		if (id != _pendingReadId)
		{
			return;
		}

		segments = StopBeforeRecordedPage(story, segments, fromPosition, id);
		if (segments.Count == 0 && _continueRead is { } cut && cut.ReadId == id)
		{
			_continueRead = null;
			_ = ContinueAtAsync(cut.Sentence);
			return;
		}

		CountVoiceOncePerBook(story, reading, segments, why); // Slice 273: counted on the pieces this read really plays, after the recorded page cut off the rest
		await AnimalClipFiles.EnsureAsync(AnimalClipRuns.KindsIn(segments));
		cancellation.ThrowIfCancellationRequested(); // a Stop, Back or another sentence during the copy: this read never plays a clip
		if (id != _pendingReadId)
		{
			return;
		}

		BeginSteps(segments, id, cancellation);
	}

	// Slice 267: the voice a book is read with is counted once per book and reading language in this session, at the moment its read really starts.
	private void CountVoiceOncePerBook(Story story, string reading, IReadOnlyList<VoicedSegment> segments, StoryVoicing.VoiceWhy why)
	{
		if (!StoryVoicing.SpeaksAnyPiece(segments))
		{
			return; // nothing is played, so nothing is counted and the once-per-book key stays free for a read that plays
		}

		string key = $"{story.Id}|{reading}";
		if (_voiceCountedFor == key)
		{
			return;
		}

		_voiceCountedFor = key;
		ReadVoiceUse use = _voicing.ReportVoiceUsed(story, segments, why);
		try
		{
			VoiceUsed?.Invoke(story, use);
		}
		catch (Exception ex)
		{
			// no-fallback: counting is best effort and never reports itself
			AudioPackLog.Write($"the voice-used count failed ({ex.GetType().Name})");
		}
	}

	// The read as steps (PlaybackSteps): a piece's clip, an animal's recording, or a piece with no clip shown for a beat. When the last step has ended the read is over.
	private void BeginSteps(IReadOnlyList<VoicedSegment> segments, int id, CancellationToken cancellation)
	{
		cancellation.ThrowIfCancellationRequested();
		var read = new StepRead(id, PlaybackSteps.Plan(segments), cancellation);
		_stepRead = read;
		_pendingAdvance = false;
		if (read.Steps.Count == 0)
		{
			_stepRead = null;
			ClearPending();
			EndRead(id);
			return;
		}

		StartStep(read);
	}

	// Every end signal of a step (the clip ended or failed, the beat is over, the clip file is missing) comes here with the read's id. A stale or cancelled read is dropped,
	// a paused one waits for Resume, and otherwise the next step starts; past the last step the read ends.
	private void AdvanceStep(int readId)
	{
		StepRead? read = _stepRead;
		bool stale = read is null || read.ReadId != readId || readId != _activeReadId;
		AnimalClipRuns.Advance decision = AnimalClipRuns.Decide(stale, read is null || read.Cancellation.IsCancellationRequested, IsPaused);
		if (decision == AnimalClipRuns.Advance.Ignore)
		{
			return;
		}

		if (decision == AnimalClipRuns.Advance.Defer)
		{
			_pendingAdvance = true;
			return;
		}

		_pendingAdvance = false;
		_recorded = RecordedPlay.None; // a clip that was playing is over
		read!.Index++;
		if (read.Index >= read.Steps.Count)
		{
			_stepRead = null;
			EndRead(readId);
			return;
		}

		RunStep(read);
	}

	private bool IsPaused => _recorded == RecordedPlay.Paused || _restartStep is not null || (_silent.Active && _silent.HeldByPerson);

	// Resume after a step ended while paused: the pause is over and the step that was waiting starts.
	private void ResumePendingAdvance()
	{
		_pendingAdvance = false;
		if (_recorded != RecordedPlay.None)
		{
			_player.Stop(); // a paused clip must not stay loaded
		}

		_recorded = RecordedPlay.None;
		if (_stepRead is { } read)
		{
			AdvanceStep(read.ReadId);
		}
	}

	// A step that follows the first one: started from an end signal, so it owns its own failure handling.
	private void RunStep(StepRead read)
	{
		int id = read.ReadId;
		try
		{
			StartStep(read);
		}
		catch (OperationCanceledException)
		{
			// no-fallback: the read was superseded or stopped
			ClearPendingIfCurrent(id);
		}
		catch (Exception)
		{
			// no-fallback: the read fails and the person is told (FailRead); nothing else is tried
			FailRead(id);
		}
	}

	private void StartStep(StepRead read)
	{
		read.Cancellation.ThrowIfCancellationRequested();
		PremadeAudioPlan.Step step = read.Steps[read.Index];
		if (step.Animal is { } clip)
		{
			PlayClip(read, clip);
			return;
		}

		if (step.Premade is { } premade)
		{
			if (TryPlayPremade(read, premade))
			{
				return;
			}

			step = read.Steps[read.Index] = PlaybackSteps.AsSilentPiece(step); // no usable clip: this piece is shown for a beat
		}

		BeginBeat(read, step);
	}

	// Plays the animal's recording. A clip that is missing or will not play is skipped silently, never read as the sound word, and the read goes straight on (posted, so it
	// never runs inside the step that is starting).
	private void PlayClip(StepRead read, AnimalKind clip)
	{
		int index = read.Index;
		string? path = AnimalClipFiles.PathOf(clip);
		if (path is null)
		{
#if DEBUG
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Reading] animal clip {clip} has no file, slot skipped");
#endif
			SkipClip(read, index);
			return;
		}

		try
		{
			_player.Play(path, () => MainThread.BeginInvokeOnMainThread(() => ApplyClipEnded(read, index, clip, failed: false)),
				() => MainThread.BeginInvokeOnMainThread(() => ApplyClipEnded(read, index, clip, failed: true)));
		}
		catch (Exception ex)
		{
#if DEBUG
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Reading] animal clip {clip} could not play ({ex.GetType().Name}), slot skipped");
#endif
			_fallbacks.Report(ReadTheStupidText.Application.Usage.FallbackArea.Speech, ReadTheStupidText.Application.Usage.FallbackReason.AnimalClipUnplayable, _reading); // Slice 273 part D: the sound is skipped
			AnimalClipFiles.Discard(clip);
			SkipClip(read, index);
			return;
		}

#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Reading] animal clip {clip} plays");
#endif
		_recorded = RecordedPlay.Playing;
		RunOnMain(() => PieceAudio?.Invoke(true)); // sound plays: an earlier silent piece's status line goes
		ClearPending();
	}

	// Slice 246 C2: plays the pre-made clip of a store book's piece at the reading speed, with the piece's sentence marked as it starts.
	// False (nothing started) when the file is gone or the player will not open it; the caller then shows the piece for a beat. The clip is "recorded" for the session's
	// state, Pause, Resume and Stop, exactly as an animal's recording is, and its end advances the read.
	private bool TryPlayPremade(StepRead read, VoicedSegment segment)
	{
		string? path = PremadeClipMarker.PathOf(segment);
		int index = read.Index;
		if (path is null || !File.Exists(path))
		{
			AudioPackLog.Write("premade clip has no file, the piece is shown for a beat");
			return false;
		}

		try
		{
			double rate = ReadingSpeedPreference.Load().Value;
			// The clip counts as playing before Play, so an ended or failed callback that reaches the main thread at once finds it playing and is handled, not dropped.
			_recorded = RecordedPlay.Playing;
			_player.Play(path, () => MainThread.BeginInvokeOnMainThread(() => ApplyPremadeEnded(read, index, failed: false)),
				() => MainThread.BeginInvokeOnMainThread(() => ApplyPremadeEnded(read, index, failed: true)), rate);
		}
		catch (Exception ex)
		{
			_recorded = RecordedPlay.None;
			AudioPackLog.Write($"premade clip could not play ({ex.GetType().Name}), the piece is shown for a beat");
			if (read.Reported.Add(index))
			{
				_fallbacks.Report(ReadTheStupidText.Application.Usage.FallbackArea.PremadeAudio, ReadTheStupidText.Application.Usage.FallbackReason.ClipUnplayable, _reading); // Slice 273 part D
			}

			return false;
		}

		AudioPackLog.Write($"premade clip plays (sentence {StorySentences.IndexAt(_sentences, segment.SourceStart)}) clip {Path.GetFileName(path)}");
		NoteFirstSound("premade clip", read.ReadId);
		RunOnMain(() => MarkPiece(read, segment));
		RunOnMain(() => PieceAudio?.Invoke(true));
		ClearPending();
		return true;
	}

	// A piece that is not played reports one fallback per occurrence, through the app's sink: once for a step, however often a pause makes it start again.
	private void ReportPieceFallback(StepRead read, int index, string reason)
	{
		if (read.Reported.Add(index))
		{
			_fallbacks.Report(ReadTheStupidText.Application.Usage.FallbackArea.PremadeAudio, reason, _reading);
		}
	}

	private void MarkPiece(StepRead read, VoicedSegment segment)
	{
		int sentence = StorySentences.IndexAt(_sentences, segment.SourceStart);
		if (read == _stepRead && sentence >= 0)
		{
			MoveTo(sentence, segment.SourceStart);
		}
	}

	private static void RunOnMain(Action action)
	{
		if (MainThread.IsMainThread)
		{
			action();
		}
		else
		{
			MainThread.BeginInvokeOnMainThread(action);
		}
	}

	// A piece with no clip and no recording is not spoken (Slice 278 part B): its sentence is marked, it stays for a short fixed beat, one fallback is reported for it, and the read
	// goes on with the next piece as if it had played. The piece is current at once, covered page or not; only the timer waits for SilentBeat (a cover or the person's pause stops it, and
	// whatever lets it run again starts a whole beat). Stop and a newer read cancel it too.
	private void BeginBeat(StepRead read, PremadeAudioPlan.Step step)
	{
		int index = read.Index;
		VoicedSegment piece = step.Run[0];
		ReportPieceFallback(read, index, ReadTheStupidText.Application.Usage.FallbackReason.ClipMissing);
		AudioPackLog.Write($"silent piece shown (sentence {StorySentences.IndexAt(_sentences, piece.SourceStart)})");
		NoteFirstSound("silent piece", read.ReadId);
		bool timerRuns = _silent.Begin(); // before ClearPending, so the state is Playing (never Loading) the moment the pending marker goes
		RunOnMain(() => MarkPiece(read, piece));
		RunOnMain(() => PieceAudio?.Invoke(false));
		ClearPending();
		if (timerRuns)
		{
			StartBeatTimer();
		}
	}

	// Starts the whole beat for the step now shown (the one place a timer is made).
	private void StartBeatTimer()
	{
		if (_stepRead is not { } read)
		{
			return;
		}

		StopBeat();
		CancellationTokenSource beat = CancellationTokenSource.CreateLinkedTokenSource(read.Cancellation);
		_beat = beat;
		_ = EndBeatAsync(read, read.Index, beat);
	}

	private async Task EndBeatAsync(StepRead read, int index, CancellationTokenSource beat)
	{
		try
		{
			await Task.Delay(PlaybackSteps.BeatMilliseconds, beat.Token);
		}
		catch (OperationCanceledException)
		{
			// no-fallback: the beat was cancelled by a pause, a stop or a newer read
			return;
		}

		MainThread.BeginInvokeOnMainThread(() => ApplyBeatEnded(read, index, beat));
	}

	private void ApplyBeatEnded(StepRead read, int index, CancellationTokenSource beat)
	{
		if (!ReferenceEquals(_beat, beat) || read != _stepRead || read.Index != index)
		{
			return; // paused, stopped or replaced meanwhile
		}

		Interlocked.CompareExchange(ref _beat, null, beat);
		beat.Dispose();
		_silent.End();
		AdvanceStep(read.ReadId);
	}

	// A clip that played to its end advances the read. One that failed while playing is shown for a beat instead: at once when playing, or when the user resumes when they had paused.
	private void ApplyPremadeEnded(StepRead read, int index, bool failed)
	{
		if (read != _stepRead || read.Index != index || _recorded == RecordedPlay.None)
		{
			return; // stopped or replaced meanwhile, or this step's end was already taken
		}

		if (!failed)
		{
			AudioPackLog.Write($"clip {index} ended, next"); // Slice 270: a crash report's log shows how far a book got
			AdvanceStep(read.ReadId);
			return;
		}

		AudioPackLog.Write("premade clip failed while playing, the piece is shown for a beat");
		ReportPieceFallback(read, index, ReadTheStupidText.Application.Usage.FallbackReason.ClipUnplayable);
		read.Steps[index] = PlaybackSteps.AsSilentPiece(read.Steps[index]);
		bool wasPaused = _recorded == RecordedPlay.Paused;
		_player.Stop();
		_recorded = RecordedPlay.None;
		if (wasPaused)
		{
			_restartStep = read;
			StateChanged?.Invoke(this, EventArgs.Empty);
			return;
		}

		RunStep(read);
	}

	private void SkipClip(StepRead read, int index)
	{
		ClearPending();
		MainThread.BeginInvokeOnMainThread(() => AdvanceIfCurrent(read, index));
	}

	// A clip that fails to play is deleted from the cache, so the next read copies a fresh one; the slot is skipped silently like a clip that ends.
	private void ApplyClipEnded(StepRead read, int index, AnimalKind clip, bool failed)
	{
		if (read != _stepRead || read.Index != index || _recorded == RecordedPlay.None)
		{
			return; // stopped or replaced meanwhile, or this step's end was already taken
		}

		if (failed)
		{
			AnimalClipFiles.Discard(clip);
		}

		AdvanceStep(read.ReadId);
	}

	private void AdvanceIfCurrent(StepRead read, int index)
	{
		if (read == _stepRead && read.Index == index)
		{
			AdvanceStep(read.ReadId);
		}
	}

	// The read has played everything: it goes on where a recorded page begins, or the story is finished.
	private void EndRead(int id)
	{
		_activeReadId = 0;
		if (_continueRead is { } cut && cut.ReadId == id)
		{
			_continueRead = null;
			_ = ContinueAtAsync(cut.Sentence);
			return;
		}

		Finished = true;
		AppTrace.Reading(TraceLines.ReadingEvent.Stopped, TraceLines.ReadingReason.Finished);
		RaiseStopped(ReadStopReason.Finished);
		StateChanged?.Invoke(this, EventArgs.Empty); // the page counts a finished book from this
	}

	// A generated read ends where the next page with a parent's recording begins, and goes on there with the recording (Slice 111).
	private IReadOnlyList<VoicedSegment> StopBeforeRecordedPage(Story story, IReadOnlyList<VoicedSegment> segments, int fromPosition, int id)
	{
		if (NextClipAfter(story, fromPosition) is not { } next)
		{
			return segments;
		}

		int boundary = _sentences[next.First].Start;
		_continueRead = (id, next.First);
		return segments.Where(s => s.SourceEnd <= boundary).ToList(); // a segment running over the break is left to the recording, so its words are not heard twice
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
			RaiseStopped(ReadStopReason.Failed);
			Failed?.Invoke(this, EventArgs.Empty);
		}
	}

	private void ClearPendingIfCurrent(int id)
	{
		if (id == _pendingReadId)
		{
			ClearPending();
		}
	}

	// The first sound of the read being traced (a premade clip or a silent piece); later sounds of the same read and other reads' sounds log nothing.
	// The trace is read through a local copy and cleared only if it is still that copy, so a trace a new read just started is never wiped; readId is the read that raised the sound.
	private void NoteFirstSound(string how, int readId)
	{
		ReadStartTrace? trace = Volatile.Read(ref _startTrace);
		if (trace?.FirstSound(readId, how) == true)
		{
			Interlocked.CompareExchange(ref _startTrace, null, trace);
		}
	}

	private void ClearPending()
	{
		_pendingReadId = 0;
		StateChanged?.Invoke(this, EventArgs.Empty);
	}
}

/// <summary>
/// Slice 259 item 1 (the trace is Debug only, in the test-hook log): how long one read takes from its start to its voiced segments and to its first sound, and whether it started
/// mid-book. A mid-book read of a translated book works out the whole book's pieces again to find its clips (<see cref="StoryVoicing"/>), so a device check reads these lines to see
/// whether the wait passes the 500 ms rule; the page's wait overlay (<c>WaitController</c>) shows only if it does, and logs when it does.
/// </summary>
internal sealed class ReadStartTrace
{
	public const string Tag = "[ReadStart]";
	private readonly ReadStartClock _clock;
	private readonly bool _midBook;

	private ReadStartTrace(int id, bool midBook)
	{
		_clock = new ReadStartClock(id);
		_midBook = midBook;
	}

#if DEBUG
	public static ReadStartTrace? Begin(int id, int position) => new(id, position > 0);
#else
	public static ReadStartTrace? Begin(int id, int position) => null; // Debug only: no Stopwatch or allocation per read in Release
#endif

	public void Voiced(int id, int segments, bool translationSkipped)
	{
		if (_clock.ElapsedFor(id) is { } elapsed)
		{
			Write($"voiced after {elapsed} ms: {segments} segments, {Where}{(translationSkipped ? ", translation skipped" : string.Empty)}");
		}
	}

	/// <summary>Logs the first sound of the traced read; false (nothing logged, trace untouched) for any other read's sound.</summary>
	public bool FirstSound(int id, string how)
	{
		if (_clock.ElapsedFor(id) is not { } elapsed)
		{
			return false;
		}

		Write($"first sound after {elapsed} ms ({how}), {Where}");
		return true;
	}

	private string Where => _midBook ? "mid-book" : "from the start";

	[Conditional("DEBUG")] // the call and its interpolated arguments are not compiled into Release
	public static void Write(string line)
	{
		Debug.WriteLine($"{Tag} {line}");
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"{Tag} {line}");
#endif
	}
}
