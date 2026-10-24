using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The 52x32 on/off switch of the Dino Reader mock (Slice 107): a rounded track (off <c>#d9d2c4</c>, disabled <c>#e6e0d4</c>, on the brand colour)
/// and a 26 white knob inset 3, at the start edge when off and the end edge when on, so a right-to-left screen
/// mirrors it (the mock's <c>inset-inline-start</c>).
/// </summary>
public sealed class ToggleSwitch : Border
{
	private const double TrackWidth = 52;
	private const double TrackHeight = 32;
	private const double KnobSize = 26;
	private const double KnobInset = 3;

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
		tap.Tapped += (_, _) =>
		{
			if (IsEnabled)
			{
				IsOn = !IsOn;
			}
		};
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

	protected override void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
	{
		base.OnPropertyChanged(propertyName);
		if (propertyName == IsEnabledProperty.PropertyName && _knob is not null)
		{
			Apply();
		}
	}

	private void Apply()
	{
		BackgroundColor = TrackColor();
		_knob.HorizontalOptions = _isOn ? LayoutOptions.End : LayoutOptions.Start;
		SemanticProperties.SetDescription(this, AppResources.Story_Translate);
	}

	// Falls back to the plain on/off colours where there is no app (design time, a test host) or a token is missing.
	private Color TrackColor()
	{
		string key = !IsEnabled ? DinoTokens.SwitchDisabled : _isOn ? DinoTokens.Brand : DinoTokens.SwitchOff;
		if (Application.Current?.Resources.TryGetValue(key, out object? found) == true && found is Color color)
		{
			return color;
		}

		return !IsEnabled ? Colors.LightGray : _isOn ? Colors.SlateBlue : Colors.Gray;
	}
}
