using DinoReader.Core;

namespace DinoReader.Tests;

public class TranslationPackSeedTests
{
    private static IReadOnlyList<string> Seed(params string[] languages) =>
        TranslationPackSeed.Languages(languages, OwnBookTranslation.PackLanguages);

    [Fact]
    public void A_fresh_install_is_never_seeded_and_an_upgrade_is_once()
    {
        Assert.False(TranslationPackSeed.ShouldSeed(freshInstall: true, alreadySeeded: false, reconciled: false));
        Assert.True(TranslationPackSeed.ShouldSeed(freshInstall: false, alreadySeeded: false, reconciled: false));
        Assert.False(TranslationPackSeed.ShouldSeed(freshInstall: false, alreadySeeded: true, reconciled: false));
        Assert.False(TranslationPackSeed.ShouldSeed(freshInstall: false, alreadySeeded: false, reconciled: true));
    }

    [Fact]
    public void Each_picked_language_seeds_its_pack_and_english()
    {
        Assert.Equal(["en", "fa", "de"], Seed("fa", "de"));
    }

    [Fact]
    public void English_alone_seeds_nothing()
    {
        Assert.Empty(Seed("en"));
    }

    [Fact]
    public void English_among_others_adds_nothing_of_its_own()
    {
        Assert.Equal(["en", "fa"], Seed("en", "fa"));
    }
}
