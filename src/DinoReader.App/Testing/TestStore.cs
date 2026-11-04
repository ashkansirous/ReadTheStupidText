#if DINO_TEST_BOOKS
using System.IO.Compression;
using DinoReader.Core;

namespace DinoReader.Testing;

/// <summary>How the Debug test store answers the store client's list (Slice 261): not at all (the live store), a fixed list, the same list with slow downloads, or "offline".</summary>
internal enum TestStoreMode
{
	Off,
	Ok,
	Slow,
	Stall,
	Offline,
}

/// <summary>
/// The Debug-only test store (Slice 261): lets the first-run book picker and the downloads run in a scripted flow without the live store. <see cref="StoreClient"/> asks it for the list while
/// <see cref="Mode"/> is on (the <c>test-store mode=ok|slow|offline|off</c> hook; off by default) and for any package whose address starts with <see cref="UrlScheme"/> (always, a Debug build never
/// has a live address like it). The books are the former starters, carried as Debug-only assets in <c>Resources/Raw/testbooks/</c>; Release has neither these assets nor this code.
/// The first two books carry the Hot and Popular tags, so the "tagged first" order can be seen.
/// </summary>
internal static class TestStore
{
	public const string UrlScheme = "testbook:";
	private const string AssetExtension = ".dino";
	private const int SlowChunks = 8;
	private const int SlowChunkMilliseconds = 350;
	// Stall: a chunk takes far longer than any flow waits, so a download is still in flight when the flow taps Cancel (a cancel ends the delay at once).
	private const int StallChunkMilliseconds = 30000;
	private const int CopyBufferBytes = 81920;

	// Oldest first: the last one imported is the newest and so comes first on the shelf, as the former starters did.
	private static readonly string[] Stems = ["tortoise-and-hare", "gingerbread-man", "three-little-pigs", "good-enough"];

	// Listed with the tagged ones last on purpose: the picker must put them first.
	private static readonly Dictionary<string, StoreTag[]> TagsByStem = new() { ["three-little-pigs"] = [StoreTag.Hot], ["tortoise-and-hare"] = [StoreTag.Popular] };

#if DINO_TEST_STORE_ON
	// The Windows VM test package has no Debug hooks to switch the test store on, so it starts on (-p:DinoTestBooks=true).
	public static TestStoreMode Mode { get; set; } = TestStoreMode.Ok;
#else
	public static TestStoreMode Mode { get; set; } = TestStoreMode.Off;
#endif

	/// <summary>The test book stems, in import order (oldest first).</summary>
	public static IReadOnlyList<string> AllStems => Stems;

	public static bool IsListing => Mode != TestStoreMode.Off;

	public static bool Serves(string url) => url.StartsWith(UrlScheme, StringComparison.Ordinal);

	public static string AssetNameOf(string stem) => stem + AssetExtension;

	/// <summary>The list the test store serves for the current mode.</summary>
	public static async Task<StoreListing> ListAsync()
	{
		if (Mode == TestStoreMode.Offline)
		{
			return new StoreListing(StoreLoadStatus.Offline, []);
		}

		var items = new List<StoreItem>();
		foreach (string stem in Stems.Reverse())
		{
			items.Add(await ItemOfAsync(stem));
		}

		return new StoreListing(StoreLoadStatus.Ok, StoreCatalog.Readable(items));
	}

	/// <summary>The listing line of one test book, or null for an unknown stem or id.</summary>
	public static async Task<StoreItem?> FindAsync(string stemOrId)
	{
		foreach (string stem in Stems)
		{
			StoreItem item = await ItemOfAsync(stem);
			if (string.Equals(stem, stemOrId, StringComparison.OrdinalIgnoreCase) || string.Equals(item.Id, stemOrId, StringComparison.OrdinalIgnoreCase))
			{
				return item;
			}
		}

		return null;
	}

	private static async Task<StoreItem> ItemOfAsync(string stem)
	{
		await using Stream asset = await FileSystem.OpenAppPackageFileAsync(AssetNameOf(stem));
		using var copy = new MemoryStream();
		await asset.CopyToAsync(copy);
		StoryPackageManifest manifest = ManifestOf(copy);
		copy.Position = 0;
		Story story = await StoryPackageReader.ReadStoryOnlyAsync(copy);
		StoreTag[] tags = TagsByStem.TryGetValue(stem, out StoreTag[]? found) ? found : [];
		return new StoreItem(manifest.Id, story.Title, story.Language, manifest.SchemaVersion, manifest.Version ?? 1, story.Pages.Count, null, null, UrlScheme + AssetNameOf(stem), Tags: tags, Bytes: copy.Length);
	}

	private static StoryPackageManifest ManifestOf(MemoryStream package)
	{
		package.Position = 0;
		using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
		ZipArchiveEntry entry = zip.GetEntry(StoryPackageManifest.ManifestFileName) ?? throw new StoryPackageException("A test book has no manifest.");
		using var reader = new StreamReader(entry.Open());
		return StoryPackageManifest.Parse(reader.ReadToEnd());
	}

	/// <summary>Writes the test book behind <paramref name="url"/> to <paramref name="toPath"/>, reporting bytes as a download would; in slow mode in a few timed chunks.</summary>
	public static async Task CopyAsync(string url, string toPath, Action<long, long?>? bytes, CancellationToken cancel)
	{
		string assetName = url[UrlScheme.Length..];
		await using Stream asset = await FileSystem.OpenAppPackageFileAsync(assetName);
		using var all = new MemoryStream();
		await asset.CopyToAsync(all, cancel);
		Directory.CreateDirectory(Path.GetDirectoryName(toPath)!);
		await using FileStream file = File.Create(toPath);
		long total = all.Length;
		bool timed = Mode is TestStoreMode.Slow or TestStoreMode.Stall;
		int chunk = timed ?(int)Math.Max(1, total / SlowChunks) : CopyBufferBytes;
		all.Position = 0;
		byte[] buffer = new byte[chunk];
		long done = 0;
		int read;
		while ((read = await all.ReadAsync(buffer, cancel)) > 0)
		{
			await file.WriteAsync(buffer.AsMemory(0, read), cancel);
			done += read;
			bytes?.Invoke(done, total);
			if (timed)
			{
				await Task.Delay(Mode == TestStoreMode.Stall ? StallChunkMilliseconds : SlowChunkMilliseconds, cancel);
			}
		}
	}
}
#endif
