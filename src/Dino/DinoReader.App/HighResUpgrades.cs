using System.Diagnostics;
using System.Text.Json;
using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Slice 375 D: what the app remembers about high-res pictures, and nothing more. There is NO list of books waiting for an upgrade (that is derived at each pass, <see cref="HighResUpgradePlan.Derive"/>).
/// Three small files in app data, each written to a temp name and renamed so a kill never leaves half a file, every change under one lock: which version of a book is installed at high-res
/// (<c>high-res-installed.json</c>), which staged update is a high-res one until it is swapped in (<c>high-res-staged-size.json</c>), and the per-book failure ledger
/// (<see cref="HighResLedger"/>, <c>high-res-failures.json</c>). It does no network and no staging: <see cref="HighResUpgradeRunner"/> downloads, <see cref="StagedBookUpdates"/> swaps.
/// </summary>
public sealed class HighResUpgrades
{
	private const string InstalledFileName = "high-res-installed.json";
	private const string StagedSizeFileName = "high-res-staged-size.json";
	private const string LedgerFileName = "high-res-failures.json";

	private readonly object _lock = new();
	private HighResLedger? _ledger;

	private static string InstalledPath => System.IO.Path.Combine(FileSystem.AppDataDirectory, InstalledFileName);

	private static string StagedSizePath => System.IO.Path.Combine(FileSystem.AppDataDirectory, StagedSizeFileName);

	private static string LedgerPath => System.IO.Path.Combine(FileSystem.AppDataDirectory, LedgerFileName);

	/// <summary>The failure ledger as it is now.</summary>
	public HighResLedger Ledger
	{
		get
		{
			lock (_lock)
			{
				return _ledger ?? LoadAndKeepLedger() ?? HighResLedger.Empty; // unreadable now: nothing is cached, the next call reads again
			}
		}
	}

	private HighResLedger? LoadAndKeepLedger() => _ledger = LoadLedger();

	/// <summary>The books recorded as installed at high-res, id to version (a copy; the pass derives from it).</summary>
	public IReadOnlyDictionary<string, int> InstalledHigh()
	{
		lock (_lock)
		{
			return LoadFile(InstalledPath) ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		}
	}

	/// <summary>A book delete: the installed-size record, any remembered staged size and the failure record go.</summary>
	public void Remove(string id)
	{
		ChangeLedger(ledger => ledger.Remove(id), $"{id} removed");
		ForgetInstalledHigh(id);
		ForgetStagedSize(id);
	}

	/// <summary>The outcome rule (<see cref="HighResLedger.Apply"/>): the one way an attempt's end changes the ledger. Done also records that the installed copy is high-res.</summary>
	public void Record(string id, int version, HighResOutcome outcome)
	{
		HighResLog.Write($"{id}: v{version} outcome {outcome}");
		if (outcome == HighResOutcome.Done)
		{
			SetInstalledHigh(id, version);
		}

		ChangeLedger(ledger => ledger.Apply(id, version, outcome), $"{id} {outcome} v{version}");
	}

	/// <summary>Whether the copy on the shelf at <paramref name="version"/> is the high-res one (a same-version hd package is then never swapped again).</summary>
	public bool InstalledIsHigh(string id, int version) => InstalledHigh().TryGetValue(id, out int have) && have == version;

	/// <summary>A staged update of <paramref name="version"/> was fetched at a size: remembered until the swap, so the installed-size record changes when the copy is really swapped in, never at staging.</summary>
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
			if (LoadFile(StagedSizePath) is not { } stagedSizes)
			{
				ForgetInstalledHigh(id); // review round 7 #9: unreadable, so the swapped-in copy is not known to be high-res: the old record is dropped (never kept wrong) and the pass derives the book again
				return;
			}

			bool high = stagedSizes.TryGetValue(id, out int staged) && staged == version;
			RecordInstalledSize(id, version, high);
			ForgetStagedSize(id);
		}
	}

	/// <summary>A copy of <paramref name="version"/> was installed (the import succeeded): remembers a high-res one, forgets the record for any other.</summary>
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
			if (LoadFile(path) is not { } map)
			{
				return; // unreadable: skip this write rather than overwrite the file with one change
			}

			change(map);
			SaveFile(path, JsonSerializer.Serialize(map));
		}
	}

	// Null when the file exists but could not be read (an IO error): the caller must not write then.
	private static Dictionary<string, int>? LoadFile(string path)
	{
		Dictionary<string, int>? map = HighResStateFile.ReadMap(path);
		if (map is null)
		{
			// no-fallback: an unreadable record is left alone, the change is skipped and made again by a later pass
			Debug.WriteLine($"[HighRes] could not read {System.IO.Path.GetFileName(path)}, change skipped");
		}

		return map;
	}

	private void ChangeLedger(Func<HighResLedger, HighResLedger> transition, string what)
	{
		lock (_lock)
		{
			HighResLedger? current = _ledger ?? LoadAndKeepLedger();
			if (current is not { } before)
			{
				return; // unreadable ledger: skip this change rather than save a ledger made from nothing
			}

			HighResLedger after = transition(before);
			if (ReferenceEquals(before, after))
			{
				return;
			}

			_ledger = after;
			SaveFile(LedgerPath, after.ToJson());
			HighResLog.Write($"ledger {what}");
		}
	}

	// Null when the ledger file could not be read (an IO error), so nothing is cached or saved from an empty ledger.
	private static HighResLedger? LoadLedger()
	{
		string? text = HighResStateFile.ReadText(LedgerPath);
		if (text is null)
		{
			// no-fallback: an unreadable ledger is left alone; the next call reads it again
			Debug.WriteLine("[HighRes] could not read the failure ledger, change skipped");
			return null;
		}

		return HighResLedger.FromJson(text);
	}

	private static void SaveFile(string path, string text)
	{
		if (!HighResStateFile.WriteAtomic(path, text))
		{
			// no-fallback: the state stays right in memory for this run where it is kept; a lost save only means it is not remembered after a restart
			Debug.WriteLine($"[HighRes] could not save {System.IO.Path.GetFileName(path)}");
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
