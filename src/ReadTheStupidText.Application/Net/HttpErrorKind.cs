namespace ReadTheStupidText.Application.Net;

/// <summary>
/// What a failed call to our server means for the person (Slice 357). Data only: each app turns a kind into its own translated text.
/// </summary>
public enum HttpErrorKind
{
    /// <summary>No answer at all: no network, a timeout or a dropped connection.</summary>
    Offline,

    /// <summary>400: the only status whose body carries a sentence (English, from the server).</summary>
    BadRequest,

    /// <summary>401: signing in is needed.</summary>
    Unauthorized,

    /// <summary>403: not allowed.</summary>
    Forbidden,

    /// <summary>404: not found.</summary>
    NotFound,

    /// <summary>402: the book has to be bought first.</summary>
    PaymentRequired,

    /// <summary>429: too many tries, wait a minute.</summary>
    TooManyRequests,

    /// <summary>Any 5xx: a general "problem connecting to the server".</summary>
    ServerError,

    /// <summary>Any other 4xx (and any other status): shown with the general server text.</summary>
    OtherClientError,
}
