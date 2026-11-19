using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using Sirous.Reading.Mobile.Shared.Waiting;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace DinoReader;

/// <summary>
/// The lock icons of the one shelf (Slice 418, decision 7, mock L1 and L4): plain strokes with no background. Closed is the 24-unit lock of the mock (the body at y 10.5, the shackle shut); open is the same lock with its
/// body one unit lower and the shackle lifted off its right side. Drawn on the mock's 24-unit grid and scaled to <c>size</c> (24 closed in ink on the bar, 30 open in brand purple, 26 in the rail, 15 in the chip).
/// </summary>
internal sealed class LockGlyph : ContentView
{
	private const double GridUnits = 24;
	private const string Closed = "M7.5 10.5h9A2.5 2.5 0 0 1 19 13v5a2.5 2.5 0 0 1-2.5 2.5h-9A2.5 2.5 0 0 1 5 18v-5a2.5 2.5 0 0 1 2.5-2.5z M8.5 10.5V8a3.5 3.5 0 0 1 7 0v2.5 M12 14.6v2";
	private const string Open = "M7.5 11h9A2.5 2.5 0 0 1 19 13.5v5a2.5 2.5 0 0 1-2.5 2.5h-9A2.5 2.5 0 0 1 5 18.5v-5A2.5 2.5 0 0 1 7.5 11z M15.5 11V5.5a3.5 3.5 0 0 0-7 0V7 M12 15v2";
	private static readonly PathGeometryConverter Converter = new();

	private readonly Path _path;
	private readonly Grid _grid;

	public LockGlyph(bool open, Color colour, double size, double stroke = 2.2)
	{
		InputTransparent = true;
		HorizontalOptions = LayoutOptions.Center;
		VerticalOptions = LayoutOptions.Center;
		_path = new Path { Stroke = new SolidColorBrush(colour), StrokeThickness = stroke, StrokeLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Aspect = Stretch.None };
		_grid = new Grid { WidthRequest = GridUnits, HeightRequest = GridUnits, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Children = { _path } };
		Content = _grid;
		Set(open, colour, size, stroke);
	}

	/// <summary>Draws the closed or the open lock in <paramref name="colour"/> at <paramref name="size"/> dp with a <paramref name="stroke"/> on the 24-unit grid.</summary>
	public void Set(bool open, Color colour, double size, double stroke = 2.2)
	{
		WidthRequest = size;
		HeightRequest = size;
		_grid.Scale = size / GridUnits;
		_path.Data = (Geometry)Converter.ConvertFromInvariantString(open ? Open : Closed)!;
		_path.Stroke = new SolidColorBrush(colour);
		_path.StrokeThickness = stroke;
	}
}

/// <summary>
/// The "Unlocked · 24s" chip next to the title (mock L4 to L6, WL2, Slice 418 decision 15): light purple, 26 high, the open lock at 15 and 12.5 bold words. It never takes a touch; the page sets the seconds once a second.
/// </summary>
internal sealed class UnlockChip : Border
{
	private const string Fill = "#ECE7FF";
	private const string Ink = "#3f3bc4";
	private const double Height = 26;
	private const double IconSize = 15;
	private const double TextSize = 12.5;

	private readonly Label _label = new() { FontSize = TextSize, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(Ink), VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.NoWrap };

	public UnlockChip()
	{
		AutomationId = "LockChip";
		InputTransparent = true;
		FlowDirection = FlowDirection.LeftToRight; // layout never mirrors (Slice 96)
		StrokeThickness = 0;
		BackgroundColor = Color.FromArgb(Fill);
		StrokeShape = new RoundRectangle { CornerRadius = Height / 2 };
		HeightRequest = Height;
		VerticalOptions = LayoutOptions.Center;
		Padding = new Thickness(6, 0, 10, 0);
		IsVisible = false;
		Content = new HorizontalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center, Children = { new LockGlyph(open: true, Color.FromArgb(Ink), IconSize, 2.4), _label } };
	}

	/// <summary>The chip's words for <paramref name="seconds"/> left ("Unlocked · 24s").</summary>
	public static string TextOf(int seconds) => AppResources.Format(AppResources.Lock_Countdown, seconds);

	public void SetSeconds(int seconds)
	{
		_label.Text = TextOf(seconds);
		SemanticProperties.SetDescription(this, AppResources.Lock_Unlocked); // the seconds are not read out every time they change
	}
}

/// <summary>
/// The lock's two small dark messages (mock L2 and L7, Slice 418): the hint over the lock when a faded tab is tapped ("Store and Settings are for grown-ups. Tap the lock to unlock.", the first sentence bold) and the
/// toast after a lock ("Locked again: ..." or "Locked."). Both are ink cards, never take a touch, and go by themselves. Placed by the gallery over the content's lower edge.
/// </summary>
internal sealed class LockOverlay : Grid
{
	private const string InkFill = "#2b2540";
	private const string AmberStroke = "#FFC24D";
	private static readonly TimeSpan HintTime = TimeSpan.FromSeconds(4);
	private static readonly TimeSpan ToastTime = TimeSpan.FromSeconds(3);

