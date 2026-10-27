using System.Collections.Concurrent;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>
/// Turns story text into voiced segments (Slice 60) and works out a story's cast (Slice 61):
/// segments the text, attributes each quote to a character and casts a voice per character
/// (never the narrator's). Who speaks is worked out once, at import, in the story's own language
/// (its <see cref="LanguageRules"/>, Slice 66), before any translation. The cast is stored with the
/// story; reading and recasting reuse the stored voices (<see cref="StoryCast.Pinned"/>) so a character
/// keeps their voice across restarts, text fixes and narrator changes. The voices a story can use are
/// the installed pack's for its language (Slice 51), or the Supertonic styles while none is installed.
/// </summary>
public sealed class StoryVoicing
{
	private readonly IVoicePackService _packs;
	private readonly NarratorChoice _narrators;
	private readonly StoryTranslator _translator;
	private readonly ConcurrentDictionary<string, IReadOnlyList<VoiceOption>> _pools = new();

	// One segmenter and attributor per language (their rules are shared per language too), built on first use.
	private readonly ConcurrentDictionary<string, LanguagePipeline> _pipelines = new();

	private sealed record LanguagePipeline(LanguageRules Rules, StorySegmenter Segmenter, SpeakerAttributor Attributor);

	// One immutable snapshot, swapped atomically: attributing a long story is the costly part, and
	// a second read of the same text (or a narrator change) does not need it again. The sentences are
	// only built when a read asks for them (the cast screen never does).
	private sealed record Cache(string Language, string Text, LineBreakMap LineBreaks, AttributionResult Attribution, Lazy<IReadOnlyList<SentenceSpan>> Sentences);

	// The analysis and the sentence pieces a read speaks, before any translation. The cast is worked out afterwards, in the language spoken (Slice 108).
	private sealed record Prepared(Cache Analysis, IReadOnlyList<SplitSegment> Pieces, bool WholeBook);

	/// <summary>A cast made for a language that had none (Slice 108), for the caller to save.</summary>
	public sealed record NewCast(string Language, IReadOnlyList<CastMember> Members);

	private sealed record SharedPrepared(Story Story, bool WholePieces, Prepared Prepared);

	private volatile Cache? _cache;
	private volatile SharedPrepared? _lastPrepared;

	// The animal-sound plan of the last story and analysis: a tap-to-seek or resume read from the middle needs the whole book's plan, and re-splitting the book each time is wasted.
	private sealed record SharedPlan(Story Story, Cache Analysis, string Language, IReadOnlyDictionary<int, AnimalKind> Plan);

	private volatile SharedPlan? _lastPlan;

	public StoryVoicing(IVoicePackService packs, IVoiceModelService model, StoryTranslator translator, VoicePackInstaller installer, NarratorChoice narrators)
	{
		_packs = packs;
		CastText.RosterSource = RosterFor;
		_narrators = narrators;
		_translator = translator;
		model.ReadyChanged += (_, _) => _pools.Clear();
		// A pack that finishes installing changes which voices a language has; a stale pool would keep fallback ids.
		installer.Changed += (_, _) => { if (installer.State == PackInstallState.Ready) _pools.Clear(); };
		// The storyteller pack coming or going (Slice 114) changes the English pool the same way.
		packs.InstalledPacksChanged += (_, _) => _pools.Clear();
	}

