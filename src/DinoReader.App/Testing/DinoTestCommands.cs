#if DEBUG
using System.Reflection;
using System.Runtime.CompilerServices;
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Mobile.Shared.Crashes;
using ReadTheStupidText.Mobile.Shared.Testing;

namespace DinoReader.Testing;

/// <summary>
/// Dino Reader's own Debug test command (Slice 175): <c>open-book id=&lt;starter&gt; [page=n] [play=1|page] [speed=2]</c> opens that book's reading screen on page n
/// (1-based, default 1), like a child tapping the tile, and with <c>play=1</c> reads from that page on to the end of the book on the app's own reader (the same session the Play
/// button uses); <c>play=page</c> stops once the page is read, so a timing run is finite. <c>id</c> is the book's package id, its story id or its title as a slug (<c>three-little-pigs</c>). It registers itself with the
/// dispatcher, so no app start-up code changes; Release has none of it. Outcome lines: <c>HOOK open-book opened ...</c> or <c>failed ...</c>;
/// with play, <c>HOOK read start</c> just before the read begins (the line the phone timing script counts from).
/// Slice 189: <c>enter-grown-up</c> puts the app in grown-up mode as a passed gate does (the gate's hold and sum are not scriptable), so a flow can
/// start from the grown-up page; the gate itself is tapped through by hand. Outcome: <c>HOOK enter-grown-up ok</c>.
/// </summary>
internal static partial class DinoTestCommands
{
	private const string Name = "open-book";
	private const string EnterGrownUpName = "enter-grown-up";
	private const string ShelfReorderMoveName = "shelf-reorder-move"; // Slice 264
	private const string ShelfOrderResetName = "shelf-order-reset"; // Slice 264
	private const string StoriesFolderName = "stories"; // same folder as MauiProgram.StoriesFolder
	private const string ReorderFromKey = "from";
	private const string ReorderToKey = "to";
	private const string SetLanguageName = "set-language";
	private const string StageBookUpdateName = "stage-book-update";
	private const string InstallAudioPackName = "install-audio-pack";
	private const string FetchAudioPackName = "fetch-audio-pack";
	private const string ForceFallbackName = "force-fallback";
	private const string MissKey = "miss";
	private const string MissTextMismatch = "text_mismatch";
	private const int MismatchEvery = 3;
	private const string StartedOutcome = "started";
	private const string VoiceKey = "voice";
	private const string VoiceOn = "1";
	private const string ForceKey = "force";
	private const int StoredWaitMilliseconds = 240000;
	private const string IdKey = TestHookDispatcher.IdKey;
	private const string PageKey = "page";
	private const string PlayKey = "play";
	private const string PlayOn = "1";
	private const string PlayPageOnly = "page";
	private const string SpeedKey = "speed";
	private const string VoiceIdKey = "voiceId";
	private const string SessionField = "_session";
	private const int RetryMilliseconds = 250;
	private const int StableChecks = 4;
	private const int StableCheckMilliseconds = 500;
	private const int StableLimitMilliseconds = 20000;
	private const int WaitMilliseconds = 30000;
	private const int ImportWaitMilliseconds = 20000;

	// A module initializer on purpose: the command registers itself, so the app's start-up code (outside this slice) stays untouched.
#pragma warning disable CA2255
	[ModuleInitializer]
#pragma warning restore CA2255
	internal static void Register()
	{
		TestHookDispatcher.AppCommands = Run;
		TestHookDispatcher.AppLaunchKeys = [IdKey, PageKey, PlayKey, VoiceKey, ForceKey, VoiceIdKey, ModeKey, ReorderFromKey, ReorderToKey];
	}

