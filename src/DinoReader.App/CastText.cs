using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>The cast screen's copy (Slice 65 new strings, English until Slice 66 adds the other languages).</summary>
public static class CastText
{
	public const string Title = "Who’s speaking";
	public const string Narrator = "Narrator";
	public const string Main = "MAIN";
	public const string Edited = "EDITED";
	public const string SharedVoice = "Shared voice";
	public const string UnknownSpeaker = "Unknown speaker";
	public const string MergeButton = "Merge two characters";
	public const string MergeTitle = "Merge characters";
	public const string MergeBody = "Pick the two names that are the same character. Their lines join up under one name and one voice.";
	public const string KeepName = "Keep the name";
	public const string VoiceTitle = "Voice";
	public const string Cancel = "Cancel";
	public const string PreviewLine = "Hello! This is how I sound when I read your story.";
	public const string SaveFailedTitle = "Couldn’t save that change";
	public const string SaveFailedBody = "The cast was not saved. Try again in a moment.";
	public const string PreviewFailedBody = "The voice could not be played right now.";
	public const string Ok = "OK";
	public const string Back = "Back";
	public const string LoadFailedTitle = "Couldn’t open the cast";
	public const string LoadFailedBody = "The cast could not be worked out for this story. Try again in a moment.";

	private const string BoyLabel = "Boy";
	private const string GirlLabel = "Girl";
	private const string UnknownGenderLabel = "?";
	private const string NameAndVoiceSeparator = " · ";

	public static string Words(int count) => count == 1 ? "1 word" : $"{count} words";

	public static string UnknownLines(int count) =>
		(count == 1 ? "1 line" : $"{count} lines") + NameAndVoiceSeparator + "uses the narrator’s voice";

	public static string HearName(string name) => "Hear " + name;

	public static string NoVoiceOfGender(Gender gender) =>
		$"This voice pack has no {GenderLabel(gender).ToLowerInvariant()} voice, so this character keeps the voice they had.";

	public static string VoiceChanged(IReadOnlyList<string> names) =>
		(names.Count == 1 ? $"{names[0]}’s voice was changed" : $"{string.Join(", ", names)} had their voices changed")
		+ " because the narrator now has the voice they were given.";

	public static string MergeInto(string name) => $"Merge into {name}";

	public static string NameAndVoice(string name, string voiceId) => name + NameAndVoiceSeparator + VoiceName(voiceId);

	public static string SharedNote(int voices) =>
		$"This voice pack only has {voices} natural voices, so some characters share a voice. The narrator’s voice is never given to a character.";

	public static string GenderLabel(Gender gender) => gender switch
	{
		Gender.Male => BoyLabel,
		Gender.Female => GirlLabel,
		_ => UnknownGenderLabel,
	};

	/// <summary>The voice's display name, or its id when the pack no longer lists it.</summary>
	public static string VoiceName(string voiceId) =>
		SupertonicVoiceTable.Voices.FirstOrDefault(v => v.Id == voiceId)?.DisplayName ?? voiceId;
}
