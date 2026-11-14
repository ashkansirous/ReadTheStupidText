using System.Diagnostics;

namespace DinoReader;

/// <summary>
/// Slice 375 D: the one owner of the books waiting for their high-res pack. It owns one small JSON file in app data (book id to the version still waiting, see <see cref="HighResUpgradeTable"/>),
/// reads it once, and every change goes through here, under one lock, written to a temp name and renamed so a kill never leaves half a file. It does no network and no staging itself:
/// <see cref="HighResUpgradeRunner"/> downloads, <see cref="StagedBookUpdates"/> swaps.
/// </summary>
public sealed class HighResUpgrades
{
	private const string FileName = "high-res-upgrades.json";
	private const string PartExtension = ".part";

	private readonly object _lock = new();
	private HighResUpgradeTable? _table;

	private const string InstalledFileName = "high-res-installed.json";

	private static string Path => System.IO.Path.Combine(FileSystem.AppDataDirectory, FileName);

	private const string StagedSizeFileName = "high-res-staged-size.json";

	private static string StagedSizePath => System.IO.Path.Combine(FileSystem.AppDataDirectory, StagedSizeFileName);

	private static string InstalledPath => System.IO.Path.Combine(FileSystem.AppDataDirectory, InstalledFileName);

	/// <summary>The table as it is now.</summary>
	public HighResUpgradeTable Snapshot()
	{
		lock (_lock)
		{
			return _table ??= Load();
		}
	}

	/// <summary>An install choice for <paramref name="version"/> (<see cref="HighResUpgradeTable.Install"/>): pending when <paramref name="upgradeLater"/>, else any older entry is removed.</summary>
	public void Install(string id, int version, bool upgradeLater) => Change(table => table.Install(id, version, upgradeLater), $"{id} install v{version} upgradeLater={upgradeLater}");

	/// <summary>A book delete (<see cref="HighResUpgradeTable.Remove"/>): the entry, the installed-size record and any remembered staged size go.</summary>
	public void Remove(string id)
	{
		Change(table => table.Remove(id), $"{id} removed");
		ForgetInstalledHigh(id);
		ForgetStagedSize(id);
	}

	public void MarkStaged(string id, int version) => Change(table => table.MarkStaged(id, version), $"{id} staged v{version}");

	/// <summary>The outcome rule (Slice 375 D): the one way an attempt's end changes an entry (<see cref="HighResUpgradeTable.Apply"/>). Done also records that the installed copy is high-res.</summary>
	public void Record(string id, int version, HighResOutcome outcome)
	{
		HighResLog.Write($"{id}: v{version} outcome {outcome}");
		if (outcome == HighResOutcome.Done)
		{
			SetInstalledHigh(id, version);
		}

		Change(table => table.Apply(id, version, outcome), $"{id} {outcome} v{version}");
	}

	/// <summary>Whether the copy on the shelf at <paramref name="version"/> is the high-res one (a same-version hd package is then never swapped again).</summary>
	public bool InstalledIsHigh(string id, int version)
	{
		lock (_lock)
		{
			return LoadInstalled().TryGetValue(id, out int have) && have == version;
		}
	}

	/// <summary>A staged update of <paramref name="version"/> was fetched at a size: remembered until the swap (review #8), so the installed-size record changes when the copy is really swapped in, never at staging.</summary>
	public void RecordStagedSize(string id, int version, bool high)
	{
		if (high)
		{
			ChangeFile(StagedSizePath, map => map[id] = version);
		}
		else
		{
			ForgetStagedSize(id);
		}
	}

	public void ForgetStagedSize(string id) => ChangeFile(StagedSizePath, map => map.Remove(id));

	/// <summary>The staged update of <paramref name="version"/> was swapped in: the installed copy is high-res only when the staged package was.</summary>
	public void SettleInstalledAtSwap(string id, int version)
	{
		lock (_lock)
		{
			bool high = LoadFile(StagedSizePath).TryGetValue(id, out int staged) && staged == version;
			RecordInstalledSize(id, version, high);
			ForgetStagedSize(id);
		}
	}

