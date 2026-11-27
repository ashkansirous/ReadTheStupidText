namespace DinoReader.Core;

/// <summary>
/// The one switch for recording a character's own words (Slice 395, wave decision 2026-11-27). While <see cref="Enabled"/> is false the Record screen, the intro, the progress counts and saving behave as before the slice:
/// every card is a sentence card and every clip a sentence clip, so no part clip is ever created. Part keys, their JSON, backup and package carriage, the piece building and the move rules stay in the code and are
/// tested with the switch on. Slice 443 turns it on together with playback of a part take, so a take that is saved always plays.
/// </summary>
public static class PartTakes
{
    /// <summary>Whether the Record screen cuts a book into parts. False until Slice 443 plays a part take.</summary>
    public static bool Enabled { get; set; }
}