	/// <summary>
	/// <see cref="Voice"/> on a worker thread: segmenting and attributing a long story is CPU work
	/// that must not block the UI. The story is translated afterwards when it is set to be (Slice 66).
	/// </summary>
	public async Task<IReadOnlyList<VoicedSegment>> VoiceAsync(
		Story story, int fromPosition = 0, CancellationToken cancellationToken = default, Action? translationSkipped = null)
	{
		bool wholePieces = _translator.UsesBundledTable(story);
		Prepared prepared = await Task.Run(() => fromPosition == 0 ? PrepareShared(story, wholePieces) : Prepare(story, fromPosition, wholePieces), cancellationToken);
		if (!_translator.Applies(story))
		{
			return await Task.Run(() => VoicedInOwnLanguage(story, prepared), cancellationToken);
		}

		StoryTranslator.Outcome outcome = await _translator.TranslateAsync(story, prepared.Pieces, cancellationToken);
		if (!outcome.Translated)
		{
			// The story is read as written, in its own language and voices, so the spoken pack goes back to its language too.
			translationSkipped?.Invoke();
			return await Task.Run(() => VoicedInOwnLanguage(story, prepared), cancellationToken);
		}

		return await Task.Run(() => VoicedTranslated(story, prepared, outcome.Pieces), cancellationToken);
	}

	/// <summary>The story's text in the language it is read in, for the reading screen (Slice 110, Decision 112); null when the screen shows the story as it is:
	/// the story is not translated now, the grown-up chose "Show original text", or a missing language model left it untranslated. <paramref name="sentences"/> are the
	/// story's sentences, which the result keeps one-to-one.</summary>
	public async Task<TranslatedStory?> TranslatedDisplayAsync(Story story, IReadOnlyList<SentenceSpan> sentences, CancellationToken cancellationToken = default)
	{
		if (story.ShowOriginalText || !_translator.Applies(story))
		{
			return null;
		}

		Prepared prepared = await Task.Run(() => PrepareShared(story, _translator.UsesBundledTable(story)), cancellationToken);
		// The engine's pieces are shared with a read that speaks them at the same time (StoryTranslator), so each is translated once.
		StoryTranslator.Outcome outcome = await _translator.TranslateAsync(story, prepared.Pieces, cancellationToken);
		return outcome.Translated ? TranslatedStoryBuilder.Build(story.Pages, sentences, outcome.Pieces, StoryRecordings.PagesShownInOwnLanguage(story)) : null;
	}

	// A translated read is cast from the spoken language's own cast (Slice 108), narrated by that language's narrator.
	// The pack scans behind the voices run here, on the worker thread that calls it.
	private IReadOnlyList<VoicedSegment> VoicedTranslated(Story story, Prepared prepared, IReadOnlyList<SplitSegment> pieces)
	{
		string spoken = _translator.SpokenLanguage(story);
		VoiceCast cast = CastIn(story, prepared, spoken);
		return WithAnimalSounds(story, prepared, cast, pieces, spoken);
	}

	private IReadOnlyList<VoicedSegment> VoicedInOwnLanguage(Story story, Prepared prepared)
	{
		VoiceCast cast = CastIn(story, prepared, story.Language);
		return WithAnimalSounds(story, prepared, cast, prepared.Pieces, story.Language);
	}

	// Animal characters say their sound after every 2nd or 3rd line of their own (Slice 117, Decision 123): one extra short segment in the character's voice,
	// spoken only (it has no text on the page). The lines are counted over the whole book in the story's language, so a read from the middle sounds like a read from the start.
	// The sound comes after the whole line, dialogue tag included, and after the last piece of a translated line.
	private IReadOnlyList<VoicedSegment> WithAnimalSounds(
		Story story, Prepared prepared, VoiceCast cast, IReadOnlyList<SplitSegment> pieces, string spokenLanguage)
	{
		IReadOnlyDictionary<int, AnimalSounds.Placement> sounds = AnimalSoundPlacements(story, prepared, pieces, spokenLanguage).ToDictionary(p => p.After);
		var voiced = new List<VoicedSegment>(pieces.Count + sounds.Count);
		for (int i = 0; i < pieces.Count; i++)
		{
			voiced.Add(ToVoicedSegment(pieces[i], cast));
			if (sounds.TryGetValue(i, out AnimalSounds.Placement? placement) && AnimalSoundAfter(pieces, placement, cast, spokenLanguage) is { } sound)
			{
				voiced.Add(sound);
			}
		}

		return voiced;
	}

