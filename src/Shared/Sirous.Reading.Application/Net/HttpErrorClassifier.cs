using System.Text.Json;

namespace Sirous.Reading.Application.Net;

/// <summary>A classified failure: its kind and, for a 400 only, the server's own English sentence.</summary>
public sealed record HttpError(HttpErrorKind Kind, string? Message);

/// <summary>
/// Turns an HTTP status (or the lack of one) into an <see cref="HttpErrorKind"/> (Slice 357). Only a 400 carries a message
/// (<c>{"message": "..."}</c>); the body of every other status is ignored, the app supplies the text.
/// </summary>
public static class HttpErrorClassifier
{
    /// <summary>The longest 400 message shown; a longer one is cut.</summary>
    public const int MaxMessageLength = 300;

    private const int ClientErrorMin = 400;
    private const int ServerErrorMin = 500;
    private const string MessageProperty = "message";

    /// <summary>The kind for a status; null (no answer at all) is <see cref="HttpErrorKind.Offline"/>.</summary>
    public static HttpErrorKind Classify(int? status) => status switch
    {
        null => HttpErrorKind.Offline,
        400 => HttpErrorKind.BadRequest,
        401 => HttpErrorKind.Unauthorized,
        402 => HttpErrorKind.PaymentRequired,
        403 => HttpErrorKind.Forbidden,
        404 => HttpErrorKind.NotFound,
        429 => HttpErrorKind.TooManyRequests,
        >= ServerErrorMin => HttpErrorKind.ServerError,
        _ => HttpErrorKind.OtherClientError,
    };

    /// <summary>The kind plus, for a 400, the message read from the body (null when the body has none or is not JSON). A non-400 body is never read.</summary>
    public static HttpError Classify(int? status, ReadOnlyMemory<byte> body)
    {
        HttpErrorKind kind = Classify(status);
        return new HttpError(kind, kind == HttpErrorKind.BadRequest ? MessageFrom(body) : null);
    }

    /// <summary>
    /// The kind for an exception from an HTTP call: its status when it has one, offline for a connection failure or timeout, and null for
    /// anything that is not about reaching the server (a disk error, a bad file), which gets no server text.
    /// </summary>
    public static HttpErrorKind? Classify(Exception exception) => ClassifyStatus(exception) ?? ClassifyNoAnswer(exception);

    /// <summary>
    /// The kind of the HTTP status found anywhere in the exception chain (an AggregateException or a rethrown wrapper still carries it), or null
    /// when no exception in the chain has a status. The one chain walk both the app clients and the pack classifier share.
    /// </summary>
    public static HttpErrorKind? ClassifyStatus(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException { StatusCode: { } status })
            {
                return Classify((int)status);
            }
        }

        return null;
    }

    private static HttpErrorKind? ClassifyNoAnswer(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException or TimeoutException or OperationCanceledException
                or IOException { InnerException: System.Net.Sockets.SocketException })
            {
                return HttpErrorKind.Offline;
            }
        }

        return null;
    }

    /// <summary>The trimmed, length-capped <c>message</c> of a 400 body, or null when the body is empty, not JSON, is not exactly the Worker's <c>{"message": "..."}</c> shape, or has a blank message. Never throws.</summary>
    public static string? MessageFrom(ReadOnlyMemory<byte> body)
    {
        if (body.IsEmpty)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || document.RootElement.EnumerateObject().Count() != 1 // the Worker's own shape is exactly {"message": "..."}; a proxy's richer JSON is not ours to show
                || !document.RootElement.TryGetProperty(MessageProperty, out JsonElement message)
                || message.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return Shorten(message.GetString());
        }
        catch (JsonException)
        {
            // no-fallback: an unreadable 400 body only means there is no server sentence; the app shows its general text
            return null;
        }
    }

    private static string? Shorten(string? text)
    {
        string trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return null;
        }

        return trimmed.Length <= MaxMessageLength ? trimmed : trimmed[..MaxMessageLength].TrimEnd();
    }
}
