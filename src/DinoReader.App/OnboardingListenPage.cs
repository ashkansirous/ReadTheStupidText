using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared.Crashes;
using ReadTheStupidText.Mobile.Shared.Waiting;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace DinoReader;

/// <summary>Which of the two pages the onboarding plays on the reading page (Slice 362): step 4, Dino reads in every voice; step 5's last screen, the same page with the grown-up's own clips.</summary>
internal enum OnboardingListenKind
{
	Listen,
	HearYourself,
}

/// <summary>What the reading page is told to play in onboarding: the demo page of a free book, the book as it is on the shelf now (with the try's clips for <see cref="OnboardingListenKind.HearYourself"/>), and which screen it is.</summary>
internal sealed record OnboardingListenPlan(OnboardingDemoPage Demo, Story Story, OnboardingListenKind Kind);

/// <summary>A small stroked glyph on the 24-unit grid of the mock's icons: the sound wave of the speaker chip and the replay arrow, which <see cref="IconKind"/> has no glyph for (Slice 362).</summary>
internal sealed class OnboardingGlyph : ContentView
{
	private const double GridUnits = 24;
	private const string SoundWave = "M4 10v4 M8 7v10 M12 4v16 M16 7v10 M20 10v4";
	private const string Replay = "M3 12a9 9 0 1 0 3-6.7 M3 4v4.5h4.5";
	private static readonly PathGeometryConverter Converter = new();

	public enum Kind
	{
		SoundWave,
		Replay,
	}

	public OnboardingGlyph(Kind kind, Color colour, double size, double stroke = 2.4)
	{
		InputTransparent = true;
		WidthRequest = size;
		HeightRequest = size;
		VerticalOptions = LayoutOptions.Center;
		HorizontalOptions = LayoutOptions.Center;
		Content = new Grid
		{
			WidthRequest = GridUnits, HeightRequest = GridUnits, Scale = size / GridUnits, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new Path
				{
					Data = (Geometry)Converter.ConvertFromInvariantString(kind == Kind.SoundWave ? SoundWave : Replay)!,
					Stroke = new SolidColorBrush(colour), StrokeThickness = stroke, StrokeLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Aspect = Stretch.None,
				},
			},
		};
	}
}

/// <summary>
/// The language bar of the listen step (mock N4a-N4c, N7): a pill of one segment per story language, the playing one white. With one language it is the reading page's round flag pill (N4d). The segment the bubble points at carries the
/// amber pulse. Tapping a segment tells the page, which restarts the demo page in that language.
/// </summary>
internal sealed class OnboardingLanguageBar : Border
{
	private const string PillFill = "#F0E8D8";
	private const double SegmentHeight = 40;
	private const double SegmentCorner = 13;
	private const double FlagSize = 26;
	private const double LoneFlagSize = 34;
	private const double LoneHeight = 46;
	private const double NameSize = 15;

	private readonly Dictionary<string, Border> _segments = [];
	private readonly Dictionary<string, PulseRing> _rings = [];

	public OnboardingLanguageBar(IReadOnlyList<string> languages, Action<string> tapped)
	{
		StrokeThickness = 0;
		BackgroundColor = Color.FromArgb(PillFill);
		StrokeShape = new RoundRectangle { CornerRadius = 16 };
		VerticalOptions = LayoutOptions.Center;
		FlowDirection = FlowDirection.LeftToRight;
		if (languages.Count < 2)
		{
			BuildLone(languages.FirstOrDefault() ?? UiLanguage.English);
			return;
		}

		Padding = new Thickness(3);
		var row = new HorizontalStackLayout { Spacing = 2 };
		foreach (string code in languages)
		{
			Border segment = BuildSegment(code, tapped);
			_segments[code] = segment;
			row.Children.Add(new Grid { Children = { segment, _rings[code] } });
		}

		Content = row;
	}

	// One language: only the reading page's flag pill (46 high, the flag 34 in a white ring).
	private void BuildLone(string code)
	{
		Padding = new Thickness(6, 0, 10, 0);
		HeightRequest = LoneHeight;
		Content = new FlagRing(code, LoneFlagSize, 2, Colors.White);
		SemanticProperties.SetDescription(this, LanguageNames.NativeName(code));
	}

