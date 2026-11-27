namespace DinoReader.Core;

/// <summary>What <see cref="PartTakeCommit.SaveAsync"/> stored: the saved story, the files the new take replaced (to delete once the save held) and whether it was the first recording. <paramref name="ReplacesBeyond"/> is the card's warning worked out from the same commit-time set as <paramref name="ReplacedFiles"/>, so a late orphan that reaches past the key is visible to the caller (the page's earlier story cannot see it).</summary>
public sealed record PartTakeSaved(Story Story, IReadOnlyList<string> ReplacedFiles, bool First, bool ReplacesBeyond);

/// <summary>
/// Slice 464 (moved out of the Record page so a test runs the real step): the story save of one take whose file is already in the book's folder. The story is read (that read adopts the part file just placed as a clip, so
/// <see cref="StoryRecordings.WithoutFile"/> takes it off again), the take is added, and the files it replaces are listed: every clip it overlaps or covers. Adoption keeps only the first of several overlapping files by
/// name, so an older overlapping part file is an orphan when the fresh one sorts first; such an orphan is put in as a clip first (<see cref="PartClipAdoption.WithOrphansAdopted"/>) and so goes exactly as an adopted clip
/// does under Slice 443: whatever its range, an overlapping file is replaced, and the card warns (<see cref="StoryRecordings.ReplacesBeyond"/>) when it reaches past the take's words. A part file that does not
/// overlap the key is kept.
/// </summary>
public static class PartTakeCommit
{
    /// <summary>Saves <paramref name="recording"/> under <paramref name="key"/> (its file already stored as <c>recording.File</c>); on failure the new file is deleted and the failure rethrown. <paramref name="fallback"/> is the story the page holds, used when the library no longer has the book.</summary>
    public static async Task<PartTakeSaved> SaveAsync(IStoryLibrary library, string storyId, Story fallback, StoryClipKey key, PageRecording recording, SentenceCover? cover)
    {
        string fileName = recording.File!;
        try
        {
            Story read = StoryRecordings.WithoutFile(await library.GetAsync(storyId) ?? fallback, fileName); // the read adopts the part file just placed as a clip; it is the take being saved, not an earlier one
            IReadOnlyList<string> onDisk = await library.PartFileNamesAsync(storyId);
            Story latest = PartClipAdoption.WithOrphansAdopted(read, key, fileName, onDisk); // an older overlapping file the read skipped is an adopted clip from here on, so one set drives everything below
            bool first = StoryRecordings.ReplacedClips(latest, key, cover).Count == 0; // the same set that drives the card's warning and the files deleted
            List<string> replaced = [.. StoryRecordings.ReplacedFiles(latest, key, cover)]; // a sentence clip over the take's words goes too, so the new take is the one that plays
            replaced.AddRange(PartClipAdoption.OverlappingOrphans(read, key, fileName, onDisk).Except(replaced)); // two orphans of one key share a clip slot, so the one the slot lost is listed here
            Story updated = StoryRecordings.WithSavedTake(latest, key, recording) with { UseRecordings = read.UseRecordings || StoryRecordings.Count(read) == 0 }; // an orphan put in above is not an earlier recording the switch has seen
            await library.UpdateAsync(updated);
            return new PartTakeSaved(updated, replaced, first, StoryRecordings.ReplacesBeyond(latest, key, cover));
        }
        catch
        {
            // no-fallback: the story did not take the clip, so the new file goes (it is the only one this commit made) and the failure is rethrown
            await library.DeleteFileAsync(storyId, fileName);
            throw;
        }
    }
}
