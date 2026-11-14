using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 286: a parent's recording belongs to one story language; recordings made before that are filed under the app's main language once, saved, and restored the same way.</summary>
public sealed class RecordingLanguageTests : IDisposable
{
    private const string SchemaPattern = "\"RecordingSchema\":\\s*2,?";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-reclang-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static Story Make(string language = "sv") => new(
        StoryIds.New(), "Pigs", language, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
        new[] { "One. Two.", "Three." }.Select(t => new StoryPage(t)).ToList(), [], CoverStyle.Colour, 0, string.Empty, SourceId: StoryIds.New());

    private static Story WithTwoLanguages(Story story)
    {
        story = StoryRecordings.WithClip(story, new StoryClipKey(0, 0, 0, "en"), new PageRecording("en", 2, "rec-en-1-1-1-aaaaaaaa.m4a"));
        story = StoryRecordings.WithClip(story, new StoryClipKey(0, 0, 0, "fa"), new PageRecording("fa", 3, "rec-fa-1-1-1-bbbbbbbb.m4a"));
        return StoryRecordings.With(story, 1, new PageRecording("fa", 4));
    }

    // The file as an app before Slice 286 wrote it: no schema mark, every clip tagged with the book's own language.
    private static string AsOldFile(string json) => Regex.Replace(json, SchemaPattern, string.Empty);

    [Fact]
    public void A_key_that_never_ran_its_constructor_reads_as_no_language_and_equals_one_built_with_none()
    {
        StoryClipKey made = default;
        var empty = new StoryClipKey(0, 0, 0, StoryClipKey.NoLanguage);

        Assert.Equal(string.Empty, made.Language);
        Assert.Equal(empty, made);
        Assert.Equal(empty.GetHashCode(), made.GetHashCode());
        Assert.Equal("rec-1-1-1.m4a", StoryRecordings.FileNameOf(made));
        Assert.Equal("rec-fa-1-1-1.m4a", StoryRecordings.FileNameOf(made.In("fa")));
    }

    [Fact]
    public void A_backup_clip_never_lands_on_the_file_or_whole_page_slot_of_another_language()
    {
        // The phone's English clips were migrated and keep their pre-language names; a backup filed under Farsi names the same files.
        Story phone = StoryRecordings.WithClip(Make("en"), new StoryClipKey(0, 0, 0, "en"), new PageRecording("en", 2, "rec-1-1-1.m4a"));
        phone = StoryRecordings.With(phone, 1, new PageRecording("en", 3));
        Story backed = StoryRecordings.WithClip(Make("en"), new StoryClipKey(0, 1, 1, "fa"), new PageRecording("fa", 2));
        backed = StoryRecordings.WithClip(backed, new StoryClipKey(0, 0, 0, "fa"), new PageRecording("fa", 2, "rec-1-1-1.m4a")); // same file name as the phone's English take
        backed = StoryRecordings.With(backed, 1, new PageRecording("fa", 4)); // whole page 2: the phone's English take holds that slot

        IReadOnlyList<StoryClipKey> mergeable = StoryRecordings.MergeableClips(phone, backed, _ => true);

        Assert.Equal([new StoryClipKey(0, 1, 1, "fa")], mergeable);
    }

    [Fact]
    public void Two_languages_on_the_same_piece_survive_the_json_round_trip()
    {
        Story story = WithTwoLanguages(Make());

        Story back = StoryJson.Deserialize(StoryJson.Serialize(story), out bool migrated, mainLanguage: "de");

        Assert.False(migrated);
        Assert.Equal(1, StoryRecordings.Count(back, "en"));
        Assert.Equal(2, StoryRecordings.Count(back, "fa")); // a piece and a whole page
        Assert.Equal("rec-fa-1-1-1-bbbbbbbb.m4a", StoryRecordings.ClipOf(back, new StoryClipKey(0, 0, 0, "fa"))!.File);
        Assert.Equal(4, StoryRecordings.ClipOf(back, StoryClipKey.OfPage(1, "fa"))!.Seconds);
    }

