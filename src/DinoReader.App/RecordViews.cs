using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>Where the piece on the Record screen is in its life (Slice 279, mock frames 180 to 182): ready to record, recording, or recorded.</summary>
internal enum RecordStep
{
	Ready,
	Recording,
	Recorded,
}

/// <summary>The little label that sits beside the speaker: nothing, "Recorded" (green) or "New take" (amber).</summary>
internal enum RecordChip
{
	None,
	Recorded,
	NewTake,
}

/// <summary>What the words card shows: the "Rex says:" line (null for a whole page), the words, their font and size, the chip, and whether a recording is running (a red ring).</summary>
internal sealed record RecordCardView(string? Says, string Words, string Language, double FontSize, RecordChip Chip, bool Recording, bool ReplacesRest = false);

/// <summary>What the button row shows and does. <see cref="Clock"/> is the running time, drawn under the stop square while recording.</summary>
internal sealed record RecordButtons(
	RecordStep Step, bool Playing, bool IsLast, bool Listening, bool Windows, Label? Clock,
	Func<Task> Skip, Func<Task> Record, Func<Task> Stop, Func<Task> Redo, Func<Task> Play, Func<Task> Next);

/// <summary>The parts of the Record screen (Slice 111, redrawn by Slice 279 from the Batch 48 mock, frames 180 to 191), built as plain views so <see cref="RecordPage"/> only decides which to show.</summary>
internal static class RecordViews
{
	private const double PictureRadius = 18;
	private const double CardRadius = 16;
	private const double SmallButton = 56;
	private const double BigButton = 80;
	private const double WindowsBigButton = 76;
	private const double MicShare = 34.0 / BigButton;
	private const double StopSquare = 26;
	private const double StopCorner = 6;
	private const double RingThickness = 3;
	private const double PulseMilliseconds = 1400;
	private const double PulseScale = 1.45;
	private const double LabelSize = 12.5;
	private const double ChipHeight = 24;
	private const double QuietHeight = 44;
	private const double WaveHeight = 28;
	private const double WaveBarWidth = 3;
	private const double WaveGap = 2;
	private const int WaveBars = 28;
	private const float CardAlpha = 0.96f;
	private const string PulseAnimation = "RecordPulse";

	// Colours the mock draws that the tokens do not hold (Batch 48 frames 182, 190 and 191).
	private static readonly Color RecordedFill = Color.FromArgb("#DFF5E3");
	private static readonly Color RecordedInk = Color.FromArgb("#2E7D32");
	private static readonly Color NewTakeFill = Color.FromArgb("#FFF0C2");
	private static readonly Color NewTakeInk = Color.FromArgb("#8A5A00");
	private static readonly Color UseDinoInk = Color.FromArgb("#C8402A");
	private static readonly Color QuietFill = Color.FromArgb("#F3EFE6");
	private static readonly Color QuietInk = Color.FromArgb("#5a5470");
	private static readonly Color CardShadow = Color.FromArgb("#2B2540");

	/// <summary>
	/// AutomationIds for the scripted check: the controls, the card's words and speaker, the clock, the intro's parts.
	/// </summary>
	internal const string RecordButtonId = "RecordButton";
	internal const string StopButtonId = "RecordStopButton";
	internal const string ClockId = "RecordClock";
	internal const string SkipId = "RecordSkip";
	internal const string NextId = "RecordNext";
	internal const string PlayId = "RecordPlay";
	internal const string RedoId = "RecordRedo";
	internal const string SaysId = "RecordSays";
	internal const string ChipId = "RecordChip";
	internal const string ReplacesRestId = "RecordReplacesRest";
	internal const string OldTakeId = "RecordOldTake";
	internal const string KeepOldId = "RecordKeepOld";
	internal const string UseDinoId = "RecordUseDino";
	internal const string UndoId = "RecordUndo";

	/// <summary>The AutomationId of the card's words label, for the scripted check.</summary>
	internal const string PageWordsId = "RecordPageWords";

	/// <summary>The words of a page: its <c>Text</c>, trimmed.</summary>
	internal static string PageWords(StoryPage page) => page.Text.Trim();

	// ---- the stage: the picture with the words card on its lower part (frames 180 to 183) ----

	/// <summary>The picture cropped from the top with the card over its lower part; a page with no picture shows the card alone, at the bottom of the same space.</summary>
	public static Grid Stage(string? picturePath, View card, double maxCardHeight)
	{
		var stage = new Grid { Margin = new Thickness(RecordCard.PictureSideMargin, 0) };
		if (picturePath is not null)
		{
			stage.Add(PictureBand(picturePath));
		}

		card.VerticalOptions = LayoutOptions.End;
		card.Margin = new Thickness(picturePath is null ? 0 : RecordCard.CardInset);
		if (card is VisualElement element)
		{
			element.MaximumHeightRequest = maxCardHeight;
		}

		stage.Add(card);
		return stage;
	}

