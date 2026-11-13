using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// Step 2 of the first run (Slice 362, mock N2 and WN2): which languages the family reads in. The app language is ticked, grey and always on; up to two more can be ticked (<see cref="StoryLanguageSet"/>). Continue is live from the
/// start, there is no Skip; it saves the sides and goes on to the book picker. Stacked (under 600 wide) the explanation is a tinted card above the list; from 600 wide it is a paragraph in the left pane. The words come from the language just applied.
/// </summary>
public sealed class OnboardingLanguagesPage : ContentPage, IDinoKeyTarget
{
	private const double InfoCorner = 16;
	private const double InfoSize = 13.5;
	private const double InfoDot = 22;
	private const double HeadingStacked = 24;
	private const double HeadingWide = 30;
	private const double BodyStacked = 14.5;
	private const double BodyWide = 15.5;
	private const string InfoInk = "#2f2b6b";
	private const string WideBodyInk = "#4a4460";

	private readonly UsageCounter _usage;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();
	private readonly StoryLanguagePicker _picker;
	private readonly Label _heading = new() { Text = AppResources.Onboarding_LanguagesTitle, FontAttributes = FontAttributes.Bold, FontFamily = DinoFonts.Ui };
	private readonly Label _body = new() { LineHeight = 1.45, TextColor = DinoTokens.Get(DinoTokens.Body) };
	private readonly Label _wideBody = new() { LineHeight = 1.5 };
	private readonly Border _infoCard;
	private readonly CoralButton _continue = new() { Text = AppResources.Common_Continue, AutomationId = "OnboardingLanguagesContinue" };
	private readonly OnboardingShell _shell;
	private Size _windowSize;
	private OnboardingShape? _shape;

	public OnboardingLanguagesPage(UsageCounter usage, IServiceProvider services)
	{
		_usage = usage;
		_services = services;
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		BackgroundColor = DinoTokens.Get(DinoTokens.Cream);
		AutomationId = "OnboardingLanguages";
		StoryLanguageSet current = StoryLanguages.Current();
		_picker = new StoryLanguagePicker(current) { OnboardingLook = true };
		string main = StoryLanguages.NameInAppLanguage(current.Main);
		_body.Text = AppResources.Format(AppResources.Onboarding_LanguagesBody, main);
		_wideBody.Text = $"{_body.Text} {AppResources.Onboarding_WhyMoreBody}";
		_infoCard = BuildInfoCard();
		SemanticProperties.SetHeadingLevel(_heading, SemanticHeadingLevel.Level1);
		UiUsage.Press(_continue, UiControls.Continue, () => _ = _busy.RunAsync(ContinueAsync));
		var left = new VerticalStackLayout { Spacing = 12, Children = { _heading, _body, _infoCard, _wideBody } };
		var list = new ScrollView { Content = _picker };
		_shell = new OnboardingShell(left, list, _continue, stepBar: new OnboardingStepBar(OnboardingStep.StoryLanguages));
		Content = _shell;
	}

	/// <summary>Called when the grown-up goes back to the language page; the page's Back (Android, iOS swipe, Esc) runs it.</summary>
	public Func<Task>? BackRequested { get; set; }

