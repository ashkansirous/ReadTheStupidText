using System.Xml.Linq;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 362: which page the listen and voice steps use (Decision 2), the try's cleanup (Decision 1), and the small pure machines of the listen step and the corner tour.</summary>
public class OnboardingDemoTests
{
    private static readonly string[] Pigs = [OnboardingDemo.ThreeLittlePigsId];

    [Fact]
    public void The_three_little_pigs_win_whenever_they_are_on_the_shelf()
    {
        OnboardingDemoPage? pick = OnboardingDemo.Pick([OnboardingDemo.XMarksTheTreasureId, OnboardingDemo.ThreeLittlePigsId, OnboardingDemo.LionAndMouseId]);

        Assert.Equal(OnboardingDemo.ThreeLittlePigsId, pick!.StoreId);
        Assert.Equal(13, pick.Page);
        Assert.Equal("Pim", pick.Character);
    }

    [Theory]
    [InlineData(new[] { OnboardingDemo.LionAndMouseId, OnboardingDemo.GingerbreadManId, OnboardingDemo.XMarksTheTreasureId }, OnboardingDemo.LionAndMouseId, 11, "Mouse")]
    [InlineData(new[] { OnboardingDemo.XMarksTheTreasureId, OnboardingDemo.GingerbreadManId }, OnboardingDemo.GingerbreadManId, 3, "Granny")]
    [InlineData(new[] { OnboardingDemo.GingerbreadManId, OnboardingDemo.LionAndMouseId }, OnboardingDemo.LionAndMouseId, 11, "Mouse")]
    [InlineData(new[] { OnboardingDemo.XMarksTheTreasureId }, OnboardingDemo.XMarksTheTreasureId, 10, "Pancake Polly")]
    public void Without_the_pigs_the_first_of_lion_gingerbread_and_x_marks_wins_whatever_the_pick_order(string[] shelf, string id, int page, string character)
    {
        OnboardingDemoPage? pick = OnboardingDemo.Pick(shelf);

        Assert.Equal(id, pick!.StoreId);
        Assert.Equal(page, pick.Page);
        Assert.Equal(character, pick.Character);
    }

    [Fact]
    public void Every_order_of_the_four_free_books_gives_the_same_pick_as_the_priority_list()
    {
        string[] ids = [.. OnboardingDemo.Pages.Select(p => p.StoreId)];
        foreach (IEnumerable<string> order in Permutations(ids))
        {
            string[] shelf = [.. order];
            string expected = ids.First(shelf.Contains);
            Assert.Equal(expected, OnboardingDemo.Pick(shelf)!.StoreId);
        }
    }

    [Fact]
    public void A_shelf_with_none_of_the_four_has_no_demo_and_ids_match_in_any_case()
    {
        Assert.Null(OnboardingDemo.Pick(["0123456789abcdef0123456789abcdef", null]));
        Assert.Null(OnboardingDemo.Pick([]));
        Assert.Equal(OnboardingDemo.ThreeLittlePigsId, OnboardingDemo.Pick(Pigs.Select(id => id.ToUpperInvariant()))!.StoreId);
    }

    [Fact]
    public void Each_demo_page_is_a_set_one_based_page_with_a_named_character()
    {
        Assert.Equal(4, OnboardingDemo.Pages.Count);
        Assert.All(OnboardingDemo.Pages, p => Assert.False(string.IsNullOrWhiteSpace(p.Character)));
        Assert.Equal(12, OnboardingDemo.Pages[0].PageIndex);
        Assert.Equal(OnboardingDemo.Pages.Count, OnboardingDemo.Pages.Select(p => p.StoreId).Distinct().Count());
    }

    [Fact]
    public void Step_five_walks_only_the_demo_characters_lines_on_the_demo_page()
    {
        OnboardingDemoPage demo = OnboardingDemo.Pages[0];
        RecordPiece[] pieces =
        [
            new(StoryClipKey.OfPage(12, "en"), "Pop", "Uh-oh!"),
            new(StoryClipKey.OfPage(12, "en"), "Pim", "Don't worry."),
            new(StoryClipKey.OfPage(11, "en"), "Pim", "Another page."),
            new(StoryClipKey.OfPage(12, "en"), "", "A narrator line."),
        ];

        IReadOnlyList<RecordPiece> lines = OnboardingDemo.LinesOf(pieces, demo);

        Assert.Equal(["Don't worry."], lines.Select(p => p.Text));
    }

    [Fact]
    public void The_demo_pages_sentences_are_found_by_page()
    {
        OnboardingDemoPage demo = OnboardingDemo.Pages[0];
        RecordSentence[] sentences = [new(11, 0, "a", "", true), new(12, 0, "b", "Pop", false), new(12, 1, "c", "Pim", true), new(13, 0, "d", "", true)];

        Assert.Equal((1, 2), OnboardingDemo.SentenceRange(sentences, demo));
        Assert.Null(OnboardingDemo.SentenceRange([new(0, 0, "a", "", true)], demo));
    }

