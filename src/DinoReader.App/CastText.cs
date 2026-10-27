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

	// The roster each language can speak with its voices now (Slice 116), remembered when the language's pool is built: a voice is named by its storyteller character.
	private static readonly ConcurrentDictionary<string, IReadOnlyList<StorytellerCharacter>> Rosters = new();

	public static string Title => AppResources.Cast_Title;
	public static string Narrator => AppResources.Cast_Narrator;
	public static string ChangeForAll => AppResources.Cast_ChangeForAll;
	public static string Main => AppResources.Cast_Main;
	public static string Edited => AppResources.Cast_Edited;
	public static string SharedVoice => AppResources.Cast_SharedVoice;
	public static string UnknownSpeaker => AppResources.Cast_UnknownSpeaker;
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

	/// <summary>"Little Pig · Cheeky Piglet": a story character and the storyteller character playing it (Slice 116).</summary>
	public static string NameAndCharacter(string name, string voiceId, VoiceFlavour flavour, string language) =>
		name + NameAndVoiceSeparator + CharacterName(voiceId, flavour, language);

	/// <summary>"Narrator · Story Granny": the narrator row of the cast screen.</summary>
	public static string NarratorLine(string voiceId, string language) => Narrator + NameAndVoiceSeparator + VoiceName(voiceId, language);

	// The storyteller voices by default (Slice 115).
	public static string StorytellerGetting => AppResources.Storyteller_Getting;
	public static string StorytellerWaitWifi => AppResources.Storyteller_WaitWifi;
	public static string StorytellerDownloadNow => AppResources.Storyteller_DownloadNow;
	public static string StorytellerFailed => AppResources.Storyteller_Failed;
	public static string StorytellerTryAgain => AppResources.Storyteller_TryAgain;
	public static string StorytellerSize(int megabytes) => AppResources.Format(AppResources.Storyteller_Size, megabytes);
	public static string StorytellerReady => AppResources.Storyteller_Ready;
	public static string StorytellerReadyBody => AppResources.Storyteller_ReadyBody;
	public static string StorytellerYourNarrator(string language) => AppResources.Format(AppResources.Storyteller_YourNarrator, LanguageNames.NativeName(language));

	/// <summary>Two labels joined the way this language writes a label and its value (a localized format, so RTL and full-width colons read right).</summary>
	public static string Pair(string label, string value) => AppResources.Format(AppResources.Cast_Pair, label, value);

	// The character picker sheet (Slice 116, mock frame 53).
	public static string PickTitle(string name) => AppResources.Format(AppResources.Cast_PickTitle, name);
	public static string PickSubtitle(string language) => AppResources.Format(AppResources.Cast_PickSubtitle, LanguageNames.NativeName(language));
	public static string FilterGirls => AppResources.Cast_FilterGirls;
	public static string FilterBoys => AppResources.Cast_FilterBoys;
	public static string FilterEveryone => AppResources.Cast_FilterEveryone;

	/// <summary>A character's plain words, then the story character already playing it in this book ("small and quick · Little Pig").</summary>
	public static string HintUsedBy(string hint, string? usedBy) => usedBy is null ? hint : hint + NameAndVoiceSeparator + usedBy;

	public static string SharedNote(int voices) => AppResources.Format(AppResources.Cast_SharedNote, voices);

	/// <summary>Remembers the roster <paramref name="language"/>'s voices can speak, so the screens can name a stored voice and flavour.</summary>
	public static void RememberRoster(string language, IReadOnlyList<StorytellerCharacter> roster) => Rosters[UiLanguage.Resolve(language)] = roster;

	/// <summary>Where a language's adapted roster comes from when none is remembered yet (set by StoryVoicing, which builds it from the language's voices), so a name never comes from the unadapted roster.</summary>
	public static Func<string, IReadOnlyList<StorytellerCharacter>>? RosterSource { get; set; }

	private static IReadOnlyList<StorytellerCharacter> RosterOf(string language) =>
		Rosters.TryGetValue(UiLanguage.Resolve(language), out var known) ? known : RosterSource?.Invoke(language) ?? CharacterRoster.For(language);

	/// <summary>The name of the storyteller character nearest to a voice and flavour in <paramref name="language"/>'s roster (never an engine or voice name).</summary>
	public static string CharacterName(string voiceId, VoiceFlavour flavour, string language)
	{
		IReadOnlyList<StorytellerCharacter> roster = RosterOf(language);
		return CharacterRoster.Nearest(roster.Where(c => c.Kind == CharacterKind.Speaker).ToList(), voiceId, flavour)?.Name ?? string.Empty;
	}

	/// <summary>Sets a label that shows a character's name or words in the direction and font of its own script (Slice 116: a Farsi name reads right to left in Vazirmatn whatever the screen's language is).</summary>
	public static Label InOwnScript(Label label)
	{
		label.FlowDirection = UiFlow.ForText(label.Text);
		if (label.FlowDirection == FlowDirection.RightToLeft)
		{
			label.FontFamily = DinoFonts.Persian;
		}

		return label;
	}

	/// <summary>Like <see cref="InOwnScript"/>, and a right-to-left label also sits against the right edge of its column (the header and name of mock 54), whatever the screen's language is.</summary>
	public static Label InOwnScriptAligned(Label label)
	{
		InOwnScript(label);
		label.HorizontalOptions = label.FlowDirection == FlowDirection.RightToLeft ? LayoutOptions.End : LayoutOptions.Start;
		return label;
	}

	/// <summary>Puts <paramref name="follower"/> on the same edge as <paramref name="lead"/>: the right when the lead is right to left (the subtitle under a Farsi title).</summary>
	public static void AlignWith(Label follower, Label lead) =>
		follower.HorizontalOptions = lead.FlowDirection == FlowDirection.RightToLeft ? LayoutOptions.End : LayoutOptions.Start;

	/// <summary>
	/// The inside of a picker: the name and a chevron. The layout never mirrors, so a left-to-right name has the chevron at the right and a right-to-left one (Farsi, Arabic)
	/// has it at the left with the name against the right edge, so the two never overlap (mock 54).
	/// </summary>
	public static Grid PickerLine(string text, double fontSize, double chevronSize, LineBreakMode lineBreak = LineBreakMode.TailTruncation)
	{
		Label name = InOwnScriptAligned(new Label { Text = text, FontSize = fontSize, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, LineBreakMode = lineBreak });
		bool rtl = name.FlowDirection == FlowDirection.RightToLeft;
		var chevron = new IconView { Kind = IconKind.ChevronDown, Color = DinoTokens.Get(DinoTokens.Body), Size = chevronSize, StrokeWidth = 1.6, VerticalOptions = LayoutOptions.Center };
		var line = new Grid { ColumnSpacing = 8, FlowDirection = FlowDirection.LeftToRight };
		line.ColumnDefinitions = rtl
			? [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)]
			: [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)];
		line.Add(rtl ? chevron : name, 0);
		line.Add(rtl ? name : chevron, 1);
		return line;
	}

	/// <summary>The name of the narrator who has <paramref name="voiceId"/> in <paramref name="language"/> (a voice no narrator has is named by its nearest character).</summary>
	public static string VoiceName(string voiceId, string language)
	{
		IReadOnlyList<StorytellerCharacter> roster = RosterOf(language);
		StorytellerCharacter? narrator = roster.FirstOrDefault(c => c.Kind == CharacterKind.Narrator && c.VoiceId == voiceId);
		return narrator?.Name ?? CharacterName(voiceId, VoiceFlavour.Storyteller, language);
	}
}
