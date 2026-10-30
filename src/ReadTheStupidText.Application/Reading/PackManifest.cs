using System.Text.Json;
using System.Text.Json.Serialization;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The <c>pack.json</c> inside an extracted pack (id, kind, language, version, engine, file list).
/// <see cref="Model"/> and <see cref="Voices"/> arrived with Slice 51: a manifest without them
/// is a Supertonic pack and gets the Supertonic voice table.
/// </summary>
public sealed record PackManifest(
    string Id,
    PackKind Kind,
    string Language,
    string Version,
    string Engine,
    IReadOnlyList<string> Files,
    VoiceEngineKind Model = VoiceEngineKind.Supertonic,
    IReadOnlyList<PackVoice>? Voices = null)
{
    /// <summary>The files the manifest lists that <paramref name="exists"/> does not find.</summary>
    public IReadOnlyList<string> MissingFiles(Func<string, bool> exists) =>
        Files.Where(f => !exists(f)).ToList();

    /// <summary>
    /// For a Piper pack (a VITS pack named in <see cref="PackConstants.PiperPackIds"/>), the model folders whose <c>tokens.txt</c> sherpa-onnx would abort on (Slice 201): a pack
    /// that lists one is broken, so it is neither installed nor loaded. <paramref name="readTokens"/> gets the pack-relative
    /// path and returns the file's raw bytes, or null when it is not there (the file-list check reports that one).
    /// </summary>
    public IReadOnlyList<string> BrokenTokenFolders(Func<string, byte[]?> readTokens)
    {
        if (Model != VoiceEngineKind.Vits || !PackConstants.PiperPackIds.Contains(Id))
        {
            return [];
        }

        IEnumerable<PackVoice> voices = Voices is { Count: > 0 } ? Voices : [new PackVoice(string.Empty, string.Empty, 0)];
        IEnumerable<string> folders = voices
            .Select(v => v.Model?.Replace('\\', '/').TrimEnd('/') ?? string.Empty)
            .Distinct();
        return folders
            .Where(f => readTokens(f.Length == 0 ? VitsFiles.Tokens : $"{f}/{VitsFiles.Tokens}") is { } bytes && PiperTokens.ProblemInBytes(bytes) is not null)
            .ToList();
    }

    /// <summary><see cref="BrokenTokenFolders"/> for the extracted pack in <paramref name="root"/>.</summary>
    public IReadOnlyList<string> BrokenTokenFoldersIn(string root) =>
        BrokenTokenFolders(path =>
        {
            string file = Path.Combine(root, path);
            return File.Exists(file) ? File.ReadAllBytes(file) : null;
        });

    public static PackManifest Parse(string json)
    {
        ManifestDto dto = Deserialize(json);
        if (dto.Id is null || dto.Language is null || dto.Version is null || dto.Engine is null || dto.Files is null
            || !Enum.TryParse(dto.Kind, ignoreCase: true, out PackKind kind) || !Enum.IsDefined(kind))
        {
            throw new FormatException("pack.json is missing required fields.");
        }

        IReadOnlyList<PackVoice> voices = ParseVoices(dto.Voices);
        RequireModelFiles(voices, dto.Files);
        return new PackManifest(dto.Id, kind, dto.Language, dto.Version, dto.Engine, dto.Files, ParseModel(dto.Model), voices);
    }

    // A voice with its own model folder must ship the model and the tokens in it, so a typo in the
    // folder fails at install (InvalidPack) and not later, at synthesis, as a native load error.
    private static void RequireModelFiles(IReadOnlyList<PackVoice> voices, string[] files)
    {
        var listed = files.Select(f => f.Replace('\\', '/')).ToHashSet(StringComparer.Ordinal);
        foreach (PackVoice voice in voices.Where(v => v.Model is not null))
        {
            string folder = voice.Model!.Replace('\\', '/').TrimEnd('/');
            if (!listed.Contains($"{folder}/{VitsFiles.Model}") || !listed.Contains($"{folder}/{VitsFiles.Tokens}"))
            {
                throw new FormatException("pack.json lists a voice whose model folder is missing its model or tokens.");
            }
        }
    }

    private static VoiceEngineKind ParseModel(string? model)
    {
        if (model is null)
        {
            return VoiceEngineKind.Supertonic;
        }

        if (!Enum.TryParse(model, ignoreCase: true, out VoiceEngineKind kind) || !Enum.IsDefined(kind))
        {
            throw new FormatException("pack.json names an unknown voice model.");
        }

        return kind;
    }

    private static IReadOnlyList<PackVoice> ParseVoices(VoiceDto[]? voices)
    {
        if (voices is null)
        {
            return [];
        }

        if (voices.Any(v => v.Id is null || v.Name is null))
        {
            throw new FormatException("pack.json lists a voice without an id or name.");
        }

        return voices.Select(v => new PackVoice(v.Id!, v.Name!, v.SpeakerId ?? 0, ValidModelFolder(v.Model))).ToList();
    }

    // A voice's model folder stays inside the pack: a real relative folder, with no "..", "." or empty segment.
    private static string? ValidModelFolder(string? folder)
    {
        if (folder is null)
        {
            return null;
        }

        bool escapes = string.IsNullOrWhiteSpace(folder)
            || Path.IsPathRooted(folder)
            || folder.TrimEnd('/', '\\').Split('/', '\\').Any(segment => segment is ".." or "." or "");
        return escapes ? throw new FormatException("pack.json lists a voice model outside the pack.") : folder;
    }

    private static ManifestDto Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ManifestDto>(json) ?? throw new FormatException("Empty pack.json.");
        }
        catch (JsonException ex)
        {
            throw new FormatException("Malformed pack.json.", ex);
        }
    }

    private sealed record ManifestDto(
        [property: JsonPropertyName(PackJsonKeys.Id)] string? Id,
        [property: JsonPropertyName(PackJsonKeys.Kind)] string? Kind,
        [property: JsonPropertyName(PackJsonKeys.Language)] string? Language,
        [property: JsonPropertyName(PackJsonKeys.Version)] string? Version,
        [property: JsonPropertyName(PackJsonKeys.Engine)] string? Engine,
        [property: JsonPropertyName(PackJsonKeys.Files)] string[]? Files,
        [property: JsonPropertyName(PackJsonKeys.Model)] string? Model = null,
        [property: JsonPropertyName(PackJsonKeys.Voices)] VoiceDto[]? Voices = null);

    private sealed record VoiceDto(
        [property: JsonPropertyName(PackJsonKeys.Id)] string? Id,
        [property: JsonPropertyName(PackJsonKeys.VoiceName)] string? Name,
        [property: JsonPropertyName(PackJsonKeys.SpeakerId)] int? SpeakerId,
        [property: JsonPropertyName(PackJsonKeys.VoiceModel)] string? Model = null);
}
