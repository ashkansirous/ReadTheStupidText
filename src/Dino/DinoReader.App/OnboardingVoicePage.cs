using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using Sirous.Reading.Application.Reading;
using Sirous.Reading.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>
/// Step 5 of the first run, "Your voice" (Slice 362, mock N5 and WN5): the grown-up is asked to be the voice of one character on the demo page and sees where their voice stays (on this device, never uploaded, deleted when setup ends).
/// Record opens the Record page on that character's lines on the demo page only (mock N6a, N6b: no Skip, no Done; the microphone is asked there, not here); when the clips are in the page shows "That's you" (<see cref="OnboardingRoute.ShowHearYourselfAsync"/>).
/// A refused microphone shows "Microphone is off" with Open settings and a quiet Continue without recording, the only way past the step with no recording (Decision 4). Android and iOS ask for the microphone in <see cref="RecordPage"/>; Windows has no prompt.
/// </summary>
internal sealed class OnboardingVoicePage : ContentPage, IDinoKeyTarget
{
	private const string PrivacyTileFill = "#DFF5E3";
	private const string PrivacyTileInk = "#2E7D32";
	private const string RecordRed = "#E5484D";
	private const double PictureHeight = 250;
	private const double PictureCorner = 24;
	private const double PictureZoom = 2.1;
	private const double BadgeSize = 68;
	private const double BadgeOverhang = 18;
	private const double HeadingSize = 24;
	private const double BodySize = 14.5;
	private static readonly TimeSpan FinishWait = TimeSpan.FromSeconds(15);

	private readonly IServiceProvider _services;
	private readonly IStoryLibrary _library;
	private readonly UsageCounter _usage;
	private readonly BusyGuard _busy = new();
	private readonly WaitOverlay _waitOverlay = DinoWait.OverPage();
	private readonly WaitController _wait;
	private readonly Grid _root = new();
	private OnboardingShell? _introShell;
	private OnboardingShell? _micOffShell;
	private CoralButton _record = null!;
	private Size _windowSize;
	private OnboardingShape? _shape;
	private OnboardingDemoPage _demo = null!;
	private Story _story = null!;
	private RecordPage? _recordPage;
	private string _characterName = string.Empty;
	private bool _micOff;

	public OnboardingVoicePage(IServiceProvider services, IStoryLibrary library, UsageCounter usage)
	{
		_services = services;
		_library = library;
		_usage = usage;
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		BackgroundColor = DinoTokens.Get(DinoTokens.Cream);
		AutomationId = "OnboardingVoice";
		_wait = new WaitController(_waitOverlay);
		Content = new Grid { Children = { _root, _waitOverlay } };
	}

	/// <summary>Builds the screen for <paramref name="demo"/>'s character on <paramref name="story"/>. Called once, before the page shows.</summary>
	public void Load(OnboardingDemoPage demo, Story story)
	{
		_demo = demo;
		_story = story;
		_characterName = NameOf(story, demo.Character);
		_introShell = BuildIntro();
		_root.Children.Add(_introShell);
	}

	// The character's name as the book's cast gives it (the Record page shows the same).
	private static string NameOf(Story story, string character) =>
		RecordSplitBook.Of(story).Speakers.FirstOrDefault(s => s.Id == character)?.Name ?? character;

	// ---- N5, the intro ----

	private OnboardingShell BuildIntro()
	{
		var heading = new Label { Text = AppResources.Onboarding_VoiceTitle, FontSize = HeadingSize, FontAttributes = FontAttributes.Bold, FontFamily = DinoFonts.Ui, LineHeight = 1.2 };
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
		var body = new Label { FontSize = BodySize, LineHeight = 1.45, TextColor = DinoTokens.Get(DinoTokens.Body), FontFamily = DinoFonts.Ui };
		OnboardingText.ShowMarkup(body, AppResources.Format(AppResources.Onboarding_VoiceBody, _characterName));
		var left = new VerticalStackLayout { Spacing = 8, Children = { heading, body } };
		var right = new ScrollView { Content = new VerticalStackLayout { Spacing = 0, Children = { BuildPicture(), BuildPrivacyCard() } } };
		var ask = new Label { Text = AppResources.Onboarding_MicAsk, FontSize = 13, TextColor = DinoTokens.Get(DinoTokens.Body), HorizontalTextAlignment = TextAlignment.Center, FontFamily = DinoFonts.Ui };
		_record = new CoralButton { Text = AppResources.Format(AppResources.Onboarding_RecordLines, _characterName), AutomationId = "OnboardingRecord" };
		UiUsage.Press(_record, UiControls.Record, () => _ = _busy.RunAsync(RecordAsync));
		var foot = new VerticalStackLayout { Spacing = 8, Children = { ask, _record } };
		return new OnboardingShell(left, right, foot, stepBar: new OnboardingStepBar(OnboardingStep.Voice));
	}

