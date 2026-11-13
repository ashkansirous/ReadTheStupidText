using Sirous.Reading.Application.Usage;

namespace DinoReader.Core;

/// <summary>Why a spoken piece found no clip in an installed pack (Slice 273): the pack's entry for its position is missing or its hash is not the text's (the pack and the text are out of line), or the entry is right but its file is not on disk.</summary>
public enum ClipMiss
{
    None,
    TextMismatch,
    FileMissing,

    /// <summary>Slice 351: the pack has no entry at all for the piece's position (an untranslated or new line); counted with <see cref="TextMismatch"/> for the read's voice detail.</summary>
    NoClip,
}

/// <summary>
/// The pieces of one read that found no clip, by why (Slice 273). Misses are kept per piece, so looking the same piece up again (a piece is looked up more than once while a read is voiced)
/// counts it once.
/// </summary>
public sealed class ClipMisses
{
    private readonly HashSet<int> _textMismatch = [];
    private readonly HashSet<int> _fileMissing = [];
    private readonly HashSet<int> _noClip = [];

    /// <summary>Pieces whose clip did not fit the text, with the ones that had no entry at all (<see cref="NoClip"/>).</summary>
    public int TextMismatch => _textMismatch.Count;

    /// <summary>Slice 351: the pieces among <see cref="TextMismatch"/> whose position the pack has no entry for.</summary>
    public int NoClip => _noClip.Count;

    public int FileMissing => _fileMissing.Count;

    public void Note(int piece, ClipMiss miss)
    {
        if (miss is ClipMiss.TextMismatch or ClipMiss.NoClip)
        {
            _textMismatch.Add(piece);
            if (miss == ClipMiss.NoClip)
            {
                _noClip.Add(piece);
            }
        }
        else if (miss == ClipMiss.FileMissing)
        {
            _fileMissing.Add(piece);
        }
    }

    /// <summary>The reason a read with no clip at all gives: a hash that misses means the pack is out of line with the text, else the files are missing.</summary>
    public DeviceVoiceReason Reason => TextMismatch > 0 ? DeviceVoiceReason.TextMismatch : DeviceVoiceReason.ClipMissing;

    /// <summary>
    /// The fallback reason a piece shown for a beat reports (Slice 313, split by cause in Slice 351). With no pack opened (<paramref name="misses"/> null) the read's own reason decides: <c>no_pack</c>,
    /// <c>pack_failed</c> or <c>text_mismatch</c>. With a pack opened: <c>text_mismatch</c> when a hash missed, <c>no_clip</c> when every miss was a position the pack has no entry for,
    /// <c>file_missing</c> when an entry's file is not on disk, else <c>clip_missing</c> (nothing was looked up).
    /// </summary>
    public static string SilentPieceReason(DeviceVoiceReason readReason, ClipMisses? misses)
    {
        return ShelfPieceReason(readReason, misses);
    }

    /// <summary>
    /// Like <see cref="SilentPieceReason"/>, for a book of a known kind: an own book (<paramref name="shelfBook"/> false) never has a pack, so a silent piece of it is the expected state and
    /// reports nothing (null), where a shelf book's silent piece names its cause.
    /// </summary>
    public static string? SilentPieceReasonFor(bool shelfBook, DeviceVoiceReason readReason, ClipMisses? misses)
    {
        return shelfBook ? ShelfPieceReason(readReason, misses) : null;
    }

    /// <summary>Slice 351 (review): the fallback reason a whole read reports for a book of a known kind: an own book never has a pack, so none is reported (null), whatever the read's cause.</summary>
    public static string? ReadReasonFor(bool shelfBook, string? reason)
    {
        return shelfBook ? reason : null;
    }

    private static string ShelfPieceReason(DeviceVoiceReason readReason, ClipMisses? misses)
    {
        if (misses is null)
        {
            return readReason switch
            {
                DeviceVoiceReason.NoPack => FallbackReason.NoPack,
                DeviceVoiceReason.PackFailed => FallbackReason.PackFailed,
                DeviceVoiceReason.TextMismatch => FallbackReason.TextMismatch,
                _ => FallbackReason.ClipMissing,
            };
        }

        if (misses.TextMismatch > 0)
        {
            return misses.NoClip == misses.TextMismatch ? FallbackReason.NoClip : FallbackReason.TextMismatch;
        }

        return misses.FileMissing > 0 ? FallbackReason.FileMissing : FallbackReason.ClipMissing;
    }
}
