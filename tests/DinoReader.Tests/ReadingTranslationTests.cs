using DinoReader.Core;
using ReadTheStupidText.Application.Translation;

namespace DinoReader.Tests;

public class ReadingTranslationTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Farsi =
        new Dictionary<string, IReadOnlyDictionary<string, string>> { ["fa"] = new Dictionary<string, string> { ["Hello there."] = "salam." } };

    private static Story StoryWith(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? translations, bool translate = true, bool showOriginal = false) =>
        new("0123456789abcdef0123456789abcdef", "T", "en", DateTimeOffset.UnixEpoch, [new StoryPage("Hello there.")], [], CoverStyle.Colour, 0, "v",
            TranslateToMyLanguage: translate, ShowOriginalText: showOriginal, Translations: translations);

    private static bool CanRead(Story story, string language, bool hasEngine, params string[] recorded) =>
        ReadingTranslation.CanRead(story, language, hasEngine, MlKitLanguages.All, OwnBookTranslation.PackLanguages(story.Language, language), new TranslationPackRecord(recorded));

    [Fact]
    public void The_books_own_language_is_always_readable()
    {
        Assert.True(CanRead(StoryWith(null, translate: false), "en", hasEngine: false));
    }

    [Fact]
    public void A_complete_table_is_readable_with_no_engine_and_no_record()
    {
        Assert.True(CanRead(StoryWith(Farsi), "fa", hasEngine: false));
        Assert.True(CanRead(StoryWith(Farsi), "fa", hasEngine: true));
    }

    [Fact]
    public void No_table_needs_the_engine_and_every_pack_in_the_record()
    {
        Story own = StoryWith(null);
        Assert.False(CanRead(own, "fa", hasEngine: true));
        Assert.False(CanRead(own, "fa", hasEngine: true, "fa"));
        Assert.True(CanRead(own, "fa", hasEngine: true, "fa", "en"));
        Assert.False(CanRead(own, "fa", hasEngine: false, "fa", "en"));
    }

    [Fact]
    public void A_table_with_holes_is_readable_only_when_the_record_covers_the_pair()
    {
        var holey = new Dictionary<string, IReadOnlyDictionary<string, string>> { ["fa"] = new Dictionary<string, string> { ["Other."] = "x" } };
        Assert.False(CanRead(StoryWith(holey), "fa", hasEngine: true));
        Assert.True(CanRead(StoryWith(holey), "fa", hasEngine: true, "en", "fa"));
    }

    [Fact]
    public void Show_original_text_and_a_switched_off_book_never_translate()
    {
        Assert.False(CanRead(StoryWith(Farsi, showOriginal: true), "fa", hasEngine: true, "en", "fa"));
        Assert.False(CanRead(StoryWith(Farsi, translate: false), "fa", hasEngine: true, "en", "fa"));
    }

    [Fact]
    public void The_engine_is_used_on_a_read_only_when_the_record_holds_every_pack()
    {
        Assert.False(ReadingTranslation.MayUseEngine(true, ["en", "fa"], new TranslationPackRecord(["fa"])));
        Assert.True(ReadingTranslation.MayUseEngine(true, ["en", "fa"], new TranslationPackRecord(["fa", "en"])));
        Assert.False(ReadingTranslation.MayUseEngine(false, ["en", "fa"], new TranslationPackRecord(["fa", "en"])));
    }

    [Fact]
    public void The_record_follows_set_add_and_round_trips_through_text()
    {
        var record = new TranslationPackRecord();
        Assert.True(record.Set("FA", true));
        Assert.False(record.Set("fa", true));
        Assert.True(record.AllReady(["fa"]));
        Assert.True(record.Set("fa", false));
        Assert.False(record.AllReady(["fa"]));
        Assert.True(record.AllReady([]));

        Assert.True(record.AddRange(["nb", "en"]));
        Assert.False(record.AddRange(["en", "nb"]));
        Assert.Equal(["en", "nb"], TranslationPackRecord.Parse(record.Serialize()).Languages);
        Assert.Empty(TranslationPackRecord.Parse(null).Languages);
    }

    [Fact]
    public void Reconciling_only_adds_so_a_pack_recorded_after_the_engine_list_was_read_stays()
    {
        var record = new TranslationPackRecord();
        record.Set("fa", true); // a download finished meanwhile
        Assert.True(record.AddRange(["en"])); // the engine list, read earlier, does not mention fa
        Assert.Equal(["en", "fa"], record.Languages);
    }

    [Fact]
    public void A_pack_goes_in_after_a_download_and_out_only_when_set_gone()
    {
        var record = new TranslationPackRecord();
        string[] pair = [.. OwnBookTranslation.PackLanguages("en", "fa")];
        foreach (string pack in pair)
        {
            record.Set(pack, true);
        }

        Assert.True(record.AllReady(pair));
        record.Set("fa", false); // the engine re-check said only fa is gone; en stays
        Assert.False(record.AllReady(pair));
        Assert.True(record.AllReady(["en"]));
    }

    [Fact]
    public void The_engine_language_table_holds_the_59_languages_with_the_app_spelling_of_norwegian()
    {
        Assert.Equal(59, MlKitLanguages.All.Count);
        Assert.Contains("nb", MlKitLanguages.All);
        Assert.DoesNotContain("no", MlKitLanguages.All);
        Assert.Equal(MlKitLanguages.All.Count, MlKitLanguages.All.Distinct().Count());
    }
}
