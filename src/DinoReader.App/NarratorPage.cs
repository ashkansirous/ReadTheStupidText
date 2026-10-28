using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>
/// The middle Narrator page (Slice 172, mock 82a): one row per story language with that language's narrator, opening its voice list
/// (<see cref="NarratorPage"/>). Every UI language is listed, the story languages first.
/// </summary>
public sealed class NarratorLanguagesPage : SettingsSubPage
{
	private readonly StoryVoicing _voicing;
	private readonly ISettingsStore _settings;
	private readonly IServiceProvider _services;

	public NarratorLanguagesPage(StoryVoicing voicing, ISettingsStore settings, IServiceProvider services)
		: base(AppResources.Settings_NarratorVoice)
	{
		_voicing = voicing;
		_settings = settings;
		_services = services;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Rebuild();
	}

	// The narrator of a language can change on the next page, so the rows are built again each time the page shows.
	private void Rebuild()
	{
		Body.Children.Clear();
		Body.Children.Add(new Label { Text = AppResources.Narrator_ListIntro, FontSize = 13.5, LineHeight = 1.45, TextColor = DinoTokens.Get(DinoTokens.Body), Margin = new Thickness(4, 0) });
		string mine = LanguageChoice.Current(_settings);
		IReadOnlyList<string> installed = _voicing.InstalledLanguages(mine);
		Body.Children.Add(SettingsRows.RowsCard(StoryVoicing.AllLanguages(StoryLanguages.Current().All).Select(language => Row(language, installed.Contains(language)))));
	}

	// A language with no voice pack shows "Not downloaded" and still opens, to the page that says how to get it.
	private Grid Row(string language, bool hasPack)
	{
		string value = hasPack ? CastText.VoiceName(_voicing.NarratorOf(language), language) : AppResources.Narrator_NoVoiceValue;
		return SettingsRows.NavRow(LanguageNames.NativeName(language), value, () => OpenAsync(language), Busy);
	}

	private Task OpenAsync(string language)
	{
		var page = _services.GetRequiredService<NarratorPage>();
		page.Language = language;
		return Navigation.PushAsync(page);
	}
}

/// <summary>
/// The grown-up Narrator page for one language (Slice 109, Decision 114; one language since Slice 172, mock 82b): a card with a row per narrator
/// character of that language, the chosen one checked, each with a sample. The title names the language. The pick is the narrator of every book
/// read in that language (starter, store and own) and is kept per language (<see cref="NarratorChoice"/>), so a change re-casts, quietly, any
/// character of any saved book who had that voice (<see cref="NarratorRecast"/>).
/// </summary>
public sealed class NarratorPage : SettingsSubPage
{
	private const double CardRadius = 18;
	private const double SampleSize = 44;
	private const double RowHeight = 60;
	private const double CheckSize = 24;

	private readonly StoryVoicing _voicing;
	private readonly IStoryLibrary _library;
	private readonly VoicePreview _preview;
	private readonly SpokenLanguage _spoken;
	private readonly ISettingsStore _settings;
	private readonly UsageCounter _usage;
	private readonly WaitOverlay _waitOverlay = DinoWait.OverPage();
	private readonly WaitController _wait;
	private CancellationTokenSource? _sampleCancel;

	public NarratorPage(StoryVoicing voicing, IStoryLibrary library, ISpeechReader reader, SpokenLanguage spoken, ISettingsStore settings, UsageCounter usage)
		: base(AppResources.Settings_NarratorVoice)
	{
		_usage = usage;
		_voicing = voicing;
		_library = library;
		_preview = new VoicePreview(reader);
		_spoken = spoken;
		_settings = settings;
		_wait = new WaitController(_waitOverlay);
		_wait.CancelRequested += OnWaitCancelled;
		AddOverlay(_waitOverlay);
	}

