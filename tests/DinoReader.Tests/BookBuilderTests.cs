using System.Text.Json;
using DinoReader.BookBuilder;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>The book builder and the content folders it reads (Slice 157, Decision 162).</summary>
public sealed class BookBuilderTests : IDisposable
{
    private const string TreasureStem = "x-marks-the-treasure";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-book-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void EveryContentFolder_IsABookWithAnIdAndTheFilesItNeeds()
    {
        Assert.Equal(16, Books.All.Count);
        Assert.Equal(4, Books.Starters.Count);
        Assert.Equal(12, Books.StoreOnly.Count);
        Assert.All(Books.All, b => Assert.True(StoryIds.IsValid(b.Info.Id), b.Stem));
        Assert.All(Books.All, b => Assert.True(File.Exists(b.CoverPath) && File.Exists(b.CoverThumbPath), b.Stem));
        Assert.All(Books.All, b => Assert.True(b.Text is not null || File.Exists(b.LinesPath), b.Stem));
    }

    [Fact]
    public async Task Check_PassesOnTheCommittedBooks_SoNoContentChangedWithoutAVersionBump()
    {
        Assert.Equal(0, await BookCommands.CheckAsync());
    }

    [Fact]
    public void TheHash_ChangesWithAWord_AndNotWithLineEndingsOrTheVersion()
    {
        BookFolder book = TinyBook("One.\n---\nTwo.");
        string hash = BookHash.Of(book);

        File.WriteAllText(Path.Combine(book.Dir, BookFolder.StoryFile), "One.\r\n---\r\nTwo.");
        Assert.Equal(hash, BookHash.Of(book));

        Assert.Equal(hash, BookHash.Of(new BookFolder(book.Stem, book.Dir, book.Info with { Version = 99, Order = 5 })));

        File.WriteAllText(Path.Combine(book.Dir, BookFolder.StoryFile), "One.\n---\nTwo!");
        Assert.NotEqual(hash, BookHash.Of(book));
    }

    [Fact]
    public void TheHash_ChangesWithAPictureAndATranslation()
    {
        BookFolder book = TinyBook("One.");
        string hash = BookHash.Of(book);

        Directory.CreateDirectory(book.PicturesPath);
        File.WriteAllBytes(Path.Combine(book.PicturesPath, "1.jpg"), [1, 2, 3]);
        string withPicture = BookHash.Of(book);
        Assert.NotEqual(hash, withPicture);

        Directory.CreateDirectory(book.TranslationsPath);
        File.WriteAllText(Path.Combine(book.TranslationsPath, "fr.json"), "{}");
        Assert.NotEqual(withPicture, BookHash.Of(book));
    }

    [Fact]
    public void TheHash_IgnoresFilesTheBuildDoesNotRead()
    {
        BookFolder book = TinyBook("One.");
        Directory.CreateDirectory(book.PicturesPath);
        Directory.CreateDirectory(book.TranslationsPath);
        string hash = BookHash.Of(book);

        File.WriteAllBytes(Path.Combine(book.PicturesPath, "Thumbs.db"), [1]);
        File.WriteAllText(Path.Combine(book.TranslationsPath, BookFolder.PiecesFile), "[]");
        File.WriteAllText(Path.Combine(book.Dir, "art.md"), "notes");

        Assert.Equal(hash, BookHash.Of(book));
    }

    [Fact]
    public void ChangedContent_WithTheSameVersionAsMain_FailsTheVersionCheck()
    {
        BookInfo onMain = TinyBook("One.").Info with { Version = 3, ContentHash = "old" };

        Assert.NotNull(GitBaseline.VersionProblem("tiny", onMain with { ContentHash = "new" }, onMain));
        Assert.Null(GitBaseline.VersionProblem("tiny", onMain with { ContentHash = "new", Version = 4 }, onMain));
        Assert.Null(GitBaseline.VersionProblem("tiny", onMain, onMain));
        Assert.Null(GitBaseline.VersionProblem("tiny", onMain with { ContentHash = "new" }, null));
    }

