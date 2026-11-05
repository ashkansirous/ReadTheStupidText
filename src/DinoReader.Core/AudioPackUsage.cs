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
    public const string DevicePrefix = "device:";
    public const string PackStarted = "started";
    public const string PackDone = "done";
    public const string PackFailedPrefix = "failed:";

    /// <summary>The detail of a <c>read_voice</c> count: the pre-made audio when any piece had a clip, else the device voice and the reason.</summary>
    public static string ReadVoice(bool anyClip, DeviceVoiceReason whyNone) => anyClip ? Premade : DevicePrefix + ReasonName(whyNone);

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
