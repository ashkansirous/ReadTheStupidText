using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>The keys the Record screen answers on the Windows head (mock frame 184): R records and stops, Space plays, S skips, Right goes on.</summary>
internal interface IRecordKeys
{
	/// <summary>Handles <paramref name="key"/> (one of R, S, Space, Right); true when it did something.</summary>
	bool HandleRecordKey(RecordKey key);
}

/// <summary>The keys <see cref="IRecordKeys"/> is given.</summary>
internal enum RecordKey
{
	Record,
	Skip,
	Play,
	Next,
	Leave,
}

/// <summary>
/// The grown-up Record screen (Slice 111, Decision 115; redrawn by Slice 279 from the Batch 48 mock, frames 180 to 184 and 190 to 191): one piece at a time in the reading page's picture-and-text layout, with
/// only Skip, Record and Next (Re-record, Play and Next once recorded). A new take is kept in a temp file and replaces the kept clip only on Next, Finish, Done or Back. The microphone is asked for only
/// here, the first time a recording starts, and a recording never outlives the screen: leaving stops it and keeps what was said.
/// </summary>
public sealed class RecordPage : ContentPage, IGrownUpPage, IRecordKeys
{
	private static readonly TimeSpan ClockTick = TimeSpan.FromMilliseconds(250);
	private static readonly TimeSpan UndoWindow = TimeSpan.FromSeconds(4);
	private const double ColumnMaxWidth = RecordCard.ColumnMaxWidth;
	private const double ButtonRowSpace = 14;
	private const double SwipeThreshold = 40;
	private const string HeaderSeparator = " · ";

