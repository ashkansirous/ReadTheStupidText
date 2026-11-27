using DinoReader.Resources.Strings;
using Sirous.Reading.Application.Net;

namespace DinoReader;

/// <summary>Which of the three first-books "could not load" screens to show (Slice 453).</summary>
public enum FirstBooksLoadKind
{
	Offline,
	Cancelled,
	Failed,
}

/// <summary>The words and log marker of one load screen; <see cref="ShowsOfflineBadge"/> is true only for the real no-connection case.</summary>
public sealed record FirstBooksLoadCopy(FirstBooksLoadKind Kind, string Title, string Body, string Marker, bool ShowsOfflineBadge)
{
	public const string OfflineMarker = "offline";
	public const string CancelledMarker = "cancelled-load";
	public const string FailedMarker = "failed-load";

	/// <summary>The list call came back as Offline: a server that answered with an error (or a bad body) is a failed load, only no answer is offline.</summary>
	public static FirstBooksLoadCopy ForOfflineListing(HttpErrorKind? errorKind) =>
		errorKind is { } kind && kind != HttpErrorKind.Offline ? For(FirstBooksLoadKind.Failed) : For(FirstBooksLoadKind.Offline);

	/// <summary>The list call was cancelled: the cancelled copy only when the grown-up's Cancel (or Back on the wait) caused it, else the failed copy.</summary>
	public static FirstBooksLoadCopy ForCancellation(bool stoppedByGrownUp) =>
		For(stoppedByGrownUp ? FirstBooksLoadKind.Cancelled : FirstBooksLoadKind.Failed);

	/// <summary>The list call threw something else.</summary>
	public static FirstBooksLoadCopy ForFailure() => For(FirstBooksLoadKind.Failed);

	private static FirstBooksLoadCopy For(FirstBooksLoadKind kind) => kind switch
	{
		FirstBooksLoadKind.Offline => new(kind, AppResources.FirstBooks_OfflineTitle, AppResources.FirstBooks_OfflineBody, OfflineMarker, true),
		FirstBooksLoadKind.Cancelled => new(kind, AppResources.FirstBooks_CancelledTitle, AppResources.FirstBooks_CancelledBody, CancelledMarker, false),
		_ => new(kind, AppResources.FirstBooks_FailedTitle, AppResources.FirstBooks_FailedBody, FailedMarker, false),
	};
}
