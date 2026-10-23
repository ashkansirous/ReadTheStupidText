using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Tests;

public class VoiceSelectionTests
{
    private static readonly string[] Payload = ["model.onnx"];

    private static InstalledVoicePack Pack(string id, string languages, VoiceEngineKind model, params PackVoice[] voices) =>
        new($"/packs/{id}", new PackManifest(id, PackKind.Voice, languages, "1", "sherpa-onnx", Payload, model, voices));

    private static InstalledVoicePack Supertonic() =>
        Pack(PackConstants.SupertonicPackId, "en,fr,es,pt,ru,ja,ar", VoiceEngineKind.Supertonic);

    private static InstalledVoicePack Piper() =>
        Pack(PackConstants.PersianPackId, "fa", VoiceEngineKind.Vits, new PackVoice("piper:fa-ganji-adabi", "Ganji Adabi", 0));

    private static InstalledVoicePack Melo() =>
        Pack(PackConstants.ChinesePackId, "zh", VoiceEngineKind.Melo, new PackVoice("melo:zh", "Mei", 0));

    [Theory]
    [InlineData("en", VoiceEngineKind.Supertonic, PackConstants.SupertonicPackId)]
    [InlineData("ar", VoiceEngineKind.Supertonic, PackConstants.SupertonicPackId)]
    [InlineData("fa", VoiceEngineKind.Vits, PackConstants.PersianPackId)]
    [InlineData("zh", VoiceEngineKind.Melo, PackConstants.ChinesePackId)]
    public void Language_resolves_to_the_engine_of_the_installed_pack_that_serves_it(string language, VoiceEngineKind kind, string packId)
    {
        VoiceModelPaths? paths = InstalledVoiceCatalog.Resolve([Supertonic(), Piper(), Melo()], language);

        Assert.NotNull(paths);
        Assert.Equal(kind, paths.Kind);
        Assert.Equal($"/packs/{packId}", paths.RootDir);
        Assert.Equal(language, paths.Language);
    }

    [Theory]
    [InlineData("fa")]
    [InlineData("zh")]
    [InlineData("de")]
    public void Language_with_no_installed_pack_resolves_to_nothing_so_the_phone_voice_reads(string language)
    {
        Assert.Null(InstalledVoiceCatalog.Resolve([Supertonic()], language));
    }

    [Fact]
    public void Resolved_model_carries_the_installed_pack_version()
    {
        Assert.Equal("1", InstalledVoiceCatalog.Resolve([Supertonic()], "en")!.PackVersion);
    }

    [Fact]
    public void Language_codes_match_case_insensitively()
    {
        Assert.NotNull(InstalledVoiceCatalog.Resolve([Piper()], "FA"));
    }

    [Fact]
    public void Pack_serving_fewer_languages_wins()
    {
        InstalledVoicePack dedicated = Pack("supertonic-fr", "fr", VoiceEngineKind.Supertonic);

        VoiceModelPaths? paths = InstalledVoiceCatalog.Resolve([Supertonic(), dedicated], "fr");

        Assert.Equal("/packs/supertonic-fr", paths!.RootDir);
    }

    [Fact]
    public void Non_supertonic_pack_without_voices_is_unusable()
    {
        InstalledVoicePack empty = Pack(PackConstants.PersianPackId, "fa", VoiceEngineKind.Vits);

        Assert.Null(InstalledVoiceCatalog.Resolve([empty], "fa"));
    }

    [Fact]
    public void Ocr_packs_never_serve_a_voice()
    {
        var ocr = new InstalledVoicePack("/packs/ocr-fa", new PackManifest("ocr-fa", PackKind.Ocr, "fa", "1", "paddle", Payload));

        Assert.Null(InstalledVoiceCatalog.Resolve([ocr], "fa"));
    }

    [Fact]
    public void Supertonic_pack_without_a_voice_list_offers_the_ten_styles_in_the_requested_language()
    {
        VoiceModelPaths paths = InstalledVoiceCatalog.Resolve([Supertonic()], "fr")!;

        Assert.Equal(10, paths.AvailableVoices.Count);
        Assert.All(paths.AvailableVoices, v => Assert.Equal("fr", v.Language));
        Assert.Equal("supertonic:M1", paths.ResolveVoice("supertonic:M1").Id);
    }

    [Fact]
    public void Pack_voices_carry_their_engine_language_and_speaker_id()
    {
        VoiceModelPaths paths = InstalledVoiceCatalog.Resolve([Piper()], "fa")!;

        VoiceInfo voice = Assert.Single(paths.AvailableVoices);
        Assert.Equal(new VoiceInfo("piper:fa-ganji-adabi", "Ganji Adabi", "fa", VoiceEngineKind.Vits, 0), voice);
    }

    [Fact]
    public void Stored_supertonic_voice_falls_back_to_the_pack_voice_for_another_engine()
    {
        VoiceModelPaths paths = InstalledVoiceCatalog.Resolve([Piper()], "fa")!;

        Assert.Equal("piper:fa-ganji-adabi", paths.ResolveVoice("supertonic:F3").Id);
        Assert.Equal(0, paths.ResolveVoice("supertonic:F3").SpeakerId);
    }

    [Fact]
    public void Unknown_voice_on_supertonic_resolves_to_the_default_male_style()
    {
        VoiceModelPaths paths = new("/m");

        Assert.Equal(SupertonicVoiceTable.Default.Id, paths.ResolveVoice("piper:fa-ganji-adabi").Id);
        Assert.Equal(SupertonicVoiceTable.DefaultSpeakerId, paths.ResolveVoice(null).SpeakerId);
    }

