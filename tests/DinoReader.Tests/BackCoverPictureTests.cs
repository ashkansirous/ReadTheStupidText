using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using DinoReader.BookBuilder;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 359: a book's drawn back-cover picture is packed with the book, never counted or read, and every book but Good Enough has one.</summary>
public class BackCoverPictureTests
{
    private const string StoryId = "a02c0731b7ac475a896e9184f3575e7b";
    private static readonly BackCover WithPicture = new("Aesop", true, BackCover.Maker, 2026, 11, BackCover.PictureName);

    private static Story Make(BackCover? cover) =>
        new(StoryId, "T", "en", DateTimeOffset.UnixEpoch, [new StoryPage("One."), new StoryPage("Two.")], [], CoverStyle.Colour, 0, string.Empty, BackCover: cover);

    [Fact]
    public void The_picture_is_one_of_the_stories_files_but_not_a_page()
    {
        Story story = Make(WithPicture);

        Assert.Contains(BackCover.PictureName, story.PictureFiles());
        Assert.Equal(2, story.Pages.Count);
        Assert.DoesNotContain(story.Pages, p => p.ImageFile == BackCover.PictureName);
        Assert.DoesNotContain("Retold", story.Text);
    }

    [Fact]
    public void The_back_cover_is_never_in_the_sentence_list_nor_the_text_nor_the_page_count()
    {
        Story with = Make(WithPicture);
        Story without = Make(null);

        Assert.Equal(without.Text, with.Text);
        Assert.Equal(without.Pages.Count, with.Pages.Count);
        IReadOnlyList<SentenceSpan> spans = StorySentences.Build(with.Text, LanguageRules.For(with.Language));
        Assert.Equal(StorySentences.Build(without.Text, LanguageRules.For(without.Language)).Count, spans.Count);
        Assert.DoesNotContain(spans, s => with.Text.Substring(s.Start, s.Length).Contains(WithPicture.StoryBy));
    }

    [Fact]
    public void The_picture_name_survives_story_json_and_an_older_app_gets_it_as_the_last_page_picture()
    {
        string json = StoryJson.Serialize(Make(WithPicture));
        JsonElement last = JsonDocument.Parse(json).RootElement.GetProperty("Pages").EnumerateArray().Last();

        Assert.Equal(BackCover.PictureName, last.GetProperty("ImageFile").GetString());
        Assert.Equal(WithPicture, StoryJson.Deserialize(json).BackCover);
    }

    [Fact]
    public void A_file_with_no_Picture_field_ignores_a_last_page_image_that_is_not_the_back_cover_name()
    {
        JsonNode root = JsonNode.Parse(StoryJson.Serialize(Make(WithPicture)))!;
        JsonObject last = root["Pages"]!.AsArray().Last()!.AsObject();
        Assert.True(last["BackCover"]!.AsObject().Remove("Picture"));
        last["ImageFile"] = "page-3.jpg";

        Assert.Null(StoryJson.Deserialize(root.ToJsonString()).BackCover!.PictureFile);
    }

    [Fact]
    public void A_file_with_no_Picture_field_takes_the_last_page_ImageFile_as_the_back_cover_picture()
    {
        JsonNode root = JsonNode.Parse(StoryJson.Serialize(Make(WithPicture)))!;
        JsonObject lastBackCover = root["Pages"]!.AsArray().Last()!["BackCover"]!.AsObject();
        Assert.True(lastBackCover.Remove("Picture"));

        Assert.Equal(BackCover.PictureName, StoryJson.Deserialize(root.ToJsonString()).BackCover!.PictureFile);
    }

    [Fact]
    public void A_file_with_neither_Picture_nor_ImageFile_has_no_back_cover_picture()
    {
        JsonNode root = JsonNode.Parse(StoryJson.Serialize(Make(WithPicture)))!;
        JsonObject last = root["Pages"]!.AsArray().Last()!.AsObject();
        last["BackCover"]!.AsObject().Remove("Picture");
        last.Remove("ImageFile");

        Assert.Null(StoryJson.Deserialize(root.ToJsonString()).BackCover!.PictureFile);
    }

    [Fact]
    public void The_size_reader_skips_fill_bytes_and_standalone_markers_before_the_frame_header()
    {
        byte[] plain = SquareJpegHead();
        byte[] padded = [0xFF, 0xD8, 0xFF, 0xFF, 0xFF, 0x01, 0xFF, 0xD0, 0xFF, .. plain.Skip(2)];

        Assert.Equal((100, 100), JpegSize.Of(plain));
        Assert.Equal((100, 100), JpegSize.Of(padded));
        Assert.Null(JpegSize.Of([0xFF, 0xD8, 0xFF, 0xDA, 0x00, 0x02, 0x00, 0x00]));
    }