	private IReadOnlyList<AnimalSounds.Placement> AnimalSoundPlacements(Story story, Prepared prepared, IReadOnlyList<SplitSegment> pieces, string spokenLanguage)
	{
		// The grown-up's one app-wide switch (Slice 152); read at every read, never cached with the plan.
		IReadOnlyDictionary<int, AnimalKind> plan = AnimalSounds.Audible(AnimalPlan(story, prepared, spokenLanguage), AnimalSoundsPreference.Load());
		return plan.Count == 0 ? [] : AnimalSounds.Placements(pieces, plan, prepared.Analysis.Sentences.Value);
	}

	// The plan counts the story's own lines; a read that is the whole book already has exactly those pieces. Kept for the same story instance and analysis.
	private IReadOnlyDictionary<int, AnimalKind> AnimalPlan(Story story, Prepared prepared, string spokenLanguage)
	{
		if (_lastPlan is { } last && ReferenceEquals(last.Story, story) && ReferenceEquals(last.Analysis, prepared.Analysis) && last.Language == spokenLanguage)
		{
			return last.Plan;
		}

		AttributionResult resolved = StoryCast.Resolve(prepared.Analysis.Attribution, story.Cast);
		IReadOnlyDictionary<string, AnimalKind> animals = AnimalSounds.WithSound(AnimalsOf(story, resolved.Characters, spokenLanguage), story.Language);
		IReadOnlyDictionary<int, AnimalKind> plan = animals.Count == 0
			? new Dictionary<int, AnimalKind>()
			: AnimalSounds.Plan(WholeBookPieces(story, prepared, resolved), animals, story.Language);
		_lastPlan = new SharedPlan(story, prepared.Analysis, spokenLanguage, plan);
		return plan;
	}

	private static IReadOnlyList<SplitSegment> WholeBookPieces(Story story, Prepared prepared, AttributionResult resolved) =>
		prepared.WholeBook ? prepared.Pieces : StoryPieces.Split(story, resolved, prepared.Analysis.Sentences.Value, 0);

	// The sound segment, a zero-width one at the last character of the line (its dialogue tag included, so the order of positions holds and the highlight
	// stays on that sentence); none when the spoken line already says it.
	private static VoicedSegment? AnimalSoundAfter(IReadOnlyList<SplitSegment> pieces, AnimalSounds.Placement placement, VoiceCast cast, string spokenLanguage)
	{
		Segment quote = pieces[placement.Quote].Segment;
		string? sound = AnimalSounds.SoundOf(placement.Kind, spokenLanguage);
		if (sound is null || AnimalSounds.SaysIt(LineText(pieces, placement.Quote), placement.Kind, spokenLanguage))
		{
			return null;
		}

		int at = Math.Max(quote.SourceStart, pieces[placement.After].Segment.SourceEnd - 1);
		return new VoicedSegment(
			sound, cast.VoiceForLine(quote.CharacterId), at, at, at, FlavourTransform.For(AnimalSounds.FlavourFor(placement.Kind, cast.FlavourForLine(quote.CharacterId))));
	}

	// The spoken text of the line the piece at <paramref name="last"/> ends: the consecutive pieces of the same character before it.
	private static string LineText(IReadOnlyList<SplitSegment> pieces, int last)
	{
		int first = last;
		while (first > 0 && pieces[first - 1].Segment.Kind == SegmentKind.Quote && pieces[first - 1].Segment.CharacterId == pieces[last].Segment.CharacterId)
		{
			first--;
		}

		return string.Join(' ', pieces.Skip(first).Take(last - first + 1).Select(p => p.Segment.Text));
	}

	/// <summary>The language a read of <paramref name="story"/> is spoken in now, and so the one whose cast the cast screen edits (Slice 108).</summary>
	public string ReadingLanguage(Story story) => _translator.SpokenLanguage(story);

