using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Tests;

public class KokoroStorytellerTests
{
    private const string ZipUrl = "https://github.com/example/repo/releases/download/packs-6/kokoro-en-1.zip";

    private static readonly PackInfo Listed = new(
        PackConstants.StorytellerPackId, ["en", "es", "pt", "zh"], PackKind.Storyteller, "1", 125_582_024, "abc", "Apache-2.0", ZipUrl);

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
    [InlineData("af_sarah", 9)]
    [InlineData("af_kore", 5)]
    [InlineData("af_aoede", 1)]
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

    [Theory]
    [InlineData("es", "ef_dora", 28)]
    [InlineData("es", "em_alex", 29)]
    [InlineData("es", "em_santa", 53)]
    [InlineData("pt", "pf_dora", 42)]
    [InlineData("pt", "pm_alex", 43)]
    [InlineData("pt", "pm_santa", 44)]
    [InlineData("zh", "zf_xiaobei", 45)]
    [InlineData("zh", "zf_xiaoni", 46)]
    [InlineData("zh", "zf_xiaoxiao", 47)]
    [InlineData("zh", "zf_xiaoyi", 48)]
    [InlineData("zh", "zm_yunjian", 49)]
    [InlineData("zh", "zm_yunxi", 50)]
    [InlineData("zh", "zm_yunxia", 51)]
    [InlineData("zh", "zm_yunyang", 52)]
    public void Each_spanish_portuguese_and_chinese_voice_has_its_speaker_id_and_language(string language, string key, int sid)
    {
        VoiceInfo voice = KokoroVoiceTable.VoicesFor(language).Single(v => v.Id == KokoroVoiceTable.IdPrefix + key);

        Assert.Equal(sid, voice.SpeakerId);
        Assert.Equal(language, voice.Language);
        Assert.Equal(VoiceEngineKind.Kokoro, voice.Engine);
        Assert.Equal(key[1] == 'f', KokoroVoiceTable.IsFemale(voice.Id));
    }

    [Fact]
    public void The_storyteller_voices_serve_english_spanish_portuguese_and_chinese_and_not_french()
    {
        Assert.All(new[] { "en", "es", "pt", "zh" }, language => Assert.True(KokoroVoiceTable.Serves(language)));
        Assert.All(new[] { "fr", "ja", "fa", "ru" }, language => Assert.False(KokoroVoiceTable.Serves(language)));
        Assert.Empty(KokoroVoiceTable.VoicesFor("fr"));
    }

    [Fact]
    public void The_whole_table_has_unique_ids_and_speakers_and_each_language_has_a_woman_and_a_man()
    {
        string[] languages = ["en", "es", "pt", "zh"];
        List<VoiceInfo> all = languages.SelectMany(KokoroVoiceTable.VoicesFor).ToList();

        Assert.Equal(all.Count, all.Select(v => v.Id).Distinct().Count());
        Assert.Equal(all.Count, all.Select(v => v.SpeakerId).Distinct().Count());
        Assert.All(languages, l => Assert.Contains(KokoroVoiceTable.VoicesFor(l), v => KokoroVoiceTable.IsFemale(v.Id)));
        Assert.All(languages, l => Assert.Contains(KokoroVoiceTable.VoicesFor(l), v => !KokoroVoiceTable.IsFemale(v.Id)));
    }

    [Theory]
    [InlineData("en", "")]
    [InlineData("es", "es")]
    [InlineData("pt", "pt-br")]
    [InlineData("zh", "cmn")]
    public void The_engine_lang_is_the_espeak_voice_and_empty_for_english(string language, string lang)
    {
        Assert.Equal(lang, KokoroVoiceTable.EngineLang(language));
    }

    [Fact]
    public void Only_the_chinese_engine_loads_the_chinese_lexicon_dictionary_and_rule_fsts()
    {
        Assert.Equal([KokoroFiles.Lexicon], KokoroFiles.LexiconsFor("es"));
        Assert.Equal([KokoroFiles.Lexicon, KokoroFiles.LexiconZh], KokoroFiles.LexiconsFor("zh"));
        Assert.True(KokoroFiles.UsesChineseFiles("zh"));
        Assert.False(KokoroFiles.UsesChineseFiles("en"));
        Assert.Equal(3, KokoroFiles.RuleFsts.Count);
    }

