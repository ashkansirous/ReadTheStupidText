using System.Collections.Concurrent;
using System.Diagnostics;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;

namespace DinoReader;

/// <summary>
/// Turns a store book's text into the pieces a read plays (Slice 60) and finds each piece's pre-made clip (Slice 246 C2). Who speaks is worked out once, in the story's own language
/// (its <see cref="LanguageRules"/>, Slice 66), before any translation, so the pieces are the ones the book builder cut the clips for. Slice 278 part B: no voice is cast, chosen or
/// spoken any more; a piece is a <see cref="VoicedSegment"/> with no voice, marked with its clip's file when the book's audio pack has one. A piece with no clip is not spoken
/// (<see cref="ReadingSession"/> shows it for a beat). A saved book's stored cast is still read, only to find the same characters the pieces were cut at.
/// </summary>
public sealed class StoryVoicing
{
	/// <summary>The voice id of every segment: none exists, the player plays files.</summary>
	private const string NoVoiceId = "";

	private readonly AudioPackStore _audioPacks; // the pre-made audio of store books and bundled starters (Slice 246 C2, 258)
	private readonly StoryTranslator _translator;

	// One segmenter and attributor per language (their rules are shared per language too), built on first use.
	private readonly ConcurrentDictionary<string, LanguagePipeline> _pipelines = new();

	private sealed record LanguagePipeline(LanguageRules Rules, StorySegmenter Segmenter, SpeakerAttributor Attributor);

	// One immutable snapshot, swapped atomically: attributing a long story is the costly part, and a second read of the same text does not need it again.
	// The sentences are only built when a read asks for them.
	private sealed record Cache(string Language, string Text, LineBreakMap LineBreaks, AttributionResult Attribution, Lazy<IReadOnlyList<SentenceSpan>> Sentences);

	// The analysis and the sentence pieces a read plays, before any translation.
	private sealed record Prepared(Cache Analysis, IReadOnlyList<SplitSegment> Pieces, bool WholeBook);

	private sealed record SharedPrepared(Story Story, bool WholePieces, Prepared Prepared);

	private volatile Cache? _cache;
	private volatile SharedPrepared? _lastPrepared;

	// The animal-sound plan of the last story and analysis: a tap-to-seek or resume read from the middle needs the whole book's plan, and re-splitting the book each time is wasted.
	private sealed record SharedPlan(Story Story, Cache Analysis, string Language, IReadOnlyDictionary<int, AnimalKind> Plan);

	private volatile SharedPlan? _lastPlan;

	public StoryVoicing(StoryTranslator translator, AudioPackStore audioPacks)
	{
		_audioPacks = audioPacks;
		_translator = translator;
	}

	/// <summary>
	/// The pieces of the story from <paramref name="fromPosition"/> on, in the reading language, each marked with its clip when the book's pack has one. On a worker thread: segmenting
	/// and attributing a long story is CPU work that must not block the UI. The story is translated afterwards when it is set to be (Slice 66).
	/// </summary>
	public async Task<IReadOnlyList<VoicedSegment>> VoiceAsync(
		Story story, string reading, int fromPosition = 0, CancellationToken cancellationToken = default, Action? translationSkipped = null, VoiceWhy? why = null)
	{
		return await VoiceCoreAsync(story, reading, fromPosition, cancellationToken, translationSkipped, why ?? new VoiceWhy());
	}

	/// <summary>Slice 267: why a read found no usable pre-made audio. One per read, filled while that read is voiced and passed to <see cref="ReportVoiceUsed"/>, so overlapping reads never share it.</summary>
	public sealed class VoiceWhy
	{
		public DeviceVoiceReason Reason { get; set; } = DeviceVoiceReason.NoPack;

		/// <summary>Slice 273: how this read's pieces found their clips; null while no usable pack was opened for it (no clip was looked for).</summary>
		public ClipMisses? Misses { get; set; }
	}

	/// <summary>Slice 273: whether a read plays any piece by its clip. A read of pieces that are only shown for a beat has no voice to count: its pieces each report the existing clip fallback instead (Slice 278 part B review).</summary>
	public static bool SpeaksAnyPiece(IReadOnlyList<VoicedSegment> segments)
	{
		return segments.Any(s => PremadeClipMarker.PathOf(s) is not null);
	}

