using System.IO.Compression;
using System.Text.Json;
using DinoReader.BookBuilder;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 375 B: the high-res pack of a store book, built beside the standard one from <c>high-res/</c>.</summary>
public sealed class HighResPackTests : IDisposable
{
    private static readonly byte[] StandardPicture = [1, 2, 3];
    private static readonly byte[] PrintPicture = [9, 9, 9, 9, 9, 9];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-hd-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void A_book_with_no_high_res_folder_has_no_high_res_pack()
    {
        BookFolder book = TinyBook(BookKind.Store);

        Assert.Null(HighResPack.Find(book));
    }

    [Fact]
    public void A_folder_missing_one_page_the_cover_or_the_back_cover_is_not_a_pack()
    {
        BookFolder book = TinyBook(BookKind.Store);
        File.WriteAllBytes(book.CoverPath, StandardPicture);
        File.WriteAllBytes(book.BackCoverPath, StandardPicture);
        WriteHighRes(book, "1.jpg", "2.jpg");
        Assert.Null(HighResPack.Find(book)); // no cover, no back cover

        WriteHighRes(book, "cover.jpg");
        Assert.Null(HighResPack.Find(book)); // no back cover

        WriteHighRes(book, "back-cover.jpg");
        HighResFiles? files = HighResPack.Find(book);
        Assert.NotNull(files);
        Assert.Equal(2, files.Pages.Count);
        Assert.NotNull(files.Cover);
        Assert.NotNull(files.BackCover);
    }

    [Fact]
    public void Only_a_back_cover_in_high_res_ships_the_standard_pack_alone()
    {
        BookFolder book = TinyBook(BookKind.Store);
        File.WriteAllBytes(book.BackCoverPath, StandardPicture);
        WriteHighRes(book, "back-cover.jpg");

        Assert.Null(HighResPack.Find(book));
    }

    [Fact]
    public void A_complete_set_with_a_git_lfs_pointer_is_found_but_flagged_and_publishing_refuses_it()
    {
        BookFolder book = TinyBook(BookKind.Store);
        WriteHighRes(book, "1.jpg", "2.jpg");
        Assert.False(HighResPack.Find(book)!.HasPointers);
        Assert.Null(HighResPack.UnpulledProblem(book));

        File.WriteAllText(Path.Combine(HighResPack.FolderOf(book), "2.jpg"), PointerOf(PrintPicture));

        Assert.True(HighResPack.Find(book)!.HasPointers);
        Assert.Contains("tiny has a complete high-res/ folder but its pictures are Git LFS pointers", HighResPack.UnpulledProblem(book));
        Assert.Null(HighResPack.UnpulledProblem(TinyBook(BookKind.Starter)));
    }

    [Fact]
    public void The_hash_is_the_same_with_the_high_res_files_pulled_or_left_as_pointers()
    {
        BookFolder book = TinyBook(BookKind.Store);
        WriteHighRes(book, "1.jpg", "2.jpg");
        string pulled = BookHash.Of(book);

        foreach (string name in new[] { "1.jpg", "2.jpg" })
        {
            File.WriteAllText(Path.Combine(HighResPack.FolderOf(book), name), PointerOf(PrintPicture));
        }

        Assert.Equal(pulled, BookHash.Of(book));

        File.WriteAllText(Path.Combine(HighResPack.FolderOf(book), "2.jpg"), PointerOf([1, 1])); // another object: another hash
        Assert.NotEqual(pulled, BookHash.Of(book));
    }

    [Fact]
    public void The_oid_of_a_pointer_and_of_the_bytes_agree()
    {
        string real = Path.Combine(_root, "real.jpg");
        string pointer = Path.Combine(_root, "pointer.jpg");
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(real, PrintPicture);
        File.WriteAllText(pointer, PointerOf(PrintPicture));

        Assert.Equal(HighResPack.Oid(real), HighResPack.Oid(pointer));
        Assert.Equal(64, HighResPack.Oid(real).Length);
    }

    private static string PointerOf(byte[] bytes) =>
        $"version https://git-lfs.github.com/spec/v1\noid sha256:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()}\nsize {bytes.Length}\n";

    [Fact]
    public void The_hash_of_a_store_book_covers_a_complete_high_res_set_and_nothing_less()
    {
        BookFolder book = TinyBook(BookKind.Store);
        string plain = BookHash.Of(book);

        WriteHighRes(book, "1.jpg");
        Assert.Equal(plain, BookHash.Of(book)); // page 2 missing: incomplete, not part of the hash

        WriteHighRes(book, "2.jpg");
        string withHighRes = BookHash.Of(book);
        Assert.NotEqual(plain, withHighRes);

        File.WriteAllBytes(Path.Combine(HighResPack.FolderOf(book), "2.jpg"), [7, 7]);
        Assert.NotEqual(withHighRes, BookHash.Of(book)); // a high-res-only change bumps the version
    }

