using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Tests;

/// <summary>Slice 144: mana is Farsi's default narrator and the literary voice leaves the roster.</summary>
public class FarsiDefaultVoiceTests
{
    private const string Mana = "piper:fa-mana";
    private const string GanjiAdabi = "piper:fa-ganji-adabi";

    [Fact]
    public void The_first_farsi_narrator_is_mana()
    {
        Assert.Equal(Mana, CharacterRoster.Narrators("fa")[0].VoiceId);
    }

    [Fact]
    public void No_farsi_roster_character_uses_the_literary_voice()
    {
        Assert.DoesNotContain(CharacterRoster.For("fa"), c => c.VoiceId == GanjiAdabi);
    }

    [Fact]
    public void The_three_farsi_narrators_have_three_voices()
    {
        Assert.Equal(3, CharacterRoster.Narrators("fa").Select(n => n.VoiceId).Distinct().Count());
    }

    [Fact]
    public void An_unpicked_character_on_the_literary_voice_is_a_retired_voice_in_farsi_only()
    {
        CastMember[] cast = [new("piglet", "Piglet", Gender.Male, GanjiAdabi)];

        Assert.True(CastEditor.UsesRetiredVoice(cast, "fa"));
        Assert.Empty(CastEditor.WithoutRetiredVoices(cast, "fa"));
    }

    [Fact]
    public void A_picked_character_on_the_literary_voice_is_not_retired()
    {
        CastMember[] cast = [new("piglet", "Piglet", Gender.Male, GanjiAdabi, Edited: true)];

        Assert.False(CastEditor.UsesRetiredVoice(cast, "fa"));
        Assert.Single(CastEditor.WithoutRetiredVoices(cast, "fa"));
    }

    [Fact]
    public void Young_farsi_animals_stay_young_for_the_animal_logic()
    {
        Assert.Equal(VoiceFlavour.BleatLamb, AnimalSounds.FlavourFor(AnimalKind.Sheep, VoiceFlavour.Chirpy));
        Assert.Equal(VoiceFlavour.Squeaky, AnimalSounds.FlavourFor(AnimalKind.Pig, VoiceFlavour.Perky));
    }

    [Fact]
    public void A_character_the_roster_plays_on_mana_does_not_clash_with_the_narrator()
    {
        CastMember[] cast = [new("mum", "Mum", Gender.Female, Mana, Flavour: VoiceFlavour.Normal), new("wolf", "Wolf", Gender.Male, "piper:fa-ganji")];

        Assert.False(CastEditor.ClashesWithNarrator(cast, Mana, "fa"));
    }

    [Fact]
    public void A_character_with_the_narrators_own_flavour_or_none_clashes_in_farsi()
    {
        Assert.True(CastEditor.ClashesWithNarrator([new("a", "A", Gender.Female, Mana, Flavour: VoiceFlavour.Storyteller)], Mana, "fa"));
        Assert.True(CastEditor.ClashesWithNarrator([new("a", "A", Gender.Female, Mana)], Mana, "fa"));
    }

    [Fact]
    public void In_english_any_character_on_the_narrators_voice_clashes()
    {
        Assert.True(CastEditor.ClashesWithNarrator([new("a", "A", Gender.Male, "supertonic:M1", Flavour: VoiceFlavour.Normal)], "supertonic:M1", "en"));
    }

    [Fact]
    public void The_literary_voice_is_retired_in_farsi_only()
    {
        CastMember[] cast = [new("a", "A", Gender.Male, GanjiAdabi)];

        Assert.False(CastEditor.UsesRetiredVoice(cast, "en"));
    }

    [Fact]
    public void A_recast_keeps_a_voice_the_grown_up_picked_unless_it_is_the_narrators()
    {
        CastMember[] before = [new("a", "A", Gender.Male, GanjiAdabi, Edited: true), new("b", "B", Gender.Female, Mana, Edited: true)];
        CastMember[] recast = [new("a", "A", Gender.Male, "piper:fa-gyro"), new("b", "B", Gender.Female, "piper:fa-ganji")];
        VoiceOption[] pool = [new(Mana, Gender.Unknown), new(GanjiAdabi, Gender.Unknown), new("piper:fa-ganji", Gender.Unknown), new("piper:fa-gyro", Gender.Unknown)];

        IReadOnlyList<CastMember> kept = CastEditor.KeepExplicitPicks(before, recast, pool, Mana);

        Assert.Equal(GanjiAdabi, kept[0].VoiceId);
        Assert.Equal("piper:fa-ganji", kept[1].VoiceId);
    }
}
