using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// The skeleton every first-run and intro page shares (Slice 325 part D, onboarding mock frames O1-O9): a left view (the picture or the heading), a right view and a foot button or bar.
/// One column under 600 dp wide (left on top, right scrolling, the foot pinned at the bottom), two panes from 600 (left on a washed pane, right and the foot beside it), or, for the waits,
/// the offline page and the ready page, one centred 440 dp column (the foot under the words on wide sizes, at the page's foot on a phone).
/// The pages hand it their views once; a size change only calls <see cref="Place"/>, so nothing the grown-up picked is ever rebuilt. Pure rules live in <see cref="OnboardingLayout"/>.
/// </summary>
internal sealed class OnboardingShell : Grid
{
	private const double StackSide = 20;
	private const double StackTop = 18;
	private const double StackBottom = 12;
	private const double WideCentredSide = 40;
	private const double WashOpacity = 0.05;
	private const double PhonePaneH = 22;
	private const double PhonePaneV = 28;
	private const double PhoneRightRight = 50;
	private const double PhoneRightLeft = 24;
	private const double PhoneRightTop = 22;
	private const double PhoneRightBottom = 26;
	private const double TabletPaneH = 48;
	private const double TabletPaneV = 56;
	private const double TabletRightH = 56;
	private const double TabletRightBottom = 44;
	private const double WindowsPaneH = 36;
	private const double WindowsPaneV = 20;
	private const double WindowsRightH = 40;
	private const double WindowsRightTop = 28;
	private const double WindowsRightBottom = 28;
	private const double FootGap = 12;
	private const double StepBarGap = 14;

	private readonly Border _wash = new() { StrokeThickness = 0, InputTransparent = true, IsVisible = false };
	private readonly Grid _leftHost = new();
	private readonly Grid _rightHost = new();
	private readonly Grid _footHost = new();
	private readonly bool _centred;

	/// <summary>Height of a bar the page floats over the top of the right pane when split (the intro bar, frame OW6); added to the right pane's top padding.</summary>
	public double RightTopInset { get; set; }

	/// <summary>Builds the shell. <paramref name="centred"/> shells (waits, offline) always show one centred column; <paramref name="right"/> and <paramref name="foot"/> may be null; <paramref name="stepBar"/> (Slice 362) goes above the left view.</summary>
	public OnboardingShell(View left, View? right, View? foot, bool centred = false, OnboardingStepBar? stepBar = null)
	{
		_centred = centred;
		_wash.BackgroundColor = Color.FromRgba(43 / 255.0, 37 / 255.0, 64 / 255.0, WashOpacity);
		_leftHost.Children.Add(stepBar is null ? left : new VerticalStackLayout { Spacing = StepBarGap, Children = { stepBar, left } }); // Slice 362: the step bar leads the left view (a pane's top when split)
		if (right is not null)
		{
			_rightHost.Children.Add(right);
		}

		if (foot is not null)
		{
			_footHost.Children.Add(foot);
		}

		Children.Add(_wash);
		Children.Add(_leftHost);
		Children.Add(_rightHost);
		Children.Add(_footHost);
		_rightHost.IsVisible = right is not null;
		_footHost.IsVisible = foot is not null;
	}

	/// <summary>Arranges the three views for <paramref name="shape"/>; cheap enough to call on every size change.</summary>
	public void Place(OnboardingShape shape)
	{
		ColumnDefinitions = [];
		RowDefinitions = [];
		if (_centred)
		{
			PlaceCentred(shape);
			return;
		}

		ResetHosts();
		if (shape.Split)
		{
			PlaceSplit(shape);
			return;
		}

		PlaceStack();
	}

	// The centred mode narrows and centres the hosts; the stack and split modes start from the defaults so nothing stale carries over.
	private void ResetHosts()
	{
		foreach (View host in new View[] { _leftHost, _rightHost, _footHost })
		{
			host.WidthRequest = -1;
			host.MaximumWidthRequest = double.PositiveInfinity;
			host.HorizontalOptions = LayoutOptions.Fill;
			host.VerticalOptions = LayoutOptions.Fill;
		}
	}

