using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

public static partial class CharacterRoster
{
    // The Norwegian pack's speaker codes by place: five women (K...) then five men (M...), in the order of the ten voice keys F1-F5, M1-M5.
    private static readonly string[] NorwegianCodes = ["knn", "ksv", "kon", "kmn", "knv", "mmn", "mnn", "msv", "mon", "mnv"];

    /// <summary>One place of the shared ten-voice layout: a voice key (F1-F5 women, M1-M5 men), a flavour and who plays it.</summary>
    private sealed record Slot(string Id, string VoiceKey, VoiceFlavour Flavour, CharacterKind Kind);

    // The same thirty-two ideas (Slice 125 added eight animals, Slice 135 six more young ones) in every language that has ten voices (Supertonic-3 for eight languages, Piper for Norwegian): three narrators, then the cast.
    // No two slots share a voice and flavour. The names and plain words of each language follow this order.
    private static readonly Slot[] TenVoiceSlots =
    [
        new("granny", "F1", VoiceFlavour.Storyteller, CharacterKind.Narrator),
        new("grandad", "M1", VoiceFlavour.Storyteller, CharacterKind.Narrator),
        new("whisperer", "F3", VoiceFlavour.Storyteller, CharacterKind.Narrator),
        new("mummy", "F2", VoiceFlavour.Normal, CharacterKind.Speaker),
        new("mouse", "F4", VoiceFlavour.Squeaky, CharacterKind.Speaker),
        new("bunny", "F5", VoiceFlavour.Small, CharacterKind.Speaker),
        new("princess", "F4", VoiceFlavour.Normal, CharacterKind.Speaker),
        new("bee", "F5", VoiceFlavour.Bouncy, CharacterKind.Speaker),
        new("witch", "F2", VoiceFlavour.Grumpy, CharacterKind.Speaker),
        new("hen", "F3", VoiceFlavour.Sleepy, CharacterKind.Speaker),
        new("troll", "M3", VoiceFlavour.Grumpy, CharacterKind.Speaker),
        new("giant", "M5", VoiceFlavour.Giant, CharacterKind.Speaker),
        new("bear", "M4", VoiceFlavour.Sleepy, CharacterKind.Speaker),
        new("fox", "M2", VoiceFlavour.Whisper, CharacterKind.Speaker),
        new("knight", "M5", VoiceFlavour.Normal, CharacterKind.Speaker),
        new("piglet", "M2", VoiceFlavour.Small, CharacterKind.Speaker),
        new("chick", "M4", VoiceFlavour.Bouncy, CharacterKind.Speaker),
        new("dragon", "M3", VoiceFlavour.Big, CharacterKind.Speaker),
        new("lamb", "F5", VoiceFlavour.Squeaky, CharacterKind.Speaker),
        new("puppy", "M5", VoiceFlavour.Bouncy, CharacterKind.Speaker),
        new("kitten", "F4", VoiceFlavour.Small, CharacterKind.Speaker),
        new("duckling", "F3", VoiceFlavour.Squeaky, CharacterKind.Speaker),
        new("cub", "M4", VoiceFlavour.Small, CharacterKind.Speaker),
        new("bigbear", "M4", VoiceFlavour.Big, CharacterKind.Speaker),
        new("wolf", "M2", VoiceFlavour.Grumpy, CharacterKind.Speaker),
        new("ewe", "F2", VoiceFlavour.Sleepy, CharacterKind.Speaker),
        new("piglet2", "M5", VoiceFlavour.Small, CharacterKind.Speaker),
        new("piglet3", "M3", VoiceFlavour.Bouncy, CharacterKind.Speaker),
        new("lamb2", "F2", VoiceFlavour.Small, CharacterKind.Speaker),
        new("duckling2", "F4", VoiceFlavour.Bouncy, CharacterKind.Speaker),
        new("puppy2", "M2", VoiceFlavour.Bouncy, CharacterKind.Speaker),
        new("kitten2", "F2", VoiceFlavour.Squeaky, CharacterKind.Speaker),
    ];

    private static IReadOnlyList<StorytellerCharacter> Norwegian() => FromSlots(TenVoiceSlots, NorwegianVoiceId, NorwegianWords());

    private static IReadOnlyList<StorytellerCharacter> SupertonicRoster(AppLanguage language) =>
        FromSlots(TenVoiceSlots, key => SupertonicVoiceTable.IdPrefix + key, WordsOf(language));

    private static string NorwegianVoiceId(string key) =>
        NorwegianVoices.IdPrefix + NorwegianCodes[(key[0] == 'F' ? 0 : 5) + (key[1] - '1')];

    private static IReadOnlyList<StorytellerCharacter> FromSlots(Slot[] slots, Func<string, string> voiceOf, (string Name, string Hint)[] words)
    {
        if (words.Length != slots.Length)
        {
            throw new InvalidOperationException($"A roster of {slots.Length} places needs {slots.Length} entries, got {words.Length}.");
        }

        return slots
            .Select((s, i) => new StorytellerCharacter(
                s.Id, voiceOf(s.VoiceKey), s.Flavour, s.VoiceKey[0] == 'F' ? Gender.Female : Gender.Male, s.Kind, words[i].Name, words[i].Hint))
            .ToList();
    }

    private static (string Name, string Hint)[] WordsOf(AppLanguage language) => language switch
    {
        AppLanguage.French => FrenchWords(),
        AppLanguage.Russian => RussianWords(),
        AppLanguage.Japanese => JapaneseWords(),
        AppLanguage.Arabic => ArabicWords(),
        AppLanguage.Danish => DanishWords(),
        AppLanguage.Swedish => SwedishWords(),
        AppLanguage.Finnish => FinnishWords(),
        AppLanguage.Dutch => DutchWords(),
        AppLanguage.German => GermanWords(),
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "No ten-voice roster for this language."),
    };
}
