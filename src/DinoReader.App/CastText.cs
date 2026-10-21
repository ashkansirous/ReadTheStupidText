using System.Collections.Concurrent;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>The cast screen's copy (Slice 65), in the UI language (Slice 66).</summary>
public static class CastText
{
	private const string NameAndVoiceSeparator = " · ";
	private const string NameSeparator = ", ";
	private const int Singular = 1;

	// Display names of the voices in the packs seen so far (a Piper pack names its own voices).
	private static readonly ConcurrentDictionary<string, string> PackVoiceNames = new();

	public static string Title => AppResources.Cast_Title;
	public static string Narrator => AppResources.Cast_Narrator;
	public static string Main => AppResources.Cast_Main;
	public static string Edited => AppResources.Cast_Edited;
	public static string SharedVoice => AppResources.Cast_SharedVoice;
	public static string UnknownSpeaker => AppResources.Cast_UnknownSpeaker;
	public static string MergeButton => AppResources.Cast_MergeButton;
	public static string MergeTitle => AppResources.Cast_MergeTitle;
	public static string MergeBody => AppResources.Cast_MergeBody;
	public static string KeepName => AppResources.Cast_KeepName;
	public static string VoiceTitle => AppResources.Cast_VoiceTitle;
	public static string Cancel => AppResources.Common_Cancel;
	public static string SaveFailedTitle => AppResources.Cast_SaveFailedTitle;
	public static string SaveFailedBody => AppResources.Cast_SaveFailedBody;
	public static string PreviewFailedBody => AppResources.Cast_PreviewFailedBody;
	public static string Ok => AppResources.Common_Ok;
	public static string Back => AppResources.Common_Back;
	public static string LoadFailedTitle => AppResources.Cast_LoadFailedTitle;
	public static string LoadFailedBody => AppResources.Cast_LoadFailedBody;

	/// <summary>The sample line a voice speaks, in the language the voice speaks now (the cast screen never switches the spoken language).</summary>
	public static string PreviewLineFor(string storyLanguage) =>
		AppResources.For(nameof(AppResources.Cast_PreviewLine), System.Globalization.CultureInfo.GetCultureInfo(UiLanguage.Resolve(storyLanguage)));

	public static string Words(int count) =>
		AppResources.Format(count == Singular ? AppResources.Cast_Words_One : AppResources.Cast_Words_Other, count);

	public static string UnknownLines(int count) =>
		AppResources.Format(count == Singular ? AppResources.Cast_UnknownLines_One : AppResources.Cast_UnknownLines_Other, count)
		+ NameAndVoiceSeparator + AppResources.Cast_UsesNarrator;

	public static string HearName(string name) => AppResources.Format(AppResources.Cast_Hear, name);

	public static string NoVoiceOfGender(Gender gender) =>
		gender == Gender.Male ? AppResources.Cast_NoBoyVoice : AppResources.Cast_NoGirlVoice;

	public static string VoiceChanged(IReadOnlyList<string> names) =>
		names.Count == Singular
			? AppResources.Format(AppResources.Cast_VoiceChanged_One, names[0])
			: AppResources.Format(AppResources.Cast_VoiceChanged_Other, string.Join(NameSeparator, names));

	public static string MergeInto(string name) => AppResources.Format(AppResources.Cast_MergeInto, name);

	public static string NameAndVoice(string name, string voiceId) => name + NameAndVoiceSeparator + VoiceName(voiceId);

	public static string SharedNote(int voices) => AppResources.Format(AppResources.Cast_SharedNote, voices);

	public static string GenderLabel(Gender gender) => gender switch
	{
		Gender.Male => AppResources.Cast_Boy,
		Gender.Female => AppResources.Cast_Girl,
		_ => AppResources.Cast_UnknownGender,
	};

	/// <summary>Remembers the display names of a pack's voices so the cast screen can show them.</summary>
	public static void RememberVoices(IEnumerable<VoiceInfo> voices)
	{
		foreach (VoiceInfo voice in voices)
		{
			PackVoiceNames[voice.Id] = voice.DisplayName;
		}
	}

	/// <summary>The voice's display name, or its id when no pack seen so far lists it.</summary>
	public static string VoiceName(string voiceId)
	{
		if (PackVoiceNames.TryGetValue(voiceId, out string? known))
		{
			return known;
		}

		return SupertonicVoiceTable.Voices.FirstOrDefault(v => v.Id == voiceId)?.DisplayName ?? voiceId;
	}
}