	/// <summary>The narrator of every book read in <paramref name="language"/> (Slice 109): the grown-up's pick for it while its pack still has that voice, else <see cref="NarratorFor"/>.</summary>
	public string NarratorOf(string language) =>
		OnNarratorRoster(language, NarratorSetting.Resolve(_narrators.Stored(language), VoicesFor(language), NarratorFor(language)));

	// A narrator picked before the storyteller characters (Slice 116) can be any voice of the pack; it becomes the roster narrator of the same gender, so the Narrator page always shows a character.
	private string OnNarratorRoster(string language, string voiceId)
	{
		IReadOnlyList<StorytellerCharacter> narrators = NarratorsFor(language);
		if (narrators.Count == 0 || narrators.Any(n => n.VoiceId == voiceId) || voiceId == VoiceChoices.PersianLiteraryVoiceId)
		{
			return voiceId;
		}

		Gender gender = VoicesFor(language).FirstOrDefault(v => v.Id == voiceId)?.Gender ?? Gender.Unknown;
		return (narrators.FirstOrDefault(n => n.Gender == gender) ?? narrators[0]).VoiceId;
	}

	/// <summary>The narrator written into <c>NarratorVoiceId</c> for older builds (Slice 109): the stored pick or the default, with no pack scan, so a save never does I/O for it.</summary>
	public string LegacyNarratorOf(string language) =>
		_narrators.Stored(language) ?? (UiLanguage.Resolve(language) == NorwegianLanguageCode ? NorwegianVoices.DefaultId : SupertonicVoiceTable.Default.Id);

	/// <summary>Makes <paramref name="voiceId"/> the narrator of <paramref name="language"/>.</summary>
	public void SetNarrator(string language, string voiceId) => _narrators.Set(language, voiceId);

	/// <summary>The languages with an installed voice pack, <paramref name="myLanguage"/> first when it is one (the Narrator page lists one card for each).
	/// The installed packs are read once, not per language.</summary>
	public IReadOnlyList<string> InstalledLanguages(string myLanguage)
	{
		string mine = UiLanguage.Resolve(myLanguage);
		IReadOnlyList<InstalledVoicePack> installed = InstalledPacks();
		return LanguageNames.All.Select(l => UiLanguage.Resolve(l.Code)).Distinct()
			.Where(code => InstalledCatalog(installed, code) is not null)
			.OrderBy(code => code == mine ? 0 : 1)
			.ToList();
	}

	/// <summary>A cast for the language <paramref name="story"/> is read in now when it has none yet (Slice 108), else null (also null while that
	/// language has no voices yet or the story no characters, so nothing empty is kept). Seeded by the genders the grown-up corrected in the
	/// story's own cast; the caller saves it so the voices stay put. On a worker thread.</summary>
	public Task<NewCast?> NewCastForReadingAsync(Story story) =>
		story.HasCastIn(ReadingLanguage(story)) ? Task.FromResult<NewCast?>(null) : Task.Run(() => NewCastForReading(story));

	private NewCast? NewCastForReading(Story story)
	{
		string language = ReadingLanguage(story);
		if (story.HasCastIn(language) || VoicesFor(language).Count == 0)
		{
			return null;
		}

		// The corrected genders, animals and the book's voice choices carry over, never the voices: those belong to the story language's pack.
		IReadOnlyList<CastMember> members = CastFor(story.Language, language, story.Text, StoryCast.SeedForOtherLanguage(story.Cast), story.BreakMap());
		return members.Count == 0 ? null : new NewCast(language, members);
	}

	/// <summary>The cast for <paramref name="text"/>, keeping any voice <paramref name="stored"/> already gives a character.</summary>
	public Task<IReadOnlyList<CastMember>> CastAsync(
		string language, string text, IReadOnlyList<CastMember> stored, LineBreakMap? lineBreaks = null) =>
		Task.Run(() => CastFor(language, text, stored, lineBreaks));