	private void PlaceStack()
	{
		Padding = new Thickness(StackSide, StackTop, StackSide, StackBottom);
		RowSpacing = 0;
		RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)];
		_wash.IsVisible = false;
		SetCell(_leftHost, 0, 0, 1, 1);
		SetCell(_rightHost, 1, 0, 1, 1);
		SetCell(_footHost, 2, 0, 1, 1);
		_leftHost.Padding = new Thickness(0);
		_rightHost.Padding = new Thickness(0);
		_footHost.Padding = new Thickness(0, FootGap, 0, 0);
		_leftHost.VerticalOptions = LayoutOptions.Start;
		_leftHost.HorizontalOptions = LayoutOptions.Fill;
		_footHost.HorizontalOptions = LayoutOptions.Fill;
	}

	private void PlaceSplit(OnboardingShape shape)
	{
		Padding = new Thickness(0);
		ColumnSpacing = 0;
		ColumnDefinitions = [new ColumnDefinition(new GridLength(shape.LeftPane)), new ColumnDefinition(GridLength.Star)];
		RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)];
		_wash.IsVisible = true;
		SetCell(_wash, 0, 0, 2, 1);
		SetCell(_leftHost, 0, 0, 2, 1);
		SetCell(_rightHost, 0, 1, 1, 1);
		SetCell(_footHost, 1, 1, 1, 1);
		(_leftHost.Padding, _rightHost.Padding, _footHost.Padding) = SplitPadding(shape.Family);
		_rightHost.Padding = new Thickness(_rightHost.Padding.Left, _rightHost.Padding.Top + RightTopInset, _rightHost.Padding.Right, _rightHost.Padding.Bottom);
		_leftHost.VerticalOptions = LayoutOptions.Center;
		_leftHost.HorizontalOptions = LayoutOptions.Fill;
		_footHost.HorizontalOptions = LayoutOptions.Fill;
	}

	// Frames OL1 (phone), OTL1 / OT2 (tablet) and OW2 (Windows): the paddings drawn for each pane, as left / right / foot.
	private static (Thickness Left, Thickness Right, Thickness Foot) SplitPadding(OnboardingFamily family) => family switch
	{
		OnboardingFamily.Windows => (
			new Thickness(WindowsPaneH, WindowsPaneV),
			new Thickness(WindowsRightH, WindowsRightTop, WindowsRightH, 0),
			new Thickness(WindowsRightH, FootGap, WindowsRightH, WindowsRightBottom)),
		OnboardingFamily.TabletLandscape or OnboardingFamily.TabletPortrait => (
			new Thickness(TabletPaneH, TabletPaneV),
			new Thickness(TabletRightH, TabletPaneV, TabletRightH, 0),
			new Thickness(TabletRightH, FootGap, TabletRightH, TabletRightBottom)),
		_ => (
			new Thickness(PhonePaneH, PhonePaneV),
			new Thickness(PhoneRightLeft, PhoneRightTop, PhoneRightRight, 0),
			new Thickness(PhoneRightLeft, FootGap, PhoneRightRight, PhoneRightBottom)),
	};

	private void PlaceCentred(OnboardingShape shape)
	{
		Padding = new Thickness(shape.Split ? WideCentredSide : StackSide, StackTop, shape.Split ? WideCentredSide : StackSide, StackBottom);
		_wash.IsVisible = false;
		bool underWords = shape.Split;
		RowDefinitions = underWords
			? [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)]
			: [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)];
		SetCell(_leftHost, 1, 0, 1, 1);
		SetCell(_rightHost, 2, 0, 1, 1);
		SetCell(_footHost, underWords ? 3 : 4, 0, 1, 1);
		_leftHost.Padding = new Thickness(0);
		_rightHost.Padding = new Thickness(0);
		_footHost.Padding = new Thickness(0, underWords ? FootGap * 2 : FootGap, 0, 0);
		foreach (View host in new View[] { _leftHost, _rightHost, _footHost })
		{
			host.HorizontalOptions = LayoutOptions.Center;
			host.MaximumWidthRequest = OnboardingLayout.CentredColumn;
		}

		_leftHost.VerticalOptions = LayoutOptions.Start;
		SizeCentredColumn(Width);
	}

	// A centred host is as wide as the column (440 at most, less the page's padding): a Center-aligned view would shrink to its text, and the foot button would not span the column.
	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		if (_centred)
		{
			SizeCentredColumn(width);
		}
	}

	private void SizeCentredColumn(double width)
	{
		if (!_centred || width <= 0)
		{
			return;
		}

		double column = OnboardingLayout.CentredWidth(width, Padding.Left);
		foreach (View host in new View[] { _leftHost, _rightHost, _footHost })
		{
			if (host.WidthRequest != column)
			{
				host.WidthRequest = column;
			}
		}
	}

	/// <summary>The width the right view gets in a window this wide: the page less the stack's sides, or the window less the left pane and the right pane's sides. Pages size their covers from it.</summary>
	public static double RightWidth(double windowWidth, OnboardingShape shape)
	{
		if (!shape.Split)
		{
			return Math.Max(0, windowWidth - (2 * StackSide));
		}

		Thickness right = SplitPadding(shape.Family).Right;
		return Math.Max(0, windowWidth - shape.LeftPane - right.Left - right.Right);
	}

	private static void SetCell(View view, int row, int column, int rowSpan, int columnSpan)
	{
		SetRow(view, row);
		SetColumn(view, column);
		SetRowSpan(view, rowSpan);
		SetColumnSpan(view, columnSpan);
	}
}