	/// <summary>A copy of <paramref name="version"/> was installed at <paramref name="size"/>: remembers a high-res one, forgets the record for any other.</summary>
	public void RecordInstalledSize(string id, int version, bool high)
	{
		if (high)
		{
			SetInstalledHigh(id, version);
		}
		else
		{
			ForgetInstalledHigh(id);
		}
	}

	public void ForgetInstalledHigh(string id) => ChangeFile(InstalledPath, map => map.Remove(id));

	private void SetInstalledHigh(string id, int version) => ChangeFile(InstalledPath, map => map[id] = version);

	private void ChangeFile(string path, Action<Dictionary<string, int>> change)
	{
		lock (_lock)
		{
			Dictionary<string, int> map = LoadFile(path);
			change(map);
			SaveFile(path, System.Text.Json.JsonSerializer.Serialize(map));
		}
	}

	private Dictionary<string, int> LoadInstalled() => LoadFile(InstalledPath);

	private static Dictionary<string, int> LoadFile(string path)
	{
		try
		{
			string? json = File.Exists(path) ? File.ReadAllText(path) : null;
			var saved = json is null ? null : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(json);
			return new Dictionary<string, int>(saved ?? [], StringComparer.OrdinalIgnoreCase);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
		{
			// no-fallback: an unreadable record means no copy is known to be high-res; at worst one same-version swap repeats
			Debug.WriteLine($"[HighRes] could not read the installed-size record: {ex.Message}");
			return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		}
	}

	public void MarkLost(string id, int version) => Change(table => table.MarkLost(id, version), $"{id} staged file lost v{version}");

	public void Reconcile(Func<string, int?> installedVersion, Func<string, int?> stagedUpdateVersion, Func<string, bool?> offersHighRes) =>
		Change(table => table.Reconcile(installedVersion, stagedUpdateVersion, offersHighRes), "reconciled");

	/// <summary>The entry the staged package of <paramref name="id"/> at <paramref name="version"/> belongs to is staged (a staged package with no staged entry is not applied).</summary>
	public bool IsStaged(string id, int version) => Snapshot().Get(id) is { Phase: HighResPhase.Staged } entry && entry.Version == version;

	private void Change(Func<HighResUpgradeTable, HighResUpgradeTable> transition, string what)
	{
		lock (_lock)
		{
			HighResUpgradeTable before = _table ??= Load();
			HighResUpgradeTable after = transition(before);
			if (ReferenceEquals(before, after))
			{
				return;
			}

			_table = after;
			Save(after);
			HighResLog.Write($"state {what}");
		}
	}

	private static HighResUpgradeTable Load()
	{
		try
		{
			return File.Exists(Path) ? HighResUpgradeTable.FromJson(File.ReadAllText(Path)) : HighResUpgradeTable.Empty;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// no-fallback: an unreadable file means no upgrades waiting; the books keep their standard pictures
			Debug.WriteLine($"[HighRes] could not read the upgrade list: {ex.Message}");
			return HighResUpgradeTable.Empty;
		}
	}

	private static void Save(HighResUpgradeTable table) => SaveFile(Path, table.ToJson());

	private static void SaveFile(string path, string text)
	{
		try
		{
			string part = path + PartExtension;
			File.WriteAllText(part, text);
			File.Move(part, path, overwrite: true);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// no-fallback: the table stays right in memory for this run; a lost save only means the upgrade is not remembered after a restart
			Debug.WriteLine($"[HighRes] could not save the upgrade list: {ex.Message}");
		}
	}
}

/// <summary>Slice 375: the Debug-only log line of every high-res decision, read by the scripted device checks (<c>[HighRes]</c> markers in the test log); Release writes nothing.</summary>
internal static class HighResLog
{
	public static void Write(string text)
	{
#if DEBUG
		Debug.WriteLine($"[HighRes] {text}");
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[HighRes] {text}");
#endif
	}
}
