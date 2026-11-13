using System.Text.RegularExpressions;
using DinoReader.Core;
using FallbackArea = ReadTheStupidText.Application.Usage.FallbackArea;
using FallbackReason = ReadTheStupidText.Application.Usage.FallbackReason;
using FallbackTally = ReadTheStupidText.Application.Usage.FallbackTally;
using FallbackTotal = ReadTheStupidText.Application.Usage.FallbackTotal;

namespace DinoReader.Tests;

/// <summary>Slice 273 part A: the <c>fallback</c> count's closed detail, once-per-session tally and the Worker's matching lists.</summary>
public sealed class UsageFallbackTests
{
    [Fact]
    public void The_detail_is_area_colon_reason_and_only_for_values_on_the_closed_lists()
    {
        Assert.Equal("premade_audio:partial", FallbackUsage.Detail(FallbackArea.PremadeAudio, FallbackReason.Partial));
        Assert.Null(FallbackUsage.Detail("somewhere", FallbackReason.Partial));
        Assert.Null(FallbackUsage.Detail(FallbackArea.PremadeAudio, "a title"));
        Assert.All(FallbackReason.All, reason => Assert.NotNull(FallbackUsage.Detail(FallbackArea.PremadeAudio, reason)));
    }

    [Theory]
    [InlineData("pack", "pack_sync_failed")]
    [InlineData("pack", "storyteller_failed")]
    [InlineData("pack", "recast_failed")]
    [InlineData("pack", "catalogue_failed")]
    [InlineData("pack", "pack_failed")]
    [InlineData("ocr", "ocr_builtin_no_pack")]
    [InlineData("ocr", "scanner_start_failed")]
    [InlineData("document", "pdf_import_failed")]
    [InlineData("store", "store_unreachable")]
    [InlineData("store", "update_check_failed")]
    [InlineData("config", "config_cached")]
    [InlineData("config", "config_default")]
    [InlineData("ui_language", "ui_language_unsupported")]
    [InlineData("library", "back_cover_missing")]
    public void Slice_273_part_C_kinds_have_a_detail_the_worker_list_accepts(string area, string reason) =>
        Assert.Equal($"{area}:{reason}", FallbackUsage.Detail(area, reason));

    [Fact]
    public void Only_the_first_fallback_of_a_kind_is_sent_at_once_and_a_repeat_comes_once_with_the_session_total()
    {
        var tally = new FallbackTally();
        List<int> atOnce = [];
        for (int i = 0; i < 120; i++)
        {
            int count = tally.Note(FallbackArea.PremadeAudio, FallbackReason.TextMismatch, "book", "fa");
            if (FallbackTally.IsFirst(count))
            {
                atOnce.Add(count);
            }
        }

        tally.Note(FallbackArea.PremadeAudio, FallbackReason.ClipMissing, "book", "fa");

        Assert.Equal([1], atOnce);
        FallbackTotal repeat = Assert.Single(tally.TakeRepeats());
        Assert.Equal(new FallbackTotal(FallbackArea.PremadeAudio, FallbackReason.TextMismatch, "book", "fa", 120), repeat);
        Assert.Empty(tally.TakeRepeats());
        Assert.Empty(tally.TakeRepeats());
        tally.Note(FallbackArea.PremadeAudio, FallbackReason.TextMismatch, "book", "fa");
        Assert.Equal(121, Assert.Single(tally.TakeRepeats()).Count);
    }

    [Fact]
    public void Each_area_reason_and_book_language_has_its_own_count()
    {
        var tally = new FallbackTally();

        Assert.Equal(1, tally.Note(FallbackArea.PremadeAudio, FallbackReason.NoPack, "a", "fa"));
        Assert.Equal(2, tally.Note(FallbackArea.PremadeAudio, FallbackReason.NoPack, "a", "fa"));
        Assert.Equal(1, tally.Note(FallbackArea.PremadeAudio, FallbackReason.NoPack, "a", "en"));
        Assert.Equal(1, tally.Note(FallbackArea.PremadeAudio, FallbackReason.Partial, "a", "fa"));
        Assert.Equal(1, new FallbackTally().Note(FallbackArea.PremadeAudio, FallbackReason.NoPack, "a", "fa"));
    }

    [Fact]
    public void Two_books_with_the_same_reason_are_two_entries_and_each_gets_its_own_first_row()
    {
        var tally = new FallbackTally();

        Assert.Equal(1, tally.Note(FallbackArea.PremadeAudio, FallbackReason.NoPack, "own-1", "en"));
        Assert.Equal(1, tally.Note(FallbackArea.PremadeAudio, FallbackReason.NoPack, "own-2", "en"));
        tally.Note(FallbackArea.PremadeAudio, FallbackReason.NoPack, "own-2", "en");

        FallbackTotal repeat = Assert.Single(tally.TakeRepeats());
        Assert.Equal(("own-2", 2), (repeat.Book, repeat.Count));
    }

    [Fact]
    public void The_counts_are_left_out_of_the_JSON_unless_set()
    {
        var plain = new UsageEvent("fallback", "none", "en", "none", false, "1.0.1", "android", "2026-11-06", 1, "premade_audio:partial");

        Assert.DoesNotContain("count", UsageBatch.QueueToJson([plain]));
        string json = UsageBatch.QueueToJson([plain with { Count = 3, DeviceCount = 2 }]);
        Assert.Contains("\"count\":3", json);
        Assert.Contains("\"deviceCount\":2", json);
        Assert.Equal(3, UsageBatch.ParseQueue(json)[0].Count);
    }

    [Fact]
    public void The_Worker_takes_exactly_the_areas_and_reasons_the_app_can_send()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "worker", "events.ts"));

        Assert.Equal(FallbackArea.All.Order(), ListIn(source, "FALLBACK_AREAS").Order());
        Assert.Equal(FallbackReason.All.Order(), ListIn(source, "FALLBACK_REASONS").Order());
    }

    [Fact]
    public void The_Worker_knows_the_fallback_event_name()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "worker", "events.ts"));

        Assert.Matches(new Regex(@"^\s+" + UsageEventName.Fallback + @":", RegexOptions.Multiline), source);
    }

    private static string[] ListIn(string source, string name)
    {
        Match list = Regex.Match(source, name + @" = \[(?<names>[^\]]*)\]");
        Assert.True(list.Success, name);
        return Regex.Matches(list.Groups["names"].Value, "\"(?<name>[a-z_:]+)\"").Select(m => m.Groups["name"].Value).ToArray();
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
