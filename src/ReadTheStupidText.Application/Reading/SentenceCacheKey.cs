using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Identifies one cached sentence of audio (Slice 47, Decision 66): the normalized sentence
/// text plus everything that changes how it sounds. Slice 60 reads each chunk in its own
/// voice and Slice 51 switches engine and language per read, so the voice, the engine kind,
/// the language, the model directory, the pack version and the voice flavour (Slice 113) are all part of the key: audio is never reused across
/// any of them. Playback speed is not (it is applied by the player, not baked into the audio).
/// </summary>
internal sealed partial record SentenceCacheKey(
    string Text, VoiceEngineKind Engine, string Language, string VoiceId, string ModelId, string PackVersion = "",
    VoiceFlavour Flavour = VoiceFlavour.Normal)
{
    private const char FieldSeparator = '\u001f';
    private const int ShortHashLength = 8;

    /// <summary>Trims the sentence and collapses every whitespace run to one space.</summary>
    public static string Normalize(string sentence) => WhitespaceRun().Replace(sentence.Trim(), " ");

    /// <summary>SHA-256 (hex) of the normalized sentence alone, for log lines.</summary>
    public string TextHash => Hash(Normalize(Text));

    /// <summary>The first eight characters of <see cref="TextHash"/>.</summary>
    public string ShortHash => TextHash[..ShortHashLength];

    /// <summary>The cache's identity for this key: SHA-256 (hex) over the sentence and every
    /// field above, safe as a file name.</summary>
    public string Id => Hash(string.Join(
        FieldSeparator, (int)Engine, Language, VoiceId, ModelId, PackVersion, Normalize(Text)) + FlavourSuffix);

    // A Normal sentence keeps the id it had before flavours existed, so its cached audio is still found
    // (Slice 113); every other flavour bakes pitch and pace into the audio, so it is a different clip.
    private string FlavourSuffix => Flavour == VoiceFlavour.Normal ? "" : FieldSeparator + Flavour.ToString();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();
}
