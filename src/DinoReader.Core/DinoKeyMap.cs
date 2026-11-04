namespace DinoReader.Core;

/// <summary>The keys Dino Reader's Windows window reacts to (Slice 235 part B). Mapped from the platform's own key type in the Windows head only.</summary>
public enum DinoKey
{
	Other,
	Space,
	Left,
	Right,
	Escape,
	Enter,
	Backspace,
	Digit0,
	Digit1,
	Digit2,
	Digit3,
	Digit4,
	Digit5,
	Digit6,
	Digit7,
	Digit8,
	Digit9,

	/// <summary>Slice 235 part C: the vertical arrows. Up and down move through a list by the window's own focus navigation; they are named here so the key map knows them.</summary>
	Up,
	Down,
}

/// <summary>Which kind of screen has the keyboard: the shelf, a book being read (or its silent back cover), the grown-up gate, a book's options sheet, or any other page.</summary>
public enum DinoScreen
{
	Other,
	Shelf,
	Reading,
	ReadingBackCover,
	Gate,
	Sheet,

	/// <summary>The Settings list (Slice 235 part C, frame 157): Right opens the focused row in the right pane.</summary>
	Settings,
}

/// <summary>What a key press asks the screen to do.</summary>
public enum DinoKeyAction
{
	None,
	PlayPause,
	PreviousPage,
	NextPage,
	Back,
	CloseSheet,
	ReadAgain,
	GateHoldStart,
	GateHoldEnd,
	GateDigit,
	GateErase,
	GateSubmit,
	SheetActivate,
	OpenRow,
}

/// <summary>
/// The keyboard map of Dino Reader's Windows window (mock Batch 46 frame 160): Space plays or pauses (on the back cover it reads again), Left and Right go to the previous and next page, Esc goes back or
/// closes a sheet (nothing on the shelf). On the gate Space held starts and ends the 3 second hold, digits type the sum, Backspace erases and Enter checks. Space and the arrows do nothing while a text field
/// has focus. Keys never mirror, in any language: Left is always previous (Slice 96).
/// </summary>
public static class DinoKeyMap
{
	/// <summary>The action for <paramref name="key"/> going down or up on <paramref name="screen"/>.</summary>
	public static DinoKeyAction Resolve(DinoKey key, DinoScreen screen, bool textFieldFocused, bool keyDown)
	{
		if (!keyDown)
		{
			return key == DinoKey.Space && screen == DinoScreen.Gate ? DinoKeyAction.GateHoldEnd : DinoKeyAction.None;
		}

		if (key == DinoKey.Escape)
		{
			return OnEscape(screen);
		}

		if (textFieldFocused)
		{
			return DinoKeyAction.None;
		}

		return screen switch
		{
			DinoScreen.Reading => OnReading(key, backCover: false),
			DinoScreen.ReadingBackCover => OnReading(key, backCover: true),
			DinoScreen.Gate => OnGate(key),
			DinoScreen.Sheet => key is DinoKey.Enter or DinoKey.Space ? DinoKeyAction.SheetActivate : DinoKeyAction.None,
			DinoScreen.Settings => key == DinoKey.Right ? DinoKeyAction.OpenRow : DinoKeyAction.None,
			_ => DinoKeyAction.None,
		};
	}

	/// <summary>The digit 0 to 9 a key types, or null for any other key.</summary>
	public static int? DigitOf(DinoKey key) =>
		key is >= DinoKey.Digit0 and <= DinoKey.Digit9 ? key - DinoKey.Digit0 : null;

	private static DinoKeyAction OnEscape(DinoScreen screen) => screen switch
	{
		DinoScreen.Shelf => DinoKeyAction.None,
		DinoScreen.Sheet => DinoKeyAction.CloseSheet,
		_ => DinoKeyAction.Back,
	};

	private static DinoKeyAction OnReading(DinoKey key, bool backCover) => key switch
	{
		DinoKey.Space => backCover ? DinoKeyAction.ReadAgain : DinoKeyAction.PlayPause,
		DinoKey.Left => DinoKeyAction.PreviousPage,
		DinoKey.Right => DinoKeyAction.NextPage,
		_ => DinoKeyAction.None,
	};

	private static DinoKeyAction OnGate(DinoKey key)
	{
		if (DigitOf(key) is not null)
		{
			return DinoKeyAction.GateDigit;
		}

		return key switch
		{
			DinoKey.Space => DinoKeyAction.GateHoldStart,
			DinoKey.Backspace => DinoKeyAction.GateErase,
			DinoKey.Enter => DinoKeyAction.GateSubmit,
			_ => DinoKeyAction.None,
		};
	}
}
