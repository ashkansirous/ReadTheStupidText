namespace ReadTheStupidText.Application.Usage;

/// <summary>Keeps what <see cref="UsageService"/> must remember between runs: the user's switch, the counts not sent yet and the last good remote config.</summary>
public interface IUsageStore
{
    /// <summary>The user's switch; on by default (Decision 143).</summary>
    bool Enabled { get; set; }

    /// <summary>The stored queue (see <see cref="UsageBatch.ParseQueue"/>), or null.</summary>
    string? LoadQueue();

    /// <summary>Keeps <paramref name="json"/> as the queue; null removes it.</summary>
    void SaveQueue(string? json);

    /// <summary>The last served config body, or null.</summary>
    string? LoadConfig();

    void SaveConfig(string json);
}

/// <summary>How a batch ended: <see cref="Retry"/> is worth trying again later, <see cref="Rejected"/> never is.</summary>
public enum UsageSendResult
{
    Sent,
    Rejected,
    Retry,
}

/// <summary>The two requests the counts make, both to the app's own host (<c>/api/app-config</c> and <c>/api/events</c>); nothing about the user is sent.</summary>
public interface IUsageTransport
{
    /// <summary>The body of <c>GET /api/app-config</c>. Throws when the host cannot be reached or answers an error.</summary>
    Task<string> FetchConfigAsync(CancellationToken cancellationToken);

    /// <summary>Posts a batch body to <c>/api/events</c>. Throws when the host cannot be reached.</summary>
    Task<UsageSendResult> PostEventsAsync(string json, CancellationToken cancellationToken);
}

/// <summary>What every count says about the build: the app version, the platform and the operating system's major version; <paramref name="Device"/> (Slice 166) is the coarse device description sent once per batch, or null to send none; <paramref name="NotCounted"/> (Slice 167) is why a test build or device only dry-runs its batches (they are logged, never posted).</summary>
public sealed record UsageBuild(string Version, string Platform, int OsMajor, DeviceContext? Device = null, UsageSkipReason NotCounted = UsageSkipReason.None);
