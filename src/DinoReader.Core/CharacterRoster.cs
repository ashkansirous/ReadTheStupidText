using System.Collections.Concurrent;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

/// <summary>
/// The storyteller characters of every language Dino reads (Slice 116, Decision 122): 16 to 30 per language, a few of them narrators,
/// each a real voice of that language's engine (Kokoro for English, Spanish, Portuguese and Chinese, Supertonic-3 for fr/ru/ja/ar/da/fi/sv/nl,
/// the Piper packs for Farsi and Norwegian) plus a flavour, with a name and a few plain words written for the language.
/// Pure data: the per-language lists are in the <c>CharacterRoster*.cs</c> files. A language the pack serves with other voices than
/// the roster names (English before the storyteller pack lands, when Supertonic speaks it) gets the roster mapped onto the voices
/// it has (<see cref="Adapt"/>), so a child never sees an engine voice.
/// </summary>
public static partial class CharacterRoster
{
    private static readonly ConcurrentDictionary<AppLanguage, IReadOnlyList<StorytellerCharacter>> Cache = new();

    /// <summary>The whole roster of <paramref name="language"/>, narrators first; English for an unknown code.</summary>
    public static IReadOnlyList<StorytellerCharacter> For(string? language)
    {
        AppLanguage which = AppLanguages.TryParse(RuleSets.PrimaryCode(language), out AppLanguage parsed) ? parsed : AppLanguage.English;
        return Cache.GetOrAdd(which, Create);
    }

    /// <summary>The narrators of <paramref name="language"/>: the choices of the Narrator page.</summary>
    public static IReadOnlyList<StorytellerCharacter> Narrators(string? language) =>
        For(language).Where(c => c.Kind == CharacterKind.Narrator).ToList();

    /// <summary>The characters that play a part in a story (everyone but the narrators).</summary>
    public static IReadOnlyList<StorytellerCharacter> Speakers(string? language) =>
        For(language).Where(c => c.Kind == CharacterKind.Speaker).ToList();

    private static IReadOnlyList<StorytellerCharacter> Create(AppLanguage language) => language switch
    {
        AppLanguage.English => English(),
        AppLanguage.Persian => Persian(),
        AppLanguage.Chinese => KokoroRoster(language),
        AppLanguage.Spanish => KokoroRoster(language),
        AppLanguage.Portuguese => KokoroRoster(language),
        AppLanguage.Norwegian => Norwegian(),
        _ => SupertonicRoster(language),
    };

    /// <summary>
    /// The part of <paramref name="roster"/> that can be spoken with the voices of <paramref name="pool"/>. Characters whose voice the pool has stay as they are.
    /// When the pool has none of the roster's voices (English, Spanish, Portuguese and Chinese read by their other engines until the storyteller pack lands) every voice is mapped to the pool's voice of the same gender
    /// at the same place, and a character that then sounds like an earlier one (same voice and flavour) is dropped. An empty pool gives an empty list.
    /// </summary>
    public static IReadOnlyList<StorytellerCharacter> Adapt(IReadOnlyList<StorytellerCharacter> roster, IReadOnlyList<VoiceOption> pool)
    {
        if (pool.Count == 0)
        {
            return [];
        }

        var ids = pool.Select(v => v.Id).ToHashSet();
        var spoken = roster.Where(c => ids.Contains(c.VoiceId)).ToList();
        return spoken.Count > 0 ? spoken : MapOnto(roster, pool);
    }

    private static IReadOnlyList<StorytellerCharacter> MapOnto(IReadOnlyList<StorytellerCharacter> roster, IReadOnlyList<VoiceOption> pool)
    {
        // Speakers' voices take the pool's first places and narrator-only voices come last, so a wrap onto a shared voice lands on a narrator rather than a character.
        var speakerVoices = roster.Where(c => c.Kind == CharacterKind.Speaker).Select(c => c.VoiceId).ToHashSet();
        var voices = roster.Select(c => (c.VoiceId, c.Gender)).Distinct().OrderBy(v => speakerVoices.Contains(v.VoiceId) ? 0 : 1).ToList();
        var map = voices.ToDictionary(v => v.VoiceId, v => PoolVoiceFor(v.Gender, voices.Where(o => o.Gender == v.Gender).ToList().IndexOf(v), pool));
        return roster.Select(c => c with { VoiceId = map[c.VoiceId] }).DistinctBy(c => (c.VoiceId, c.Flavour)).ToList();
    }

