using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The 46x28 on/off switch of the Dino Reader v2 mock: a rounded track (off <c>#d9d2c4</c>, on the brand colour)
/// and a 24 white knob inset 2, at the start edge when off and the end edge when on, so a right-to-left screen
/// mirrors it (the mock's <c>inset-inline-start</c>).
/// </summary>
public sealed class ToggleSwitch : Border
{
	private const double TrackWidth = 46;
	private const double TrackHeight = 28;
	private const double KnobSize = 24;
	private const double KnobInset = 2;

	private readonly Border _knob;
	private bool _isOn;

	public ToggleSwitch()
	{
		WidthRequest = TrackWidth;
		HeightRequest = TrackHeight;
		StrokeThickness = 0;
		StrokeShape = new RoundRectangle { CornerRadius = TrackHeight / 2 };
		_knob = new Border
		{
			WidthRequest = KnobSize,
			HeightRequest = KnobSize,
			Margin = new Thickness(KnobInset),
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			VerticalOptions = LayoutOptions.Center,
			StrokeShape = new RoundRectangle { CornerRadius = KnobSize / 2 },
			Shadow = new Shadow { Brush = Brush.Black, Opacity = 0.2f, Radius = 3, Offset = new Point(0, 1) },
		};
		Content = new Grid { Children = { _knob } };
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => IsOn = !IsOn;
		GestureRecognizers.Add(tap);
		Apply();
	}

	public event EventHandler? Toggled;

	public bool IsOn
	{
		get => _isOn;
		set
		{
			if (_isOn == value)
			{
				return;
			}

			_isOn = value;
			Apply();
			Toggled?.Invoke(this, EventArgs.Empty);
		}
	}

	private void Apply()
	{
		BackgroundColor = DinoTokens.Get(_isOn ? DinoTokens.Brand : DinoTokens.SwitchOff);
		_knob.HorizontalOptions = _isOn ? LayoutOptions.End : LayoutOptions.Start;
		SemanticProperties.SetDescription(this, AppResources.Story_Translate);
	}
}