	/// <summary>The voices a character of a story in <paramref name="language"/> can have (the whole pack; the narrator's is left out by the caller).</summary>
	public IReadOnlyList<VoiceOption> VoicesFor(string language) =>
		_pools.GetOrAdd(UiLanguage.Resolve(language), BuildPool);

	/// <summary>The language a voice preview speaks: the story's when its own pack is installed, else the one the voice speaks now. Never switches the voice.</summary>
	public string PreviewLanguage(string storyLanguage, string current)
	{
		string code = UiLanguage.Resolve(storyLanguage);
		return InstalledVoices(code) is not null ? code : current;
	}

	private const string NorwegianLanguageCode = "nb";

	/// <summary>The voice a new story in <paramref name="language"/> starts with as its narrator: the default style when the pack has it, else the pack's first voice
	/// (Norwegian, before its pack installs: the pack's first voice, so the saved id is one the pack knows).</summary>
	public string NarratorFor(string language)
	{
		IReadOnlyList<VoiceOption> pool = VoicesFor(language);
		if (pool.Count == 0)
		{
			return UiLanguage.Resolve(language) == NorwegianLanguageCode ? NorwegianVoices.DefaultId : SupertonicVoiceTable.Default.Id;
		}

		// Farsi starts with mana, the female storyteller (Slice 144, Decision 150), whatever order the pack lists its voices in.
		if (pool.Any(v => v.Id == VoiceChoices.PersianDefaultVoiceId))
		{
			return VoiceChoices.PersianDefaultVoiceId;
		}

		return pool.Any(v => v.Id == SupertonicVoiceTable.Default.Id) ? SupertonicVoiceTable.Default.Id : pool[0].Id;
	}

	/// <summary>
	/// The animal each story character plays in <paramref name="castLanguage"/>'s cast (Slice 136): the one place the cast screen and a read both ask, so the card and the sounds agree.
	/// A member the book gives an animal (or none) keeps it whatever voice a grown-up picks (Slice 152, Decision 157); any other member keeps the old rule: a character
	/// no grown-up has recast keeps its animal from the story, one a grown-up recast plays its character's. Animals with no sound are included.
	/// </summary>
	public IReadOnlyDictionary<string, AnimalKind> AnimalsOf(Story story, IReadOnlyList<Character> characters, string castLanguage)
	{
		IReadOnlyList<CastMember> cast = story.CastIn(castLanguage);
		IReadOnlyList<StorytellerCharacter> speakers = RosterFor(castLanguage).Where(c => c.Kind == CharacterKind.Speaker).ToList();
		return speakers.Count == 0
			? AnimalSounds.Resolve(characters, story.Language, cast, story.Text)
			: AnimalSounds.OfCast(characters, story.Language, cast, speakers, story.Text);
	}

	/// <summary>How many storyteller characters story characters can share out once <paramref name="language"/>'s narrator has taken a voice (Slice 116).</summary>
	public int CharacterVoiceCount(string language) => ChoicesFor(language).Count;

	/// <summary>The language's whole roster that its voices can speak (Slice 116): narrators first, voices mapped when another engine speaks the language for now.</summary>
	public IReadOnlyList<StorytellerCharacter> RosterFor(string language) =>
		CharacterRoster.Adapt(CharacterRoster.For(language), VoicesFor(language));

	/// <summary>The narrators a grown-up can pick for <paramref name="language"/> on the Narrator page.</summary>
	public IReadOnlyList<StorytellerCharacter> NarratorsFor(string language) =>
		RosterFor(language).Where(c => c.Kind == CharacterKind.Narrator).ToList();

	/// <summary>The storyteller characters a story character can be given in <paramref name="language"/>: its roster without the narrator's voice (the cast screen's picker).</summary>
	public IReadOnlyList<StorytellerCharacter> ChoicesFor(string language) =>
		CharacterRoster.Choices(RosterFor(language).Where(c => c.Kind == CharacterKind.Speaker).ToList(), NarratorOf(language));