    [Fact]
    public void Recordings_of_an_older_file_are_filed_under_the_main_language_and_keep_their_files()
    {
        Story old = StoryRecordings.WithClip(Make(), new StoryClipKey(0, 0, 1, "sv"), new PageRecording("sv", 5, "rec-1-1-2-0123abcd.m4a"));
        old = StoryRecordings.WithClip(old, new StoryClipKey(0, 0, 0, "sv"), new PageRecording("sv", 6)); // replaces the piece above: one clip per sentence per language
        old = StoryRecordings.With(old, 1, new PageRecording("sv", 7, "rec-2-0123abcd.m4a"));

        Story migrated = StoryJson.Deserialize(AsOldFile(StoryJson.Serialize(old)), out bool wasOld, mainLanguage: "fa");

        Assert.True(wasOld);
        Assert.Equal(2, StoryRecordings.Count(migrated, "fa"));
        Assert.Equal(0, StoryRecordings.Count(migrated, "sv"));
        Assert.Equal("rec-1-1-1.m4a", StoryRecordings.FileOf(migrated, new StoryClipKey(0, 0, 0, "fa"))); // the name the clip had before it had a language
        Assert.Equal("rec-2-0123abcd.m4a", StoryRecordings.FileOf(migrated, StoryClipKey.OfPage(1, "fa")));
        Assert.True(migrated.UseRecordings);
    }

    [Fact]
    public void A_migrated_file_is_saved_once_and_the_clips_stay_in_their_language_when_the_app_language_changes()
    {
        Story old = StoryRecordings.WithClip(Make(), new StoryClipKey(0, 0, 0, "sv"), new PageRecording("sv", 5));
        string oldFile = AsOldFile(StoryJson.Serialize(old));

        string saved = StoryJson.Serialize(StoryJson.Deserialize(oldFile, out bool migrated, mainLanguage: "fa"));
        Story later = StoryJson.Deserialize(saved, out bool again, mainLanguage: "de");

        Assert.True(migrated);
        Assert.False(again);
        Assert.Equal(1, StoryRecordings.Count(later, "fa"));
        Assert.Equal(0, StoryRecordings.Count(later, "de"));
    }

    [Fact]
    public async Task The_library_files_a_book_with_older_recordings_under_the_main_language_and_writes_it_back()
    {
        var library = new FileStoryLibrary(Path.Combine(_root, "lib"));
        Story story = Make();
        await library.AddAsync(story);
        Story old = StoryRecordings.WithClip(story, new StoryClipKey(0, 0, 0, "sv"), new PageRecording("sv", 5));
        await File.WriteAllTextAsync(Path.Combine(_root, "lib", story.Id, "story.json"), AsOldFile(StoryJson.Serialize(old)));

        Story read;
        using (MainLanguage.Is("fa"))
        {
            read = (await library.GetAsync(story.Id))!;
        }

        string onDisk = await File.ReadAllTextAsync(Path.Combine(_root, "lib", story.Id, "story.json"));

        Assert.Equal(1, StoryRecordings.Count(read, "fa"));
        Assert.Contains("RecordingSchema", onDisk);
        Assert.False(StoryJson.Deserialize(onDisk, out bool stillOld).Id.Length == 0);
        Assert.False(stillOld);
    }

    [Fact]
    public async Task A_backup_made_before_the_language_key_restores_into_the_main_language()
    {
        var source = new FileStoryLibrary(Path.Combine(_root, "source"));
        Story story = Make();
        await source.AddAsync(story);
        // the name an older app gave the clip: no language in it
        await source.StoreClipAsync(story.Id, new StoryClipKey(0, 0, 0, StoryClipKey.NoLanguage), path => File.WriteAllTextAsync(path, "audio"));
        Story recorded = StoryRecordings.WithClip(story, new StoryClipKey(0, 0, 0, "sv"), new PageRecording("sv", 3, "rec-1-1-1.m4a"));
        await source.UpdateAsync(recorded);
        using var package = new MemoryStream();
        await StoryPackageWriter.WriteAsync((await source.GetAsync(story.Id))!, source, package, includeRecordings: true);
        byte[] oldPackage = StripSchema(package.ToArray());

        var target = new FileStoryLibrary(Path.Combine(_root, "target"));
        Story restored;
        using (MainLanguage.Is("fa"))
        {
            restored = await StoryPackageReader.ImportAsync(new MemoryStream(oldPackage), target, keepId: true);
        }

        Assert.Equal(1, StoryRecordings.Count(restored, "fa"));
        Assert.Equal(0, StoryRecordings.Count(restored, "sv"));
    }

