using Sirous.Reading.Application.Net;

namespace DinoReader;

/// <summary>The server answered the list call but its body is not a listing (Slice 453); only <c>StoreCatalog.Parse</c> raises it, never a local read.</summary>
public sealed class StoreListFormatException(Exception inner) : Exception("The store listing could not be parsed.", inner);

/// <summary>Which error kind the first-books list call reports for a failure (Slice 453): a bad listing body is a server answer, every other failure is classified as before.</summary>
public static class StoreListError
{
	public static HttpErrorKind? KindOf(Exception ex) =>
		ex is StoreListFormatException ? HttpErrorKind.ServerError : HttpErrorClassifier.Classify(ex);
}
