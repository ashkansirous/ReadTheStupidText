#if DINO_TEST_BOOKS
using DinoReader.Core;

namespace DinoReader.Testing;

/// <summary>
/// What the <c>full-shelf</c> hook does with the live store's list (Slice 404, Decision 3): 12 books on the shelf, not the whole catalogue: the four Debug test books <c>skip-first-run</c> already put there (they stay as they are,
/// no package, no fetch) and the eight <see cref="BookIds"/> (two free ones and six paid ones that need a package). The order is in <see cref="For"/>; the newest import comes first on the shelf, so
/// <see cref="LastBookId"/> landing last is what a flow waits for to know the shelf is full.
/// A paid book is not downloaded (the store refuses it without a real receipt): its package, the one the store upload already made (fetched by <c>.github/scripts/fetch-shelf-packages.mjs</c>, never built), is read from
/// <see cref="PackageFolder"/> in the app's data folder, <see cref="PackageName"/> per book.
/// </summary>
internal sealed record FullShelfPlan(IReadOnlyList<StoreItem> Download)
{
	/// <summary>
	/// The eight books added to the four test books (Slice 404, Decision 3), the fewest paid packages for a shelf of 12: the free books first (The Lion and the Mouse, X Marks the Treasure!), then six paid ones
	/// (Goldilocks, The Little Red Hen, Jack and the Beanstalk, Little Red Riding Hood, The Elves and the Shoemaker, The Three Billy Goats Gruff). <c>tools/dino/full-shelf-books.json</c> lists the same ids for the fetch script;
	/// a test keeps the two equal.
	/// </summary>
	public static readonly IReadOnlyList<string> BookIds =
	[
		"1ca50c6936cc44788b992726784e8132", // The Lion and the Mouse (free)
		"a02c0731b7ac475a896e9184f3575e7b", // X Marks the Treasure! (free)
		"67695905c10a46bd81aa498beab9609e", // Goldilocks and the Three Bears (paid)
		"6eff8d779a9542fcafa76ae58ba1e96b", // The Little Red Hen (paid)
		"e2fb7f5de4ab4c4eb8bd2bb97c91accc", // Jack and the Beanstalk (paid, the pinned last book)
		"1f0f0cb207b244368b7aef26028f4ac5", // Little Red Riding Hood (paid)
		"34efea95400343ac998e9ed99158d370", // The Elves and the Shoemaker (paid)
		"32d04b935a5649088293314cc648c825", // The Three Billy Goats Gruff (paid)
	];

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
	/// Plans the shelf for <paramref name="listed"/> (the live list, readable books only) given <paramref name="testBookIds"/> (the Debug test books' ids, which <c>skip-first-run</c> put on the shelf and which stay as they are):
	/// each of <see cref="BookIds"/> that the list holds and is not a test book, once, by id, then <see cref="LastBookId"/>, so the last book showing means the rest landed. A listed book outside <see cref="BookIds"/> is left off.
	/// </summary>
	public static FullShelfPlan For(IEnumerable<StoreItem> listed, IEnumerable<string> testBookIds)
	{
		var tests = new HashSet<string>(testBookIds, StringComparer.OrdinalIgnoreCase);
		var wanted = new HashSet<string>(BookIds, StringComparer.OrdinalIgnoreCase);
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<StoreItem> download = [.. listed.Where(item => wanted.Contains(item.Id) && !tests.Contains(item.Id) && seen.Add(item.Id)).OrderBy(item => Rank(item.Id)).ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)];
		return new FullShelfPlan(download);
	}

	/// <summary>What the done marker holds: the plan's book ids, so a marker written for another list of books never ends a call <c>ok already</c>.</summary>
	public static string MarkerText => string.Join(",", BookIds.Order(StringComparer.Ordinal));

	/// <summary>The ids of <see cref="BookIds"/> the plan does not hold (not in the live list); empty when all eight will land, which with the four test books is the 12 of the shelf.</summary>
	public IReadOnlyList<string> MissingBooks() =>
		[.. BookIds.Where(id => !Download.Any(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)))];

	private static int Rank(string id) => string.Equals(id, LastBookId, StringComparison.OrdinalIgnoreCase) ? 1 : 0;

	/// <summary>Slice 434, the offline Windows VM: the ids of <see cref="BookIds"/> (free ones too) whose package is not among <paramref name="presentFiles"/>; empty when all eight can be read from the folder.</summary>
	public static IReadOnlyList<string> MissingAllPackages(IReadOnlySet<string> presentFiles) =>
		[.. BookIds.Where(id => !presentFiles.Contains(PackageName(id)))];

	/// <summary>The paid books of <see cref="Download"/> whose package is not among <paramref name="presentFiles"/> (file names in <see cref="PackageFolder"/>); empty when every paid book can be imported.</summary>
	public IReadOnlyList<string> MissingPackages(IReadOnlySet<string> presentFiles) =>
		[.. Download.Where(item => item.Sale == BookSale.Paid && !presentFiles.Contains(PackageName(item.Id))).Select(item => item.Id)];
}
#endif