	private static string? Run(TestHookCommand command)
	{
		if (command.Name == SetLanguageName)
		{
			return SetLanguage(command);
		}

		if (command.Name == StageBookUpdateName)
		{
			_ = StageBookUpdateAsync(command.Get(IdKey, string.Empty), command.Flag(ForceKey));
			return StartedOutcome;
		}

		if (command.Name == InstallAudioPackName)
		{
			_ = InstallAudioPackAsync(command.Get(IdKey, string.Empty), command.Get(TestHookDispatcher.LanguageKey, string.Empty), command.Get(MissKey, string.Empty) == MissTextMismatch);
			return StartedOutcome;
		}

		if (command.Name == ForceFallbackName)
		{
			return ForceFallback(command.Get(TestHookDispatcher.LanguageKey, string.Empty), command.Get(IdKey, string.Empty));
		}

		if (command.Name == FetchAudioPackName)
		{
			TrackPackFetch(FetchAudioPackAsync(command.Get(IdKey, string.Empty), command.Get(TestHookDispatcher.LanguageKey, string.Empty)));
			return StartedOutcome;
		}

		if (command.Name == ImportTestBookName)
		{
			_ = ImportTestBookAsync(command.Get(IdKey, string.Empty));
			return StartedOutcome;
		}

		if (command.Name == TestStoreName)
		{
			return SetTestStore(command.Get(ModeKey, string.Empty));
		}

		if (command.Name == ShelfAudioName)
		{
			return StartShelfAudio(command.Get(TestHookDispatcher.LanguageKey, string.Empty));
		}

		if (command.Name == BackupRecordingsName)
		{
			_ = BackupRecordingsAsync(command.Get(IdKey, string.Empty));
			return StartedOutcome;
		}

		if (command.Name == ShelfReorderMoveName)
		{
			_ = ShelfReorderMoveAsync(command);
			return StartedOutcome;
		}

		if (command.Name == ShelfOrderResetName)
		{
			ShelfOrderReset();
			return StartedOutcome;
		}

		if (command.Name == EnterGrownUpName)
		{
			_ = EnterGrownUpAsync();
			return TestHookDispatcher.Ok;
		}

		if (command.Name != Name)
		{
			return null;
		}

		_ = OpenAsync(command);
		return TestHookDispatcher.Ok;
	}

	// Slice 273 part B: sends one speech fallback (neural_not_ready) through the app's registered IFallbackSink, the same object the shared readers and translators report to, so a flow sees the
	// `[Usage] fallback speech:neural_not_ready times=1 queued` line without depending on which voice packs the emulator holds. Logs "HOOK force-fallback ok <lang>".
	// Slice 273 part C: with id=<area>:<reason> it sends that fallback instead (a pack, OCR, store or config one), so a flow proves each area reaches the counter.
	private static string ForceFallback(string language, string kind)
	{
		var sink = IPlatformApplication.Current!.Services.GetRequiredService<ReadTheStupidText.Application.Usage.IFallbackSink>();
		(string area, string reason) = ReadTheStupidText.Application.Usage.FallbackKinds.Split(kind);
		sink.Report(area, reason, language.Length == 0 ? null : language);
		return $"{TestHookDispatcher.Ok} {language}";
	}

	// Slice 209: the Language page's pick path (LanguageChoice.Apply: screens' culture and stored UI language, then SpokenLanguage.Use for the voice pack as a grown-up's tap),
	// then the root rebuilt in the new language as leaving that page does. A code that is not one of the fifteen fails loudly (UiLanguage.Resolve would fall back to English).
	// The voice-pack start (SpokenLanguage.Use) runs only with voice=1: it is known to crash the Debug x86_64 emulator build (see CompleteFirstRun), so the default skips it and a real phone passes voice=1.
	private static string SetLanguage(TestHookCommand command)
	{
		string code = command.Get(TestHookDispatcher.LanguageKey, string.Empty);
		if (!UiLanguage.IsSupported(code))
		{
			return $"{TestHookDispatcher.Failed} {TestHookDispatcher.Unknown} {code}";
		}

		IServiceProvider services = IPlatformApplication.Current!.Services;
		bool withVoice = command.Get(VoiceKey, string.Empty) == VoiceOn;
		if (withVoice)
		{
			SpokenLanguage spoken = services.GetRequiredService<SpokenLanguage>();
			LanguageChoice.Apply(code, spoken, PackStartReason.User);
			_ = LogStoredWhenServedAsync(spoken, UiLanguage.Resolve(code));
		}
		else
		{
			services.GetRequiredService<ISettingsStore>().Language = LanguageChoice.ApplyScreens(code); // as CompleteFirstRun: the stored language is what the state hook reports
		}

		((DinoApp)Microsoft.Maui.Controls.Application.Current!).RebuildRoot();
		return withVoice ? StartedOutcome : $"{TestHookDispatcher.Ok} {UiLanguage.Resolve(code)}";
	}

