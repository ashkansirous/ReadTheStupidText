using System.Net;
using System.Text.Json;
using DinoReader.Core;

namespace DinoReader;

/// <summary>What the Worker answered about the subscription: its state (<see cref="SubscriptionAccess.WorkerStates"/>) and the end of the paid, trial or grace period.</summary>
public sealed record WorkerSubscription(string State, DateTimeOffset? ExpiresAt);

/// <summary>Where the app gets the subscription's real end date on iOS and Android (Slice 384): the Worker, which asks Apple or Google. A seam so the tests and the Debug fake store do not call the network.</summary>
public interface ISubscriptionEnd
{
    /// <summary>Asks about the subscription whose proof header is <paramref name="proofHeader"/>; null when there is no answer (offline, a refusal, an error). Never throws.</summary>
    Task<WorkerSubscription?> AskAsync(string proofHeader, CancellationToken cancel);
}

/// <summary>
/// <c>GET /api/stories/subscription</c> with the same <c>X-Purchase-Proof</c> header a paid book's files carry (Slice 384). A reply that is not 200 with a readable body is no answer: the caller keeps its record, it never
/// treats silence as an expired subscription. Runs in the background; the shelf never waits for it.
/// </summary>
public sealed class SubscriptionEndClient : ISubscriptionEnd
{
    private const string Path = "/api/stories/subscription";
    private const int MaxBodyBytes = 4096;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

    public async Task<WorkerSubscription?> AskAsync(string proofHeader, CancellationToken cancel)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        limit.CancelAfter(Timeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(StoreClient.BaseAddress), Path));
            request.Headers.TryAddWithoutValidation(PurchaseProofs.HeaderName, proofHeader);
            using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limit.Token);
            return response.StatusCode == HttpStatusCode.OK ? Parse(await ReadBodyAsync(response, limit.Token)) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or IOException)
        {
            // no-fallback: no answer keeps the held record; BookPurchases reports one store fallback when the device was online
            return null;
        }
    }

    private static async Task<byte[]> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancel)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancel);
        using var memory = new MemoryStream();
        var buffer = new byte[1024];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancel)) > 0)
        {
            memory.Write(buffer, 0, read);
            if (memory.Length > MaxBodyBytes)
            {
                throw new JsonException("The subscription answer is too long.");
            }
        }

        return memory.ToArray();
    }

    /// <summary>Reads <c>{ "state": "...", "expiresAt": "ISO time" | null }</c>; null for anything else.</summary>
    internal static WorkerSubscription? Parse(byte[] body)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("state", out JsonElement state) || state.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        DateTimeOffset? ends = root.TryGetProperty("expiresAt", out JsonElement at) && at.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(at.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out DateTimeOffset parsed) ? parsed : null;
        return new WorkerSubscription(state.GetString() ?? string.Empty, ends);
    }
}
