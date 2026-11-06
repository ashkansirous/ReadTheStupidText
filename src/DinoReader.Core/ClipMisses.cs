namespace DinoReader.Core;

/// <summary>Why a spoken piece found no clip in an installed pack (Slice 273): the pack's entry for its position is missing or its hash is not the text's (the pack and the text are out of line), or the entry is right but its file is not on disk.</summary>
public enum ClipMiss
{
    None,
    TextMismatch,
    FileMissing,
}

/// <summary>
/// The pieces of one read that found no clip, by why (Slice 273). Misses are kept per piece, so looking the same piece up again (a piece is looked up more than once while a read is voiced)
/// counts it once.
/// </summary>
public sealed class ClipMisses
{
    private readonly HashSet<int> _textMismatch = [];
    private readonly HashSet<int> _fileMissing = [];

    public int TextMismatch => _textMismatch.Count;

    public int FileMissing => _fileMissing.Count;

    public void Note(int piece, ClipMiss miss)
    {
        if (miss == ClipMiss.TextMismatch)
        {
            _textMismatch.Add(piece);
        }
        else if (miss == ClipMiss.FileMissing)
        {
            _fileMissing.Add(piece);
        }
    }

    /// <summary>The reason a read with no clip at all gives: a hash that misses means the pack is out of line with the text, else the files are missing.</summary>
    public DeviceVoiceReason Reason => TextMismatch > 0 ? DeviceVoiceReason.TextMismatch : DeviceVoiceReason.ClipMissing;
}