	// SpokenLanguage.Use stores the language on a worker only after the previous language's pack download has settled (ServeAsync waits for it), so on a phone that was still fetching the last
	// language the stored setting (what the state hook reports) lags the tap. The hook therefore answers ok only once the language is stored, or failed after a bound.
	private static async Task LogStoredWhenServedAsync(SpokenLanguage spoken, string language)
	{
		for (int waited = 0; waited < StoredWaitMilliseconds; waited += RetryMilliseconds)
		{
			if (spoken.HasStored(language))
			{
				Log($"{SetLanguageName} {TestHookDispatcher.Ok} {language}");
				return;
			}

			await Task.Delay(RetryMilliseconds);
		}

		Log($"{SetLanguageName} {TestHookDispatcher.Failed} not stored within {StoredWaitMilliseconds / 1000} s");
	}

	// Slice 209: stages the store's newer version of the book for the next cold start (BookUpdater.StageNowAsync, the staging the 7-day check uses). Logs
	// "HOOK stage-book-update ok <id> v<n>" or "failed <reason>"; the apply is then timed by the [BookUpdate] lines at the next start.
	private static async Task StageBookUpdateAsync(string id, bool force)
	{
		try
		{
			if (id.Trim().Length == 0)
			{
				Log($"{StageBookUpdateName} failed no book");
				return;
			}

			IServiceProvider services = IPlatformApplication.Current!.Services;
			var updater = new BookUpdater(services.GetRequiredService<StoreClient>(), services.GetRequiredService<StarterStories>(), services.GetRequiredService<StagedBookUpdates>());
			string storeId = await StoreIdOfAsync(services.GetRequiredService<IStoryLibrary>(), id.Trim());
			Log($"{StageBookUpdateName} {await Task.Run(() => updater.StageNowAsync(storeId, CancellationToken.None, force))}");
		}
		catch (Exception ex)
		{
			Log($"{StageBookUpdateName} failed {TestHookLog.FaultSummary(ex)}");
		}
	}

	// Slice 246 C2 (Debug only): installs a small fixture audio pack for a book on the shelf, through the real pack store (size check, unzip, completeness check, rename), so a flow can read the
	// book with pre-made clips without the network. Every translated piece of the book's table for the language gets the same short bundled recording (the pig's), keyed by the piece text's hash,
	// so a piece the table lacks (a speaker tag the app adds) is still read by the device voice. Logs "HOOK install-audio-pack ok <book> <lang> v1" or "failed <reason>".
	// Slice 273: miss=text_mismatch stores a wrong hash for every third entry, so those pieces find no clip (ClipMiss.TextMismatch) and the read is partial; no miss option = a complete pack.
	private static async Task InstallAudioPackAsync(string id, string language, bool mismatch = false)
	{
		try
		{
			IServiceProvider services = IPlatformApplication.Current!.Services;
			Story? story = await FindWhenImportedAsync(services.GetRequiredService<IStoryLibrary>(), id.Trim());
			IReadOnlyDictionary<string, string>? table = story is null ? null : BundledTranslations.TableFor(story, language);
			if (story?.SourceId is not { } bookId || table is null)
			{
				Log($"{InstallAudioPackName} failed no book or no table for {language}");
				return;
			}

			string key = BundledTranslations.Key(language);
			string zip = await WriteFixturePackAsync(bookId, key, table.Values, mismatch);
			var pack = new AudioPackInfo(FixtureVersion, new FileInfo(zip).Length, "fixture");
			AudioPackStore packs = services.GetRequiredService<AudioPackStore>();
			await packs.RemoveAsync(bookId, key); // always replace: a pack left by an earlier run must never make this install "already installed"
			PackInstall result = await packs.InstallAsync(bookId, key, pack, null, CancellationToken.None, new FixtureFetcher(zip));
			Log(result == PackInstall.Installed || result == PackInstall.AlreadyInstalled ? $"{InstallAudioPackName} ok {bookId} {key} v{FixtureVersion}" : $"{InstallAudioPackName} failed {result}");
		}
		catch (Exception ex)
		{
			Log($"{InstallAudioPackName} failed {TestHookLog.FaultSummary(ex)}");
		}
	}