    private static string PoolVoiceFor(Gender gender, int place, IReadOnlyList<VoiceOption> pool)
    {
        var sameGender = pool.Where(v => gender == Gender.Unknown || v.Gender == gender || v.Gender == Gender.Unknown).ToList();
        IReadOnlyList<VoiceOption> from = sameGender.Count > 0 ? sameGender : pool;
        return from[place % from.Count].Id;
    }

    /// <summary>
    /// The speakers a story's characters can be given: the narrator's voice is left out, so no character sounds like the narrator. A pool with only the
    /// narrator's voice (the Chinese Melo voice, when the storyteller pack is not installed, is one) keeps it, the flavours telling the characters apart.
    /// So does a gender whose only voice is the narrator's (Spanish and Portuguese have one woman's voice, Slice 118): its characters keep that voice, a flavour apart from the narrator.
    /// </summary>
    public static IReadOnlyList<StorytellerCharacter> Choices(IReadOnlyList<StorytellerCharacter> speakers, string narratorVoiceId)
    {
        var others = speakers.Where(c => c.VoiceId != narratorVoiceId).ToList();
        if (others.Count == 0)
        {
            return speakers;
        }

        var left = others.Select(c => c.Gender).ToHashSet();
        return speakers.Where(c => c.VoiceId != narratorVoiceId || !left.Contains(c.Gender)).ToList();
    }

    /// <summary>
    /// The character a stored voice and flavour is nearest to (the cast screen names it): the same voice and flavour, else the same voice with the
    /// closest flavour, else the closest flavour of the gender. Null only when <paramref name="roster"/> is empty.
    /// </summary>
    public static StorytellerCharacter? Nearest(IReadOnlyList<StorytellerCharacter> roster, string voiceId, VoiceFlavour flavour, Gender gender = Gender.Unknown) =>
        roster
            .Select((c, order) => (Character: c, Order: order))
            .OrderBy(x => x.Character.VoiceId == voiceId ? 0 : 1)
            .ThenBy(x => FlavourDistance(x.Character.Flavour, flavour))
            .ThenBy(x => GenderMisfit(x.Character.Gender, gender))
            .ThenBy(x => x.Order)
            .Select(x => x.Character)
            .FirstOrDefault();

    private const int SameFamily = 1;
    private const int NextToNormal = 3;
    private const int FarApart = 5;
    private const int GenderMiss = 50;
    private const int GenderUnsure = 1;

    /// <summary>How far two flavours sound from each other: 0 the same, 1 in the same family (high, low or soft), 3 when one is Normal, 5 otherwise.</summary>
    public static int FlavourDistance(VoiceFlavour a, VoiceFlavour b)
    {
        if (a == b)
        {
            return 0;
        }

        if (FamilyOf(a) == FamilyOf(b))
        {
            return SameFamily;
        }

        return a == VoiceFlavour.Normal || b == VoiceFlavour.Normal ? NextToNormal : FarApart;
    }

    /// <summary>How badly a character of <paramref name="has"/> gender fits a story character of <paramref name="wanted"/>: 0 fits, 1 when either is unsure, 50 when opposite.</summary>
    public static int GenderMisfit(Gender has, Gender wanted)
    {
        if (has == wanted)
        {
            return 0;
        }

        return has == Gender.Unknown || wanted == Gender.Unknown ? GenderUnsure : GenderMiss;
    }

    private static int FamilyOf(VoiceFlavour flavour) => flavour switch
    {
        VoiceFlavour.Small or VoiceFlavour.Squeaky or VoiceFlavour.Bouncy => 1,
        VoiceFlavour.Big or VoiceFlavour.Giant or VoiceFlavour.Grumpy => 2,
        VoiceFlavour.Sleepy or VoiceFlavour.Whisper => 3,
        VoiceFlavour.Storyteller => 4,
        _ => 0,
    };
}
