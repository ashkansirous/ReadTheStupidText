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
	private const string QuickInstalledFileName = "quick-installed.json"; // Slice 463: which version of a book is installed as the quick package
	private const string QuickPacksFileName = "quick-packs.json"; // Slice 463: which audio packs are installed as the quick copy ("<book id>|<language>" to version)
	private const char PackKeySeparator = '|';

	private readonly object _lock = new();
	private HighResLedger? _ledger;
	private readonly HashSet<string> _quickWrittenThisLaunch = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _quickPacksWrittenThisLaunch = new(StringComparer.OrdinalIgnoreCase);

	private static string InstalledPath => System.IO.Path.Combine(FileSystem.AppDataDirectory, InstalledFileName);

	private static string StagedSizePath => System.IO.Path.Combine(FileSystem.AppDataDirectory, StagedSizeFileName);

	private static string QuickInstalledPath => System.IO.Path.Combine(FileSystem.AppDataDirectory, QuickInstalledFileName);

	private static string QuickPacksPath => System.IO.Path.Combine(FileSystem.AppDataDirectory, QuickPacksFileName);

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
		string[] packKeys = QuickPacks().Where(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)).Select(p => QuickUpgradePlan.PackLedgerKey(p.Id, p.Language)).ToArray();
		ChangeLedger(ledger => packKeys.Aggregate(ledger.Remove(id).Remove(QuickUpgradePlan.LedgerKey(id)), (l, key) => l.Remove(key)), $"{id} removed");
		ForgetInstalledHigh(id);
		ForgetStagedSize(id);
		ForgetInstalledQuick(id);
		ForgetQuickPacks(id);
	}

	/// <summary>The books recorded as installed quick (Slice 463), id to version (a copy; the quick upgrade pass derives from it).</summary>
	public IReadOnlyDictionary<string, int> InstalledQuick()
	{
		lock (_lock)
		{
			return LoadFile(QuickInstalledPath) ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		}
	}

	/// <summary>Whether the copy on the shelf at <paramref name="version"/> is the quick one.</summary>
	public bool InstalledIsQuick(string id, int version) => InstalledQuick().TryGetValue(id, out int have) && have == version;

	/// <summary>Slice 463: the variant of the copy on the shelf at <paramref name="version"/>. The one place that answers it: quick, high-res or standard.</summary>
	public BookVariant InstalledVariant(string id, int version) =>
		InstalledIsQuick(id, version) ? BookVariant.Quick : InstalledIsHigh(id, version) ? BookVariant.High : BookVariant.Standard;

	/// <summary>Slice 463: a quick copy of <paramref name="version"/> was installed (the import succeeded). It is never also high-res.</summary>
	public void RecordInstalledQuick(string id, int version)
	{
		ForgetInstalledHigh(id);
		lock (_lock)
		{
			_quickWrittenThisLaunch.Add(id);
		}

		ChangeFile(QuickInstalledPath, map => map[id] = version);
		HighResLog.Write($"{id}: v{version} installed as {FirstBookSwitch.VariantName(BookVariant.Quick)}");
	}

	/// <summary>Check pass 1, rule 2: the ids whose quick record THIS launch wrote (the launch is the process; a record in the file with no entry here is from an earlier launch). The orphan drop never removes these.</summary>
	public IReadOnlySet<string> QuickWrittenThisLaunch()
	{
		lock (_lock)
		{
			return new HashSet<string>(_quickWrittenThisLaunch, StringComparer.OrdinalIgnoreCase);
		}
	}

	public void ForgetInstalledQuick(string id) => ChangeFile(QuickInstalledPath, map => map.Remove(id));

	/// <summary>Slice 463: the quick copy of a book's pack in <paramref name="language"/> was installed at <paramref name="version"/>; remembered until the standard pack replaces it.</summary>
	public void RecordQuickPack(string id, string language, int version)
	{
		lock (_lock)
		{
			_quickPacksWrittenThisLaunch.Add(PackKey(id, language));
		}

		ChangeFile(QuickPacksPath, map => map[PackKey(id, language)] = version);
	}

	/// <summary>Check pass 2 #1 (rule 2): whether this launch wrote the pack record (its install may still be in flight, so the drop leaves it).</summary>
	public bool QuickPackWrittenThisLaunch(string id, string language)
	{
		lock (_lock)
		{
			return _quickPacksWrittenThisLaunch.Contains(PackKey(id, language));
		}
	}

	public void ForgetQuickPack(string id, string language)
	{
		ChangeFile(QuickPacksPath, map => map.Remove(PackKey(id, language)));
		ChangeLedger(ledger => ledger.Remove(QuickUpgradePlan.PackLedgerKey(id, language)), $"{id}/{language} quick pack forgotten");
	}

	/// <summary>Review round 2 (#1): the standard copy of a quick pack failed to install; counted in the ledger so a broken zip is not fetched again on every pass.</summary>
	public void RecordQuickPackFailure(string id, string language, int version) =>
		ChangeLedger(ledger => ledger.Apply(QuickUpgradePlan.PackLedgerKey(id, language), version, HighResOutcome.BadPackage), $"{id}/{language} quick pack failed v{version}");

	/// <summary>The packs recorded as installed quick: (book id, language) to pack version.</summary>
	public IReadOnlyList<(string Id, string Language, int Version)> QuickPacks()
	{
		lock (_lock)
		{
			return (LoadFile(QuickPacksPath) ?? []).Select(entry => (Parts: entry.Key.Split(PackKeySeparator), entry.Value))
				.Where(x => x.Parts.Length == 2).Select(x => (x.Parts[0], x.Parts[1], x.Value)).ToList();
		}
	}

	private void ForgetQuickPacks(string id) =>
		ChangeFile(QuickPacksPath, map =>
		{
			foreach (string key in map.Keys.Where(key => key.StartsWith(id + PackKeySeparator, StringComparison.OrdinalIgnoreCase)).ToList())
			{
				map.Remove(key);
			}
		});

	private static string PackKey(string id, string language) => $"{id}{PackKeySeparator}{language}";

	/// <summary>
	/// Slice 463: the quick-to-standard upgrade of a book ended as <paramref name="outcome"/>, counted under its own ledger key. Done: the swapped-in copy is standard, so the quick record goes (the high-res pass then
	/// derives the book if the device wants it). Gone: an edited copy is never swapped, so the quick record goes with the attempts. BadPackage: a failure is counted and the copy stays quick.
	/// </summary>
	public void RecordQuickUpgrade(string id, int version, HighResOutcome outcome)
	{
		HighResLog.Write($"{id}: quick upgrade of v{version} outcome {outcome}");
		if (outcome is HighResOutcome.Done or HighResOutcome.Gone)
		{
			ForgetInstalledQuick(id);
		}

		ChangeLedger(ledger => ledger.Apply(QuickUpgradePlan.LedgerKey(id), version, outcome), $"{id} quick {outcome} v{version}");
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
				ForgetInstalledQuick(id); // review round 2 (#5): a standard copy was swapped over whatever was there, so it is no longer the quick one
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
		ForgetInstalledQuick(id); // a standard or high-res copy was installed over whatever was there: it is no longer the quick one
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
