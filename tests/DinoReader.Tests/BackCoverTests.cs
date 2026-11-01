using DinoReader.BookBuilder;
using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Tests;

/// <summary>Slice 237: every book ends on a silent back cover (credits); it is beside the pages, never in them, and an older app reads it as a last page.</summary>
public class BackCoverTests
{
    private static readonly BackCover Retold = new("Aesop", true, BackCover.Maker, 2026, 11);
    private static readonly BackCover Own = new("Ashkan Sirous", false, BackCover.Maker, 2026, 9);

    private static Story Make(BackCover? cover) =>
        new("a02c0731b7ac475a896e9184f3575e7b", "T", "en", DateTimeOffset.UnixEpoch, [new StoryPage("One."), new StoryPage("Two.")], [], CoverStyle.Colour, 0, string.Empty, BackCover: cover);

    [Fact]
    public void A_retold_book_names_the_original_author_the_retelling_the_illustration_and_the_month()
    {
        Assert.Equal(
            [BackCoverLineKind.OriginalStoryBy, BackCoverLineKind.RetoldBy, BackCoverLineKind.Illustration, BackCoverLineKind.Date],
            Retold.Lines().Select(l => l.Kind));
        Assert.Equal("Aesop", Retold.Lines()[0].Name);
        Assert.Equal("Ashkan Sirous", Retold.Lines()[1].Name);
    }

    [Fact]
    public void An_own_book_says_story_by()
    {
        Assert.Equal([BackCoverLineKind.StoryBy, BackCoverLineKind.Illustration, BackCoverLineKind.Date], Own.Lines().Select(l => l.Kind));
    }

    [Fact]
    public void A_traditional_tale_has_no_author_line_but_is_still_retold()
    {
        var cover = new BackCover(BackCover.Traditional, true, BackCover.Maker, 2026, 10);

        Assert.Equal([BackCoverLineKind.TraditionalTale, BackCoverLineKind.RetoldBy, BackCoverLineKind.Illustration, BackCoverLineKind.Date], cover.Lines().Select(l => l.Kind));
    }

    [Fact]
    public void The_english_page_text_has_one_paragraph_per_line()
    {
        Assert.Equal("Original story by Aesop\n\nRetold for young readers by Ashkan Sirous\n\nIllustration: Ashkan Sirous\n\nNovember 2026", BackCoverEnglish.PageText(Retold));
        Assert.Equal("Story by Ashkan Sirous\n\nIllustration: Ashkan Sirous\n\nSeptember 2026", BackCoverEnglish.PageText(Own));
    }

    [Fact]
    public void It_round_trips_through_story_json_as_a_back_cover_and_not_as_a_page()
    {
        Story back = StoryJson.Deserialize(StoryJson.Serialize(Make(Retold)));

        Assert.Equal(Retold, back.BackCover);
        Assert.Equal(["One.", "Two."], back.Pages.Select(p => p.Text));
        Assert.DoesNotContain("Aesop", back.Text);
    }

    [Fact]
    public void An_older_app_sees_it_as_one_more_last_page_with_the_english_credits()
    {
        using var document = System.Text.Json.JsonDocument.Parse(StoryJson.Serialize(Make(Retold)));
        var pages = document.RootElement.GetProperty("Pages").EnumerateArray().ToList();

        Assert.Equal(3, pages.Count);
        Assert.Equal(BackCoverEnglish.PageText(Retold), pages[2].GetProperty("Text").GetString());
    }

    [Fact]
    public void A_book_without_one_writes_no_extra_page()
    {
        using var document = System.Text.Json.JsonDocument.Parse(StoryJson.Serialize(Make(null)));

        Assert.Equal(2, document.RootElement.GetProperty("Pages").GetArrayLength());
        Assert.Null(StoryJson.Deserialize(StoryJson.Serialize(Make(null))).BackCover);
    }

    [Fact]
    public void A_damaged_back_cover_is_dropped_with_its_page()
    {
        string json = StoryJson.Serialize(Make(Retold)).Replace("2026-11", "soon");

        Story back = StoryJson.Deserialize(json);

        Assert.Null(back.BackCover);
        Assert.Equal(2, back.Pages.Count);
    }

