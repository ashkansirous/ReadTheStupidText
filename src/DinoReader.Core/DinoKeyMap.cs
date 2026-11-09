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

	/// <summary>The vertical arrows: Settings (Slice 235 part C) and the reorder mode (Slice 264) use them.</summary>
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
	Reorder, // Slice 264: the shelf's reorder mode

	/// <summary>The Settings list (Slice 235 part C, frame 157): Right opens the focused row in the right pane.</summary>
	Settings,

	/// <summary>The first-run book picker (Slice 325 part D, frame OW2): Space ticks the focused cover, Enter is Continue.</summary>
	FirstBooks,

	/// <summary>The intro wizard (Slice 325 part E, frame OW6): Enter is Next, Esc is Skip all, the arrows step back and forward.</summary>
	Intro,

	/// <summary>The grown-ups' corner on any tab (Slice 325 part A, frame W1): Ctrl+1/2/3 switch tabs, Esc goes back to the child's shelf.</summary>
	Corner,
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
	ReorderCancel,
	ReorderDone,
	ReorderToggle,
	ReorderLeft,
	ReorderRight,
	ReorderUp,
	ReorderDown,
	PickToggle,
	PickContinue,
	IntroNext,
	IntroSkipAll,
	IntroPrevious,
	IntroForward,

	/// <summary>Esc on the corner (any tab): the same way back as the coral button (Slice 325 part A).</summary>
	LeaveCorner,
	CornerBooks,
	CornerStore,
	CornerSettings,

	/// <summary>Esc on the kids' shelf: "Close Dino Book Reader?", as Android's Back (Slice 325 part C).</summary>
	AskToClose,
}

/// <summary>
/// The keyboard map of Dino Reader's Windows window (mock Batch 46 frame 160): Space plays or pauses (on the back cover it reads again), Left and Right go to the previous and next page, Esc goes back or
/// closes a sheet (on the shelf it asks "Close Dino Book Reader?", in the corner it returns to the shelf). On the gate Space held starts and ends the 3 second hold, digits type the sum, Backspace erases and Enter checks. Space and the arrows do nothing while a text field
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
			DinoScreen.Settings or DinoScreen.Corner => key == DinoKey.Right ? DinoKeyAction.OpenRow : DinoKeyAction.None, // Slice 325 part B: the Settings tab is part of the corner, so Right opens its focused row there
			DinoScreen.Reorder => OnReorder(key),
			DinoScreen.FirstBooks => OnFirstBooks(key),
			DinoScreen.Intro => OnIntro(key),
			_ => DinoKeyAction.None,
		};
	}

	// Slice 325 part D (frame OW2): Space ticks the focused cover and Enter is Continue (the page ignores it until two are picked).
	private static DinoKeyAction OnFirstBooks(DinoKey key) => key switch
	{
		DinoKey.Space => DinoKeyAction.PickToggle,
		DinoKey.Enter => DinoKeyAction.PickContinue,
		_ => DinoKeyAction.None,
	};

	// Slice 325 part E (frame OW6): Enter is Next, Left and Right step back and forward; Esc (see OnEscape) is Skip all.
	private static DinoKeyAction OnIntro(DinoKey key) => key switch
	{
		DinoKey.Enter => DinoKeyAction.IntroNext,
		DinoKey.Left => DinoKeyAction.IntroPrevious,
		DinoKey.Right => DinoKeyAction.IntroForward,
		_ => DinoKeyAction.None,
	};

	// Slice 264: Space lifts the focused cover and drops it again, the arrows move the focus (or the lifted cover), Enter is Done and Esc is Cancel.
	private static DinoKeyAction OnReorder(DinoKey key) => key switch
	{
		DinoKey.Enter => DinoKeyAction.ReorderDone,
		DinoKey.Space => DinoKeyAction.ReorderToggle,
		DinoKey.Left => DinoKeyAction.ReorderLeft,
		DinoKey.Right => DinoKeyAction.ReorderRight,
		DinoKey.Up => DinoKeyAction.ReorderUp,
		DinoKey.Down => DinoKeyAction.ReorderDown,
		_ => DinoKeyAction.None,
	};

	/// <summary>The digit 0 to 9 a key types, or null for any other key.</summary>
	public static int? DigitOf(DinoKey key) =>
		key is >= DinoKey.Digit0 and <= DinoKey.Digit9 ? key - DinoKey.Digit0 : null;

	/// <summary>
	/// A Ctrl+digit shortcut (Slice 325 part A, frame W1): Ctrl+1, Ctrl+2 and Ctrl+3 pick the corner's Books, Store and Settings tab. Only the corner has any; the keyboard hook asks this before it
	/// lets a Ctrl key combination pass to the focused control.
	/// </summary>
	public static DinoKeyAction ResolveShortcut(DinoKey key, DinoScreen screen) => screen == DinoScreen.Corner
		? key switch
		{
			DinoKey.Digit1 => DinoKeyAction.CornerBooks,
			DinoKey.Digit2 => DinoKeyAction.CornerStore,
			DinoKey.Digit3 => DinoKeyAction.CornerSettings,
			_ => DinoKeyAction.None,
		}
		: DinoKeyAction.None;

	private static DinoKeyAction OnEscape(DinoScreen screen) => screen switch
	{
		DinoScreen.Shelf => DinoKeyAction.AskToClose,
		DinoScreen.Corner => DinoKeyAction.LeaveCorner,
		DinoScreen.Sheet => DinoKeyAction.CloseSheet,
		DinoScreen.Reorder => DinoKeyAction.ReorderCancel,
		DinoScreen.Intro => DinoKeyAction.IntroSkipAll,
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
