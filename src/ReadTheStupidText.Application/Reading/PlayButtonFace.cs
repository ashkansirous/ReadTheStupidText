namespace ReadTheStupidText.Application.Reading;

/// <summary>What the control panel's play slot shows (Slice 164): a spinner while a read is
/// generating or a wait is on, and a Cancel glyph over it when that wait can be cancelled.</summary>
public readonly record struct PlayButtonFace(bool ShowSpinner, bool ShowCancel)
{
    public bool ShowPlayPause => !ShowSpinner;

    public static PlayButtonFace For(bool generating, bool waiting, bool waitCanCancel) =>
        new(generating || waiting, waiting && waitCanCancel);
}
