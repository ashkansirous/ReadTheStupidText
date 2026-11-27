using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 459, "Concurrency, one design" (review pass 2): the pure rules of the one serial background worker, plus the check pass's leftover-part match and missing-listing rule.</summary>
public class BackgroundQueueTests
{
    private const string Pigs = "pigs";
    private const string Lion = "lion";
    private const string Fox = "fox";

    private static BackgroundJob Package(string id, int attempts = 0) => new(id, BackgroundJobKind.Package, null, attempts);

    private static BackgroundJob Pack(string id, string language, int attempts = 0) => new(id, BackgroundJobKind.Pack, language, attempts);

    private static HashSet<string> Ids(params string[] ids) => new(ids, StringComparer.OrdinalIgnoreCase);

    // --- job parse (finding 5) ---

    [Fact]
    public void A_saved_list_holding_a_null_entry_drops_it_and_is_reported_damaged()
    {
        IReadOnlyList<BackgroundJob> jobs = BackgroundJobStore.Parse("[null]", out bool damaged);

        Assert.Empty(jobs);
        Assert.True(damaged);
    }

    [Fact]
    public void A_null_entry_beside_a_good_one_keeps_the_good_one()
    {
        IReadOnlyList<BackgroundJob> jobs = BackgroundJobStore.Parse("[null,{\"BookId\":\"lion\",\"Kind\":0,\"Language\":null,\"Attempts\":2}]", out bool damaged);

        Assert.Equal([Package(Lion, 2)], jobs);
        Assert.True(damaged);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"a\":1}")]
    [InlineData("[1,2]")]
    [InlineData("[\"x\"]")]
    [InlineData("{not json")]
    public void Any_unreadable_saved_value_is_empty_and_damaged_and_never_throws(string saved)
    {
        Assert.Empty(BackgroundJobStore.Parse(saved, out bool damaged));
        Assert.True(damaged);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void No_saved_value_is_empty_and_not_damaged(string? saved)
    {
        Assert.Empty(BackgroundJobStore.Parse(saved, out bool damaged));
        Assert.False(damaged);
    }

    [Fact]
    public void An_old_save_without_attempts_reads_as_zero_attempts()
    {
        Assert.Equal([Pack(Pigs, "fa")], BackgroundJobStore.Parse("[{\"BookId\":\"pigs\",\"Kind\":1,\"Language\":\"fa\"}]"));
    }

    // --- drop rules (findings 3, 10 and the retry budget) ---

    [Fact]
    public void A_pack_job_of_a_book_deleted_from_the_shelf_is_dropped_at_resume()
    {
        BackgroundJob[] saved = [Pack(Lion, "fa"), Pack(Pigs, "fa")];

        IReadOnlyList<BackgroundJob> jobs = BackgroundJobStore.ResumeJobs(saved, Ids(Pigs), [], "en");

        Assert.Equal([Pack(Pigs, "fa")], jobs);
    }

    [Fact]
    public void A_pack_job_of_a_book_whose_package_is_still_queued_is_kept_at_resume()
    {
        BackgroundJob[] saved = [Package(Fox), Pack(Fox, "fa")];

        Assert.Equal(saved, BackgroundJobStore.ResumeJobs(saved, Ids(), [], "en"));
    }

    [Fact]
    public void A_job_past_its_retry_budget_is_dropped_at_resume_and_one_at_the_budget_stays()
    {
        BackgroundJob[] saved = [Package(Lion, BackgroundJobRules.MaxRetryLaunches), Package(Fox, BackgroundJobRules.MaxRetryLaunches + 1)];

        Assert.Equal([Package(Lion, BackgroundJobRules.MaxRetryLaunches)], BackgroundJobStore.ResumeJobs(saved, Ids(), [], "en"));
    }

    [Theory]
    [InlineData(BackgroundJobKind.Package, false, true, false, JobVerdict.Drop)]
    [InlineData(BackgroundJobKind.Pack, false, true, true, JobVerdict.Drop)]
    [InlineData(BackgroundJobKind.Package, true, false, false, JobVerdict.Run)]
    [InlineData(BackgroundJobKind.Package, true, true, false, JobVerdict.Drop)]
    [InlineData(BackgroundJobKind.Pack, true, true, false, JobVerdict.Run)]
    [InlineData(BackgroundJobKind.Pack, true, false, false, JobVerdict.Drop)]
    [InlineData(BackgroundJobKind.Pack, true, false, true, JobVerdict.Fail)]
    public void The_verdict_drops_a_gone_book_a_deleted_book_and_a_finished_package(BackgroundJobKind kind, bool listed, bool onShelf, bool packageQueued, JobVerdict expected)
    {
        BackgroundJob job = new(Pigs, kind, kind == BackgroundJobKind.Pack ? "fa" : null);

        Assert.Equal(expected, BackgroundJobRules.Verdict(job, listed, liveListingRead: true, onShelf, packageQueued));
    }

    // --- generation filter and serial worker (findings 4, 6, 8, 9) ---

    [Theory]
    [InlineData(BackgroundResult.Done, false, BackgroundResult.Done)]
    [InlineData(BackgroundResult.Failed, false, BackgroundResult.Failed)]
    [InlineData(BackgroundResult.Cancelled, false, BackgroundResult.Failed)]
    [InlineData(BackgroundResult.Done, true, BackgroundResult.Cancelled)]
    [InlineData(BackgroundResult.Failed, true, BackgroundResult.Cancelled)]
    [InlineData(BackgroundResult.Cancelled, true, BackgroundResult.Cancelled)]
    public void A_stale_job_is_forgotten_and_a_cancel_nobody_asked_for_is_a_failure(BackgroundResult result, bool stale, BackgroundResult counted) =>
        Assert.Equal(counted, BackgroundJobRules.Settle(result, stale));

    [Fact]
    public void A_job_cancelled_by_another_owner_fails_and_the_queue_goes_on()
    {
        BackgroundQueueModel queue = new();
        queue.Add([Package(Lion), Package(Fox)]);
        queue.TakeNext();

        queue.Settle(BackgroundResult.Cancelled);

        Assert.Equal(1, queue.Failed);
        Assert.Equal(Fox, queue.TakeNext()!.BookId);
    }

    [Fact]
    public void Start_while_a_queue_runs_adds_its_jobs_and_drops_none()
    {
        BackgroundQueueModel queue = new();
        queue.Add([Package(Lion)]);
        queue.TakeNext();

        int added = queue.Add([Package(Fox), Pack(Fox, "fa")]);

        Assert.Equal(2, added);
        Assert.Equal(3, queue.Remaining);
        Assert.Equal(2, queue.BooksRemaining);
    }

    [Fact]
    public void A_job_already_waiting_or_running_is_not_added_twice()
    {
        BackgroundQueueModel queue = new();
        queue.Add([Package(Lion), Package(Fox)]);
        queue.TakeNext();

        Assert.Equal(0, queue.Add([Package(Lion), Package("FOX")]));
        Assert.Equal(2, queue.Remaining);
    }

    [Fact]
    public void Cancel_then_Start_at_once_runs_the_new_jobs_after_the_old_one_unwinds()
    {
        BackgroundQueueModel queue = new();
        queue.Add([Package(Lion), Package(Fox)]);
        queue.TakeNext(); // Lion runs

        queue.Cancel();
        queue.Add([Package(Pigs)]);

        Assert.True(queue.CurrentIsStale);
        Assert.Equal(1, queue.Remaining); // only Pigs counts: Lion is stale, Fox was forgotten
        Assert.Equal(BackgroundResult.Cancelled, queue.Settle(BackgroundResult.Done)); // Lion landed anyway: it is not counted, not failed
        Assert.Equal(0, queue.Failed);
        Assert.Equal(Pigs, queue.TakeNext()!.BookId);
        Assert.Null(queue.TakeNext());
    }

    [Fact]
    public void Cancel_forgets_the_failed_jobs_and_a_kill_then_saves_nothing_of_the_old_generation()
    {
        BackgroundQueueModel queue = new();
        queue.Add([Package(Lion), Package(Fox)]);
        queue.TakeNext();
        queue.Settle(BackgroundResult.Failed);
        queue.TakeNext();

        queue.Cancel();

        Assert.Equal(0, queue.Failed);
        Assert.Empty(queue.Snapshot());
        Assert.Equal(0, queue.Remaining);
    }

    [Fact]
    public void Retry_puts_the_failed_jobs_back_in_order_with_one_more_attempt_counted()
    {
        BackgroundQueueModel queue = new();
        queue.Add([Package(Lion), Pack(Lion, "fa")]);
        queue.TakeNext();
        queue.Settle(BackgroundResult.Failed);
        queue.TakeNext();
        queue.Settle(BackgroundResult.Failed);

        Assert.Equal(2, queue.Retry());

        Assert.Equal(Package(Lion, 1), queue.TakeNext());
        Assert.Equal(Pack(Lion, "fa", 1), queue.TakeNext());
    }

    [Fact]
    public void The_snapshot_keeps_failed_then_running_then_waiting_for_a_kill()
    {
        BackgroundQueueModel queue = new();
        queue.Add([Package(Lion), Package(Fox), Pack(Fox, "fa")]);
        queue.TakeNext();
        queue.Settle(BackgroundResult.Failed);
        queue.TakeNext();

        Assert.Equal([Package(Lion, 1), Package(Fox), Pack(Fox, "fa")], queue.Snapshot());
    }

    [Fact]
    public void A_pack_waits_as_a_failed_job_while_its_package_is_failed_or_queued()
    {
        BackgroundQueueModel queue = new();
        queue.Add([Package(Fox)]);
        Assert.True(queue.PackageQueued(Fox));
        queue.TakeNext();
        queue.Settle(BackgroundResult.Failed);

        Assert.True(queue.PackageQueued("FOX"));
        Assert.False(queue.PackageQueued(Lion));
    }

    // --- check pass #3: leftover .part files ---

    [Theory]
    [InlineData("story-0123456789abcdef0123456789ABCDEF.part", "story", true)]
    [InlineData("STORY-0123456789abcdef0123456789abcdef.PART", "story", true)]
    [InlineData("story-fa-0123456789abcdef0123456789abcdef.part", "story", false)]
    [InlineData("story-fa-0123456789abcdef0123456789abcdef.part", "story-fa", true)]
    [InlineData("story-0123456789abcdef0123456789abcde.part", "story", false)]
    [InlineData("story-0123456789abcdef0123456789abcdeg.part", "story", false)]
    [InlineData("story-0123456789abcdef0123456789abcdef.zip", "story", false)]
    [InlineData("other-0123456789abcdef0123456789abcdef.part", "story", false)]
    public void A_leftover_part_matches_only_its_own_book_by_the_exact_name(string fileName, string bookId, bool expected) =>
        Assert.Equal(expected, LeftoverParts.IsPartOf(fileName, bookId));

    // --- check pass #8: a book missing from the listing ---

    [Theory]
    [InlineData(true, JobVerdict.Drop)]
    [InlineData(false, JobVerdict.Fail)]
    public void A_book_missing_from_the_listing_is_gone_only_when_the_listing_was_read_live(bool liveListingRead, JobVerdict expected)
    {
        BackgroundJob job = Package(Pigs);

        Assert.Equal(expected, BackgroundJobRules.Verdict(job, listed: false, liveListingRead, onShelf: false, packageQueued: false));
    }

    [Fact]
    public void A_job_failed_for_a_missing_listing_keeps_its_place_until_the_retry_budget_is_spent()
    {
        BackgroundQueueModel queue = new();
        queue.Add([Package(Pigs)]);
        queue.TakeNext();
        queue.Settle(BackgroundResult.Failed);

        Assert.Equal(1, queue.Failed);
        BackgroundJob failed = Assert.Single(queue.Snapshot());
        Assert.Equal(1, failed.Attempts);
        Assert.Single(BackgroundJobStore.ResumeJobs([failed], Ids(), [], "en"));
        Assert.Empty(BackgroundJobStore.ResumeJobs([failed with { Attempts = BackgroundJobRules.MaxRetryLaunches + 1 }], Ids(), [], "en"));
    }
}
