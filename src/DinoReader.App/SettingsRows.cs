using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>The rows of the white cards on the add and edit screens (mock 9 and 8b): a title, a value or control at the end, an optional chevron.</summary>
public static class SettingsRows
{
	private const double RowHeight = 50;
	private const double RowPadding = 14;
	private const double RowFontSize = 14.5;
	private const double ChevronSize = 14;

	public static Label ValueLabel(string text) => new()
	{
		Text = text,
		FontSize = RowFontSize,
		FontAttributes = FontAttributes.Bold,
		TextColor = DinoTokens.Get(DinoTokens.Body),
		HorizontalTextAlignment = TextAlignment.End,
		VerticalOptions = LayoutOptions.Center,
	};

	public static IconView Chevron() => new()
	{
		Kind = IconKind.Chevron,
		Color = DinoTokens.Get(DinoTokens.Muted),
		Size = ChevronSize,
		StrokeWidth = 1.6,
		VerticalOptions = LayoutOptions.Center,
	};

	/// <summary>One row: <paramref name="title"/> at the start, <paramref name="end"/> after it, a chevron when <paramref name="tapped"/> is given.</summary>
	public static Grid Row(string title, View end, Func<Task>? tapped, BusyGuard busy)
	{
		var row = new Grid
		{
			MinimumHeightRequest = RowHeight,
			Padding = new Thickness(RowPadding, 0),
			ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)],
			ColumnSpacing = 8,
		};
		row.Add(new Label { Text = title, FontSize = RowFontSize, VerticalOptions = LayoutOptions.Center }, 0);
		row.Add(end, 1);
		if (tapped is not null)
		{
			row.Add(Chevron(), 2);
			var tap = new TapGestureRecognizer();
			tap.Tapped += async (_, _) => await busy.RunAsync(tapped);
			row.GestureRecognizers.Add(tap);
		}

		return row;
	}

	public static BoxView Divider() => new() { HeightRequest = 1, Color = DinoTokens.Get(DinoTokens.Hairline) };

	/// <summary>
	/// The "Show the intro again" row (Slice 155, mock frame 77): brand-coloured and bold, it replays the intro's three steps on top of
	/// <paramref name="navigation"/> and ends back there (never the language page or the voice download). The card that holds the Version
	/// row puts it first, above Version, followed by a <see cref="Divider"/>.
	/// </summary>
	public static Grid IntroReplayRow(INavigation navigation, IServiceProvider services, BusyGuard busy)
	{
		Grid row = Row(AppResources.Intro_ShowAgain, new BoxView { WidthRequest = 0, Color = Colors.Transparent }, () => IntroWizardPage.ReplayAsync(navigation, services), busy);
		if (row.Children.OfType<Label>().FirstOrDefault() is { } title)
		{
			title.FontAttributes = FontAttributes.Bold;
			title.TextColor = DinoTokens.Get(DinoTokens.Brand);
		}

		return row;
	}
}

/// <summary>The grown-up's app-wide "Animal sounds" switch (Slice 152, Decision 157): on when nothing is stored. The per-book <c>Story.AnimalSoundsOn</c> is kept in the format but ignored.</summary>
public static class AnimalSoundsPreference
{
	private const string Key = "dino.animal.sounds";

	public static bool Load()
	{
		try
		{
			return Preferences.Default.Get(Key, true);
		}
		catch (Exception)
		{
			// A value of another type under this key, or a preferences store that cannot be read: treat it as missing (on).
			return true;
		}
	}

	public static void Save(bool on) => Preferences.Default.Set(Key, on);
}

/// <summary>
/// The "Animal sounds" card of the grown-up settings (Slice 152, mock Batch 31 frame 70): a title, a helper line and a switch, on by default,
/// between the Reading speed and "Help make Dino better" cards. One setting for every book.
/// </summary>
public static class AnimalSoundsCard
{
	private const double CardRadius = 18;
	private const double CardMinHeight = 64;
	private const double CardPadding = 14;
	private const double TitleSize = 15;
	private const double HelperSize = 12.5;

	private static void SaveQuietly(bool on)
	{
		try
		{
			AnimalSoundsPreference.Save(on);
		}
		catch (Exception)
		{
			// The switch still shows the grown-up's choice; it is saved again on the next change.
		}
	}

	public static Border Build()
	{
		var toggle = new ToggleSwitch { IsOn = AnimalSoundsPreference.Load(), AccessibleName = AppResources.Settings_AnimalSounds_Title, VerticalOptions = LayoutOptions.Center };
		toggle.Toggled += (_, _) => SaveQuietly(toggle.IsOn);
		var text = new VerticalStackLayout
		{
			Spacing = 2,
			VerticalOptions = LayoutOptions.Center,
			Children =
			{
				CastText.InOwnScript(new Label { Text = AppResources.Settings_AnimalSounds_Title, FontSize = TitleSize, FontAttributes = FontAttributes.Bold }),
				CastText.InOwnScript(new Label { Text = AppResources.Settings_AnimalSounds_Sub, FontSize = HelperSize, TextColor = DinoTokens.Get(DinoTokens.Body) }),
			},
		};
		var row = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)], ColumnSpacing = 12 };
		row.Add(text, 0);
		row.Add(toggle, 1);
		return new Border
		{
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = CardRadius },
			MinimumHeightRequest = CardMinHeight,
			Padding = CardPadding,
			Content = row,
		};
	}
}

