using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using DinoReader.Core;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Mobile.Shared.ModelDownload;
using ReadTheStupidText.Mobile.Shared.Network;

namespace DinoReader;

/// <summary>
/// Translates a story's sentences into the grown-up's language after the speakers were worked out in
/// the story's own language (Slice 66, Decision 74). Each piece is looked up in the story's bundled
/// <see cref="Story.Translations"/> first, then the on-device engine (<see cref="ITranslator"/>, ML Kit on Android and iOS from
/// Mobile.Shared; none on Windows), else it is left as it is (Slice 95). A story with a full bundled table therefore
/// translates on every platform with no engine. A missing engine model never blocks reading.
/// </summary>
public sealed class StoryTranslator
{
	// Google ML Kit (the 2026-10-29 ML Kit-only-for-grown-ups fix) loads only on grown-up paths (Settings, the pack prompt, a save), plus while reading when the
	// pack record already holds every pack of the pair and a table has gaps. The engine is resolved on first use, never in the constructor, and whether this
	// build has one (and its languages) is answered without it.
	private readonly bool _hasEngine;
	private readonly Lazy<ITranslator?> _engineLazy;
	private readonly TranslationPackStore _packStore = new();
	private readonly IServiceProvider _services;
	private const int ReconcileRetryMilliseconds = 60_000; // after a failed pack listing, ask again no sooner than this
	private static readonly TimeSpan ForgetCheckTimeout = TimeSpan.FromSeconds(10);
	private readonly object _reconcileGate = new();
	private Task? _reconcile;
	private long _reconcileFailedAt;
	private readonly ConcurrentDictionary<(string Text, string Source, string Target), string> _cache = new();
	private readonly SharedWork<(string Text, string Source, string Target), string> _inFlight = new(); // the screen and the voice translate the same pieces (Slice 110)

	public StoryTranslator(IServiceProvider services)
	{
		_services = services;
		_hasEngine = (services.GetService(typeof(IServiceProviderIsService)) as IServiceProviderIsService)?.IsService(typeof(ITranslator)) == true;
		_engineLazy = new Lazy<ITranslator?>(ResolveEngine, LazyThreadSafetyMode.PublicationOnly); // a failed start is not cached: the next use tries again
		SeedPackRecordOnce();
	}

	// First run after the record existed: guess the packs an older install has from the story languages (no ML Kit), a grown-up screen's real check corrects it.
	private void SeedPackRecordOnce()
	{
		if (!_hasEngine || _packStore.IsSeeded)
		{
			return;
		}

		// A fresh install was marked seeded at launch (DinoApp), before this runs, so only an upgrade gets here unmarked.
		if (TranslationPackSeed.ShouldSeed(freshInstall: false, _packStore.IsSeeded, _packStore.IsReconciled))
		{
			_packStore.AddRange(TranslationPackSeed.Languages(StoryLanguages.Current().All, OwnBookTranslation.PackLanguages));
		}

		_packStore.MarkSeeded();
	}

	private ITranslator? ResolveEngine()
	{
		var engine = _services.GetService(typeof(ITranslator)) as ITranslator;
		if (engine is ITranslationPacks packs)
		{
			packs.AutoDownload = false; // also set where the engine is registered (MauiProgram), so whoever resolves it first gets it off
		}

#if DEBUG && (ANDROID || IOS)
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write("MLKIT_ENGINE_RESOLVED"); // proof for a device check: the engine loads only on a grown-up path (the 2026-10-29 ML Kit-only-for-grown-ups fix)
#endif
		return engine;
	}

	// Only a grown-up path (a download, a save, a pack check) or a read of a book whose packs a grown-up downloaded may touch these.
	private ITranslator Engine => _engineLazy.Value!;

	// Resolving the engine starts ML Kit (component discovery, reflection): done off the UI thread.
	private async Task<ITranslator> EngineAsync() => (await Task.Run(() => Engine))!;

	private async Task<ITranslationPacks> PacksAsync() => (ITranslationPacks)await EngineAsync();

	// The engine's languages, from a plain table (never the engine itself); null with no engine in this build.
	private IReadOnlyCollection<string>? EngineLanguages => _hasEngine ? MlKitLanguages.All : null;

	/// <summary>Whether this build has a live translation engine (for a story being typed or scanned; a shipped story brings its own table).</summary>
	public bool IsAvailable => _hasEngine;

