using System.IO.Compression;
using System.Text;
using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 246 C2: the pack store's states, with a fake download. A folder with the final name exists only when whole.
public sealed class AudioPackStoreTests : IDisposable
{
    private const string Book = "0123456789abcdef0123456789abcdef";
    private const string Text = "یک متن";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dino-audio-store-test-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _log = [];

    private string Root => Path.Combine(_dir, "packs");

    private string Cache => Path.Combine(_dir, "cache");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private AudioPackStore Store(IAudioPackFetcher fetcher) => new(Root, Cache, fetcher, _log.Add);

    private static byte[] Zip(string language = "fa", IEnumerable<string>? clipsInManifest = null, IEnumerable<string>? clipsInZip = null, string entryPrefix = "")
    {
        string[] manifestClips = (clipsInManifest ?? ["a.m4a"]).ToArray();
        string[] zipClips = (clipsInZip ?? manifestClips).ToArray();
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            string entries = string.Join(',', manifestClips.Select((c, i) => $$"""{"index":{{i}},"hash":"{{PremadeAudio.HashOf(Text + c)}}","clip":"{{c}}"}"""));
            Write(zip, entryPrefix + "manifest.json", Encoding.UTF8.GetBytes($$"""{"version":3,"book":"b","language":"{{language}}","entries":[{{entries}}]}"""));
            foreach (string clip in zipClips)
            {
                Write(zip, entryPrefix + clip, [1, 2, 3]);
            }
        }

