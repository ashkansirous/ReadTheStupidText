using System.Collections.Concurrent;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>The cast screen's copy (Slice 65), in the UI language (Slice 66).</summary>
public static class CastText
{
	private const string NameAndVoiceSeparator = " · ";
	private const int Singular = 1;
	private const string FlavourHintSeparator = " - ";

	// Display names of the voices in the packs seen so far (a Piper pack names its own voices).
	private static readonly ConcurrentDictionary<string, string> PackVoiceNames = new();

	// Whether the English pool is the Kokoro storyteller's: a kept Supertonic pick is then spoken as, and shown with the name of, its mapped Kokoro voice.
	private static volatile bool _kokoroSpeaksSupertonicPicks;

	public static string Title => AppResources.Cast_Title;
	public static string Narrator => AppResources.Cast_Narrator;
	public static string ChangeForAll => AppResources.Cast_ChangeForAll;
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

	/// <summary>The chip under the header (Slice 108): whose cast is being edited, the language named in itself.</summary>
	public static string VoicesFor(string language) => AppResources.Format(AppResources.Cast_VoicesFor, LanguageNames.NativeName(language));
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

	public static string MergeInto(string name) => AppResources.Format(AppResources.Cast_MergeInto, name);

	public static string NameAndVoice(string name, string voiceId, string language) => name + NameAndVoiceSeparator + VoiceName(voiceId, language);

	public static string Sounds => AppResources.Cast_Sounds;

	/// <summary>The line under a collapsed character (mock frame 49, Slice 114): "Boy · Puck · Small", the gender, the voice and how it sounds.</summary>
	public static string RowSummary(Gender gender, string voiceId, VoiceFlavour flavour, string language) =>
		string.Join(NameAndVoiceSeparator, GenderLabel(gender), VoiceName(voiceId, language), FlavourName(flavour));

	// The storyteller voices card (Slice 114).
	public static string StorytellerTitle => AppResources.Cast_Storyteller_Title;
	public static string StorytellerDownload => AppResources.Cast_Storyteller_Download;
	public static string StorytellerHearOne => AppResources.Cast_Storyteller_HearOne;
	public static string StorytellerOn => AppResources.Cast_Storyteller_On;
	public static string StorytellerUseFirst => AppResources.Cast_Storyteller_UseFirst;
	public static string StorytellerFailed => AppResources.Cast_Storyteller_Failed;
	public static string StorytellerText(int megabytes) => AppResources.Format(AppResources.Cast_Storyteller_Text, megabytes);

	/// <summary>Two labels joined the way this language writes a label and its value (a localized format, so RTL and full-width colons read right).</summary>
	public static string Pair(string label, string value) => AppResources.Format(AppResources.Cast_Pair, label, value);

	/// <summary>The name of a voice flavour as the Sounds picker shows it (Slice 113).</summary>
	public static string FlavourName(VoiceFlavour flavour) => flavour switch
	{
		VoiceFlavour.Small => AppResources.Cast_Flavour_Small,
		VoiceFlavour.Big => AppResources.Cast_Flavour_Big,
		VoiceFlavour.Bouncy => AppResources.Cast_Flavour_Bouncy,
		VoiceFlavour.Sleepy => AppResources.Cast_Flavour_Sleepy,
		_ => AppResources.Cast_Flavour_Normal,
	};

	/// <summary>The line under a flavour's name in the picker; empty for Normal, which needs none.</summary>
	public static string FlavourHint(VoiceFlavour flavour) => flavour switch
	{
		VoiceFlavour.Small => AppResources.Cast_FlavourHint_Small,
		VoiceFlavour.Big => AppResources.Cast_FlavourHint_Big,
		VoiceFlavour.Bouncy => AppResources.Cast_FlavourHint_Bouncy,
		VoiceFlavour.Sleepy => AppResources.Cast_FlavourHint_Sleepy,
		_ => string.Empty,
	};

	/// <summary>A picker entry: the name, then its hint after a dash ("Small - higher, a little quicker").</summary>
	public static string FlavourChoice(VoiceFlavour flavour) =>
		FlavourHint(flavour).Length == 0 ? FlavourName(flavour) : FlavourName(flavour) + FlavourHintSeparator + FlavourHint(flavour);

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
			TrackStorytellerPool(voice);
		}
	}

	// Only the English pool decides: the other languages' Supertonic pools say nothing about the storyteller.
	private static void TrackStorytellerPool(VoiceInfo voice)
	{
		if (voice.Engine == VoiceEngineKind.Kokoro)
		{
			_kokoroSpeaksSupertonicPicks = true;
		}
		else if (voice.Engine == VoiceEngineKind.Supertonic && voice.Language == KokoroVoiceTable.EnglishCode)
		{
			_kokoroSpeaksSupertonicPicks = false;
		}
	}

	/// <summary>The voice's display name for a cast in <paramref name="language"/> (a kept Supertonic pick shows its mapped Kokoro voice for English while the storyteller pool is active), or its id when no pack seen so far lists it.</summary>
	public static string VoiceName(string voiceId, string language)
	{
		string? norwegian = NorwegianLabel(voiceId);
		if (norwegian is not null)
		{
			return norwegian;
		}

		if (_kokoroSpeaksSupertonicPicks && KokoroVoiceTable.Serves(UiLanguage.Resolve(language)) && KokoroVoiceTable.MapFromSupertonic(voiceId) is { } mapped)
		{
			return mapped.DisplayName;
		}

		if (PackVoiceNames.TryGetValue(voiceId, out string? known))
		{
			return known;
		}

		return SupertonicVoiceTable.Voices.FirstOrDefault(v => v.Id == voiceId)?.DisplayName ?? voiceId;
	}

	// The Norwegian pack's speaker codes are shown as a localised "Voice N (woman)" / "Voice N (man)" (Slice 104).
	private static string? NorwegianLabel(string voiceId)
	{
		int? position = NorwegianVoices.Position(voiceId);
		bool? female = NorwegianVoices.IsFemale(voiceId);
		if (position is null || female is null)
		{
			return null;
		}

		return AppResources.Format(female.Value ? AppResources.Voice_NbWoman : AppResources.Voice_NbMan, position.Value);
	}
}