	private static View PictureBand(string picturePath)
	{
		double aspect = PageImageSize.Read(picturePath) is { Height: > 0 } size ? size.Width / size.Height : 0;
		var image = new Image { Source = ImageSource.FromFile(picturePath), Aspect = Aspect.AspectFill };
		var host = new AbsoluteLayout { Children = { image } };
		AbsoluteLayout.SetLayoutFlags(image, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
		AbsoluteLayout.SetLayoutBounds(image, new Rect(0, 0, 1, 1));
		host.SizeChanged += (_, _) => CoverTop(host, image, aspect);
		return new Border { StrokeThickness = 0, BackgroundColor = Colors.Transparent, StrokeShape = new RoundRectangle { CornerRadius = PictureRadius }, Content = host };
	}

	// The picture covers its area and keeps its top (the books' art leaves the bottom free for words); an unknown shape stays filled and cropped.
	private static void CoverTop(AbsoluteLayout host, Image image, double aspect)
	{
		var frame = PictureTextBook.CoverFromTop(host.Width, host.Height, aspect);
		AbsoluteLayout.SetLayoutFlags(image, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
		AbsoluteLayout.SetLayoutBounds(image, new Rect(frame.X, frame.Y, frame.Width, frame.Height));
		image.Aspect = aspect > 0 ? Aspect.Fill : Aspect.AspectFill;
	}

	// ---- the words card ----

	/// <summary>The card (frames 180 to 183, 191): the speaker line with the chip, the words as the book shows them, and, over a kept recording, the two quiet buttons of a new take.</summary>
	public static Border WordsCard(RecordCardView view, Func<Task>? oldTake, Func<Task>? keepOld)
	{
		var column = new VerticalStackLayout { Spacing = 6 };
		View? top = SpeakerRow(view);
		if (top is not null)
		{
			column.Add(top);
		}

		if (view.ReplacesRest)
		{
			column.Add(ReplacesRestNote());
		}

		column.Add(WordsLabel(view));
		if (view.Chip == RecordChip.NewTake && oldTake is not null && keepOld is not null)
		{
			column.Add(TakeButtons(oldTake, keepOld));
		}

		return new Border
		{
			StrokeThickness = view.Recording ? RingThickness : 0,
			Stroke = DinoTokens.Get(DinoTokens.Danger),
			BackgroundColor = Colors.White.WithAlpha(CardAlpha),
			Padding = new Thickness(RecordCard.CardPaddingX, RecordCard.CardPaddingY),
			StrokeShape = new RoundRectangle { CornerRadius = CardRadius },
			Shadow = new Shadow { Brush = new SolidColorBrush(CardShadow), Offset = new Point(0, 4), Radius = 12, Opacity = 0.16f },
			Content = column,
		};
	}

	// One quiet line (Slice 279): the new take is narrower than the old clip, so the old clip's other sentences lose their recording when the take is kept.
	private static Label ReplacesRestNote() => new()
	{
		AutomationId = ReplacesRestId, Text = AppResources.Record_ReplacesRest, FontSize = 12, TextColor = DinoTokens.Get(DinoTokens.Muted),
	};

	private static View? SpeakerRow(RecordCardView view)
	{
		if (view.Says is null && view.Chip == RecordChip.None)
		{
			return null;
		}

		var row = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)], MinimumHeightRequest = RecordCard.SaysRowHeight };
		if (view.Says is not null)
		{
			var says = new Label
			{
				AutomationId = SaysId, Text = view.Says, FontSize = 13.5, FontAttributes = FontAttributes.Bold,
				TextColor = DinoTokens.Get(DinoTokens.Brand), VerticalOptions = LayoutOptions.Center,
			};
			OwnScript.InOwnScript(says);
			row.Add(says, 0);
		}

		if (view.Chip != RecordChip.None)
		{
			row.Add(Chip(view.Chip), 1);
		}