	private readonly IStoryLibrary _library;
	private readonly IMicRecorder _recorder;
	private readonly IRecordingPlayer _player;
	private readonly UsageCounter _usage;
	private readonly BusyGuard _busy = new();
	private readonly WaitOverlay _waitOverlay = DinoWait.OverPage();
	private readonly WaitController _wait;
	private readonly GrownUpHeader _header = new("RecordDone");
	private readonly Grid _column = new() { RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)], MaximumWidthRequest = ColumnMaxWidth, HorizontalOptions = LayoutOptions.Center };
	private readonly Grid _toastHost = new() { VerticalOptions = LayoutOptions.End, InputTransparent = false };
	private Grid _waveHost = new();
	private Size _windowSize;
	private bool _playOnAppear;

	private Label _clock = RecordViews.ClockLabel();
	private RecordSetup? _setup;
	private List<RecordPiece> _pieces = [];
	private Story? _story;
	private int _index;
	private RecorderState _state;
	private Task _stopTask = Task.CompletedTask;
	private ActiveTake? _active;
	private bool _visible;
	private bool _left;
	private Task? _finishUp;
	private Task? _leave; // the one leave of this page (LeaveOnceAsync): every way out shares it
	private int _alerts; // alerts showing now: the Windows record keys stay quiet behind one (Esc would pop the page behind the dialog)
	private bool _appeared; // the page has been on screen since Load: a page that was never pushed has nothing to finish up
	private bool _popped; //the page has been popped (by us, or by the system's own back); nothing pops again
	private Task? _settle; // the removal commit in flight or done, so a second settle waits for it
	private long _recordingStart;
	private IDispatcherTimer? _timer;
	private Take? _take;
	private Playing _playing = Playing.None;
	private double _playedSeconds;
	private Undo? _undo;
	private string? _pictureKey;
	private string? _picturePath;

	private enum Playing
	{
		None,
		Clip,
		Take,
		OldClip,
	}

	/// <summary>The one state of the recorder (Slice 279, design 2): Record, Skip, Next, a swipe and "Use Dino's voice" act only in Idle; Stop only in Recording; everything waits while Stopping, which ends
	/// only after the recorder has stopped and the take is kept. Starting is the wait for the microphone to open.</summary>
	private enum RecorderState
	{
		Idle,
		Starting,
		Recording,
		Stopping,
	}

	/// <summary>A recording in progress, bound to the piece shown when Record was pressed: the page's index may move on, the take stays that piece's.</summary>
	private sealed record ActiveTake(RecordPiece Piece, string Path);

	/// <summary>A new take of <see cref="Piece"/>, kept in a temp file until Next, Finish, Done or Back commits it over the clips it replaces (Slice 279).</summary>
	private sealed record Take(RecordPiece Piece, string Path, TimeSpan Length);

	/// <summary>What one committed take changed in the story: the story as saved, the files of the clips it replaced, and whether it was the first recording of those sentences.</summary>
	private sealed record Committed(Story Story, IReadOnlyList<string> ReplacedFiles, bool First);

	/// <summary>A clip the grown-up gave back to Dino's voice: the model no longer has it, the file goes when the 4 second window ends (Undo puts the entry back).</summary>
	private sealed record Undo(StoryClipKey Key, PageRecording Recording, RecordPiece Piece, int Index, CancellationTokenSource Window);

	public RecordPage(IStoryLibrary library, IMicRecorder recorder, IRecordingPlayer player, UsageCounter usage)
	{
		_usage = usage;
		_library = library;
		_recorder = recorder;
		_player = player;
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		_wait = new WaitController(_waitOverlay);
		_header.BackRequested += async (_, _) => await RunHandlerAsync(() => LeaveOnceAsync(toCaller: false));
		_header.DoneRequested += async (_, _) => await RunHandlerAsync(() => LeaveOnceAsync(toCaller: true)); // leaving stops and keeps a recording in progress, as Back does
		var root = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
		root.Add(_header, 0, 0);
		root.Add(_column, 0, 1);
		_toastHost.Margin = new Thickness(0, 0, 0, 0);
		Content = new Grid { Children = { root, _toastHost, _waitOverlay } };
	}

	/// <summary>Set by the intro wizard when it pushes the pages: the header button then reads "Continue" (Slice 229).</summary>
	public bool LaunchedFromIntro { get; set; }

	/// <summary>Opens the screen on the pieces of <paramref name="setup"/> at <paramref name="start"/>; a take file a killed app left in the cache is deleted first.</summary>
	internal void Load(RecordSetup setup, int start)
	{
		_setup = setup;
		_story = setup.Story;
		_pieces = [.. setup.Pieces];
		_index = Math.Clamp(start, 0, Math.Max(0, _pieces.Count - 1));
		_left = false;
		_finishUp = null;
		_appeared = false;
		_leave = null;
		_popped = false;
		_take = null;
		_header.Continue = LaunchedFromIntro;
		_header.Title = setup.Story.Title;
		DeleteStrayTakes();
		Log($"[Record] open mode={setup.Choice.Mode} pieces={_pieces.Count} start={_index} listening={setup.Listening}");
		_playOnAppear = setup.Listening; // each recording plays by itself on open (frame 190), once the screen is up
		Render();
	}

	private static void Log(string line)
	{
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write(line);
#endif
#if WINDOWS
		WindowTrace.Write($"Dino Record: {line}"); // the Release build on the VM shows no Debug.WriteLine; the VM check reads this back
#endif
		Debug.WriteLine($"Dino Reader {line}");
	}

	// ---- the page's life ----

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_visible = true;
		_appeared = true;
		if (Window is { } window)
		{
			window.Stopped += OnWindowStopped;
		}

		if (_playOnAppear)
		{
			_playOnAppear = false;
			PlayKept();
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_visible = false;
		if (Window is { } window)
		{
			window.Stopped -= OnWindowStopped;
		}

		StopPlayback();
		if (Navigation.NavigationStack.Contains(this))
		{
			// Still on the stack: a page pushed over it, or the app going to the background. Not a leave; a running recording is stopped and kept, nothing more.
			KeepRunningRecording();
			return;
		}

		_popped = true; // really gone (our Back, a swipe, the system's back): a later leave must not pop the page beneath
		_ = FinishUpAsync(); // a swipe back or a system back leaves the same way Back does: the take stays
	}

	private void KeepRunningRecording()
	{
		if (Recording)
		{
			_ = StopRecordingAsync();
		}
	}

	protected override bool OnBackButtonPressed()
	{
		_ = LeaveOnceAsync(toCaller: false); // never throws; the one leave, queued behind a running action
		return true;
	}

	// The app going to the background does not disappear this page, so a running recording is stopped here: the microphone is never open out of sight. What was said is kept as the take.
	private void OnWindowStopped(object? sender, EventArgs e)
	{
		StopPlayback();
		KeepRunningRecording();
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		if (width > 0 && height > 0 && _setup is not null && (width != _windowSize.Width || height != _windowSize.Height))
		{
			_windowSize = new Size(width, height);
			Render();
		}
	}

	// ---- leaving ----

	// Stops a running recording (keeping it), puts a pending take in place, settles the Undo window, then pops: Back goes to the intro page, Done and Finish go on to where the intro was opened from.
	// The one owner of leaving (Back, Done, Finish, Esc, hardware back, the empty page closing): the first call starts it, every later call gets the same task. It waits for a running action instead of being dropped, and never throws.
	private Task LeaveOnceAsync(bool toCaller) => _leave ??= LeaveQueuedAsync(toCaller);

	private async Task LeaveQueuedAsync(bool toCaller)
	{
		await Task.Yield(); // the caller has stored this task in _leave before the code below can finish and clear it
		try
		{
			await _busy.RunWhenFreeAsync(() => LeaveCoreAsync(toCaller));
		}
		catch (Exception ex)
		{
			// no-fallback: leaving failed after the take was kept or the old clip stayed; the page stays (_popped is still false) and the next Back tries again
			Debug.WriteLine($"Dino Reader record page could not leave: {ex}");
		}
		finally
		{
			_leave = null; // a finished or failed leave is never cached: a later Back leaves again (or finds the page already popped)
		}
	}

	private async Task LeaveCoreAsync(bool toCaller)
	{
		await FinishUpAsync();
		if (_popped || !Navigation.NavigationStack.Contains(this))
		{
			return; // already gone (the system's own back): never pop the page beneath
		}

		await Navigation.PopAsync();
		_popped = true; // only after the pop returned: a pop that threw leaves the page up and the next Back retries
		if (toCaller && Navigation.NavigationStack.LastOrDefault() is RecordIntroPage)
		{
			await Navigation.PopAsync();
		}
	}

	/// <summary>Completes when the screen has put a pending take in place and settled the Undo window after it was left; the intro waits for it before it reads the book again (Slice 279).</summary>
	// Finishing up runs only when the page is really gone from the navigation stack (the same test as OnDisappearing), so a page shown over it or a cancelled swipe back changes nothing;
	// if the intro asks before OnDisappearing has run, the finish-up starts here.
	// A page that never appeared (the intro asked before the push finished, or the push never happened) has nothing to finish and must stay usable once it is pushed.
	internal Task FinishedAsync => _finishUp ?? (_appeared && (_popped || !Navigation.NavigationStack.Contains(this)) ? FinishUpAsync() : Task.CompletedTask);

	private async Task AlertAsync(string title, string message, string ok)
	{
		_alerts++;
		try
		{
			await DisplayAlertAsync(title, message, ok);
		}
		finally
		{
			_alerts--;
		}
	}

	// Every caller gets the one run, so a second caller (the swipe back after Back) waits for the first one's save instead of returning at once.
	private Task FinishUpAsync() => _finishUp ??= FinishUpCoreAsync();

	// Never faulted (the intro and every leave await it): a step that fails is logged, the old clip stays, and the page still leaves.
	private async Task FinishUpCoreAsync()
	{
		_left = true;
		StopPlayback();
		try
		{
			await StopRecordingAsync(); // a running recording is stopped and kept as its piece's take; a stop already under way is waited for
			await CommitTakeAsync();
			await SettleUndoAsync();
		}
		catch (Exception ex)
		{
			// no-fallback: saving on the way out failed (disk full, a locked file); the earlier clip stays and the page still leaves
			Debug.WriteLine($"Dino Reader record page could not finish up: {ex}");
		}
	}

	// ---- drawing ----

	private RecordPiece? Piece => _index >= 0 && _index < _pieces.Count ? _pieces[_index] : null;

	private bool Listening => _setup?.Listening == true;

	private StoryClipKey? KeptClip() => Piece is { } piece && _story is { } story ? RecordSplitWalk.ClipOf(story, piece) : null;

	private bool Idle => _state == RecorderState.Idle;

	private bool Recording => _state == RecorderState.Recording;

	private RecordStep Step => _state is RecorderState.Recording or RecorderState.Stopping ? RecordStep.Recording : _take is not null || KeptClip() is not null ? RecordStep.Recorded : RecordStep.Ready;

	private void Render()
	{
		if (_setup is not { } setup || _story is not { } story || Piece is not { } piece)
		{
			return;
		}

		ShowHeaderLine(setup, story, piece);
		_column.Children.Clear();
		var stage = RecordViews.Stage(PicturePathOf(story, piece), WordsCardFor(setup, piece), RecordCard.MaxCardHeight(Height > 0 ? Height : 667));
		AttachSwipe(stage);
		_column.Add(stage, 0, 0);
		_column.Add(Bottom(), 0, 1);
		FocusRecordButton();
	}

	// Frame 184 (Windows head): the keyboard lands on the Record button, with a focus ring, whenever a piece waiting to be recorded opens. Nothing happens elsewhere.
	private void FocusRecordButton()
	{
#if WINDOWS
		if (Step == RecordStep.Ready && !Listening && FindById(_column, RecordViews.RecordButtonId) is Border button)
		{
			RecordFocus.Land(button);
		}
#endif
	}