	/// <summary>One line in the rolling log, and the <see cref="AudioPackUsage.ReadVoice"/> detail (<c>premade</c>, or <c>device:&lt;reason&gt;</c>) for the caller to count: whether every piece had a clip, and when a piece had none, why. The caller calls it once a read has actually started. The "device" pieces are the ones with no clip (Slice 278: no device voice reads them, they are shown for a beat).</summary>
	public ReadVoiceUse ReportVoiceUsed(Story story, IReadOnlyList<VoicedSegment> segments, VoiceWhy why)
	{
		int clips = segments.Count(s => PremadeClipMarker.PathOf(s) is not null);
		int device = segments.Count(s => PremadeClipMarker.PathOf(s) is null && s.Transform is not AnimalClipMarker && PremadeAudio.IsSpoken(s.Text));
		bool missRecorded = why.Misses is { TextMismatch: > 0 } or { FileMissing: > 0 };
		DeviceVoiceReason reason = clips == 0 && missRecorded ? why.Misses!.Reason : why.Reason; // with no miss recorded the read's own reason stands
		ReadVoiceUse use = AudioPackUsage.ReadVoice(clips, device, reason, why.Misses?.TextMismatch ?? 0, why.Misses?.FileMissing ?? 0);
		if (clips == 0 && why.Misses is not null && !missRecorded)
		{
			use = use with { FallbackReason = null }; // a pack was opened but no clip was looked up (nothing spoken was checked): not a fallback
		}

		AudioPackLog.Write($"{story.SourceId ?? UsageSource.Own}: read voice {use.Detail} ({clips} pieces by clip, {device} with no clip)");
		return use;
	}

	private async Task<IReadOnlyList<VoicedSegment>> VoiceCoreAsync(
		Story story, string reading, int fromPosition, CancellationToken cancellationToken, Action? translationSkipped, VoiceWhy why)
	{
		bool wholePieces = _translator.UsesBundledTable(story, reading);
		Prepared prepared = await Task.Run(() => fromPosition == 0 ? PrepareShared(story, wholePieces) : Prepare(story, fromPosition, wholePieces), cancellationToken);
		if (!_translator.Applies(story, reading))
		{
			return await VoicedOwnWithPremadeAsync(story, prepared, fromPosition, wholePieces, reading, why, cancellationToken);
		}

		StoryTranslator.Outcome outcome = await TranslateOffUiAsync(story, prepared, reading, cancellationToken, forSpeech: true);
		if (!outcome.Translated)
		{
			// The story is read as written, in its own language.
			translationSkipped?.Invoke();
			return await VoicedOwnWithPremadeAsync(story, prepared, fromPosition, wholePieces, reading, why, cancellationToken);
		}

		PremadeSource? premade = await PremadeSourceAsync(story, prepared, fromPosition, wholePieces, outcome, reading, why, cancellationToken);
		return await Task.Run(() => VoicedTranslated(story, prepared, outcome.Pieces, reading, premade), cancellationToken);
	}

	// The story read in its own language (the reading language is the story's, or translation was skipped): its pre-made audio for the story's language is used too (Slice 246 C2).
	private async Task<IReadOnlyList<VoicedSegment>> VoicedOwnWithPremadeAsync(
		Story story, Prepared prepared, int fromPosition, bool wholePieces, string reading, VoiceWhy why, CancellationToken cancellationToken)
	{
		PremadeSource? premade = await PremadeSourceAsync(story, prepared, fromPosition, wholePieces, null, reading, why, cancellationToken);
		return await Task.Run(() => VoicedInOwnLanguage(story, prepared, premade), cancellationToken);
	}

	/// <summary>
	/// The installed pre-made audio of a store book or bundled starter for the language it is read in, with the plain (displayed) text of each piece (Slice 246 C2). A piece's clip is the pack's entry at the piece's
	/// position among the pieces with a letter or digit (<see cref="Offset"/> when the read starts mid-book), confirmed by the hash of the plain text; null when there is none.
	/// </summary>
	private sealed record PremadeSource(InstalledPack Pack, IReadOnlyList<SplitSegment> PlainPieces, int Offset, ClipMisses Misses)
	{
		public string? PathOf(int piece)
		{
			string text = PlainPieces[piece].Segment.Text;
			if (!PremadeAudio.IsSpoken(text))
			{
				return null;
			}

			ClipLookup found = Pack.Find(Offset + PremadeAudio.SpokenCount(PlainPieces.Take(piece).Select(p => p.Segment.Text)), text);
			Misses.Note(piece, found.Miss);
			return found.Path;
		}

