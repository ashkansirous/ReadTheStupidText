namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// One voice a pack's <c>pack.json</c> offers: stable id, display name and the model's speaker id.
/// <paramref name="Model"/> is the pack-relative folder holding the voice's own <c>model.onnx</c> and
/// <c>tokens.txt</c> (Slice 89); null keeps the pack-root files.
/// </summary>
public sealed record PackVoice(string Id, string DisplayName, int SpeakerId, string? Model = null);