    [Fact]
    public void Only_the_clips_the_try_recorded_are_listed_for_deletion()
    {
        Story book = new(StoryIds.New(), "Pigs", "en", DateTimeOffset.UnixEpoch, Enumerable.Range(0, 14).Select(i => new StoryPage($"Page {i}.")).ToList(), [], CoverStyle.Colour, 0, string.Empty);
        StoryClipKey before = StoryClipKey.OfPage(2, "en");
        StoryClipKey tried = StoryClipKey.OfPage(12, "en");
        Story story = StoryRecordings.WithClip(StoryRecordings.WithClip(book, before, new PageRecording("en", 3, "rec-3-aa.m4a")), tried, new PageRecording("en", 4, "rec-13-bb.m4a"));
        OnboardingTryRecord record = new(book.Id, [new TryClip(tried, "rec-13-bb.m4a")]);

        Assert.Equal([tried], record.ClipsToDelete(story));
    }

    [Fact]
    public void A_clip_replaced_since_or_already_gone_is_not_listed()
    {
        Story book = new(StoryIds.New(), "Pigs", "en", DateTimeOffset.UnixEpoch, Enumerable.Range(0, 14).Select(i => new StoryPage($"Page {i}.")).ToList(), [], CoverStyle.Colour, 0, string.Empty);
        StoryClipKey key = StoryClipKey.OfPage(12, "en");
        OnboardingTryRecord record = new(book.Id, [new TryClip(key, "rec-13-bb.m4a")]);
        Story replaced = StoryRecordings.WithClip(book, key, new PageRecording("en", 4, "rec-13-cc.m4a"));

        Assert.Empty(record.ClipsToDelete(replaced));
        Assert.Empty(record.ClipsToDelete(book));
    }

    [Fact]
    public void A_clip_of_another_book_joins_the_record_and_keeps_the_earlier_ones_listed()
    {
        StoryClipKey key = StoryClipKey.OfPage(12, "en");
        OnboardingTryRecord record = new OnboardingTryRecord("book-a", []).With(key, "a.m4a", "book-a").With(key, "b.m4a", "book-b");

        OnboardingTryRecord? back = OnboardingTryRecord.FromJson(record.ToJson());

        Assert.NotNull(back);
        Assert.Equal(2, back.Clips.Count);
        Assert.Equal(["book-a", "book-b"], back.PerBook().Select(r => r.BookId).Order().ToList());
        Assert.Contains(new TryClip(key, "a.m4a"), back.Clips);
        Assert.Contains(new TryClip(key, "b.m4a", "book-b"), back.Clips);
    }

    [Fact]
    public void The_record_survives_a_round_trip_and_a_re_recorded_key_replaces_its_clip()
    {
        StoryClipKey key = new(12, 0, 1, "en");
        OnboardingTryRecord record = new OnboardingTryRecord("book", []).With(key, "a.m4a").With(new StoryClipKey(12, 2, 3, "en"), "b.m4a").With(key, "c.m4a");

        OnboardingTryRecord? back = OnboardingTryRecord.FromJson(record.ToJson());

        Assert.NotNull(back);
        Assert.Equal("book", back.BookId);
        Assert.Equal(record.Clips, back.Clips);
        Assert.Equal(2, back.Clips.Count);
        Assert.Contains(new TryClip(key, "c.m4a"), back.Clips);
        Assert.Null(OnboardingTryRecord.FromJson("not json"));
        Assert.Null(OnboardingTryRecord.FromJson(null));
    }

    [Theory]
    [InlineData(false, null, false)]
    [InlineData(true, OnboardingStep.Voice, false)]
    [InlineData(true, OnboardingStep.Corner, true)]
    [InlineData(true, OnboardingStep.Listen, true)]
    [InlineData(true, null, true)]
    public void An_abandoned_try_is_deleted_at_start_unless_onboarding_is_on_the_voice_step(bool recordExists, OnboardingStep? step, bool expected) =>
        Assert.Equal(expected, OnboardingTryRules.DeleteAtStart(recordExists, step));

    [Fact]
    public void The_bar_has_six_segments_and_each_step_fills_up_to_itself()
    {
        Assert.Equal(6, OnboardingFlow.Count);
        Assert.Equal([1, 2, 3, 4, 5, 6], Enum.GetValues<OnboardingStep>().Select(OnboardingFlow.FilledAt));
        Assert.Equal(1, OnboardingFlow.NumberOf(OnboardingStep.Language));
        Assert.Equal(5, OnboardingFlow.NumberOf(OnboardingStep.Voice));
    }

