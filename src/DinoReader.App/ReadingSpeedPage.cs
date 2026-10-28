using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;

namespace DinoReader;

/// <summary>
/// The Reading speed sub-page of Settings (Slice 172, mock frame 83): just <see cref="ReadingSpeedCard"/>, saved at once. Its Play button
/// reads the preview line in the narrator's voice at the chosen speed (Slice 150), so the page owns the preview.
/// </summary>
public sealed class ReadingSpeedPage : SettingsSubPage
{
	private readonly ISettingsStore _settings;
	private readonly SpokenLanguage _spoken;
	private readonly StoryVoicing _voicing;
	private readonly VoicePreview _preview;

	public ReadingSpeedPage(ISpeechReader reader, ISettingsStore settings, SpokenLanguage spoken, StoryVoicing voicing)
		: base(AppResources.Speed_Title)
	{
		_settings = settings;
		_spoken = spoken;
		_voicing = voicing;
		_preview = new VoicePreview(reader);
		Body.Children.Add(ReadingSpeedCard.Build(reader, SampleAsync, _preview.Stop));
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_preview.Attach();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_preview.Detach();
	}

	private async Task SampleAsync()
	{
		// The UI language's voice only when its pack is installed, else the language the voice speaks now (the voice and the line always match).
		string language = _voicing.PreviewLanguage(LanguageChoice.Current(_settings), _spoken.Current);
		string line = CastText.PreviewLineFor(language);
		try
		{
			await _preview.PlayAsync(line, _voicing.NarratorOf(language), VoiceFlavour.Storyteller);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader speed sample failed: {ex}");
			// Not awaited: the alert would keep the spinner up until it is dismissed.
			_ = DisplayAlertAsync(CastText.Title, CastText.PreviewFailedBody, CastText.Ok);
		}
	}
}
