using System.Text.RegularExpressions;
using DinoReader.Core;
using Sirous.Reading.Application.Usage;

namespace DinoReader.Tests;

// Slice 313: a shelf book's text and its audio pack stay in step: which books are behind the store, which packs wait for their text, and what a silent piece reports.
public sealed class BookTextCurrencyTests
{
    private static ISet<string> Shelf(params string[] ids) => new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);

    private static StoreItem Book(string id, int version, int farsiPack)
    {
        var audio = new Dictionary<string, AudioPackInfo> { ["fa"] = new AudioPackInfo(farsiPack, 1000, $"https://x/{id}-fa.zip") };
        return new StoreItem(id, id, "en", 1, version, 5, 3, null, "https://x/" + id, Audio: audio);
    }

    private static Func<string, int?> Recorded(params (string Id, int Version)[] versions) =>
        id => versions.Where(v => v.Id == id).Select(v => (int?)v.Version).FirstOrDefault();

    [Fact]
    public void A_shelf_book_below_the_store_version_is_behind()
    {
        var store = new[] { new StoreBookVersion("a", 6), new StoreBookVersion("b", 3) };

        IReadOnlyList<StoreBookVersion> behind = BookTextCurrency.Behind(store, Shelf("a", "b"), Recorded(("a", 5), ("b", 3)));

        Assert.Equal(["a"], behind.Select(b => b.Id));
    }

    [Fact]
    public void A_book_with_no_recorded_version_counts_as_the_first_release()
    {
        IReadOnlyList<StoreBookVersion> behind = BookTextCurrency.Behind([new StoreBookVersion("a", 2)], Shelf("a"), Recorded());

        Assert.Single(behind);
    }

    [Fact]
    public void A_store_book_that_is_not_on_the_shelf_is_never_behind()
    {
        Assert.Empty(BookTextCurrency.Behind([new StoreBookVersion("a", 9)], Shelf("other"), Recorded(("a", 1))));
    }

    [Fact]
    public void Still_behind_keeps_an_update_waiting_and_an_unfetched_baseline_but_not_skipped_books()
    {
        var decisions = new[]
        {
            new BookUpdateDecision("a", BookUpdateAction.Update),
            new BookUpdateDecision("b", BookUpdateAction.NeedsBaseline),
            new BookUpdateDecision("c", BookUpdateAction.Skip, BookUpdateSkip.Edited),
            new BookUpdateDecision("d", BookUpdateAction.Skip, BookUpdateSkip.UpToDate),
            new BookUpdateDecision("e", BookUpdateAction.Update),
        };

        ISet<string> still = BookTextCurrency.StillBehind(decisions, ["A", "b", "c", "d"]);

        Assert.Equal(2, still.Count);
        Assert.Contains("a", still);
        Assert.Contains("b", still);
        Assert.DoesNotContain("e", still);
    }

    [Fact]
    public void A_pack_made_for_newer_text_is_not_fetched_while_the_text_is_behind()
    {
        // Freddy, 2026-11-07: v5 text on the phone, a pack made for v6 in the store. The pack waits; the text goes first.
        StoreItem freddy = Book("freddy", version: 6, farsiPack: 3);

        ShelfAudioWork work = ShelfAudioPlan.For([freddy], Shelf("freddy"), "fa", (_, _) => null, "en", textBehind: Shelf("freddy"));

        Assert.Empty(work.Needed);
        Assert.Empty(work.NoAudioTitles);
    }

    [Fact]
    public void The_pack_is_fetched_once_the_text_is_current()
    {
        StoreItem freddy = Book("freddy", version: 6, farsiPack: 3);

        ShelfAudioWork work = ShelfAudioPlan.For([freddy], Shelf("freddy"), "fa", (_, _) => 2, "en", textBehind: Shelf());

        Assert.Equal(["freddy"], work.Needed.Select(n => n.Item.Id));
    }

    [Fact]
    public void A_book_whose_text_is_current_is_planned_as_before_beside_one_that_is_behind()
    {
        ShelfAudioWork work = ShelfAudioPlan.For([Book("old", 6, 3), Book("new", 2, 1)], Shelf("old", "new"), "fa", (_, _) => null, "en", textBehind: Shelf("old"));

        Assert.Equal(["new"], work.Needed.Select(n => n.Item.Id));
    }

    [Fact]
    public void A_silent_piece_of_a_pack_out_of_line_with_the_text_reports_text_mismatch()
    {
        var misses = new ClipMisses();
        misses.Note(0, ClipMiss.TextMismatch);
        misses.Note(1, ClipMiss.FileMissing);

        Assert.Equal(FallbackReason.TextMismatch, ClipMisses.SilentPieceReason(DeviceVoiceReason.ClipMissing, misses));
    }

    [Fact]
    public void A_silent_piece_with_only_missing_files_reports_file_missing()
    {
        var misses = new ClipMisses();
        misses.Note(0, ClipMiss.FileMissing);

        Assert.Equal(FallbackReason.FileMissing, ClipMisses.SilentPieceReason(DeviceVoiceReason.ClipMissing, misses));
    }

    [Fact]
    public void A_silent_piece_the_pack_never_had_reports_no_clip_but_a_hash_miss_still_wins()
    {
        var misses = new ClipMisses();
        misses.Note(0, ClipMiss.NoClip);
        Assert.Equal(FallbackReason.NoClip, ClipMisses.SilentPieceReason(DeviceVoiceReason.ClipMissing, misses));

        misses.Note(1, ClipMiss.TextMismatch);
        Assert.Equal(FallbackReason.TextMismatch, ClipMisses.SilentPieceReason(DeviceVoiceReason.ClipMissing, misses));
    }

    [Theory]
    [InlineData(DeviceVoiceReason.NoPack)]
    [InlineData(DeviceVoiceReason.ClipMissing)]
    public void A_silent_piece_of_an_own_book_reports_nothing_but_a_shelf_books_names_its_cause(DeviceVoiceReason reason)
    {
        Assert.Null(ClipMisses.SilentPieceReasonFor(false, reason, null));
        Assert.Null(ClipMisses.SilentPieceReasonFor(false, reason, new ClipMisses()));
        Assert.Equal(ClipMisses.SilentPieceReason(reason, null), ClipMisses.SilentPieceReasonFor(true, reason, null));
    }

    [Theory]
    [InlineData(FallbackReason.NoPack)]
    [InlineData(FallbackReason.PackFailed)]
    [InlineData(FallbackReason.TextMismatch)]
    public void A_whole_read_of_an_own_book_reports_no_pack_fallback_but_a_shelf_book_keeps_its_reason(string reason)
    {
        Assert.Null(ClipMisses.ReadReasonFor(false, reason));
        Assert.Equal(reason, ClipMisses.ReadReasonFor(true, reason));
    }

    [Fact]
    public void A_pack_with_nothing_looked_up_reports_clip_missing()
    {
        Assert.Equal(FallbackReason.ClipMissing, ClipMisses.SilentPieceReason(DeviceVoiceReason.ClipMissing, new ClipMisses()));
    }

    [Theory]
    [InlineData(DeviceVoiceReason.TextMismatch, FallbackReason.TextMismatch)]
    [InlineData(DeviceVoiceReason.NoPack, FallbackReason.NoPack)]
    [InlineData(DeviceVoiceReason.PackFailed, FallbackReason.PackFailed)]
    [InlineData(DeviceVoiceReason.ClipMissing, FallbackReason.ClipMissing)]
    public void With_no_pack_opened_the_read_reason_decides(DeviceVoiceReason reason, string expected)
    {
        Assert.Equal(expected, ClipMisses.SilentPieceReason(reason, null));
    }

    [Theory]
    [InlineData(BookUpdateResult.Staged, 5, 6, "staged:5:6")]
    [InlineData(BookUpdateResult.Updated, 1, 12, "updated:1:12")]
    [InlineData(BookUpdateResult.SkippedEdited, 5, 6, "skipped_edited:5:6")]
    [InlineData(BookUpdateResult.Failed, -1, 6, "failed:0:6")]
    public void The_book_update_detail_is_result_from_to(string result, int from, int to, string expected)
    {
        Assert.Equal(expected, BookUpdateUsage.Detail(result, from, to));
    }

    [Fact]
    public void The_book_update_event_name_is_the_one_the_worker_accepts()
    {
        Assert.Equal("book_update", DinoReader.Core.UsageEventName.BookUpdate);
        Assert.Equal(["staged", "updated", "skipped_edited", "failed"], BookUpdateResult.All);
    }

    [Fact]
    public void The_Worker_takes_exactly_the_book_update_results_the_app_can_send()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "worker", "events.ts"));
        Match list = Regex.Match(source, @"BOOK_UPDATE_RESULTS = \[(?<names>[^\]]*)\]");

        Assert.True(list.Success);
        string[] inWorker = Regex.Matches(list.Groups["names"].Value, "\"(?<name>[a-z_]+)\"").Select(m => m.Groups["name"].Value).ToArray();
        Assert.Equal(BookUpdateResult.All.Order(), inWorker.Order());
    }

    [Fact]
    public void A_book_whose_edit_was_settled_for_the_store_version_is_not_behind()
    {
        var store = new[] { new StoreBookVersion("a", 6), new StoreBookVersion("b", 6) };

        IReadOnlyList<StoreBookVersion> behind = BookTextCurrency.Behind(store, Shelf("a", "b"), Recorded(("a", 5), ("b", 5)), Recorded(("a", 6)));

        Assert.Equal(["b"], behind.Select(b => b.Id));
    }

    [Fact]
    public void A_seen_version_below_the_store_version_leaves_the_book_behind()
    {
        IReadOnlyList<StoreBookVersion> behind = BookTextCurrency.Behind([new StoreBookVersion("a", 7)], Shelf("a"), Recorded(("a", 5)), Recorded(("a", 6)));

        Assert.Single(behind);
    }

    [Fact]
    public void A_book_given_up_for_a_version_is_given_up_for_that_version()
    {
        Assert.True(BookTextGiveUp.IsGivenUp(BookTextGiveUp.Note(6), 6));
    }

    [Fact]
    public void A_newer_store_version_is_not_given_up()
    {
        Assert.False(BookTextGiveUp.IsGivenUp(BookTextGiveUp.Note(6), 7));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("6:3")]
    public void Nothing_stored_or_an_unreadable_note_is_not_given_up(string? note)
    {
        Assert.False(BookTextGiveUp.IsGivenUp(note, 6));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReadTheStupidText.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo root not found.");
    }
}
