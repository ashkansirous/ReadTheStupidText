using DinoReader.BookBuilder;

namespace DinoReader.Tests;

/// <summary>Slice 409: the committed listening track. check-audio fails a missing, empty or stale one; build-listen refuses clips that no longer match the pack record and drops the older track.</summary>
public sealed class ListenTrackFilesTests : IDisposable
{
    private const string Language = "fa";
    private const string Manifest = """{"version":3,"book":"x","language":"fa","entries":[{"index":0,"sentence":0,"hash":"aa","clip":"a.m4a","text":"t"}]}""";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "dino-listen-files-" + Guid.NewGuid().ToString("N"));

    public ListenTrackFilesTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private AudioPackRecord PackedRecord(int version = 2, int? listenSeconds = 90)
    {
        File.WriteAllText(Path.Combine(_folder, "manifest.json"), Manifest);
        File.WriteAllBytes(Path.Combine(_folder, "a.m4a"), [1, 2, 3]);
        string hash = AudioPacks.ContentHash(_folder, AudioPacks.PackFiles(_folder, Language));
        return new AudioPackRecord(version, hash, 10, listenSeconds);
    }

    private void WriteTrack(int version, byte[]? bytes = null) =>
        File.WriteAllBytes(Path.Combine(_folder, ListenTrackPlan.FileName(Language, version)), bytes ?? [9, 9]);

    [Fact]
    public void ACurrentTrackWithALengthIsNoProblem()
    {
        AudioPackRecord record = PackedRecord();
        WriteTrack(2);

        Assert.Empty(ListenTrackFiles.Problems(record, Language, _folder));
    }

    [Fact]
    public void AMissingTrackIsNamedWithItsFile()
    {
        AudioPackRecord record = PackedRecord();

        string problem = Assert.Single(ListenTrackFiles.Problems(record, Language, _folder));

        Assert.Contains("listen-fa-v2.m4a", problem);
        Assert.Contains("missing", problem);
    }

    [Fact]
    public void ATrackOfAnOlderPackVersionDoesNotCount()
    {
        AudioPackRecord record = PackedRecord(version: 3);
        WriteTrack(2);

        Assert.Contains(ListenTrackFiles.Problems(record, Language, _folder), p => p.Contains("listen-fa-v3.m4a") && p.Contains("missing"));
    }

    [Fact]
    public void AnEmptyTrackAndARecordWithNoLengthAreProblems()
    {
        AudioPackRecord record = PackedRecord(listenSeconds: null);
        WriteTrack(2, bytes: []);

        List<string> problems = ListenTrackFiles.Problems(record, Language, _folder).ToList();

        Assert.Contains(problems, p => p.Contains("no listening track length"));
        Assert.Contains(problems, p => p.Contains("empty"));
    }

    [Fact]
    public void ATrackOlderThanItsClipsIsAProblem()
    {
        AudioPackRecord record = PackedRecord();
        WriteTrack(2);
        File.WriteAllBytes(Path.Combine(_folder, "a.m4a"), [1, 2, 3, 4]);

        Assert.Contains(ListenTrackFiles.Problems(record, Language, _folder), p => p.Contains("clips changed"));
    }

    [Fact]
    public void ClipsThatWereNotPulledSkipOnlyTheStaleCheck()
    {
        AudioPackRecord record = PackedRecord();
        WriteTrack(2);
        File.WriteAllText(Path.Combine(_folder, "a.m4a"), "version https://git-lfs.github.com/spec/v1\noid sha256:00\nsize 3\n");

        Assert.Empty(ListenTrackFiles.Problems(record, Language, _folder));
        File.Delete(Path.Combine(_folder, ListenTrackPlan.FileName(Language, 2)));
        Assert.Single(ListenTrackFiles.Problems(record, Language, _folder));
    }

    [Fact]
    public void WhenOnlySomeClipsArePointersTheStateIsUnknownNotChanged()
    {
        const string TwoClips = """{"version":3,"book":"x","language":"fa","entries":[{"index":0,"sentence":0,"hash":"aa","clip":"a.m4a","text":"t"},{"index":1,"sentence":1,"hash":"bb","clip":"b.m4a","text":"u"}]}""";
        File.WriteAllText(Path.Combine(_folder, "manifest.json"), TwoClips);
        File.WriteAllBytes(Path.Combine(_folder, "a.m4a"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(_folder, "b.m4a"), [4, 5, 6]);
        var record = new AudioPackRecord(2, AudioPacks.ContentHash(_folder, AudioPacks.PackFiles(_folder, Language)), 10, 90);
        WriteTrack(2);
        File.WriteAllText(Path.Combine(_folder, "b.m4a"), "version https://git-lfs.github.com/spec/v1\noid sha256:00\nsize 3\n");

        Assert.Empty(ListenTrackFiles.Problems(record, Language, _folder));
        Assert.False(ListenTrackFiles.IsUpToDate(record, _folder, Language));
    }

    [Fact]
    public void BuildingRefusesClipsThatNoLongerMatchTheRecord()
    {
        AudioPackRecord record = PackedRecord();
        ListenTrackFiles.RequireCurrentRecord(record, _folder, Language);
        File.WriteAllBytes(Path.Combine(_folder, "a.m4a"), [7]);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ListenTrackFiles.RequireCurrentRecord(record, _folder, Language));

        Assert.Contains("build-audio", error.Message);
    }

    [Fact]
    public void OnlyTheTracksOfOtherVersionsAreRemoved()
    {
        WriteTrack(1);
        WriteTrack(2);
        File.WriteAllBytes(Path.Combine(_folder, ListenTrackPlan.FileName("de", 1)), [1]);

        ListenTrackFiles.RemoveOlder(_folder, Language, 2);

        Assert.False(File.Exists(Path.Combine(_folder, ListenTrackPlan.FileName(Language, 1))));
        Assert.True(File.Exists(Path.Combine(_folder, ListenTrackPlan.FileName(Language, 2))));
        Assert.True(File.Exists(Path.Combine(_folder, ListenTrackPlan.FileName("de", 1))));
    }

    private string StoreFolderOf(string name) => Path.Combine(_folder, "store", name);

    [Fact]
    public void ACommittedTrackIsCopiedToTheStoreFolderUnderItsVersionedName()
    {
        AudioPackRecord record = PackedRecord();
        WriteTrack(2, bytes: [4, 5, 6]);

        ListenTrackFiles.StageTrack(record, Language, _folder, StoreFolderOf("id"));

        Assert.Equal([4, 5, 6], File.ReadAllBytes(Path.Combine(StoreFolderOf("id"), "listen-fa-v2.m4a")));
    }

    [Fact]
    public void AnLfsPointerIsNeverStagedAsTheTrack()
    {
        AudioPackRecord record = PackedRecord();
        File.WriteAllText(Path.Combine(_folder, ListenTrackPlan.FileName(Language, 2)), "version https://git-lfs.github.com/spec/v1\noid sha256:00\nsize 99\n");

        Assert.Throws<InvalidOperationException>(() => ListenTrackFiles.StageTrack(record, Language, _folder, StoreFolderOf("id")));

        Assert.False(Directory.Exists(StoreFolderOf("id")));
    }

    [Fact]
    public void AMissingTrackAndARecordWithNoLengthAreNotStaged()
    {
        AudioPackRecord record = PackedRecord();
        Assert.Throws<InvalidOperationException>(() => ListenTrackFiles.StageTrack(record, Language, _folder, StoreFolderOf("id")));

        WriteTrack(2);
        AudioPackRecord noLength = record with { ListenSeconds = null };
        Assert.Throws<InvalidOperationException>(() => ListenTrackFiles.StageTrack(noLength, Language, _folder, StoreFolderOf("id")));

        Assert.False(Directory.Exists(StoreFolderOf("id")));
    }

    [Fact]
    public void StageFailsTheLanguageThatHasNoPackRecordAndStagesNothing()
    {
        string stem = Books.All.First(b => File.Exists(Path.Combine(b.Dir, AudioPacks.CastFile))).Stem;

        int exit = ListenTrackFiles.Stage(stem, "xx", _folder, StoreFolderOf("id"));

        Assert.Equal(1, exit);
        Assert.False(Directory.Exists(StoreFolderOf("id")));
    }

    [Fact]
    public void ACurrentTrackIsUpToDateSoARerunSkipsIt()
    {
        AudioPackRecord record = PackedRecord();
        WriteTrack(2);

        Assert.True(ListenTrackFiles.IsUpToDate(record, _folder, Language));
    }

    [Fact]
    public void ATrackThatIsMissingOrStaleOrUnrecordedIsNotUpToDate()
    {
        AudioPackRecord record = PackedRecord();
        Assert.False(ListenTrackFiles.IsUpToDate(record, _folder, Language));

        WriteTrack(2);
        Assert.False(ListenTrackFiles.IsUpToDate(null, _folder, Language));
        Assert.False(ListenTrackFiles.IsUpToDate(record with { ListenSeconds = null }, _folder, Language));

        File.WriteAllBytes(Path.Combine(_folder, "a.m4a"), [1, 2, 3, 4]);
        Assert.False(ListenTrackFiles.IsUpToDate(record, _folder, Language));
    }

    [Fact]
    public void AFailedPackBuildIsReportedNotThrownSoTheOtherLanguagesGoOn()
    {
        bool built = ListenTrackFiles.TryBuildPack("no-such-book-409", Language, new Dictionary<string, AudioPackRecord>());

        Assert.False(built);
    }

    [Fact]
    public void ALanguageWithNoClipsFolderHasNoPulledManifest()
    {
        BookFolder book = Books.All.First(b => File.Exists(Path.Combine(b.Dir, AudioPacks.CastFile)));

        Assert.False(ListenTrackFiles.HasPulledManifest(book, "xx"));
    }
}