	private Border BuildSegment(string code, Action<string> tapped)
	{
		bool rtl = DinoFonts.IsRightToLeft(code);
		var name = new Label
		{
			Text = LanguageNames.NativeName(code), FontSize = NameSize, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center,
			FontFamily = rtl ? DinoFonts.Persian : DinoFonts.Ui, FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
		};
		var content = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center, Children = { new FlagRing(code, FlagSize, 1.5, Color.FromRgba(43 / 255.0, 37 / 255.0, 64 / 255.0, 0.12)), name } };
		var segment = new Border
		{
			AutomationId = "OnboardingLanguage-" + code, HeightRequest = SegmentHeight, StrokeThickness = 0, Padding = new Thickness(6, 0, 12, 0), StrokeShape = new RoundRectangle { CornerRadius = SegmentCorner },
			Content = content,
		};
		SemanticProperties.SetDescription(segment, name.Text);
		Taps.AddTap(segment, () =>
		{
			tapped(code);
			return Task.CompletedTask;
		}, UiControls.Language);
		_rings[code] = new PulseRing(SegmentCorner) { IsVisible = false, HeightRequest = SegmentHeight, VerticalOptions = LayoutOptions.Fill };
		Style(segment, name, selected: false);
		return segment;
	}

	/// <summary>Whites the segment of <paramref name="language"/> (ink text, a soft shadow); the others are the pill's own colour with grey text.</summary>
	public void Select(string language)
	{
		foreach ((string code, Border segment) in _segments)
		{
			var name = (Label)((HorizontalStackLayout)segment.Content!).Children[1];
			Style(segment, name, code == language);
		}
	}

	private static void Style(Border segment, Label name, bool selected)
	{
		segment.BackgroundColor = selected ? Colors.White : Colors.Transparent;
		segment.Shadow = selected ? new Shadow { Brush = new SolidColorBrush(DinoTokens.Get(DinoTokens.Ink)), Offset = new Point(0, 1), Radius = 4, Opacity = 0.16f } : null!;
		name.TextColor = selected ? DinoTokens.Get(DinoTokens.Ink) : DinoTokens.Get(DinoTokens.Body);
	}

	/// <summary>Pulses the amber ring on <paramref name="language"/>'s segment (null stops every ring).</summary>
	public void PointAt(string? language)
	{
		foreach ((string code, PulseRing ring) in _rings)
		{
			bool on = code == language;
			ring.IsVisible = on;
			if (on)
			{
				ring.Start();
			}
			else
			{
				ring.Stop();
			}
		}
	}
}

/// <summary>
/// The chrome the reading page wears in onboarding (Slice 362, mock N4a-N4d, N7, WN4): the step bar, the heading and line, the language bar, the book's title, the page itself in the middle, and the foot (replay, Continue; on "That's you" also
/// Play again). Stacked under 600 wide; from 600 two panes, the words left on the washed pane and the page and every button right. It only draws and reports taps: <see cref="ReadingPage"/> decides what each one does.
/// </summary>
internal sealed class OnboardingListenChrome : Grid
{
	private const string WashFill = "#FFE9C7";
	private const string OutlineStroke = "#d9d2c4";
	private const string PlayAgainInk = "#3a3450";
	private const double ReplaySize = 60;
	private const double WideReplaySize = 56;
	private const double LiveOpacity = 1;
	private const double DimOpacity = 0.45;
	private const double BubbleWidth = 250;