		public static int SpokenCount(IReadOnlyList<SplitSegment> pieces) => PremadeAudio.SpokenCount(pieces.Select(p => p.Segment.Text));
	}

	// The clips are keyed by the plain text, but the pieces a translated read plays carry the speak: table's marks, so the plain pieces are translated again (a table lookup); an own-language read
	// plays the plain pieces as they are. Only a book with a source id (a store book or a bundled starter, Slice 258) can have a pack; a translated read needs its own complete table. A mismatch in the piece count means no clips.
	private async Task<PremadeSource?> PremadeSourceAsync(
		Story story, Prepared prepared, int fromPosition, bool wholePieces, StoryTranslator.Outcome? spoken, string reading, VoiceWhy why, CancellationToken cancellationToken)
	{
		bool translated = spoken is not null;
		why.Misses = null;
		if (story.SourceId is not { } bookId)
		{
			return null; // a book with no source id has no pack: the default reason, no_pack
		}

		if (translated && !_translator.UsesBundledTable(story, reading))
		{
			why.Reason = DeviceVoiceReason.TextMismatch; // a translation that is not the book's own table never lines up with a pack's clips; not a missing or failed pack
			return null;
		}

		string language = BundledTranslations.Key(translated ? _translator.SpokenLanguage(story, reading) : story.Language);
		InstalledPack? pack = _audioPacks.Open(bookId, language);
		if (pack is null)
		{
			why.Reason = _audioPacks.FailedThisLaunch(bookId, language) ? DeviceVoiceReason.PackFailed : DeviceVoiceReason.NoPack;
			return null;
		}

		why.Reason = DeviceVoiceReason.TextMismatch;
		IReadOnlyList<SplitSegment>? plain = await PlainPiecesAsync(story, prepared, reading, translated, cancellationToken);
		if (plain is null || (spoken is not null && plain.Count != spoken.Pieces.Count))
		{
			AudioPackLog.Write($"{bookId}/{language}: the plain and spoken pieces do not line up, no clips for the whole read");
			return null;
		}

		int offset = fromPosition == 0 ? 0 : await ClipOffsetAsync(story, plain, wholePieces, reading, translated, cancellationToken);
		why.Reason = offset < 0 ? DeviceVoiceReason.TextMismatch : DeviceVoiceReason.ClipMissing; // a usable source whose pieces then find no clip is a missing clip
		if (offset < 0)
		{
			return null;
		}

		why.Misses = new ClipMisses(); // one tally per voicing of this read, so a second voicing never adds to the first
		return new PremadeSource(pack, plain, offset, why.Misses);
	}

	private async Task<IReadOnlyList<SplitSegment>?> PlainPiecesAsync(Story story, Prepared prepared, string reading, bool translated, CancellationToken cancellationToken)
	{
		if (!translated)
		{
			return prepared.Pieces;
		}

		StoryTranslator.Outcome plain = await TranslateOffUiAsync(story, prepared, reading, cancellationToken);
		return plain.Translated ? plain.Pieces : null;
	}

	// Slice 259 item 1: a read's translation (a mid-book read works out the whole book's pieces again for its clips) runs on a worker thread. Called on the UI thread, a book's worth of
	// table lookups that finish at once would hold the UI thread and keep the page's 500 ms wait timer from ever firing, so the loading state could not show however long the wait was.
	private Task<StoryTranslator.Outcome> TranslateOffUiAsync(Story story, Prepared prepared, string reading, CancellationToken cancellationToken, bool forSpeech = false)
	{
		return Task.Run(() => _translator.TranslateAsync(story, prepared.Pieces, prepared.Analysis.Sentences.Value, reading, cancellationToken, forSpeech), cancellationToken);
	}

