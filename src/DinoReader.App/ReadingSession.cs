using System.Diagnostics;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;
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
	// The read as a list of steps (speak a run / play an animal's recording, Slice 255): one read id, the step being taken and whether the next one waits for Resume.
	// A step is a spoken run, an animal's recording, or (Slice 246 C2) a pre-made clip of a store book's piece; a clip that cannot play turns its step into a spoken run in place.
	private sealed class StepRead(int readId, List<PremadeAudioPlan.Step> steps, string spoken, CancellationToken cancellation)
	{
		public int ReadId { get; } = readId;

		public List<PremadeAudioPlan.Step> Steps { get; } = steps;

		public string Spoken { get; } = spoken;

		public CancellationToken Cancellation { get; } = cancellation;

		public int Index { get; set; }
	}

	private StepRead? _stepRead;
	private StepRead? _restartStep; // Slice 246 C2: a pre-made clip failed while the user had paused; its piece is read by the device voice when they resume
	private bool _pendingAdvance; // a step ended while the user had paused: Resume takes the next one
	private (int Id, int Page)? _recordingEndedWhilePaused; // a recorded page ended while the user had paused: Resume goes on from it
	// Completed carries no id, so a speech step's end is accepted only for the run that was seen Playing (AnimalClipRuns.AcceptCompleted): _run counts the speech steps
	// started (and Stop/StartAt), _playingRun is the run last seen Playing, and OnCompleted stamps _run when the signal is raised.
	private int _run;
	private int _playingRun;
	private Func<Story, Task>? _saveCast;
	private IReadOnlyList<SentenceSpan> _sentences = [];
	private int _readSequence;
	private int _pendingReadId;
	private string? _voiceCountedFor; // the book and reading language whose voice was counted: one read_voice count per book in this session, not one per mid-book start
	private int _returnedReadId;

	// The read whose chunks and completion count: set when a read starts, cleared when it is
	// stopped, fails or completes. Chunk events carry no id, so they are tagged with this when raised
	// and dropped if the read they belong to is no longer the active one.
	private int _activeReadId;

	// Slice 250: true between a counted read_started and its read_stopped, so every start gets exactly one stop.
	private bool _countedOpen;
	private int _readOrigin;

	// Slice 259 item 1 (Debug trace): the time from a read's start to its first sound, once per read.
	private ReadStartTrace? _startTrace;

	// Cancelled when the read is stopped or superseded, so a translation still running for it is abandoned.
	private CancellationTokenSource? _readCancellation;

	private readonly ReadTheStupidText.Application.Usage.IFallbackSink _fallbacks; // Slice 273 part B

	public ReadingSession(ISpeechReader reader, StoryVoicing voicing, IRecordingPlayer player, IStoryLibrary library, ISettingsStore settings, IVoiceModelService model, ReadTheStupidText.Application.Usage.IFallbackSink? fallbacks = null)
	{
		_fallbacks = fallbacks ?? ReadTheStupidText.Application.Usage.NoFallbackSink.Instance;
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
			// no-fallback: a warm-up only; the read resolves and builds the pack itself
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

	/// <summary>Slice 250: a read started, paused, resumed or stopped (with the closed reason for a stop; null for the other verbs). The page turns each into a usage count.</summary>
	public event Action<ReadingEventKind, ReadStopReason?>? Playback;

	/// <summary>Slice 267: this session's read really started; the story and the voice detail (<c>premade</c>, or <c>device:&lt;reason&gt;</c>). Raised on the session itself, so only the page that owns it counts the read.</summary>
	public event Action<Story, ReadVoiceUse>? VoiceUsed;

	public event EventHandler? Failed;

	public SessionState State
	{
		get
		{
			if (_recorded != RecordedPlay.None)
			{
				return _recorded == RecordedPlay.Playing ? SessionState.Playing : SessionState.Paused;
			}

			if (_restartStep is not null)
			{
				return SessionState.Paused;
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
		AppTrace.Reading(TraceLines.ReadingEvent.Detached, TraceLines.ReadingReason.PageLeft); // Slice 243 item 5
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

		if (_reader.State == PlaybackState.Playing)
		{
			AppTrace.Reading(TraceLines.ReadingEvent.Paused, reason);
			RaiseIf(counted, ReadingEventKind.Paused);
			_reader.Pause();
		}
	}

	/// <param name="counted">False for a resume of a pause the app made itself (the language menu), which is not a count (Slice 250).</param>
	public void Resume(bool counted = true)
	{
		if (_restartStep is { } restart)
		{
			_restartStep = null;
			AppTrace.Reading(TraceLines.ReadingEvent.Resumed, TraceLines.ReadingReason.User);
			RaiseIf(counted, ReadingEventKind.Resumed);
			_ = RunStepAsync(restart);
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
			if (_recordingEndedWhilePaused is { } ended)
			{
				_recordingEndedWhilePaused = null;
				ApplyRecordingEnded(ended.Id, ended.Page); // the clip had already ended: go on instead of resuming a finished player
				return;
			}

			_player.Resume();
			StateChanged?.Invoke(this, EventArgs.Empty);
			return;
		}

		if (_reader.State == PlaybackState.Paused)
		{
			AppTrace.Reading(TraceLines.ReadingEvent.Resumed, TraceLines.ReadingReason.User);
			RaiseIf(counted, ReadingEventKind.Resumed);
			_reader.Resume();
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
		_reader.Stop();
		_activeReadId = 0;
		NextRun();
		Stalled = false;
		ClearPending();
	}

	/// <summary>Reads from the start of <paramref name="sentence"/> on, in each character's voice. A start asked for from outside counts as a started read (Slice 250); the read's own continuations do not.</summary>
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

		_reader.Stop();
		StopRecording();
		AppTrace.Reading(TraceLines.ReadingEvent.Started);
		CancellationToken cancellation = BeginReadCancellation();
		Finished = false;
		MoveTo(sentence);
		int id = ++_readSequence;
		_pendingReadId = id;
		NextRun();
		Volatile.Write(ref _activeReadId, id);
		Volatile.Write(ref _startTrace, ReadStartTrace.Begin(id, _sentences[sentence].Start));
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
		_stepRead = null;
		_restartStep = null;
		_pendingAdvance = false;
		_recordingEndedWhilePaused = null; // a stale flag must not fire on a later Resume
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
		bool recorded = PlaysRecording(story, page);
		string? path = recorded ? _library.TryResolveFile(story.Id, StoryRecordings.FileNameOf(page)) : null;
		if (path is null || !File.Exists(path))
		{
			if (recorded)
			{
				ReportRecordingUnplayable(); // Slice 273 part B: the page was to play a recording but its file is gone
			}

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
			ReportRecordingUnplayable();
			return false;
		}

		_recorded = RecordedPlay.Playing;
		_recordingEndedWhilePaused = null;
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

		if (_recorded == RecordedPlay.Paused)
		{
			_recordingEndedWhilePaused = (id, page); // Slice 270: Pause landed after the clip ended; the read holds here and Resume goes on
			return;
		}

		_recorded = RecordedPlay.None;
		int next = _pageSentences[page].Last + 1;
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

	// The recording cannot be played (a corrupt or empty file): this page is read in the generated voice instead, and the read goes on from its start.
	private void ApplyRecordingFailed(int id, int page)
	{
		if (id != _activeReadId || _recorded == RecordedPlay.None)
		{
			return;
		}

		_unplayable.Add(page);
		ReportRecordingUnplayable();
		if (_recorded == RecordedPlay.Paused)
		{
			_player.Stop();
			_recorded = RecordedPlay.None;
			StateChanged?.Invoke(this, EventArgs.Empty);
			return; // paused by the user: the page reads in the generated voice when they resume playing, not on its own
		}

		_ = ContinueAtAsync(_pageSentences[page].First);
	}

	// Slice 273 part B: a recording that cannot be played falls back to the generated voice.
	private void ReportRecordingUnplayable() =>
		_fallbacks.Report(ReadTheStupidText.Application.Usage.FallbackArea.Speech, ReadTheStupidText.Application.Usage.FallbackReason.RecordingUnplayable, _reading);

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
			// no-fallback: the read was superseded or stopped
			ClearPendingIfCurrent(id);
			return false;
		}
		catch (Exception)
		{
			// no-fallback: the read fails and the person is told (FailRead); no other voice is tried
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
		var why = new StoryVoicing.VoiceWhy();
		IReadOnlyList<VoicedSegment> segments = await _voicing.VoiceAsync(story, reading, fromPosition, cancellation, () => untranslated = true, why);
		_startTrace?.Voiced(id, segments.Count, untranslated);
		if (id != _pendingReadId)
		{
			return false;
		}

		segments = StopBeforeRecordedPage(story, segments, fromPosition, id);
		if (segments.Count == 0 && _continueRead is { } cut && cut.ReadId == id)
		{
			_continueRead = null;
			_ = ContinueAtAsync(cut.Sentence);
			return false;
		}

		CountVoiceOncePerBook(story, reading, segments, why); // Slice 273: counted on the pieces this read really speaks, after the recorded page cut off the rest

		_readOrigin = segments.Count > 0 ? segments[0].SourceStart : fromPosition;
		await AnimalClipFiles.EnsureAsync(AnimalClipRuns.KindsIn(segments));
		cancellation.ThrowIfCancellationRequested(); // a Stop, Back or another sentence during the copy: this read never speaks or plays a clip
		if (id != _pendingReadId)
		{
			return false;
		}

		await SpeakRunsAsync(segments, untranslated ? UiLanguage.Resolve(story.Language) : _voicing.ReadingLanguage(story, reading), id, cancellation); // a translation that was skipped leaves the story's own voices
		return true;
	}

	// Slice 267: the voice a book is read with is counted once per book and reading language in this session, at the moment its read really starts.
	private void CountVoiceOncePerBook(Story story, string reading, IReadOnlyList<VoicedSegment> segments, StoryVoicing.VoiceWhy why)
	{
		if (!StoryVoicing.SpeaksAnyPiece(segments))
		{
			return; // nothing is spoken, so nothing is counted and the once-per-book key stays free for a read that speaks
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

	// The animals' recordings (Slice 255): the speech engine is only ever given the segments up to the next animal-sound slot. When that run has been spoken (ApplyCompleted)
	// the animal's recording plays, and when it ends the segments after the slot go on, so the engine never voices a sound word and Pause, Resume, Back and Stop work as for a recorded page.
	private async Task SpeakRunsAsync(IReadOnlyList<VoicedSegment> segments, string spoken, int id, CancellationToken cancellation)
	{
		cancellation.ThrowIfCancellationRequested();
		var read = new StepRead(id, PremadeAudioPlan.Plan(segments), spoken, cancellation);
		_stepRead = read;
		_pendingAdvance = false;
		await StartStepAsync(read);
	}

	// Every end signal of a step (the run was spoken, the clip ended or failed, the clip file is missing) comes here with the read's id. A stale or cancelled read is dropped,
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

		_ = RunStepAsync(read);
	}

	private bool IsPaused => _recorded == RecordedPlay.Paused || _restartStep is not null || _reader.State == PlaybackState.Paused;

	// Resume after a step ended while paused: the pause is over and the step that was waiting starts.
	private void ResumePendingAdvance()
	{
		_pendingAdvance = false;
		NextRun(); // the Stop below raises a Completed that belongs to the run that had ended
		if (_reader.State == PlaybackState.Paused)
		{
			_reader.Stop(); // its run had already ended
		}

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

	// A step that follows the first one: started from an end signal, so it owns its own failure handling and first-audio wait.
	private async Task RunStepAsync(StepRead read)
	{
		int id = read.ReadId;
		try
		{
			await StartStepAsync(read);
			await WaitForFirstAudioAsync(id);
		}
		catch (OperationCanceledException)
		{
			// no-fallback: the read was superseded or stopped
			ClearPendingIfCurrent(id);
		}
		catch (Exception)
		{
			// no-fallback: the read fails and the person is told (FailRead); no other voice is tried
			FailRead(id);
		}
	}

	private async Task StartStepAsync(StepRead read)
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

			step = read.Steps[read.Index] = PremadeAudioPlan.AsSpoken(step); // no usable clip: this piece is read by the device voice
		}

		if (read.Index > 0)
		{
			_pendingReadId = read.ReadId; // the run after a recording loads like the first one
			StateChanged?.Invoke(this, EventArgs.Empty);
		}

		NextRun();
		if (step.Run.Count > 0)
		{
			_readOrigin = step.Run[0].SourceStart;
		}

		await SpeakAsync(step.Run, read.Spoken);
	}

	// A new speech run (or a stop): any Completed raised before this point belongs to an earlier run and must not advance this one.
	private void NextRun() => Interlocked.Increment(ref _run);

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
		ClearPending();
	}

	// Slice 246 C2: plays the pre-made clip of a store book's piece at the reading speed, with the piece's sentence marked as it starts (the device voice marks it from its chunk event).
	// False (nothing started) when the file is gone or the player will not open it; the caller then reads the piece with the device voice. The clip is "recorded" for the session's
	// state, Pause, Resume and Stop, exactly as an animal's recording is, and its end advances the read like a spoken run's Completed does.
	private bool TryPlayPremade(StepRead read, VoicedSegment segment)
	{
		string? path = PremadeClipMarker.PathOf(segment);
		int index = read.Index;
		if (path is null || !File.Exists(path))
		{
			AudioPackLog.Write("premade clip has no file, device voice for this piece");
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
			AudioPackLog.Write($"premade clip could not play ({ex.GetType().Name}), device voice for this piece");
			_fallbacks.Report(ReadTheStupidText.Application.Usage.FallbackArea.PremadeAudio, ReadTheStupidText.Application.Usage.FallbackReason.ClipUnplayable, _reading); // Slice 273 part D
			return false;
		}

		AudioPackLog.Write($"premade clip plays (sentence {StorySentences.IndexAt(_sentences, segment.SourceStart)})");
		NoteFirstSound("premade clip", read.ReadId);
		RunOnMain(() => MarkPremade(read, segment));
		ClearPending();
		return true;
	}

	private void MarkPremade(StepRead read, VoicedSegment segment)
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

	// A clip that played to its end advances the read. One that failed while playing is read by the device voice instead, from its start: at once when playing, or when the user
	// resumes when they had paused.
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

		AudioPackLog.Write("premade clip failed while playing, device voice for this piece");
		read.Steps[index] = PremadeAudioPlan.AsSpoken(read.Steps[index]);
		bool wasPaused = _recorded == RecordedPlay.Paused;
		_player.Stop();
		_recorded = RecordedPlay.None;
		if (wasPaused)
		{
			_restartStep = read;
			StateChanged?.Invoke(this, EventArgs.Empty);
			return;
		}

		_ = RunStepAsync(read);
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

	// The read has spoken everything: it goes on where a recorded page begins, or the story is finished.
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
			// no-fallback: the cast stays in memory, so what is heard is unchanged; only the save is lost
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
			RaiseStopped(ReadStopReason.Failed);
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

	// The first sound of the read being traced (a premade clip or the device voice); later sounds of the same read and other reads' sounds log nothing.
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
		_returnedReadId = 0;
		StateChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnReaderStateChanged(object? sender, PlaybackState state)
	{
		int readId = Volatile.Read(ref _activeReadId); // the read this state belongs to, taken when the reader raised it: by the time the main thread runs it a newer read may be active
		MainThread.BeginInvokeOnMainThread(() => ApplyReaderState(state, readId));
	}

	private void ApplyReaderState(PlaybackState state, int readId)
	{
		bool idleAfterReturn = state == PlaybackState.Idle
			&& _pendingReadId != 0 && _pendingReadId == _returnedReadId;
		if (state == PlaybackState.Playing)
		{
			_playingRun = _run;
			NoteFirstSound("device voice", readId);
		}

		if (state == PlaybackState.Playing || idleAfterReturn)
		{
			if (idleAfterReturn)
			{
				RaiseStopped(ReadStopReason.Other); // Slice 250: a read that ended without ever playing still closes its start, so the next start is not charged a stale "restarted" stop
			}

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
		AppTrace.Reading(next ? TraceLines.ReadingEvent.Stalled : TraceLines.ReadingEvent.Unstalled);
		StateChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnCompleted(object? sender, EventArgs e)
	{
		int id = Volatile.Read(ref _activeReadId);
		int run = Volatile.Read(ref _run);
		MainThread.BeginInvokeOnMainThread(() => ApplyCompleted(id, run));
	}

	// Only the active read's current run can end a step, and only once that run was seen Playing (as on origin/main): a stopped or replaced run's late Completed
	// (the event carries no id) is stamped with an older run, or arrives before the new run plays, and is ignored. A duplicate is ignored too.
	private void ApplyCompleted(int id, int stampedRun)
	{
		if (_recorded != RecordedPlay.None || !AnimalClipRuns.AcceptCompleted(id, _activeReadId, stampedRun, _run, _playingRun))
		{
			return;
		}

		_playingRun = 0; // this run's end is taken
		AdvanceStep(id); // the run has been spoken: the next step starts, or the read ends
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