    [Fact]
    public void Each_step_counts_under_its_own_closed_name_and_the_path_runs_in_order()
    {
        Assert.Equal(IntroSteps.Onboarding, Enum.GetValues<OnboardingStep>().Select(OnboardingFlow.NameOf));
        Assert.Equal(OnboardingStep.StoryLanguages, OnboardingFlow.After(OnboardingStep.Language));
        Assert.Equal(OnboardingStep.Corner, OnboardingFlow.After(OnboardingStep.Voice));
        Assert.Null(OnboardingFlow.After(OnboardingStep.Corner));
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(0, null)]
    [InlineData(2, null)]
    [InlineData(3, OnboardingStep.Listen)]
    [InlineData(4, OnboardingStep.Voice)]
    [InlineData(5, OnboardingStep.Corner)]
    [InlineData(6, null)]
    public void Only_the_listen_voice_and_corner_steps_are_resumed_after_a_restart(int stored, OnboardingStep? expected)
    {
        Assert.Equal(expected, OnboardingFlow.Saved(stored));
    }

    [Fact]
    public void With_two_languages_the_bubble_follows_the_first_page_and_continue_wakes_as_the_second_starts()
    {
        var demo = new ListenDemo(["en", "fa"]);
        demo.LanguageStarted("en");
        Assert.Equal(ListenStage.First, demo.Stage);
        Assert.False(demo.ContinueLive);
        Assert.False(demo.BubbleShown);

        demo.PageEnded();
        Assert.True(demo.BubbleShown);
        Assert.Equal("fa", demo.Pointed);
        Assert.False(demo.ContinueLive);

        demo.LanguageStarted("fa");
        Assert.Equal(ListenStage.Other, demo.Stage);
        Assert.True(demo.ContinueLive);
        Assert.False(demo.BubbleShown);
    }

    [Fact]
    public void Tapping_the_second_language_early_wakes_continue_without_the_bubble()
    {
        var demo = new ListenDemo(["en", "fa", "de"]);

        demo.LanguageStarted("de");

        Assert.True(demo.ContinueLive);
        Assert.False(demo.BubbleShown);
    }

    [Fact]
    public void With_one_family_language_continue_wakes_when_the_page_ends_and_there_is_no_bubble()
    {
        var demo = new ListenDemo(["en"]);
        Assert.False(demo.HasOther);
        Assert.Null(demo.Pointed);
        Assert.False(demo.ContinueLive);

        demo.PageEnded();

        Assert.True(demo.ContinueLive);
        Assert.False(demo.BubbleShown);
    }

    [Fact]
    public void The_tour_runs_welcome_then_three_tabs_then_the_shelf_pointer_then_the_shelf_card()
    {
        List<TourStop> walked = [TourStop.Welcome];
        while (CornerTourFlow.Next(walked[^1]) is { } next)
        {
            walked.Add(next);
        }

        Assert.Equal([TourStop.Welcome, TourStop.Books, TourStop.Store, TourStop.Settings, TourStop.ShelfPointer, TourStop.ChildShelf], walked);
        Assert.Equal([0, 1, 2, 3, 0, 0], walked.Select(CornerTourFlow.CounterOf));
        Assert.Equal(3, CornerTourFlow.CounterTotal);
        Assert.Equal([false, true, true, true, false, false], walked.Select(CornerTourFlow.LiftsATab));
    }

    private static IEnumerable<IEnumerable<T>> Permutations<T>(IReadOnlyList<T> items)
    {
        if (items.Count <= 1)
        {
            yield return items;
            yield break;
        }

        for (int i = 0; i < items.Count; i++)
        {
            T head = items[i];
            List<T> rest = [.. items.Where((_, index) => index != i)];
            foreach (IEnumerable<T> tail in Permutations(rest))
            {
                yield return tail.Prepend(head);
            }
        }
    }
}

