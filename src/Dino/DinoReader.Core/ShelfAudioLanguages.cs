namespace DinoReader.Core;

/// <summary>
/// Which languages a change of the story-language set has to fetch shelf audio for (Slice 285): every language of the new set that the set the page opened with did not hold, the
/// new main language included. Pure; the app starts one shelf run for each.
/// </summary>
public static class ShelfAudioLanguages
{
    /// <summary>The languages of <paramref name="after"/> that <paramref name="before"/> lacked, in the new set's order (main first).</summary>
    public static IReadOnlyList<string> Added(StoryLanguageSet before, StoryLanguageSet after) =>
        after.All.Where(language => !before.Contains(language)).ToList();

    /// <summary>
    /// The languages a shelf audio run starts for when the Language page is really left: the ones <see cref="Added"/> that this page has not already started (<paramref name="alreadyStarted"/>),
    /// so Back and a swipe back never start one twice.
    /// </summary>
    public static IReadOnlyList<string> ToStartOnLeaving(StoryLanguageSet before, StoryLanguageSet after, IReadOnlySet<string> alreadyStarted) =>
        Added(before, after).Where(language => !alreadyStarted.Contains(language)).ToList();
}
