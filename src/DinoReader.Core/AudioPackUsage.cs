using ReadTheStupidText.Application.Usage;

namespace DinoReader.Core;

/// <summary>Why a read used the device voice instead of the pre-made audio (Slice 267). The closed list the Worker accepts after <c>device:</c>.</summary>
public enum DeviceVoiceReason
{
    /// <summary>No pack is installed for the book in that language (an own book, or the pack was never fetched).</summary>
    NoPack,

    /// <summary>A pack download for the book and language failed in this launch.</summary>
    PackFailed,

    /// <summary>The pack is installed, but the plain and spoken pieces do not line up, so none of its clips fit.</summary>
    TextMismatch,

    /// <summary>The pack is installed and lines up, but no piece had a clip.</summary>
    ClipMissing,
}

/// <summary>Why one pack download failed (Slice 267): the closed list the Worker accepts after <c>failed:</c>.</summary>
public enum PackFailureKind
{
    Network,
    Size,
    Unzip,
    Clip,

    /// <summary>The store has no such pack version (a 404).</summary>
    Missing,

    /// <summary>The phone could not write the pack: disk full or no access.</summary>
    Storage,
}

/// <summary>
/// The detail values of the two audio counts (Slice 267): <c>read_voice</c> says which voice a read used (<c>premade</c>, or <c>device:&lt;reason&gt;</c>) and <c>audio_pack_download</c> says how a pack
/// download went (<c>started</c>, <c>done</c>, or <c>failed:&lt;kind&gt;</c>). Closed lists only: no book text, title or file name can be carried. The Worker checks the same lists.
/// </summary>
public static class AudioPackUsage
{
    public const string Premade = "premade";
    public const string PartialDetail = "partial";
    public const string DevicePrefix = "device:";
    public const string PackStarted = "started";
    public const string PackDone = "done";
    public const string PackFailedPrefix = "failed:";

    /// <summary>The reader ended the download (Slice 275): not a failure, so it adds no retry count and no pack_failed fallback.</summary>
    public const string PackCancelled = "cancelled";

    /// <summary>
    /// What a read used (Slice 273): <c>premade</c> when every spoken piece had a clip, <c>partial</c> when some had one and the others fell back to the device voice (Slice 272: a read that
    /// falls back from page 5 on is not a premade read), else <c>device:&lt;reason&gt;</c>. Carries the number of pieces read by clips and by the device voice.
    /// The fallback reason of a partial read is <c>clip_missing</c> when more of its misses were missing files than hash mismatches, else <c>text_mismatch</c> (a tie, or no miss counted, says text_mismatch).
    /// A read with no spoken piece at all is not a fallback, and neither is a read whose book has no pack to use (<c>no_pack</c>: an own book, or a book with no pack published in that language).
    /// </summary>
    public static ReadVoiceUse ReadVoice(int clipPieces, int devicePieces, DeviceVoiceReason whyNone, int textMismatches = 0, int filesMissing = 0) => (clipPieces, devicePieces) switch
    {
        (> 0, 0) => new ReadVoiceUse(Premade, clipPieces, devicePieces, null),
        (> 0, _) => new ReadVoiceUse(PartialDetail, clipPieces, devicePieces, filesMissing > textMismatches ? FallbackReason.ClipMissing : FallbackReason.TextMismatch),
        (0, 0) => new ReadVoiceUse(DevicePrefix + ReasonName(whyNone), clipPieces, devicePieces, null),
        _ => new ReadVoiceUse(DevicePrefix + ReasonName(whyNone), clipPieces, devicePieces, whyNone == DeviceVoiceReason.NoPack ? null : ReasonName(whyNone)),
    };

    /// <summary>The detail of a failed <c>audio_pack_download</c> count.</summary>
    public static string PackFailed(PackFailureKind kind) => PackFailedPrefix + KindName(kind);

    /// <summary>The reason after <c>device:</c>.</summary>
    public static string ReasonName(DeviceVoiceReason reason) => reason switch
    {
        DeviceVoiceReason.NoPack => "no_pack",
        DeviceVoiceReason.PackFailed => "pack_failed",
        DeviceVoiceReason.TextMismatch => "text_mismatch",
        _ => "clip_missing",
    };

    /// <summary>The kind after <c>failed:</c>.</summary>
    public static string KindName(PackFailureKind kind) => kind switch
    {
        PackFailureKind.Network => "network",
        PackFailureKind.Size => "size",
        PackFailureKind.Unzip => "unzip",
        PackFailureKind.Missing => "missing",
        PackFailureKind.Storage => "storage",
        _ => "clip",
    };
}

/// <summary>
/// The voice a read used (Slice 273): the <c>read_voice</c> detail, how many pieces were read by pre-made clips and by the device voice, and the <c>fallback</c> reason (a
/// <see cref="FallbackReason"/> value) when some or all pieces fell back, else null.
/// </summary>
public sealed record ReadVoiceUse(string Detail, int ClipPieces, int DevicePieces, string? FallbackReason);