	private readonly OnboardingListenKind _kind;
	private readonly Border _wash = new() { StrokeThickness = 0, BackgroundColor = Color.FromArgb(WashFill), IsVisible = false };
	private readonly VerticalStackLayout _head = new() { Spacing = 8 };
	private readonly Label _title = new() { FontAttributes = FontAttributes.Bold, FontFamily = DinoFonts.Ui, LineHeight = 1.2 };
	private readonly Label _body = new() { TextColor = DinoTokens.Get(DinoTokens.Body), FontFamily = DinoFonts.Ui, LineHeight = 1.4 };
	private readonly Label _bookTitle = new() { FontAttributes = FontAttributes.Bold, LineHeight = 1.4, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
	private readonly Grid _topBar = new() { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)], RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)] };
	private readonly OnboardingLanguageBar _bar;
	private readonly Grid _foot = new() { ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)], ColumnSpacing = 14 };
	private readonly Border _replay;
	private readonly Border? _playAgain;
	private readonly VerticalStackLayout _footRight = new() { Spacing = 8, VerticalOptions = LayoutOptions.Center };
	private readonly CoralButton _continue = new() { Text = AppResources.Common_Continue, AutomationId = "OnboardingContinue" };
	private readonly Grid _bubble = new() { AutomationId = "OnboardingBubble", IsVisible = false, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 6, 10, 0), WidthRequest = BubbleWidth, InputTransparent = true };
	private readonly Label _bubbleText = new() { FontSize = 14.5, LineHeight = 1.4, TextColor = Colors.White, FontFamily = DinoFonts.Ui };
	private readonly Border _stageHost = new() { StrokeThickness = 0, BackgroundColor = Colors.Transparent };
	private bool _live;
	private bool _wide;

	public OnboardingListenChrome(OnboardingListenPlan plan, IReadOnlyList<string> languages, View stage)
	{
		_kind = plan.Kind;
		BackgroundColor = DinoTokens.Get(DinoTokens.Cream);
		AutomationId = plan.Kind == OnboardingListenKind.Listen ? "OnboardingListen" : "OnboardingHearYourself";
		_bar = new OnboardingLanguageBar(languages, code => LanguageTapped?.Invoke(code));
		_replay = BuildReplay();
		_playAgain = plan.Kind == OnboardingListenKind.HearYourself ? BuildPlayAgain() : null;
		SemanticProperties.SetHeadingLevel(_title, SemanticHeadingLevel.Level1);
		_head.Children.Add(new OnboardingStepBar(plan.Kind == OnboardingListenKind.Listen ? OnboardingStep.Listen : OnboardingStep.Voice));
		_head.Children.Add(_title);
		_head.Children.Add(_body);
		_topBar.Add(_bookTitle);
		_topBar.Add(_bar);
		BuildBubble();
		BuildFoot();
		_stageHost.Content = stage;
		UiUsage.Press(_continue, UiControls.Continue, () =>
		{
			if (_live)
			{
				ContinueTapped?.Invoke();
			}
		});
		Children.Add(_wash);
		Children.Add(_head);
		Children.Add(_topBar);
		Children.Add(_stageHost);
		Children.Add(_bubble);
		Children.Add(_foot);
		SetContinueLive(false);
	}

	public event Action? ContinueTapped;

	public event Action? ReplayTapped;

	public event Action<string>? LanguageTapped;

	/// <summary>The sizes and places for the window: stacked under 600 wide, two panes from 600.</summary>
	public void Place(OnboardingShape shape)
	{
		_wide = shape.Split;
		ColumnDefinitions = [];
		RowDefinitions = [];
		if (_wide)
		{
			PlaceWide(shape);
		}
		else
		{
			PlaceStacked();
		}

		_title.FontSize = _wide ? 30 : 21;
		_body.FontSize = _wide ? 15.5 : 14;
		_body.TextColor = _wide ? Color.FromArgb("#4a4460") : DinoTokens.Get(DinoTokens.Body);
		_bookTitle.FontSize = 20;
		_wash.IsVisible = _wide;
		_continue.SetSize(_wide ? 260 : double.NaN, 56);
		_continue.HorizontalOptions = _wide ? LayoutOptions.End : LayoutOptions.Fill;
		double replay = _wide ? WideReplaySize : ReplaySize;
		_replay.WidthRequest = replay;
		_replay.HeightRequest = replay;
		_replay.StrokeShape = new RoundRectangle { CornerRadius = replay / 2 };
		_foot.Padding = _wide ? new Thickness(32, 12, 32, 24) : new Thickness(20, 12, 20, 40);
		PlaceTopBar();
	}

	private void PlaceStacked()
	{
		RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)];
		_head.Padding = new Thickness(20, 6, 20, 6);
		_head.VerticalOptions = LayoutOptions.Start;
		Put(_head, 0, 0, 1, 1);
		Put(_topBar, 1, 0, 1, 1);
		Put(_stageHost, 2, 0, 1, 1);
		Put(_bubble, 2, 0, 1, 1);
		Put(_foot, 3, 0, 1, 1);
		Put(_wash, 0, 0, 1, 1);
		_topBar.Padding = new Thickness(8, 4, 12, 0);
		_stageHost.Margin = new Thickness(14, 6, 14, 0);
	}

	private void PlaceWide(OnboardingShape shape)
	{
		ColumnDefinitions = [new ColumnDefinition(new GridLength(shape.LeftPane)), new ColumnDefinition(GridLength.Star)];
		RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)];
		_head.Padding = new Thickness(32, 28);
		_head.VerticalOptions = LayoutOptions.Center;
		Put(_wash, 0, 0, 3, 1);
		Put(_head, 0, 0, 3, 1);
		Put(_topBar, 0, 1, 1, 1);
		Put(_stageHost, 1, 1, 1, 1);
		Put(_bubble, 1, 1, 1, 1);
		Put(_foot, 2, 1, 1, 1);
		_topBar.Padding = new Thickness(32, 20, 32, 0);
		_stageHost.Margin = new Thickness(32, 12, 32, 0);
	}

	// Stacked, the bar is on its own row with the book title under it; two panes, one row: the title at the start, the bar at the end.
	private void PlaceTopBar()
	{
		Grid.SetRow(_bar, 0);
		Grid.SetColumn(_bar, 1);
		Grid.SetRow(_bookTitle, _wide ? 0 : 1);
		Grid.SetColumn(_bookTitle, 0);
		Grid.SetColumnSpan(_bookTitle, _wide ? 1 : 2);
		_bookTitle.Padding = _wide ? new Thickness(0) : new Thickness(8, 4, 0, 0);
	}

	private static void Put(BindableObject view, int row, int column, int rowSpan, int columnSpan)
	{
		SetRow(view, row);
		SetColumn(view, column);
		SetRowSpan(view, rowSpan);
		SetColumnSpan(view, columnSpan);
	}

	private Border BuildReplay()
	{
		var replay = new Border
		{
			AutomationId = "OnboardingReplay", StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.Brand), VerticalOptions = LayoutOptions.Center,
			Content = new OnboardingGlyph(OnboardingGlyph.Kind.Replay, Colors.White, 26, 2.4),
		};
		SemanticProperties.SetDescription(replay, AppResources.Onboarding_PlayAgain);
		Taps.AddTap(replay, () =>
		{
			ReplayTapped?.Invoke();
			return Task.CompletedTask;
		}, UiControls.Play);
		return replay;
	}

	private Border BuildPlayAgain()
	{
		var content = new HorizontalStackLayout
		{
			Spacing = 8, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new OnboardingGlyph(OnboardingGlyph.Kind.Replay, Color.FromArgb(PlayAgainInk), 18, 2.2),
				new Label { Text = AppResources.Onboarding_PlayAgain, FontSize = 14.5, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(PlayAgainInk), VerticalOptions = LayoutOptions.Center, FontFamily = DinoFonts.Ui },
			},
		};
		var button = new Border
		{
			AutomationId = "OnboardingPlayAgain", HeightRequest = 40, StrokeThickness = 2, Stroke = Color.FromArgb(OutlineStroke), BackgroundColor = Colors.Transparent, Padding = new Thickness(18, 0),
			StrokeShape = new RoundRectangle { CornerRadius = 20 }, Content = content,
		};
		SemanticProperties.SetDescription(button, AppResources.Onboarding_PlayAgain);
		Taps.AddTap(button, () =>
		{
			ReplayTapped?.Invoke();
			return Task.CompletedTask;
		}, UiControls.Play);
		return button;
	}

	// The bubble (mock N4b): ink card, 250 wide, with a pointer square rotated 45 degrees at its top edge towards the second language.
	private void BuildBubble()
	{
		var pointer = new Border
		{
			WidthRequest = 16, HeightRequest = 16, StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.Ink), Rotation = 45, HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 0, 46, 0), TranslationY = -8, StrokeShape = new RoundRectangle { CornerRadius = 3 },
		};
		var card = new Border
		{
			StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.Ink), Padding = new Thickness(14, 12), StrokeShape = new RoundRectangle { CornerRadius = 16 }, Content = _bubbleText,
			Shadow = new Shadow { Brush = new SolidColorBrush(DinoTokens.Get(DinoTokens.Ink)), Offset = new Point(0, 8), Radius = 20, Opacity = 0.35f },
		};
		_bubble.Children.Add(card);
		_bubble.Children.Add(pointer);
	}

	private void BuildFoot()
	{
		_footRight.Children.Add(_continue);
		if (_playAgain is not null)
		{
			_footRight.Children.Insert(0, _playAgain);
		}

		_foot.Add(_replay, 0);
		_foot.Add(_footRight, 1);
	}

	/// <summary>The heading and the line under it.</summary>
	public void SetTexts(string title, string body)
	{
		_title.Text = title;
		_body.Text = body;
		OwnScript.InOwnScriptAligned(_body);
	}

	/// <summary>The book's title line (translated when the book is shown in another language); it follows the language's direction.</summary>
	public void SetBookTitle(string title, string language)
	{
		_bookTitle.Text = title;
		_bookTitle.FontFamily = DinoFonts.IsRightToLeft(language) ? DinoFonts.Persian : DinoFonts.Ui;
		_bookTitle.FlowDirection = DinoFonts.IsRightToLeft(language) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
	}

	/// <summary>Continue is dim and does nothing until the step lets it (mock: opacity .45), then live.</summary>
	public void SetContinueLive(bool live)
	{
		_live = live;
		_continue.Opacity = live ? LiveOpacity : DimOpacity;
		_continue.InputTransparent = !live;
		SemanticProperties.SetHint(_continue, string.Empty);
	}

	public bool ContinueLive => _live;

	/// <summary>The language that plays now is the white segment.</summary>
	public void ShowPlaying(string language) => _bar.Select(language);

	/// <summary>The bubble and the pulse point at <paramref name="language"/>; null hides both.</summary>
	public void ShowBubble(string? language)
	{
		_bubble.IsVisible = language is not null;
		_bar.PointAt(language);
		if (language is not null)
		{
			_bubbleText.Text = AppResources.Format(AppResources.Onboarding_TapLanguage, LanguageNames.NativeName(language));
			SemanticProperties.SetDescription(_bubble, _bubbleText.Text);
		}
	}

	public bool IsWide => _wide;

	public OnboardingListenKind Kind => _kind;

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		(width, height) = WindowShapeSeam.Effective(DesktopTips.IsWindows, width, height);
		if (width > 0 && height > 0)
		{
			Place(OnboardingLayout.Shape(width, height, DesktopTips.IsWindows, intro: false));
		}
	}
}

