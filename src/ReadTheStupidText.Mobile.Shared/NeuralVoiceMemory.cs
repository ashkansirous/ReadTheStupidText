using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// The neural voice last picked for each engine family (Slice 51), persisted one key per
/// family so choosing a Piper or Melo voice never overwrites the Supertonic choice.
/// </summary>
public static class NeuralVoiceMemory
{
    private const string KeyPrefix = "LastNeuralVoiceId.";

    // Written before per-family memory existed: one key for every engine.
    private const string LegacyKey = "LastNeuralVoiceId";

    public static string? Get(VoiceEngineKind kind)
    {
        string? stored = Preferences.Default.Get<string?>(KeyPrefix + kind, null);
        if (stored is not null)
        {
            return stored;
        }

        string? legacy = Preferences.Default.Get<string?>(LegacyKey, null);
        return VoiceChoices.KindOf(legacy) == kind ? legacy : null;
    }

    public static void Remember(string voiceId)
    {
        if (VoiceChoices.KindOf(voiceId) is { } kind)
        {
            Preferences.Default.Set(KeyPrefix + kind, voiceId);
        }
    }

    /// <summary>The voice for the model's family: the persisted latest pick when it belongs to
    /// the model, else the one remembered for the family, else the model's default.</summary>
    public static VoiceInfo Resolve(VoiceModelPaths model, string? latestPick) =>
        VoiceChoices.Resolve(model, latestPick, Get(model.Kind));
}
