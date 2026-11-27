using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 459: the pure rules behind the first book's prefetch (queue order, early-open point, untick, language save, background failure).</summary>
public class FirstBookPrefetchTests
{
    private const string Pigs = "pigs";
    private const string Lion = "lion";
    private const string Fox = "fox";

    private static HashSet<string> Ids(params string[] ids) => new(ids, StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData(PrefetchState.Idle, false)]
    [InlineData(PrefetchState.Listing, true)]
    [InlineData(PrefetchState.Downloading, true)]
    [InlineData(PrefetchState.Landed, true)]
    [InlineData(PrefetchState.Failed, false)]
    [InlineData(PrefetchState.Cancelled, false)]
    public void Only_a_live_or_landed_prefetch_is_joined(PrefetchState state, bool joinable) =>
        Assert.Equal(joinable, FirstBooks.IsJoinable(state));

    [Fact]
    public void The_prefetched_book_goes_first_and_the_rest_queue_in_tick_order_then_the_other_languages()
    {
        FirstBookPlan plan = FirstBooks.PlanContinue([Lion, Pigs, Fox], Ids(), Pigs, PrefetchState.Downloading, ["en", "fa", "fr"], "en");

        Assert.Equal(new FirstBookStep(Pigs, FirstBookSource.JoinPrefetch, "en"), plan.First);
        Assert.Equal(
            [
                new BackgroundJob(Lion, BackgroundJobKind.Package, null),
                new BackgroundJob(Fox, BackgroundJobKind.Package, null),
                new BackgroundJob(Lion, BackgroundJobKind.Pack, "fa"),
                new BackgroundJob(Pigs, BackgroundJobKind.Pack, "fa"),
                new BackgroundJob(Fox, BackgroundJobKind.Pack, "fa"),
                new BackgroundJob(Lion, BackgroundJobKind.Pack, "fr"),
                new BackgroundJob(Pigs, BackgroundJobKind.Pack, "fr"),
                new BackgroundJob(Fox, BackgroundJobKind.Pack, "fr"),
            ],
            plan.Background);
    }

    [Theory]
    [InlineData(PrefetchState.Idle)]
    [InlineData(PrefetchState.Failed)]
    [InlineData(PrefetchState.Cancelled)]
    public void Without_a_joinable_prefetch_the_first_ticked_book_is_fetched_normally(PrefetchState state)
    {
        FirstBookPlan plan = FirstBooks.PlanContinue([Lion, Pigs], Ids(), Pigs, state, ["en"], "en");

        Assert.Equal(new FirstBookStep(Lion, FirstBookSource.Download, "en"), plan.First);
        Assert.Equal([new BackgroundJob(Pigs, BackgroundJobKind.Package, null)], plan.Background);
    }

    [Fact]
    public void A_first_book_already_on_the_shelf_only_needs_its_pack_and_shelf_books_are_not_fetched_again()
    {
        FirstBookPlan plan = FirstBooks.PlanContinue([Pigs, Lion], Ids(Pigs, Lion), null, PrefetchState.Idle, ["en", "fa"], null);

        Assert.Equal(new FirstBookStep(Pigs, FirstBookSource.OnShelf, "en"), plan.First);
        Assert.Equal([new BackgroundJob(Pigs, BackgroundJobKind.Pack, "fa"), new BackgroundJob(Lion, BackgroundJobKind.Pack, "fa")], plan.Background);
    }

    [Fact]
    public void A_single_language_family_queues_no_pack_jobs()
    {
        FirstBookPlan plan = FirstBooks.PlanContinue([Pigs, Lion], Ids(), Pigs, PrefetchState.Landed, ["en"], "en");

        Assert.All(plan.Background, job => Assert.Equal(BackgroundJobKind.Package, job.Kind));
        Assert.Single(plan.Background);
    }