	private readonly ConditionalWeakTable<Story, ConcurrentDictionary<string, bool>> _complete = new();

	/// <summary>"My language": the grown-up's stored pick, else the device language when it is one of the fifteen. Not <c>CurrentUICulture</c>,
	/// which a language pick only changes for the screens opened afterwards and the reading screen can still see the startup culture.
	/// This is the app language; the grown-up screens and the shelf titles use it. The reading screen passes its own reading language (the child's pick, Slice 173) to every call below.</summary>
	public static string MyLanguage => LanguageChoice.Current();

	/// <summary>Whether reading <paramref name="story"/> in <paramref name="reading"/> goes through the translator.</summary>
	public bool Applies(Story story, string reading) =>
		BundledTranslations.Applies(story, reading, EngineLanguages)
			&& (_hasEngine || TableIsComplete(story, reading));

	/// <summary>Whether the translate switch can do anything for <paramref name="story"/> in <paramref name="reading"/> (a table, a supported engine pair, or nothing to translate).</summary>
	public bool CanTranslate(Story story, string reading) =>
		BundledTranslations.CanTranslate(story, reading, EngineLanguages);

	/// <summary>With no engine a table with holes is not used (see <see cref="TranslateAsync"/>), so the read stays in the story's language; checked once per story instance.</summary>
	private bool TableIsComplete(Story story, string language) =>
		_complete.GetValue(story, _ => new()).GetOrAdd(language, l => BundledTranslations.IsComplete(story, l));

	/// <summary>Whether this read goes through a table the story carries (so it is read in whole sentences).</summary>
	public bool UsesBundledTable(Story story, string reading) => Applies(story, reading) && BundledTranslations.Has(story, reading);

	/// <summary>The language the voice speaks the story in: the reading language when translating, the story's own otherwise.</summary>
	public string SpokenLanguage(Story story, string reading) => Applies(story, reading) ? UiLanguage.Resolve(reading) : UiLanguage.Resolve(story.Language);

	/// <summary>The pieces, and whether their text was translated (false when a missing language model left the story as it was).</summary>
	public sealed record Outcome(IReadOnlyList<SplitSegment> Pieces, bool Translated);

	/// <summary>The pieces with their text translated; a missing language model leaves the story as it was.</summary>
	public async Task<Outcome> TranslateAsync(
		Story story, IReadOnlyList<SplitSegment> pieces, string reading, CancellationToken cancellationToken, bool forSpeech = false)
	{
		string source = UiLanguage.Resolve(story.Language);
		string target = UiLanguage.Resolve(reading);
		bool live = MayUseEngine(source, target);
		if (!live && BundledTranslations.MissesAny(story, target, pieces.Select(p => p.Segment.Text)))
		{
			// A table with holes and nothing to fill them: reading half in one language would be worse than the source.
			return new Outcome(pieces, false);
		}

		PieceTranslator engine = (text, token) => PieceAsync(text, source, target, token);
		try
		{
			PieceTranslator? fallback = live ? engine : null;
			PieceTranslator table = forSpeech ? BundledTranslations.ComposeSpoken(story, target, fallback) : BundledTranslations.Compose(story, target, fallback);
			var translated = await StoryTranslation.TranslateAsync(pieces, table, cancellationToken);
			return new Outcome(translated, true);
		}
		catch (TranslationModelUnavailableException ex)
		{
			Debug.WriteLine($"Dino Reader translation skipped, language model missing: {ex.Message}");
			ForgetGonePacksInBackground(source, target); // the record said ready but the engine says a model is gone; the read does not wait for the check
			return new Outcome(pieces, false);
		}
	}

	/// <summary>
	/// The title the shelf shows (Slice 159): in the grown-up's language, from the book's own table (which holds the title an own book was
	/// translated with) or the shipped-titles table, else the title as it is. Synchronous and offline; the switch, the language pair and the grown-up's "Show original text" decide, as for the text.
	/// </summary>
	public string ShelfTitle(Story story)
	{
		string target = MyLanguage;
		string source = UiLanguage.Resolve(story.Language);
		if (story.ShowOriginalText || !TranslationFlow.ShouldTranslate(story.TranslateToMyLanguage, source, target))
		{
			return story.Title;
		}

		return BundledTranslations.Lookup(story, target, story.Title)
			?? (source == UiLanguage.English ? ShippedTitles.ForBook(story, target) : null)
			?? story.Title;
	}

