using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Tests;

public class KokoroStorytellerTests
{
    private const string ZipUrl = "https://github.com/example/repo/releases/download/packs-6/kokoro-en-1.zip";

    private static readonly PackInfo Listed = new(
        PackConstants.StorytellerPackId, ["en"], PackKind.Storyteller, "1", 125_582_024, "abc", "Apache-2.0", ZipUrl);

    private static readonly PackInfo ListedSupertonic = new(
        PackConstants.SupertonicPackId, ["en", "fr"], PackKind.Voice, "1", 145_000_000, "def", "Apache-2.0", "https://example.com/supertonic-3-1.zip");

    private static InstalledVoicePack Installed(PackKind kind, string id, string language, VoiceEngineKind model) =>
        new($"/packs/{id}", new PackManifest(id, kind, language, "1", "sherpa-onnx", [], model));

    [Theory]
    [InlineData("af_heart", 3)]
    [InlineData("af_bella", 2)]
    [InlineData("af_nicole", 6)]
    [InlineData("af_sky", 10)]
    [InlineData("af_nova", 7)]
    [InlineData("bf_emma", 21)]
    [InlineData("bf_lily", 23)]
    [InlineData("bf_alice", 20)]
    [InlineData("am_puck", 18)]
    [InlineData("am_santa", 19)]
    [InlineData("bm_fable", 25)]
    [InlineData("am_liam", 15)]
    [InlineData("am_michael", 16)]
    [InlineData("bm_daniel", 24)]
    public void Each_voice_has_the_speaker_id_it_has_in_the_kokoro_v1_voices_table(string key, int sid)
    {
        VoiceInfo voice = KokoroVoiceTable.Voices.Single(v => v.Id == KokoroVoiceTable.IdPrefix + key);

        Assert.Equal(sid, voice.SpeakerId);
        Assert.Equal(VoiceEngineKind.Kokoro, voice.Engine);
    }

    [Fact]
    public void The_table_has_unique_ids_and_speakers_all_inside_the_english_block()
    {
        const int EnglishSpeakers = 28;

        Assert.Equal(KokoroVoiceTable.Voices.Count, KokoroVoiceTable.Voices.Select(v => v.Id).Distinct().Count());
        Assert.Equal(KokoroVoiceTable.Voices.Count, KokoroVoiceTable.Voices.Select(v => v.SpeakerId).Distinct().Count());
        Assert.All(KokoroVoiceTable.Voices, v => Assert.InRange(v.SpeakerId, 0, EnglishSpeakers - 1));
    }

    [Fact]
    public void A_supertonic_pick_maps_to_a_kokoro_voice_of_the_same_gender_and_different_picks_stay_different()
    {
        foreach (bool female in new[] { true, false })
        {
            List<string> picks = SupertonicVoiceTable.Voices.Where(v => SupertonicVoiceTable.IsFemale(v.Id) == female).Select(v => v.Id).ToList();
            List<VoiceInfo> mapped = picks.Select(id => KokoroVoiceTable.MapFromSupertonic(id)!).ToList();

            Assert.All(mapped, v => Assert.Equal(female, KokoroVoiceTable.IsFemale(v.Id)));
            Assert.Equal(picks.Count, mapped.Select(v => v.Id).Distinct().Count());
        }
    }

    [Fact]
    public void The_supertonic_mapping_is_stable_and_ignores_ids_that_are_not_supertonic()
    {
        Assert.Equal(KokoroVoiceTable.MapFromSupertonic("supertonic:M2"), KokoroVoiceTable.MapFromSupertonic("supertonic:M2"));
        Assert.Null(KokoroVoiceTable.MapFromSupertonic("kokoro:af_heart"));
        Assert.Null(KokoroVoiceTable.MapFromSupertonic(null));
    }