    [Fact]
    public void The_hash_of_a_starter_ignores_high_res_so_the_stored_hashes_stay_current()
    {
        BookFolder starter = TinyBook(BookKind.Starter);
        string plain = BookHash.Of(starter);

        WriteHighRes(starter, "1.jpg", "2.jpg");

        Assert.Equal(plain, BookHash.Of(starter));
    }

    [Fact]
    public async Task The_two_packs_carry_the_same_manifest_and_story_and_the_high_res_one_the_print_pictures()
    {
        BookFolder book = TinyBook(BookKind.Store);
        WriteHighRes(book, "1.jpg", "2.jpg");
        HighResFiles files = HighResPack.Find(book)!;
        string folder = Path.Combine(_root, "out");
        Directory.CreateDirectory(folder);

        long hdBytes = 0;
        await StarterBuilder.InScratchAsync(async library =>
        {
            Story story = StarterBuilder.WithPictures(StarterBuilder.Build(book.Info.Id, book.Info.Title, book.Text!, book.Info.Colour), StarterBuilder.PictureFilesIn(book.PicturesPath));
            await library.AddAsync(story);
            StarterBuilder.CopyPictures(story, StarterBuilder.PictureFilesIn(book.PicturesPath), library);
            await using (FileStream standard = File.Create(Path.Combine(folder, StoreBuilder.PackageFileName(3))))
            {
                await StoryPackageWriter.WriteAsync(story, library, standard, includeRecordings: false, version: 3);
            }

            hdBytes = await HighResPack.WriteAsync(story, library, files, folder, 3);
        });

        string hdPath = Path.Combine(folder, HighResPack.HdPackageFileName(3));
        Assert.Equal("story-v3.hd.dino", Path.GetFileName(hdPath));
        Assert.Equal(new FileInfo(hdPath).Length, hdBytes);
        Assert.Null(HighResPack.SameTextAndNames(Path.Combine(folder, StoreBuilder.PackageFileName(3)), hdPath));
        Assert.Equal(StandardPicture, EntryBytes(Path.Combine(folder, StoreBuilder.PackageFileName(3)), "page-1.jpg"));
        Assert.Equal(PrintPicture, EntryBytes(hdPath, "page-1.jpg"));
        Assert.Equal(PrintPicture, EntryBytes(hdPath, "page-2.jpg"));
    }

    [Fact]
    public async Task Two_packs_that_differ_in_text_or_version_are_reported()
    {
        string a = Path.Combine(_root, "a.dino");
        string b = Path.Combine(_root, "b.dino");
        string c = Path.Combine(_root, "c.dino");
        Directory.CreateDirectory(_root);
        await WriteTinyPackageAsync(a, "One.", 3);
        await WriteTinyPackageAsync(b, "One.", 3);
        await WriteTinyPackageAsync(c, "Two.", 3);
        string d = Path.Combine(_root, "d.dino");
        await WriteTinyPackageAsync(d, "One.", 4);

        Assert.Null(HighResPack.SameTextAndNames(a, b));
        Assert.Contains("story.json", HighResPack.SameTextAndNames(a, c));
        Assert.Contains("package.json", HighResPack.SameTextAndNames(a, d)); // the manifest, which carries the version
    }

    [Fact]
    public void Meta_lists_hdBytes_only_when_the_book_has_a_high_res_pack()
    {
        BookFolder book = TinyBook(BookKind.Store);
        Story story = StarterBuilder.Build(book.Info.Id, book.Info.Title, book.Text!, book.Info.Colour);

        using JsonDocument with = JsonDocument.Parse(StoreBuilder.MetaJson(book.ToSpec(), story, 1000, 9000));
        using JsonDocument without = JsonDocument.Parse(StoreBuilder.MetaJson(book.ToSpec(), story, 1000));

        Assert.Equal(9000, with.RootElement.GetProperty("hdBytes").GetInt64());
        Assert.False(without.RootElement.TryGetProperty("hdBytes", out _));
    }

    [Fact]
    public void A_live_package_at_the_same_version_keeps_its_listed_high_res_size()
    {
        BookFolder book = TinyBook(BookKind.Store);
        string hash = BookHash.Of(book);
        var live = new Dictionary<string, LiveEntry> { [book.Info.Id] = new LiveEntry(4, hash, 4, 1000, 9000) };

        StoreStorySpec same = StoreVersioning.Versioned(book, live);
        StoreStorySpec changed = StoreVersioning.Versioned(book, new Dictionary<string, LiveEntry> { [book.Info.Id] = new LiveEntry(4, "other", 4, 1000, 9000) });

        Assert.Equal(9000, same.LiveHdBytes);
        Assert.Null(changed.LiveHdBytes);
    }

