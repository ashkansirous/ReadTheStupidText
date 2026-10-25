using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Turns the installed voice packs into what the reader and the voice picker need for one
/// language (Slice 51): the model to drive (language -> pack -> engine) and its voices, or
/// null when no installed pack serves the language, in which case the phone voice reads.
/// Pure: the packs come from <see cref="IVoicePackService.GetInstalledVoicePacks"/>, so this is
/// testable without a device. Like <see cref="VoicePackSelector"/>, the pack serving the fewest
/// languages wins, because it is the most specific one.
/// </summary>
public static class InstalledVoiceCatalog
{
    public static VoiceModelPaths? Resolve(IEnumerable<InstalledVoicePack> installed, string language)
    {
        // A storyteller pack (Dino only, Slice 114) wins over the language's ordinary voice pack when it is installed.
        IEnumerable<InstalledVoicePack> serving = PackSelection.Serving(installed, PackKind.Storyteller, language)
            .Concat(PackSelection.Serving(installed, PackKind.Voice, language));
        foreach (InstalledVoicePack pack in serving)
        {
            IReadOnlyList<VoiceInfo> voices = VoicesOf(pack.Manifest, language);
            if (voices.Count > 0)
            {
                var paths = new VoiceModelPaths(pack.Folder, pack.Manifest.Model, language, voices, pack.Manifest.Version);

                // Aimed at the default voice's model, for a reader that does not pick a model per voice.
                return paths.ForVoice(paths.ResolveVoice(null));
            }
        }

        return null;
    }

    // A pack that lists voices owns them; a Supertonic pack without a list offers the
    // ten styles (Slice 50 packs). Any other pack with no voices is unusable.
    private static IReadOnlyList<VoiceInfo> VoicesOf(PackManifest manifest, string language)
    {
        if (manifest.Voices is { Count: > 0 } listed)
        {
            return listed.Select(v => new VoiceInfo(v.Id, v.DisplayName, language, manifest.Model, v.SpeakerId, v.Model ?? string.Empty)).ToList();
        }

        return manifest.Model switch
        {
            VoiceEngineKind.Supertonic => SupertonicVoiceTable.VoicesFor(language),
            VoiceEngineKind.Kokoro when KokoroVoiceTable.Serves(language) => KokoroVoiceTable.Voices,
            _ => [],
        };
    }
}
