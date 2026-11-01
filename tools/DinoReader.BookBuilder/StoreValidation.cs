using System.Text.Json;
using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.BookBuilder;

/// <summary>
/// Slice 239: what <c>build --live</c> (the <c>publish-store</c> job, which runs no test suite) checks about each store book before anything is built or uploaded: a unique id, the required
/// <c>book.json</c> fields, a page count a store book may have, one picture per page, a complete <c>titles.json</c> and a cast whose named characters each have their own voice. Any problem fails the build.
/// </summary>
internal static class StoreValidation
{
    private const int MinPages = 8;
    private const int MaxPages = 18; // the user's limit for a store book (2026-10-26, X Marks the Treasure!)
    private const int MaxAge = 18;

    /// <summary>Every problem of the given store books (and of any id shared with another book); empty when all are fine.</summary>
    public static IReadOnlyList<string> Problems(IEnumerable<BookFolder> storeBooks)
    {
        var problems = new List<string>();
        problems.AddRange(BookCommands.IdProblems());
        foreach (BookFolder book in storeBooks)
        {
            problems.AddRange(BookProblems(book));
        }

        return problems;
    }

    /// <summary>The problems of each store book that has any, by stem (an id shared with another book counts against each store book that has it); a valid book is absent.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ProblemsByBook(IEnumerable<BookFolder> storeBooks)
    {
        Dictionary<string, string> shared = Books.All.GroupBy(b => b.Info.Id).Where(g => g.Count() > 1).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(b => b.Stem)));
        var result = new Dictionary<string, IReadOnlyList<string>>();
        foreach (BookFolder book in storeBooks)
        {
            List<string> problems = BookProblems(book).ToList();
            if (shared.TryGetValue(book.Info.Id, out string? stems))
            {
                problems.Add($"the books {stems} share the id {book.Info.Id}");
            }

            if (problems.Count > 0)
            {
                result[book.Stem] = problems;
            }
        }

        return result;
    }

    public static IReadOnlyList<string> BookProblems(BookFolder book)
    {
        var problems = new List<string>();
        problems.AddRange(FieldProblems(book));
        if (BookCommands.TitlesProblem(book.Stem, book.Titles) is { } titles)
        {
            problems.Add(titles);
        }

        if (book.Text is null)
        {
            problems.Add($"{book.Stem} has no {BookFolder.StoryFile}");
            return problems;
        }

        try
        {
            problems.AddRange(StoryProblems(book, StoreBuilder.Build(book.ToSpec())));
        }
        catch (Exception e) when (e is InvalidOperationException or JsonException or IOException)
        {
            problems.Add($"{book.Stem} does not build: {e.Message}");
        }

        return problems;
    }

    private static IEnumerable<string> FieldProblems(BookFolder book)
    {
        if (!StoryIds.IsValid(book.Info.Id))
        {
            yield return $"{book.Stem} has an invalid id {book.Info.Id}";
        }

        if (string.IsNullOrWhiteSpace(book.Info.Credits) || string.IsNullOrWhiteSpace(book.Info.Author) || string.IsNullOrWhiteSpace(book.Info.Title))
        {
            yield return $"{book.Stem}/book.json needs a title, an author and a credit line";
        }

        if (book.Info.AgeMin < 0 || book.Info.AgeMax < book.Info.AgeMin || book.Info.AgeMax > MaxAge)
        {
            yield return $"{book.Stem}/book.json has an invalid age range {book.Info.AgeMin}-{book.Info.AgeMax}";
        }
    }

    private static IEnumerable<string> StoryProblems(BookFolder book, Story story)
    {
        if (story.Pages.Count is < MinPages or > MaxPages)
        {
            yield return $"{book.Stem} has {story.Pages.Count} pages; a store book has {MinPages} to {MaxPages}";
        }

        int pictures = StarterBuilder.PictureFilesIn(book.PicturesPath).Count;
        if (pictures != story.Pages.Count)
        {
            yield return $"{book.Stem} has {story.Pages.Count} pages but {pictures} pictures";
        }

        foreach (string problem in CastProblems(book, story))
        {
            yield return problem;
        }
    }

    private static IEnumerable<string> CastProblems(BookFolder book, Story story)
    {
        IReadOnlyList<string> found = story.Cast.Select(c => c.Name).ToList();
        foreach (string expected in (book.Info.Characters ?? []).Where(c => !found.Contains(c)))
        {
            yield return $"{book.Stem}: no character {expected}; found {string.Join(", ", found)}";
        }

        if (story.Cast.Count != story.Cast.Select(c => (c.VoiceId, c.Flavour)).Distinct().Count())
        {
            yield return $"{book.Stem}: two characters share one (voice, flavour)";
        }

        if (story.Cast.Any(c => c.VoiceId == SupertonicVoiceTable.Default.Id))
        {
            yield return $"{book.Stem}: a character has the default voice";
        }
    }
}
