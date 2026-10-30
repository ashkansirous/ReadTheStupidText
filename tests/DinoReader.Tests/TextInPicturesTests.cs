using DinoReader.BookBuilder;
using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Tests;

/// <summary>Slice 196: a book whose pictures carry its words opens in picture only until the reader picks a view.</summary>
public class TextInPicturesTests
{
    private static readonly StoryPage Picture = new("a", "page-01.jpg");
    private static readonly StoryPage Bare = new("a");

    private static Story Make(StoryPage page, bool textInPictures, ReadingMode? preferred = null) =>
        new("a02c0731b7ac475a896e9184f3575e7b", "T", "en", DateTimeOffset.UnixEpoch, [page], [], CoverStyle.Colour, 0, string.Empty,
            PreferredMode: preferred, TextInPictures: textInPictures);

    [Fact]
    public void Flag_on_with_pictures_opens_picture_only() =>
        Assert.Equal(ReadingMode.PictureOnly, Make(Picture, true).Mode);

    [Fact]
    public void Flag_off_with_pictures_opens_picture_and_text() =>
        Assert.Equal(ReadingMode.PictureAndText, Make(Picture, false).Mode);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void No_pictures_opens_text(bool flag) =>
        Assert.Equal(ReadingMode.Text, Make(Bare, flag).Mode);

    [Theory]
    [InlineData(true, ReadingMode.PictureAndText)]
    [InlineData(false, ReadingMode.PictureOnly)]
    [InlineData(true, ReadingMode.Text)]
    public void A_saved_choice_wins(bool flag, ReadingMode choice) =>
        Assert.Equal(choice, Make(Picture, flag, choice).Mode);

    [Fact]
    public void A_saved_choice_wins_without_pictures() =>
        Assert.Equal(ReadingMode.Text, Make(Bare, true, ReadingMode.PictureOnly).Mode);

    [Fact]
    public void The_flag_round_trips_through_story_json()
    {
        Story back = StoryJson.Deserialize(StoryJson.Serialize(Make(Picture, true)));
        Assert.True(back.TextInPictures);
        Assert.Equal(ReadingMode.PictureOnly, back.Mode);
    }

    [Fact]
    public void The_flag_is_not_written_when_off_and_reads_false_when_absent()
    {
        string json = StoryJson.Serialize(Make(Picture, false));
        Assert.DoesNotContain("TextInPictures", json);
        Assert.False(StoryJson.Deserialize(json).TextInPictures);
    }

    [Fact]
    public void A_flagged_book_page_without_lines_keeps_its_text_card_in_picture_only()
    {
        Assert.True(new StoryPage("Hello.", "9.jpg", []).ShowsTextCardInPictureOnly(true));
        Assert.True(new StoryPage("Hello.", "9.jpg").ShowsTextCardInPictureOnly(true));
    }

    [Fact]
    public void A_flagged_book_page_with_printed_words_stays_picture_only()
    {
        var lines = new[] { new PageLine("Hello.", new NormalizedRect(0.1, 0.1, 0.5, 0.1)) };
        Assert.False(new StoryPage("Hello.", "1.jpg", lines).ShowsTextCardInPictureOnly(true));
    }

    [Fact]
    public void An_unflagged_book_page_without_lines_stays_picture_only()
    {
        Assert.False(new StoryPage("Hello.", "9.jpg", []).ShowsTextCardInPictureOnly(false));
        Assert.False(new StoryPage("Hello.", "9.jpg").ShowsTextCardInPictureOnly(false));
    }

    [Fact]
    public async Task A_translated_page_keeps_whether_its_picture_prints_words()
    {
        var lines = new[] { new PageLine("Hello.", new NormalizedRect(0.1, 0.1, 0.5, 0.1)) };
        var story = new Story("t", "T", "en", DateTimeOffset.UnixEpoch, [new StoryPage("Hello.", "1.jpg", lines), new StoryPage("Bye.", "2.jpg")], [], CoverStyle.Colour, 0, string.Empty);
        var sentences = StorySentences.Build(story.Text, LanguageRules.For("en"), story.BreakMap());
        var pieces = await StoryTranslation.TranslateAsync(StoryPieces.Of(story), (t, _) => Task.FromResult("T:" + t), CancellationToken.None);

        TranslatedStory translated = TranslatedStoryBuilder.Build(story.Pages, sentences, pieces);

        Assert.False(translated.Pages[0].ShowsTextCardInPictureOnly(true));
        Assert.True(translated.Pages[1].ShowsTextCardInPictureOnly(true));
    }

    [Fact]
    public void The_book_flag_is_applied_wherever_a_package_is_built()
    {
        Story built = StarterBuilder.WithTranslations(
            new Story(StarterBuilder.GoodEnoughId, "Good Enough", "en", DateTimeOffset.UnixEpoch, [new StoryPage("a")], [], CoverStyle.Colour, 0, string.Empty),
            StarterBuilder.GoodEnoughStem);
        Assert.True(built.TextInPictures);
    }

    [Fact]
    public void The_flag_is_read_from_book_json_for_Good_Enough_only()
    {
        Assert.True(Books.Folder(StarterBuilder.GoodEnoughStem).Info.TextInPictures);
        Assert.False(Books.Folder("goldilocks").Info.TextInPictures == true);
    }

    [Fact]
    public async Task Every_built_good_enough_package_has_the_flag()
    {
        await StarterBuilder.InScratchAsync(async library =>
        {
            var built = await StarterBuilder.BuildAllAsync(library, f => f == StarterBuilder.GoodEnoughFile);
            Assert.True(built.Single().Story.TextInPictures);
        });
    }

    [Fact]
    public void Flipping_the_flag_changes_the_content_hash()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dino-tip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, BookFolder.StoryFile), "One.");
            var info = new BookInfo(BookKind.Store, "a02c0731b7ac475a896e9184f3575e7b", "Tiny", 1, 3, 6, "Me", "Mine", 0, "2026-10-27", 0, null, null, null, null, string.Empty);
            string off = BookHash.Of(new BookFolder("tiny", dir, info));
            string on = BookHash.Of(new BookFolder("tiny", dir, info with { TextInPictures = true }));
            Assert.NotEqual(off, on);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
