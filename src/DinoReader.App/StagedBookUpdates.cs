using System.Diagnostics;
using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// The packages the background check downloaded for books that are due an update (Slice 181), kept in app data as <c>book-updates/&lt;id&gt;/v&lt;n&gt;.dino</c> until the next cold start.
/// The background run never touches a stored book; <see cref="ApplyOnceAsync"/> swaps the staged packages in before the gallery first loads, when no book can be open.
/// A crash mid-apply leaves the entry, and the next launch tries it again (<see cref="StarterStories.SwapFromStoreAsync"/> is safe to run twice).
/// </summary>
public sealed class StagedBookUpdates(IStoryLibrary library, StarterStories starters)
{
	private const string LogTag = "[BookUpdate]";
	private const string FolderName = "book-updates";
	private const string FilePrefix = "v";
	private const string FileExtension = ".dino";
	private const string PartExtension = ".part";

	private readonly object _lock = new();
	private Task? _applyTask;

	private sealed record Entry(string Id, int Version, string Path);

	private static string Root => System.IO.Path.Combine(FileSystem.AppDataDirectory, FolderName);

	/// <summary>The books the library can be judged on: every catalogue book on the shelf, against the given store (or staged) versions.</summary>
	public async Task<IReadOnlyList<BookUpdateDecision>> PlanAsync(IReadOnlyList<StoreBookVersion> versions)
	{
		List<Story> shelf = (await library.ListAsync()).Where(s => s.SourceId is { } id && StarterStories.IsCatalogueId(id)).ToList();
		var shelfBooks = new List<ShelfBook>();
		foreach (Story story in shelf)
		{
			shelfBooks.Add(new ShelfBook(story.SourceId!, StarterUpdate.PageTextHash(story), await starters.ContentHashOfAsync(story)));
		}

		var records = shelf.Select(s => s.SourceId!).Distinct(StringComparer.OrdinalIgnoreCase)
			.Select(id => new InstalledBookRecord(id, StarterStories.RecordedVersion(id), StarterStories.RecordedTextHash(id), StarterStories.RecordedSeenVersion(id))).ToList();
		return BookUpdatePlan.Decide(versions, records, shelfBooks);
	}