    [Fact]
    public void A_kokoro_model_speaks_a_stored_supertonic_pick_as_its_mapped_voice_and_a_supertonic_model_keeps_it()
    {
        var kokoro = new VoiceModelPaths("/k", VoiceEngineKind.Kokoro, "en", KokoroVoiceTable.Voices);
        var supertonic = new VoiceModelPaths("/s");

        Assert.Equal(KokoroVoiceTable.MapFromSupertonic("supertonic:F3"), kokoro.ResolveVoice("supertonic:F3"));
        Assert.Equal(KokoroVoiceTable.MapFromSupertonic("supertonic:M4"), VoiceChoices.Resolve(kokoro, "supertonic:M4"));
        Assert.Equal("supertonic:F3", supertonic.ResolveVoice("supertonic:F3").Id);
    }

    [Fact]
    public void Heart_is_the_default_narrator_and_comes_first()
    {
        Assert.Equal("kokoro:af_heart", KokoroVoiceTable.Default.Id);
        Assert.Equal(KokoroVoiceTable.Default, KokoroVoiceTable.Voices[0]);
    }

    [Fact]
    public void Gender_follows_the_voice_name_and_there_are_voices_of_both()
    {
        Assert.True(KokoroVoiceTable.IsFemale("kokoro:af_bella"));
        Assert.True(KokoroVoiceTable.IsFemale("kokoro:bf_emma"));
        Assert.False(KokoroVoiceTable.IsFemale("kokoro:am_puck"));
        Assert.False(KokoroVoiceTable.IsFemale("supertonic:F1"));
        Assert.Contains(KokoroVoiceTable.Voices, v => KokoroVoiceTable.IsFemale(v.Id));
        Assert.Contains(KokoroVoiceTable.Voices, v => !KokoroVoiceTable.IsFemale(v.Id));
    }

    [Fact]
    public void A_kokoro_voice_id_names_the_kokoro_family()
    {
        Assert.Equal(VoiceEngineKind.Kokoro, VoiceChoices.KindOf("kokoro:af_heart"));
    }

    [Fact]
    public void A_storyteller_manifest_parses_with_its_kind_and_model()
    {
        const string json = """
            {"id":"kokoro-en","kind":"storyteller","language":"en","version":"1","engine":"sherpa-onnx","model":"kokoro",
             "files":["model.onnx","voices.bin","tokens.txt","lexicon-us-en.txt"]}
            """;

        PackManifest manifest = PackManifest.Parse(json);

        Assert.Equal(PackKind.Storyteller, manifest.Kind);
        Assert.Equal(VoiceEngineKind.Kokoro, manifest.Model);
    }

    [Fact]
    public void The_catalog_reads_a_storyteller_entry_and_the_text_readers_voice_choice_never_picks_it()
    {
        const string json = """
            [{"id":"kokoro-en","language":"en","kind":"storyteller","version":"1","sizeBytes":125582024,"sha256":"abc","license":"x","url":"https://example.com/kokoro-en-1.zip"},
             {"id":"supertonic-3","language":"en,fr","kind":"voice","version":"1","sizeBytes":145000000,"sha256":"def","license":"x","url":"https://example.com/supertonic-3-1.zip"}]
            """;

        IReadOnlyList<PackInfo> catalog = PackCatalogParser.Parse(json);

        Assert.Contains(catalog, p => p.Kind == PackKind.Storyteller);
        Assert.Equal(PackConstants.SupertonicPackId, VoicePackSelector.Select(catalog, "en")!.Id);
    }

    [Fact]
    public void Without_a_storyteller_pack_english_resolves_to_supertonic()
    {
        InstalledVoicePack supertonic = Installed(PackKind.Voice, PackConstants.SupertonicPackId, "en,fr", VoiceEngineKind.Supertonic);

        VoiceModelPaths? model = InstalledVoiceCatalog.Resolve([supertonic], "en");

        Assert.Equal(VoiceEngineKind.Supertonic, model!.Kind);
    }