#if WINDOWS
	private static View? FindById(IVisualTreeElement parent, string automationId)
	{
		foreach (IVisualTreeElement child in parent.GetVisualChildren())
		{
			if (child is View view && view.AutomationId == automationId)
			{
				return view;
			}

			if (FindById(child, automationId) is { } found)
			{
				return found;
			}
		}

		return null;
	}
#endif

	// Frame 194: with more than one story language the small line starts with the picked language's round flag and its name (in the app language): "Farsi · Line 4 of 41".
	private void ShowHeaderLine(RecordSetup setup, Story story, RecordPiece piece)
	{
		string line = HeaderLine(setup, story, piece);
		bool named = StoryLanguages.Current().HasChoice;
		_header.SetSubtitleFlag(named ? setup.Language : null);
		_header.Subtitle = named ? $"{StoryLanguages.NameInAppLanguage(setup.Language)}{HeaderSeparator}{line}" : line;
	}

	private string HeaderLine(RecordSetup setup, Story story, RecordPiece piece)
	{
		if (setup.Listening)
		{
			return AppResources.Format(AppResources.Record_YourRecordings, _index + 1, _pieces.Count);
		}

		if (setup.Choice.Mode == RecordMode.PerCharacter)
		{
			return AppResources.Format(AppResources.Record_LineOf, _index + 1, _pieces.Count);
		}

		int parts = RecordSplit.PartsOf(_pieces, piece.Key.Page);
		return parts <= 1
			? AppResources.Format(AppResources.Record_PageOf, piece.Key.Page + 1, story.Pages.Count)
			: AppResources.Format(AppResources.Record_PagePart, piece.Key.Page + 1, story.Pages.Count, RecordSplit.PartNumber(_pieces, piece), parts);
	}

	private Border WordsCardFor(RecordSetup setup, RecordPiece piece)
	{
		bool whole = setup.Choice.Mode == RecordMode.WholePage;
		string? says = whole ? null : RecordCard.SaysOf(setup.Book, piece.Speaker);
		RecordChip chip = _take is not null ? RecordChip.NewTake : KeptClip() is not null ? RecordChip.Recorded : RecordChip.None;
		double font = RecordCard.FittedFontSize(whole, Width > 0 ? Width : RecordCardFit.ReferenceWindowWidth, Height > 0 ? Height : RecordCardFit.ReferenceWindowHeight); // the pieces are cut once for the reference card; a smaller card shows them smaller
		bool replacesRest = _take is { } take && StoryRecordings.ReplacesBeyond(_story!, take.Piece.Key);
		var view = new RecordCardView(says, piece.Text, setup.Language, font, chip, Recording, replacesRest);
		return RecordViews.WordsCard(view, PlayOldTakeAsync, KeepOldAsync);
	}

	private string? PicturePathOf(Story story, RecordPiece piece)
	{
		StoryPage page = story.Pages[piece.Key.Page];
		string key = $"{story.Id}/{piece.Key.Page}/{page.ImageFile}";
		if (_pictureKey != key)
		{
			string? picture = page.ImageFile is null ? null : _library.TryResolveFile(story.Id, page.ImageFile);
			_picturePath = picture is not null && File.Exists(picture) ? picture : null;
			_pictureKey = key;
		}

		return _picturePath;
	}

	private View Bottom()
	{
		var stack = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(0, ButtonRowSpace, 0, ButtonRowSpace) };
		if (Listening)
		{
			_waveHost = new Grid(); // a fresh host each time: the old one is still a child of the stack that was cleared
			stack.Add(_waveHost);
			UpdateWave();
		}

		stack.Add(RecordViews.Controls(ButtonsFor()));
		if (Listening && KeptClip() is not null)
		{
			stack.Add(RecordViews.UseDinoLink(UseDinoVoiceAsync));
		}

		return stack;
	}

	private RecordButtons ButtonsFor()
	{
		bool running = _state is RecorderState.Recording or RecorderState.Stopping;
		if (running)
		{
			_clock = RecordViews.ClockLabel();
			_clock.Text = StoryRecordings.Clock(Elapsed());
		}

		return new RecordButtons(
			Step, _playing != Playing.None, IsLast: _index >= _pieces.Count - 1, Listening, DesktopTips.IsWindows, running ? _clock : null,
			SkipAsync, StartRecordingAsync, StopRecordingAsync, RecordAgainAsync, TogglePlaybackAsync, NextAsync);
	}

	private TimeSpan Elapsed() => Stopwatch.GetElapsedTime(_recordingStart);

	// ---- moving on ----

	private Task SkipAsync()
	{
		if (!Idle || Listening)
		{
			return Task.CompletedTask;
		}

		Log($"[Record] skipped piece={_index + 1} key={Piece?.Key}");
		return GoToAsync(_index + 1);
	}

	private Task NextAsync() => _busy.RunAsync(async () =>
	{
		if (!Idle || Step != RecordStep.Recorded)
		{
			return;
		}

		await CommitTakeAsync();
		await SettleUndoAsync();
		if (_index >= _pieces.Count - 1)
		{
			Log("[Record] finish");
			await FinishAsync();
			return;
		}

		await GoToAsync(_index + 1);
	});

	// Runs inside the guard (Next), so it uses the guard-free core; a Back queued meanwhile finds _popped set and does not pop again.
	private Task FinishAsync() => LeaveCoreAsync(toCaller: true); // Finish returns to where the intro was opened from

	private async Task GoToAsync(int index)
	{
		StopPlayback();
		if (index >= _pieces.Count)
		{
			await FinishAsync();
			return;
		}

		_index = Math.Clamp(index, 0, _pieces.Count - 1);
		_take = null;
		Render();
		Log($"[Record] piece={_index + 1} of {_pieces.Count} key={Piece?.Key} speaker={Piece?.Speaker} recorded={KeptClip() is not null}");
		if (Listening)
		{
			PlayKept();
		}
	}

	private Task SwipeBackAsync()
	{
		if (_index <= 0 || _state == RecorderState.Starting)
		{
			return Task.CompletedTask;
		}

		return _busy.RunAsync(async () =>
		{
			await StopRecordingAsync(); // a running recording is kept as its own piece's take first
			await CommitTakeAsync();
			await GoToAsync(_index - 1);
		});
	}

	private void AttachSwipe(View stage)
	{
		if (!Listening)
		{
			return;
		}

		var swipe = new SwipeGestureRecognizer { Direction = SwipeDirection.Right, Threshold = (uint)SwipeThreshold };
		swipe.Swiped += async (_, _) => await RunHandlerAsync(SwipeBackAsync);
		stage.GestureRecognizers.Add(swipe);
	}

	// ---- recording ----

	private async Task RecordAgainAsync()
	{
		StopPlayback();
		await StartRecordingAsync();
	}

	private async Task StartRecordingAsync()
	{
		if (!Idle || _story is null || Piece is not { } piece)
		{
			return;
		}

		_state = RecorderState.Starting;
		try
		{
			StopPlayback();
			await BeginRecordingAsync(piece);
		}
		finally
		{
			if (_state == RecorderState.Starting)
			{
				_state = RecorderState.Idle;
			}
		}
	}

	private async Task BeginRecordingAsync(RecordPiece piece)
	{
		if (!await EnsureMicrophoneAsync() || !_visible || _left)
		{
			return;
		}

		string temp = Path.Combine(FileSystem.CacheDirectory, StoryRecordings.NewTakeFileName());
		if (!await TryStartRecorderAsync(temp))
		{
			return;
		}

		if (!_visible || _left)
		{
			await _recorder.CancelAsync(); // the page was left while the recorder opened: the microphone does not stay open
			TryDelete(temp);
			return;
		}

		_active = new ActiveTake(piece, temp); // the take belongs to this piece from here on, whatever the page shows later
		_state = RecorderState.Recording;
		_recordingStart = Stopwatch.GetTimestamp();
		StartTimer();
		Log($"[Record] recording piece={_index + 1} key={piece.Key}");
		Render();
	}

	private async Task<bool> TryStartRecorderAsync(string temp)
	{
		try
		{
			await _recorder.StartAsync(temp);
			return true;
		}
		catch (UnauthorizedAccessException ex)
		{
			// no-fallback: the person is told the microphone was refused; nothing is recorded
			Debug.WriteLine($"Dino Reader was refused the microphone: {ex.Message}");
			TryDelete(temp);
			await AlertAsync(AppResources.Rec_NoMicTitle, AppResources.Rec_NoMic, AppResources.Common_Ok);
		}
		catch (Exception ex)
		{
			// no-fallback: the person is told the recording failed; nothing is recorded
			Debug.WriteLine($"Dino Reader could not start recording: {ex}");
			TryDelete(temp);
			await AlertAsync(AppResources.Record_Title, AppResources.Rec_Failed, AppResources.Common_Ok);
		}

		return false;
	}

	// The microphone is asked for here and nowhere else (Slice 111); Windows asks when the recorder opens the device, so it has no request of its own.
	private async Task<bool> EnsureMicrophoneAsync()
	{
#if WINDOWS
		return true;
#else
		PermissionStatus status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
		if (status != PermissionStatus.Granted)
		{
			status = await Permissions.RequestAsync<Permissions.Microphone>();
		}

		if (status == PermissionStatus.Granted)
		{
			return true;
		}

		await AlertAsync(AppResources.Rec_NoMicTitle, AppResources.Rec_NoMic, AppResources.Common_Ok);
		return false;
#endif
	}

	private void StartTimer()
	{
		_timer ??= CreateTimer();
		_timer.Start();
	}

	private IDispatcherTimer CreateTimer()
	{
		IDispatcherTimer timer = Dispatcher.CreateTimer();
		timer.Interval = ClockTick;
		timer.Tick += (_, _) => OnTick();
		return timer;
	}

	private void OnTick()
	{
		if (Recording)
		{
			_clock.Text = StoryRecordings.Clock(Elapsed());
		}
		else if (_playing != Playing.None)
		{
			_playedSeconds += ClockTick.TotalSeconds;
			UpdateWave();
		}
	}

	// Stops the recorder. A piece that has no kept clip saves the take at once; a piece that has one keeps the take as "New take" until Next, Finish, Done or Back (or "Keep the old one" drops it).
	// Returns the stop in progress, so a caller that must not go on before the recorder has stopped and the take is kept (leaving, a swipe) waits for it; with nothing recording it returns at once.
	private Task StopRecordingAsync()
	{
		if (_state == RecorderState.Recording)
		{
			_state = RecorderState.Stopping;
			_stopTask = StopAndKeepAsync();
		}

		return _state == RecorderState.Stopping ? _stopTask : Task.CompletedTask;
	}

	// Stopping ends only after StopAsync has returned and the take is kept; the take is the piece Record was pressed on, read from the active take and never from the page's current index.
	private async Task StopAndKeepAsync()
	{
		_timer?.Stop();
		ActiveTake active = _active!;
		_active = null;
		try
		{
			TimeSpan length = await _recorder.StopAsync();
			await KeepTakeAsync(new Take(active.Piece, active.Path, length));
		}
		catch (Exception ex)
		{
			// no-fallback: the person is told the recording failed; nothing is saved
			Debug.WriteLine($"Dino Reader could not save the recording: {ex}");
			if (_take?.Path != active.Path)
			{
				TryDelete(active.Path); // a take that failed to save stays as the New take
			}
			if (_visible)
			{
				await AlertAsync(AppResources.Record_Title, AppResources.Rec_Failed, AppResources.Common_Ok);
			}
		}
		finally
		{
			_state = RecorderState.Idle;
			if (_visible)
			{
				Render();
			}
		}
	}

	// Clips already in the book that share a sentence with a piece: a clip of exactly that piece, or a legacy whole-page one, a part or a line cut differently (Slice 279). They are the "old take"
	// of a new recording and go only when it is committed.
	private IReadOnlyList<StoryClipKey> OldClipsOf(StoryClipKey key) =>
		_story is { } story ? StoryRecordings.Clips(story).Select(c => c.Key).Where(k => k.Overlaps(key)).ToList() : [];

	private async Task KeepTakeAsync(Take take)
	{
		if (OldClipsOf(take.Piece.Key).Count == 0)
		{
			await SaveFirstTakeAsync(take);
			return;
		}

		if (_take is { } earlier)
		{
			TryDelete(earlier.Path); // one take back only: the earlier new take goes, the kept clip stays the "Old take"
		}

		_take = take;
		Log($"[Record] new-take piece={_index + 1} key={take.Piece.Key} seconds={StoryRecordings.WholeSeconds(take.Length)}");
	}

	// A piece with no clip yet saves its take at once; a failed save keeps the take as the New take, so the person can save it again.
	private async Task SaveFirstTakeAsync(Take take)
	{
		try
		{
			await SaveTakeAsync(take);
		}
		catch
		{
			// no-fallback: the take is kept as the New take and the failure is rethrown, so the person is told the recording failed
			_take = take;
			throw;
		}
	}

	private async Task CommitTakeAsync()
	{
		if (_take is not { } take)
		{
			return;
		}

		_take = null;
		try
		{
			await SaveTakeAsync(take);
		}
		catch (Exception ex)
		{
			// no-fallback: the person is told the recording failed; the old clips and their files stay, and the take file stays as the New take (a retry can save it)
			Debug.WriteLine($"Dino Reader could not keep the new take: {ex}");
			_take = take;
			if (_visible)
			{
				await AlertAsync(AppResources.Record_Title, AppResources.Rec_Failed, AppResources.Common_Ok);
			}
		}
	}

	// Slice 143: saving shows the wait overlay if it takes over 500 ms; there is no Cancel, a half-saved recording is worse than waiting.
	private async Task SaveTakeAsync(Take take)
	{
		_wait.Begin(WaitStage.Saving, AppResources.Wait_Saving, canCancel: false);
		try
		{
			await CommitAsync(take);
		}
		finally
		{
			_wait.End();
		}
	}

	// The one place a take becomes a clip (design 2). The take goes into a file with a fresh name (nothing is overwritten), the story is saved once with the new clip added and the replaced clips removed
	// (on top of the latest stored one: the reading page may have saved its own switches meanwhile), and only after that save are the replaced files deleted. If the save fails the new file goes and the old
	// clips and files are untouched.
	private async Task CommitAsync(Take take)
	{
		StoryClipKey key = take.Piece.Key;
		string storyId = _story!.Id;
		string fileName = StoryRecordings.NewFileName(key);
		await PlaceFileAsync(storyId, fileName, take.Path);
		Committed committed = await SaveWithClipAsync(storyId, take, fileName);
		_story = committed.Story; // the story is saved: from here on nothing below can make this recording "fail"
		TryDelete(take.Path);
		await DeleteReplacedFilesAsync(storyId, committed.ReplacedFiles);
		Log($"[Record] saved key={key} file={fileName} seconds={StoryRecordings.WholeSeconds(take.Length)} clips={StoryRecordings.Count(committed.Story)}");
		CountFirstRecording(committed.First, committed.Story, key.Language);
	}

	private async Task PlaceFileAsync(string storyId, string fileName, string temp) =>
		await _library.StoreRecordingFileAsync(storyId, fileName, path =>
		{
			File.Copy(temp, path, overwrite: true); // a copy: the take stays where it is until the story has saved
			return Task.CompletedTask;
		});

	private async Task<Committed> SaveWithClipAsync(string storyId, Take take, string fileName)
	{
		StoryClipKey key = take.Piece.Key;
		try
		{
			Story latest = await _library.GetAsync(storyId) ?? _story!;
			bool first = StoryRecordings.Clips(latest).All(c => !c.Key.Overlaps(key));
			IReadOnlyList<string> replaced = StoryRecordings.ReplacedFiles(latest, key);
			Story updated = StoryRecordings.WithClip(latest, key, new PageRecording(key.Language, StoryRecordings.WholeSeconds(take.Length), fileName));
			await _library.UpdateAsync(updated);
			return new Committed(updated, replaced, first);
		}
		catch
		{
			// no-fallback: the story did not take the clip, so the new file goes (it is the only one this commit made) and the failure is rethrown
			await _library.DeleteFileAsync(storyId, fileName);
			throw;
		}
	}

	// Best effort: the story already names the new clip, so a replaced file that cannot be deleted is only disk space, never a failed recording.
	private async Task DeleteReplacedFilesAsync(string storyId, IReadOnlyList<string> fileNames)
	{
		foreach (string name in fileNames)
		{
			try
			{
				await _library.DeleteFileAsync(storyId, name);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// no-fallback: the recording is saved; a replaced file left on disk is never read
				Debug.WriteLine($"Dino Reader could not delete a replaced recording file: {ex.Message}");
			}
		}
	}

	// Which kind of book and how many of its clips are recorded, as a bucket (Slice 134); a re-recorded piece is not counted again.
	private void CountFirstRecording(bool first, Story updated, string language)
	{
		if (first)
		{
			_usage.Record(UsageEventName.RecordingMade, updated, UsageDetail.PagesBucket(StoryRecordings.Count(updated, language))); // Slice 286: the progress in the language just recorded
		}
	}

	// "Keep the old one" (frame 191): the new take is dropped and the kept clip stays.
	private Task KeepOldAsync()
	{
		if (_take is { } take)
		{
			StopPlayback();
			TryDelete(take.Path);
			_take = null;
			Log($"[Record] kept-old piece={_index + 1} key={Piece?.Key}");
			Render();
		}

		return Task.CompletedTask;
	}

	// ---- playing ----

	private Task TogglePlaybackAsync()
	{
		if (_playing != Playing.None)
		{
			StopPlayback();
			Render();
			return Task.CompletedTask;
		}

		if (_take is { } take)
		{
			PlayFile(take.Path, Playing.Take);
		}
		else
		{
			PlayKept();
		}

		return Task.CompletedTask;
	}

	private Task PlayOldTakeAsync()
	{
		if (_playing != Playing.None)
		{
			StopPlayback();
		}

		if (PathOf(KeptClip() ?? (Piece is { } shown ? OldClipsOf(shown.Key).Select(k => (StoryClipKey?)k).FirstOrDefault() : null)) is { } path)
		{
			PlayFile(path, Playing.OldClip);
		}

		return Task.CompletedTask;
	}

	private void PlayKept()
	{
		if (KeptPath() is { } path)
		{
			PlayFile(path, Playing.Clip);
		}
	}

	private string? KeptPath() => PathOf(KeptClip());

	private string? PathOf(StoryClipKey? clip)
	{
		if (clip is not { } key || _story is null)
		{
			return null;
		}

		string storyId = _story.Id;
		string? path = _library.TryResolveFile(storyId, StoryRecordings.FileOf(_story, key, name => _library.TryResolveFile(storyId, name) is { } p && File.Exists(p)));
		return path is not null && File.Exists(path) ? path : null;
	}

	private void PlayFile(string path, Playing what)
	{
		try
		{
			_player.Play(path, () => MainThread.BeginInvokeOnMainThread(OnPlaybackEnded), () => MainThread.BeginInvokeOnMainThread(OnPlaybackEnded));
		}
		catch (Exception ex)
		{
			// no-fallback: the preview of a recording is not played; no other sound is used
			Debug.WriteLine($"Dino Reader could not play the recording: {ex}");
			return;
		}

		_playing = what;
		_playedSeconds = 0;
		StartTimer();
		Render();
	}

	private void OnPlaybackEnded()
	{
		_playing = Playing.None;
		_timer?.Stop();
		Render();
	}

	private void StopPlayback()
	{
		if (_playing != Playing.None)
		{
			_player.Stop();
			_playing = Playing.None;
			if (!Recording)
			{
				_timer?.Stop();
			}
		}
	}

	private void UpdateWave()
	{
		double seconds = KeptClip() is { } key && _story is not null ? Math.Max(1, StoryRecordings.ClipOf(_story, key)?.Seconds ?? 1) : 1;
		_waveHost.Children.Clear();
		_waveHost.Add(RecordViews.Wave(_playing == Playing.None ? 0 : _playedSeconds / seconds));
	}

	// ---- "Use Dino's voice" (frame 190) ----

	// The piece is only marked as Dino's voice (it leaves the walk, the Undo toast shows): nothing is saved or deleted until the 4 second window is over, so Undo has no IO and cannot race or fail.
	// The removal is committed once, by SettleUndoAsync: when the window ends, or on the next action or leaving the page.
	private Task UseDinoVoiceAsync() => RunHandlerAsync(() => _busy.RunAsync(GiveBackToDinoAsync)); // a second tap while the first is running is dropped

	// An async-void tap must never throw: whatever fails here is logged and the page carries on.
	private static async Task RunHandlerAsync(Func<Task> handler)
	{
		try
		{
			await handler();
		}
		catch (Exception ex)
		{
			// no-fallback: a tap on this page that fails changes nothing the person has recorded
			Debug.WriteLine($"Dino Reader record page action failed: {ex}");
		}
	}

	private async Task GiveBackToDinoAsync()
	{
		if (!Idle || Piece is not { } piece || KeptClip() is not { } key)
		{
			return;
		}

		await SettleUndoAsync(); // a second Use Dino's voice commits the first one first
		StopPlayback();
		PageRecording? recording = _story is { } current ? StoryRecordings.ClipOf(current, key) : null; // the settle above may have saved the story
		if (recording is null)
		{
			return;
		}

		var undo = new Undo(key, recording, piece, _index, new CancellationTokenSource());
		_undo = undo;
		_pieces.RemoveAt(_index);
		DropPendingTake();
		Log($"[Record] use-dino key={key}");
		_toastHost.Children.Clear();
		_toastHost.Add(RecordViews.UndoToast(AppResources.Record_UseDino, UndoAsync));
		_ = EndUndoWindowAsync(undo);
		if (_pieces.Count == 0)
		{
			return; // nothing recorded is left to walk: the screen closes when the window ends
		}

		_index = Math.Min(_index, _pieces.Count - 1);
		Render();
		PlayKept();
	}

	// The piece given back to Dino had a new take waiting: it goes with the clip, file and all.
	private void DropPendingTake()
	{
		if (_take is { } take)
		{
			TryDelete(take.Path);
			_take = null;
		}
	}

	private async Task EndUndoWindowAsync(Undo undo)
	{
		try
		{
			await Task.Delay(UndoWindow, undo.Window.Token);
		}
		catch (OperationCanceledException)
		{
			// no-fallback: the window was ended early (Undo, or leaving the page), which settles it itself
			return;
		}

		await MainThread.InvokeOnMainThreadAsync(() => RunHandlerAsync(() => CommitWhenWindowEndsAsync(undo)));
	}

	// Goes through the page's serialized path and waits for a running action instead of dropping: the removal is never lost.
	private async Task CommitWhenWindowEndsAsync(Undo undo)
	{
		await _busy.RunWhenFreeAsync(async () =>
		{
			if (_undo == undo) // Undo, the next action or leaving may have settled it meanwhile
			{
				await SettleUndoAsync();
			}
		});
		if (_pieces.Count == 0 && !_left)
		{
			await LeaveOnceAsync(toCaller: false); // the one leave: waits for a running action, and does nothing when the page is already leaving
		}
	}

	// Commits the removal: ends the window, saves the story without the clip, then deletes its file. Called when the window runs out, on the next action and on leaving.
	// A second caller while the commit runs waits for the same one, so the intro never reads the book before it is saved.
	private Task SettleUndoAsync()
	{
		if (_undo is not { } undo)
		{
			return _settle ?? Task.CompletedTask;
		}

		_undo = null;
		return _settle = CommitRemovalAsync(undo);
	}

	private async Task CommitRemovalAsync(Undo undo)
	{
		await undo.Window.CancelAsync();
		_toastHost.Children.Clear();
		if (_story is not { } shown)
		{
			return;
		}

		try
		{
			Story latest = await _library.GetAsync(shown.Id) ?? shown;
			Story without = StoryRecordings.WithoutClip(latest, undo.Key);
			await _library.UpdateAsync(without);
			_story = without; // only after the save: a failed save keeps the clip in the story and on disk
		}
		catch (Exception ex)
		{
			// no-fallback: any failure (a disk error, an unreadable story) keeps the clip in the story and its file on disk, nothing is lost; it shows again next time the book is opened.
			// The task must not fault: every later settle, Next and leaving awaits it, and the user could not leave the screen.
			Debug.WriteLine($"Dino Reader could not save the removal of a recording: {ex.Message}");
			Log($"[Record] use-dino-failed key={undo.Key}");
			return;
		}

		await DeleteGivenBackFileAsync(shown.Id, undo, _story!);
	}

	private async Task DeleteGivenBackFileAsync(string storyId, Undo undo, Story saved)
	{
		try
		{
			string name = StoryRecordings.FileOf(undo.Recording, undo.Key);
			if (StoryRecordings.IsReferenced(saved, name))
			{
				return; // another clip (any language) still names this file: it stays
			}

			await _library.DeleteFileAsync(storyId, name);
			Log($"[Record] use-dino-settled key={undo.Key}");
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// no-fallback: the clip is already out of the story; a file that cannot be deleted now is never read
			Debug.WriteLine($"Dino Reader could not delete a recording: {ex.Message}");
		}
	}

	// Undo inside the window only clears the mark: no IO. It waits for a running action instead of being dropped.
	private Task UndoAsync() => RunHandlerAsync(() => _busy.RunWhenFreeAsync(RestoreClipAsync));

	private async Task RestoreClipAsync()
	{
		if (_undo is not { } undo)
		{
			return; // the removal was already committed
		}

		_undo = null;
		await undo.Window.CancelAsync();
		_toastHost.Children.Clear();
		_pieces.Insert(Math.Min(undo.Index, _pieces.Count), undo.Piece);
		_index = Math.Min(undo.Index, _pieces.Count - 1);
		_take = null;
		StopPlayback();
		Log($"[Record] undo key={undo.Key}");
		Render();
		PlayKept();
	}

	// ---- Windows keys (frame 184) ----

	bool IRecordKeys.HandleRecordKey(RecordKey key)
	{
		if (_alerts > 0 || _waitOverlay.IsVisible)
		{
			return key == RecordKey.Leave; // an alert or the wait overlay is up: the keys belong to it, and Esc is swallowed so it cannot pop the page behind the dialog
		}

		if (key == RecordKey.Leave)
		{
			_ = LeaveOnceAsync(toCaller: false); // Esc is Back; the one leave waits for a running action and never throws
			return true;
		}

		if (_busy.IsBusy || _setup is null)
		{
			return false;
		}

		switch (key)
		{
			case RecordKey.Record when Recording:
				_ = StopRecordingAsync();
				return true;
			case RecordKey.Record when Idle && Step != RecordStep.Recording && !Listening:
				_ = StartRecordingAsync();
				return true;
			case RecordKey.Play when Step == RecordStep.Recorded:
				_ = TogglePlaybackAsync();
				return true;
			case RecordKey.Skip when Step == RecordStep.Ready && !Listening:
				_ = SkipAsync();
				return true;
			case RecordKey.Next when Step == RecordStep.Recorded:
				_ = NextAsync();
				return true;
			default:
				return false;
		}
	}

	// ---- files ----

	// A take left in the cache by an app that was killed mid-take is not a recording: it is deleted when the screen opens.
	private static void DeleteStrayTakes()
	{
		try
		{
			foreach (string path in Directory.GetFiles(FileSystem.CacheDirectory, $"{StoryRecordings.TakePrefix}-*").Where(p => StoryRecordings.IsTakeFileName(Path.GetFileName(p))))
			{
				TryDelete(path);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// no-fallback: cache cleanup, nothing the person gets changes
			Debug.WriteLine($"Dino Reader could not look for stray takes: {ex.Message}");
		}
	}

	private static void TryDelete(string? path)
	{
		try
		{
			if (path is not null)
			{
				File.Delete(path);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// no-fallback: temp file cleanup, nothing the person gets changes
			Debug.WriteLine($"Dino Reader could not remove a temporary recording: {ex.Message}");
		}
	}
}
