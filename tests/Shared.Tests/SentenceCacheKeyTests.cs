using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Tests;

public class SentenceCacheKeyTests
{
    [Fact]
    public void Normalize_trims_and_collapses_whitespace()
    {
        Assert.Equal("Hello big world.", SentenceCacheKey.Normalize("  Hello \t big\n\n world.  "));
    }

    [Fact]
    public void Whitespace_differences_do_not_change_the_key()
    {
        string a = SentenceCacheTestData.Key("Hello   there.").Id;
        string b = SentenceCacheTestData.Key("  Hello there.\n").Id;

        Assert.Equal(a, b);
    }

    [Fact]
    public void The_id_is_stable_lowercase_sha256_hex()
    {
        string id = SentenceCacheTestData.Key().Id;

        Assert.Equal(64, id.Length);
        Assert.Matches("^[0-9a-f]{64}$", id);
        Assert.Equal(id, SentenceCacheTestData.Key().Id);
    }

    [Fact]
    public void The_text_hash_is_the_sha256_of_the_normalized_sentence_alone()
    {
        // SHA-256("abc"), independent of voice and language.
        const string abc = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

        Assert.Equal(abc, SentenceCacheTestData.Key(" abc ", voice: "M3", language: "fr").TextHash);
        Assert.Equal(abc[..8], SentenceCacheTestData.Key("abc").ShortHash);
    }

    [Fact]
    public void A_different_voice_never_shares_audio()
    {
        Assert.NotEqual(SentenceCacheTestData.Key(voice: "F1").Id, SentenceCacheTestData.Key(voice: "M1").Id);
    }

    [Fact]
    public void A_different_language_never_shares_audio()
    {
        Assert.NotEqual(SentenceCacheTestData.Key(language: "en").Id, SentenceCacheTestData.Key(language: "fr").Id);
    }

    [Fact]
    public void A_different_engine_never_shares_audio()
    {
        string supertonic = SentenceCacheTestData.Key(engine: VoiceEngineKind.Supertonic).Id;

        Assert.NotEqual(supertonic, SentenceCacheTestData.Key(engine: VoiceEngineKind.Vits).Id);
        Assert.NotEqual(supertonic, SentenceCacheTestData.Key(engine: VoiceEngineKind.Melo).Id);
    }

    [Fact]
    public void A_different_model_directory_never_shares_audio()
    {
        Assert.NotEqual(
            SentenceCacheTestData.Key(model: "/packs/a").Id, SentenceCacheTestData.Key(model: "/packs/b").Id);
    }

    [Fact]
    public void A_different_pack_version_never_shares_audio()
    {
        Assert.NotEqual(
            SentenceCacheTestData.Key(packVersion: "1.0.0").Id, SentenceCacheTestData.Key(packVersion: "1.1.0").Id);
    }

    [Fact]
    public void A_different_sentence_never_shares_audio()
    {
        Assert.NotEqual(SentenceCacheTestData.Key("One.").Id, SentenceCacheTestData.Key("Two.").Id);
    }

    [Fact]
    public void Fields_cannot_blur_into_each_other()
    {
        // "a" + "bc" must not collide with "ab" + "c" across the voice and language fields.
        Assert.NotEqual(
            SentenceCacheTestData.Key(voice: "a", language: "bc").Id,
            SentenceCacheTestData.Key(voice: "ab", language: "c").Id);
    }
}