	// A read from the middle starts at some piece of the book: its clips are the pack's entries after the spoken pieces before it. Negative when the whole book's pieces cannot be worked out.
	private async Task<int> ClipOffsetAsync(Story story, IReadOnlyList<SplitSegment> partial, bool wholePieces, string reading, bool translated, CancellationToken cancellationToken)
	{
		var watch = Stopwatch.StartNew();
		Prepared whole = await Task.Run(() => PrepareShared(story, wholePieces), cancellationToken);
		IReadOnlyList<SplitSegment>? all = await PlainPiecesAsync(story, whole, reading, translated, cancellationToken);
		ReadStartTrace.Write($"whole book worked out for the clip offset in {watch.ElapsedMilliseconds} ms ({all?.Count ?? 0} pieces, translated={translated})");
		return all is null ? -1 : PremadeSource.SpokenCount(all) - PremadeSource.SpokenCount(partial);
	}

	// A piece whose plain text has a clip in the pack is marked with its file; the read plays it. The coverage is logged once per read (Debug builds reach the test-hook log).
	private static VoicedSegment WithPremade(VoicedSegment voiced, PremadeSource? premade, int piece)
	{
		string? path = premade?.PathOf(piece);
		return path is null ? voiced : PremadeClipMarker.Mark(voiced, path);
	}

	/// <summary>The story's text in the language it is read in, for the reading screen (Slice 110, Decision 112); null when the screen shows the story as it is:
	/// the story is not translated now. <paramref name="sentences"/> are the
	/// story's sentences, which the result keeps one-to-one.</summary>
	public async Task<TranslatedStory?> TranslatedDisplayAsync(Story story, IReadOnlyList<SentenceSpan> sentences, string reading, CancellationToken cancellationToken = default)
	{
		if (!_translator.Applies(story, reading))
		{
			return null;
		}

		Prepared prepared = await Task.Run(() => PrepareShared(story, _translator.UsesBundledTable(story, reading)), cancellationToken);
		// The engine's pieces are shared with a read that plays them at the same time (StoryTranslator), so each is translated once.
		StoryTranslator.Outcome outcome = await TranslateOffUiAsync(story, prepared, reading, cancellationToken);
		return outcome.Translated ? TranslatedStoryBuilder.Build(story.Pages, sentences, outcome.Pieces, StoryRecordings.PagesShownInOwnLanguage(story)) : null;
	}

	// A translated read plays the spoken language's pieces and clips.
	private IReadOnlyList<VoicedSegment> VoicedTranslated(Story story, Prepared prepared, IReadOnlyList<SplitSegment> pieces, string reading, PremadeSource? premade)
	{
		string spoken = _translator.SpokenLanguage(story, reading);
		return WithAnimalSounds(story, prepared, pieces, spoken, premade);
	}

	private IReadOnlyList<VoicedSegment> VoicedInOwnLanguage(Story story, Prepared prepared, PremadeSource? premade = null)
	{
		return WithAnimalSounds(story, prepared, prepared.Pieces, story.Language, premade);
	}

	// Animal characters say their sound after every 2nd or 3rd line of their own (Slice 117, Decision 123): one extra short segment, a slot where the animal's recording plays
	// (it has no text on the page). The lines are counted over the whole book in the story's language, so a read from the middle sounds like a read from the start.
	// The sound sits right after the line's last quote piece, never after a dialogue tag that follows it (Slice 254), and after the last piece of a translated line.
	private IReadOnlyList<VoicedSegment> WithAnimalSounds(
		Story story, Prepared prepared, IReadOnlyList<SplitSegment> pieces, string spokenLanguage, PremadeSource? premade)
	{
		IReadOnlyDictionary<int, AnimalSounds.Placement> sounds = AnimalSoundPlacements(story, prepared, pieces, spokenLanguage).ToDictionary(p => p.After);
		var voiced = new List<VoicedSegment>(pieces.Count + sounds.Count);
		for (int i = 0; i < pieces.Count; i++)
		{
			voiced.Add(WithPremade(ToVoicedSegment(pieces[i]), premade, i));
			if (sounds.TryGetValue(i, out AnimalSounds.Placement? placement) && AnimalSoundAfter(pieces, placement, spokenLanguage) is { } sound)
			{
				voiced.Add(sound);
			}
		}

		if (premade is not null)
		{
			AudioPackLog.Write($"{story.SourceId}/{premade.Pack.Audio.Language} v{premade.Pack.Version}: {voiced.Count(s => PremadeClipMarker.PathOf(s) is not null)} of {pieces.Count} pieces have a clip");
		}

		return voiced;
	}

