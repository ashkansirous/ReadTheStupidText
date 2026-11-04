namespace ReadTheStupidText.Application.Usage;

/// <summary>
/// Which dataset a build's usage counts belong to (Slice 251): a closed set, so a new case cannot silently reach production. The old yes/no
/// (<see cref="UsageEligibility.Check"/>) is unchanged and means <see cref="Production"/> or not; only an app that chooses to send <see cref="Beta"/> does.
/// </summary>
public enum UsageChannel
{
    /// <summary>Counts are not sent from here (debug, unstamped, emulator, robot, harness, non-store Windows, unreadable facts).</summary>
    None,

    /// <summary>A store install: counts go to the production dataset.</summary>
    Production,

    /// <summary>An iOS TestFlight (or App Review) build: counts go to the separate beta dataset, only for an app that sends them (Dino Reader).</summary>
    Beta,
}

/// <summary>Where a <see cref="UsageChannel"/> posts its batch: the production route is unchanged, beta has its own route, so production bodies are identical to old builds'.</summary>
public static class UsageChannels
{
    /// <summary>The route suffix of the Worker's beta endpoint (<c>/api/events/beta</c>); an old Worker has no such route and answers 404, so a beta batch cannot land in production.</summary>
    public const string BetaRouteSuffix = "/beta";

    /// <summary>
    /// The path a batch is posted to, or null when it is not posted (a dry run). One decision from one <see cref="UsageEligibility.ChannelOf"/> result, so the skip reason and the route cannot drift:
    /// <see cref="UsageChannel.Production"/> posts to <paramref name="eventsPath"/>; <see cref="UsageChannel.Beta"/> posts to the beta route only for an app that <paramref name="sendsBeta"/>; anything else posts nothing.
    /// </summary>
    public static string? PathFor(string eventsPath, UsageChannel channel, bool sendsBeta) => channel switch
    {
        UsageChannel.Production => eventsPath,
        UsageChannel.Beta when sendsBeta => eventsPath + BetaRouteSuffix,
        _ => null,
    };
}