/// <summary>The speaker chip on the picture (mock N4, N7): a pill with the sound-wave glyph and the speaker's name, ink for Dino's voices and coral for the grown-up's own ("Pim · your voice").</summary>
internal sealed class SpeakerChip : Border
{
	public SpeakerChip()
	{
		HeightRequest = 34;
		StrokeThickness = 0;
		Padding = new Thickness(10, 0, 12, 0);
		StrokeShape = new RoundRectangle { CornerRadius = 17 };
		HorizontalOptions = LayoutOptions.Start;
		VerticalOptions = LayoutOptions.Start;
		Margin = new Thickness(10, 10, 0, 0);
		InputTransparent = true;
		IsVisible = false;
		AutomationId = "OnboardingSpeaker";
		Shadow = new Shadow { Brush = new SolidColorBrush(DinoTokens.Get(DinoTokens.Ink)), Offset = new Point(0, 4), Radius = 12, Opacity = 0.3f };
	}

	/// <summary>Shows <paramref name="text"/> (null hides the chip); <paramref name="yourVoice"/> makes it coral.</summary>
	public void Show(string? text, bool yourVoice)
	{
		IsVisible = text is not null;
		if (text is null)
		{
			return;
		}

		BackgroundColor = DinoTokens.Get(yourVoice ? DinoTokens.Coral : DinoTokens.Ink);
		var label = new Label { Text = text, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center, FontFamily = DinoFonts.UiMediumForText(text) };
		Content = new HorizontalStackLayout { Spacing = 8, Children = { new OnboardingGlyph(OnboardingGlyph.Kind.SoundWave, Colors.White, 16, 2.2), label } };
		SemanticProperties.SetDescription(this, text);
	}
}