    [Fact]
    public void A_backup_clip_of_another_language_merges_next_to_the_phones_clip_of_the_same_sentences()
    {
        Story phone = StoryRecordings.WithClip(Make(), new StoryClipKey(0, 0, 0, "en"), new PageRecording("en", 2, "rec-en-1-1-1-aaaaaaaa.m4a"));
        Story backed = StoryRecordings.WithClip(Make() with { Id = phone.Id }, new StoryClipKey(0, 0, 0, "fa"), new PageRecording("fa", 3, "rec-fa-1-1-1-bbbbbbbb.m4a"));

        IReadOnlyList<StoryClipKey> mergeable = StoryRecordings.MergeableClips(phone, backed, _ => true);
        Story merged = StoryRecordings.WithMergedClips(phone, backed, mergeable);

        Assert.Equal([new StoryClipKey(0, 0, 0, "fa")], mergeable);
        Assert.Equal(1, StoryRecordings.Count(merged, "en"));
        Assert.Equal(1, StoryRecordings.Count(merged, "fa"));
    }

    // ---- review pass 2: no clip is ever filed under a guessed language, and no file is shared or lost ----

    [Fact]
    public async Task A_read_before_the_main_language_is_known_migrates_and_saves_nothing_and_a_later_read_files_the_clips()
    {
        var library = new FileStoryLibrary(Path.Combine(_root, "lib"));
        Story story = Make();
        await library.AddAsync(story);
        Story old = StoryRecordings.WithClip(story, new StoryClipKey(0, 0, 0, "sv"), new PageRecording("sv", 5));
        string path = Path.Combine(_root, "lib", story.Id, "story.json");
        string oldFile = AsOldFile(StoryJson.Serialize(old));
        await File.WriteAllTextAsync(path, oldFile);

        Story early;
        using (MainLanguage.Is(null))
        {
            early = (await library.GetAsync(story.Id))!;
            Assert.Equal(oldFile, await File.ReadAllTextAsync(path)); // the file is untouched
            await library.UpdateAsync(early with { Title = "Pigs again" }); // a save by a host that read early keeps the clips waiting
        }

        Assert.Equal(0, StoryRecordings.Count(early, "en"));
        Assert.Equal(0, StoryRecordings.Count(early, "sv"));
        Assert.Equal(1, StoryRecordings.Count(early)); // the clip is still there, filed under no language yet
        Assert.DoesNotContain("\"RecordingSchema\": 2", await File.ReadAllTextAsync(path));

        Story later;
        using (MainLanguage.Is("de"))
        {
            later = (await library.GetAsync(story.Id))!;
        }

        Assert.Equal(1, StoryRecordings.Count(later, "de"));
        Assert.Contains("\"RecordingSchema\": 2", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public void A_whole_page_clip_of_a_second_language_never_overwrites_the_first_and_each_keeps_its_own_file()
    {
        Story story = StoryRecordings.WithClip(Make(), StoryClipKey.OfPage(0, "en"), new PageRecording("en", 2, "rec-en-1-aaaaaaaa.m4a"));
        IReadOnlyList<string> replaced = StoryRecordings.ReplacedFiles(story, StoryClipKey.OfPage(0, "fa"));

        story = StoryRecordings.WithClip(story, StoryClipKey.OfPage(0, "fa"), new PageRecording("fa", 3, "rec-fa-1-bbbbbbbb.m4a"));

        Assert.Empty(replaced);
        Assert.Equal(2, StoryRecordings.Count(story));
        Assert.Equal(2, StoryRecordings.ClipOf(story, StoryClipKey.OfPage(0, "en"))!.Seconds);
        Assert.Equal(3, StoryRecordings.ClipOf(story, StoryClipKey.OfPage(0, "fa"))!.Seconds);

        Story back = StoryJson.Deserialize(StoryJson.Serialize(story), out _, mainLanguage: "de");
        Assert.Equal("rec-en-1-aaaaaaaa.m4a", StoryRecordings.FileOf(back, StoryClipKey.OfPage(0, "en")));
        Assert.Equal("rec-fa-1-bbbbbbbb.m4a", StoryRecordings.FileOf(back, StoryClipKey.OfPage(0, "fa")));

        Story withoutFarsi = StoryRecordings.WithoutClip(back, StoryClipKey.OfPage(0, "fa"));
        Assert.Equal([StoryClipKey.OfPage(0, "en")], StoryRecordings.Clips(withoutFarsi).Select(c => c.Key));
    }

    [Fact]
    public void Re_recording_one_languages_whole_page_replaces_only_its_own_file()
    {
        Story story = StoryRecordings.WithClip(Make(), StoryClipKey.OfPage(0, "en"), new PageRecording("en", 2, "rec-en-1-aaaaaaaa.m4a"));
        story = StoryRecordings.WithClip(story, StoryClipKey.OfPage(0, "fa"), new PageRecording("fa", 3, "rec-fa-1-bbbbbbbb.m4a"));

        Assert.Equal(["rec-fa-1-bbbbbbbb.m4a"], StoryRecordings.ReplacedFiles(story, StoryClipKey.OfPage(0, "fa")));
        Story again = StoryRecordings.WithClip(story, StoryClipKey.OfPage(0, "fa"), new PageRecording("fa", 4, "rec-fa-1-cccccccc.m4a"));

        Assert.Equal(2, StoryRecordings.Count(again));
        Assert.Equal(["rec-en-1-aaaaaaaa.m4a", "rec-fa-1-cccccccc.m4a"], StoryRecordings.Files(again).Order().ToList());
    }

    [Fact]
    public void A_file_another_clip_still_names_is_never_listed_as_replaced_or_deletable()
    {
        // Two clips of different languages that (wrongly) name one file, built in memory: replacing one must not delete the other's audio.
        Story story = StoryRecordings.WithClip(Make(), new StoryClipKey(0, 0, 0, "en"), new PageRecording("en", 2, "rec-1-1-1.m4a"));
        story = StoryRecordings.WithClip(story, new StoryClipKey(0, 0, 0, "fa"), new PageRecording("fa", 3, "rec-1-1-1.m4a"));

        Assert.Empty(StoryRecordings.ReplacedFiles(story, new StoryClipKey(0, 0, 0, "fa")));
        Assert.True(StoryRecordings.IsReferenced(story, "rec-1-1-1.m4a"));
        Assert.False(StoryRecordings.IsReferenced(StoryRecordings.WithoutClip(StoryRecordings.WithoutClip(story, new StoryClipKey(0, 0, 0, "fa")), new StoryClipKey(0, 0, 0, "en")), "rec-1-1-1.m4a"));
    }

    [Fact]
    public void A_stored_file_that_two_languages_name_is_kept_for_the_first_clip_only()
    {
        Story both = StoryRecordings.WithClip(Make(), new StoryClipKey(0, 0, 0, "en"), new PageRecording("en", 2, "rec-1-1-1.m4a"));
        both = StoryRecordings.WithClip(both, new StoryClipKey(0, 0, 0, "fa"), new PageRecording("fa", 3, "rec-1-1-1.m4a"));

        Story read = StoryJson.Deserialize(StoryJson.Serialize(both), out _, mainLanguage: "de");

        Assert.Equal(1, StoryRecordings.Count(read));
        Assert.Equal(1, StoryRecordings.Count(read, "en"));
        Assert.Equal(["rec-1-1-1.m4a"], StoryRecordings.Files(read).ToList());
    }

    [Fact]
    public void A_key_built_for_no_language_says_so_and_takes_a_language_only_through_In()
    {
        Assert.Equal(string.Empty, StoryClipKey.NoLanguage);
        Assert.Equal("fa", StoryClipKey.OfPage(0, StoryClipKey.NoLanguage).In("fa").Language);
    }

    // Sets the process-wide main language the library reads old recordings with (one test at a time), and puts the previous one back.
    private sealed class MainLanguage : IDisposable
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private readonly Func<string?> _previous;

        private MainLanguage(string? language)
        {
            Gate.Wait();
            _previous = StoryJson.MainLanguageOf;
            StoryJson.MainLanguageOf = () => language;
        }

        public static MainLanguage Is(string? language) => new(language);

        public void Dispose()
        {
            StoryJson.MainLanguageOf = _previous;
            Gate.Release();
        }
    }

    private static byte[] StripSchema(byte[] package)
    {
        using var stream = new MemoryStream();
        stream.Write(package);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            ZipArchiveEntry entry = zip.GetEntry("story.json")!;
            string text;
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
            {
                text = reader.ReadToEnd();
            }

            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("story.json").Open(), new UTF8Encoding(false));
            writer.Write(AsOldFile(text));
        }

        return stream.ToArray();
    }
}