	// Slice 258 (Debug only): installs the book's REAL pre-made pack for a language from the live store listing (the route a starter's pack travels, same as a store book's), through the pack
	// store. Logs "HOOK fetch-audio-pack ok <book> <lang> v<n>" or "failed <reason>" (no network, the listing has no pack for the book, an install error).
	private static async Task FetchAudioPackAsync(string id, string language)
	{
		try
		{
			IServiceProvider services = IPlatformApplication.Current!.Services;
			StoreClient store = services.GetRequiredService<StoreClient>();
			string storeId = await StoreIdOfAsync(services.GetRequiredService<IStoryLibrary>(), id.Trim());
			StoreListing listing = await store.ListAsync(CancellationToken.None);
			StoreItem? item = listing.Items.FirstOrDefault(i => string.Equals(i.Id, storeId, StringComparison.OrdinalIgnoreCase));
			string key = BundledTranslations.Key(language);
			AudioPackInfo? pack = item?.AudioIn(key);
			if (item is null || pack is null)
			{
				Log($"{FetchAudioPackName} failed " + (item is null ? $"book {storeId} not in the listing ({listing.Status}, {listing.Items.Count} items)" : $"no listed pack for {storeId} in language '{key}'"));
				return;
			}

			if (await EnsureOnShelfAsync(services.GetRequiredService<IStoryLibrary>(), store, item) is { } refusal)
			{
				Log($"{FetchAudioPackName} failed {refusal}");
				return;
			}

			bool ok = await store.InstallPacksAsync(item, [(key, pack)], null, CancellationToken.None);
			Log(ok ? $"{FetchAudioPackName} ok {storeId} {key} v{pack.Version}" : $"{FetchAudioPackName} failed install");
		}
		catch (Exception ex)
		{
			Log($"{FetchAudioPackName} failed {TestHookLog.FaultSummary(ex)}");
		}
	}

	// A store book that is not on the Debug shelf (the shelf holds only the four test books) is downloaded from the live listing first, so a flow can read it. Null when it is on the shelf, else the reason.
	private static async Task<string?> EnsureOnShelfAsync(IStoryLibrary library, StoreClient store, StoreItem item)
	{
		if (BookMatch.Find(await library.ListAsync(), item.Id) is not null)
		{
			return null;
		}

		using var timeout = new CancellationTokenSource(ShelfDownloadTimeout);
		DownloadOutcome outcome = await store.DownloadAsync(item, new Progress<double>(), timeout.Token);
		return outcome == DownloadOutcome.Done ? null : $"book {item.Id} did not download: {outcome}";
	}

	private static readonly TimeSpan ShelfDownloadTimeout = TimeSpan.FromMinutes(2);

	private const int FixtureVersion = 1;
	private const string FixtureClipName = "fixture.m4a";