	/// <summary><see cref="RecastCharacters"/> on a worker thread.</summary>
	public Task<IReadOnlyList<CastMember>> RecastCharacterAsync(Story story, string castLanguage, IReadOnlyList<CastMember> cast, string characterId) =>
		RecastCharactersAsync(story, castLanguage, cast, [characterId]);

	/// <summary><see cref="RecastCharacters"/> on a worker thread.</summary>
	public Task<IReadOnlyList<CastMember>> RecastCharactersAsync(Story story, string castLanguage, IReadOnlyList<CastMember> cast, IReadOnlyCollection<string> characterIds) =>
		Task.Run(() => RecastCharacters(story, castLanguage, cast, characterIds));

	/// <summary>The cast with one character's voice picked again (its animal changed, Slice 125), from its new animal; the voice a grown-up chose by hand stays.
	/// The other characters keep the voices they have.</summary>
	public IReadOnlyList<CastMember> RecastCharacter(Story story, string castLanguage, IReadOnlyList<CastMember> cast, string characterId) =>
		RecastCharacters(story, castLanguage, cast, [characterId]);

	/// <summary>The cast with the voices of several characters picked again in one pass (their animals changed); a voice a grown-up chose by hand stays, and so does every other character's.</summary>
	public IReadOnlyList<CastMember> RecastCharacters(Story story, string castLanguage, IReadOnlyList<CastMember> cast, IReadOnlyCollection<string> characterIds)
	{
		var changing = cast.Where(m => characterIds.Contains(m.CharacterId) && !m.Edited).Select(m => m.CharacterId).ToHashSet();
		if (changing.Count == 0)
		{
			return cast;
		}

		// The others are held as picked for this pass only (never written as hand-picked), so the new picks avoid every voice they hold.
		HashSet<string> held = cast.Select(m => m.CharacterId).Where(id => !changing.Contains(id)).ToHashSet();
		var forgotten = cast.Select(m => changing.Contains(m.CharacterId) ? m with { VoiceId = string.Empty } : m).ToList();
		IReadOnlyList<CastMember> recast = CastFor(story.Language, castLanguage, story.Text, forgotten, story.BreakMap(), held);
		return cast.Select(m => changing.Contains(m.CharacterId) ? recast.FirstOrDefault(r => r.CharacterId == m.CharacterId) ?? m : m).ToList();
	}

	/// <summary>The story's characters and lines as the cast screen shows them: an edited
	/// character's gender in place of the guess. On a worker thread, like <see cref="VoiceAsync"/>.</summary>
	public Task<AttributionResult> ResolveAsync(Story story, string castLanguage) =>
		Task.Run(() => StoryCast.Resolve(Analyse(story.Language, story.Text, story.BreakMap()).Attribution, story.CastIn(castLanguage)));

	/// <summary>The voiced segments of the story from <paramref name="fromPosition"/> on, untranslated (a story index: a tapped
	/// or skipped-to sentence), read by the language's narrator and the story's cast.</summary>
	public IReadOnlyList<VoicedSegment> Voice(Story story, int fromPosition = 0)
	{
		return VoicedInOwnLanguage(story, Prepare(story, fromPosition));
	}

	/// <summary><see cref="CastFor(string, string, string, IReadOnlyList{CastMember}, LineBreakMap?)"/> on a worker thread.</summary>
	public Task<IReadOnlyList<CastMember>> CastForAsync(
		string storyLanguage, string castLanguage, string text, IReadOnlyList<CastMember> stored,
		LineBreakMap? lineBreaks = null) =>
		Task.Run(() => CastFor(storyLanguage, castLanguage, text, stored, lineBreaks));