    [Fact]
    public void Several_names_are_listed_in_order_and_traditional_ignores_case_and_spaces()
    {
        Assert.Equal(["Elham Shafiei Gol", "Ashkan Sirous"], BackCover.Names("Elham Shafiei Gol; Ashkan Sirous"));
        Assert.True(BackCover.IsTraditional("  Traditional "));
        Assert.False(BackCover.IsTraditional("Aesop"));
        var two = new BackCover("Elham Shafiei Gol; Ashkan Sirous", false, BackCover.Maker, 2026, 11);
        Assert.StartsWith("Story by Elham Shafiei Gol and Ashkan Sirous", BackCoverEnglish.PageText(two));
        Assert.Equal(BackCoverLineKind.TraditionalTale, new BackCover("Traditional", true, BackCover.Maker, 2026, 11).Lines()[0].Kind);
    }

    [Fact]
    public void A_story_file_with_only_a_back_cover_page_is_refused()
    {
        string json = StoryJson.Serialize(Make(Retold) with { Pages = [] });

        Assert.Throws<StoryFormatException>(() => StoryJson.Deserialize(json));
    }

    [Fact]
    public async Task It_survives_a_package()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dino-backcover-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileStoryLibrary(folder);
            Story story = Make(Own);
            await library.AddAsync(story);
            using var stream = new MemoryStream();
            await StoryPackageWriter.WriteAsync(story, library, stream);
            stream.Position = 0;
            var reader = new FileStoryLibrary(folder + "-in");
            Story imported = await StoryPackageReader.ImportAsync(stream, reader);

            Assert.Equal(Own, imported.BackCover);
        }
        finally
        {
            foreach (string path in new[] { folder, folder + "-in" }.Where(Directory.Exists))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("2026-11", true, 2026, 11)]
    [InlineData("2026-10-24", true, 2026, 10)]
    [InlineData("2026-13", false, 0, 0)]
    [InlineData("soon", false, 0, 0)]
    [InlineData(null, false, 0, 0)]
    public void The_month_comes_from_the_added_date(string? text, bool ok, int year, int month)
    {
        Assert.Equal(ok, BackCover.TryParseMonth(text, out int y, out int m));
        if (ok)
        {
            Assert.Equal((year, month), (y, m));
        }
    }

    [Fact]
    public void Every_book_has_a_back_cover_built_from_its_book_json()
    {
        foreach (BookFolder book in Books.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(book.Info.StoryBy), $"{book.Stem}: storyBy");
            Assert.NotNull(book.Info.BackCover);
        }
    }

    [Fact]
    public void Books_by_the_user_are_not_retold_and_the_classics_are()
    {
        Assert.Equal("Elham Shafiei Gol; Ashkan Sirous", Books.Folder("freddy-and-the-slipper").Info.StoryBy);
        foreach (string own in new[] { "freddy-and-the-slipper", "good-enough", "x-marks-the-treasure" })
        {
            Assert.NotEqual(true, Books.Folder(own).Info.Retold);
        }

        foreach (BookFolder classic in Books.All.Where(b => b.Info.StoryBy != "Ashkan Sirous" && !b.Stem.StartsWith("freddy", StringComparison.Ordinal)))
        {
            Assert.True(classic.Info.Retold, classic.Stem);
        }
    }

    [Fact]
    public void The_back_cover_is_not_a_page_so_it_is_not_counted_by_the_store()
    {
        BookFolder book = Books.Folder("lion-and-mouse");
        Story story = StarterBuilder.WithTranslations(StarterBuilder.Build(book.Info.Id, book.Info.Title, book.Text!, book.Info.Colour), book.Stem);

        Assert.NotNull(story.BackCover);
        Assert.DoesNotContain("Retold for young readers", story.Text);
        Assert.Equal(story.Pages.Count, System.Text.Json.JsonDocument.Parse(StoreBuilder.MetaJson(book.ToSpec(), story)).RootElement.GetProperty("pageCount").GetInt32());
    }

    [Fact]
    public void The_hash_covers_the_back_cover_fields()
    {
        BookFolder book = Books.Folder("lion-and-mouse");
        BookFolder other = new(book.Stem, book.Dir, book.Info with { StoryBy = "Somebody else" });

        Assert.NotEqual(BookHash.Of(book), BookHash.Of(other));
    }

    [Fact]
    public void A_book_json_without_storyBy_fails_to_load()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dino-nostoryby-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "story.md"), "Hi.");
            File.WriteAllText(Path.Combine(folder, "book.json"), File.ReadAllText(Path.Combine(Books.ContentRoot(), "lion-and-mouse", "book.json")).Replace("\"storyBy\"", "\"storyByX\""));

            Assert.Throws<InvalidOperationException>(() => Books.ReadInfo(folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