public partial class ReadingPage
{
	private OnboardingListenChrome? _onboarding;
	private OnboardingListenPlan? _plan;
	private ListenDemo _listen = new([]);
	private (int First, int Last)? _demoRange;
	private RecordBook? _demoBook;
	private Func<Task>? _demoContinued;
	private readonly SpeakerChip _chip = new();
	private IReadOnlyList<string> _demoLanguages = [];
	private readonly BusyGuard _demoBusy = new();
	private bool _switching;

	/// <summary>
	/// Slice 362 (mock N4a-N4d, WN4 and N7): the page plays one set page of its book in onboarding. It is shown in the picture view, plays by itself in the family's first story language, wears the onboarding chrome (step bar, language bar, replay,
	/// Continue) instead of its own back button, view switch and player row, and stops at the end of the demo page. Swipes, taps on a sentence and the back button do nothing. <paramref name="continued"/> runs when the grown-up taps Continue.
	/// </summary>
	internal void EnterOnboarding(OnboardingListenPlan plan, Func<Task> continued)
	{
		_plan = plan;
		_demoContinued = continued;
		ReadOrigin = ReadOrigins.Wizard; // the onboarding's reads say so, never "shelf" (the listen and "That's you" steps are the old wizard's successor; no separate onboarding value exists)
		MarkDemoRead(); // counts no book open or finish
		Load(plan.Story with { PreferredMode = ReadingMode.PictureAndText });
		_demoBook = RecordSplitBook.Of(plan.Story);
		_demoRange = OnboardingDemo.SentenceRange(_demoBook.Sentences, plan.Demo);
		if (_demoRange is { } range && range.First < _sentences.Count)
		{
			_session.MoveTo(range.First);
		}

		_demoLanguages = StoryLanguages.Current().All;
		_listen = new ListenDemo(_demoLanguages);
		Header.IsVisible = false;
		Transport.IsVisible = false;
		Dots.IsVisible = false;
		SurfaceHost.Children.Add(_chip);
		Content = null;
		_onboarding = new OnboardingListenChrome(plan, _demoLanguages, Root);
		_onboarding.ContinueTapped += () => _ = RunDemoAsync(ContinueDemoAsync);
		_onboarding.ReplayTapped += () => _ = RunDemoAsync(ReplayDemoAsync);
		_onboarding.LanguageTapped += code => _ = RunDemoAsync(() => SwitchDemoLanguageAsync(code));
		Content = _onboarding;
		_onboarding.SetBookTitle(plan.Story.Title, _session.ReadingLanguage);
		_listen.LanguageStarted(_demoLanguages[0]);
		SyncDemoChrome();
		DemoLog($"enter kind={plan.Kind} page={plan.Demo.Page} languages={string.Join(',', _demoLanguages)}");
		_ = StartDemoAsync();
	}

