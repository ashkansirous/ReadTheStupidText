namespace DinoReader.Core;

/// <summary>
/// The intro wizard's arrangement: <see cref="Split"/> puts the picture panel (<see cref="PicturePanel"/> dp wide) left of the copy, with Skip and the main button in one row at the foot;
/// <see cref="TextMaxWidth"/> caps the copy block (infinite on a phone).
/// </summary>
/// <remarks>A tablet held upright (Slice 234 part B, mock Batch 45 frame 141) stacks: <see cref="PanelHeight"/> is the height of the picture panel above the copy (0 elsewhere), with the buttons in one row at the foot.</remarks>
public readonly record struct IntroShape(bool Split, double PicturePanel, double TextMaxWidth, bool ButtonsInRow, double PanelHeight = 0);

/// <summary>The grown-up gate's arrangement: <see cref="Split"/> puts the question left and the answers (or the hold ring) right.</summary>
/// <remarks>A tablet held upright (frame 137) gets a centred <see cref="Card"/> of <see cref="CardWidth"/> dp with <see cref="KeyHeight"/> dp answers and a <see cref="RingSize"/> dp hold ring; elsewhere those are the phone's 52 and 120.</remarks>
public readonly record struct GateShape(bool Split, bool Card = false, double CardWidth = 0, double KeyHeight = OnboardingLayout.UprightKeyHeight, double RingSize = OnboardingLayout.PhoneHoldRing, double CardMaxHeight = 0);

/// <summary>The first-run pages' arrangement: <see cref="Split"/> puts the welcome left and the language grid right (Continue pinned under it), with <see cref="LanguageColumns"/> columns.</summary>
/// <remarks>A tablet held upright (frame 138) gets a centred <see cref="Card"/> of <see cref="CardWidth"/> dp that holds all three pages.</remarks>
public readonly record struct FirstRunShape(bool Split, int LanguageColumns, bool Card = false, double CardWidth = 0, double CardMaxHeight = 0);

/// <summary>One cell of a grid, counted from 0.</summary>
public readonly record struct GridCell(int Row, int Column);

/// <summary>
/// The pure size rules of the intro wizard, the grown-up gate and the first run when the window is held sideways (Slice 232 part E, mock Batch 44 frames 121, 122, 123, 130).
/// Phone or tablet and sideways or upright come from <see cref="LandscapeLayout"/>; this file only adds what these three screens decide on top.
/// Each page asks once per size change and only places the views it already has: the wizard step, the gate's sum and the first-run pick are never in a layout.
/// </summary>
public static class OnboardingLayout
{
    /// <summary>A tablet's copy block is never wider than this, in dp (mock frame 130).</summary>
    public const double TabletTextMaxWidth = 460;

    /// <summary>Language buttons per row on the first-run page: 3 held sideways, 2 upright.</summary>
    public const int SidewaysLanguageColumns = 3;

    public const int UprightLanguageColumns = 2;

    /// <summary>The gate's answer keys: 52 dp upright, up to 64 dp held sideways so a covering hand never hides the sum (frame 122).</summary>
    public const double UprightKeyHeight = 52;

    public const double SidewaysKeyHeight = 64;

    /// <summary>The hold ring on a phone, in dp.</summary>
    public const double PhoneHoldRing = 120;
    public const double MinSidewaysKeyHeight = 40;

    /// <summary>The number pad has four rows.</summary>
    public const int KeyRows = 4;

    /// <summary>Held sideways, the intro splits: picture panel left (300 dp on a phone, 360 on a tablet), copy right, buttons in one row. Upright it stacks, as before; a tablet's copy is capped at 460 either way.</summary>
    public static IntroShape Intro(double width, double height)
    {
        bool landscape = LandscapeLayout.IsLandscape(width, height);
        if (LandscapeLayout.IsTabletPortrait(width, height))
        {
            return new IntroShape(false, 0, TabletCardLayout.IntroTextMaxWidth, true, TabletCardLayout.IntroPanelHeight(height));
        }

        double cap = LandscapeLayout.SizeOf(width, height) == DeviceSize.Tablet ? TabletTextMaxWidth : double.PositiveInfinity;
        return new IntroShape(landscape, landscape ? LandscapeLayout.ListPaneWidth(width, height) : 0, cap, landscape);
    }

    /// <summary>Held sideways the gate splits (question left, answers right, the hold ring where the answers were); upright it is the bottom sheet with 52 dp keys, and a tablet held upright gets a centred card with 80 dp keys. Sideways the layout itself shares the height between the four key rows, each key 40..64 dp.</summary>
    public static GateShape Gate(double width, double height)
    {
        if (LandscapeLayout.IsTabletPortrait(width, height))
        {
            return new GateShape(false, true, TabletCardLayout.CardWidth(width, TabletCardKind.Gate), TabletCardLayout.GateKeyHeight, TabletCardLayout.GateHoldRing, TabletCardLayout.CardMaxHeight(height));
        }

        return new GateShape(LandscapeLayout.IsLandscape(width, height));
    }

    /// <summary>First run: three language columns and the split held sideways; two columns and the single column upright, on a card on a tablet held upright.</summary>
    public static FirstRunShape FirstRun(double width, double height)
    {
        bool landscape = LandscapeLayout.IsLandscape(width, height);
        if (LandscapeLayout.IsTabletPortrait(width, height))
        {
            return new FirstRunShape(false, UprightLanguageColumns, true, TabletCardLayout.CardWidth(width, TabletCardKind.FirstRun), TabletCardLayout.CardMaxHeight(height));
        }

        return new FirstRunShape(landscape, landscape ? SidewaysLanguageColumns : UprightLanguageColumns);
    }

    /// <summary>Where the item at <paramref name="index"/> goes in a grid of <paramref name="columns"/> columns, filled row by row.</summary>
    public static GridCell CellOf(int index, int columns)
    {
        int safe = Math.Max(1, columns);
        return new GridCell(Math.Max(0, index) / safe, Math.Max(0, index) % safe);
    }

    /// <summary>How many rows <paramref name="count"/> items need in <paramref name="columns"/> columns.</summary>
    public static int RowsFor(int count, int columns)
    {
        int safe = Math.Max(1, columns);
        return (Math.Max(0, count) + safe - 1) / safe;
    }
}
