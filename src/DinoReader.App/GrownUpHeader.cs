using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The header row of every grown-up page (Slice 229, Batch 43 frames 104-108): a back button at the left, the title with an optional sub-line, and one main button at the
/// right, "Done" or, when the intro wizard pushed the page, "Continue" with a chevron. The row never mirrors, in any language (Slice 96): Back stays left and the button stays
/// right; only the words read in their own direction. The page decides what each tap does (<see cref="BackRequested"/>, <see cref="DoneRequested"/>); both go
/// to the same place unless the page has something to save first.
/// </summary>
public sealed class GrownUpHeader : Grid
{
	private const double Spacing = 6;
	private const double BackSize = 44;
	private const double BackIconSize = 22;
	private const double TitleSize = 20;
	private const double SubtitleSize = 12;
	private const double ButtonHeight = 44;
	private const double ButtonLabelSize = 15;
	private const double DonePadding = 20;
	private const double ContinuePaddingStart = 18;
	private const double ContinuePaddingEnd = 12;
	private const double ChevronSize = 16;
	private const double ChevronGap = 4;
	private const string DoneAutomationId = "GrownUpDone";

	private readonly Label _title = new() { FontSize = TitleSize, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
	private readonly Label _subtitle = new() { FontSize = SubtitleSize, IsVisible = false, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
	private readonly Border _button = new() { HeightRequest = ButtonHeight, StrokeThickness = 0, VerticalOptions = LayoutOptions.Center };
	private readonly Button _hit = new() { BackgroundColor = Colors.Transparent, TextColor = Colors.Transparent, BorderWidth = 0, Padding = 0 };
	private readonly Grid _face = new();
	private bool _continue;

	/// <summary>XAML needs a parameterless constructor; the id is the shared default.</summary>
	public GrownUpHeader() : this(DoneAutomationId)
	{
	}

	/// <param name="doneId">The Done/Continue button's automation id; pages that sit in one navigation stack give each its own so a flow taps the top one.</param>
	public GrownUpHeader(string doneId)
	{
		// MAUI allows an AutomationId to be set only once (a second set throws InvalidOperationException), so it is set here, the one place, and has no setter.
		_hit.AutomationId = doneId;
		FlowDirection = FlowDirection.LeftToRight; // never mirrored (Slice 96)
		Padding = new Thickness(Spacing, 8, 12, 10);
		ColumnSpacing = Spacing;
		ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)];
		SemanticProperties.SetHeadingLevel(_title, SemanticHeadingLevel.Level1);
		_subtitle.TextColor = DinoTokens.Get(DinoTokens.Muted);
		_button.StrokeShape = new RoundRectangle { CornerRadius = ButtonHeight / 2 };
		_button.BackgroundColor = DinoTokens.Get(DinoTokens.Brand);
		this.Add(BuildBack(), 0);
		this.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { _title, _subtitle } }, 1);
		this.Add(_button, 2);
		_face.Children.Add(_hit);
		_button.Content = _face;
		_hit.Clicked += (_, _) => DoneRequested?.Invoke(this, EventArgs.Empty);
		BuildButton();
	}

	/// <summary>The back button was tapped.</summary>
	public event EventHandler? BackRequested;

	/// <summary>The Done or Continue button was tapped.</summary>
	public event EventHandler? DoneRequested;

	/// <summary>The title label, for a page that gives it an automation id.</summary>
	public Label TitleLabel => _title;

	public string Title
	{
		get => _title.Text ?? string.Empty;
		set
		{
			_title.Text = value;
			CastText.InOwnScriptAligned(_title);
			CastText.AlignWith(_subtitle, _title);
		}
	}

	/// <summary>The line under the title; empty hides it.</summary>
	public string Subtitle
	{
		get => _subtitle.Text ?? string.Empty;
		set
		{
			_subtitle.Text = value;
			_subtitle.IsVisible = !string.IsNullOrEmpty(value);
			CastText.AlignWith(_subtitle, _title);
		}
	}

	/// <summary>False hides the Done/Continue button (the Add wizard's steps with nothing to save yet); Back stays.</summary>
	public bool ButtonVisible
	{
		get => _button.IsVisible;
		set => _button.IsVisible = value;
	}

	/// <summary>True when the intro wizard pushed this page: the button reads "Continue" (with a chevron) and means "back to the intro, next step".</summary>
	public bool Continue
	{
		get => _continue;
		set
		{
			if (_continue == value)
			{
				return;
			}

			_continue = value;
			BuildButton();
		}
	}

	private Border BuildBack()
	{
		var back = new Border
		{
			WidthRequest = BackSize,
			HeightRequest = BackSize,
			StrokeThickness = 0,
			BackgroundColor = Colors.Transparent,
			Content = new IconView { Kind = IconKind.Back, Color = DinoTokens.Get(DinoTokens.Ink), Size = BackIconSize, StrokeWidth = 2.6 },
		};
		SemanticProperties.SetDescription(back, AppResources.Common_Back);
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
		back.GestureRecognizers.Add(tap);
		return back;
	}

	private void BuildButton()
	{
		string text = _continue ? AppResources.Common_Continue : AppResources.Common_Done;
		var label = new Label { Text = text, FontSize = ButtonLabelSize, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center };
		var row = new HorizontalStackLayout { Spacing = ChevronGap, VerticalOptions = LayoutOptions.Center, Children = { label } };
		if (_continue)
		{
			row.Children.Add(new IconView { Kind = IconKind.Chevron, Color = Colors.White, Size = ChevronSize, StrokeWidth = 2.4, VerticalOptions = LayoutOptions.Center });
		}

		_button.Padding = _continue ? new Thickness(ContinuePaddingStart, 0, ContinuePaddingEnd, 0) : new Thickness(DonePadding, 0);
		// A transparent Button over the pill gives screen readers the button role, focus and Enter/Space activation.
		_hit.Text = text;
		if (_face.Children.Count > 1)
		{
			_face.Children.RemoveAt(0);
		}

		_face.Children.Insert(0, row);
	}
}