		return row;
	}

	private static Border Chip(RecordChip chip)
	{
		bool recorded = chip == RecordChip.Recorded;
		string text = recorded ? AppResources.Record_Recorded : AppResources.Record_NewTake;
		var label = new Label { Text = text, FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = recorded ? RecordedInk : NewTakeInk, VerticalOptions = LayoutOptions.Center };
		var content = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
		if (recorded)
		{
			content.Add(new IconView { Kind = IconKind.Check, Size = 13, StrokeWidth = 2.6, Color = RecordedInk, VerticalOptions = LayoutOptions.Center });
		}

		content.Add(label);
		return new Border
		{
			AutomationId = ChipId, HeightRequest = ChipHeight, Padding = new Thickness(10, 0), StrokeThickness = 0,
			BackgroundColor = recorded ? RecordedFill : NewTakeFill, VerticalOptions = LayoutOptions.Center,
			StrokeShape = new RoundRectangle { CornerRadius = ChipHeight / 2 },
			Content = content,
		};
	}

	private static Label WordsLabel(RecordCardView view) => new()
	{
		AutomationId = PageWordsId, Text = view.Words, FontFamily = DinoFonts.ReadingFor(view.Language), FontSize = view.FontSize, LineHeight = RecordCard.LineHeightFactor,
		FontAutoScalingEnabled = false, TextColor = DinoTokens.Get(DinoTokens.Ink),
		FlowDirection = DinoFonts.IsRightToLeft(view.Language) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
	};

	private static HorizontalStackLayout TakeButtons(Func<Task> oldTake, Func<Task> keepOld)
	{
		var row = new HorizontalStackLayout { Spacing = 10, Margin = new Thickness(0, 6, 0, 0) };
		row.Add(QuietButton(OldTakeId, AppResources.Record_OldTake, oldTake, withPlay: true));
		row.Add(QuietButton(KeepOldId, AppResources.Record_KeepOld, keepOld, withPlay: false));
		return row;
	}

	private static Border QuietButton(string id, string text, Func<Task> tapped, bool withPlay)
	{
		var content = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
		if (withPlay)
		{
			content.Add(new IconView { Kind = IconKind.Play, Filled = true, Size = 11, Color = QuietInk, VerticalOptions = LayoutOptions.Center });
		}

		content.Add(new Label { Text = text, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = QuietInk, VerticalOptions = LayoutOptions.Center });
		var button = new Border
		{
			AutomationId = id, HeightRequest = QuietHeight, Padding = new Thickness(16, 0), StrokeThickness = 0, BackgroundColor = QuietFill,
			StrokeShape = new RoundRectangle { CornerRadius = QuietHeight / 2 }, Content = content,
		};
		SemanticProperties.SetDescription(button, text);
		Taps.AddTap(button, tapped, UiControls.Pick);
		return button;
	}

	// ---- the buttons (frames 180 to 182, 184) ----

	/// <summary>The row under the stage: Skip, Record, Next while nothing is recorded; Skip and Next wait while recording; Re-record, Play, Next once recorded. Every button has its label under it.</summary>
	public static Grid Controls(RecordButtons b)
	{
		var row = new Grid
		{
			ColumnSpacing = 22, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)],
		};
		row.Add(LeftButton(b), 0);
		row.Add(MiddleButton(b), 1);
		row.Add(RightButton(b), 2);
		return row;
	}

	private static View LeftButton(RecordButtons b) => b.Step == RecordStep.Recorded
		? Labelled(RoundIcon(RedoId, IconKind.Mic, SmallButton, DinoTokens.Get(DinoTokens.BrandTint), DinoTokens.Get(DinoTokens.Brand), AppResources.Rec_Redo, b.Redo, enabled: true), Caption(AppResources.Rec_Redo), SmallButton)
		: Labelled(RoundIcon(SkipId, IconKind.SkipForward, SmallButton, DinoTokens.Get(DinoTokens.BrandTint), DinoTokens.Get(DinoTokens.Brand), AppResources.Record_Skip, b.Skip, enabled: b.Step == RecordStep.Ready && !b.Listening),
			Caption(b.Windows ? AppResources.Record_SkipKey : AppResources.Record_Skip), SmallButton);

	private static View MiddleButton(RecordButtons b)
	{
		double size = b.Windows ? WindowsBigButton : BigButton;
		return b.Step switch
		{
			RecordStep.Recording => Labelled(StopButton(size, b.Stop), b.Clock ?? Caption(string.Empty), size),
			RecordStep.Recorded => Labelled(PlayButton(size, b.Playing, b.Play), Caption(b.Playing ? AppResources.Record_Playing : AppResources.Record_Play), size),
			_ => Labelled(RecordButton(size, b.Record), Caption(b.Windows ? AppResources.Record_RecordKey : AppResources.Rec_Start), size),
		};
	}

	private static View RightButton(RecordButtons b)
	{
		bool recorded = b.Step == RecordStep.Recorded;
		string text = b.IsLast ? AppResources.Record_Finish : AppResources.Record_Next;
		Color fill = recorded ? DinoTokens.Get(DinoTokens.Brand) : DinoTokens.Get(DinoTokens.BrandTint);
		Color ink = recorded ? Colors.White : DinoTokens.Get(DinoTokens.Brand);
		Border next = RoundIcon(NextId, b.IsLast ? IconKind.Check : IconKind.Chevron, SmallButton, fill, ink, text, b.Next, enabled: recorded);
		return Labelled(next, Caption(b.Windows && !b.IsLast ? AppResources.Record_NextKey : text), SmallButton);
	}

	private static Label Caption(string text) => new()
	{
		Text = text, FontSize = LabelSize, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Body),
		HorizontalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.Center, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation,
	};

	/// <summary>The running time under the stop square.</summary>
	internal static Label ClockLabel() => new()
	{
		AutomationId = ClockId, FontSize = LabelSize, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Danger),
		HorizontalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.Center,
	};

	private static VerticalStackLayout Labelled(View button, View label, double width) => new()
	{
		Spacing = 6, WidthRequest = Math.Max(width, 84), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
		Children = { Centred(button), label },
	};

	private static View Centred(View button)
	{
		button.HorizontalOptions = LayoutOptions.Center;
		return button;
	}

	private static Border RoundIcon(string id, IconKind kind, double size, Color fill, Color ink, string description, Func<Task> tapped, bool enabled)
	{
		var button = Circle(size, fill);
		button.AutomationId = id;
		button.Opacity = enabled ? 1 : 0.4;
		button.Content = new IconView { Kind = kind, Size = size * 0.4, StrokeWidth = 2.4, Color = ink, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		SemanticProperties.SetDescription(button, description);
		button.IsEnabled = enabled; // AddTap reads the live IsEnabled: a greyed-out round icon is not counted
		Taps.AddTap(button, tapped, UiControls.Pick);
		DesktopTips.Hover(button);
		return button;
	}

	private static View RecordButton(double size, Func<Task> tapped)
	{
		Border button = Circle(size, DinoTokens.Get(DinoTokens.Danger));
		button.AutomationId = RecordButtonId;
		button.Shadow = new Shadow { Brush = new SolidColorBrush(DinoTokens.Get(DinoTokens.RecordEdge)), Offset = new Point(0, 5), Radius = 0, Opacity = 1f };
		button.Content = new IconView { Kind = IconKind.Mic, Color = Colors.White, Size = size * MicShare, StrokeWidth = 2.4, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		SemanticProperties.SetDescription(button, AppResources.Rec_Start);
		Taps.AddTap(button, tapped, UiControls.Record);
		DesktopTips.Hover(button);
		return button;
	}

	private static View StopButton(double size, Func<Task> tapped)
	{
		Border halo = Circle(size, DinoTokens.Get(DinoTokens.RecordHalo));
		halo.InputTransparent = true;
		Border button = Circle(size, DinoTokens.Get(DinoTokens.Danger));
		button.AutomationId = StopButtonId;
		button.Content = new Border
		{
			WidthRequest = StopSquare, HeightRequest = StopSquare, StrokeThickness = 0, BackgroundColor = Colors.White,
			HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, StrokeShape = new RoundRectangle { CornerRadius = StopCorner },
		};
		SemanticProperties.SetDescription(button, AppResources.Rec_Stop);
		Taps.AddTap(button, tapped, UiControls.Stop);
		StartPulse(halo);
		return new Grid { WidthRequest = size, HeightRequest = size, Children = { halo, button } };
	}

	// A soft pulse around the stop square; with reduced motion the ring stays still.
	private static void StartPulse(Border halo)
	{
		if (ReduceMotion.IsOn)
		{
			halo.Scale = 1.2;
			return;
		}

		var pulse = new Animation(scale => halo.Scale = scale, 1, PulseScale);
		pulse.Commit(halo, PulseAnimation, length: (uint)PulseMilliseconds, easing: Easing.CubicOut, repeat: () => true);
	}

	private static View PlayButton(double size, bool playing, Func<Task> tapped)
	{
		Border button = Circle(size, DinoTokens.Get(DinoTokens.Coral));
		button.AutomationId = PlayId;
		button.Shadow = new Shadow { Brush = new SolidColorBrush(DinoTokens.Get(DinoTokens.CoralEdge)), Offset = new Point(0, 5), Radius = 0, Opacity = 1f };
		button.Content = new IconView { Kind = playing ? IconKind.Pause : IconKind.Play, Filled = true, Size = size * 0.34, Color = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		SemanticProperties.SetDescription(button, playing ? AppResources.Record_Playing : AppResources.Record_Play);
		Taps.AddTap(button, tapped, playing ? UiControls.Pause : UiControls.Play);
		DesktopTips.Hover(button);
		return button;
	}

	// ---- listening (frame 190): the wave and "Use Dino's voice" ----

	/// <summary>The 28 px wave under the words that fills as the clip plays; <paramref name="progress"/> is 0 to 1.</summary>
	public static Grid Wave(double progress)
	{
		var wave = new Grid { HeightRequest = WaveHeight, HorizontalOptions = LayoutOptions.Center, ColumnSpacing = WaveGap };
		int filled = (int)Math.Round(Math.Clamp(progress, 0, 1) * WaveBars);
		for (int i = 0; i < WaveBars; i++)
		{
			wave.ColumnDefinitions.Add(new ColumnDefinition(WaveBarWidth));
			double height = WaveHeight * (0.35 + 0.65 * Math.Abs(Math.Sin(i * 0.9)));
			wave.Add(new Border
			{
				WidthRequest = WaveBarWidth, HeightRequest = height, VerticalOptions = LayoutOptions.Center, StrokeThickness = 0,
				BackgroundColor = i < filled ? DinoTokens.Get(DinoTokens.Brand) : DinoTokens.Get(DinoTokens.BrandTint),
				StrokeShape = new RoundRectangle { CornerRadius = WaveBarWidth / 2 },
			}, i, 0);
		}

		return wave;
	}

	/// <summary>"Use Dino's voice": a red-brown link under the buttons that gives the piece back to the pre-made audio.</summary>
	public static Label UseDinoLink(Func<Task> tapped)
	{
		var link = new Label
		{
			AutomationId = UseDinoId, Text = AppResources.Record_UseDino, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = UseDinoInk,
			HorizontalOptions = LayoutOptions.Center, Padding = new Thickness(16, 12),
		};
		SemanticProperties.SetDescription(link, AppResources.Record_UseDino);
		Taps.AddTap(link, tapped, UiControls.Pick);
		return link;
	}

	/// <summary>The 4 second "Undo" toast after "Use Dino's voice" (frame 190): a dark bar with the word and an Undo link.</summary>
	public static Border UndoToast(string text, Func<Task> undo)
	{
		var undoLabel = new Label { AutomationId = UndoId, Text = AppResources.Record_Undo, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#B9B6FF"), VerticalOptions = LayoutOptions.Center, Padding = new Thickness(12, 8) };
		Taps.AddTap(undoLabel, undo, UiControls.Cancel);
		var row = new Grid { ColumnSpacing = 8, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		row.Add(new Label { Text = text, FontSize = 14, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center }, 0);
		row.Add(undoLabel, 1);
		return new Border
		{
			BackgroundColor = DinoTokens.Get(DinoTokens.Ink), StrokeThickness = 0, Padding = new Thickness(16, 6), Margin = new Thickness(16, 0, 16, 12),
			StrokeShape = new RoundRectangle { CornerRadius = 14 }, VerticalOptions = LayoutOptions.End, Content = row,
		};
	}

	private static Border Circle(double size, Color fill) => new()
	{
		WidthRequest = size, HeightRequest = size, StrokeThickness = 0, BackgroundColor = fill,
		StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
	};

	// ---- the stay note (the intro wizard's Record step still shows it, Slice 230) ----

	/// <summary>The AutomationId of the "recordings stay on this phone" line, for the scripted check.</summary>
	internal const string StayNoteId = "RecordingsStayNote";

	/// <summary>The line that says where recordings go (Slice 230, mock frames 106 and 129): they stay on this phone or tablet, Dino never uploads or backs them up, so the grown-up keeps a copy.</summary>
	internal static string StayNoteText() => StayNoteFor(DeviceInfo.Idiom);

	private static string StayNoteFor(DeviceIdiom idiom) =>
		idiom == DeviceIdiom.Phone ? AppResources.Recordings_StayHere_Phone
		: idiom == DeviceIdiom.Tablet ? AppResources.Recordings_StayHere_Tablet
		: AppResources.Recordings_StayHere_Device;

	/// <summary>The line as a small label, 12.5 px in the body colour, for the intro wizard's record step.</summary>
	public static Label StayNote() => new()
	{
		AutomationId = StayNoteId, Text = StayNoteText(), FontSize = 12.5, LineHeight = 1.4,
		TextColor = DinoTokens.Get(DinoTokens.Body), Margin = new Thickness(4, 0),
	};
}
