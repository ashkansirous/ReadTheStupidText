using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 262: which shelf books lack their pre-made audio in a language, and the state record the Story languages card shows.
public sealed class ShelfAudioPlanTests
{
    private const string English = "en";
    private const string Farsi = "fa";

    private static StoreItem Book(string id, string title, int? farsiPack = null, int? swedishPack = null, IReadOnlyDictionary<string, string>? titles = null)
    {
        var audio = new Dictionary<string, AudioPackInfo>();
        if (farsiPack is { } fa)
        {
            audio[Farsi] = new AudioPackInfo(fa, 1000, $"https://x/{id}-fa.zip");
        }

        if (swedishPack is { } sv)
        {
            audio["sv"] = new AudioPackInfo(sv, 1000, $"https://x/{id}-sv.zip");
        }

        return new StoreItem(id, title, English, 1, 1, 5, 3, null, "https://x/" + id, Titles: titles, Audio: audio.Count == 0 ? null : audio);
    }

    private static ISet<string> Shelf(params string[] ids) => new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);

    private static Func<string, string, int?> Installed(params (string Book, string Lang, int Version)[] packs) =>
        (book, lang) => packs.Where(p => p.Book == book && p.Lang == lang).Select(p => (int?)p.Version).FirstOrDefault();

    [Fact]
    public void A_shelf_book_with_a_listed_pack_and_nothing_installed_is_needed()
    {
        ShelfAudioWork work = ShelfAudioPlan.For([Book("a", "A", farsiPack: 1), Book("b", "B", farsiPack: 2)], Shelf("a", "b"), Farsi, Installed(), English);

        Assert.Equal(["a", "b"], work.Needed.Select(n => n.Item.Id));
        Assert.Equal(2, work.Needed[1].Pack.Version);
        Assert.Empty(work.NoAudioTitles);
    }

    [Fact]
    public void An_installed_pack_of_the_listed_version_is_not_needed_and_an_older_one_is()
    {
        ShelfAudioWork work = ShelfAudioPlan.For([Book("a", "A", farsiPack: 2), Book("b", "B", farsiPack: 2)], Shelf("a", "b"), Farsi, Installed(("a", Farsi, 2), ("b", Farsi, 1)), English);

        Assert.Equal(["b"], work.Needed.Select(n => n.Item.Id));
    }

    [Fact]
    public void A_newer_installed_pack_than_listed_is_left_alone()
    {
        ShelfAudioWork work = ShelfAudioPlan.For([Book("a", "A", farsiPack: 1)], Shelf("a"), Farsi, Installed(("a", Farsi, 3)), English);

        Assert.Empty(work.Needed);
    }

    [Fact]
    public void Books_off_the_shelf_are_ignored_and_the_shelf_match_ignores_case()
    {
        ShelfAudioWork work = ShelfAudioPlan.For([Book("ABC", "A", farsiPack: 1), Book("def", "D", farsiPack: 1)], Shelf("abc"), Farsi, Installed(), English);

        Assert.Equal(["ABC"], work.Needed.Select(n => n.Item.Id));
    }

    [Fact]
    public void A_book_the_store_has_no_pack_for_is_named_and_not_needed()
    {
        ShelfAudioWork work = ShelfAudioPlan.For([Book("a", "A", farsiPack: 1), Book("lion", "The Lion and the Mouse", swedishPack: 1)], Shelf("a", "lion"), Farsi, Installed(), English);

        Assert.Equal(["a"], work.Needed.Select(n => n.Item.Id));
        Assert.Equal(["The Lion and the Mouse"], work.NoAudioTitles);
    }

    [Fact]
    public void A_book_holding_a_pack_the_listing_no_longer_offers_is_neither()
    {
        ShelfAudioWork work = ShelfAudioPlan.For([Book("a", "A")], Shelf("a"), Farsi, Installed(("a", Farsi, 1)), English);

        Assert.Empty(work.Needed);
        Assert.Empty(work.NoAudioTitles);
    }

    [Fact]
    public void The_no_audio_title_is_the_listing_title_in_the_screen_language_when_it_has_one()
    {
        var titles = new Dictionary<string, string> { ["sv"] = "Lejonet och musen" };

        ShelfAudioWork work = ShelfAudioPlan.For([Book("lion", "The Lion and the Mouse", titles: titles)], Shelf("lion"), Farsi, Installed(), "sv");

        Assert.Equal(["Lejonet och musen"], work.NoAudioTitles);
    }

    [Fact]
    public void The_language_is_looked_up_by_its_book_key()
    {
        ShelfAudioWork work = ShelfAudioPlan.For([Book("a", "A", farsiPack: 1)], Shelf("a"), "fa-IR", Installed(("a", Farsi, 1)), English);

        Assert.Empty(work.Needed);
    }

    [Fact]
    public void An_empty_shelf_or_listing_needs_nothing()
    {
        Assert.Empty(ShelfAudioPlan.For([], Shelf("a"), Farsi, Installed(), English).Needed);
        Assert.Empty(ShelfAudioPlan.For([Book("a", "A", farsiPack: 1)], Shelf(), Farsi, Installed(), English).Needed);
    }

    [Fact]
    public void The_state_counts_books_done_and_never_passes_the_total()
    {
        var state = new ShelfAudioState(ShelfAudioPhase.Running, 0, 2, "A");

        state = state.WithBookDone();
        Assert.Equal(0.5, state.Fraction);
        Assert.Null(state.CurrentTitle);

        state = state.WithBookDone().WithBookDone();
        Assert.Equal(2, state.Done);
        Assert.Equal(1, state.Fraction);
    }

    [Fact]
    public void A_french_and_farsi_family_is_planned_for_exactly_fr_and_fa()
    {
        var family = StoryLanguageSet.Create("fr", ["fa"]);
        StoreItem book = Book("a", "A", farsiPack: 1) with { Audio = new Dictionary<string, AudioPackInfo>
        {
            ["fa"] = new AudioPackInfo(1, 1000, "https://x/a-fa.zip"),
            ["fr"] = new AudioPackInfo(1, 1000, "https://x/a-fr.zip"),
            ["en"] = new AudioPackInfo(1, 1000, "https://x/a-en.zip"),
        } };

        string[] asked = [.. family.All.SelectMany(lang => ShelfAudioPlan.For([book], Shelf("a"), lang, Installed(), English).Needed.Select(n => BundledTranslations.Key(lang)))];

        Assert.Equal(["fr", "fa"], asked);
    }

    [Fact]
    public void A_main_language_equal_to_the_books_own_gets_no_no_audio_note()
    {
        Assert.Empty(ShelfAudioPlan.For([Book("a", "A")], Shelf("a"), "en", Installed(), English).NoAudioTitles);
    }

    [Fact]
    public void A_book_that_gave_up_is_failed_not_done_and_still_moves_the_bar()
    {
        var state = new ShelfAudioState(ShelfAudioPhase.Running, 0, 3, "A").WithBookFailed();
        Assert.Equal(0, state.Done);
        Assert.Equal(1, state.Failed);
        Assert.Equal(2, state.Position);

        state = state.WithBookDone().WithBookDone();
        Assert.Equal(2, state.Done);
        Assert.Equal(1, state.Failed);
        Assert.Equal(1, state.Fraction);
        Assert.Equal(3, state.Position);
    }

    [Fact]
    public void The_no_audio_list_defaults_to_empty_and_keeps_its_snapshot()
    {
        Assert.Empty(ShelfAudioState.Idle.NoAudio);
        var titles = new List<string> { "A" };
        var state = ShelfAudioState.Idle with { NoAudioTitles = [.. titles] };
        titles.Add("B");
        Assert.Equal(["A"], state.NoAudio);
    }

    [Fact]
    public void Preparing_shows_the_card_with_an_empty_bar()
    {
        var state = new ShelfAudioState(ShelfAudioPhase.Preparing);
        Assert.True(state.IsActive);
        Assert.Equal(0, state.Fraction);
        Assert.Equal(0, state.Position);
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(4, 240)]
    [InlineData(5, 300)]
    [InlineData(40, 300)]
    [InlineData(-1, 15)]
    public void The_backoff_doubles_from_15_seconds_up_to_5_minutes(int attempt, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), ShelfAudioBackoff.Delay(attempt));
    }

    [Fact]
    public void Only_a_running_or_waiting_state_shows_the_card()
    {
        Assert.False(ShelfAudioState.Idle.IsActive);
        Assert.False(new ShelfAudioState(ShelfAudioPhase.Cancelled).IsActive);
        Assert.True(new ShelfAudioState(ShelfAudioPhase.Running, 0, 1).IsActive);
        Assert.True(new ShelfAudioState(ShelfAudioPhase.WaitingForInternet, 0, 1).IsActive);
        Assert.True(new ShelfAudioState(ShelfAudioPhase.WaitingForWifi, 0, 1).IsActive);
        Assert.Equal(0, ShelfAudioState.Idle.Fraction);
    }
}
