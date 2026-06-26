using ReadTheStupidText.Infrastructure.Settings;

namespace ReadTheStupidText.Tests;

// Decision 22: the single legacy IsEnabled toggle splits into AutoReadOnSelection
// + AutoReadOnCopy, both defaulting on. An existing profile with IsEnabled=false
// must migrate to both flags off; a stored new value always wins over the legacy one.
public class AutoReadFlagMigrationTests
{
    [Fact]
    public void Fresh_install_defaults_on()
    {
        Assert.True(LocalSettingsStore.ResolveAutoReadFlag(storedValue: null, legacyValue: null));
    }

    [Fact]
    public void Legacy_disabled_migrates_to_off()
    {
        Assert.False(LocalSettingsStore.ResolveAutoReadFlag(storedValue: null, legacyValue: false));
    }

    [Fact]
    public void Legacy_enabled_migrates_to_on()
    {
        Assert.True(LocalSettingsStore.ResolveAutoReadFlag(storedValue: null, legacyValue: true));
    }

    [Theory]
    [InlineData(true, false)]   // stored on, legacy off -> stored wins
    [InlineData(false, true)]   // stored off, legacy on -> stored wins
    public void Stored_value_takes_precedence_over_legacy(bool stored, bool legacy)
    {
        Assert.Equal(stored, LocalSettingsStore.ResolveAutoReadFlag(stored, legacy));
    }
}