        return stream.ToArray();
    }

    private static void Write(ZipArchive zip, string name, byte[] content)
    {
        using Stream entry = zip.CreateEntry(name).Open();
        entry.Write(content);
    }

    private static AudioPackInfo Info(byte[] zip, int version = 1) => new(version, zip.Length, "u");

    [Fact]
    public async Task Install_PutsTheWholePackInItsFinalFolder_AndOpenFindsTheClip()
    {
        byte[] zip = Zip();
        AudioPackStore store = Store(new FakeFetcher(zip));

        PackInstall result = await store.InstallAsync(Book, "fa", Info(zip), null, CancellationToken.None);

        Assert.Equal(PackInstall.Installed, result);
        Assert.Equal(1, store.InstalledVersion(Book, "fa"));
        InstalledPack pack = store.Open(Book, "fa")!;
        Assert.Equal(Path.Combine(Root, Book, "fa-v1", "a.m4a"), pack.PathOf(0, Text + "a.m4a"));
        Assert.Empty(Directory.EnumerateFiles(Cache));
        Assert.DoesNotContain(Directory.EnumerateDirectories(Path.Combine(Root, Book)), d => d.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Install_TheSameVersionTwice_IsSkippedTheSecondTime_WithoutADownload()
    {
        byte[] zip = Zip();
        var fetcher = new FakeFetcher(zip);
        AudioPackStore store = Store(fetcher);

        await store.InstallAsync(Book, "fa", Info(zip), null, CancellationToken.None);
        PackInstall again = await store.InstallAsync(Book, "fa", Info(zip), null, CancellationToken.None);

        Assert.Equal(PackInstall.AlreadyInstalled, again);
        Assert.Equal(1, fetcher.Calls);
    }

    [Fact]
    public async Task Install_ANewVersion_ReplacesTheOldFolder()
    {
        byte[] first = Zip();
        byte[] second = Zip(clipsInManifest: ["b.m4a"]);
        AudioPackStore store = Store(new FakeFetcher(first));
        await store.InstallAsync(Book, "fa", Info(first), null, CancellationToken.None);

        PackInstall result = await Store(new FakeFetcher(second)).InstallAsync(Book, "fa", Info(second, 2), null, CancellationToken.None);

        Assert.Equal(PackInstall.Installed, result);
        Assert.Equal(2, store.InstalledVersion(Book, "fa"));
        Assert.False(Directory.Exists(Path.Combine(Root, Book, "fa-v1")));
        Assert.NotNull(Store(new FakeFetcher(second)).Open(Book, "fa")!.PathOf(0, Text + "b.m4a"));
    }

    [Fact]
    public async Task Install_AnotherLanguageIsKeptSeparate()
    {
        byte[] fa = Zip();
        byte[] nb = Zip("nb");
        await Store(new FakeFetcher(fa)).InstallAsync(Book, "fa", Info(fa), null, CancellationToken.None);

        await Store(new FakeFetcher(nb)).InstallAsync(Book, "nb", Info(nb, 4), null, CancellationToken.None);

        AudioPackStore store = Store(new FakeFetcher(fa));
        Assert.Equal(1, store.InstalledVersion(Book, "fa"));
        Assert.Equal(4, store.InstalledVersion(Book, "nb"));
    }

    [Fact]
    public async Task Install_ABytesMismatch_KeepsNothing()
    {
        byte[] zip = Zip();
        AudioPackStore store = Store(new FakeFetcher(zip));

        PackInstall result = await store.InstallAsync(Book, "fa", new AudioPackInfo(1, zip.Length + 5, "u"), null, CancellationToken.None);

        Assert.Equal(PackInstall.Failed, result);
        Assert.Null(store.InstalledVersion(Book, "fa"));
        Assert.Empty(Directory.EnumerateFiles(Cache));
        Assert.Contains(_log, l => l.Contains("listing says", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Install_UnknownBytes_SkipsTheSizeCheckButStillVerifiesTheClips()
    {
        byte[] zip = Zip();

        PackInstall result = await Store(new FakeFetcher(zip)).InstallAsync(Book, "fa", new AudioPackInfo(1, null, "u"), null, CancellationToken.None);

        Assert.Equal(PackInstall.Installed, result);
    }

    [Fact]
    public async Task Install_Offline_FailsButKeepsThePreviousPack()
    {
        byte[] first = Zip();
        AudioPackStore store = Store(new FakeFetcher(first));
        await store.InstallAsync(Book, "fa", Info(first), null, CancellationToken.None);

        PackInstall result = await Store(new FakeFetcher(null) { Throw = new HttpRequestException("offline") }).InstallAsync(Book, "fa", Info(first, 2), null, CancellationToken.None);

        Assert.Equal(PackInstall.Failed, result);
        Assert.Equal(1, store.InstalledVersion(Book, "fa"));
        Assert.Empty(Directory.EnumerateFiles(Cache));
    }

    [Fact]
    public async Task Install_NotInTheStore_IsNotFound_AndNotRetriedAsAFailure()
    {
        PackInstall result = await Store(new FakeFetcher(null) { Throw = new AudioPackNotFoundException("u") }).InstallAsync(Book, "fa", new AudioPackInfo(1, 10, "u"), null, CancellationToken.None);

        Assert.Equal(PackInstall.NotFound, result);
    }

    [Fact]
    public async Task Install_Cancelled_LeavesNothingBehind()
    {
        using var cancel = new CancellationTokenSource();
        byte[] zip = Zip();
        var fetcher = new FakeFetcher(zip) { OnFetch = cancel.Cancel };
        AudioPackStore store = Store(fetcher);

        PackInstall result = await store.InstallAsync(Book, "fa", Info(zip), null, cancel.Token);

        Assert.Equal(PackInstall.Cancelled, result);
        Assert.Null(store.InstalledVersion(Book, "fa"));
        Assert.Empty(Directory.EnumerateFiles(Cache));
    }

    [Fact]
    public async Task Install_AZipMissingAClipTheManifestNames_IsNotKept()
    {
        byte[] zip = Zip(clipsInManifest: ["a.m4a", "b.m4a"], clipsInZip: ["a.m4a"]);
        AudioPackStore store = Store(new FakeFetcher(zip));

        PackInstall result = await store.InstallAsync(Book, "fa", Info(zip), null, CancellationToken.None);

        Assert.Equal(PackInstall.Failed, result);
        Assert.Null(store.InstalledVersion(Book, "fa"));
        Assert.False(Directory.Exists(Path.Combine(Root, Book, "fa-v1")));
        Assert.False(Directory.Exists(Path.Combine(Root, Book, "fa-v1.tmp")));
    }

    [Fact]
    public async Task Install_AManifestOfAnotherLanguage_IsNotKept()
    {
        byte[] zip = Zip("nb");

        PackInstall result = await Store(new FakeFetcher(zip)).InstallAsync(Book, "fa", Info(zip), null, CancellationToken.None);

        Assert.Equal(PackInstall.Failed, result);
    }

    [Fact]
    public async Task Install_AFileInAFolderOfTheZip_IsRefused()
    {
        byte[] zip = Zip(entryPrefix: "sub/");

        PackInstall result = await Store(new FakeFetcher(zip)).InstallAsync(Book, "fa", Info(zip), null, CancellationToken.None);

        Assert.Equal(PackInstall.Failed, result);
    }

    [Fact]
    public async Task Install_ACorruptZip_Fails()
    {
        byte[] junk = [1, 2, 3, 4, 5, 6, 7, 8];

        PackInstall result = await Store(new FakeFetcher(junk)).InstallAsync(Book, "fa", Info(junk), null, CancellationToken.None);

        Assert.Equal(PackInstall.Failed, result);
    }

    [Fact]
    public async Task Install_AnUnsafeBookOrLanguageName_IsNeverTouched()
    {
        byte[] zip = Zip();
        var fetcher = new FakeFetcher(zip);
        AudioPackStore store = Store(fetcher);

        Assert.Equal(PackInstall.Failed, await store.InstallAsync("../x", "fa", Info(zip), null, CancellationToken.None));
        Assert.Equal(PackInstall.Failed, await store.InstallAsync(Book, "fa/../..", Info(zip), null, CancellationToken.None));
        Assert.Equal(0, fetcher.Calls);
    }

    [Fact]
    public void Cleanup_DeletesInterruptedPartFilesAndTmpFolders_ButNotAFinishedPack()
    {
        Directory.CreateDirectory(Cache);
        Directory.CreateDirectory(Path.Combine(Root, Book, "fa-v2.tmp"));
        Directory.CreateDirectory(Path.Combine(Root, Book, "fa-v1"));
        File.WriteAllText(Path.Combine(Root, Book, "fa-v1", "manifest.json"), "{}");
        File.WriteAllText(Path.Combine(Cache, "audio-packs-leftover.part"), "x");
        string partFolder = Path.Combine(Cache, "audio-packs");
        Directory.CreateDirectory(partFolder);
        File.WriteAllText(Path.Combine(partFolder, Book + "-fa-v2-abc.part"), "half");
        File.WriteAllText(Path.Combine(partFolder, "other.txt"), "not ours");

        Store(new FakeFetcher(null)).CleanupInterrupted();

        Assert.Empty(Directory.EnumerateFiles(partFolder, "*.part"));
        Assert.True(File.Exists(Path.Combine(partFolder, "other.txt")));
        Assert.False(Directory.Exists(Path.Combine(Root, Book, "fa-v2.tmp")));
        Assert.True(Directory.Exists(Path.Combine(Root, Book, "fa-v1")));
    }

    [Fact]
    public async Task Install_StartsByCleaningWhatAKillLeft_ThenInstallsNormally()
    {
        string partFolder = Path.Combine(Cache, "audio-packs");
        Directory.CreateDirectory(partFolder);
        File.WriteAllText(Path.Combine(partFolder, Book + "-fa-v1-dead.part"), "half");
        Directory.CreateDirectory(Path.Combine(Root, Book, "fa-v1.tmp"));
        File.WriteAllText(Path.Combine(Root, Book, "fa-v1.tmp", "a.m4a"), "half");
        byte[] zip = Zip();

        PackInstall result = await Store(new FakeFetcher(zip)).InstallAsync(Book, "fa", Info(zip), null, CancellationToken.None);

        Assert.Equal(PackInstall.Installed, result);
        Assert.Empty(Directory.EnumerateFiles(partFolder));
        Assert.False(Directory.Exists(Path.Combine(Root, Book, "fa-v1.tmp")));
    }

    [Fact]
    public void Open_AFolderWithoutAManifest_IsNotAnInstalledPack()
    {
        Directory.CreateDirectory(Path.Combine(Root, Book, "fa-v1"));

        Assert.Null(Store(new FakeFetcher(null)).InstalledVersion(Book, "fa"));
        Assert.Null(Store(new FakeFetcher(null)).Open(Book, "fa"));
    }

    [Fact]
    public async Task PathOf_NoClipForTheText_OrTheFileGone_FallsBackToTheDeviceVoice()
    {
        byte[] zip = Zip();
        AudioPackStore store = Store(new FakeFetcher(zip));
        await store.InstallAsync(Book, "fa", Info(zip), null, CancellationToken.None);
        InstalledPack pack = store.Open(Book, "fa")!;

        Assert.Null(pack.PathOf(0, "an edited sentence"));
        Assert.Null(pack.PathOf(1, Text + "a.m4a"));
        File.Delete(Path.Combine(pack.Folder, "a.m4a"));
        Assert.Null(pack.PathOf(0, Text + "a.m4a"));
    }

    [Fact]
    public async Task Sweep_DeletesThePacksOfBooksNotOnTheShelf_AndKeepsTheOthers()
    {
        byte[] zip = Zip();
        const string other = "ffffffffffffffffffffffffffffffff";
        AudioPackStore store = Store(new FakeFetcher(zip));
        await store.InstallAsync(Book, "fa", Info(zip), null, CancellationToken.None);
        await store.InstallAsync(other, "fa", Info(zip), null, CancellationToken.None);

        await store.SweepAsync(() => Task.FromResult<ISet<string>?>(new HashSet<string> { Book.ToUpperInvariant() }));

        Assert.Equal(1, store.InstalledVersion(Book, "fa"));
        Assert.Null(store.InstalledVersion(other, "fa"));
    }

    [Fact]
    public async Task Install_AnyUnexpectedFailure_IsAFailedInstall_NeverAnException()
    {
        byte[] zip = Zip();
        AudioPackStore store = Store(new FakeFetcher(zip) { Throw = new UriFormatException("bad address") });

        PackInstall result = await store.InstallAsync(Book, "fa", Info(zip), null, CancellationToken.None);

        Assert.Equal(PackInstall.Failed, result);
        Assert.Null(store.InstalledVersion(Book, "fa"));
    }

    [Fact]
    public async Task Sweep_AnEmptyShelfList_DeletesNothing()
    {
        byte[] zip = Zip();
        AudioPackStore store = Store(new FakeFetcher(zip));
        await store.InstallAsync(Book, "fa", Info(zip), null, CancellationToken.None);

        await store.SweepAsync(() => Task.FromResult<ISet<string>?>(new HashSet<string>()));

        Assert.Equal(1, store.InstalledVersion(Book, "fa"));
    }

    [Fact]
    public async Task Sweep_AnIncompleteReadOfTheLibrary_DeletesNothing()
    {
        byte[] zip = Zip();
        AudioPackStore store = Store(new FakeFetcher(zip));
        await store.InstallAsync(Book, "fa", Info(zip), null, CancellationToken.None);

        await store.SweepAsync(() => Task.FromResult<ISet<string>?>(null));

        Assert.Equal(1, store.InstalledVersion(Book, "fa"));
    }

    [Fact]
    public async Task LibraryFolders_AnUnreadableStoryFolder_IsNotAnswered_SoItsPacksAreKept()
    {
        string libraryRoot = Path.Combine(_dir, "stories");
        const string brokenId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        Directory.CreateDirectory(Path.Combine(libraryRoot, brokenId));
        File.WriteAllText(Path.Combine(libraryRoot, brokenId, "story.json"), "{ not json");
        var library = new FileStoryLibrary(libraryRoot);
        byte[] zip = Zip();
        AudioPackStore store = Store(new FakeFetcher(zip));
        await store.InstallAsync(Book, "fa", Info(zip), null, CancellationToken.None);

        Assert.Null(await library.TryListSourceIdsAsync());
        await store.SweepAsync(library.TryListSourceIdsAsync);

        Assert.Equal(1, store.InstalledVersion(Book, "fa"));
    }

    [Fact]
    public async Task LibraryFolders_AMissingRoot_IsNotAnswered()
    {
        Assert.Null(await new FileStoryLibrary(Path.Combine(_dir, "nowhere")).TryListSourceIdsAsync());
    }

    [Fact]
    public async Task DeleteBook_RemovesEveryLanguageOfThatBook()
    {
        byte[] fa = Zip();
        byte[] nb = Zip("nb");
        AudioPackStore store = Store(new FakeFetcher(fa));
        await store.InstallAsync(Book, "fa", Info(fa), null, CancellationToken.None);
        await Store(new FakeFetcher(nb)).InstallAsync(Book, "nb", Info(nb), null, CancellationToken.None);

        await store.DeleteBookAsync(Book);

        Assert.Null(store.InstalledVersion(Book, "fa"));
        Assert.Null(store.InstalledVersion(Book, "nb"));
    }

    [Fact]
    public async Task Install_ReportsTheBytesAsTheyArrive()
    {
        byte[] zip = Zip();
        var seen = new List<long>();
        var progress = new SyncProgress<long>(seen.Add);

        await Store(new FakeFetcher(zip)).InstallAsync(Book, "fa", Info(zip), progress, CancellationToken.None);

        Assert.Equal(zip.Length, seen.Last());
    }

    [Fact]
    public void SummedProgress_CountsTheBookAndTheListedPackBytesTogether_AndNeverGoesBackOrPastOne()
    {
        var seen = new List<double>();
        var summed = new SummedProgress(new SyncProgress<double>(seen.Add), 100);

        summed.Book(50, 100);
        summed.Book(100, 100);
        summed.Pack(40);
        summed.PackEnded();
        summed.Pack(60);
        summed.PackEnded();

        Assert.Equal(0.25, seen[0], 3);
        Assert.Equal(0.5, seen[1], 3);
        Assert.Equal(seen.Order().ToList(), seen);
        Assert.Equal(1.0, seen.Last(), 3);
        summed.Pack(500);
        Assert.Equal(1.0, seen.Last(), 3);
    }

    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class FakeFetcher(byte[]? content) : IAudioPackFetcher
    {
        public int Calls { get; private set; }

        public Exception? Throw { get; init; }

        public Action? OnFetch { get; init; }

        public Task FetchAsync(string url, string toPath, IProgress<long> bytesSoFar, CancellationToken cancel)
        {
            Calls++;
            OnFetch?.Invoke();
            cancel.ThrowIfCancellationRequested();
            if (Throw is not null)
            {
                throw Throw;
            }

            File.WriteAllBytes(toPath, content!);
            bytesSoFar.Report(content!.Length);
            return Task.CompletedTask;
        }
    }
}