	/// <summary>The newest version already staged for the book (null when none).</summary>
	public int? StagedVersion(string id) => ListEntries().Where(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)).Select(e => (int?)e.Version).Max();

	/// <summary>Keeps a downloaded package for the next cold start (copied under a temporary name and renamed, so a half-copied file is never taken for a whole one); the caller deletes <paramref name="file"/>.</summary>
	public void Stage(string id, int version, string file)
	{
		string folder = FolderOf(id);
		Directory.CreateDirectory(folder);
		string target = System.IO.Path.Combine(folder, $"{FilePrefix}{version}{FileExtension}");
		string part = target + PartExtension;
		File.Copy(file, part, overwrite: true);
		File.Move(part, target, overwrite: true);
		foreach (string older in Directory.GetFiles(folder).Where(f => f != target))
		{
			File.Delete(older);
		}
	}

	/// <summary>
	/// Applies every staged package once per process: the first call starts the one shared task, every later call (the gallery's refreshes, whoever asks first) gets the same task, so
	/// the shelf is never read while a swap runs. Each is judged again against the library as it is now: a book edited, deleted or already newer meanwhile just loses its entry.
	/// It never throws: an unexpected failure is logged, and a staged entry that failed for a reason other than storage is dropped, so one bad package cannot crash every launch.
	/// </summary>
	public Task ApplyOnceAsync()
	{
		lock (_lock)
		{
			return _applyTask ??= RunApplyAsync();
		}
	}

	private async Task RunApplyAsync()
	{
		var clock = Stopwatch.StartNew();
		Debug.WriteLine($"{LogTag} apply start");
		try
		{
			await ApplyAllAsync(ListEntries());
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"{LogTag} applying staged books failed, retried at the next launch: {ex}");
		}

		Debug.WriteLine($"{LogTag} apply end after {clock.ElapsedMilliseconds} ms");
	}

	private async Task ApplyAllAsync(IReadOnlyList<Entry> entries)
	{
		if (entries.Count == 0)
		{
			return;
		}

#if DEBUG
		foreach (Entry forced in entries.Where(e => StarterStories.ConsumeForcedApplyForTest(e.Id)))
		{
			Debug.WriteLine($"{LogTag} {forced.Id}: forced apply, recorded version lowered to v{BookUpdatePlan.ForcedRecordedVersion(forced.Version)}");
			StarterStories.RecordVersionForTest(forced.Id, BookUpdatePlan.ForcedRecordedVersion(forced.Version));
		}
#endif
		IReadOnlyList<BookUpdateDecision> decisions = await PlanAsync(entries.Select(e => new StoreBookVersion(e.Id, e.Version)).ToList());
		foreach (Entry entry in entries)
		{
			BookUpdateDecision? decision = decisions.FirstOrDefault(d => string.Equals(d.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
			await ApplyOneAsync(entry, decision);
		}
	}

	private async Task ApplyOneAsync(Entry entry, BookUpdateDecision? decision)
	{
		if (decision is not { Action: BookUpdateAction.Update })
		{
			Debug.WriteLine($"{LogTag} {entry.Id}: staged v{entry.Version} dropped ({decision?.Skip.ToString() ?? decision?.Action.ToString() ?? "no record"})");
			if (decision?.Skip == BookUpdateSkip.Edited)
			{
				StarterStories.RecordSeenVersion(entry.Id, entry.Version);
			}

			DeleteEntry(entry);
			return;
		}

		try
		{
			bool swapped = await SwapAsync(entry);
			Debug.WriteLine($"{LogTag} {entry.Id}: {(swapped ? $"swapped to v{entry.Version}" : "edited, left as it is")}");
			if (!swapped)
			{
				StarterStories.RecordSeenVersion(entry.Id, entry.Version);
			}

			DeleteEntry(entry);
		}
		catch (Exception ex) when (ex is StoryPackageException or InvalidDataException)
		{
			Debug.WriteLine($"{LogTag} {entry.Id}: bad staged package dropped: {ex.Message}");
			DeleteEntry(entry);
		}
		catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"{LogTag} {entry.Id}: swap failed, entry kept for the next launch: {ex.Message}");
		}
		catch (Exception ex)
		{
			// Never the stored book: only the staged copy goes, so a package that fails the same way every time cannot loop.
			Debug.WriteLine($"{LogTag} {entry.Id}: unexpected failure, staged entry dropped: {ex}");
			DeleteEntry(entry);
		}
	}

	private async Task<bool> SwapAsync(Entry entry)
	{
		var package = new MemoryStream();
		await using (FileStream stream = File.OpenRead(entry.Path))
		{
			await stream.CopyToAsync(package);
		}

		return await starters.SwapFromStoreAsync(package, entry.Id, entry.Version);
	}

	private static string FolderOf(string id) =>
		id == System.IO.Path.GetFileName(id) && id.Length > 0 ? System.IO.Path.Combine(Root, id) : throw new ArgumentException("Not a book id.", nameof(id));

	private static List<Entry> ListEntries()
	{
		var entries = new List<Entry>();
		if (!Directory.Exists(Root))
		{
			return entries;
		}

		foreach (string folder in Directory.GetDirectories(Root))
		{
			string id = System.IO.Path.GetFileName(folder);
			entries.AddRange(Directory.GetFiles(folder, $"{FilePrefix}*{FileExtension}")
				.Select(f => (File: f, Version: ParseVersion(f))).Where(x => x.Version is not null)
				.Select(x => new Entry(id, x.Version!.Value, x.File)));
		}

		return entries;
	}

	private static int? ParseVersion(string file)
	{
		string name = System.IO.Path.GetFileNameWithoutExtension(file);
		return name.StartsWith(FilePrefix, StringComparison.Ordinal) && int.TryParse(name.AsSpan(FilePrefix.Length), out int version) ? version : null;
	}

	private static void DeleteEntry(Entry entry)
	{
		try
		{
			File.Delete(entry.Path);
			string folder = System.IO.Path.GetDirectoryName(entry.Path)!;
			if (!Directory.EnumerateFileSystemEntries(folder).Any())
			{
				Directory.Delete(folder);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"{LogTag} could not remove a staged file: {ex.Message}");
		}
	}
}
