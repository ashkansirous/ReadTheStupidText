using System.Text;

namespace DinoReader;

/// <summary>
/// The subscription proof a linked Sonos home was last given (Slice 439, C4), kept on the device, and the call that swaps it at the Worker when the store gives a different one after a renewal or a change of plan
/// (<see cref="SonosProofRenewal"/> decides). Same on Android and iOS: <see cref="BookPurchases"/> reads each store's own proof after every store read (iOS StoreKit 2 transaction updates and Android Play Billing reads both
/// end there) and calls <see cref="RenewAsync"/>. The proof is a purchase token: it stays in the app's private Preferences and goes only to the Dino Worker, never to a log.
/// </summary>
internal static class SonosLinkedProof
{
    private const string PreferenceKey = "dino.sonos.linkedProof";
    private const string SettledKey = "dino.sonos.settledProof";
    private const string SettledAtKey = "dino.sonos.settledAt";
    private static readonly object Sync = new();
    private const string SonosEndpoint = StoreClient.BaseAddress + "/sonos";
    private const string JsonMime = "application/json";
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(20);
    private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>The proof the Worker holds for the linked home, or null when no home is linked with one.</summary>
    public static string? Linked => Preferences.Default.Get(PreferenceKey, string.Empty) is { Length: > 0 } proof ? proof : null;

    private static string? Settled => Preferences.Default.Get(SettledKey, string.Empty) is { Length: > 0 } proof ? proof : null;

    private static DateTimeOffset? SettledAt => Preferences.Default.Get(SettledAtKey, 0L) is > 0 and var ticks ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;

    private static bool Needed(string? linked, string current) => SonosProofRenewal.Needed(linked, current, Settled, SettledAt, DateTimeOffset.UtcNow);

    /// <summary>Notes the proof the Link screen just linked with (the Worker confirmed it), or forgets the old one when the home was linked without a proof, so the app's idea of the linked proof follows the link.</summary>
    public static void Remember(string? proof)
    {
        lock (Sync) // review 3, finding 5: one lock for every write of the linked proof, so a renew that finishes after a re-link cannot overwrite the new proof (see Apply)
        {
            ForgetSettled();
            if (proof is null)
            {
                Preferences.Default.Remove(PreferenceKey);
                return;
            }

            Preferences.Default.Set(PreferenceKey, proof);
        }
    }

    private static void ForgetSettled()
    {
        Preferences.Default.Remove(SettledKey);
        Preferences.Default.Remove(SettledAtKey);
    }

    /// <summary>Sends the Worker the swap when <paramref name="current"/> differs from the linked proof. Never throws; a failure keeps the old proof for the next store read.</summary>
    public static async Task RenewAsync(string current)
    {
        if (!Needed(Linked, current))
        {
            return;
        }

        await Gate.WaitAsync();
        try
        {
            if (Linked is { } linked && Needed(linked, current))
            {
                (int? status, string? body) = await PostAsync(SonosProofRenewal.Body(linked, current));
                SonosProofRenewal.Outcome outcome = SonosProofRenewal.OutcomeOf(status, body);
                Apply(outcome, linked, current);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    private static void Apply(SonosProofRenewal.Outcome outcome, string sentFrom, string current)
    {
        lock (Sync)
        {
            if (Linked != sentFrom)
            {
                PurchaseTrace.Line("sonos proof renew: dropped, the home was linked again meanwhile"); // review 3, finding 5: the answer is for a proof that is no longer the linked one
                return;
            }

            ApplyLocked(outcome, current);
        }

        PurchaseTrace.Line($"sonos proof renew: {outcome}");
#if DEBUG
        Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[SonosLink] proof renew {outcome}");
#endif
    }

    private static void ApplyLocked(SonosProofRenewal.Outcome outcome, string current)
    {
        if (SonosProofRenewal.Settled(outcome))
        {
            // review 3, findings 3, 4 and 7: a settled answer holds for this proof until it changes or SettledFor passes, then the renew is posted once more
            Preferences.Default.Set(SettledKey, current);
            Preferences.Default.Set(SettledAtKey, DateTimeOffset.UtcNow.UtcTicks);
        }
        else if (outcome == SonosProofRenewal.Outcome.Renewed)
        {
            Remember(current);
        }
        else if (outcome == SonosProofRenewal.Outcome.HomeGone)
        {
            Preferences.Default.Remove(PreferenceKey);
        }
    }

    private static async Task<(int? Status, string? Body)> PostAsync(string body)
    {
        using var timeout = new CancellationTokenSource(CallTimeout);
        try
        {
            using var content = new StringContent(body, Encoding.UTF8, JsonMime);
            using HttpResponseMessage response = await Http.PostAsync(SonosEndpoint, content, timeout.Token);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(timeout.Token));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // no-fallback: an unreachable Worker or a timeout means "try again after the next store read"; the old proof stays
            return (null, null);
        }
    }

    /// <summary>Runs <see cref="RenewAsync"/> as a background task that is observed: a failure that escapes it is traced by type name only (never the proof), so it is not lost as an unobserved task exception.</summary>
    public static async Task RenewObservedAsync(string current)
    {
        try
        {
            await RenewAsync(current);
        }
        catch (Exception ex)
        {
            // no-fallback: the renew is best effort and runs again after the next store read; the failure is traced, never thrown into the purchase read
            PurchaseTrace.Line($"sonos proof renew failed: {ex.GetType().Name}");
        }
    }
}