/// <summary>
/// The "Help make Dino better" card of the grown-up settings (Slice 133, Decision 141, mock frame 62): its own white card with a title, a
/// helper line saying what is counted, and the usage-counts switch. On by default; off sends nothing. A grown-up page puts it in its column of cards.
/// </summary>
public static class UsageCountsCard
{
	private const double CardRadius = 18;
	private const double CardMinHeight = 64;
	private const double CardPadding = 14;
	private const double TitleSize = 15;
	private const double HelperSize = 12.5;

	public static Border Build(UsageCounter counter)
	{
		var toggle = new ToggleSwitch { IsOn = counter.Enabled, AccessibleName = AppResources.Usage_Title, VerticalOptions = LayoutOptions.Center };
		toggle.Toggled += (_, _) => counter.Enabled = toggle.IsOn;
		var text = new VerticalStackLayout
		{
			Spacing = 2,
			VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new Label { Text = AppResources.Usage_Title, FontSize = TitleSize, FontAttributes = FontAttributes.Bold },
				new Label { Text = AppResources.Usage_Helper, FontSize = HelperSize, TextColor = DinoTokens.Get(DinoTokens.Body) },
			},
		};
		var row = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)], ColumnSpacing = 12 };
		row.Add(text, 0);
		row.Add(toggle, 1);
		return new Border
		{
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = CardRadius },
			MinimumHeightRequest = CardMinHeight,
			Padding = CardPadding,
			Content = row,
		};
	}
}

/// <summary>
/// The "Reading speed" card of the grown-up settings (Slice 150, Decision 155, mock frame 70): a title, the chosen speed, a Play button that
/// reads a sample sentence at it, and a slider from 0.5x to 1.2x in 0.05 steps. One setting for the whole app; the reader takes a change from
/// the next sentence. A page puts it in its column of cards and supplies <c>playSample</c> (it knows the voice).
/// </summary>
public static class ReadingSpeedCard
{
	private const double CardRadius = 18;
	private const double CardPadding = 14;
	private const double TitleSize = 15;
	private const double HelperSize = 12.5;
	private const double ValueSize = 17;
	private const double ValueMinWidth = 44;
	private const double PlaySize = 44;
	private const double SpinnerSize = 22;
	private const double SliderHeight = 44;
	private const double EndLabelSize = 12;
	private const double StepsPerUnit = 1 / ReadingSpeed.Step;
	private const double SnapTolerance = 1e-9;
	private const int SpinnerDelayMs = 500;