	private static async Task<string> WriteFixturePackAsync(string bookId, string language, IEnumerable<string> texts, bool mismatch = false)
	{
		string path = Path.Combine(FileSystem.CacheDirectory, $"fixture-pack-{bookId}-{language}.zip");
		File.Delete(path);
		using (var archive = new System.IO.Compression.ZipArchive(File.Create(path), System.IO.Compression.ZipArchiveMode.Create))
		{
			var entries = texts.Where(t => t.Any(char.IsLetterOrDigit)).Select((t, i) => new { index = i, hash = PremadeAudio.HashOf(mismatch && i % MismatchEvery == 0 ? t + "#" : t), clip = FixtureClipName });
			var manifest = new { version = PremadeAudio.ManifestVersion, book = bookId, language, entries };
			await using (Stream stream = archive.CreateEntry(PremadeAudio.ManifestFile).Open())
			{
				await System.Text.Json.JsonSerializer.SerializeAsync(stream, manifest);
			}

			await using Stream clip = archive.CreateEntry(FixtureClipName).Open();
			await using Stream asset = await FileSystem.OpenAppPackageFileAsync(AnimalSoundClips.AssetNameOf(AnimalKind.Pig));
			await asset.CopyToAsync(clip);
		}

		return path;
	}

	private sealed class FixtureFetcher(string zip) : IAudioPackFetcher
	{
		public Task FetchAsync(string url, string toPath, IProgress<long> bytesSoFar, CancellationToken cancel)
		{
			File.Copy(zip, toPath, overwrite: true);
			return Task.CompletedTask;
		}
	}

	// A slug, title or story id as the other hooks take (BookMatch) becomes the book's catalogue id, which is the store's id; an id the shelf does not know is passed on as typed.
	private static async Task<string> StoreIdOfAsync(IStoryLibrary library, string id) =>
		BookMatch.Find(await library.ListAsync(), id)?.SourceId ?? id;

	// Waits for the gallery to be the window's root (a new window starts in kids' mode and would undo an earlier Enter), then enters grown-up mode on the UI thread.
	// The one-time "Storyteller voices are ready" scrim would cover the gear and the book "..." buttons, so the hook marks it as seen before entering.
	private static void EnterAcknowledgingReadyNote()
	{
		IServiceProvider services = IPlatformApplication.Current!.Services;
		services.GetRequiredService<StorytellerDefault>().AcknowledgeReady();
		services.GetRequiredService<GrownUpMode>().Enter();
	}

	// The window's start-up stop/resume calls can still reset kids' mode: enter, then wait until grown-up mode has stayed on for several checks in a row (entering again when it was reset), within a bound.
	private static async Task<bool> EnterUntilStableAsync()
	{
		GrownUpMode mode = IPlatformApplication.Current!.Services.GetRequiredService<GrownUpMode>();
		DateTime deadline = DateTime.UtcNow.AddMilliseconds(StableLimitMilliseconds);
		int stable = 0;
		while (DateTime.UtcNow < deadline)
		{
			if (!await MainThread.InvokeOnMainThreadAsync(() => mode.IsGrownUp))
			{
				stable = 0;
				await MainThread.InvokeOnMainThreadAsync(EnterAcknowledgingReadyNote);
			}
			else if (++stable >= StableChecks)
			{
				return true;
			}

			await Task.Delay(StableCheckMilliseconds);
		}

		return false;
	}