	// The first play must never throw unobserved: when it fails, Continue is opened (the page counts as heard) so the grown-up is never stuck on a step that has no Skip.
	private async Task StartDemoAsync()
	{
		try
		{
			await PlayDemoAsync();
		}
		catch (Exception ex)
		{
			// no-fallback: the demo could not start; the step goes on without it and the failure is logged
			Debug.WriteLine($"Dino Reader onboarding demo could not start: {ex}");
			_listen.PageEnded();
			SyncDemoChrome();
		}
	}

	// An async tap must never throw: a failure is logged and the page carries on.
	private static async Task RunDemoAsync(Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (Exception ex)
		{
			// no-fallback: a tap on the onboarding page that fails changes nothing the grown-up has done; Continue stays available
			Debug.WriteLine($"Dino Reader onboarding page action failed: {ex}");
		}
	}

	private async Task PlayDemoAsync()
	{
		await _appeared.Task;
		await _languageReady;
		if (_demoRange is not { } range)
		{
			_listen.PageEnded(); // a page with no sentences has nothing to play: Continue must not wait for it
			SyncDemoChrome();
			return;
		}

		_follower.ResumeAt(Math.Max(0, Surface.PageOfSentence(range.First)));
		await StartReadAtAsync(range.First);
		DemoLog("playing");
	}

