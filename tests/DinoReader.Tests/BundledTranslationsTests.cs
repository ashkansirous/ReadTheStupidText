using DinoReader.Core;

namespace DinoReader.Tests;

public class BundledTranslationsTests
{

    private static Story StoryWith(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? translations, bool translate = true, string language = "en") =>
        new("0123456789abcdef0123456789abcdef", "T", language, DateTimeOffset.UnixEpoch, [new StoryPage("Hello there.")], [], CoverStyle.Colour, 0, "v",
            TranslateToMyLanguage: translate, Translations: translations);

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Farsi =
        new Dictionary<string, IReadOnlyDictionary<string, string>> { ["fa"] = new Dictionary<string, string> { ["Hello there."] = "salam." } };

    [Fact]
    public void IsComplete_is_true_only_when_every_piece_has_an_entry()
    {
        Assert.True(BundledTranslations.IsComplete(StoryWith(Farsi), "fa"));
        Assert.False(BundledTranslations.IsComplete(StoryWith(Farsi), "fr"));
        var holey = new Dictionary<string, IReadOnlyDictionary<string, string>> { ["fa"] = new Dictionary<string, string> { ["Other."] = "x" } };
        Assert.False(BundledTranslations.IsComplete(StoryWith(holey), "fa"));
    }

    [Fact]
    public void CanTranslate_is_true_for_the_same_language_a_complete_table_or_an_engine_pair()
    {
        string[] engine = ["en", "fa"];

        Assert.True(BundledTranslations.CanTranslate(StoryWith(null), "en", null));
        Assert.True(BundledTranslations.CanTranslate(StoryWith(Farsi), "fa", null));
        Assert.True(BundledTranslations.CanTranslate(StoryWith(null), "fa", engine));
        Assert.False(BundledTranslations.CanTranslate(StoryWith(null), "fa", null));
        Assert.False(BundledTranslations.CanTranslate(StoryWith(null), "fr", engine));
        Assert.True(BundledTranslations.CanTranslate(StoryWith(null, translate: false), "fa", engine));
    }

    [Fact]
    public void CanTranslate_ignores_a_table_with_holes_only_when_there_is_no_engine()
    {
        var holey = new Dictionary<string, IReadOnlyDictionary<string, string>> { ["fa"] = new Dictionary<string, string> { ["Other."] = "x" } };

        Assert.False(BundledTranslations.CanTranslate(StoryWith(holey), "fa", null));
        Assert.True(BundledTranslations.CanTranslate(StoryWith(holey), "fa", ["en", "fr"]));
    }

    [Fact]
    public async Task A_bundled_table_translates_with_no_engine()
    {
        Story story = StoryWith(Farsi);
        var pieces = StoryPieces.Of(story);

        var result = await StoryTranslation.TranslateAsync(pieces, BundledTranslations.Compose(story, "fa", null), CancellationToken.None);

        Assert.Equal("salam.", result.Single().Segment.Text);
        Assert.Equal(pieces.Single().Segment.SourceStart, result.Single().Segment.SourceStart);
    }

    [Fact]
    public async Task A_piece_missing_from_the_table_goes_to_the_engine_then_stays_as_it_is()
    {
        Story story = StoryWith(Farsi);
        PieceTranslator engine = (text, _) => Task.FromResult("E:" + text);

        Assert.Equal("salam.", await BundledTranslations.Compose(story, "fa", engine)("Hello there.", CancellationToken.None));
        Assert.Equal("E:Other.", await BundledTranslations.Compose(story, "fa", engine)("Other.", CancellationToken.None));
        Assert.Equal("Other.", await BundledTranslations.Compose(story, "fa", null)("Other.", CancellationToken.None));
    }

    [Fact]
    public void Applies_needs_the_switch_a_different_language_and_a_table_or_an_engine_pair()
    {
        string[] engine = ["en", "fa"];

        Assert.True(BundledTranslations.Applies(StoryWith(Farsi), "fa", null));
        Assert.False(BundledTranslations.Applies(StoryWith(Farsi, translate: false), "fa", null));
        Assert.False(BundledTranslations.Applies(StoryWith(Farsi), "en", null));
        Assert.False(BundledTranslations.Applies(StoryWith(Farsi), "fr", null));
        Assert.True(BundledTranslations.Applies(StoryWith(null), "fa", engine));
        Assert.False(BundledTranslations.Applies(StoryWith(null), "fr", engine));
        Assert.False(BundledTranslations.Applies(StoryWith(null), "fa", null));
    }

