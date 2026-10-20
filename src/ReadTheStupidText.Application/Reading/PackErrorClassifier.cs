using System.Net;
using System.Net.Sockets;

namespace ReadTheStupidText.Application.Reading;

/// <summary>Maps a download or install exception to a <see cref="PackDownloadResult"/>.</summary>
public static class PackErrorClassifier
{
    // Android's HttpClient handler surfaces Java exceptions, which Application cannot
    // reference, so they are recognised by full type name (Slice 50).
    private static readonly HashSet<string> JavaNetworkTypeNames =
    [
        "Java.Net.UnknownHostException",
        "Java.Net.ConnectException",
        "Java.Net.SocketTimeoutException",
        "Java.Net.SocketException",
        "Java.Net.NoRouteToHostException",
    ];

    private const string JavaIoExceptionTypeName = "Java.IO.IOException";

    /// <summary>Offline when anything in the chain is a connectivity failure, otherwise Failed.</summary>
    public static PackDownloadResult Classify(Exception exception)
    {
        bool fromHttp = exception is HttpRequestException;
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (IsConnectivityFailure(current, fromHttp))
            {
                return PackDownloadResult.Offline;
            }
        }

        return PackDownloadResult.Failed;
    }

    /// <summary>
    /// Classifies a failure while unpacking a verified download: a bad zip, manifest or
    /// file list is deterministic (InvalidPack); anything else (disk full, I/O) is a normal failure.
    /// </summary>
    public static PackDownloadResult ClassifyInstall(Exception exception)
    {
        bool invalid = exception is InvalidDataException or FormatException
            or FileNotFoundException or DirectoryNotFoundException;
        return invalid ? PackDownloadResult.InvalidPack : Classify(exception);
    }

    private static bool IsConnectivityFailure(Exception exception, bool fromHttp)
    {
        if (exception is SocketException or WebException)
        {
            return true;
        }

        if (exception is HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError })
        {
            return true;
        }

        return HasNetworkTypeName(exception.GetType(), fromHttp);
    }

    private static bool HasNetworkTypeName(Type type, bool fromHttp)
    {
        for (Type? current = type; current is not null && current != typeof(Exception); current = current.BaseType)
        {
            string? name = current.FullName;
            if (name is not null && (JavaNetworkTypeNames.Contains(name) || (fromHttp && name == JavaIoExceptionTypeName)))
            {
                return true;
            }
        }

        return false;
    }
}
