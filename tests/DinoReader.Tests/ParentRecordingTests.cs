using System.IO.Compression;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 111, Decision 115: the parent's per-page recordings, the files they live in and how they travel in packages, backups and edits.</summary>
public sealed class ParentRecordingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-rec-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private FileStoryLibrary Library(string name) => new(Path.Combine(_root, name));

    private static Story Make(params string[] pageTexts) =>
        new(StoryIds.New(), "Pigs", "en", DateTimeOffset.UnixEpoch, pageTexts.Select(t => new StoryPage(t)).ToList(), [], CoverStyle.Colour, 0, string.Empty);

    private static Story Recorded(params int[] pages) =>
        pages.Aggregate(Make("One.", "Two.", "Three.", "Four."), (s, p) => StoryRecordings.With(s, p, new PageRecording("en", 10 + p)));

    [Fact]
    public void File_names_are_one_based_and_only_those_names_are_recording_files()
    {
        Assert.Equal("rec-1.m4a", StoryRecordings.FileNameOf(0));
        Assert.Equal("rec-12.m4a", StoryRecordings.FileNameOf(11));
        Assert.True(StoryRecordings.IsRecordingFileName("rec-3.m4a"));
        Assert.False(StoryRecordings.IsRecordingFileName("rec-0.m4a"));
        Assert.False(StoryRecordings.IsRecordingFileName("rec-3.mp3"));
        Assert.False(StoryRecordings.IsRecordingFileName("story.json"));
        Assert.False(StoryRecordings.IsRecordingFileName("../rec-3.m4a"));
        Assert.False(StoryRecordings.IsRecordingFileName(null));
    }

    [Fact]
    public void The_first_recording_turns_the_switch_on_and_the_last_removed_turns_it_off()
    {
        Story one = Recorded(1);
        Assert.True(one.UseRecordings);
        Assert.True(StoryRecordings.Plays(one, 1));
        Assert.False(StoryRecordings.Plays(one, 0));

        Story twoThenSwitchedOff = StoryRecordings.With(one with { UseRecordings = false }, 2, new PageRecording("en", 5));
        Assert.False(twoThenSwitchedOff.UseRecordings);
        Assert.False(StoryRecordings.Plays(twoThenSwitchedOff, 1));

        Story none = StoryRecordings.Without(StoryRecordings.Without(twoThenSwitchedOff, 1), 2);
        Assert.Null(none.Recordings);
        Assert.False(none.UseRecordings);
        Assert.Equal(0, StoryRecordings.Count(none));
    }

    [Fact]
    public void A_recording_survives_the_json_round_trip_and_a_damaged_one_is_dropped()
    {
        Story story = Recorded(0, 2);
        Story back = StoryJson.Deserialize(StoryJson.Serialize(story));
        Assert.Equal(new PageRecording("en", 10), back.Recordings![0]);
        Assert.Equal(new PageRecording("en", 12), back.Recordings![2]);
        Assert.True(back.UseRecordings);

        string json = StoryJson.Serialize(story).Replace("\"Page\": 2", "\"Page\": 9");
        Story damaged = StoryJson.Deserialize(json);
        Assert.Equal([0], damaged.Recordings!.Keys);
    }

    [Fact]
    public void A_story_without_recordings_writes_both_fields_as_null()
    {
        string json = StoryJson.Serialize(Make("One."));
        Assert.Contains("\"Recordings\": null", json);
        Assert.Contains("\"UseRecordings\": null", json);
    }

    [Fact]
    public void An_edit_keeps_a_recording_only_for_a_page_that_is_still_there_with_the_same_text()
    {
        Story story = Recorded(0, 1, 3);
        IReadOnlyList<StoryPage> edited = [new StoryPage("One."), new StoryPage("Two changed."), new StoryPage("Three.")];

        IReadOnlyDictionary<int, PageRecording>? kept = StoryRecordings.Kept(story, edited);

        Assert.Equal([0], kept!.Keys);
        Assert.Equal(["rec-2.m4a", "rec-4.m4a"], StoryRecordings.Dropped(story, kept));
        Assert.Null(StoryRecordings.Kept(story, [new StoryPage("Other.")]));
    }

    [Fact]
    public void The_clock_shows_minutes_and_two_digit_seconds_and_a_length_is_at_least_a_second()
    {
        Assert.Equal("0:14", StoryRecordings.Clock(TimeSpan.FromSeconds(14.4)));
        Assert.Equal("1:05", StoryRecordings.Clock(TimeSpan.FromSeconds(65)));
        Assert.Equal("12:00", StoryRecordings.Clock(TimeSpan.FromMinutes(12)));
        Assert.Equal(1, StoryRecordings.WholeSeconds(TimeSpan.FromMilliseconds(200)));
        Assert.Equal(21, StoryRecordings.WholeSeconds(TimeSpan.FromSeconds(20.6)));
    }

    [Fact]
    public void Page_starts_follow_the_joined_text()
    {
        Story story = Make("ab", "cde", "f");
        IReadOnlyList<int> starts = StoryRecordings.PageStarts(story.Pages);

        Assert.Equal([0, 4, 9], starts);
        Assert.Equal(0, StoryRecordings.PageOfPosition(starts, 1));
        Assert.Equal(0, StoryRecordings.PageOfPosition(starts, 3));
        Assert.Equal(1, StoryRecordings.PageOfPosition(starts, 4));
        Assert.Equal(2, StoryRecordings.PageOfPosition(starts, 9));
    }

    private async Task<(FileStoryLibrary Source, Story Story)> StoredWithRecordingsAsync()
    {
        FileStoryLibrary source = Library("source");
        Story story = Recorded(0, 2);
        await source.AddAsync(story);
        await source.StoreRecordingAsync(story.Id, 0, path => File.WriteAllTextAsync(path, "audio-1"));
        await source.StoreRecordingAsync(story.Id, 2, path => File.WriteAllTextAsync(path, "audio-3"));
        return (source, story);
    }

    private static async Task<byte[]> ExportAsync(Story story, IStoryLibrary library, bool includeRecordings = true)
    {
        using var stream = new MemoryStream();
        await StoryPackageWriter.WriteAsync(story, library, stream, includeRecordings);
        return stream.ToArray();
    }

    private static List<string> EntryNames(byte[] package)
    {
        using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        return zip.Entries.Select(e => e.FullName).ToList();
    }

    [Fact]
    public async Task A_package_carries_the_recordings_and_a_restore_brings_them_back()
    {
        (FileStoryLibrary source, Story story) = await StoredWithRecordingsAsync();
        byte[] package = await ExportAsync(story, source);

        FileStoryLibrary target = Library("target");
        Story imported = await StoryPackageReader.ImportAsync(new MemoryStream(package), target, keepId: true);

        Assert.Equal(["rec-1.m4a", "rec-3.m4a"], EntryNames(package).Where(StoryRecordings.IsRecordingFileName).Order());
        Assert.Equal([0, 2], imported.Recordings!.Keys.Order());
        Assert.True(imported.UseRecordings);
        Assert.Equal("audio-3", await File.ReadAllTextAsync(target.TryResolveFile(imported.Id, "rec-3.m4a")!));
    }

    [Fact]
    public async Task A_store_style_export_has_no_recording_file_and_no_recording_field()
    {
        (FileStoryLibrary source, Story story) = await StoredWithRecordingsAsync();
        byte[] package = await ExportAsync(story, source, includeRecordings: false);

        Assert.DoesNotContain(EntryNames(package), StoryRecordings.IsRecordingFileName);
        Story imported = await StoryPackageReader.ImportAsync(new MemoryStream(package), Library("target"));
        Assert.Null(imported.Recordings);
        Assert.False(imported.UseRecordings);
    }

    [Fact]
    public async Task A_package_written_with_the_defaults_never_carries_a_parents_voice()
    {
        (FileStoryLibrary source, Story story) = await StoredWithRecordingsAsync();
        using var stream = new MemoryStream();
        await StoryPackageWriter.WriteAsync(story, source, stream);

        Assert.DoesNotContain(EntryNames(stream.ToArray()), StoryRecordings.IsRecordingFileName);
    }

    [Fact]
    public async Task A_recording_whose_file_the_folder_lacks_is_left_out_and_dropped_on_a_restore()
    {
        FileStoryLibrary source = Library("source");
        Story story = Recorded(0, 2);
        await source.AddAsync(story);
        await source.StoreRecordingAsync(story.Id, 0, path => File.WriteAllTextAsync(path, "audio-1"));

        Story imported = await StoryPackageReader.ImportAsync(new MemoryStream(await ExportAsync(story, source)), Library("target"), keepId: true);

        Assert.Equal([0], imported.Recordings!.Keys);
    }

    [Fact]
    public async Task An_ordinary_import_ignores_the_recordings_a_package_names()
    {
        (FileStoryLibrary source, Story story) = await StoredWithRecordingsAsync();

        Story imported = await StoryPackageReader.ImportAsync(new MemoryStream(await ExportAsync(story, source)), Library("target"));

        Assert.Null(imported.Recordings);
        Assert.False(imported.UseRecordings);
    }

    [Fact]
    public void A_recording_plays_only_in_the_language_it_was_made_in()
    {
        Story story = Recorded(1);
        Assert.True(StoryRecordings.Plays(story, 1));
        Assert.Contains(1, StoryRecordings.PagesShownInOwnLanguage(story));

        Story edited = story with { Language = "sv" };
        Assert.False(StoryRecordings.Plays(edited, 1));
        Assert.Empty(StoryRecordings.PagesShownInOwnLanguage(edited));
    }

    [Fact]
    public async Task A_backup_round_trip_brings_the_recordings_back()
    {
        (FileStoryLibrary source, Story story) = await StoredWithRecordingsAsync();
        using var zip = new MemoryStream();
        await StoryBackupWriter.WriteAsync([story], source, zip);

        zip.Position = 0;
        FileStoryLibrary target = Library("restored");
        await StoryBackupReader.RestoreAsync(zip, target);

        Story? restored = await target.GetAsync(story.Id);
        Assert.Equal([0, 2], restored!.Recordings!.Keys.Order());
        Assert.True(File.Exists(target.TryResolveFile(story.Id, "rec-1.m4a")));
    }

    [Fact]
    public async Task Storing_a_page_again_replaces_its_file_and_only_recording_files_can_be_deleted()
    {
        FileStoryLibrary library = Library("lib");
        Story story = Recorded(1);
        await library.AddAsync(story);
        await library.StoreRecordingAsync(story.Id, 1, path => File.WriteAllTextAsync(path, "first"));
        await library.StoreRecordingAsync(story.Id, 1, path => File.WriteAllTextAsync(path, "second"));
        string file = library.TryResolveFile(story.Id, "rec-2.m4a")!;
        Assert.Equal("second", await File.ReadAllTextAsync(file));

        await library.DeleteFileAsync(story.Id, "story.json");
        Assert.NotNull(await library.GetAsync(story.Id));
        await library.DeleteFileAsync(story.Id, "rec-2.m4a");
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task A_failed_write_keeps_the_earlier_recording()
    {
        FileStoryLibrary library = Library("lib");
        Story story = Recorded(0);
        await library.AddAsync(story);
        await library.StoreRecordingAsync(story.Id, 0, path => File.WriteAllTextAsync(path, "keep"));

        await Assert.ThrowsAsync<IOException>(() => library.StoreRecordingAsync(story.Id, 0, _ => throw new IOException("disk")));

        Assert.Equal("keep", await File.ReadAllTextAsync(library.TryResolveFile(story.Id, "rec-1.m4a")!));
        Assert.Equal(["rec-1.m4a", "story.json"], Directory.GetFiles(library.FolderOf(story.Id)).Select(Path.GetFileName).Order());
    }

    [Fact]
    public async Task A_kept_source_page_shows_its_own_words_in_a_translated_story()
    {
        Story story = Make("The pig ran.", "The wolf came.");
        var sentences = StorySentences.Build(story.Text, LanguageRules.For("en"), story.BreakMap());
        var pieces = await StoryTranslation.TranslateAsync(StoryPieces.Of(story), (t, _) => Task.FromResult("T:" + t), CancellationToken.None);

        TranslatedStory result = TranslatedStoryBuilder.Build(story.Pages, sentences, pieces, new HashSet<int> { 1 });

        Assert.Equal("T:The pig ran.\n\nThe wolf came.", result.Text);
        Assert.Equal("The wolf came.", result.Text[result.Sentences[1].Start..result.Sentences[1].End]);
    }
}