    [Fact]
    public async Task Translations_round_trip_through_a_dino_package()
    {
        string root = Path.Combine(Path.GetTempPath(), "dino-tr-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileStoryLibrary(root);
            Story story = StoryWith(Farsi);
            await library.AddAsync(story);
            using var package = new MemoryStream();
            await StoryPackageWriter.WriteAsync(story, library, package);
            package.Position = 0;

            Story imported = await StoryPackageReader.ImportAsync(package, library);

            Assert.Equal("salam.", BundledTranslations.Lookup(imported, "fa", "Hello there."));
            Assert.Null(StoryJson.Deserialize(StoryJson.Serialize(StoryWith(null))).Translations);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static Story TwoSentences(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? translations = null) =>
        StoryWith(translations) with { Pages = [new StoryPage("Hello there. Good night.")] };

    [Fact]
    public void A_table_with_a_hole_is_reported_missing_but_a_punctuation_only_piece_is_not()
    {
        Story story = StoryWith(Farsi);

        Assert.False(BundledTranslations.MissesAny(story, "fa", ["Hello there.", "..."]));
        Assert.True(BundledTranslations.MissesAny(story, "fa", ["Hello there.", "Other."]));
    }

    [Fact]
    public void A_table_filed_under_a_regional_or_upper_case_code_is_found()
    {
        var table = new Dictionary<string, string> { ["Hello there."] = "salam." };
        Story regional = StoryWith(new Dictionary<string, IReadOnlyDictionary<string, string>> { ["fa-IR"] = table });
        Story upper = StoryWith(new Dictionary<string, IReadOnlyDictionary<string, string>> { ["FA"] = table });

        Assert.Equal("salam.", BundledTranslations.Lookup(regional, "fa", "Hello there."));
        Assert.Equal("salam.", BundledTranslations.Lookup(upper, "fa-IR", "Hello there."));
        Assert.True(BundledTranslations.Applies(regional, "fa", null));
    }

    [Fact]
    public void Language_keys_are_normalised_and_merged_when_a_story_file_is_read()
    {
        var raw = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["FA"] = new Dictionary<string, string> { ["Hello there."] = "salam." },
            ["fa-IR"] = new Dictionary<string, string> { ["Hello there."] = "other", ["Bye."] = "khoda" },
        };
        string json = StoryJson.Serialize(StoryWith(raw));

        Story story = StoryJson.Deserialize(json);

        Assert.Equal(["fa"], story.Translations!.Keys);
        Assert.Equal("salam.", story.Translations["fa"]["Hello there."]);
        Assert.Equal("khoda", story.Translations["fa"]["Bye."]);
    }

    [Fact]
    public void A_read_resumed_inside_a_sentence_starts_at_that_sentences_start()
    {
        Story story = TwoSentences();
        var pieces = StoryPieces.Of(story);
        int inFirst = pieces[0].Segment.SourceStart + 3;
        int inSecond = pieces[1].Segment.SourceStart + 2;

        Assert.Equal(["Hello there.", "Good night."], StoryPieces.FromWholePiece(pieces, inFirst).Select(p => p.Segment.Text));
        Assert.Equal(["Good night."], StoryPieces.FromWholePiece(pieces, inSecond).Select(p => p.Segment.Text));
        Assert.Empty(StoryPieces.FromWholePiece(pieces, story.Text.Length));
    }

    [Fact]
    public void The_stored_copy_of_a_starter_without_translations_takes_the_bundled_ones_and_keeps_its_edits()
    {
        Story bundled = StoryWith(Farsi, translate: true);
        Story stored = StoryWith(null, translate: false) with { Title = "Mine", CoverColourIndex = 3 };

        Story adopted = BundledTranslations.Adopt(stored, bundled);

        Assert.Equal("salam.", BundledTranslations.Lookup(adopted, "fa", "Hello there."));
        Assert.True(adopted.TranslateToMyLanguage);
        Assert.Equal("Mine", adopted.Title);
        Assert.Equal(3, adopted.CoverColourIndex);
    }

    [Fact]
    public void A_nine_language_copy_gains_only_the_languages_it_lacks_and_keeps_its_own_tables()
    {
        var old = new Dictionary<string, IReadOnlyDictionary<string, string>> { ["fa"] = new Dictionary<string, string> { ["Hello there."] = "my edit" } };
        var shipped = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["fa"] = new Dictionary<string, string> { ["Hello there."] = "bundled fa" },
            ["sv"] = new Dictionary<string, string> { ["Hello there."] = "hej." },
            ["nl"] = new Dictionary<string, string> { ["Hello there."] = "hallo." },
        };
        Story stored = StoryWith(old, translate: false) with { Title = "Mine" };

        Story adopted = BundledTranslations.Adopt(stored, StoryWith(shipped));

        Assert.Equal("my edit", BundledTranslations.Lookup(adopted, "fa", "Hello there."));
        Assert.Equal("hej.", BundledTranslations.Lookup(adopted, "sv", "Hello there."));
        Assert.Equal("hallo.", BundledTranslations.Lookup(adopted, "nl", "Hello there."));
        Assert.False(adopted.TranslateToMyLanguage);
        Assert.Equal("Mine", adopted.Title);
    }

    [Fact]
    public void A_copy_that_already_has_every_bundled_language_or_a_bundle_without_any_is_left_alone()
    {
        Story withTables = StoryWith(Farsi, translate: false);

        Assert.Same(withTables, BundledTranslations.Adopt(withTables, StoryWith(Farsi)));
        Story plain = StoryWith(null, translate: false);
        Assert.Same(plain, BundledTranslations.Adopt(plain, StoryWith(null)));
    }
}
