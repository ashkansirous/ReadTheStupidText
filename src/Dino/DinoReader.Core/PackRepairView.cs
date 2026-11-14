namespace DinoReader.Core;

/// <summary>Which of the mock's four states the reading page shows under the words card for a book whose sound is not there (Slice 360, Design SS1-SS4).</summary>
public enum PackSoundKind
{
    /// <summary>Nothing to say (the book has no pack at all in this language, or no read is waiting on one).</summary>
    None,

    /// <summary>SS1: the sound is downloading, with its percent.</summary>
    Getting,

    /// <summary>SS2, or SS4 without a button: there is no internet.</summary>
    NoInternet,

    /// <summary>SS3: the pack is missing on the server or the download failed; Dino tries again soon.</summary>
    Failed,
}

/// <summary>What the strip shows: the kind, the percent (only for <see cref="PackSoundKind.Getting"/>) and whether a "Read in {other language}" button goes with it.</summary>
public sealed record PackSoundView(PackSoundKind Kind, int Percent = 0, string? SwitchTo = null)
{
    public static readonly PackSoundView Nothing = new(PackSoundKind.None);

    /// <summary>Whether the card has the "Read in {0}" button (SS2, SS3); without it the card is SS4 (or SS3 without a button).</summary>
    public bool HasSwitch => SwitchTo is not null;

    /// <summary>
    /// The one decision for a pack that is expected (<see cref="PackExpectation"/>) and not on the phone, following the latest news: no internet (SS2/SS4) replaces a percent that is no longer moving, then a
    /// download shows its percent (SS1), then a failed or missing pack (SS3); with no outcome yet it is SS1 at 0% (the open-book run is starting). <paramref name="switchTo"/> is another family language whose sound
    /// for this book is on the phone, or null.
    /// </summary>
    public static PackSoundView For(int? percent, bool online, PackOutcome? outcome, string? switchTo)
    {
        if (!online)
        {
            return new PackSoundView(PackSoundKind.NoInternet, 0, switchTo);
        }

        if (percent is { } pct)
        {
            return new PackSoundView(PackSoundKind.Getting, pct);
        }

        return outcome is PackOutcome.Failed or PackOutcome.Missing404 or PackOutcome.Offline
            ? new PackSoundView(PackSoundKind.Failed, 0, switchTo)
            : new PackSoundView(PackSoundKind.Getting, 0);
    }

    /// <summary>The whole page rule: a pack that is installed or not expected says nothing; an expected, missing one is <see cref="For"/>.</summary>
    public static PackSoundView ForPack(PackExpectationKind expectation, bool installed, int? percent, bool online, PackOutcome? outcome, string? switchTo) =>
        installed || expectation == PackExpectationKind.NotExpected ? Nothing : For(percent, online, outcome, switchTo);

    /// <summary>The first family language, other than <paramref name="reading"/>, whose pack for the book is on the phone (the main language counts only when it is installed too).</summary>
    public static string? OtherInstalledLanguage(IEnumerable<string> familyLanguages, string reading, Func<string, bool> installed) =>
        familyLanguages.FirstOrDefault(language => !string.Equals(language, reading, StringComparison.Ordinal) && installed(language));
}
