using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>
/// The grown-up Record screen (Slice 111, Decision 115, mock frames 44 and 45): a parent reads a book page by page into the microphone, listens back,
/// re-records or deletes, and moves on; any page can be skipped. A recording is saved the moment it stops (file first, then <c>story.json</c> on top of the
/// latest stored story). The microphone is asked for only here, the first time a recording starts, and a recording never outlives the screen.
/// </summary>
public sealed class RecordPage : ContentPage, IGrownUpPage
{
	private static readonly TimeSpan ClockTick = TimeSpan.FromMilliseconds(250);
	private const double RecordGap = RecordLayout.ColumnGap;
	private const double TabletSideMargin = 40;
	private const double TabletVerticalMargin = 24;
	private const double RailClockSize = 12;

	private Size _windowSize;
	private RecordShape _shown;

	private readonly IStoryLibrary _library;
	private readonly IMicRecorder _recorder;
	private readonly IRecordingPlayer _player;
	private readonly UsageCounter _usage;
	private readonly BusyGuard _busy = new();

	/// <summary>Set by the intro wizard when it pushes this page: the header button then reads "Continue" (Slice 229).</summary>
	public bool LaunchedFromIntro { get; set; }
	private readonly WaitOverlay _waitOverlay = DinoWait.OverPage();
	private readonly WaitController _wait;
	private Label _time = NewTimeLabel(sideways: false);
	private string? _pathKey;
	private string? _picturePath;
	private string? _aspectOf;
	private double _aspect;
	private long _recordingStart; // when the running recording began: the clock reads from it, so a layout pass never touches it
	private readonly Grid _root = new() { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)] };
	private Story? _story;
	private int _page;
	private bool _recording;
	private bool _playing;
	private bool _starting;
	private bool _visible;
	private string? _tempPath;
	private IDispatcherTimer? _timer;

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
		Content = new Grid { Children = { _root, _waitOverlay } };
	}

	/// <summary>Opens the screen on the first page of <paramref name="story"/>.</summary>
	public Task LoadAsync(Story story)
	{
		_story = story;
		_page = 0;
		Render();
		return Task.CompletedTask;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_visible = true;
		if (Window is { } window)
		{
			window.Stopped += OnWindowStopped;
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
		if (_recording)
		{
			_ = DiscardRecordingAsync();
		}
	}

	// The app going to the background does not disappear this page, so a recording is dropped here: the microphone is never open out of sight.
	private void OnWindowStopped(object? sender, EventArgs e)
	{
		StopPlayback();
		if (_recording)
		{
			_ = DiscardRecordingAsync().ContinueWith(_ => MainThread.BeginInvokeOnMainThread(Render), TaskScheduler.Default);
		}
	}

	// The clock's AutomationId is set only on the sideways layouts, so a scripted check for it fails on the wrong layout.
	private static Label NewTimeLabel(bool sideways)
	{
		var label = new Label { FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Danger) };
		if (sideways)
		{
			label.AutomationId = RecordViews.ClockId;
		}

		return label;
	}

	// Slice 232 part C (mock Batch 44 frames 116, 129): the window size picks one pure shape (RecordLayout.For) and Render lays the same state out in it. A rotation only calls Render again:
	// _recording, the recorder, the clock, the playback and the page all live in fields, so a recording goes on through it and the stop square comes back in place.
	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		if (width > 0 && height > 0 && (width != _windowSize.Width || height != _windowSize.Height))
		{
			_windowSize = new Size(width, height);
			ReapplyShape();
		}
	}

	private void ReapplyShape()
	{
		if (_story is not { Pages.Count: > 0 } story)
		{
			return;
		}

		RecordShape shape = ShapeFor(story);
		if (shape == _shown)
		{
			return;
		}

		if (!ResizePictureColumn(shape))
		{
			Render();
		}

#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] record layout={_shown.Columns} recording={_recording} page={_page}");
#endif
	}

	// A change of only the picture column's width (the pane follows the window width) re-places that column; no view is rebuilt, so the running clock, the recording card and playback are untouched.
	private bool ResizePictureColumn(RecordShape shape)
	{
		bool onlyPane = shape.Columns == _shown.Columns && shape.RailWidth == _shown.RailWidth && shape.PicturePane > 0 && _shown.PicturePane > 0 && _root.ColumnDefinitions.Count > 0;
		if (onlyPane)
		{
			_root.ColumnDefinitions[0].Width = new GridLength(shape.PicturePane);
			_shown = shape;
		}

		return onlyPane;
	}

	private RecordShape ShapeFor(Story story)
	{
		bool landscape = LandscapeLayout.IsLandscape(_windowSize.Width, _windowSize.Height);
		string? picture = landscape ? PicturePathOf(story) : null;
		return RecordLayout.For(_windowSize.Width, _windowSize.Height, picture is null ? null : AspectOf(picture));
	}

	// The picture file is opened once per picture, not on every size change.
	private double AspectOf(string picture)
	{
		if (_aspectOf != picture)
		{
			_aspect = PageImageSize.Read(picture) is { Height: > 0 } size ? size.Width / size.Height : 0;
			_aspectOf = picture;
		}

		return _aspect;
	}

	// Resolved (and checked on disk) once per story page, not on every size change.
	private string? PicturePathOf(Story story)
	{
		StoryPage page = story.Pages[_page];
		string key = $"{story.Id}/{_page}/{page.ImageFile}";
		if (_pathKey != key)
		{
			string? picture = page.ImageFile is null ? null : _library.TryResolveFile(story.Id, page.ImageFile);
			_picturePath = picture is not null && File.Exists(picture) ? picture : null;
			_pathKey = key;
		}

		return _picturePath;
	}

	private TimeSpan Elapsed() => Stopwatch.GetElapsedTime(_recordingStart);

	private string RecordingText(TimeSpan elapsed) => AppResources.Format(AppResources.Rec_Recording, StoryRecordings.Clock(elapsed));

	private void Render()
	{
		if (_story is not { Pages.Count: > 0 } story)
		{
			return;
		}

		_root.Children.Clear();
		_shown = ShapeFor(story);
		if (_shown.Columns == RecordColumns.Stacked)
		{
			RenderStacked(story);
			return;
		}

		RenderSideways(story, _shown);
	}

	private void RenderStacked(Story story)
	{
		_root.Padding = new Thickness(0);
		_root.ColumnDefinitions = [];
		_root.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)];
		_root.Add(Header(story), 0, 0);
		_root.Add(Body(story), 0, 1);
		_root.Add(Footer(story), 0, 2);
	}

	// Held sideways: the picture column (when the page has a picture), the middle column and, on a phone, the rail with Done on top and the microphone in the middle.
	private void RenderSideways(Story story, RecordShape shape)
	{
		bool phone = shape.Columns == RecordColumns.Three;
		_root.Padding = phone ? new Thickness(RecordLayout.PhoneStartPadding, 10, RecordLayout.PhoneEndPadding, 12) : new Thickness(TabletSideMargin, TabletVerticalMargin);
		_root.RowDefinitions = [new RowDefinition(GridLength.Star)];
		_root.ColumnDefinitions = phone
			? [new ColumnDefinition(shape.PicturePane), new ColumnDefinition(GridLength.Star), new ColumnDefinition(shape.RailWidth)]
			: [new ColumnDefinition(shape.PicturePane), new ColumnDefinition(GridLength.Star)];
		if (shape.PicturePane > 0 && PicturePathOf(story) is { } picture)
		{
			_root.Add(RecordViews.PictureColumn(picture), 0, 0);
		}

		View middle = SidewaysMiddle(story, shape);
		middle.Margin = new Thickness(shape.PicturePane > 0 ? RecordGap : 0, 0, phone ? RecordGap : 0, 0);
		_root.Add(middle, 1, 0);
		if (phone)
		{
			_root.Add(SidewaysRail(story), 2, 0);
		}
	}

	// The header, the words card, the recordings line and the recording's own card; a tablet adds the whole footer (big button, dots, arrows), a phone only the dots (its rail has the rest).
	private Grid SidewaysMiddle(Story story, RecordShape shape)
	{
		bool phone = shape.Columns == RecordColumns.Three;
		var column = new Grid
		{
			RowSpacing = phone ? 8 : 16,
			RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)],
		};
		GrownUpHeader header = Header(story);
		header.ButtonVisible = !phone; // the phone's Done stands at the top of its rail
		column.Add(header, 0, 0);
		string words = RecordViews.PageWords(story.Pages[_page]);
		if (words.Length > 0)
		{
			column.Add(RecordViews.TextPage(words, story.Language, RecordLayout.WordsSize(shape.Columns)), 0, 1);
		}

		column.Add(RecordViews.StayNote(), 0, 2);
		column.Add(StoryRecordings.Of(story, _page) is { } recording && !_recording
			? RecordViews.RecordedCard(recording, _playing, TogglePlaybackAsync, RecordAgainAsync, DeleteAsync)
			: RecordViews.ScriptCard(), 0, 3);

		column.Add(phone ? RecordViews.Dots(story.Pages.Count, _page, p => StoryRecordings.Of(story, p) is not null) : Footer(story, sideways: true), 0, 4);
		return column;
	}

	private Grid SidewaysRail(Story story)
	{
		var rail = new Grid { RowSpacing = 8, RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)] };
		rail.Add(RecordViews.RailDone(LaunchedFromIntro, () => _busy.RunAsync(() => Navigation.PopAsync())), 0, 0);
		rail.Add(RailMicrophone(story), 0, 1);
		if (!_recording)
		{
			// As upright: nothing moves the page while a recording runs (the recording would be saved under the wrong page).
			rail.Add(RecordViews.RailNav(_page > 0, _page == story.Pages.Count - 1, () => GoToPageAsync(_page - 1), NextAsync), 0, 2);
		}

		return rail;
	}

	// The microphone while nothing is recorded for the page, the stop square (in the same place) with its clock while recording, nothing when the page already has a recording (its card re-records).
	private VerticalStackLayout RailMicrophone(Story story)
	{
		var centre = new VerticalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center };
		if (_recording)
		{
			_time = NewTimeLabel(sideways: true);
			_time.FontSize = RailClockSize;
			_time.Text = RecordingText(Elapsed());
			centre.Add(RecordViews.RecordingLabel(_time));
			centre.Add(RecordViews.RecordButton(recording: true, StopRecordingAsync, RecordLayout.PhoneButtonSize, sideways: true));
		}
		else if (StoryRecordings.Of(story, _page) is null)
		{
			centre.Add(RecordViews.RecordButton(recording: false, StartRecordingAsync, RecordLayout.PhoneButtonSize, sideways: true));
		}

		return centre;
	}

	private GrownUpHeader Header(Story story)
	{
		var header = new GrownUpHeader
		{
			Title = AppResources.Rec_Title,
			Subtitle = AppResources.Format(AppResources.Rec_Subtitle, story.Title, _page + 1, story.Pages.Count),
			Continue = LaunchedFromIntro,
		};
		header.BackRequested += async (_, _) => await _busy.RunAsync(() => Navigation.PopAsync());
		header.DoneRequested += async (_, _) => await _busy.RunAsync(() => Navigation.PopAsync()); // leaving stops and keeps a recording in progress, as Back does
		return header;
	}

	// The page as the child sees it, then either the script line (nothing recorded yet) or the recording's own card (frame 45).
	private Grid Body(Story story)
	{
		StoryPage page = story.Pages[_page];
		string? picture = page.ImageFile is null ? null : _library.TryResolveFile(story.Id, page.ImageFile);
		var body = new Grid { Padding = new Thickness(14, 0), RowSpacing = 12, RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)] };
		body.Add(RecordViews.PageView(picture is not null && File.Exists(picture) ? picture : null, page, story.Language), 0, 0);
		body.Add(RecordViews.StayNote(), 0, 1);
		body.Add(StoryRecordings.Of(story, _page) is { } recording && !_recording
			? RecordViews.RecordedCard(recording, _playing, TogglePlaybackAsync, RecordAgainAsync, DeleteAsync)
			: RecordViews.ScriptCard(), 0, 2);
		return body;
	}

	private VerticalStackLayout Footer(Story story, bool sideways = false)
	{
		var footer = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(14, 12, 14, 22) };
		if (_recording)
		{
			_time = NewTimeLabel(sideways);
			_time.Text = RecordingText(Elapsed());
			footer.Add(RecordViews.RecordingLabel(_time));
			footer.Add(RecordViews.RecordButton(recording: true, StopRecordingAsync, sideways: sideways));
			return footer;
		}

		if (StoryRecordings.Of(story, _page) is null)
		{
			footer.Add(RecordViews.RecordButton(recording: false, StartRecordingAsync, sideways: sideways));
		}

		footer.Add(RecordViews.Dots(story.Pages.Count, _page, p => StoryRecordings.Of(story, p) is not null));
		footer.Add(RecordViews.NavRow(_page > 0, _page == story.Pages.Count - 1, () => GoToPageAsync(_page - 1), NextAsync));
		return footer;
	}

	private Task NextAsync() => _story is { } story && _page < story.Pages.Count - 1 ? GoToPageAsync(_page + 1) : _busy.RunAsync(() => Navigation.PopAsync());

	private Task GoToPageAsync(int page)
	{
		StopPlayback();
		_page = Math.Clamp(page, 0, (_story?.Pages.Count ?? 1) - 1);
		Render();
		return Task.CompletedTask;
	}

	private Task RecordAgainAsync()
	{
		StopPlayback();
		return StartRecordingAsync();
	}

	private async Task StartRecordingAsync()
	{
		if (_recording || _starting || _story is null)
		{
			return;
		}

		_starting = true;
		try
		{
			await BeginRecordingAsync();
		}
		finally
		{
			_starting = false;
		}
	}

	private async Task BeginRecordingAsync()
	{
		if (!await EnsureMicrophoneAsync() || !_visible)
		{
			return;
		}

		string temp = Path.Combine(FileSystem.CacheDirectory, $"{StoryRecordings.FilePrefix}-{Guid.NewGuid():N}{StoryRecordings.FileExtension}");
		if (!await TryStartRecorderAsync(temp))
		{
			return;
		}

		if (!_visible)
		{
			await _recorder.CancelAsync(); // the page was left while the recorder opened: the microphone does not stay open
			TryDelete(temp);
			return;
		}

		_tempPath = temp;
		_recording = true;
		_recordingStart = Stopwatch.GetTimestamp();
		StartTimer();
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
			Debug.WriteLine($"Dino Reader was refused the microphone: {ex.Message}");
			TryDelete(temp);
			await DisplayAlertAsync(AppResources.Rec_NoMicTitle, AppResources.Rec_NoMic, AppResources.Common_Ok);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not start recording: {ex}");
			TryDelete(temp);
			await DisplayAlertAsync(AppResources.Rec_Title, AppResources.Rec_Failed, AppResources.Common_Ok);
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

		await DisplayAlertAsync(AppResources.Rec_NoMicTitle, AppResources.Rec_NoMic, AppResources.Common_Ok);
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
		timer.Tick += (_, _) => _time.Text = RecordingText(Elapsed());
		return timer;
	}

	private async Task StopRecordingAsync()
	{
		if (!_recording)
		{
			return;
		}

		_timer?.Stop();
		_recording = false;
		string? temp = _tempPath;
		_tempPath = null;
		try
		{
			TimeSpan length = await _recorder.StopAsync();
			await SaveWithWaitAsync(temp!, length);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not save the recording: {ex}");
			await DisplayAlertAsync(AppResources.Rec_Title, AppResources.Rec_Failed, AppResources.Common_Ok);
		}
		finally
		{
			TryDelete(temp);
			Render();
		}
	}

	// Slice 143: saving shows the wait overlay if it takes over 500 ms; there is no Cancel, a half-saved recording is worse than waiting.
	private async Task SaveWithWaitAsync(string temp, TimeSpan length)
	{
		_wait.Begin(WaitStage.Saving, AppResources.Wait_Saving, canCancel: false);
		try
		{
			await SaveRecordingAsync(temp, length);
		}
		finally
		{
			_wait.End();
		}
	}

	// The story goes in first, on top of the latest stored one (the reading page may have saved its own switches meanwhile), then the audio moves into
	// place: a failure of either leaves the page's old recording and its stored length as they were.
	private async Task SaveRecordingAsync(string temp, TimeSpan length)
	{
		Story shown = _story!;
		Story latest = await _library.GetAsync(shown.Id) ?? shown;
		Story updated = StoryRecordings.With(latest, _page, new PageRecording(latest.Language, StoryRecordings.WholeSeconds(length)));
		await _library.UpdateAsync(updated);
		try
		{
			await _library.StoreRecordingAsync(shown.Id, _page, path =>
			{
				File.Move(temp, path, overwrite: true);
				return Task.CompletedTask;
			});
		}
		catch
		{
			await _library.UpdateAsync(latest);
			throw;
		}

		_story = updated;
		// Which kind of book and how many of its pages are recorded, as a bucket (Slice 134); a re-recorded page is not counted again.
		if (StoryRecordings.Of(latest, _page) is null)
		{
			_usage.Record(UsageEventName.RecordingMade, updated, UsageDetail.PagesBucket(StoryRecordings.Count(updated)));
		}
	}

	private async Task DiscardRecordingAsync()
	{
		_timer?.Stop();
		_recording = false;
		string? temp = _tempPath;
		_tempPath = null;
		await _recorder.CancelAsync();
		TryDelete(temp);
	}

	private Task TogglePlaybackAsync()
	{
		if (_playing)
		{
			StopPlayback();
			Render();
			return Task.CompletedTask;
		}

		if (_story is { } story && _library.TryResolveFile(story.Id, StoryRecordings.FileNameOf(_page)) is { } path && File.Exists(path))
		{
			PlayFile(path);
		}

		return Task.CompletedTask;
	}

	private void PlayFile(string path)
	{
		try
		{
			_player.Play(path, () => MainThread.BeginInvokeOnMainThread(OnPlaybackEnded), () => MainThread.BeginInvokeOnMainThread(OnPlaybackEnded));
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not play the recording: {ex}");
			return;
		}

		_playing = true;
		Render();
	}

	private void OnPlaybackEnded()
	{
		_playing = false;
		Render();
	}

	private void StopPlayback()
	{
		if (_playing)
		{
			_player.Stop();
			_playing = false;
		}
	}

	private async Task DeleteAsync()
	{
		if (_story is null || !await DisplayAlertAsync(AppResources.Rec_Delete, AppResources.Rec_DeleteBody, AppResources.Common_Delete, AppResources.Common_Keep))
		{
			return;
		}

		StopPlayback();
		try
		{
			await RemoveRecordingAsync(_story, _page);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader could not delete the recording: {ex}");
		}

		Render();
	}

	private async Task RemoveRecordingAsync(Story shown, int page)
	{
		await _library.DeleteFileAsync(shown.Id, StoryRecordings.FileNameOf(page));
		Story latest = await _library.GetAsync(shown.Id) ?? shown;
		Story updated = StoryRecordings.Without(latest, page);
		await _library.UpdateAsync(updated);
		_story = updated;
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
			Debug.WriteLine($"Dino Reader could not remove a temporary recording: {ex.Message}");
		}
	}
}
