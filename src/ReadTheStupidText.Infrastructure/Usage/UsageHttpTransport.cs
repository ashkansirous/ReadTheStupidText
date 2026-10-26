using System.Net;
using System.Text;
using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText.Infrastructure.Usage;

/// <summary>
/// The two requests of the usage counts (Slice 140), both to Read The Stupid Text's own host, as the phone does (Slice 137): a plain GET of
/// <c>/api/app-config</c> and a POST of a batch to <c>/api/events</c>. No cookies, no redirect is ever followed, nothing about the person
/// is sent.
/// </summary>
public sealed class UsageHttpTransport : IUsageTransport
{
    public const string BaseAddress = "https://readthestupidtext.sirous.uk";

    private const string ConfigPath = "/api/app-config";
    private const string EventsPath = "/api/events";
    private const string JsonType = "application/json";
    private const int MaxConfigBytes = 64 * 1024;

    private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    private readonly Uri _baseAddress;

    public UsageHttpTransport(string baseAddress = BaseAddress) => _baseAddress = new Uri(baseAddress);

    public async Task<string> FetchConfigAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await Http.GetAsync(new Uri(_baseAddress, ConfigPath), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxConfigBytes)
        {
            throw new IOException("The config is larger than allowed.");
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return body.Length > MaxConfigBytes ? throw new IOException("The config is larger than allowed.") : body;
    }

    public async Task<UsageSendResult> PostEventsAsync(string json, CancellationToken cancellationToken)
    {
        using var body = new StringContent(json, Encoding.UTF8, JsonType);
        using HttpResponseMessage response = await Http.PostAsync(new Uri(_baseAddress, EventsPath), body, cancellationToken).ConfigureAwait(false);
        return Classify(response.StatusCode);
    }

    // 2xx is delivered; 400, 404 and 413 (a body or a route that could never succeed) are final; every other status (a captive portal's 302, a proxy's 407, 408, 429, a 5xx such as the Worker's 503 while counting is not set up) is worth trying again.
    internal static UsageSendResult Classify(HttpStatusCode status)
    {
        int code = (int)status;
        if (code is >= 200 and < 300)
        {
            return UsageSendResult.Sent;
        }

        return code is 400 or 404 or 413 ? UsageSendResult.Rejected : UsageSendResult.Retry;
    }
}
