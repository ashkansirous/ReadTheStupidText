using System.Text.RegularExpressions;

namespace Sirous.Reading.Application.Usage;

/// <summary>
/// Slice 460: names a network failure that Android's <c>AndroidMessageHandler</c> (a Java exception inside the <see cref="HttpRequestException"/>) or iOS's <c>NSUrlSessionHandler</c> (a Foundation <c>NSErrorException</c> in the
/// NSURLErrorDomain) surfaces. This shared project cannot see those types, so they are matched by full type name (walking the base types, most derived first) and, for the Foundation error, by its domain and code. Pure and unit-tested.
/// The type names are from the SDK reference packs' own docs (Mono.Android, Microsoft.iOS): <c>Java.Net.UnknownHostException</c>, <c>Java.Net.SocketTimeoutException</c>, <c>Javax.Net.Ssl.SSLException</c>, <c>Foundation.NSErrorException</c>, <c>Foundation.NSUrlError</c>.
/// </summary>
public static partial class NativeNetworkErrors
{
    private const string UrlErrorDomain = "NSURLErrorDomain";
    private const string FoundationError = "Foundation.NSErrorException";

    // Most specific first for a reader; a lookup walks the exception's own type chain, so a subclass finds its nearest listed base.
    private static readonly Dictionary<string, StoreCause> JavaTypes = new()
    {
        ["Java.Net.UnknownHostException"] = StoreCause.Dns,
        ["Java.Net.SocketTimeoutException"] = StoreCause.Timeout,
        ["Javax.Net.Ssl.SSLException"] = StoreCause.Tls,
        ["Java.Security.Cert.CertificateException"] = StoreCause.Tls,
        ["Java.Net.ConnectException"] = StoreCause.NoNetwork,
        ["Java.Net.NoRouteToHostException"] = StoreCause.NoNetwork,
        ["Java.Net.SocketException"] = StoreCause.NoNetwork,
        ["Java.IO.IOException"] = StoreCause.NoNetwork,
    };

    // NSURLError codes (Foundation.NSUrlError): the values are Apple's constants, fixed since iOS 2.
    private static readonly Dictionary<long, StoreCause> UrlErrorCodes = new()
    {
        [-1001] = StoreCause.Timeout, // TimedOut
        [-1003] = StoreCause.Dns, // CannotFindHost
        [-1006] = StoreCause.Dns, // DNSLookupFailed
        [-1004] = StoreCause.NoNetwork, // CannotConnectToHost
        [-1005] = StoreCause.NoNetwork, // NetworkConnectionLost
        [-1009] = StoreCause.NoNetwork, // NotConnectedToInternet
        [-1018] = StoreCause.NoNetwork, // InternationalRoamingOff
        [-1020] = StoreCause.NoNetwork, // DataNotAllowed
        [-2000] = StoreCause.NoNetwork, // CannotLoadFromNetwork
        [-1200] = StoreCause.Tls, // SecureConnectionFailed
        [-1201] = StoreCause.Tls, // ServerCertificateHasBadDate
        [-1202] = StoreCause.Tls, // ServerCertificateUntrusted
        [-1203] = StoreCause.Tls, // ServerCertificateHasUnknownRoot
        [-1204] = StoreCause.Tls, // ServerCertificateNotYetValid
        [-1205] = StoreCause.Tls, // ClientCertificateRejected
        [-1206] = StoreCause.Tls, // ClientCertificateRequired
    };

    /// <summary>The cause of this one exception (not its inner ones) when it is a Java or Foundation network error; null when it is neither.</summary>
    public static StoreCause? Of(Exception ex)
    {
        for (Type? type = ex.GetType(); type is not null; type = type.BaseType)
        {
            if (type.FullName is { } name && JavaTypes.TryGetValue(name, out StoreCause cause))
            {
                return cause;
            }

            if (type.FullName == FoundationError)
            {
                return OfFoundation(ex);
            }
        }

        return null;
    }

    private static StoreCause? OfFoundation(Exception ex)
    {
        (string? domain, long? code) = ReadFoundation(ex);
        return domain == UrlErrorDomain && code is { } known && UrlErrorCodes.TryGetValue(known, out StoreCause cause) ? cause : null;
    }

    // NSErrorException has Domain and Code properties; its message ("Error Domain=NSURLErrorDomain Code=-1009 ...") is the fallback when a trimmed build left the properties out.
    private static (string? Domain, long? Code) ReadFoundation(Exception ex)
    {
        Type type = ex.GetType();
        string? domain = type.GetProperty("Domain")?.GetValue(ex) as string;
        long? code = CodeToLong(type.GetProperty("Code")?.GetValue(ex));
        if (domain is not null && code is { } known)
        {
            return (domain, known);
        }

        Match match = MessageFormat().Match(ex.Message);
        return match.Success && long.TryParse(match.Groups["code"].Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long parsed) ? (match.Groups["domain"].Value, parsed) : (null, null);
    }

    // On iOS NSErrorException.Code is a native-sized integer (nint), which boxes as IntPtr; IntPtr is not IConvertible, so Convert would throw. Nothing here throws: an unreadable code is null and the message is the fallback.
    private static long? CodeToLong(object? code) => code switch
    {
        IntPtr native => native.ToInt64(),
        UIntPtr native => unchecked((long)native.ToUInt64()),
        IConvertible convertible => TryConvert(convertible),
        _ => null,
    };

    private static long? TryConvert(IConvertible value)
    {
        try
        {
            return value.ToInt64(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            // no-fallback: an unreadable code is not a failure of the store call; the caller reads the message instead
            return null;
        }
    }

    [GeneratedRegex(@"Domain=(?<domain>\w+)\s+Code=(?<code>-?\d+)")]
    private static partial Regex MessageFormat();
}
