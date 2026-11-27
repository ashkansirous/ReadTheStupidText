// Slice 460: stand-ins named exactly like the Android (Mono.Android) and iOS (Microsoft.iOS) exception types the shared code matches by full type name, with the same base-type chain.
#pragma warning disable SA1403, SA1402, IDE0130, CA1716 // several namespaces in one test file, named to match the SDK types
namespace Java.IO
{
    internal class IOException(string message) : Exception(message);
}

namespace Java.Net
{
    internal class SocketException(string message) : Java.IO.IOException(message);

    internal sealed class ConnectException(string message) : SocketException(message);

    internal sealed class UnknownHostException(string message) : Java.IO.IOException(message);

    internal sealed class SocketTimeoutException(string message) : Java.IO.IOException(message);
}

namespace Javax.Net.Ssl
{
    internal class SSLException(string message) : Java.IO.IOException(message);

    internal sealed class SSLHandshakeException(string message) : SSLException(message);
}

namespace Foundation
{
    /// <summary>With no properties (a trimmed build): only the message says the domain and code.</summary>
    internal class NSErrorException(string domain, long code) : Exception($"Error Domain={domain} Code={code} \"x\"");
}

namespace Foundation.WithProperties
{
    /// <summary>Derives from the type named <c>Foundation.NSErrorException</c> and carries the real exception's Domain and Code properties.</summary>
    internal sealed class NSErrorExceptionWithProperties(string domain, long code) : Foundation.NSErrorException(domain, code)
    {
        public string Domain { get; } = domain;

        public long Code { get; } = code;
    }

    /// <summary>As the real iOS exception: Code is a native-sized integer (nint), which boxes as IntPtr (not IConvertible).</summary>
    internal sealed class NSErrorExceptionWithNativeCode(string domain, nint code) : Foundation.NSErrorException(domain, code)
    {
        public string Domain { get; } = domain;

        public nint Code { get; } = code;
    }
}