	private IReadOnlyList<AnimalSounds.Placement> AnimalSoundPlacements(Story story, Prepared prepared, IReadOnlyList<SplitSegment> pieces, string spokenLanguage)
	{
		// The grown-up's one app-wide switch (Slice 152); read at every read, never cached with the plan.
		IReadOnlyDictionary<int, AnimalKind> plan = AnimalSounds.Audible(AnimalPlan(story, prepared, spokenLanguage), AnimalSoundsPreference.Load());
		return plan.Count == 0 ? [] : AnimalSounds.Placements(pieces, plan);
	}

	// The plan counts the story's own lines, in the order the read plays them (speaker first puts the two quotes of a quote-tag-quote line side by side, so they count as one line);
	// a read that is the whole book already has exactly those pieces. Kept for the same story instance and analysis.
	private IReadOnlyDictionary<int, AnimalKind> AnimalPlan(Story story, Prepared prepared, string spokenLanguage)
	{
		if (_lastPlan is { } last && ReferenceEquals(last.Story, story) && ReferenceEquals(last.Analysis, prepared.Analysis) && last.Language == spokenLanguage)
		{
			return last.Plan;
		}

		AttributionResult resolved = StoryCast.Resolve(prepared.Analysis.Attribution, story.Cast);
		IReadOnlyDictionary<string, AnimalKind> animals = AnimalSoundClips.WithClip(AnimalsOf(story, resolved.Characters, spokenLanguage));
		IReadOnlyDictionary<int, AnimalKind> plan = animals.Count == 0
			? new Dictionary<int, AnimalKind>()
			: AnimalSounds.Plan(SpokenOrder(story, prepared, WholeBookPieces(story, prepared, resolved), spokenLanguage), animals, story.Language);
		_lastPlan = new SharedPlan(story, prepared.Analysis, spokenLanguage, plan);
		return plan;
	}

	// Only a translated read is regrouped: a failed or skipped translation is read in the story's own language (VoicedInOwnLanguage), which keeps its order.
	private static IReadOnlyList<SplitSegment> SpokenOrder(Story story, Prepared prepared, IReadOnlyList<SplitSegment> pieces, string spokenLanguage) =>
		SpeakerFirst.Regroups(spokenLanguage, story.Language, translated: spokenLanguage != UiLanguage.Resolve(story.Language))
			? SpeakerFirst.SourceOrder(pieces, spokenLanguage, story.Text, prepared.Analysis.Sentences.Value)
			: pieces;

	private static IReadOnlyList<SplitSegment> WholeBookPieces(Story story, Prepared prepared, AttributionResult resolved) =>
		prepared.WholeBook ? prepared.Pieces : StoryPieces.Split(story, resolved, prepared.Analysis.Sentences.Value, 0);

	// The sound segment, a zero-width one at the last character of the line's last quote piece (its dialogue tag is not included, Slice 254); none when the line already says it.
	private static VoicedSegment? AnimalSoundAfter(IReadOnlyList<SplitSegment> pieces, AnimalSounds.Placement placement, string spokenLanguage)
	{
		Segment quote = pieces[placement.Quote].Segment;
		// Slice 255: the slot carries a marker, and the reading session plays the animal's recording there. The recording is the same in every language,
		// so the slot depends on the animal having a recording, not on a sound word in the spoken language (which is only a label here).
		string sound = AnimalSounds.SoundOf(placement.Kind, spokenLanguage) ?? placement.Kind.ToString();
		if (!AnimalSoundClips.Has(placement.Kind) || AnimalSounds.SaysIt(LineText(pieces, placement.Quote), placement.Kind, spokenLanguage))
		{
			return null;
		}

		int at = Math.Max(quote.SourceStart, pieces[placement.After].Segment.SourceEnd - 1);
		return new VoicedSegment(sound, NoVoiceId, at, at, at, new AnimalClipMarker(placement.Kind));
	}

	// The text of the line the piece at <paramref name="last"/> ends: the consecutive pieces of the same character before it.
	private static string LineText(IReadOnlyList<SplitSegment> pieces, int last)
	{
		int first = last;
		while (first > 0 && pieces[first - 1].Segment.Kind == SegmentKind.Quote && pieces[first - 1].Segment.CharacterId == pieces[last].Segment.CharacterId)
		{
			first--;
		}

		return string.Join(' ', pieces.Skip(first).Take(last - first + 1).Select(p => p.Segment.Text));
	}

