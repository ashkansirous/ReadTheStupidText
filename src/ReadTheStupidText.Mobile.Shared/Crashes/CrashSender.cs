using System.Diagnostics;
using System.Net;
using System.Text;
using ReadTheStupidText.Application.Crashes;

namespace ReadTheStupidText.Mobile.Shared.Crashes;

/// <summary>Where and when a <see cref="CrashSender"/> posts: the app's own host and the remote config's <c>crashReports.enabled</c> (Slice 227).</summary>
/// <param name="BaseAddress">The app's own host; no redirect is ever followed.</param>
/// <param name="RemoteEnabled">True while the remote config switches crash reports on (default off).</param>
/// <param name="RemoteKnown">True once a real config backs <paramref name="RemoteEnabled"/>; false while it is only the default. Either way the pending reports are kept (a stale or default "off" never deletes them).</param>
/// <param name="RefreshRemote">Fetches the remote config (the app's own client; it joins a fetch already running).</param>
public sealed record CrashSendOptions(string BaseAddress, Func<bool> RemoteEnabled, Func<bool> RemoteKnown, Func<Task> RefreshRemote);

/// <summary>
/// Posts the pending crash reports to <c>POST /api/crash</c> on the app's host (Slice 227), off the UI thread and a little after the first screen. A report is
/// deleted only after the Worker answers 2xx or says the payload itself is bad (400, 413, 422: <see cref="CrashSendPolicy"/>); it is kept on a network failure, a 404 or 405
/// (a Worker not deployed yet), 408, 429, 5xx and while the remote config says off or is not known (the store keeps at most ten, the oldest dropped first). The person's
/// switch, read again before every report, is the one thing that deletes pending reports. A report written by the crash handler has no log tail; the previous run's
/// tail is attached here to the newest pending managed report only (it describes the last run, not an older one). The request is a plain POST: no cookies, no redirects, nothing but the report. Never throws.
/// </summary>
/// <param name="discardAll">Deletes everything crash reporting keeps on the phone (pending reports, the rolling log, the run marker): the person's switch is off.</param>
/// <param name="previousRunTail">The end of the previous run's rolling log, read at start-up before this run logged anything.</param>
internal sealed class CrashSender(
    PendingCrashStore store, CrashSendOptions options, Func<bool> switchOn, Func<bool> mayReport, Action discardAll, Func<Task<string>> previousRunTail, Action<string> note)
{
    private const string CrashPath = "/api/crash";
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    // Only the app's own host, no redirects, no cookies (the same approach as UsageQueueSender).
    private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };

#if DEBUG
    // The scripted check posts plain http to a local stub; Android's cleartext policy is enforced by its Java HTTP stack, which a managed SocketsHttpHandler does not use.
    private static readonly HttpClient StubHttp = new(new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
#endif

    private readonly SemaphoreSlim _sending = new(1, 1);

    public async Task RunAfterStartAsync()
    {
#if DEBUG
        TimeSpan delay = CrashDebug.StubAddress is null ? StartDelay : TimeSpan.FromSeconds(1);
#else
        TimeSpan delay = StartDelay;
#endif
        await Task.Delay(delay).ConfigureAwait(false);
        await SendAsync().ConfigureAwait(false);
    }

    public async Task SendAsync()
    {
        if (!await _sending.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await SendPendingAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"crash reports could not be sent: {ex.Message}");
        }
        finally
        {
            _sending.Release();
        }
    }

    private async Task SendPendingAsync()
    {
        if (!switchOn())
        {
            discardAll();
            note("switch off, pending reports and log dropped");
            return;
        }

        if (store.List().Count == 0 || !mayReport())
        {
            return;
        }

        string address = await ReadyAddressAsync().ConfigureAwait(false);
        if (address.Length == 0)
        {
            return;
        }

        string? tailOwner = store.NewestManagedPath();
        string tail = tailOwner is null ? string.Empty : await previousRunTail().ConfigureAwait(false);
        foreach (string path in store.List())
        {
            if (!switchOn())
            {
                discardAll(); // turned off while sending: the rest is deleted, not sent
                note("switch off, pending reports and log dropped");
                return;
            }

            if (!await SendOneAsync(path, address, path == tailOwner ? tail : string.Empty).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    // The base address to post to, or empty when nothing is to be sent now (config unknown or off: pending kept, capped at ten by the store).
    private async Task<string> ReadyAddressAsync()
    {
#if DEBUG
        if (CrashDebug.StubAddress is { } stub)
        {
            return stub;
        }
#endif
        await options.RefreshRemote().ConfigureAwait(false);
        if (!options.RemoteKnown())
        {
            note("remote config unknown, reports kept");
            return string.Empty;
        }

        if (!options.RemoteEnabled())
        {
            note("remote config off, reports kept");
            return string.Empty;
        }

        return options.BaseAddress;
    }

    // True to go on with the next report; false to stop (a retry-worthy failure).
    private async Task<bool> SendOneAsync(string path, string address, string tail)
    {
        string? body = store.ReadBody(path, tail);
        if (body is null)
        {
            store.Delete(path);
            note("damaged report dropped");
            return true;
        }

        HttpStatusCode? status = await PostAsync(body, address).ConfigureAwait(false);
        int? code = status is null ? null : (int)status.Value;
        if (!CrashSendPolicy.DeleteAfter(code))
        {
            note($"report kept (status {(code is null ? "none" : code)})");
            return false;
        }

        store.Delete(path);
        note($"report sent (status {code})");
        return true;
    }

    private static HttpClient ClientFor()
    {
#if DEBUG
        if (CrashDebug.StubAddress is not null)
        {
            return StubHttp;
        }
#endif
        return Http;
    }

    private static async Task<HttpStatusCode?> PostAsync(string body, string address)
    {
        try
        {
            using var timeout = new CancellationTokenSource(RequestTimeout);
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await ClientFor().PostAsync(new Uri(new Uri(address), CrashPath), content, timeout.Token).ConfigureAwait(false);
            return response.StatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return null;
        }
    }
}