	private readonly Grid _hint = new() { AutomationId = "LockHint", IsVisible = false, WidthRequest = 240, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, 12, 20) };
	private readonly Label _hintText = new() { FontSize = 14, LineHeight = 1.4, TextColor = Colors.White, FontFamily = DinoFonts.Ui };
	private readonly Border _toast = new() { AutomationId = "LockToast", IsVisible = false, StrokeThickness = 0, BackgroundColor = Color.FromArgb(InkFill), MinimumHeightRequest = 48, Padding = new Thickness(14, 10), StrokeShape = new RoundRectangle { CornerRadius = 16 }, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.End, Margin = new Thickness(14, 0, 14, 10) };
	private readonly Label _toastText = new() { FontSize = 14, FontAttributes = FontAttributes.None, LineHeight = 1.35, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center, FontFamily = DinoFonts.Ui };
	private readonly TimedVisibility _hintShow;
	private readonly TimedVisibility _toastShow;

	public LockOverlay()
	{
		InputTransparent = true;
		CascadeInputTransparent = true;
		FlowDirection = FlowDirection.LeftToRight;
		_hintShow = new TimedVisibility(_hint, HintTime);
		_toastShow = new TimedVisibility(_toast, ToastTime);
		BuildHint();
		BuildToast();
		Children.Add(_hint);
		Children.Add(_toast);
	}

	private void BuildHint()
	{
		var pointer = new Border { WidthRequest = 16, HeightRequest = 16, StrokeThickness = 0, BackgroundColor = Color.FromArgb(InkFill), Rotation = 45, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, 30, -8), StrokeShape = new RoundRectangle { CornerRadius = 3 } };
		var card = new Border
		{
			StrokeThickness = 0, BackgroundColor = Color.FromArgb(InkFill), Padding = new Thickness(14, 12), StrokeShape = new RoundRectangle { CornerRadius = 16 }, Content = _hintText,
			Shadow = new Shadow { Brush = new SolidColorBrush(Color.FromArgb(InkFill)), Offset = new Point(0, 8), Radius = 20, Opacity = 0.35f },
		};
		_hint.Children.Add(card);
		_hint.Children.Add(pointer);
	}

	private void BuildToast()
	{
		var row = new HorizontalStackLayout { Spacing = 10, VerticalOptions = LayoutOptions.Center, Children = { new LockGlyph(open: false, Color.FromArgb(AmberStroke), 18, 2.4), _toastText } };
		_toast.Content = row;
	}

	/// <summary>Where the hint sits: over the lock at the bar's right (a bar), or at the content's lower left beside the rail's lock row (a rail).</summary>
	public void Place(bool rail)
	{
		_hint.HorizontalOptions = rail ? LayoutOptions.Start : LayoutOptions.End;
		_hint.Margin = rail ? new Thickness(12, 0, 0, 20) : new Thickness(0, 0, 12, 20);
	}

	/// <summary>Shows the hint (first sentence bold) for a few seconds.</summary>
	public void ShowHint(string text)
	{
		int cut = text.IndexOf(". ", StringComparison.Ordinal);
		var formatted = new FormattedString();
		if (cut > 0)
		{
			formatted.Spans.Add(new Span { Text = text[..(cut + 1)], FontAttributes = FontAttributes.Bold });
			formatted.Spans.Add(new Span { Text = text[(cut + 1)..] });
		}
		else
		{
			formatted.Spans.Add(new Span { Text = text });
		}

		_hintText.FormattedText = formatted;
		SemanticProperties.SetDescription(_hint, text);
		_hintShow.Show();
	}

	/// <summary>Shows the dark message (with the amber lock) for a few seconds.</summary>
	public void ShowToast(string text)
	{
		_toastText.Text = text;
		SemanticProperties.SetDescription(_toast, text);
		_toastShow.Show();
	}

	public void HideAll()
	{
		_hintShow.Hide();
		_toastShow.Hide();
	}
}

/// <summary>Makes a view visible for a while and hides it again; a newer <see cref="Show"/> restarts the time, <see cref="Hide"/> ends it at once.</summary>
internal sealed class TimedVisibility(View view, TimeSpan time)
{
	private CancellationTokenSource? _cts;

	public async void Show()
	{
		_cts?.Cancel();
		var mine = _cts = new CancellationTokenSource();
		view.IsVisible = true;
		try
		{
			await Task.Delay(time, mine.Token);
			view.IsVisible = false;
		}
		catch (OperationCanceledException)
		{
			// no-fallback: a newer message replaced this one, or the page hid it; whoever cancelled decides what shows
		}
	}

	public void Hide()
	{
		_cts?.Cancel();
		view.IsVisible = false;
	}
}

/// <summary>
/// The amber ring that pulses around the lock for two seconds when a faded tab is tapped (mock L2, Slice 418 decision 9). With reduce motion there is no pulse at all (the desk's decision for the mock's L2).
/// </summary>
internal sealed class LockPulse
{
	private static readonly TimeSpan PulseTime = TimeSpan.FromSeconds(2);

	private readonly PulseRing _ring = new(15) { IsVisible = false, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
	private CancellationTokenSource? _cts;

	public PulseRing Ring => _ring;

	/// <summary>Starts the ring for two seconds; does nothing when the system asks for reduced motion.</summary>
	public async void Run()
	{
		if (ReduceMotion.IsOn)
		{
			return;
		}

		_cts?.Cancel();
		var mine = _cts = new CancellationTokenSource();
		_ring.IsVisible = true;
		_ring.Start();
		try
		{
			await Task.Delay(PulseTime, mine.Token);
			Stop();
		}
		catch (OperationCanceledException)
		{
			// no-fallback: a newer pulse replaced this one; it stops the ring itself
		}
	}

	public void Stop()
	{
		_cts?.Cancel();
		_ring.Stop();
		_ring.IsVisible = false;
	}
}