    [Fact]
    public void A_supertonic_pick_maps_into_the_voices_of_the_models_language()
    {
        var spanish = new VoiceModelPaths("/k", VoiceEngineKind.Kokoro, "es", KokoroVoiceTable.VoicesFor("es"));

        VoiceInfo woman = spanish.ResolveVoice("supertonic:F3");
        VoiceInfo man = spanish.ResolveVoice("supertonic:M4");

        Assert.Equal("es", woman.Language);
        Assert.True(KokoroVoiceTable.IsFemale(woman.Id));
        Assert.False(KokoroVoiceTable.IsFemale(man.Id));
    }

    [Theory]
    [InlineData("es")]
    [InlineData("pt")]
    [InlineData("zh")]
    public void With_a_storyteller_pack_that_lists_the_language_it_resolves_to_that_languages_kokoro_voices(string language)
    {
        InstalledVoicePack supertonic = Installed(PackKind.Voice, PackConstants.SupertonicPackId, "en,fr,es,pt", VoiceEngineKind.Supertonic);
        InstalledVoicePack kokoro = Installed(PackKind.Storyteller, PackConstants.StorytellerPackId, "en,es,pt,zh", VoiceEngineKind.Kokoro);

        VoiceModelPaths? model = InstalledVoiceCatalog.Resolve([supertonic, kokoro], language);

        Assert.Equal(VoiceEngineKind.Kokoro, model!.Kind);
        Assert.Equal(language, model.Language);
        Assert.Equal(KokoroVoiceTable.VoicesFor(language).Select(v => v.Id), model.AvailableVoices.Select(v => v.Id));
    }

    [Fact]
    public void An_older_storyteller_pack_that_lists_only_english_leaves_spanish_on_supertonic_and_french_always_does()
    {
        InstalledVoicePack supertonic = Installed(PackKind.Voice, PackConstants.SupertonicPackId, "en,fr,es", VoiceEngineKind.Supertonic);
        InstalledVoicePack oldKokoro = Installed(PackKind.Storyteller, PackConstants.StorytellerPackId, "en", VoiceEngineKind.Kokoro);
        InstalledVoicePack kokoro = Installed(PackKind.Storyteller, PackConstants.StorytellerPackId, "en,es,pt,zh", VoiceEngineKind.Kokoro);

        Assert.Equal(VoiceEngineKind.Supertonic, InstalledVoiceCatalog.Resolve([supertonic, oldKokoro], "es")!.Kind);
        Assert.Equal(VoiceEngineKind.Supertonic, InstalledVoiceCatalog.Resolve([supertonic, kokoro], "fr")!.Kind);
    }

    [Fact]
    public void The_catalog_lists_the_storyteller_pack_for_each_language_it_speaks()
    {
        Assert.All(new[] { "en", "es", "pt", "zh" }, language => Assert.Equal(Listed, StorytellerOffer.ListedFor([Listed], language)));
        Assert.Null(StorytellerOffer.ListedFor([Listed], "fr"));
    }

    [Fact]
    public void The_packs_workflow_ships_the_chinese_files_and_lists_the_four_languages()
    {
        string workflow = File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", "packs.yml"));

        Assert.Contains(KokoroFiles.LexiconZh, workflow);
        Assert.All(KokoroFiles.RuleFsts, fst => Assert.Contains(fst, workflow));
        Assert.Contains("\"$src/" + KokoroFiles.DictDir + "\"", workflow);
        Assert.Contains("KOKORO_LANGUAGES: 'en,es,pt,zh'", workflow);
        Assert.Contains("entry \"$KOKORO_ID\" \"$KOKORO_LANGUAGES\"", workflow);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReadTheStupidText.slnx")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
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