	/// <summary>The story's title in the grown-up's language for the reading screen (Slice 110): the bundled table's entry, else the engine's, else the title as it is.</summary>
	public async Task<string> TitleAsync(Story story, string reading, CancellationToken cancellationToken)
	{
		string target = UiLanguage.Resolve(reading);
		if (BundledTranslations.Lookup(story, target, story.Title) is { } bundled)
		{
			return bundled;
		}

		if (UiLanguage.Resolve(story.Language) == UiLanguage.English && ShippedTitles.ForBook(story, target) is { } shipped)
		{
			return shipped;
		}

		if (!story.Title.Any(char.IsLetter) || !MayUseEngine(UiLanguage.Resolve(story.Language), target))
		{
			return story.Title;
		}

		try
		{
			string translated = (await PieceAsync(story.Title, UiLanguage.Resolve(story.Language), target, cancellationToken)).Trim();
			return translated.Length > 0 ? translated : story.Title;
		}
		catch (TranslationModelUnavailableException)
		{
			ForgetGonePacksInBackground(UiLanguage.Resolve(story.Language), target);
			return story.Title;
		}
	}

	/// <summary>
	/// Whether <paramref name="story"/> should be translated on the phone into any of its story languages (Slice 173) but is not yet: the switch is on, the
	/// language differs from the book's, the book has no full table for it, and this build has an engine that covers the pair (none on Windows: such a book
	/// stays in its own language).
	/// </summary>
	public bool NeedsOwnTranslation(Story story, IReadOnlyCollection<string>? only = null) => OwnTargets(story, only).Count > 0;

	/// <summary>Whether <paramref name="story"/> still needs its table for <paramref name="language"/> (see <see cref="NeedsOwnTranslation"/>).</summary>
	public bool NeedsOwnTranslationIn(Story story, string language) => OwnTargets(story).Contains(UiLanguage.Resolve(language));

	// The story languages (the app language first) the book still has to be translated into on this phone; only: just these (the reader asks for its own language).
	private List<string> OwnTargets(Story story, IReadOnlyCollection<string>? only = null)
	{
		if (!_hasEngine)
		{
			return [];
		}

		string source = UiLanguage.Resolve(story.Language);
		IReadOnlyCollection<string> supported = MlKitLanguages.All;
		return StoryLanguages.Current().All
			.Where(language => only is null || only.Contains(language))
			.Where(language => TranslationFlow.ShouldTranslate(story.TranslateToMyLanguage, source, language)
				&& TranslationFlow.IsPairSupported(supported, source, language)
				&& !BundledTranslations.IsComplete(story, language)
				&& OwnBookTranslation.Missing(story, language).Count > 0)
			.ToList();
	}

	/// <summary>Whether the child can read <paramref name="story"/> in <paramref name="language"/> right now, with nothing to download (Slice 173): it is the book's own
	/// language, or it carries a table for it, or the translation engine and the packs for the pair are on the phone. False when the parent chose "Show original text" or the book does not translate.</summary>
	public Task<bool> CanReadInAsync(Story story, string language)
	{
		string source = UiLanguage.Resolve(story.Language);
		string target = UiLanguage.Resolve(language);
		return Task.FromResult(ReadingTranslation.CanRead(
			story, language, _hasEngine, MlKitLanguages.All, OwnBookTranslation.PackLanguages(source, target), _packStore.Record));
	}

	/// <summary>The language packs the translations still needed need that are not on the device yet (none when they all are).</summary>
	public async Task<IReadOnlyList<string>> MissingPacksAsync(Story story, IReadOnlyCollection<string>? only = null)
	{
		if (!_hasEngine)
		{
			return [];
		}

		await ReconcilePacksAsync(); // a grown-up path (the pack prompt, the book sheet): the first engine use brings the record up to date
		return await MissingPacksForAsync(UiLanguage.Resolve(story.Language), OwnTargets(story, only));
	}

	/// <summary>The story languages the book still needs a table for whose packs are all on the phone now (so each can be translated, whatever happened to the others).</summary>
	public async Task<IReadOnlyList<string>> TranslatableTargetsAsync(Story story, IReadOnlyCollection<string>? only = null)
	{
		var ready = new List<string>();
		string source = UiLanguage.Resolve(story.Language);
		await ReconcilePacksAsync();
		HashSet<string> downloaded = await DownloadedSetAsync(CancellationToken.None);
		foreach (string target in OwnTargets(story, only))
		{
			if (MissingPacksFrom(downloaded, source, [target]).Count == 0)
			{
				ready.Add(target);
			}
		}

		return ready;
	}

