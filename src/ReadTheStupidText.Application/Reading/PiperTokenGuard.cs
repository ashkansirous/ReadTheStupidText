using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The engine-build guard shared by the Windows, Android and iOS sherpa builders (Slice 201, Slice 303): sherpa-onnx's Piper
/// reader aborts the whole process (no managed exception) on a bad <c>tokens.txt</c>, so a Piper pack is checked before the native load.
/// It gates on the Piper pack ids, not on the VITS kind, so a non-Piper VITS model needs no tokens file.
/// </summary>
public static class PiperTokenGuard
{
    /// <summary>
    /// True when <paramref name="paths"/> is a voice of a Piper pack (a VITS pack named in <see cref="PackConstants.PiperPackIds"/>).
    /// Fails safe: a VITS voice with no pack id is checked too, as before Slice 303, since sherpa aborts the process on a bad file.
    /// </summary>
    public static bool AppliesTo(VoiceModelPaths paths) =>
        paths.Kind == VoiceEngineKind.Vits
        && (string.IsNullOrEmpty(paths.PackId) || PackConstants.PiperPackIds.Contains(paths.PackId));

    /// <summary>The first rule the voice's <c>tokens.txt</c> breaks, or null when it is sound or the check does not apply.</summary>
    public static string? Problem(VoiceModelPaths paths) =>
        AppliesTo(paths) ? PiperTokens.ProblemInFile(Path.Combine(paths.ModelFolder, VitsFiles.Tokens)) : null;

    /// <summary>Throws <see cref="InvalidDataException"/> before the native load when the voice's tokens file would crash the engine.</summary>
    public static void Require(VoiceModelPaths paths)
    {
        string? problem = Problem(paths);
        if (problem is not null)
        {
            throw new InvalidDataException($"Piper tokens file in '{paths.ModelFolder}' would crash the speech engine: {problem}.");
        }
    }
}