	/// <summary>The language a read of <paramref name="story"/> in <paramref name="reading"/> plays in: the reading language when it is translated, the story's own otherwise.</summary>
	public string ReadingLanguage(Story story, string reading) => _translator.SpokenLanguage(story, reading);

	/// <summary>
	/// The animal each story character plays in <paramref name="castLanguage"/> (Slice 136): a member the book gives an animal (or none) keeps it (Slice 152, Decision 157); any other member
	/// plays the animal its name or the story says. Animals with no sound are included. The roster is the language's own, as the book's cast was made from it.
	/// </summary>
	public IReadOnlyDictionary<string, AnimalKind> AnimalsOf(Story story, IReadOnlyList<Character> characters, string castLanguage)
	{
		IReadOnlyList<CastMember> cast = story.CastIn(castLanguage);
		IReadOnlyList<StorytellerCharacter> speakers = CharacterRoster.Speakers(castLanguage);
		return speakers.Count == 0
			? AnimalSounds.Resolve(characters, story.Language, cast, story.Text)
			: AnimalSounds.OfCast(characters, story.Language, cast, speakers, story.Text);
	}

	// The screen and the read both prepare the whole story: the last one is kept for the same story instance, so the split runs once for the two.
	private Prepared PrepareShared(Story story, bool wholePieces)
	{
		if (_lastPrepared is { } last && ReferenceEquals(last.Story, story) && last.WholePieces == wholePieces)
		{
			return last.Prepared;
		}

		Prepared prepared = Prepare(story, 0, wholePieces);
		_lastPrepared = new SharedPrepared(story, wholePieces, prepared);
		return prepared;
	}

	private Prepared Prepare(Story story, int fromPosition, bool wholePieces = false)
	{
		Cache cache = Analyse(story.Language, story.Text, story.BreakMap());
		AttributionResult resolved = StoryCast.Resolve(cache.Attribution, story.Cast);

		// One piece per sentence, so the highlight steps with it. StoryPieces is shared with the starter builder, so a bundled translation and a clip are keyed by exactly these pieces (Slice 95).
		return new Prepared(cache, StoryPieces.Split(story, resolved, cache.Sentences.Value, fromPosition, wholePieces), fromPosition == 0 && !wholePieces);
	}

	private Cache Analyse(string language, string text, LineBreakMap? breaks = null)
	{
		Cache? cached = _cache;
		if (cached is not null && cached.Text == text && cached.Language == language && (breaks is null || SameBreaks(cached.LineBreaks, breaks)))
		{
			return cached;
		}

		LineBreakMap lineBreaks = breaks ?? LineReflow.BreakMap(text);
		LanguagePipeline pipeline = _pipelines.GetOrAdd(UiLanguage.Resolve(language), PipelineFor);
		AttributionResult attribution = pipeline.Attributor.Attribute(text, pipeline.Segmenter.Split(text, lineBreaks));
		var fresh = new Cache(language, text, lineBreaks, attribution, new Lazy<IReadOnlyList<SentenceSpan>>(() => StorySentences.Build(text, pipeline.Rules, lineBreaks)));
		_cache = fresh;
		return fresh;
	}

	private static bool SameBreaks(LineBreakMap a, LineBreakMap b) =>
		a.SentenceBreaks.SetEquals(b.SentenceBreaks) && a.HeadingBreaks.SetEquals(b.HeadingBreaks) && a.DroppedHyphenBreaks.SetEquals(b.DroppedHyphenBreaks);

	private static LanguagePipeline PipelineFor(string language)
	{
		LanguageRules rules = LanguageRules.For(language);
		return new LanguagePipeline(rules, new StorySegmenter(rules), new SpeakerAttributor(rules));
	}

	// A piece plays no voice: its clip is attached afterwards (WithPremade), and a piece with no clip is shown for a beat.
	private static VoicedSegment ToVoicedSegment(SplitSegment piece)
	{
		Segment segment = piece.Segment;
		return new VoicedSegment(segment.Text, NoVoiceId, segment.SourceStart, segment.SourceEnd, piece.TextStart);
	}
}
