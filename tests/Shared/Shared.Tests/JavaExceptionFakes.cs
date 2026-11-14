using System;
// Stand-ins for the Java exceptions Android's HttpClient handler throws; Application matches them by full type name.
namespace Java.Net
{
    public class UnknownHostException : Exception;

    public class ConnectException : Exception;

    public class SocketTimeoutException : Exception;

    public class DerivedHostException : UnknownHostException;
}

namespace Java.IO
{
    public class IOException : Exception;
}