    [Fact]
    public void Check_reports_every_problem_and_fails_a_book_that_lost_its_credit_fields_with_no_picture()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dino-backall-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "back-cover.jpg");

            IReadOnlyList<string> noPicture = BookCommands.BackCoverProblems("goldilocks", path, hasCredits: false);
            Assert.Equal(2, noPicture.Count);
            Assert.Contains(noPicture, p => p.Contains("no credit fields"));
            Assert.Contains(noPicture, p => p.Contains("no back-cover.jpg"));
            File.WriteAllBytes(path, SquareJpegHead());
            Assert.Equal(2, BookCommands.BackCoverProblems("goldilocks", path, hasCredits: false).Count); // the credit fields, and the square picture
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_back_cover_without_a_picture_keeps_the_live_page_and_writes_no_picture()
    {
        var plain = new BackCover("Aesop", true, BackCover.Maker, 2026, 11);
        string json = StoryJson.Serialize(Make(plain));

        Assert.False(JsonDocument.Parse(json).RootElement.GetProperty("Pages").EnumerateArray().Last().TryGetProperty("ImageFile", out JsonElement image) && image.ValueKind == JsonValueKind.String);
        Assert.Null(StoryJson.Deserialize(json).BackCover!.PictureFile);
    }

    [Fact]
    public async Task The_picture_travels_in_the_package_and_a_package_without_it_drops_the_name()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dino-backpic-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileStoryLibrary(folder);
            Story story = Make(WithPicture);
            await library.AddAsync(story);
            byte[] bytes = [0xFF, 0xD8, 0xFF, 0x01];
            await File.WriteAllBytesAsync(library.TryResolveFile(story.Id, BackCover.PictureName)!, bytes);
            using var package = new MemoryStream();
            await StoryPackageWriter.WriteAsync(story, library, package);

            Story imported = await StoryPackageReader.ImportAsync(new MemoryStream(package.ToArray()), new FileStoryLibrary(folder + "-in"));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(new FileStoryLibrary(folder + "-in").TryResolveFile(imported.Id, imported.BackCover!.PictureFile)!));

            using var bare = new MemoryStream();
            await StoryPackageWriter.WriteAsync(story, new FileStoryLibrary(folder + "-empty"), bare);
            Story without = await StoryPackageReader.ImportAsync(new MemoryStream(bare.ToArray()), new FileStoryLibrary(folder + "-in2"));
            Assert.Null(without.BackCover!.PictureFile);
        }
        finally
        {
            foreach (string path in new[] { folder, folder + "-in", folder + "-empty", folder + "-in2" }.Where(Directory.Exists))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [Fact]
    public async Task A_built_book_packs_its_back_cover_picture_and_the_page_count_ignores_it()
    {
        BookFolder book = Books.Folder("lion-and-mouse");
        await StarterBuilder.InScratchAsync(async library =>
        {
            Story story = StarterBuilder.WithTranslations(StarterBuilder.Build(book.Info.Id, book.Info.Title, book.Text!, book.Info.Colour), book.Stem);
            await library.AddAsync(story);
            StarterBuilder.CopyBackCoverPicture(story, book.Stem, library);
            using var package = new MemoryStream();
            await StoryPackageWriter.WriteAsync(story, library, package);

            Assert.Equal(BackCover.PictureName, story.BackCover!.PictureFile);
            using var zip = new ZipArchive(new MemoryStream(package.ToArray()));
            Assert.NotNull(zip.GetEntry(BackCover.PictureName));
            Assert.Equal(story.Pages.Count, JsonDocument.Parse(StoreBuilder.MetaJson(book.ToSpec(), story)).RootElement.GetProperty("pageCount").GetInt32());
        });
    }

    [Fact]
    public void Every_book_except_the_exempt_ones_has_its_picture_and_check_accepts_them()
    {
        string[] exempt = [StarterBuilder.GoodEnoughStem];
        foreach (BookFolder book in Books.All)
        {
            Assert.Null(BookCommands.BackCoverProblem(book.Stem, book.BackCoverPath, book.Info.BackCover is not null));
            if (!exempt.Contains(book.Stem))
            {
                Assert.True(File.Exists(book.BackCoverPath), book.Stem);
            }
        }
    }

    [Fact]
    public void Check_fails_a_picture_with_no_credit_fields_and_a_picture_that_is_not_three_by_four()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dino-backshape-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "back-cover.jpg");
            File.Copy(Books.Folder("lion-and-mouse").BackCoverPath, path);

            Assert.Null(BookCommands.BackCoverProblem("lion-and-mouse", path));
            Assert.Contains("no credit fields", BookCommands.BackCoverProblem("lion-and-mouse", path, hasCredits: false));
            File.WriteAllBytes(path, SquareJpegHead());
            Assert.Contains("not 3:4", BookCommands.BackCoverProblem("lion-and-mouse", path));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // A start-of-image marker and one baseline frame header saying 100 x 100 pixels: just enough for the size reader.
    private static byte[] SquareJpegHead() =>
        [0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x64, 0x00, 0x64, 0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01];

    [Fact]
    public void Check_fails_a_book_with_no_picture_except_good_enough_and_a_file_that_is_not_a_jpeg()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dino-backchk-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "back-cover.jpg");

            Assert.Contains("no back-cover.jpg", BookCommands.BackCoverProblem("goldilocks", path));
            Assert.Null(BookCommands.BackCoverProblem(StarterBuilder.GoodEnoughStem, path));
            File.WriteAllText(path, "not a picture");
            Assert.Contains("not a JPEG", BookCommands.BackCoverProblem("goldilocks", path));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_hash_covers_the_picture_but_not_the_high_res_original()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dino-backhash-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "high-res"));
            BookInfo info = Books.Folder("lion-and-mouse").Info;
            var book = new BookFolder("t", folder, info);
            string before = BookHash.Of(book);
            File.WriteAllBytes(Path.Combine(folder, "high-res", "back-cover.jpg"), [1, 2, 3]);
            Assert.Equal(before, BookHash.Of(book));

            File.WriteAllBytes(book.BackCoverPath, [0xFF, 0xD8, 0xFF]);
            Assert.NotEqual(before, BookHash.Of(book));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
