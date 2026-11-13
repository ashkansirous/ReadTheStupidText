using ReadTheStupidText.Mobile.Shared;

namespace ReadTheStupidText.Tests;

public class VoiceCancelHoldTests
{
    [Fact]
    public void A_hold_applies_to_its_language_only_and_ignores_case()
    {
        var hold = new VoiceCancelHold(new MemoryVoiceCancelStore());
        hold.Hold("fa");

        Assert.True(hold.IsHeld("FA"));
        Assert.False(hold.IsHeld("en"));
    }

    [Fact]
    public void Release_clears_the_hold()
    {
        var hold = new VoiceCancelHold(new MemoryVoiceCancelStore());
        hold.Hold("fa");
        hold.Release();

        Assert.False(hold.IsHeld("fa"));
    }

    [Fact]
    public void A_language_change_lifts_a_hold_for_another_language_and_keeps_one_for_the_same()
    {
        var hold = new VoiceCancelHold(new MemoryVoiceCancelStore());
        hold.Hold("fa");
        hold.ReleaseUnlessFor("fa");
        Assert.True(hold.IsHeld("fa"));

        hold.ReleaseUnlessFor("en");
        Assert.False(hold.IsHeld("fa"));
    }

    [Fact]
    public void The_hold_survives_a_restart_because_a_new_hold_reads_the_same_store()
    {
        var store = new MemoryVoiceCancelStore();
        new VoiceCancelHold(store).Hold("fa");

        Assert.True(new VoiceCancelHold(store).IsHeld("fa"));
    }

    [Fact]
    public void The_preferences_store_round_trips_and_clears()
    {
        var store = new PreferencesVoiceCancelStore();
        store.Write("fa");
        Assert.Equal("fa", store.Read());
        store.Write(null);
        Assert.Null(store.Read());
    }
}