	// Runs inside OnSentenceChanged: true when the read has gone past the demo page, which it stops (the surface stays on the page).
	private bool DemoSentenceChanged(int sentence)
	{
		if (_onboarding is null || _demoRange is not { } range || _demoBook is null)
		{
			return false;
		}

		if (sentence > range.Last || sentence < range.First)
		{
			_session.Stop(ReadStopReason.Cancelled);
			DemoPageEnded();
			return true;
		}

		ShowSpeaker(sentence);
		return false;
	}

	private void ShowSpeaker(int sentence)
	{
		if (_demoBook is null || _plan is null || sentence >= _demoBook.Sentences.Count)
		{
			_chip.Show(null, false);
			return;
		}

		string speaker = _demoBook.Sentences[sentence].Speaker;
		RecordSpeaker? who = _demoBook.Speakers.FirstOrDefault(s => s.Id == speaker);
		if (speaker == RecordSplit.Storyteller || who is null)
		{
			_chip.Show(null, false); // the narration has no chip
			return;
		}

		bool yours = _plan.Kind == OnboardingListenKind.HearYourself && speaker == _plan.Demo.Character;
		_chip.Show(yours ? AppResources.Format(AppResources.Onboarding_YourVoiceChip, who.Name) : who.Name, yours);
	}

	private void DemoPageEnded()
	{
		_chip.Show(null, false);
		_listen.PageEnded();
		SyncDemoChrome();
		DemoLog($"page-ended stage={_listen.Stage} continue={_listen.ContinueLive}");
	}

	private void DemoStateChanged()
	{
		if (_onboarding is not null && _session.Finished)
		{
			DemoPageEnded(); // a demo page that is the book's last one ends with the book
		}
	}

	private bool DemoHoldsThePage() => _onboarding is not null;

	private int? DemoFirstSentence() => _onboarding is not null ? _demoRange?.First : null;

	// The heading, line, bar, bubble and Continue all follow the stage of the listen machine (N4a to N4d) or the plain "That's you" screen (N7).
	private void SyncDemoChrome()
	{
		if (_onboarding is not { } chrome || _plan is null)
		{
			return;
		}

		chrome.ShowPlaying(_session.ReadingLanguage);
		if (_plan.Kind == OnboardingListenKind.HearYourself)
		{
			string who = _demoBook?.Speakers.FirstOrDefault(s => s.Id == _plan.Demo.Character)?.Name ?? _plan.Demo.Character;
			chrome.SetTexts(AppResources.Onboarding_ThatsYou, AppResources.Format(AppResources.Onboarding_ThatsYouBody, who));
			chrome.SetContinueLive(true);
			chrome.ShowBubble(null);
			return;
		}

		chrome.SetTexts(AppResources.Onboarding_ListenTitle, ListenBody(chrome));
		chrome.SetContinueLive(_listen.ContinueLive && !_switching);
		chrome.ShowBubble(_listen.BubbleShown ? _listen.Pointed : null);
	}