    [Fact]
    public void ABookJson_WithATypoedKeyOrAMissingOne_FailsWithTheFileNamed()
    {
        BookFolder book = TinyBook("One.");
        Books.WriteInfo(book.Dir, book.Info);
        string path = Path.Combine(book.Dir, BookFolder.BookFile);
        Assert.Equal("Tiny", Books.ReadInfo(book.Dir).Title);

        File.WriteAllText(path, File.ReadAllText(path).Replace("\"ageMin\"", "\"ageMn\""));
        Assert.Contains(path, Assert.Throws<InvalidOperationException>(() => Books.ReadInfo(book.Dir)).Message);

        Books.WriteInfo(book.Dir, book.Info);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"title\"", "\"name\""));
        Assert.Contains(path, Assert.Throws<InvalidOperationException>(() => Books.ReadInfo(book.Dir)).Message);
    }

    [Fact]
    public void BookJson_RoundTripsThroughTheWriter_SoStampChangesOnlyTheVersionAndHash()
    {
        BookFolder book = Books.Folder(TreasureStem);
        string before = File.ReadAllText(Path.Combine(book.Dir, BookFolder.BookFile)).ReplaceLineEndings("\n");

        Directory.CreateDirectory(_root);
        Books.WriteInfo(_root, book.Info);

        Assert.Equal(before, File.ReadAllText(Path.Combine(_root, BookFolder.BookFile)));
    }

    // X Marks the Treasure! builds from its folder the way the old test-project builder did: one picture per page, a picture cover, the folder's version in meta.json.
    [Fact]
    public void XMarksTheTreasure_BuildsFromItsFolder_WithOnePicturePerPageAndItsVersion()
    {
        BookFolder book = Books.Folder(TreasureStem);
        StoreStorySpec spec = book.ToSpec();

        Story story = StoreBuilder.Build(spec);

        Assert.Equal(StarterBuilder.PictureFilesOf(TreasureStem).Count, story.Pages.Count);
        Assert.All(story.Pages, p => Assert.NotNull(p.ImageFile));
        using JsonDocument meta = JsonDocument.Parse(StoreBuilder.MetaJson(spec, story));
        Assert.Equal(book.Info.Version, meta.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(book.Info.Id, story.Id);
    }

    [Fact]
    public async Task APackageRebuiltFromUnchangedContent_CountsAsTheSame_WhateverItsTimestamps()
    {
        string first = Path.Combine(_root, "a.dino");
        string second = Path.Combine(_root, "b.dino");
        Directory.CreateDirectory(_root);

        await StarterBuilder.InScratchAsync(async library =>
        {
            Story story = StarterBuilder.WithPictures(StarterBuilder.Build("a02c0731b7ac475a896e9184f3575e7b", "T", "One.\n---\nTwo.", 0), []);
            await library.AddAsync(story);
            await StarterBuilder.WriteOneAsync(story, first, library, 1);
            await Task.Delay(2100, TestContext.Current.CancellationToken);
            await StarterBuilder.WriteOneAsync(story, second, library, 1);
            Assert.True(PackageContent.Same(first, second));
            await StarterBuilder.WriteOneAsync(story with { Title = "Other" }, second, library, 1);
            Assert.False(PackageContent.Same(first, second));
        });
    }

    private static Dictionary<string, string> AllCredits() =>
        UiLanguage.Supported.Where(l => l != UiLanguage.English).ToDictionary(l => l, l => "Mine " + l);

    [Fact]
    public void ABookJson_WithoutAllFourteenCreditLanguages_FailsWithTheFileNamed()
    {
        BookFolder book = TinyBook("One.");
        string path = Path.Combine(book.Dir, BookFolder.BookFile);
        var partial = AllCredits();
        partial.Remove("fa");
        partial["xx"] = "Mine";
        Books.WriteInfo(book.Dir, book.Info with { CreditsByLanguage = partial });

        string message = Assert.Throws<InvalidOperationException>(() => Books.ReadInfo(book.Dir)).Message;
        Assert.Contains(path, message);
        Assert.Contains("missing: fa", message);
        Assert.Contains("unknown: xx", message);
    }

    private BookFolder TinyBook(string text)
    {
        string dir = Path.Combine(_root, "tiny");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, BookFolder.StoryFile), text);
        var info = new BookInfo(BookKind.Store, "a02c0731b7ac475a896e9184f3575e7b", "Tiny", 1, 3, 6, "Me", "Mine", 0, "2026-10-27", 0, null, null, null, null, string.Empty, null, AllCredits());
        return new BookFolder("tiny", dir, info);
    }
}
