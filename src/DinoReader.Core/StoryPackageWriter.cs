using System.IO.Compression;
using System.Text;

namespace DinoReader.Core;

/// <summary>Writes a story as a <c>.dino</c> package (Slice 91): the manifest, <c>story.json</c> and the pictures the story names.</summary>
public static class StoryPackageWriter
{
    /// <summary>Writes <paramref name="story"/> and its page and cover pictures from <paramref name="library"/> to <paramref name="output"/>.
    /// A picture the story names but the folder lacks is left out. The package id is the story's <see cref="Story.SourceId"/> when it
    /// came from a package, so a re-exported story keeps its identity, else its own id. The parent's page recordings (Slice 111) go in only when
    /// <paramref name="includeRecordings"/> is true (a backup); a shared or exported package never carries a parent's voice.
    /// <paramref name="version"/> is the content version a bundled starter records in its manifest (omitted when not set).</summary>
    public static async Task WriteAsync(Story story, IStoryLibrary library, Stream output, bool includeRecordings = false, int? version = null)
    {
        if (!includeRecordings)
        {
            story = story with { Recordings = null, UseRecordings = false };
        }

        string packageId = story.SourceId ?? story.Id;
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var manifest = new StoryPackageManifest(StoryPackageManifest.CurrentSchemaVersion, packageId, story.Title, story.Language, version);
        await AddTextAsync(zip, StoryPackageManifest.ManifestFileName, manifest.ToJson());
        await AddTextAsync(zip, StoryPackageManifest.StoryFileName, StoryJson.Serialize(story with { SourceId = null, ShowOriginalText = false }));
        foreach (string name in story.PictureFiles().Concat(StoryRecordings.Files(story)))
        {
            await AddPictureAsync(zip, library.TryResolveFile(story.Id, name), name);
        }
    }

    private static async Task AddTextAsync(ZipArchive zip, string name, string text)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using Stream stream = entry.Open();
        await stream.WriteAsync(new UTF8Encoding(false).GetBytes(WithPackageLineEnding(text)));
    }

    /// <summary>The indented JSON uses the OS line ending, so one build gave different bytes on Windows and Linux; every package text is written with CRLF (what the shipped starters and store books already hold), whichever OS builds it.</summary>
    private static string WithPackageLineEnding(string text) => text.Replace("\r\n", "\n").Replace("\n", "\r\n");

    private static async Task AddPictureAsync(ZipArchive zip, string? path, string name)
    {
        if (path is null || !File.Exists(path))
        {
            return;
        }

        // Pictures and recordings are already compressed, so storing them saves time for nothing lost.
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
        await using Stream target = entry.Open();
        await using FileStream source = File.OpenRead(path);
        await source.CopyToAsync(target);
    }
}
