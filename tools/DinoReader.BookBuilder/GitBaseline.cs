using System.Diagnostics;
using System.Text.Json;

namespace DinoReader.BookBuilder;

/// <summary>
/// A book's <c>book.json</c> as it was where this branch left <c>origin/main</c> (the merge base), so <c>check</c> can prove a changed book also got a higher version. Null when
/// there is no git, no <c>origin/main</c> (a shallow clone) or the book is new: the version check is then skipped, the hash check still runs.
/// </summary>
internal static class GitBaseline
{
    private const string MainBranch = "origin/main";

    private const string CiVariable = "GITHUB_ACTIONS";
    private const string EventVariable = "GITHUB_EVENT_NAME";
    private const string PullRequestEvent = "pull_request";
    private const string PullRequestTargetEvent = "pull_request_target";

    private static readonly Lazy<string?> MergeBase = new(() => Git("merge-base", "HEAD", MainBranch)?.Trim());

    public static BookInfo? Of(string stem)
    {
        if (string.IsNullOrEmpty(MergeBase.Value))
        {
            return IsPullRequestInCi()
                ? throw new InvalidOperationException($"cannot find the merge base with {MainBranch} on a pull request, so the version check cannot run (check out with fetch-depth: 0)")
                : null;
        }

        string? json = Git("show", $"{MergeBase.Value}:{Books.ContentFolder}/{stem}/{BookFolder.BookFile}");
        return json is null ? null : TryParse(json);
    }

    /// <summary>The problem when the content changed since <paramref name="baseline"/> (a different hash) but the version did not rise; otherwise null.</summary>
    public static string? VersionProblem(string stem, BookInfo current, BookInfo? baseline) =>
        baseline is not null && !string.IsNullOrEmpty(baseline.ContentHash) && baseline.ContentHash != current.ContentHash && current.Version <= baseline.Version
            ? $"{stem} changed since {MainBranch} (v{baseline.Version}) but its version is still v{current.Version}: it would never reach the store or the installed starters (run: dotnet run --project tools/DinoReader.BookBuilder -- stamp {stem})"
            : null;

    private static bool IsPullRequestInCi() =>
        Environment.GetEnvironmentVariable(CiVariable) == "true" && Environment.GetEnvironmentVariable(EventVariable) is PullRequestEvent or PullRequestTargetEvent;

    private static BookInfo? TryParse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<BookInfo>(json, Books.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Git(params string[] arguments)
    {
        try
        {
            var start = new ProcessStartInfo("git") { WorkingDirectory = Books.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true };
            arguments.ToList().ForEach(start.ArgumentList.Add);
            using Process process = Process.Start(start)!;
            Task<string> error = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            error.GetAwaiter().GetResult();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }
}
