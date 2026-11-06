using DinoReader.Core;
using ReadTheStupidText.Application.Usage;
using Xunit;

namespace DinoReader.Tests;

/// <summary>Slice 278 part B review: the old voice and storyteller packs are deleted by known id only; audio packs, books and anything unknown survive.</summary>
public class OldVoicePacksTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-oldvoicepacks-" + Guid.NewGuid().ToString("N"));

    public OldVoicePacksTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Folder(params string[] parts)
    {
        string path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "file.bin"), "x");
        return path;
    }

    private string File1(string name)
    {
        string path = Path.Combine(_root, name);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void KnownVoicePackFoldersZipsPartialsAndScratch_AreDeleted()
    {
        string supertonic = Folder("supertonic-3");
        string kokoro = Folder("kokoro-en");
        string scratch = Folder("piper-fa.extracting");
        string removing = Folder("melo-zh-en.removing");
        string zip = File1("supertonic-3-1.zip");
        string partial = File1("kokoro-en-2.zip.partial");

        OldVoicePacksResult result = OldVoicePacks.Delete(_root, new RecordingSink());

        Assert.Equal(6, result.Deleted.Count);
        Assert.Equal(0, result.Failed);
        Assert.All(new[] { supertonic, kokoro, scratch, removing }, path => Assert.False(Directory.Exists(path)));
        Assert.All(new[] { zip, partial }, path => Assert.False(File.Exists(path)));
    }

    [Fact]
    public void AudioPackAndBookFolders_AndUnknownEntries_Survive()
    {
        string audioPacks = Folder("audio-packs", "book-1", "en");
        string book = Folder("books", "story-1");
        string unknownPack = Folder("some-other-pack");
        string unknownZip = File1("some-other-pack-1.zip");
        string longerId = File1("supertonic-3-xx-1.zip"); // a longer id sharing the prefix is not supertonic-3's
        Folder("kokoro-en");

        OldVoicePacks.Delete(_root, new RecordingSink());

        Assert.True(Directory.Exists(audioPacks));
        Assert.True(Directory.Exists(book));
        Assert.True(Directory.Exists(unknownPack));
        Assert.True(File.Exists(unknownZip));
        Assert.True(File.Exists(longerId));
        Assert.False(Directory.Exists(Path.Combine(_root, "kokoro-en")));
    }

    [Fact]
    public void AMissingPacksFolder_IsNothingToDo()
    {
        OldVoicePacksResult result = OldVoicePacks.Delete(Path.Combine(_root, "nope"), new RecordingSink());

        Assert.Empty(result.Deleted);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public void ADeleteThatFails_IsReportedCountedAndTheOthersStillGo()
    {
        File1("supertonic-3-1.zip");
        File1("kokoro-en-1.zip");
        string locked = Path.Combine(_root, "kokoro-en-1.zip");
        using var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None); // a held file cannot be deleted on Windows
        var sink = new RecordingSink();

        OldVoicePacksResult result = OldVoicePacks.Delete(_root, sink);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(1, result.Failed);
            Assert.Single(sink.Reports);
            Assert.Equal((FallbackArea.Pack, FallbackReason.PackSyncFailed), sink.Reports[0]);
            Assert.False(File.Exists(Path.Combine(_root, "supertonic-3-1.zip")));
        }
    }

    private sealed class RecordingSink : IFallbackSink
    {
        public List<(string Area, string Reason)> Reports { get; } = [];

        public void Report(string area, string reason, string? language = null) => Reports.Add((area, reason));
    }
}
