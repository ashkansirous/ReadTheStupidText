using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json;

namespace Sirous.Reading.Application.Usage;

/// <summary>Slice 460: why a store call failed, the closed list the fallback detail carries (never error text or an HRESULT string). <see cref="Other"/> is the old <c>store_failed</c>.</summary>
public enum StoreCause
{
    Timeout,
    NoNetwork,
    Dns,
    Tls,
    Http5xx,
    Http4xx,
    NotAssociated,
    NoWindow,
    SettleTimeout,
    Other,
}

/// <summary>Slice 460: names a failed store call's cause from the exception, and gives the fallback reason for each cause. Pure, so the mapping is unit-tested.</summary>
public static class StoreCauses
{
    /// <summary>The HRESULT the Microsoft Store gives an app that is not associated with a Store listing (ERROR_INSTALL_PACKAGE_NOT_ASSOCIATED).</summary>
    public const int NotAssociatedHResult = unchecked((int)0x803F6107);

    private const int ServerErrorFrom = 500;
    private const int ClientErrorFrom = 400;
    private const int ErrorTo = 600;
    private const int RequestTimeout = 408;
    private const int TooManyRequests = 429;

    /// <summary>The <see cref="FallbackReason"/> of <paramref name="cause"/>; <see cref="StoreCause.Other"/> is <see cref="FallbackReason.StoreFailed"/>.</summary>
    public static string Reason(StoreCause cause) => cause switch
    {
        StoreCause.Timeout => FallbackReason.StoreTimeout,
        StoreCause.NoNetwork => FallbackReason.StoreNoNetwork,
        StoreCause.Dns => FallbackReason.StoreDns,
        StoreCause.Tls => FallbackReason.StoreTls,
        StoreCause.Http5xx => FallbackReason.StoreHttp5xx,
        StoreCause.Http4xx => FallbackReason.StoreHttp4xx,
        StoreCause.NotAssociated => FallbackReason.StoreNotAssociated,
        StoreCause.NoWindow => FallbackReason.StoreNoWindow,
        StoreCause.SettleTimeout => FallbackReason.StoreSettleTimeout,
        _ => FallbackReason.StoreFailed,
    };

    /// <summary>The cause of a failed call to the app's own server, read from the exception and its inner exceptions.</summary>
    public static StoreCause Of(Exception ex)
    {
        if (ex is OperationCanceledException or TimeoutException)
        {
            return StoreCause.Timeout;
        }

        if (HasJsonException(ex))
        {
            return StoreCause.Other; // the server answered with a body that is not the listing, even when a network type wraps the error
        }

        if (ex is HttpRequestException { StatusCode: { } status })
        {
            return OfStatus(status);
        }

        if (ex is HttpRequestException http && OfRequestError(http.HttpRequestError) is { } fromError)
        {
            return fromError;
        }

        return ex.InnerException is { } inner ? Of(inner, ex) : OfSelf(ex);
    }

    /// <summary>The cause of a Windows Store call that threw a COM error with this HRESULT.</summary>
    public static StoreCause OfStoreHResult(int hresult) => hresult == NotAssociatedHResult ? StoreCause.NotAssociated : StoreCause.Other;

    /// <summary>True when the exception or one inside it is a <see cref="JsonException"/>: the server answered with a body that is not the listing, whatever network type wraps it. Never retried.</summary>
    public static bool HasJsonException(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is JsonException)
            {
                return true;
            }
        }

        return false;
    }

    private static StoreCause Of(Exception inner, Exception outer)
    {
        StoreCause cause = Of(inner);
        return cause == StoreCause.Other ? OfSelf(outer) : cause;
    }

    private static StoreCause OfSelf(Exception ex) => ex switch
    {
        AuthenticationException => StoreCause.Tls,
        SocketException socket => OfSocket(socket.SocketErrorCode),
        _ => NativeNetworkErrors.Of(ex) ?? StoreCause.Other, // Android's Java and iOS's Foundation network errors (Slice 460)
    };

    /// <summary>True for a cause that can pass by itself, so the automatic retry waits for it: a timeout, no network, DNS, TLS, a 5xx or a window that is not there yet. Never a 4xx, "not associated" or a bad body.</summary>
    public static bool IsTransient(StoreCause cause) => cause is StoreCause.Timeout or StoreCause.NoNetwork or StoreCause.Dns or StoreCause.Tls or StoreCause.Http5xx or StoreCause.NoWindow;

    /// <summary>
    /// Slice 460, check 4 #1: the one retry rule for a failed server call. Retried: a transient cause, an HTTP 429, and a transport failure with no status and no named error (<see cref="HttpRequestError.Unknown"/>, a bare
    /// <see cref="IOException"/> or <see cref="SocketException"/>), which is cause <see cref="StoreCause.Other"/> (store_failed) but can pass. Never retried: a 4xx, a named error that is not a network one, or a JSON or bad-body failure.
    /// </summary>
    public static bool IsRetryable(Exception ex)
    {
        if (HasJsonException(ex))
        {
            return false;
        }

        StoreCause cause = Of(ex);
        return IsTooManyRequests(ex) || IsTransient(cause) || (cause == StoreCause.Other && IsBareTransportFailure(ex));
    }

    private static bool IsBareTransportFailure(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: null, HttpRequestError: HttpRequestError.Unknown } => true,
        HttpRequestException => false,
        IOException or SocketException => true,
        _ => false,
    };

    /// <summary>True when the exception or one inside it is an HTTP 429: reported as <see cref="StoreCause.Http4xx"/> but still retried, because a rate limit passes by itself.</summary>
    public static bool IsTooManyRequests(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is HttpRequestException { StatusCode: { } status } && (int)status == TooManyRequests)
            {
                return true;
            }
        }

        return false;
    }

    private static StoreCause OfStatus(HttpStatusCode status)
    {
        int code = (int)status;
        if (code == RequestTimeout)
        {
            return StoreCause.Timeout; // 408 passes by itself, so it is retried like a timeout; 429 is a 4xx cause (the closed list) that is still retried: see IsTooManyRequests (transience is separate from the cause)
        }

        return code is >= ServerErrorFrom and < ErrorTo ? StoreCause.Http5xx : code is >= ClientErrorFrom and < ServerErrorFrom ? StoreCause.Http4xx : StoreCause.Other;
    }

    private static StoreCause? OfRequestError(HttpRequestError error) => error switch
    {
        HttpRequestError.NameResolutionError => StoreCause.Dns,
        HttpRequestError.SecureConnectionError => StoreCause.Tls,
        HttpRequestError.ConnectionError => StoreCause.NoNetwork,
        _ => null,
    };

    private static StoreCause OfSocket(SocketError error) => error switch
    {
        SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData => StoreCause.Dns,
        SocketError.TimedOut => StoreCause.Timeout,
        _ => StoreCause.NoNetwork,
    };
}
