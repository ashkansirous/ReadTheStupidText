using System.Text.Json;

namespace DinoReader.Core;

/// <summary>
/// Slice 459 (kill recovery, review 1 #2/#7): the pure rules for the first run's background queue across a kill. The remaining jobs are saved as one string (the app keeps it in Preferences) each time they change and
/// are read back at the next launch; a bad or missing string is an empty list, never an error.
/// </summary>
public static class BackgroundJobStore
{
    /// <summary>The jobs as the one string the app saves; an empty list is the empty string.</summary>
    public static string Serialize(IEnumerable<BackgroundJob> jobs)
    {
        List<BackgroundJob> list = jobs.ToList();
        return list.Count == 0 ? string.Empty : JsonSerializer.Serialize(list);
    }

    /// <summary>The saved jobs; null, empty or unreadable text gives an empty list, and a null entry or one with no book id is dropped.</summary>
    public static IReadOnlyList<BackgroundJob> Parse(string? saved) => Parse(saved, out _);

    /// <summary>
    /// As <see cref="Parse(string?)"/>, and <paramref name="damaged"/> is true when the text could not be read as a job list or held an entry that had to be dropped (the owner then clears the saved value).
    /// </summary>
    public static IReadOnlyList<BackgroundJob> Parse(string? saved, out bool damaged)
    {
        damaged = false;
        if (string.IsNullOrWhiteSpace(saved))
        {
            return [];
        }

        try
        {
            List<BackgroundJob?>? read = JsonSerializer.Deserialize<List<BackgroundJob?>>(saved);
            List<BackgroundJob> jobs = (read ?? []).OfType<BackgroundJob>().Where(IsValid).ToList();
            damaged = read is null || jobs.Count != read.Count;
            return jobs;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            // no-fallback: a damaged save loses the queue, never the launch; the shelf's own pack repair still fills what is missing
            damaged = true;
            return [];
        }
    }

    private static bool IsValid(BackgroundJob job) =>
        !string.IsNullOrWhiteSpace(job.BookId) && (job.Kind == BackgroundJobKind.Package || !string.IsNullOrWhiteSpace(job.Language));

    /// <summary>True when both jobs fetch the same thing (kind, book and language, ignoring case); the attempt count does not matter.</summary>
    public static bool SameJob(BackgroundJob a, BackgroundJob b) => JobComparer.Instance.Equals(a, b);

    /// <summary>
    /// The jobs to run at the next launch: the saved ones minus a job past its retry budget, a book package whose book is already on the shelf and a pack of a book that is not on the shelf (it was deleted) unless
    /// that book's package is kept, with a main-language pack job first for every shelf book in <paramref name="booksMissingMainPack"/> that has none queued, and no job twice.
    /// </summary>
    public static IReadOnlyList<BackgroundJob> ResumeJobs(IEnumerable<BackgroundJob> saved, ISet<string> onShelf, IEnumerable<string> booksMissingMainPack, string mainLanguage)
    {
        List<BackgroundJob> kept = saved
            .Where(BackgroundJobRules.WithinRetryBudget)
            .Where(job => job.Kind != BackgroundJobKind.Package || !onShelf.Contains(job.BookId))
            .ToList();
        HashSet<string> keptPackages = kept.Where(job => job.Kind == BackgroundJobKind.Package).Select(job => job.BookId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<BackgroundJob> result = booksMissingMainPack.Where(onShelf.Contains).Select(id => new BackgroundJob(id, BackgroundJobKind.Pack, mainLanguage)).ToList();
        result.AddRange(kept.Where(job => job.Kind == BackgroundJobKind.Package || onShelf.Contains(job.BookId) || keptPackages.Contains(job.BookId)));
        return result.Distinct(JobComparer.Instance).ToList();
    }

    private sealed class JobComparer : IEqualityComparer<BackgroundJob>
    {
        public static readonly JobComparer Instance = new();

        public bool Equals(BackgroundJob? a, BackgroundJob? b) =>
            a is not null && b is not null && a.Kind == b.Kind && string.Equals(a.BookId, b.BookId, StringComparison.OrdinalIgnoreCase) && string.Equals(a.Language, b.Language, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(BackgroundJob job) => HashCode.Combine(job.Kind, job.BookId.ToLowerInvariant(), job.Language?.ToLowerInvariant());
    }
}
