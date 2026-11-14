namespace DinoReader.Core;

/// <summary>The own-book wizard's three steps and what the buttons do on each (Slice 164 follow-up).</summary>
public static class WizardSteps
{
    public const int Pages = 0;
    public const int Cover = 1;
    public const int Text = 2;

    public static bool ShowsBack(int step) => step > Pages;

    /// <summary>Next shares the row with Back after the first step, and spans both columns on it.</summary>
    public static int NextColumnSpan(int step) => ShowsBack(step) ? 1 : 2;

    public static NextAction NextActionFor(int step) => step switch
    {
        Text => NextAction.Save,
        Pages => NextAction.GoToCover,
        _ => NextAction.Next,
    };

    /// <summary>The first step needs at least one page before Next works.</summary>
    public static bool NextEnabled(int step, int pageCount) => step != Pages || pageCount > 0;
}

public enum NextAction
{
    Next,
    GoToCover,
    Save,
}
