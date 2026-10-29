using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Keeps the <see cref="TranslationPackRecord"/> in preferences: the translation packs a grown-up downloaded. Read by the child's screens instead of
/// the translation engine, so Google ML Kit stays unloaded until a grown-up asks for a translation.
/// </summary>
public sealed class TranslationPackStore
{
	private const string PacksKey = "dino.translation.packs";
	private readonly object _gate = new(); // mutate and save together, so the saved text is always the latest record
	private const string ReconciledKey = "dino.translation.packs.reconciled";
	private const string SeededKey = "dino.translation.packs.seeded";

	/// <summary>The recorded packs (loaded from preferences on first use).</summary>
	public TranslationPackRecord Record { get; } = TranslationPackRecord.Parse(Preferences.Default.Get(PacksKey, (string?)null));

	/// <summary>Whether the record was ever brought in line with the packs on the phone (see <see cref="StoryTranslator.ReconcilePacksAsync"/>).</summary>
	public bool IsReconciled => Preferences.Default.Get(ReconciledKey, false);

	/// <summary>Whether the one-time first-run guess (see <see cref="TranslationPackSeed"/>) was already made.</summary>
	public bool IsSeeded => Preferences.Default.Get(SeededKey, false);

	/// <summary>Called at launch: a fresh install (no language chosen yet) is marked seeded without a guess, so it never gets one later.</summary>
	public void NoteInstall(bool freshInstall)
	{
		if (!TranslationPackSeed.ShouldSeed(freshInstall, IsSeeded, IsReconciled))
		{
			MarkSeeded();
		}
	}

	public void MarkSeeded() => Preferences.Default.Set(SeededKey, true);

	public void MarkReconciled() => Preferences.Default.Set(ReconciledKey, true);

	/// <summary>Records one pack as on the phone or gone, and saves the record when it changed.</summary>
	public void Set(string language, bool ready)
	{
		lock (_gate)
		{
			if (Record.Set(language, ready))
			{
				Save();
			}
		}
	}

	/// <summary>Adds the packs the engine reports (the pack languages as the app spells them); never drops a recorded pack.</summary>
	public void AddRange(IEnumerable<string> languages)
	{
		lock (_gate)
		{
			if (Record.AddRange(languages))
			{
				Save();
			}
		}
	}

	private void Save() => Preferences.Default.Set(PacksKey, Record.Serialize());
}