    [Theory]
    [InlineData("supertonic:F1", 0)]
    [InlineData("supertonic:M5", 9)]
    public void Supertonic_voices_keep_their_sid_whatever_the_language(string id, int sid)
    {
        VoiceInfo voice = SupertonicVoiceTable.VoicesFor("ja").Single(v => v.Id == id);

        Assert.Equal(sid, voice.SpeakerId);
        Assert.Equal(sid, SupertonicVoiceTable.SpeakerIdFor(id));
    }

    [Fact]
    public void Legacy_manifest_without_model_or_voices_is_a_supertonic_pack()
    {
        const string json = """
            { "id": "supertonic-3", "kind": "voice", "language": "en", "version": "1",
              "engine": "sherpa-onnx", "files": ["voice.bin"] }
            """;

        PackManifest manifest = PackManifest.Parse(json);

        Assert.Equal(VoiceEngineKind.Supertonic, manifest.Model);
        Assert.Empty(manifest.Voices!);
    }

    [Fact]
    public void Manifest_parses_model_and_voices()
    {
        const string json = """
            { "id": "piper-fa", "kind": "voice", "language": "fa", "version": "1", "engine": "sherpa-onnx",
              "files": ["model.onnx"], "model": "vits",
              "voices": [ { "id": "piper:fa-ganji-adabi", "name": "Ganji Adabi", "sid": 0 } ] }
            """;

        PackManifest manifest = PackManifest.Parse(json);

        Assert.Equal(VoiceEngineKind.Vits, manifest.Model);
        Assert.Equal(new PackVoice("piper:fa-ganji-adabi", "Ganji Adabi", 0), Assert.Single(manifest.Voices!));
    }

    [Fact]
    public void Manifest_parses_a_voice_model_folder_and_the_catalog_aims_the_engine_at_it()
    {
        const string json = """
            { "id": "piper-fa", "kind": "voice", "language": "fa", "version": "2", "engine": "sherpa-onnx",
              "files": ["voices/mana/model.onnx", "voices/mana/tokens.txt", "voices/ganji_adabi/model.onnx", "voices/ganji_adabi/tokens.txt"], "model": "vits",
              "voices": [ { "id": "piper:fa-ganji-adabi", "name": "Ganji Adabi", "sid": 0, "model": "voices/ganji_adabi" },
                          { "id": "piper:fa-mana", "name": "Mana", "sid": 0, "model": "voices/mana" } ] }
            """;
        PackManifest manifest = PackManifest.Parse(json);
        var installed = new InstalledVoicePack("/packs/piper-fa", manifest);

        VoiceModelPaths paths = InstalledVoiceCatalog.Resolve([installed], "fa")!;
        VoiceInfo mana = paths.AvailableVoices.Single(v => v.Id == "piper:fa-mana");

        Assert.Equal("voices/ganji_adabi", paths.ModelDir);
        Assert.Equal(Path.Combine("/packs/piper-fa", "voices/mana"), paths.ForVoice(mana).ModelFolder);
        Assert.Equal("/packs/piper-fa", paths.ForVoice(mana).RootDir);
        Assert.Same(paths, paths.ForVoice(paths.ResolveVoice(null)));
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("voices/../../evil")]
    [InlineData("/abs")]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("voices//x")]
    public void Manifest_rejects_a_voice_model_folder_outside_the_pack(string folder)
    {
        string json = $$"""
            { "id": "a", "kind": "voice", "language": "fa", "version": "1", "engine": "e", "files": [],
              "voices": [ { "id": "x", "name": "X", "model": "{{folder}}" } ] }
            """;

        Assert.Throws<FormatException>(() => PackManifest.Parse(json));
    }

    [Theory]
    [InlineData("""["voices/gnaji/model.onnx"]""")]
    [InlineData("""["voices/ganji/model.onnx"]""")]
    [InlineData("""["voices/ganji/tokens.txt"]""")]
    public void Manifest_rejects_a_voice_model_folder_missing_its_model_or_tokens(string files)
    {
        string json = $$"""
            { "id": "a", "kind": "voice", "language": "fa", "version": "1", "engine": "e", "files": {{files}},
              "voices": [ { "id": "x", "name": "X", "model": "voices/ganji" } ] }
            """;

        Assert.Throws<FormatException>(() => PackManifest.Parse(json));
    }

    [Fact]
    public void A_saved_amir_choice_resolves_to_the_packs_first_voice()
    {
        VoiceModelPaths paths = InstalledVoiceCatalog.Resolve([Piper()], "fa")!;

        Assert.Equal("piper:fa-ganji-adabi", VoiceChoices.Resolve(paths, "piper:fa-amir", null).Id);
    }

    [Theory]
    [InlineData("""{ "id": "a", "kind": "voice", "language": "fa", "version": "1", "engine": "e", "files": [], "model": "bogus" }""")]
    [InlineData("""{ "id": "a", "kind": "voice", "language": "fa", "version": "1", "engine": "e", "files": [], "voices": [ { "id": "x" } ] }""")]
    public void Manifest_with_an_unknown_model_or_a_nameless_voice_is_rejected(string json)
    {
        Assert.Throws<FormatException>(() => PackManifest.Parse(json));
    }
}