    [Fact]
    public void Nothing_ticked_plans_nothing()
    {
        Assert.Null(FirstBooks.PlanContinue([], Ids(), Pigs, PrefetchState.Landed, ["en"], "en").First);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void The_wait_ends_when_the_first_book_is_on_the_shelf_and_its_main_pack_has_ended(bool onShelf, bool packEnded, bool ready) =>
        Assert.Equal(ready, FirstBooks.IsFirstBookReady(onShelf, packEnded));

    [Theory]
    [InlineData(PrefetchState.Listing, Pigs, false, PrefetchAction.Cancel)]
    [InlineData(PrefetchState.Downloading, Pigs, false, PrefetchAction.Cancel)]
    [InlineData(PrefetchState.Landed, Pigs, true, PrefetchAction.CancelAndRemove)]
    [InlineData(PrefetchState.Landed, Pigs, false, PrefetchAction.Cancel)]
    [InlineData(PrefetchState.Failed, Pigs, false, PrefetchAction.Keep)]
    [InlineData(PrefetchState.Downloading, Lion, false, PrefetchAction.Keep)]
    public void Unticking_the_prefetched_book_cancels_it_and_only_a_book_this_run_landed_is_removed(PrefetchState state, string unticked, bool installedByRun, PrefetchAction expected) =>
        Assert.Equal(expected, FirstBooks.OnUntick(Pigs, state, unticked, installedByRun));

    [Theory]
    [InlineData(PrefetchState.Downloading, "en", "en", true)]
    [InlineData(PrefetchState.Landed, "EN", "en", true)]
    [InlineData(PrefetchState.Downloading, "en", "fr", false)]
    [InlineData(PrefetchState.Landed, null, "en", false)]
    [InlineData(PrefetchState.Failed, "en", "en", false)]
    public void Continue_joins_the_prefetch_only_for_the_main_language_it_fetches(PrefetchState state, string? prefetchLanguage, string main, bool joins) =>
        Assert.Equal(joins, FirstBooks.JoinsPrefetch(state, prefetchLanguage, main));

    [Fact]
    public void A_prefetch_for_another_main_language_is_not_the_first_book_source()
    {
        FirstBookPlan plan = FirstBooks.PlanContinue([Pigs, Lion], Ids(), Pigs, PrefetchState.Downloading, ["fr", "en"], "en");

        Assert.Equal(FirstBookSource.Download, plan.First!.Source);
    }

    [Fact]
    public void Background_jobs_survive_a_save_and_a_damaged_save_is_empty()
    {
        BackgroundJob[] jobs = [new(Lion, BackgroundJobKind.Package, null), new(Pigs, BackgroundJobKind.Pack, "fa")];

        Assert.Equal(jobs, BackgroundJobStore.Parse(BackgroundJobStore.Serialize(jobs)));
        Assert.Equal(string.Empty, BackgroundJobStore.Serialize([]));
        Assert.Empty(BackgroundJobStore.Parse("{not json"));
        Assert.Empty(BackgroundJobStore.Parse(null));
        Assert.Empty(BackgroundJobStore.Parse("[{\"BookId\":\"\",\"Kind\":0,\"Language\":null}]"));
    }

    [Fact]
    public void Resume_skips_books_already_on_the_shelf_and_queues_a_missing_main_pack_first_without_doubles()
    {
        BackgroundJob[] saved = [new(Lion, BackgroundJobKind.Package, null), new(Fox, BackgroundJobKind.Package, null), new(Pigs, BackgroundJobKind.Pack, "en"), new(Fox, BackgroundJobKind.Pack, "fa")];

        IReadOnlyList<BackgroundJob> jobs = FirstBooks_ResumeJobs(saved);

        Assert.Equal([new BackgroundJob(Pigs, BackgroundJobKind.Pack, "en"), new BackgroundJob(Fox, BackgroundJobKind.Package, null), new BackgroundJob(Fox, BackgroundJobKind.Pack, "fa")], jobs);
    }

    private static IReadOnlyList<BackgroundJob> FirstBooks_ResumeJobs(BackgroundJob[] saved) =>
        BackgroundJobStore.ResumeJobs(saved, Ids(Lion, Pigs), [Pigs, "ghost"], "en");

    [Theory]
    [InlineData(0, 0, 2, QueueLine.Failed)]
    [InlineData(0, 0, 0, QueueLine.Hidden)]
    [InlineData(3, 1, 2, QueueLine.OneBook)]
    public void The_banner_shows_a_failed_job_only_when_nothing_is_running(int jobs, int books, int failed, QueueLine expected) =>
        Assert.Equal(expected, FirstBooks.LineFor(jobs, books, failed));

    [Theory]
    [InlineData(PrefetchState.Idle, null, "en", PrefetchStart.Begin)]
    [InlineData(PrefetchState.Failed, "en", "en", PrefetchStart.Begin)]
    [InlineData(PrefetchState.Cancelled, "en", "en", PrefetchStart.Begin)]
    [InlineData(PrefetchState.Downloading, "en", "en", PrefetchStart.Keep)]
    [InlineData(PrefetchState.Landed, "en", "en", PrefetchStart.Keep)]
    [InlineData(PrefetchState.Downloading, "en", "fr", PrefetchStart.Restart)]
    [InlineData(PrefetchState.Landed, "en", "fr", PrefetchStart.Restart)]
    public void Saving_the_language_again_restarts_only_for_a_new_main_language(PrefetchState state, string? runLanguage, string newMain, PrefetchStart expected) =>
        Assert.Equal(expected, FirstBooks.OnLanguageSaved(state, runLanguage, newMain));

    [Theory]
    [InlineData(0, 0, QueueLine.Hidden)]
    [InlineData(0, 2, QueueLine.Hidden)]
    [InlineData(3, 0, QueueLine.Audio)]
    [InlineData(4, 1, QueueLine.OneBook)]
    [InlineData(5, 2, QueueLine.ManyBooks)]
    public void The_shelf_queue_banner_is_hidden_with_no_job_and_counts_books_before_audio(int jobs, int books, QueueLine expected) =>
        Assert.Equal(expected, FirstBooks.LineFor(jobs, books));

}