	/// <summary>The language whose narrator this page picks; the app's own when not set.</summary>
	public string Language { get; set; } = string.Empty;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_preview.Attach();
		Rebuild();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_preview.Detach();
	}

	private string CurrentLanguage => Language.Length > 0 ? Language : UiLanguage.Resolve(LanguageChoice.Current(_settings));

	private void Rebuild()
	{
		string language = CurrentLanguage;
		string name = LanguageNames.NativeName(language);
		SetTitle(AppResources.Format(AppResources.Narrator_TitleFor, name));
		string current = _voicing.NarratorOf(language);
		Body.Children.Clear();
		Body.Children.Add(new Label { Text = AppResources.Format(AppResources.Narrator_OneIntro, name), FontSize = 13.5, LineHeight = 1.45, TextColor = DinoTokens.Get(DinoTokens.Body), Margin = new Thickness(4, 0) });
		if (_voicing.NarratorsFor(language).Count == 0)
		{
			Body.Children.Add(NoVoiceCard(name));
			return;
		}

		Body.Children.Add(Card(language, current));
		Body.Children.Add(new Label { Text = AppResources.Narrator_Footnote, FontSize = 13, LineHeight = 1.45, TextColor = DinoTokens.Get(DinoTokens.Body), Margin = new Thickness(4, 2) });
	}

	// No pack for this language: say how to get it instead of an empty card.
	private static Border NoVoiceCard(string name) => SettingsRows.RowsCard([new Label
	{
		Text = AppResources.Format(AppResources.Narrator_NoVoiceHint, name),
		FontSize = 14.5,
		LineHeight = 1.45,
		Padding = new Thickness(14, 14),
		TextColor = DinoTokens.Get(DinoTokens.Body),
	}]);

	private Border Card(string language, string current) =>
		SettingsRows.RowsCard(_voicing.NarratorsFor(language).Select(narrator => NarratorRow(language, narrator, narrator.VoiceId == current)));

	private Grid NarratorRow(string language, StorytellerCharacter narrator, bool selected)
	{
		var text = new VerticalStackLayout
		{
			VerticalOptions = LayoutOptions.Center,
			Children =
			{
				CastText.InOwnScript(new Label { Text = narrator.Name, FontSize = 15, FontAttributes = FontAttributes.Bold }),
				CastText.InOwnScript(new Label { Text = narrator.Hint, FontSize = 12.5, TextColor = DinoTokens.Get(DinoTokens.Muted) }),
			},
		};
		var row = new Grid
		{
			MinimumHeightRequest = RowHeight,
			Padding = new Thickness(14, 8),
			ColumnSpacing = 10,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
		};
		row.Add(CheckCircle(selected), 0);
		row.Add(text, 1);
		row.Add(SampleButton(language, narrator.VoiceId), 2);
		SemanticProperties.SetDescription(text, CastText.Pair(narrator.Name, narrator.Hint));
		SemanticProperties.SetHint(text, selected ? ReadingText.ModeSelected : null);
		CastViews.AddTap(text, () => Busy.RunAsync(() => ChooseAsync(language, narrator)));
		return row;
	}

	private static Border CheckCircle(bool filled)
	{
		var circle = new Border
		{
			WidthRequest = CheckSize,
			HeightRequest = CheckSize,
			VerticalOptions = LayoutOptions.Center,
			StrokeShape = new RoundRectangle { CornerRadius = CheckSize / 2 },
			BackgroundColor = filled ? DinoTokens.Get(DinoTokens.Brand) : Colors.Transparent,
			Stroke = filled ? DinoTokens.Get(DinoTokens.Brand) : DinoTokens.Get(DinoTokens.CheckEmpty),
			StrokeThickness = filled ? 0 : 2,
		};
		if (filled)
		{
			circle.Content = new IconView { Kind = IconKind.Check, Color = Colors.White, Size = 13, StrokeWidth = 3.4, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		}

		return circle;
	}

	private Border SampleButton(string language, string voice)
	{
		var button = new Border
		{
			WidthRequest = SampleSize,
			HeightRequest = SampleSize,
			StrokeThickness = 0,
			BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
			StrokeShape = new RoundRectangle { CornerRadius = SampleSize / 2 },
			Content = new IconView { Kind = IconKind.Play, Color = DinoTokens.Get(DinoTokens.Brand), Filled = true, Size = 16 },
		};
		SemanticProperties.SetDescription(button, AppResources.Narrator_Sample + ": " + CastText.VoiceName(voice, language));
		CastViews.AddTap(button, () => Busy.RunAsync(() => SampleAsync(language, voice)));
		return button;
	}

	private async Task ChooseAsync(string language, StorytellerCharacter narrator)
	{
		if (narrator.VoiceId != _voicing.NarratorOf(language))
		{
			_voicing.SetNarrator(language, narrator.VoiceId);
			_usage.RecordGeneral(UsageEventName.VoiceChanged, language, UsageDetail.ChangeNarrator);
			await _wait.RunAsync(WaitStage.Saving, AppResources.Wait_Saving, canCancel: true, RecastLibraryAsync);
		}

		Rebuild();
	}

	// Every saved book follows the new narrator at once: a character who had its voice is given another (starter, store and own books alike).
	// Cancel stops after the book being saved: the narrator is already kept, and a book not yet recast is freed of the narrator's voice when its cast screen opens.
	private async Task<bool> RecastLibraryAsync(CancellationToken cancel)
	{
		try
		{
			foreach (Story story in await _library.ListAsync())
			{
				cancel.ThrowIfCancellationRequested();
				Story? changed = await NarratorRecast.TryRecastAsync(_voicing, story);
				if (changed is not null)
				{
					await _library.UpdateAsync(changed);
				}
			}
		}
		catch (OperationCanceledException)
		{
			return false;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader narrator recast failed: {ex}");
			await DisplayAlertAsync(CastText.SaveFailedTitle, CastText.SaveFailedBody, CastText.Ok);
			return false;
		}

		return true;
	}

	// Cancel stops the sample and releases the wait at once, even if the reader is slow to return; a deliberate cancel is never a failure.
	private void OnWaitCancelled()
	{
		_sampleCancel?.Cancel();
		_preview.Stop();
	}

	private async Task SampleAsync(string language, string voice)
	{
		string line = CastText.PreviewLineFor(_voicing.PreviewLanguage(language, _spoken.Current));
		bool failed = false;
		using var cancel = new CancellationTokenSource();
		_sampleCancel = cancel;
		_wait.Begin(WaitStage.PreparingVoice, AppResources.Wait_GettingVoices, canCancel: true);
		try
		{
			await _preview.PlayAsync(line, voice, VoiceFlavour.Storyteller).WaitAsync(cancel.Token);
		}
		catch (OperationCanceledException)
		{
			Debug.WriteLine("Dino Reader narrator sample cancelled.");
		}
		catch (Exception ex) when (!cancel.IsCancellationRequested)
		{
			Debug.WriteLine($"Dino Reader narrator sample failed: {ex}");
			failed = true;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader narrator sample ended after cancel: {ex.Message}");
		}
		finally
		{
			_sampleCancel = null;
			_wait.End();
		}

		if (failed)
		{
			await DisplayAlertAsync(CastText.Title, CastText.PreviewFailedBody, CastText.Ok);
		}
	}
}
