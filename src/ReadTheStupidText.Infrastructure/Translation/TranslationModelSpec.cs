using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReadTheStupidText.Infrastructure.Translation;

/// <summary>
/// The decoding parameters of one translation leg, read from the pack's <c>translation.json</c>
/// (copied from the model's <c>config.json</c> at build time). <see cref="TargetToken"/> is the
/// leading <c>&gt;&gt;xx&lt;&lt;</c> a multi-target model needs in the source text, null for a single-target model.
/// </summary>
public sealed record TranslationModelSpec(
    string Source,
    string Target,
    string? TargetToken,
    int DecoderStartTokenId,
    int EosTokenId,
    int PadTokenId,
    int MaxLength)
{
    public static TranslationModelSpec Parse(string json)
    {
        SpecDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<SpecDto>(json) ?? throw new FormatException("Empty translation.json.");
        }
        catch (JsonException ex)
        {
            throw new FormatException("Malformed translation.json.", ex);
        }

        if (dto.Source is null || dto.Target is null || dto.DecoderStartTokenId is null
            || dto.EosTokenId is null || dto.PadTokenId is null || dto.MaxLength is not > 0)
        {
            throw new FormatException("translation.json is missing required fields.");
        }

        return new TranslationModelSpec(
            dto.Source, dto.Target, string.IsNullOrEmpty(dto.TargetToken) ? null : dto.TargetToken,
            dto.DecoderStartTokenId.Value, dto.EosTokenId.Value, dto.PadTokenId.Value, dto.MaxLength.Value);
    }

    private sealed record SpecDto(
        [property: JsonPropertyName("source")] string? Source,
        [property: JsonPropertyName("target")] string? Target,
        [property: JsonPropertyName("targetToken")] string? TargetToken,
        [property: JsonPropertyName("decoderStartTokenId")] int? DecoderStartTokenId,
        [property: JsonPropertyName("eosTokenId")] int? EosTokenId,
        [property: JsonPropertyName("padTokenId")] int? PadTokenId,
        [property: JsonPropertyName("maxLength")] int? MaxLength);
}
