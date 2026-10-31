#if DEBUG
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Mobile.Shared.Testing;

namespace ReadTheStupidText.Mobile.Testing;

/// <summary>
/// Read The Stupid Text's own Debug test command (Slice 211): <c>hold-detection</c> keeps the Review page's language detection from running after typing
/// (the idle delay becomes ten minutes), so a flow can tap Play before detection and always reach the detect-now path. It registers itself with the
/// dispatcher, so no start-up code changes; Release has none of it. Outcome line: <c>HOOK hold-detection ok</c>.
/// </summary>
internal static class RtstTestCommands
{
	private const string HoldDetectionName = "hold-detection";
	private const string SetLanguageName = "set-language";
	private const string SetVoiceName = "set-voice";
	private const string SetTranslateName = "set-translate";
	private const string ModeKey = "mode";
	private const string On = "on";
	private const string Off = "off";
	private const string Unavailable = "unavailable";
	private const string AutoVoice = "auto";

	/// <summary>True once a launch asked to hold detection; read by the Review page when it schedules a typed-text detection.</summary>
	internal static volatile bool HoldDetection;

	// A module initializer on purpose: the command registers itself, so the app's start-up code stays untouched.
#pragma warning disable CA2255
	[ModuleInitializer]
#pragma warning restore CA2255
	internal static void Register()
	{
		TestHookDispatcher.AppCommands = Run;
		TestHookDispatcher.AppLaunchKeys = [ModeKey];
	}

	private static string? Run(TestHookCommand command) => command.Name switch
	{
		HoldDetectionName => HoldDetectionOn(),
		SetLanguageName => SetLanguage(command.Get(TestHookDispatcher.LanguageKey, string.Empty)),
		SetVoiceName => SetVoice(command.Get(TestHookDispatcher.IdKey, string.Empty)),
		SetTranslateName => SetTranslate(command.Get(ModeKey, string.Empty)),
		_ => null,
	};

	private static string HoldDetectionOn()
	{
		HoldDetection = true;
		return TestHookDispatcher.Ok;
	}

	// Slice 209: the Settings page's Language row path (LanguageChooser.Apply: setting, culture, the editor draft's before-rebuild, ShowMain), on the main thread
	// the dispatcher already runs on. A code the app does not ship fails loudly. The picked language being the stored one is a no-op, as in the picker.
	private static string SetLanguage(string code)
	{
		if (!AppLanguages.TryParse(code, out AppLanguage language))
		{
			return $"{TestHookDispatcher.Failed} {TestHookDispatcher.Unknown} {code}";
		}

		IServiceProvider services = IPlatformApplication.Current!.Services;
		var settings = services.GetRequiredService<ISettingsStore>();
		string stored = AppLanguages.Code(language);
		if (stored != settings.Language)
		{
			LanguageChooser.Apply(language, settings, services.GetRequiredService<EditorDraft>().PrepareForRebuild);
		}

		return $"{TestHookDispatcher.Ok} {stored}";
	}

	// Slice 209: set-translate mode=on|off sets the Review page's Translate tick for the text on screen (ReviewPage.TrySetTick, the row's own path). Mobile Translate is that per-text tick, not a stored
	// setting, and it has no source-language choice (the source is detected), so there is no from=. Not on the Review page, or a translation running: failed.
	private static string SetTranslate(string mode)
	{
		if (mode is not (On or Off))
		{
			return $"{TestHookDispatcher.Failed} {TestHookDispatcher.Unknown} {mode}";
		}

		if (Shell.Current?.CurrentPage is not ReviewPage review)
		{
			return $"{TestHookDispatcher.Failed} not-on-review";
		}

		if (mode == On && !review.TranslateAvailable)
		{
			return $"{TestHookDispatcher.Failed} {Unavailable}";
		}

		return review.TrySetTick(mode == On) ? $"{TestHookDispatcher.Ok} {mode}" : $"{TestHookDispatcher.Failed} busy";
	}

	// Slice 209: the voice picker's tap path (VoicePickerPage.ApplyVoice: stored, then the reader); Settings shows the new name when it next appears (Slice 99).
	// Only the phone voice or a voice of the installed pack is accepted.
	private static string SetVoice(string id)
	{
		IServiceProvider services = IPlatformApplication.Current!.Services;
		var settings = services.GetRequiredService<ISettingsStore>();
		var model = services.GetRequiredService<IVoiceModelService>();
		string picked = id == AutoVoice ? FirstOtherVoice(model, settings.VoiceId) : id;
		if (picked.Length == 0 || !IsKnownVoice(model, picked))
		{
			return $"{TestHookDispatcher.Failed} {TestHookDispatcher.Unknown} {id}";
		}

		VoicePickerPage.ApplyVoice(settings, services.GetRequiredService<ISpeechReader>(), picked);
		return $"{TestHookDispatcher.Ok} {picked}";
	}

	// id=auto: the first voice of the installed pack that is not the one in use, so a check needs no knowledge of which pack (Supertonic, Piper, Kokoro) the language uses. Empty when the pack has none.
	private static string FirstOtherVoice(IVoiceModelService model, string? current) =>
		model.Paths?.AvailableVoices.Select(v => v.Id).FirstOrDefault(v => v != current) ?? string.Empty;

	private static bool IsKnownVoice(IVoiceModelService model, string id) =>
		id == SystemVoice.Id || (model.Paths?.AvailableVoices.Any(v => v.Id == id) ?? false);
}
#endif