	private async Task<IReadOnlyList<string>> MissingPacksForAsync(string source, IEnumerable<string> targets)
	{
		return MissingPacksFrom(await DownloadedSetAsync(CancellationToken.None), source, targets);
	}

	// One question to the engine for the whole check, not one per pack.
	private async Task<HashSet<string>> DownloadedSetAsync(CancellationToken cancellationToken)
	{
		ITranslationPacks packs = await PacksAsync();
		IReadOnlyCollection<string> downloaded = await packs.DownloadedPacksAsync().WaitAsync(cancellationToken);
		return new HashSet<string>(downloaded, StringComparer.OrdinalIgnoreCase);
	}

	// A grown-up path: the engine's list is the truth, and the record follows what it says.
	private IReadOnlyList<string> MissingPacksFrom(HashSet<string> downloaded, string source, IEnumerable<string> targets)
	{
		var missing = new List<string>();
		foreach (string language in targets.SelectMany(target => OwnBookTranslation.PackLanguages(source, target)).Distinct())
		{
			bool ready = downloaded.Contains(language);
			_packStore.Set(language, ready);
			if (!ready)
			{
				missing.Add(language);
			}
		}

		return missing;
	}

	/// <summary>
	/// Downloads the given packs one by one; <paramref name="progress"/> gets 0..1 across them. One pack failing does not stop the others (a cancel does); the languages
	/// whose pack could not be fetched are returned. Each pack runs in a download session (Slice 187): with no internet it waits and carries on by itself;
	/// <paramref name="observe"/>, when given, gets each pack's language and session and returns what stops the display of it.
	/// </summary>
	public async Task<IReadOnlyList<string>> DownloadPacksAsync(
		IReadOnlyList<string> languages, IProgress<double>? progress, CancellationToken cancellationToken, Func<string, TranslationModelDownloadSession, IDisposable?>? observe = null)
	{
		var failed = new List<string>();
		await ReconcilePacksAsync();
		ITranslationPacks packs = await PacksAsync();
		for (int i = 0; i < languages.Count; i++)
		{
			try
			{
				await DownloadPackInSessionAsync(packs, languages[i], observe, cancellationToken);
				_packStore.Set(languages[i], true);
			}
			catch (TranslationModelUnavailableException ex)
			{
				Debug.WriteLine($"Dino Reader could not get the {languages[i]} translation pack: {ex.Message}");
				_packStore.Set(languages[i], false);
				failed.Add(languages[i]);
			}

			progress?.Report((i + 1) / (double)languages.Count);
		}

		return failed;
	}

	private async Task DownloadPackInSessionAsync(
		ITranslationPacks packs, string language, Func<string, TranslationModelDownloadSession, IDisposable?>? observe, CancellationToken cancellationToken)
	{
		using TranslationModelDownloadSession session = ConnectivityTranslationNetwork.NewSession(
			TranslationModelDownloadSession.PackKey(EngineLanguageCodes.ToEngine(language)),
			(progress, attemptToken) => packs.DownloadPackAsync(language, progress, attemptToken),
			_services.GetRequiredService<PackResumeWatcher>());
		using IDisposable? view = observe?.Invoke(language, session);
		await session.RunAsync(cancellationToken);
	}

	/// <summary>
	/// Translates what <paramref name="story"/> misses in each of <paramref name="targets"/> (text and title) and saves it inside the book, so the shelf and the reader show it offline afterwards.
	/// One language failing does not undo the others. False only when no table could be saved at all (a pack is missing); the book is then left as it was.
	/// </summary>
	public async Task<bool> SaveOwnTranslationAsync(Story story, IReadOnlyList<string> targets, CancellationToken cancellationToken)
	{
		IStoryLibrary? library = _services.GetService(typeof(IStoryLibrary)) as IStoryLibrary;
		if (!_hasEngine || library is null)
		{
			return false;
		}

		await ReconcilePacksAsync();
		bool any = false;
		foreach (string language in targets)
		{
			any |= await SaveOwnTranslationAsync(story, language, library, cancellationToken);
		}

		return any;
	}

