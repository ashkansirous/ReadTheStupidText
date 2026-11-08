using ReadTheStupidText.Application.Translation;

namespace DinoReader.Core;

/// <summary>Which title a shelf tile shows (Slice 159, 323): the one rule, free of any screen so it can be tested.</summary>
public static class ShelfTitleChoice
{
    /// <summary>Whether the child can read <paramref name="story"/> in <paramref name="language"/> without a download: it is the book's own language, or it carries a full table for it (Slice 173).</summary>
    public static bool CanReadIn(Story story, string language)
    {
        string target = UiLanguage.Resolve(language);
        return target == UiLanguage.Resolve(story.Language)
            || (story.TranslateToMyLanguage && BundledTranslations.IsComplete(story, target) && BundledTranslations.Has(story, target));
    }

    /// <summary>The title in <paramref name="language"/> from the book's own table or <paramref name="shipped"/> (the shipped-titles table, English books only); the title as it is when there is none.</summary>
    public static string TitleIn(Story story, string language, Func<Story, string, string?> shipped)
    {
        string source = UiLanguage.Resolve(story.Language);
        if (!TranslationFlow.ShouldTranslate(story.TranslateToMyLanguage, source, language))
        {
            return story.Title;
        }

        return BundledTranslations.Lookup(story, language, story.Title)
            ?? (source == UiLanguage.English ? shipped(story, language) : null)
            ?? story.Title;
    }

    /// <summary>The kids' shelf's title: in the picked language only for a book that can be read in it, else in the app language <paramref name="main"/>, so a tile never names a book in a language it will not open in.</summary>
    public static string For(Story story, string picked, string main, Func<Story, string, string?> shipped)
    {
        string pick = UiLanguage.Resolve(picked);
        string app = UiLanguage.Resolve(main);
        return pick == app || CanReadIn(story, pick) ? TitleIn(story, pick, shipped) : TitleIn(story, app, shipped);
    }
}