/// <summary>Slice 362, Decision 1: when onboarding ends the try's clips and their files are gone from the book's folder.</summary>
public sealed class OnboardingTryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-onb-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Deleting_the_try_leaves_no_clip_and_no_recording_file_on_the_book()
    {
        FileStoryLibrary library = new(_root);
        Story book = new(StoryIds.New(), "Pigs", "en", DateTimeOffset.UnixEpoch, Enumerable.Range(0, 14).Select(i => new StoryPage($"Page {i}.")).ToList(), [], CoverStyle.Colour, 0, string.Empty);
        await library.AddAsync(book);
        StoryClipKey key = StoryClipKey.OfPage(12, "en");
        string file = await library.StoreRecordingFileAsync(book.Id, "rec-13-ab12cd34.m4a", path => File.WriteAllTextAsync(path, "audio", TestContext.Current.CancellationToken));
        Story tried = StoryRecordings.WithClip(book, key, new PageRecording("en", 4, file));
        await library.UpdateAsync(tried);
        Assert.True(File.Exists(Path.Combine(library.FolderOf(book.Id), file)));

        await OnboardingTry.DeleteAsync(library, new OnboardingTryRecord(book.Id, [new TryClip(key, file)]));

        Story saved = (await library.GetAsync(book.Id))!;
        Assert.Equal(0, StoryRecordings.Count(saved));
        Assert.False(File.Exists(Path.Combine(library.FolderOf(book.Id), file)));
        Assert.Empty(Directory.EnumerateFiles(library.FolderOf(book.Id), "rec-*"));
    }

    [Fact]
    public async Task A_clip_the_book_held_before_the_try_stays()
    {
        FileStoryLibrary library = new(_root);
        Story book = new(StoryIds.New(), "Pigs", "en", DateTimeOffset.UnixEpoch, Enumerable.Range(0, 14).Select(i => new StoryPage($"Page {i}.")).ToList(), [], CoverStyle.Colour, 0, string.Empty);
        await library.AddAsync(book);
        StoryClipKey old = StoryClipKey.OfPage(2, "en");
        StoryClipKey tryKey = StoryClipKey.OfPage(12, "en");
        string oldFile = await library.StoreRecordingFileAsync(book.Id, "rec-3-aa11bb22.m4a", path => File.WriteAllTextAsync(path, "old", TestContext.Current.CancellationToken));
        string tryFile = await library.StoreRecordingFileAsync(book.Id, "rec-13-ab12cd34.m4a", path => File.WriteAllTextAsync(path, "try", TestContext.Current.CancellationToken));
        await library.UpdateAsync(StoryRecordings.WithClip(StoryRecordings.WithClip(book, old, new PageRecording("en", 3, oldFile)), tryKey, new PageRecording("en", 4, tryFile)));

        await OnboardingTry.DeleteAsync(library, new OnboardingTryRecord(book.Id, [new TryClip(tryKey, tryFile)]));

        Story saved = (await library.GetAsync(book.Id))!;
        Assert.Equal(1, StoryRecordings.Count(saved));
        Assert.NotNull(StoryRecordings.ClipOf(saved, old));
        Assert.True(File.Exists(Path.Combine(library.FolderOf(book.Id), oldFile)));
        Assert.False(File.Exists(Path.Combine(library.FolderOf(book.Id), tryFile)));
    }

    [Fact]
    public async Task A_book_that_is_gone_is_nothing_to_delete()
    {
        FileStoryLibrary library = new(_root);

        await OnboardingTry.DeleteAsync(library, new OnboardingTryRecord("missing-book", [new TryClip(StoryClipKey.OfPage(0, "en"), "rec-1-aa.m4a")]));
    }
}

/// <summary>Slice 362: the copy's bold markup.</summary>
public class OnboardingMarkupTests
{
    [Fact]
    public void Words_between_double_stars_are_bold_runs()
    {
        Assert.Equal(
            [new MarkupRun("Record Pim’s lines, then hear it with ", false), new MarkupRun("you", true), new MarkupRun(" as Pim.", false)],
            OnboardingMarkup.Split("Record Pim’s lines, then hear it with **you** as Pim."));
    }

    [Fact]
    public void Text_with_no_markup_is_one_plain_run_and_an_open_marker_is_shown_as_it_is()
    {
        Assert.Equal([new MarkupRun("Plain.", false)], OnboardingMarkup.Split("Plain."));
        Assert.Equal([new MarkupRun("Tap **Child", false)], OnboardingMarkup.Split("Tap **Child"));
        Assert.Empty(OnboardingMarkup.Split(string.Empty));
    }

    [Fact]
    public void Every_bold_marker_in_every_language_is_closed()
    {
        string folder = Path.Combine(RepoRoot(), "src", "Dino", "DinoReader.App", "Resources", "Strings");
        string[] boldKeys = ["Onboarding_VoiceBody", "Tour_ShelfLine", "Tour_ChildShelfBack"];
        foreach (string file in Directory.EnumerateFiles(folder, "AppResources*.resx"))
        {
            Dictionary<string, string> values = XDocument.Load(file).Descendants("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")!.Value);
            foreach (string key in boldKeys)
            {
                Assert.True(values.ContainsKey(key), $"{file}: {key} missing");
                Assert.Contains(OnboardingMarkup.Split(values[key]), run => run.Bold);
            }
        }
    }

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src", "Dino", "DinoReader.App")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new DirectoryNotFoundException("Repo root not found.");
    }
}