	private string ListenBody(OnboardingListenChrome chrome)
	{
		if (!_listen.HasOther)
		{
			return AppResources.Onboarding_ListenBody;
		}

		string first = LanguageNames.NativeName(_demoLanguages[0]);
		string second = LanguageNames.NativeName(_demoLanguages[1]);
		if (chrome.IsWide)
		{
			return AppResources.Format(AppResources.Onboarding_ListenWide, first, second);
		}

		return _listen.Stage switch
		{
			ListenStage.PointAtSecond => AppResources.Format(AppResources.Onboarding_ListenNext, first),
			ListenStage.Other => AppResources.Format(AppResources.Onboarding_ListenOther, LanguageNames.NativeName(_session.ReadingLanguage)),
			_ => AppResources.Onboarding_ListenBody,
		};
	}

	// A tap on a language restarts the demo page in it, from its first sentence; the pick is this reading's only (the child's own pick is left alone).
	private async Task SwitchDemoLanguageAsync(string language)
	{
		if (_story is not { } story || language == _session.ReadingLanguage)
		{
			return;
		}

		_switching = true; // Continue stays asleep until the second language has really started: the pack may still be downloading
		SyncDemoChrome();
		try
		{
			bool failed = false;
			EventHandler onFailed = (_, _) => failed = true; // the session swallows a read that cannot start and raises Failed: the language did not start
			_session.Failed += onFailed;
			try
			{
				await _wait.RunAsync(WaitStage.PreparingVoice, AppResources.Wait_GettingBook, canCancel: false, async _ =>
				{
					await SwitchLanguageAsync(story, language, restart: true, remember: false);
					return true;
				}); // the shared wait overlay shows only after 500 ms
			}
			finally
			{
				_session.Failed -= onFailed;
			}

			if (!failed)
			{
				_listen.LanguageStarted(language); // only a switch that really happened lets Continue go live
			}
		}
		finally
		{
			_switching = false;
		}

		SyncDemoChrome();
		DemoLog($"language={language} stage={_listen.Stage} continue={_listen.ContinueLive}");
	}

	private async Task ReplayDemoAsync()
	{
		if (_demoRange is not { } range)
		{
			return;
		}

		_session.Stop(ReadStopReason.Restarted);
		_follower.ResumeAt(Math.Max(0, Surface.PageOfSentence(range.First)));
		await StartReadAtAsync(range.First);
		DemoLog("replay");
	}

	/// <summary>Slice 362: the page is replaced as the window's root (never popped): the read stops and its handlers go, the same clean-up as leaving the stack. A page that never appeared has nothing to release.</summary>
	internal void ReleaseWhenReplaced()
	{
		if (_onStack)
		{
			LeavePage();
		}
	}

	// One Continue at a time: a double tap or a repeated Enter would show the next step twice (two roots, double counts, two deletions of the try).
	private async Task ContinueDemoAsync() => await _demoBusy.RunAsync(ContinueOnceAsync);

	private async Task ContinueOnceAsync()
	{
		if (_onboarding is not { ContinueLive: true } || _demoContinued is not { } continued)
		{
			return;
		}

		_session.Stop(ReadStopReason.LeftBook);
		DemoLog("continue");
		await continued();
	}

	// Windows (mock WN4): Enter is Continue, Space plays or pauses (a finished page plays again), Esc does nothing. The screen reports DinoScreen.FirstBooks while onboarding, so Enter is PickContinue and Space PickToggle.
	private bool OnboardingKey(DinoKeyAction action)
	{
		switch (action)
		{
			case DinoKeyAction.PickContinue:
				_ = RunDemoAsync(ContinueDemoAsync);
				return true;
			case DinoKeyAction.PickToggle:
				_ = RunDemoAsync(PlayOrPauseDemoAsync);
				return true;
			case DinoKeyAction.Back:
				return true; // the onboarding has no way back; the key is ours so the page below never pops
			default:
				return false;
		}
	}

	private Task PlayOrPauseDemoAsync()
	{
		switch (_session.State)
		{
			case SessionState.Playing:
				_session.Pause(TraceLines.ReadingReason.User);
				return Task.CompletedTask;
			case SessionState.Paused:
				_session.Resume();
				return Task.CompletedTask;
			default:
				return ReplayDemoAsync();
		}
	}

	private static void DemoLog(string line)
	{
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Onboarding] listen {line}");
#endif
		Debug.WriteLine($"Dino Reader onboarding listen {line}");
	}
}
