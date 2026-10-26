namespace ReadTheStupidText.Application.Usage;

/// <summary>
/// Where the app reports its anonymous usage counts (Slice 140, Decisions 143-145). Recording never throws and never blocks the caller:
/// counting must not change what the person's action does.
/// </summary>
public interface IUsageRecorder
{
    /// <summary>Whether counts may be sent at all (the control panel's "Help improve the app" switch); turning it off also forgets what was waiting.</summary>
    bool Enabled { get; set; }

    /// <summary>Queues the count <paramref name="make"/> builds (null for a count that does not apply), unless counting is off.</summary>
    void Record(Func<UsageContext, UsageEvent?> make);
}
