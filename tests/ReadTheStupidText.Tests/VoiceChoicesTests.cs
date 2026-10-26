using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Tests;

public class VoiceChoicesTests
{
    private static readonly VoiceModelPaths English = new("/packs/supertonic", VoiceEngineKind.Supertonic, "en", SupertonicVoiceTable.Voices);

    private static readonly VoiceModelPaths Persian = new(
        "/packs/piper-fa", VoiceEngineKind.Vits, "fa",
        [new VoiceInfo("piper:fa-ganji-adabi", "Ganji Adabi", "fa", VoiceEngineKind.Vits, 0), new VoiceInfo("piper:fa-b", "B", "fa", VoiceEngineKind.Vits, 1)]);

    [Theory]
    [InlineData("supertonic:M2", VoiceEngineKind.Supertonic)]
    [InlineData("piper:fa-ganji-adabi", VoiceEngineKind.Vits)]
    [InlineData("melo:zh", VoiceEngineKind.Melo)]
    public void Voice_id_prefix_names_its_engine_family(string id, VoiceEngineKind kind)
    {
        Assert.Equal(kind, VoiceChoices.KindOf(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("system")]
    public void Ids_of_no_family_have_no_kind(string? id)
    {
        Assert.Null(VoiceChoices.KindOf(id));
    }

    [Fact]
    public void A_piper_pick_does_not_displace_the_remembered_supertonic_voice()
    {
        const string latestPick = "piper:fa-ganji-adabi";
        const string rememberedSupertonic = "supertonic:M2";

        Assert.Equal("supertonic:M2", VoiceChoices.Resolve(English, null, latestPick, rememberedSupertonic).Id);
        Assert.Equal("piper:fa-ganji-adabi", VoiceChoices.Resolve(Persian, null, latestPick, null).Id);
    }

    [Fact]
    public void The_latest_pick_wins_when_it_belongs_to_the_model()
    {
        Assert.Equal("supertonic:F1", VoiceChoices.Resolve(English, null, "supertonic:F1", "supertonic:M2").Id);
    }

    [Fact]
    public void No_candidate_in_the_model_falls_back_to_the_models_default()
    {
        Assert.Equal(SupertonicVoiceTable.Default.Id, VoiceChoices.Resolve(English, "piper:fa-ganji-adabi", null).Id);
        Assert.Equal("piper:fa-ganji-adabi", VoiceChoices.Resolve(Persian, "supertonic:M2").Id);
    }

    [Fact]
    public void The_persian_pack_defaults_to_mana_whatever_its_listing_order()
    {
        var model = new VoiceModelPaths(
            "/packs/piper-fa", VoiceEngineKind.Vits, "fa",
            [new VoiceInfo("piper:fa-ganji-adabi", "Ganji Adabi", "fa", VoiceEngineKind.Vits, 0), new VoiceInfo("piper:fa-mana", "Mana", "fa", VoiceEngineKind.Vits, 0)]);

        Assert.Equal("piper:fa-mana", model.ResolveVoice(null).Id);
        Assert.Equal("piper:fa-ganji-adabi", VoiceChoices.Resolve(model, "piper:fa-ganji-adabi").Id);
    }

    [Fact]
    public void A_piper_pack_of_another_language_keeps_its_first_voice_as_default()
    {
        var model = new VoiceModelPaths(
            "/packs/piper-nb", VoiceEngineKind.Vits, "nb",
            [new VoiceInfo("piper:nb-talesyntese", "Talesyntese", "nb", VoiceEngineKind.Vits, 0), new VoiceInfo("piper:fa-mana", "Mana", "nb", VoiceEngineKind.Vits, 0)]);

        Assert.Equal("piper:nb-talesyntese", model.ResolveVoice(null).Id);
    }
}
