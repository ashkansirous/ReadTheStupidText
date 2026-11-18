#if DINO_TEST_BOOKS
using DinoReader.Core;

namespace DinoReader.Testing;

/// <summary>
/// What the <c>full-shelf</c> hook does with the live store's list (Slice 404): the books to put on the shelf (each listed book once; the order is in <see cref="For"/>; the newest import comes first on the shelf, so
/// <see cref="LastBookId"/> landing last is what a flow waits for to know the shelf is full), and the Debug test copies a real book replaces, one book at a time, so no test copy stands in for the real book.
/// A paid book is not downloaded (the store refuses it without a real receipt): its package is read from <see cref="PackageFolder"/> in the app's data folder, <see cref="PackageName"/> per book.
/// </summary>
internal sealed record FullShelfPlan(IReadOnlyList<StoreItem> Download, IReadOnlySet<string> ClearFirst)
{
	/// <summary>The folder (inside the app's data folder) the paid books' packages are copied to before a store-picture run.</summary>
	public const string PackageFolder = "full-shelf";

	private const string PackageExtension = ".dino";

	/// <summary>The file name of one book's package in <see cref="PackageFolder"/>.</summary>
	public static string PackageName(string id) => id + PackageExtension;

	/// <summary>
	/// The book the hook always lands last, and the title the flows wait for (they cannot read the hook's log): pinned here, not "the book with the last id", so a new catalogue book never moves it. A test checks every flow
	/// that waits for the shelf waits for <see cref="LastBookTitle"/>. When this book is not in the live list the hook just ends with the last id, and the flows' wait is on a book that is not there (the hook logs it).
	/// </summary>
	public const string LastBookId = "e2fb7f5de4ab4c4eb8bd2bb97c91accc";

	/// <summary>The English title of <see cref="LastBookId"/> (Jack and the Beanstalk), which the flows wait for.</summary>
	public const string LastBookTitle = "Jack and the Beanstalk";

	/// <summary>The file in the app's data folder the hook writes when it finished: a later call (a retried shot) ends at once instead of clearing and fetching the shelf again.</summary>
	public const string DoneMarker = "full-shelf.done";

	/// <summary>
	/// Plans the shelf for <paramref name="listed"/> (the live list, readable books only) given <paramref name="testBookIds"/> (the Debug test books' ids, which <c>skip-first-run</c> put on the shelf). The order:
	/// books not on the shelf yet (by id), then the books that replace a test copy (by id), then <see cref="LastBookId"/>, so a failed fetch never leaves the shelf emptier than it was and the last book showing means the rest landed.
	/// </summary>
	public static FullShelfPlan For(IEnumerable<StoreItem> listed, IEnumerable<string> testBookIds)
	{
		var tests = new HashSet<string>(testBookIds, StringComparer.OrdinalIgnoreCase);
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<StoreItem> download = [.. listed.Where(item => seen.Add(item.Id)).OrderBy(item => Rank(item.Id, tests)).ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)];
		return new FullShelfPlan(download, tests);
	}

	private static int Rank(string id, IReadOnlySet<string> tests) =>
		string.Equals(id, LastBookId, StringComparison.OrdinalIgnoreCase) ? 2 : tests.Contains(id) ? 1 : 0;

	/// <summary>The paid books of <see cref="Download"/> whose package is not among <paramref name="presentFiles"/> (file names in <see cref="PackageFolder"/>); empty when every paid book can be imported.</summary>
	public IReadOnlyList<string> MissingPackages(IReadOnlySet<string> presentFiles) =>
		[.. Download.Where(item => item.Sale == BookSale.Paid && !presentFiles.Contains(PackageName(item.Id))).Select(item => item.Id)];
}
#endif