	// Slice 264: stands in for the drag, which a Maestro swipe cannot make (the system drag starts on a long press). Opens the reorder mode if it is closed, then drops the book at 1-based position
	// from before the one at to, through the same order code a drop uses. Logs "HOOK shelf-reorder-move moved <ids in the new order>" or "failed <reason>".
	private static async Task ShelfReorderMoveAsync(TestHookCommand command)
	{
		try
		{
			NavigationPage? root = await WaitForAsync(() => Task.FromResult(GalleryRoot()));
			if (root?.RootPage is not GalleryPage gallery || !int.TryParse(command.Get(ReorderFromKey, string.Empty), out int from) || !int.TryParse(command.Get(ReorderToKey, string.Empty), out int to))
			{
				Log($"{ShelfReorderMoveName} failed no gallery or bad from/to");
				return;
			}

			string order = string.Empty;
			await MainThread.InvokeOnMainThreadAsync(() => order = gallery.DebugReorderMove(from, to));
			Log(order.Length == 0 ? $"{ShelfReorderMoveName} failed not in grown-up mode or position out of range" : $"{ShelfReorderMoveName} moved {order}");
		}
		catch (Exception ex)
		{
			Log($"{ShelfReorderMoveName} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	// Slice 264: deletes the stored shelf order, so a flow starts from the default order whatever an earlier run saved. The shelf already on screen is not reloaded: the flow restarts the app after this. Logs "HOOK shelf-order-reset ok".
	private static void ShelfOrderReset()
	{
		try
		{
			File.Delete(Path.Combine(FileSystem.AppDataDirectory, StoriesFolderName, ShelfOrder.FileName));
			Log($"{ShelfOrderResetName} ok");
		}
		catch (Exception ex)
		{
			Log($"{ShelfOrderResetName} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	private static async Task EnterGrownUpAsync()
	{
		try
		{
			NavigationPage? root = await WaitForAsync(() => Task.FromResult(GalleryRoot()));
			if (root is null)
			{
				Log($"{EnterGrownUpName} failed no gallery");
				return;
			}

			if (!await EnterUntilStableAsync())
			{
				Log($"{EnterGrownUpName} failed mode kept resetting");
				return;
			}

			Log($"{EnterGrownUpName} entered");
		}
		catch (Exception ex)
		{
			Log($"{EnterGrownUpName} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	private static async Task OpenAsync(TestHookCommand command)
	{
		try
		{
			string id = command.Get(IdKey, string.Empty);
			int page = int.TryParse(command.Get(PageKey, "1"), out int parsed) && parsed >= 1 ? parsed : 1;
			string play = command.Get(PlayKey, string.Empty);
			if (!TrySetSpeed(command.Get(SpeedKey, string.Empty)))
			{
				Log($"{Name} failed bad speed");
				return;
			}

			if (!TrySetVoice(command.Get(VoiceIdKey, string.Empty)))
			{
				return;
			}

			await OpenBookAsync(id, page, play);
		}
		catch (Exception ex)
		{
			Log($"{Name} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	// Item 1 of Slice 221: voiceId=<id> makes the read use that voice, as a voice pick does: the reader's own voice (SetVoice, what a read takes at its start, in this process only) and, when an installed
	// pack lists the voice, that language's narrator (StoryVoicing.SetNarrator, what a book's voiced segments take). The narrator PERSISTS (the same stored setting a Settings pick writes), by design:
	// the hook exists so runs stop editing prefs by hand. A flow that must leave a dev install as it found it ends by setting the voice it started with. The id must be one an installed pack or the
	// built-in Supertonic table offers. Logs "HOOK open-book voice <id>" (the line a check counts) or "failed unknown voice <id>"; no voiceId leaves the voice alone.
	private static bool TrySetVoice(string voiceId)
	{
		if (voiceId.Length == 0)
		{
			return true;
		}

		IServiceProvider services = IPlatformApplication.Current!.Services;
		var voicing = services.GetRequiredService<StoryVoicing>();
		string? packLanguage = PackLanguageOfVoice(voicing, voiceId);
		if (packLanguage is null && !ModelOffersVoice(services, voiceId))
		{
			Log($"{Name} failed {TestHookDispatcher.Unknown} voice {voiceId}");
			return false;
		}

		services.GetRequiredService<ISpeechReader>().SetVoice(voiceId);
		if (packLanguage is not null)
		{
			voicing.SetNarrator(packLanguage, voiceId);
		}

		Log($"{Name} voice {voiceId}");
		return true;
	}

	// The language that itself lists the voice (the current one first, then the story languages), or null: a voice is never registered under a language that does not list it.
	private static string? PackLanguageOfVoice(StoryVoicing voicing, string voiceId)
	{
		IEnumerable<string> languages = voicing.NarratorLanguages().Prepend(LanguageChoice.Current());
		return languages.FirstOrDefault(l => voicing.VoicesFor(l).Any(v => v.Id == voiceId));
	}

	// True when the model in use, or the built-in English table, offers the voice (it can be spoken, but belongs to no language's narrator).
	private static bool ModelOffersVoice(IServiceProvider services, string voiceId)
	{
		IReadOnlyList<VoiceInfo> modelVoices = services.GetRequiredService<IVoiceModelService>().Paths?.AvailableVoices ?? SupertonicVoiceTable.Voices;
		return modelVoices.Any(v => v.Id == voiceId);
	}

	// The reader's own speed, as the read hook sets it (nothing is stored); no speed given leaves it alone.
	private static bool TrySetSpeed(string text)
	{
		if (text.Length == 0)
		{
			return true;
		}

		if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double speed))
		{
			return false;
		}

		IPlatformApplication.Current!.Services.GetRequiredService<ISpeechReader>().SetSpeed(new PlaybackRate(speed));
		return true;
	}

	// A fetch-audio-pack download runs in the background; open-book waits for it (bounded) so the read finds the pack, instead of the flow guessing a delay. Never faults (the fetch catches its own errors).
	// Every unfinished fetch is kept (not just the latest); a finished or failed one removes itself.
	private static readonly System.Collections.Concurrent.ConcurrentDictionary<Task, byte> s_fetchesInFlight = new();
	private static readonly TimeSpan PackFetchWait = TimeSpan.FromSeconds(120);

	private static void TrackPackFetch(Task fetch)
	{
		s_fetchesInFlight[fetch] = 0;
		_ = fetch.ContinueWith(done => s_fetchesInFlight.TryRemove(done, out _), TaskScheduler.Default);
	}

	private static async Task WaitForPackFetchAsync()
	{
		try
		{
			await Task.WhenAll(s_fetchesInFlight.Keys).WaitAsync(PackFetchWait);
		}
		catch (TimeoutException)
		{
			Log($"{FetchAudioPackName} still running after {PackFetchWait.TotalSeconds:0} s");
		}
	}

	private static async Task OpenBookAsync(string id, int page, string play)
	{
		if (id.Trim().Length == 0)
		{
			Log($"{Name} failed no book");
			return;
		}

		await WaitForPackFetchAsync();
		IServiceProvider services = IPlatformApplication.Current!.Services;
		NavigationPage? root = await WaitForAsync(() => Task.FromResult(GalleryRoot()));
		if (root is null)
		{
			Log($"{Name} failed no gallery");
			return;
		}

		string? refusal = Refusal(root, services.GetRequiredService<GrownUpMode>());
		Story? story = refusal is null ? await FindWhenImportedAsync(services.GetRequiredService<IStoryLibrary>(), id) : null;
		if (refusal is not null || story is null)
		{
			Log($"{Name} failed {refusal ?? "no book " + id}");
			return;
		}

		ReadingPage? reading = await MainThread.InvokeOnMainThreadAsync(() => ReadingForAsync(services, root, story));
		if (reading is null)
		{
			Log($"{Name} failed no reading screen");
			return;
		}

		await MainThread.InvokeOnMainThreadAsync(() => GoToPageAsync(reading, story, page, play));
	}

	// The starter books are imported one after another on a cold start, so "no book" only counts once the import had its time (bounded).
	private static Task<Story?> FindWhenImportedAsync(IStoryLibrary library, string id)
	{
		return WaitForAsync(async () => BookMatch.Find(await library.ListAsync(), id), ImportWaitMilliseconds);
	}

	// On the UI thread, the same way a tile tap opens it (StoryActions.ReadAsync); the page it pushed is the top of the stack.
	private static async Task<ReadingPage?> OpenReadingAsync(IServiceProvider services, NavigationPage root, Story story)
	{
		await services.GetRequiredService<StoryActions>().ReadAsync(root.Navigation, story);
		return root.Navigation.NavigationStack.LastOrDefault() as ReadingPage;
	}

	// The states in which a child cannot open a book from the gallery: grown-up mode (a tap edits), the intro wizard over the gallery (a reading screen already open is reused).
	private static string? Refusal(NavigationPage root, GrownUpMode mode)
	{
		if (mode.IsGrownUp)
		{
			return "grown-up";
		}

		if (root.Navigation.ModalStack.Any(p => p is NavigationPage { RootPage: IntroWizardPage }))
		{
			return "intro";
		}

		return null;
	}

	// Slice 177: the reading screen on top of the stack is reused only when it is a ReadingPage loaded for this same book (a second read in the same
	// process, the warm timing run). Any other ReadingPage in the stack (under another page, or for another book) is closed first by going back to the
	// gallery, so a second reading screen is never pushed. On the UI thread, which owns the navigation stack.
	private static async Task<ReadingPage?> ReadingForAsync(IServiceProvider services, NavigationPage root, Story story)
	{
		if (root.Navigation.NavigationStack.LastOrDefault() is ReadingPage top && top.LoadedStoryId == story.Id)
		{
			return top;
		}

		if (root.Navigation.NavigationStack.Any(p => p is ReadingPage))
		{
			await root.Navigation.PopToRootAsync();
		}

		return await OpenReadingAsync(services, root, story);
	}

	// The gallery is the root of the window's navigation once first run is done; before that there is nothing to open a book from.
	private static NavigationPage? GalleryRoot() =>
		Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page is NavigationPage { RootPage: GalleryPage } nav ? nav : null;

	private static async Task<T?> WaitForAsync<T>(Func<Task<T?>> probe, int limitMilliseconds = WaitMilliseconds) where T : class
	{
		for (int waited = 0; waited < limitMilliseconds; waited += RetryMilliseconds)
		{
			if (await probe() is { } found)
			{
				return found;
			}

			await Task.Delay(RetryMilliseconds);
		}

		return null;
	}

	private static async Task GoToPageAsync(ReadingPage reading, Story story, int page, string play)
	{
		(int First, int Last)? range = BookMatch.SentencesOfPage(story, page - 1);
		FieldInfo? field = typeof(ReadingPage).GetField(SessionField, BindingFlags.Instance | BindingFlags.NonPublic);
		if (range is not { } sentences || field?.GetValue(reading) is not ReadingSession session)
		{
			Log($"{Name} failed page {page} of {story.Pages.Count}");
			return;
		}

		Log($"{Name} opened page={page} of={story.Pages.Count} sentence={sentences.First}");
		if (play != PlayOn && play != PlayPageOnly)
		{
			session.MoveTo(sentences.First);
			return;
		}

		if (play == PlayPageOnly)
		{
			StopAfter(session, sentences.Last);
		}

		TestHookLog.NoteReadRequested();
		Log("read start");
		await session.StartAtAsync(sentences.First);
	}

	// Stops the read as soon as it moves past the page's last sentence (once), so a one-page timing run ends by itself.
	private static void StopAfter(ReadingSession session, int lastSentence)
	{
		EventHandler<int>? handler = null;
		handler = (_, sentence) =>
		{
			if (sentence > lastSentence)
			{
				session.SentenceChanged -= handler;
				MainThread.BeginInvokeOnMainThread(() => session.Stop(ReadStopReason.Other));
			}
		};
		session.SentenceChanged += handler;
	}

	private static void Log(string text) => TestHookLog.Write($"{TestHookLog.HookPrefix} {text}");
}
#endif