    [Fact]
    public void With_a_storyteller_pack_english_resolves_to_kokoro_with_heart_first()
    {
        InstalledVoicePack supertonic = Installed(PackKind.Voice, PackConstants.SupertonicPackId, "en,fr", VoiceEngineKind.Supertonic);
        InstalledVoicePack kokoro = Installed(PackKind.Storyteller, PackConstants.StorytellerPackId, "en", VoiceEngineKind.Kokoro);

        VoiceModelPaths? model = InstalledVoiceCatalog.Resolve([supertonic, kokoro], "en");

        Assert.Equal(VoiceEngineKind.Kokoro, model!.Kind);
        Assert.Equal("/packs/kokoro-en", model.RootDir);
        Assert.Equal(KokoroVoiceTable.Voices.Select(v => v.Id), model.AvailableVoices.Select(v => v.Id));
        Assert.Equal("kokoro:af_heart", model.ResolveVoice(null).Id);
    }

    [Fact]
    public void A_storyteller_pack_leaves_other_languages_on_their_own_pack()
    {
        InstalledVoicePack supertonic = Installed(PackKind.Voice, PackConstants.SupertonicPackId, "en,fr", VoiceEngineKind.Supertonic);
        InstalledVoicePack kokoro = Installed(PackKind.Storyteller, PackConstants.StorytellerPackId, "en", VoiceEngineKind.Kokoro);

        Assert.Equal(VoiceEngineKind.Supertonic, InstalledVoiceCatalog.Resolve([supertonic, kokoro], "fr")!.Kind);
    }

    [Fact]
    public void A_newer_catalog_version_of_the_installed_storyteller_pack_is_an_update_and_the_same_one_is_not()
    {
        InstalledVoicePack kokoro = Installed(PackKind.Storyteller, PackConstants.StorytellerPackId, "en", VoiceEngineKind.Kokoro);
        PackInfo newer = Listed with { Version = "2" };

        Assert.True(StorytellerOffer.UpdateAvailable(kokoro, [newer]));
        Assert.False(StorytellerOffer.UpdateAvailable(kokoro, [Listed]));
        Assert.False(StorytellerOffer.UpdateAvailable(kokoro, [ListedSupertonic]));
    }

    [Fact]
    public void A_service_that_does_not_say_otherwise_has_no_storyteller_support()
    {
        IVoicePackService service = new NoStorytellerService();

        Assert.False(service.SupportsStorytellerPacks);
        Assert.Empty(service.GetInstalledStorytellerPacks());
        Assert.False(service.Uninstall(PackConstants.StorytellerPackId));
    }

    [Fact]
    public void A_storyteller_pack_alone_is_not_a_voice_for_the_text_reader()
    {
        InstalledVoicePack kokoro = Installed(PackKind.Storyteller, PackConstants.StorytellerPackId, "en", VoiceEngineKind.Kokoro);

        Assert.Empty(PackSelection.Serving([kokoro], PackKind.Voice, "en"));
    }

    [Theory]
    [InlineData(125_582_024L, 126)]
    [InlineData(90_400_000L, 90)]
    [InlineData(10L, 1)]
    public void The_size_is_named_in_whole_megabytes(long bytes, int megabytes)
    {
        Assert.Equal(megabytes, StorytellerOffer.Megabytes(bytes));
    }

    // Implements only the members every service must: the storyteller ones keep their defaults.
    private sealed class NoStorytellerService : IVoicePackService
    {
        public Task<PackCatalogResult> GetCatalogAsync(CancellationToken cancellationToken) =>
            Task.FromResult(PackCatalogResult.Failure(PackDownloadResult.Failed));

        public Task<PackCatalogResult> GetCatalogCachedAsync(TimeSpan maxAge, CancellationToken cancellationToken) => GetCatalogAsync(cancellationToken);
        public string? GetInstalledPath(string packId) => null;
        public IReadOnlyList<InstalledVoicePack> GetInstalledVoicePacks() => [];
        public IReadOnlyList<InstalledVoicePack> GetInstalledOcrPacks() => [];

        public Task<PackDownloadResult> DownloadAsync(string packId, IProgress<double>? progress, CancellationToken cancellationToken) =>
            Task.FromResult(PackDownloadResult.Failed);
    }
}
