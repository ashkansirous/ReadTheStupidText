using DinoReader.Core;

namespace DinoReader.Tests;

public class VoiceCasterTests
{
    private static VoiceOption M(int n) => new($"Male{n}", Gender.Male);

    private static VoiceOption F(int n) => new($"Female{n}", Gender.Female);

    private static Character C(string id, Gender g, int words) => new(id, id, g, words);

    private static readonly VoiceOption[] Pack =
        [M(1), M(2), M(3), M(4), M(5), F(1), F(2), F(3), F(4), F(5)];

    [Fact]
    public void Peter_and_Cotton_Tail_get_gender_matched_voices_with_the_narrator_excluded()
    {
        var characters = new[] { C("Peter", Gender.Male, 10), C("Cotton Tail", Gender.Female, 5) };

        var cast = new VoiceCaster().Cast(characters, Pack, "Male2");

        Assert.Equal("Male1", cast.VoiceFor("Peter"));
        Assert.Equal("Female1", cast.VoiceFor("Cotton Tail"));
        Assert.All(cast.Assignments, a => Assert.False(a.IsShared));
    }

    [Fact]
    public void Narrator_voice_is_never_handed_out()
    {
        var characters = Enumerable.Range(1, 12).Select(i => C($"C{i}", Gender.Male, i)).ToList();

        var cast = new VoiceCaster().Cast(characters, Pack, "Male1");

        Assert.DoesNotContain(cast.Assignments, a => a.VoiceId == "Male1");
    }

    [Fact]
    public void Unknown_gender_takes_the_first_free_voice()
    {
        var cast = new VoiceCaster().Cast([C("Blob", Gender.Unknown, 3)], Pack, "Female1");

        Assert.Equal("Male1", cast.VoiceFor("Blob"));
    }

    [Fact]
    public void No_two_characters_share_while_enough_voices_exist()
    {
        var characters = Enumerable.Range(1, 9).Select(i => C($"C{i}", Gender.Male, i)).ToList();

        var cast = new VoiceCaster().Cast(characters, Pack, "Male1");

        Assert.Equal(9, cast.Assignments.Select(a => a.VoiceId).Distinct().Count());
    }

    [Fact]
    public void Overflow_uses_fewest_words_voice_and_never_reuses_main_voices()
    {
        var voices = new[] { M(1), M(2), M(3), M(4), M(5) };
        var characters = new[]
        {
            C("A", Gender.Male, 100), C("B", Gender.Male, 90), C("D", Gender.Male, 20),
            C("E", Gender.Male, 10), C("F", Gender.Male, 5), C("G", Gender.Male, 7),
        };

        var cast = new VoiceCaster().Cast(characters, voices, "Male5");

        Assert.Equal("Male1", cast.VoiceFor("A"));
        Assert.Equal("Male2", cast.VoiceFor("B"));
        Assert.Equal("Male3", cast.VoiceFor("D"));
        Assert.Equal("Male4", cast.VoiceFor("E"));
        Assert.Equal("Male4", cast.VoiceFor("F"));   // Male4 (10 words) < Male3 (20)
        Assert.Equal("Male4", cast.VoiceFor("G"));   // Male4 (15 words) < Male3 (20)
        Assert.All(
            cast.Assignments.Where(a => a.CharacterId is not ("A" or "B")),
            a => Assert.DoesNotContain(a.VoiceId, new[] { "Male1", "Male2" }));
        Assert.True(cast.Assignments.Single(a => a.CharacterId == "F").IsShared);
        Assert.False(cast.Assignments.Single(a => a.CharacterId == "A").IsShared);
    }

    [Fact]
    public void Overflow_prefers_a_gender_matched_voice()
    {
        var voices = new[] { M(1), F(1), M(2), F(2) };
        var characters = new[]
        {
            C("M1", Gender.Male, 50), C("F1", Gender.Female, 40),
            C("M2", Gender.Male, 9), C("F2", Gender.Female, 8),
            C("F3", Gender.Female, 3),
        };

        var cast = new VoiceCaster(mainCharacterCount: 2).Cast(characters, voices, "none");

        Assert.Equal("Female2", cast.VoiceFor("F3"));   // Female1 is main-owned, Female2 is not
    }

    [Fact]
    public void Few_voice_pack_lets_mains_share()
    {
        var voices = new[] { new VoiceOption("Only1", Gender.Male), new VoiceOption("Only2", Gender.Female) };
        var characters = new[]
        {
            C("A", Gender.Male, 30), C("B", Gender.Female, 20), C("C", Gender.Male, 10),
        };

        var cast = new VoiceCaster().Cast(characters, voices, "narrator");

        Assert.Equal("Only1", cast.VoiceFor("A"));
        Assert.Equal("Only2", cast.VoiceFor("B"));
        Assert.Equal("Only1", cast.VoiceFor("C"));
        Assert.True(cast.Assignments.Single(a => a.CharacterId == "C").IsShared);
        Assert.True(cast.Assignments.Single(a => a.CharacterId == "A").IsShared);
    }

    [Fact]
    public void Narrator_exclusion_holds_even_with_one_voice_left()
    {
        var voices = new[] { new VoiceOption("N", Gender.Male), new VoiceOption("V", Gender.Male) };
        var characters = new[] { C("A", Gender.Male, 3), C("B", Gender.Male, 2), C("C", Gender.Male, 1) };

        var cast = new VoiceCaster().Cast(characters, voices, "N");

        Assert.All(cast.Assignments, a => Assert.Equal("V", a.VoiceId));
    }

    [Fact]
    public void Casting_is_deterministic()
    {
        var characters = Enumerable.Range(1, 14)
            .Select(i => C($"C{i}", i % 3 == 0 ? Gender.Female : Gender.Male, 20 - i)).ToList();

        var first = new VoiceCaster().Cast(characters, Pack, "Male3");
        var second = new VoiceCaster().Cast(characters, Pack, "Male3");

        Assert.Equal(first.Assignments, second.Assignments);
    }

    [Fact]
    public void No_voices_gives_an_empty_cast()
    {
        var cast = new VoiceCaster().Cast([C("A", Gender.Male, 1)], [], "N");

        Assert.Empty(cast.Assignments);
    }

    [Fact]
    public void Mains_that_appear_after_minors_still_get_unshared_voices()
    {
        var voices = new[] { M(0), M(1), M(2), M(3), M(4) };
        var characters = new[]
        {
            C("A", Gender.Male, 1), C("B", Gender.Male, 1), C("C", Gender.Male, 1),
            C("D", Gender.Male, 1), C("E", Gender.Male, 100), C("F", Gender.Male, 90),
        };

        var cast = new VoiceCaster().Cast(characters, voices, "Male0");

        Assert.NotEqual(cast.VoiceFor("E"), cast.VoiceFor("F"));
        Assert.All(cast.Assignments.Where(a => a.CharacterId is "E" or "F"), a => Assert.False(a.IsShared));
        Assert.Equal(characters.Select(c => c.Id), cast.Assignments.Select(a => a.CharacterId));
    }

    [Fact]
    public void Duplicate_voice_ids_are_deduplicated_first_wins()
    {
        var voices = new[] { new VoiceOption("M1", Gender.Male), new VoiceOption("M1", Gender.Female), M(2) };
        var characters = new[] { C("A", Gender.Male, 5), C("B", Gender.Male, 4) };

        var cast = new VoiceCaster().Cast(characters, voices, "none");

        Assert.Equal("M1", cast.VoiceFor("A"));
        Assert.Equal("Male2", cast.VoiceFor("B"));
    }
}