	// The tint card of the stacked layout: a small info dot, the bold question and the explanation (mock N2: radius 16, fill the brand tint, 13.5 on #2f2b6b).
	private static Border BuildInfoCard()
	{
		var title = new Label { Text = AppResources.Onboarding_WhyMoreTitle, FontSize = InfoSize, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(InfoInk), FontFamily = DinoFonts.Ui };
		var text = new Label { Text = AppResources.Onboarding_WhyMoreBody, FontSize = InfoSize, LineHeight = 1.45, TextColor = Color.FromArgb(InfoInk), FontFamily = DinoFonts.Ui };
		var dot = new Border
		{
			WidthRequest = InfoDot, HeightRequest = InfoDot, StrokeThickness = 0, StrokeShape = new Ellipse(), BackgroundColor = DinoTokens.Get(DinoTokens.Brand), VerticalOptions = LayoutOptions.Start,
			Content = new Label { Text = "i", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};
		var row = new Grid { ColumnSpacing = 10, ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)] };
		row.Add(dot, 0);
		row.Add(new VerticalStackLayout { Spacing = 2, Children = { title, text } }, 1);
		return new Border { StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint), StrokeShape = new RoundRectangle { CornerRadius = InfoCorner }, Padding = new Thickness(14, 12), Content = row };
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		UiScreen.Show(UiScreens.StoryLanguages, this);
		LogShape();
	}

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
			Place(shape);
		}
	}

	// Stacked: the heading and line on top with the tint card, the list scrolling, Continue pinned; two panes: the heading, line and why-more paragraph left, the list and Continue right (WN2).
	private void Place(OnboardingShape shape)
	{
		_shell.Place(shape);
		(double width, double height) = OnboardingLayout.ContinueSize(shape);
		_continue.SetSize(width, height);
		_continue.HorizontalOptions = shape.Split ? LayoutOptions.End : LayoutOptions.Fill; // WN2: Continue sits right-aligned under the list
		_heading.FontSize = shape.Split ? HeadingWide : HeadingStacked;
		_body.FontSize = BodyStacked;
		_wideBody.FontSize = BodyWide;
		_wideBody.TextColor = Color.FromArgb(WideBodyInk);
		_body.IsVisible = !shape.Split;
		_infoCard.IsVisible = !shape.Split;
		_wideBody.IsVisible = shape.Split;
		LogShape();
	}

	private void LogShape()
	{
#if DEBUG
		if (_shape is { } shape)
		{
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Redesign] onboarding page=languages shape={(shape.Split ? "split" : "stack")}");
		}
#endif
	}

	private async Task ContinueAsync()
	{
		StoryLanguageSet before = StoryLanguages.Current();
		StoryLanguageSet set = _picker.Set;
		StoryLanguages.SaveSides(set);
		if (!set.Sides.SequenceEqual(before.Sides))
		{
			StoryLanguageCounts.RecordSet(_usage, set, UsageDetail.LanguagesFromIntro, set.Sides.Except(before.Sides)); // only languages new to this phone count as added
		}

		FirstBooksGate.StoryLanguagesPending = false; // the pick is saved first; a kill from here reopens on the book picker
		OnboardingUsage.StepDone(_usage, OnboardingStep.StoryLanguages, tried: set.Sides.Count > 0);
		await ShowBooksAsync();
	}

	// The picker takes its words from the language just applied; Back returns here; Completed carries on to the listen step.
	private async Task ShowBooksAsync()
	{
		FirstBooksPage page = _services.GetRequiredService<FirstBooksPage>();
		page.BackRequested = () => Navigation.PopAsync();
		page.Completed = () => OnboardingRoute.AfterBooksAsync(_services);
		await Navigation.PushAsync(page);
	}

	// Android Back, the iOS swipe-back and Esc on Windows return to the language page, which lets the first choice go.
	protected override bool OnBackButtonPressed()
	{
		if (BackRequested is not { } back)
		{
			return base.OnBackButtonPressed();
		}

		if (!_busy.IsBusy)
		{
			_ = _busy.RunAsync(back);
		}

		return true;
	}

	// Windows (mock WN2): Enter is Continue, Space ticks the row that has the focus, Esc goes back. Nothing reaches this page off Windows.
	DinoScreen IDinoKeyTarget.Screen => DinoScreen.FirstBooks;

	bool IDinoKeyTarget.Handle(DinoKeyAction action, DinoKey key)
	{
		switch (action)
		{
			case DinoKeyAction.PickToggle:
				return _picker.ToggleFocusedRow();
			case DinoKeyAction.PickContinue when !_continue.IsFocused:
				_ = _busy.RunAsync(ContinueAsync);
				return true;
			case DinoKeyAction.Back:
				return OnBackButtonPressed();
			default:
				return false;
		}
	}
}