	public static Border Build(ISpeechReader reader, Func<Task> playSample, Action stopSample)
	{
		double current = ReadingSpeedPreference.Load().Value;
		Label value = ValueLabel(current);
		var column = new VerticalStackLayout { Spacing = 10, Children = { TopRow(value, PlayButton(reader, playSample, stopSample)), Slider(current, value, reader), EndsRow() } };
		return new Border
		{
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = CardRadius },
			Padding = CardPadding,
			Content = column,
		};
	}

	private static Label ValueLabel(double current) => new()
	{
		Text = Display(current),
		FontSize = ValueSize,
		FontAttributes = FontAttributes.Bold,
		TextColor = DinoTokens.Get(DinoTokens.Brand),
		MinimumWidthRequest = ValueMinWidth,
		HorizontalTextAlignment = TextAlignment.End,
		VerticalOptions = LayoutOptions.Center,
	};

	private static Grid TopRow(Label value, Border play)
	{
		var top = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)], ColumnSpacing = 10 };
		top.Add(TitleBlock(), 0);
		top.Add(value, 1);
		top.Add(play, 2);
		return top;
	}

	private static Grid EndsRow()
	{
		var ends = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		ends.Add(EndLabel(AppResources.Speed_EndSlow), 0);
		ends.Add(EndLabel(AppResources.Format(AppResources.Speed_Value, ReadingSpeed.Max)), 1);
		return ends;
	}

	private static string Display(double speed) => AppResources.Format(AppResources.Speed_Value, speed);

	private static VerticalStackLayout TitleBlock() => new()
	{
		Spacing = 2,
		VerticalOptions = LayoutOptions.Center,
		Children =
		{
			new Label { Text = AppResources.Speed_Title, FontSize = TitleSize, FontAttributes = FontAttributes.Bold },
			new Label { Text = AppResources.Speed_Sub, FontSize = HelperSize, TextColor = DinoTokens.Get(DinoTokens.Body) },
		},
	};

	private static Label EndLabel(string text) => new() { Text = text, FontSize = EndLabelSize, TextColor = DinoTokens.Get(DinoTokens.Muted) };

	private static Slider Slider(double current, Label value, ISpeechReader reader)
	{
		var slider = new Slider
		{
			Minimum = ReadingSpeed.Min,
			Maximum = ReadingSpeed.Max,
			Value = current,
			HeightRequest = SliderHeight,
			MinimumTrackColor = DinoTokens.Get(DinoTokens.Brand),
			MaximumTrackColor = DinoTokens.Get(DinoTokens.BrandTint),
			ThumbColor = DinoTokens.Get(DinoTokens.Brand),
		};
		SemanticProperties.SetDescription(slider, Described(current));
		WireSlider(slider, value, reader);
		return slider;
	}

	// A drag commits once when it ends; any other change (TalkBack, keyboard) commits at once. A drag that never reports its end
	// (page torn down) is closed on Unloaded so the flag cannot stay set.
	private static void WireSlider(Slider slider, Label value, ISpeechReader reader)
	{
		bool dragging = false;
		slider.DragStarted += (_, _) => dragging = true;
		slider.DragCompleted += (_, _) =>
		{
			dragging = false;
			Commit(slider, reader);
		};
		slider.Unloaded += (_, _) =>
		{
			if (dragging)
			{
				dragging = false;
				Commit(slider, reader);
			}
		};
		slider.ValueChanged += (_, e) =>
		{
			if (!Snap(slider, e.NewValue))
			{
				return;
			}

			value.Text = Display(slider.Value);
			SemanticProperties.SetDescription(slider, Described(slider.Value));
			if (!dragging)
			{
				Commit(slider, reader);
			}
		};
	}

	// The title plus the spoken value, so a screen reader says "Reading speed, 0.8x" rather than a raw percentage.
	private static string Described(double speed) => $"{AppResources.Speed_Title}, {Display(speed)}";

	// Puts the thumb on a step; false when it had to move (the move raises ValueChanged again).
	private static bool Snap(Slider slider, double raw)
	{
		PlaybackRate rate = ReadingSpeed.Clamp(Math.Round(raw * StepsPerUnit) / StepsPerUnit);
		if (Math.Abs(slider.Value - rate.Value) <= SnapTolerance)
		{
			return true;
		}

		slider.Value = rate.Value;
		return false;
	}

	// Saves and applies the speed once the thumb settles, not on every tick of a drag (the reader takes it from the next sentence).
	private static void Commit(Slider slider, ISpeechReader reader)
	{
		PlaybackRate rate = ReadingSpeed.Clamp(slider.Value);
		ReadingSpeedPreference.Save(rate);
		reader.SetSpeed(rate);
	}

	// Slice 150 review: the play icon becomes a spinner only when the sample takes over 500 ms (no flash on a fast one).
	private static async Task PlayWithSpinnerAsync(Border button, Func<Task> playSample)
	{
		View icon = button.Content;
		using var fast = new CancellationTokenSource();
		_ = ShowSpinnerAfterDelayAsync(button, fast.Token);
		try
		{
			await playSample();
		}
		finally
		{
			fast.Cancel();
			button.Content = icon;
		}
	}

	private static async Task ShowSpinnerAfterDelayAsync(Border button, CancellationToken token)
	{
		try
		{
			await Task.Delay(SpinnerDelayMs, token);
		}
		catch (OperationCanceledException)
		{
			return;
		}

		button.Content = new ActivityIndicator { IsRunning = true, Color = DinoTokens.Get(DinoTokens.Brand), WidthRequest = SpinnerSize, HeightRequest = SpinnerSize, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
	}

	private static Border PlayButton(ISpeechReader reader, Func<Task> playSample, Action stopSample)
	{
		var button = new Border
		{
			WidthRequest = PlaySize,
			HeightRequest = PlaySize,
			StrokeThickness = 0,
			BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
			StrokeShape = new RoundRectangle { CornerRadius = PlaySize / 2 },
			VerticalOptions = LayoutOptions.Center,
			Content = new IconView { Kind = IconKind.Play, Color = DinoTokens.Get(DinoTokens.Brand), Filled = true, Size = 16 },
		};
		SemanticProperties.SetDescription(button, AppResources.Speed_Play);
		bool starting = false;
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) =>
		{
			// A tap while the sample is preparing or playing stops it (the Cancel).
			if (starting || reader.State != PlaybackState.Idle)
			{
				stopSample();
				return;
			}

			starting = true;
			try
			{
				await PlayWithSpinnerAsync(button, playSample);
			}
			finally
			{
				starting = false;
			}
		};
		button.GestureRecognizers.Add(tap);
		return button;
	}
}
