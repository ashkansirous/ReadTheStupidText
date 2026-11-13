using System.Text.Json;

namespace DinoReader.Core;

/// <summary>One clip of the try: its key on the book and the exact file this recording made (a clip that was there before has another file).</summary>
public sealed record TryClip(StoryClipKey Key, string File, string? BookId = null);

/// <summary>
/// The try of the voice step, one owned record (Slice 362, Decision 1): the book and the exact clips recorded there, saved the moment a clip is recorded. Only these clips are ever deleted; a clip the
/// book held before, or one recorded later outside the step, is never part of it. Pure: the app stores the JSON in a preference and runs <see cref="OnboardingTry.DeleteAsync"/> under one lock.
/// </summary>
public sealed record OnboardingTryRecord(string BookId, IReadOnlyList<TryClip> Clips)
{
    /// <summary>
    /// This record with a clip recorded as <paramref name="file"/> at <paramref name="key"/> of <paramref name="bookId"/> (default: the record's own book); a clip of the same key and book (re-recorded) is replaced.
    /// A clip of another book joins the record, so ids kept from a failed deletion stay listed and are retried (never replaced).
    /// </summary>
    public OnboardingTryRecord With(StoryClipKey key, string file, string? bookId = null)
    {
        string book = bookId ?? BookId;
        TryClip added = new(key, file, book == BookId ? null : book);
        return this with { Clips = [.. Clips.Where(c => c.Key != key || (c.BookId ?? BookId) != book), added] };
    }

    /// <summary>The keys to take off <paramref name="story"/>: those of this record's clips on that book whose clip still has the recorded file. A clip replaced since (another file) or already gone is not listed.</summary>
    public IReadOnlyList<StoryClipKey> ClipsToDelete(Story story) =>
        [.. Clips.Where(c => (c.BookId ?? BookId) == story.Id || c.BookId is null).Where(c => string.Equals(StoryRecordings.ClipOf(story, c.Key)?.File, c.File, StringComparison.Ordinal)).Select(c => c.Key)];

    /// <summary>One record per book the try touched, each holding that book's clips: what <see cref="OnboardingTry.DeleteAsync"/> settles book by book.</summary>
    public IReadOnlyList<OnboardingTryRecord> PerBook() =>
        [.. Clips.GroupBy(c => c.BookId ?? BookId).Select(g => new OnboardingTryRecord(g.Key, [.. g.Select(c => c with { BookId = null })]))];

    public string ToJson() => JsonSerializer.Serialize(new Dto(BookId, [.. Clips.Select(c => new ClipDto(c.Key.Page, c.Key.First, c.Key.Last, c.Key.Language, c.File, c.BookId))]));

    /// <summary>The record in <paramref name="json"/>, or null for nothing or text that is not a record.</summary>
    public static OnboardingTryRecord? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            Dto? dto = JsonSerializer.Deserialize<Dto>(json);
            return dto?.BookId is { Length: > 0 } id && dto.Clips is { } clips
                ? new OnboardingTryRecord(id, [.. clips.Select(c => new TryClip(new StoryClipKey(c.Page, c.First, c.Last, c.Language ?? string.Empty), c.File ?? string.Empty, c.BookId))])
                : null;
        }
        catch (JsonException)
        {
            // no-fallback: a record that cannot be read names nothing to delete
            return null;
        }
    }

    private sealed record ClipDto(int Page, int First, int Last, string? Language, string? File, string? BookId = null);

    private sealed record Dto(string? BookId, List<ClipDto>? Clips);
}

/// <summary>When the try is deleted (Slice 362, Decision 1): when the child leaves the voice step, and at a start or a gallery while a record exists and onboarding is not on the voice step (an abandoned setup).</summary>
public static class OnboardingTryRules
{
    /// <summary>True when a start or the gallery appearing must delete the recorded try: a record exists and the saved step is not the voice step (which still owns it).</summary>
    public static bool DeleteAtStart(bool recordExists, OnboardingStep? savedStep) => recordExists && savedStep != OnboardingStep.Voice;
}

/// <summary>Deletes exactly the clips of an <see cref="OnboardingTryRecord"/>: each leaves the book and its file leaves the book's folder.</summary>
public static class OnboardingTry
{
    /// <summary>Removes the record's clips from its book and their files; a book that is gone or a clip already removed is fine. Throws when the library fails, so the caller keeps the record until every listed clip is gone. A file another clip still names stays.</summary>
    public static async Task DeleteAsync(IStoryLibrary library, OnboardingTryRecord record)
    {
        foreach (OnboardingTryRecord book in record.PerBook())
        {
            await DeleteBookAsync(library, book);
        }
    }

    private static async Task DeleteBookAsync(IStoryLibrary library, OnboardingTryRecord record)
    {
        Story? story = await library.GetAsync(record.BookId);
        if (story is null)
        {
            return;
        }

        IReadOnlyList<StoryClipKey> keys = record.ClipsToDelete(story);
        Story clean = keys.Aggregate(story, StoryRecordings.WithoutClip);
        if (keys.Count > 0)
        {
            await library.UpdateAsync(clean);
        }

        foreach (string file in record.Clips.Select(c => c.File).Distinct(StringComparer.Ordinal).Where(f => !StoryRecordings.IsReferenced(clean, f)))
        {
            await library.DeleteFileAsync(record.BookId, file);
        }
    }
}