	// The demo page's picture cropped on the character (the mock's background is 210% wide at 50% 52%), with the red microphone badge hanging 18 below the card.
	private View BuildPicture()
	{
		string? file = _story.Pages[_demo.PageIndex].ImageFile is { } name ? _library.TryResolveFile(_story.Id, name) : null;
		var card = new Border
		{
			HeightRequest = PictureHeight, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = PictureCorner }, BackgroundColor = DinoTokens.Get(DinoTokens.Sand),
			Margin = new Thickness(0, 14, 0, BadgeOverhang),
		};
		if (file is not null && File.Exists(file))
		{
			card.Content = new Image { Source = ImageSource.FromFile(file), Aspect = Aspect.AspectFill, Scale = PictureZoom, AnchorY = 0.52, AnchorX = 0.5 };
		}

		var badge = new Border
		{
			WidthRequest = BadgeSize, HeightRequest = BadgeSize, StrokeShape = new Ellipse(), StrokeThickness = 4, Stroke = DinoTokens.Get(DinoTokens.Cream), BackgroundColor = Color.FromArgb(RecordRed),
			HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.End,
			Content = new IconView { Kind = IconKind.Mic, Color = Colors.White, Size = 30, StrokeWidth = 2.4, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};
		SemanticProperties.SetDescription(card, _characterName);
		return new Grid { Children = { card, badge } };
	}