    [Fact]
    public void Print_pictures_must_be_jpegs_in_three_by_four()
    {
        BookFolder book = TinyBook(BookKind.Store);
        WriteHighRes(book, "1.jpg", "2.jpg");
        File.WriteAllBytes(Path.Combine(HighResPack.FolderOf(book), "1.jpg"), JpegHead(1792, 2400));
        File.WriteAllBytes(Path.Combine(HighResPack.FolderOf(book), "2.jpg"), JpegHead(100, 100));

        IReadOnlyList<string> problems = HighResPack.PictureProblems("tiny", HighResPack.Find(book)!);

        Assert.Single(problems);
        Assert.Contains("2.jpg is 100x100, not 3:4", problems[0]);
    }

    [Fact]
    public void A_print_picture_over_one_and_a_half_megabytes_is_a_problem_naming_book_and_file()
    {
        BookFolder book = TinyBook(BookKind.Store);
        WriteHighRes(book, "1.jpg", "2.jpg");
        File.WriteAllBytes(Path.Combine(HighResPack.FolderOf(book), "1.jpg"), JpegHead(1792, 2400));
        File.WriteAllBytes(Path.Combine(HighResPack.FolderOf(book), "2.jpg"), [.. JpegHead(1792, 2400), .. new byte[1_600_000]]);

        string problem = Assert.Single(HighResPack.PictureProblems("tiny", HighResPack.Find(book)!));

        Assert.Contains("tiny/high-res/2.jpg", problem);
        Assert.Contains("1.5 MB", problem);
    }

    [Fact]
    public void The_publish_gate_reports_a_wrong_aspect_print_picture_and_ignores_a_book_with_no_set()
    {
        BookFolder book = TinyBook(BookKind.Store);
        Assert.Empty(HighResPack.GateProblems(book));

        WriteHighRes(book, "1.jpg", "2.jpg");
        File.WriteAllBytes(Path.Combine(HighResPack.FolderOf(book), "1.jpg"), JpegHead(1792, 2400));
        File.WriteAllBytes(Path.Combine(HighResPack.FolderOf(book), "2.jpg"), JpegHead(100, 100));

        Assert.Contains("2.jpg is 100x100, not 3:4", Assert.Single(HighResPack.GateProblems(book)));
    }

    [Fact]
    public void A_pointer_with_no_oid_line_is_named_by_the_gate_and_never_throws_in_the_hash()
    {
        BookFolder book = TinyBook(BookKind.Store);
        WriteHighRes(book, "1.jpg", "2.jpg");
        File.WriteAllText(Path.Combine(HighResPack.FolderOf(book), "2.jpg"), "version https://git-lfs.github.com/spec/v1\nsize 12\n");

        Assert.Null(HighResPack.TryOid(Path.Combine(HighResPack.FolderOf(book), "2.jpg")));
        Assert.NotEmpty(BookHash.Of(book));
        Assert.Contains("tiny/high-res/2.jpg is a Git LFS pointer with no oid line", Assert.Single(HighResPack.GateProblems(book)));
    }

    private static async Task WriteTinyPackageAsync(string path, string text, int version)
    {
        await StarterBuilder.InScratchAsync(async library =>
        {
            Story story = StarterBuilder.Build("a02c0731b7ac475a896e9184f3575e7b", "Tiny", text, 0);
            await library.AddAsync(story);
            await using FileStream output = File.Create(path);
            await StoryPackageWriter.WriteAsync(story, library, output, includeRecordings: false, version: version);
        });
    }

    private static byte[] EntryBytes(string package, string name)
    {
        using ZipArchive zip = ZipFile.OpenRead(package);
        using Stream stream = zip.GetEntry(name)!.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    // A start-of-image marker and one baseline frame header of the given size: just enough for the size reader.
    private static byte[] JpegHead(int width, int height) =>
        [0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x11, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01];

    private static void WriteHighRes(BookFolder book, params string[] names)
    {
        Directory.CreateDirectory(HighResPack.FolderOf(book));
        foreach (string name in names)
        {
            File.WriteAllBytes(Path.Combine(HighResPack.FolderOf(book), name), PrintPicture);
        }
    }

    // Two pages ("One." / "Two.") with a picture each, in a store (or starter) book folder.
    private BookFolder TinyBook(BookKind kind)
    {
        string dir = Path.Combine(_root, "tiny");
        Directory.CreateDirectory(Path.Combine(dir, BookFolder.PicturesDirectory));
        File.WriteAllText(Path.Combine(dir, BookFolder.StoryFile), "One.\n---\nTwo.");
        File.WriteAllBytes(Path.Combine(dir, BookFolder.PicturesDirectory, "1.jpg"), StandardPicture);
        File.WriteAllBytes(Path.Combine(dir, BookFolder.PicturesDirectory, "2.jpg"), StandardPicture);
        var info = new BookInfo(kind, "a02c0731b7ac475a896e9184f3575e7b", "Tiny", 1, 3, 6, "Me", "Mine", 0, "2026-10-27", 0, null, null, null, null, null, null, null, "Me");
        return new BookFolder("tiny", dir, info);
    }
}