	private async Task<bool> SaveOwnTranslationAsync(Story story, string language, IStoryLibrary library, CancellationToken cancellationToken)
	{
		string source = UiLanguage.Resolve(story.Language);
		var table = new Dictionary<string, string>(BundledTranslations.TableFor(story, language) ?? new Dictionary<string, string>(), StringComparer.Ordinal);
		try
		{
			foreach (string text in OwnBookTranslation.Missing(story, language))
			{
				string translated = (await PieceAsync(text, source, language, cancellationToken)).Trim();
				table[text] = translated.Length > 0 ? translated : text;
			}
		}
		catch (TranslationModelUnavailableException ex)
		{
			Debug.WriteLine($"Dino Reader could not save a translation, language model missing: {ex.Message}");
			await ForgetGonePacksAsync(source, language, CancellationToken.None);
			return false;
		}

		return await StoreTableAsync(library, story.Id, language, table);
	}

	// Applied to the book as it is on disk now, so an edit saved meanwhile is kept; only the language's table changes.
	private static async Task<bool> StoreTableAsync(IStoryLibrary library, string id, string language, IReadOnlyDictionary<string, string> table)
	{
		if (await library.GetAsync(id) is not { } current)
		{
			return false;
		}

		await library.UpdateAsync(OwnBookTranslation.WithTable(current, language, table));
		return true;
	}

	// The engine may translate on a read only when a grown-up downloaded every pack of the pair (the record says so); otherwise ML Kit stays unloaded.
	private bool MayUseEngine(string source, string target) =>
		ReadingTranslation.MayUseEngine(_hasEngine, OwnBookTranslation.PackLanguages(source, target), _packStore.Record);

	// The read falls back to untranslated at once; the check runs behind it, bounded, and never throws.
	private void ForgetGonePacksInBackground(string source, string target)
	{
		_ = Task.Run(async () =>
		{
			using var timeout = new CancellationTokenSource(ForgetCheckTimeout);
			await ForgetGonePacksAsync(source, target, timeout.Token);
		});
	}

	// The engine reported a missing model for the pair, which can also be a passing failure: ask the engine once and forget only the packs it says are gone.
	// If the check itself fails or times out, nothing is forgotten.
	private async Task ForgetGonePacksAsync(string source, string target, CancellationToken cancellationToken)
	{
		try
		{
			HashSet<string> downloaded = await DownloadedSetAsync(cancellationToken);
			foreach (string language in OwnBookTranslation.PackLanguages(source, target).Where(language => !downloaded.Contains(language)))
			{
				_packStore.Set(language, false);
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not re-check the translation packs: {ex.Message}");
		}
	}

	/// <summary>
	/// Adds the packs on the phone to the pack record, once, for an install that downloaded packs before the record existed. Only grown-up paths call it (Settings,
	/// Story languages, the pack prompt, a download or a save), because it loads ML Kit; the child's screens never do. It only adds, so a pack recorded
	/// meanwhile is never dropped.
	/// </summary>
	public Task ReconcilePacksAsync()
	{
		if (!_hasEngine || _packStore.IsReconciled)
		{
			return Task.CompletedTask;
		}

		lock (_reconcileGate)
		{
			if (_reconcile is { IsCompleted: false })
			{
				return _reconcile; // concurrent callers share the one query
			}

			if (_reconcileFailedAt != 0 && Environment.TickCount64 - _reconcileFailedAt < ReconcileRetryMilliseconds)
			{
				return Task.CompletedTask; // it failed a moment ago: not again on every call
			}

			_reconcile = ReconcileOnceAsync();
			return _reconcile;
		}
	}

	private async Task ReconcileOnceAsync()
	{
		try
		{
			_packStore.AddRange(await (await PacksAsync()).DownloadedPacksAsync());
			_packStore.MarkReconciled();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not list the translation packs: {ex.Message}");
			lock (_reconcileGate)
			{
				_reconcileFailedAt = Environment.TickCount64;
			}
		}
	}

	private async Task<string> PieceAsync(string text, string source, string target, CancellationToken cancellationToken)
	{
		if (_cache.TryGetValue((text, source, target), out string? known))
		{
			return known;
		}

		var key = (text, source, target);
		return await _inFlight.RunAsync(key, async token =>
		{
			string translated = await (await EngineAsync()).TranslateAsync(text, source, target, token);
			_cache[key] = translated;
			return translated;
		}, cancellationToken);
	}
}