	// The privacy card (mock N5): a green tile with the device icon, "Your voice stays on this phone", and the promise under it.
	private static Border BuildPrivacyCard()
	{
		var tile = new Border
		{
			WidthRequest = 40, HeightRequest = 40, StrokeThickness = 0, BackgroundColor = Color.FromArgb(PrivacyTileFill), StrokeShape = new RoundRectangle { CornerRadius = 12 }, VerticalOptions = LayoutOptions.Start,
			Content = new IconView { Kind = IconKind.Phone, Color = Color.FromArgb(PrivacyTileInk), Size = 22, StrokeWidth = 2, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};
		var title = new Label { Text = OnboardingText.StaysTitle(), FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), FontFamily = DinoFonts.Ui };
		var text = new Label { Text = AppResources.Onboarding_StaysBody, FontSize = 13.5, LineHeight = 1.4, TextColor = DinoTokens.Get(DinoTokens.Body), FontFamily = DinoFonts.Ui };
		var row = new Grid { ColumnSpacing = 12, ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)] };
		row.Add(tile, 0);
		row.Add(new VerticalStackLayout { Spacing = 2, Children = { title, text } }, 1);
		return new Border { StrokeThickness = 0, BackgroundColor = Colors.White, StrokeShape = new RoundRectangle { CornerRadius = 16 }, Padding = new Thickness(14), Margin = new Thickness(0, 16, 0, 12), Content = row };
	}

	// ---- recording ----

	// Opens the Record page on the demo character's lines on the demo page. The page's pieces are cut from that one page only, so it is quick.
	private async Task RecordAsync()
	{
		RecordSetup? setup = await _wait.RunAsync(WaitStage.GettingFile, AppResources.Wait_GettingBook, canCancel: false, _ => PrepareAsync());
		if (setup is null)
		{
			await OnboardingRoute.VoiceDoneAsync(_services, tried: false); // the page has no line to record: nothing to try, the step is passed over
			return;
		}

		var record = _services.GetRequiredService<RecordPage>();
		record.Onboarding = new OnboardingRecordMode(_characterName);
		record.Load(setup, 0);
		_recordPage = record;
		await Navigation.PushAsync(record);
		OnboardingLog($"record opened lines={setup.Pieces.Count}");
	}

	private async Task<RecordSetup?> PrepareAsync()
	{
		Story story = await _library.GetAsync(_story.Id) ?? _story;
		string language = StoryLanguages.Current().Main;
		RecordBook source = RecordSplitBook.Of(story);
		RecordBook book = await RecordBooks.InAsync(_services, story, source, language, CancellationToken.None) ?? source;
		RecordBook demoPage = book with { Sentences = [.. book.Sentences.Where(s => s.Page == _demo.PageIndex)] };
		var choice = new RecordChoice(RecordMode.PerCharacter, new HashSet<string> { _demo.Character });
		IReadOnlyList<RecordPiece> pieces = OnboardingDemo.LinesOf(await Task.Run(() => RecordCard.Pieces(story, demoPage, choice, language)), _demo);
		return pieces.Count == 0 ? null : new RecordSetup(story, book, choice, pieces, Listening: false, language);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		UiScreen.Show(UiScreens.RecordIntro, this);
		if (Window is { } window)
		{
			window.Resumed -= OnWindowResumed; // OnAppearing can fire twice without OnDisappearing
			window.Resumed += OnWindowResumed;
		}

		if (_recordPage is { } record)
		{
			_recordPage = null;
			_ = _busy.RunWhenFreeAsync(() => AfterRecordAsync(record));
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		if (Window is { } window)
		{
			window.Resumed -= OnWindowResumed;
		}
	}

	// Back from the Record page: the microphone was refused (the "Microphone is off" state), or at least one clip is in ("That's you"), or the grown-up backed out with nothing recorded (stay).
	private async Task AfterRecordAsync(RecordPage record)
	{
		try
		{
			await record.FinishedAsync.WaitAsync(FinishWait); // a Record page that never reports finished must not keep this page's busy guard for ever: Record stays usable
		}
		catch (TimeoutException)
		{
			// no-fallback: the Record page did not report finished in time; the recorded clips (if any) are read below and the page stays usable
			OnboardingLog("record page did not report finished");
		}

		if (record.MicRefused)
		{
			ShowMicOff();
			return;
		}

		// Only a clip recorded in this step (the owned try record) counts; a clip the book held before, or from an earlier run, never jumps to "That's you".
		Story? saved = await _library.GetAsync(_story.Id);
		if (saved is not null && OnboardingTryStore.Current is { } tried && tried.BookId == saved.Id && tried.ClipsToDelete(saved).Count > 0)
		{
			await OnboardingRoute.ShowHearYourselfAsync(_services, _demo);
		}
	}

	// Coming back from the system's settings: a microphone that is allowed now takes the grown-up back to the intro, so Record works at once.
	private async void OnWindowResumed(object? sender, EventArgs e)
	{
		if (!_micOff)
		{
			return;
		}

		try
		{
			if (await Permissions.CheckStatusAsync<Permissions.Microphone>() == PermissionStatus.Granted)
			{
				ShowIntro();
			}
		}
		catch (Exception ex)
		{
			// no-fallback: the check failed; the grown-up can still tap Continue without recording
			Debug.WriteLine($"Dino Reader could not re-check the microphone: {ex.Message}");
		}
	}

	// ---- the microphone is off (Decision 4; no mock frame, built from the shipped "Microphone is off" panel) ----

	private void ShowMicOff()
	{
		_micOff = true;
		_micOffShell ??= BuildMicOff();
		_root.Children.Clear();
		_root.Children.Add(_micOffShell);
		if (_shape is { } shape)
		{
			_micOffShell.Place(shape);
		}

		OnboardingLog("microphone off");
	}

	private void ShowIntro()
	{
		_micOff = false;
		_root.Children.Clear();
		_root.Children.Add(_introShell!);
		if (_shape is { } shape)
		{
			_introShell!.Place(shape);
		}
	}

	private OnboardingShell BuildMicOff()
	{
		var icon = new Border
		{
			WidthRequest = 96, HeightRequest = 96, StrokeThickness = 0, StrokeShape = new Ellipse(), BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint), HorizontalOptions = LayoutOptions.Center,
			Content = new IconView { Kind = IconKind.Mic, Color = DinoTokens.Get(DinoTokens.Brand), Size = 44, StrokeWidth = 2.4, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};
		var title = new Label { Text = AppResources.Rec_NoMicTitle, FontSize = 22, FontAttributes = FontAttributes.Bold, FontFamily = DinoFonts.Ui, HorizontalTextAlignment = TextAlignment.Center, AutomationId = "OnboardingMicOffTitle" };
		SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
		var text = new Label { Text = AppResources.Onboarding_MicOffBody, FontSize = 14.5, LineHeight = 1.4, TextColor = DinoTokens.Get(DinoTokens.Body), FontFamily = DinoFonts.Ui, HorizontalTextAlignment = TextAlignment.Center };
		var words = new VerticalStackLayout { Spacing = 14, Children = { icon, title, text } };
		var open = new CoralButton { Text = AppResources.Onboarding_OpenSettings, AutomationId = "OnboardingOpenSettings" };
		UiUsage.Press(open, UiControls.Confirm, OpenSettings);
		Button without = BuildQuietLink();
		var foot = new VerticalStackLayout { Spacing = 4, Children = { open, without } };
		return new OnboardingShell(words, null, foot, centred: true, stepBar: null);
	}

	// The quiet text link under the coral button: the only way past this step without a recording.
	private Button BuildQuietLink()
	{
		var link = new Button
		{
			AutomationId = "OnboardingContinueWithout", Text = AppResources.Onboarding_ContinueWithoutRecording, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Body),
			BackgroundColor = Colors.Transparent, BorderWidth = 0, HeightRequest = 44,
		};
		UiUsage.Press(link, UiControls.Skip, () => _ = _busy.RunAsync(() => OnboardingRoute.VoiceDoneAsync(_services, tried: false)));
		return link;
	}

	private static void OpenSettings()
	{
		try
		{
			AppInfo.Current.ShowSettingsUI();
		}
		catch (Exception ex)
		{
			// no-fallback: the settings screen could not be opened; the grown-up can allow the microphone by hand or continue without recording
			Debug.WriteLine($"Dino Reader could not open the system settings: {ex.Message}");
		}
	}

	// ---- layout ----

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		(width, height) = WindowShapeSeam.Effective(DesktopTips.IsWindows, width, height);
		if (width <= 0 || height <= 0 || (width == _windowSize.Width && height == _windowSize.Height))
		{
			return;
		}

		_windowSize = new Size(width, height);
		OnboardingShape shape = OnboardingLayout.Shape(width, height, DesktopTips.IsWindows, intro: false);
		if (shape != _shape)
		{
			_shape = shape;
			_introShell?.Place(shape);
			_micOffShell?.Place(shape);
			(double buttonWidth, double buttonHeight) = OnboardingLayout.ContinueSize(shape);
			if (_record is not null)
			{
				_record.SetSize(buttonWidth, buttonHeight);
				_record.HorizontalOptions = shape.Split ? LayoutOptions.Start : LayoutOptions.Fill;
			}

			OnboardingLog($"shape={(shape.Split ? "split" : "stack")}");
		}
	}

	// Android Back does nothing here, as Esc on Windows: the voice step has no way back.
	protected override bool OnBackButtonPressed() => true;

	// Windows: Enter is Record, Esc does nothing (the step has no way back).
	DinoScreen IDinoKeyTarget.Screen => DinoScreen.FirstBooks;

	bool IDinoKeyTarget.Handle(DinoKeyAction action, DinoKey key)
	{
		switch (action)
		{
			case DinoKeyAction.PickContinue when !_micOff && !_record.IsFocused:
				_ = _busy.RunAsync(RecordAsync);
				return true;
			case DinoKeyAction.Back:
				return true;
			default:
				return false;
		}
	}

	private static void OnboardingLog(string line)
	{
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Onboarding] voice {line}");
#endif
		Debug.WriteLine($"Dino Reader onboarding voice {line}");
	}
}