	public IReadOnlyList<CastMember> CastFor(
		string language, string text, IReadOnlyList<CastMember> stored, LineBreakMap? lineBreaks = null) =>
		CastFor(language, language, text, stored, lineBreaks);

	/// <summary>The cast of a story written in <paramref name="storyLanguage"/>, voiced from <paramref name="castLanguage"/>'s pack (Slice 108):
	/// who speaks is worked out in the story's language, the voices come from the language it is read in.</summary>
	public IReadOnlyList<CastMember> CastFor(
		string storyLanguage, string castLanguage, string text, IReadOnlyList<CastMember> stored,
		LineBreakMap? lineBreaks = null, IReadOnlySet<string>? heldIds = null)
	{
		AttributionResult resolved = StoryCast.Resolve(Analyse(storyLanguage, text, lineBreaks).Attribution, stored);
		VoiceCast cast = Cast(castLanguage, resolved, stored, storyLanguage, text, heldIds);
		return StoryCast.ToMembers(resolved.Characters, cast, LanguageRules.For(storyLanguage), stored);
	}

	// The screen and the voice both prepare the whole story: the last one is kept for the same story instance, so the split runs once for the two.
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

		// One piece per sentence, so the reader raises a chunk per sentence and the highlight steps with it.
		// StoryPieces is shared with the starter builder, so a bundled translation is keyed by exactly these pieces (Slice 95).
		return new Prepared(cache, StoryPieces.Split(story, resolved, cache.Sentences.Value, fromPosition, wholePieces), fromPosition == 0 && !wholePieces);
	}

	// The voices of the language's own cast: its pins and edited genders, in its own pack. A language with no stored cast is cast fresh here.
	private VoiceCast CastIn(Story story, Prepared prepared, string language)
	{
		bool hasCast = story.HasCastIn(language);
		// A language with no cast yet (its voices are missing, or the save failed) is cast like a fresh one: the story's corrected genders carry over,
		// and while its own pack has no voices the story language's pool is used, so characters do not all fall to the narrator. The next read tries again.
		IReadOnlyList<CastMember> stored = hasCast ? story.CastIn(language) : StoryCast.SeedForOtherLanguage(story.Cast);
		IReadOnlyList<VoiceOption> pool = hasCast || VoicesFor(language).Count > 0 ? VoicesFor(language) : VoicesFor(story.Language);
		AttributionResult resolved = StoryCast.Resolve(prepared.Analysis.Attribution, stored);
		// Names are in the story's language, so its rules guess the flavours; the stored voice and flavour (a grown-up's pick included) win (Slices 113, 116).
		return CastWithRoster(resolved.Characters, language, pool, stored, story.Language, story.Text);
	}

	private VoiceCast Cast(string language, AttributionResult attribution, IReadOnlyList<CastMember> stored, string storyLanguage, string text, IReadOnlySet<string>? heldIds = null) =>
		CastWithRoster(attribution.Characters, language, VoicesFor(language), stored, storyLanguage, text, heldIds);

	// Each story character gets the storyteller character of the language's roster that fits it best (Slice 116); the roster is the one the pool's voices can speak.
	private VoiceCast CastWithRoster(
		IReadOnlyList<Character> characters, string language, IReadOnlyList<VoiceOption> pool, IReadOnlyList<CastMember> stored, string storyLanguage, string text,
		IReadOnlySet<string>? heldIds = null)
	{
		// Names are in the story's language, so its rules guess the flavours; an animal character is cast to its animal's character (Slice 125).
		LanguageRules nameRules = LanguageRules.For(storyLanguage);
		IReadOnlyDictionary<string, AnimalKind> animals = AnimalSounds.Resolve(characters, storyLanguage, stored, text);
		string narrator = NarratorOf(language);
		IReadOnlyList<StorytellerCharacter> roster = CharacterRoster.Adapt(CharacterRoster.For(language), pool);
		IReadOnlyList<StorytellerCharacter> choices = CharacterRoster.Choices(roster.Where(c => c.Kind == CharacterKind.Speaker).ToList(), narrator);
		return CastCharacters.Cast(
			characters, choices, roster, narrator, c => FlavourGuess.Guess(c.Name, nameRules), Held(StoryCast.PinnedCharacters(stored), heldIds),
			animalOf: c => animals.TryGetValue(c.Id, out AnimalKind kind) ? AnimalCharacters.HintFor(kind, c.Name, storyLanguage) : null);
	}

	// The characters named in heldIds keep their voices for this casting only, as if the grown-up had picked them; nothing is written to the cast.
	private static IReadOnlyDictionary<string, PinnedCharacter> Held(IReadOnlyDictionary<string, PinnedCharacter> pinned, IReadOnlySet<string>? heldIds) =>
		heldIds is null ? pinned : pinned.ToDictionary(p => p.Key, p => heldIds.Contains(p.Key) ? p.Value with { Edited = true } : p.Value);

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

	private IReadOnlyList<VoiceOption> BuildPool(string language)
	{
		// A language Supertonic does not serve (Norwegian) has an empty pool until its pack installs, so no Supertonic id
		// is saved into a cast that the pack's own voices would then not know (pinned stored voices are kept as they are).
		IReadOnlyList<VoiceInfo> voices = InstalledVoices(language)
			?? (SupertonicVoiceTable.Serves(language) ? SupertonicVoiceTable.VoicesFor(language) : []);
		IReadOnlyList<VoiceOption> options = voices.Select(ToOption).ToList();
		CastText.RememberRoster(language, CharacterRoster.Adapt(CharacterRoster.For(language), options));
		return options;
	}

	private IReadOnlyList<VoiceInfo>? InstalledVoices(string language) => InstalledCatalog(InstalledPacks(), language);

	private static IReadOnlyList<VoiceInfo>? InstalledCatalog(IReadOnlyList<InstalledVoicePack> installed, string language) =>
		InstalledVoiceCatalog.Resolve(installed, language)?.AvailableVoices;

	private IReadOnlyList<InstalledVoicePack> InstalledPacks()
	{
		try
		{
			// The storyteller packs (Slice 114) take part: an installed one gives its language its Kokoro voices.
			return _packs.GetInstalledVoiceAndStorytellerPacks();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return [];
		}
	}

	// A pack that does not say which of its voices are for girls (Persian Piper, Melo) gives Unknown, which any character may take.
	// Supertonic, the storyteller voices (Slice 114) and the Norwegian pack (speaker codes, Slice 104) know theirs.
	private static VoiceOption ToOption(VoiceInfo voice)
	{
		return new VoiceOption(voice.Id, GenderOf(voice.Id));
	}

	private static Gender GenderOf(string voiceId)
	{
		if (voiceId.StartsWith(SupertonicVoiceTable.IdPrefix, StringComparison.Ordinal))
		{
			return SupertonicVoiceTable.IsFemale(voiceId) ? Gender.Female : Gender.Male;
		}

		if (voiceId.StartsWith(KokoroVoiceTable.IdPrefix, StringComparison.Ordinal))
		{
			return KokoroVoiceTable.IsFemale(voiceId) ? Gender.Female : Gender.Male;
		}

		return NorwegianVoices.IsFemale(voiceId) switch { true => Gender.Female, false => Gender.Male, null => Gender.Unknown };
	}

	// Narration, and a quote nobody could be attributed to, are read by the narrator (VoiceForLine).
	private static VoicedSegment ToVoicedSegment(SplitSegment piece, VoiceCast cast)
	{
		Segment segment = piece.Segment;
		return new VoicedSegment(
			segment.Text, cast.VoiceForLine(segment.CharacterId), segment.SourceStart, segment.SourceEnd, piece.TextStart,
			FlavourTransform.For(cast.FlavourForLine(segment.CharacterId)));
	}
}
