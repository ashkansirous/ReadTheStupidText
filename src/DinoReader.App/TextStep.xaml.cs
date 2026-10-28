using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;

namespace DinoReader;

/// <summary>
/// Step 3 of the wizard (Slice 78): the title (pre-filled from the first heading the text has), one card per page showing its picture
/// and text (tap to fix the text), the story language and the cast check. Save is the wizard's Next button on this step.
/// </summary>
public partial class TextStep : ContentView
{
	private const double ThumbWidth = 52;
	private const double ThumbHeight = 68;
	private const int PreviewLines = 3;

	private readonly BusyGuard _busy = new();
	private WizardSession? _session;
	private StoryVoicing? _voicing;
	private StoryTranslator? _translator;
	private LanguageCard? _languageCard;
	private CancellationTokenSource? _analysis;
	private bool _settingTitle;
	private string? _analysedKey;

	public TextStep() => InitializeComponent();

	/// <summary>Connects the step to the wizard's session. Called once.</summary>
	public void Attach(WizardSession session)
	{
		_session = session;
		_voicing = session.Services.GetRequiredService<StoryVoicing>();
		_translator = session.Services.GetRequiredService<StoryTranslator>();
		BuildRows();
	}

	private StoryDraft Draft => _session!.Draft;

	/// <summary>The title a story gets when the grown-up types none: the first heading of its text, else its first line.</summary>
	public static string SuggestTitle(StoryDraft draft)
	{
		string first = draft.PageDrafts.Select(p => p.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? string.Empty;
		ReflowUnit? heading = LineReflow.FromPlainText(first).FirstOrDefault(u => u.Kind == UnitKind.Heading);
		return StoryTitles.FromText(heading?.Text ?? first);
	}

	public void Show()
	{
		if (_session is null)
		{
			return;
		}

		_settingTitle = true;
		TitleEntry.Text = string.IsNullOrWhiteSpace(Draft.Title) ? SuggestTitle(Draft) : Draft.Title;
		_settingTitle = false;
		BuildCards();
		DetectLanguage();
		ShowLanguage();
		if (AnalysisIsCurrent())
		{
			ShowCast(Draft.Cast);
			return;
		}

		_ = AnalyseSafelyAsync();
	}

	// What the cast analysis depends on; when it has not changed since the last analysis, the cast is shown as it is.
	private string AnalysisKey() => string.Join('', Draft.Language, Story.JoinPages(Draft.Pages));

	private bool AnalysisIsCurrent() => Draft.Cast.Count > 0 && _analysedKey == AnalysisKey();

	/// <summary>Shows the cast as it stands (after the cast page changed it) without analysing the story again.</summary>
	public void ShowCurrentCast()
	{
		if (_session is not null && Draft.Cast.Count > 0)
		{
			ShowCast(Draft.Cast);
		}
	}

	private void OnTitleChanged(object? sender, TextChangedEventArgs e)
	{
		if (!_settingTitle && _session is not null)
		{
			Draft.Title = e.NewTextValue ?? string.Empty;
		}
	}

	private void BuildCards()
	{
		PageCards.Children.Clear();
		for (int i = 0; i < Draft.PageDrafts.Count; i++)
		{
			PageCards.Children.Add(PageCard(i, Draft.PageDrafts[i]));
		}
	}

	private Border PageCard(int index, PageDraft page)
	{
		var texts = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
		texts.Children.Add(new Label { Text = AppResources.Format(AppResources.Wizard_PageN, index + 1), FontSize = 12, TextColor = DinoTokens.Get(DinoTokens.Muted) });
		bool blank = string.IsNullOrWhiteSpace(page.Text);
		texts.Children.Add(new Label
		{
			Text = blank ? AppResources.Wizard_NoText : page.Text, FontFamily = DinoFonts.Reading, FontSize = 14, MaxLines = PreviewLines,
			LineBreakMode = LineBreakMode.TailTruncation,
			TextColor = blank ? DinoTokens.Get(DinoTokens.Muted) : DinoTokens.Get(DinoTokens.Ink),
		});
		var grid = new Grid
		{
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
			ColumnSpacing = 12,
		};
		grid.Add(Thumbnail(page), 0);
		grid.Add(texts, 1);
		bool withRegions = RegionPicker.Offers(page) && page.PictureFile is not null;
		var card = new Border
		{
			BackgroundColor = Colors.White, StrokeThickness = 0, Padding = 10,
			StrokeShape = new RoundRectangle { CornerRadius = 16 }, Content = withRegions ? WithRegions(grid, index, page) : grid,
		};
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) => await _busy.RunAsync(() => _session!.Host.Navigation.PushAsync(new PageTextPage(Draft, index)));
		// With the picture's boxes under the text, only the text row opens the editor: a tap on a box is the box's own.
		(withRegions ? grid : (View)card).GestureRecognizers.Add(tap);
		return card;
	}

	// A page with text regions that are worth choosing between (Slice 79) shows its picture with them outlined under the text;
	// a tap on a box includes or leaves out that region and the cards are drawn again with the text recomposed.
	private View WithRegions(Grid grid, int index, PageDraft page)
	{
		var overlay = new RegionOverlay(page.PictureFile!, page.Ocr!.Regions!, region => _ = _busy.RunAsync(() => ToggleRegionAsync(index, region)));
		return new VerticalStackLayout { Spacing = 8, Children = { grid, overlay, RegionPicker.Helper() } };
	}

	// Typed text on the page is asked about first; Keep leaves the page as it is. Otherwise only this page's card is drawn again
	// and the cast is analysed again, as the text it was analysed from has changed.
	private async Task ToggleRegionAsync(int index, int region)
	{
		PageDraft page = Draft.PageDrafts[index];
		if (!await RegionPicker.MayReplaceAsync(_session!.Host, page.HasEditedText))
		{
			return;
		}

		Draft.ReplacePageDraft(page.WithRegionToggled(region));
		PageCards.Children[index] = PageCard(index, Draft.PageDrafts[index]);
		_ = AnalyseSafelyAsync();
	}

	private static Border Thumbnail(PageDraft page)
	{
		View content = page.PictureFile is { } file
			? new Image { Source = file, Aspect = Aspect.AspectFill }
			: new IconView { Kind = IconKind.Type, Color = DinoTokens.Get(DinoTokens.Brand), Size = 22, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		return new Border
		{
			WidthRequest = ThumbWidth, HeightRequest = ThumbHeight, StrokeThickness = 0, Padding = 0, Content = content,
			BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
			StrokeShape = new RoundRectangle { CornerRadius = 8 },
		};
	}

	// The language card (Slice 66), built once: the language and, when this build can translate, the switch.
	private void BuildRows()
	{
		var card = new LanguageCard(CanTranslate(), _translator!.IsAvailable, Draft.Language, ChooseLanguageAsync, _busy) { Translate = Draft.TranslateToMyLanguage };
		card.TranslateChanged += () => Draft.TranslateToMyLanguage = card.Translate;
		_languageCard = card;
		LanguageHost.Content = card;
	}

	// The switch works when the story's language to my language can be translated (a table, or an engine that supports the pair) or
	// there is nothing to translate; otherwise it shows greyed (Slice 107). Re-checked whenever the story language changes.
	private bool CanTranslate() => _translator!.CanTranslate(Draft.Current, StoryTranslator.MyLanguage); // the grown-up writing a book: the app language

	private void ShowLanguage()
	{
		_languageCard!.SetCanTranslate(CanTranslate(), _translator!.IsAvailable, Draft.Language);
		_languageCard.Show(Draft.Language, _session!.LanguageDetected);
	}

	// The language is guessed from the text until the grown-up picks one; "· detected" shows while the guess stands.
	private void DetectLanguage()
	{
		if (_session!.LanguageChosen)
		{
			return;
		}

		if (ScriptLanguageGuesser.Guess(Story.JoinPages(Draft.Pages)) is not { } guess)
		{
			_session.LanguageDetected = false;
			return;
		}

		Draft.Language = guess;
		_session.LanguageDetected = true;
	}

	private async Task ChooseLanguageAsync()
	{
		if (await LanguageChooser.ChooseAsync(_session!.Host) is not { } picked)
		{
			return;
		}

		_analysis?.Cancel();
		Draft.Language = picked;
		_session.LanguageChosen = true;
		_session.LanguageDetected = false;
		ShowLanguage();
		await AnalyseSafelyAsync();
	}

	// The preview is a convenience: a failed analysis hides the card, it never crashes the app.
	private async Task AnalyseSafelyAsync()
	{
		_analysis?.Cancel();
		var analysis = _analysis = new CancellationTokenSource();
		try
		{
			await AnalyseAsync(analysis.Token);
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			DinoLog.Error(_session!.Services, "cast preview failed", ex);
			CastCard.IsVisible = false;
		}
	}

	private async Task AnalyseAsync(CancellationToken token)
	{
		string text = Story.JoinPages(Draft.Pages);
		if (string.IsNullOrWhiteSpace(text))
		{
			CastCard.IsVisible = false;
			return;
		}

		string key = AnalysisKey();
		IReadOnlyList<CastMember> cast = await _voicing!.CastAsync(Draft.Language, text, Draft.Cast, Story.BreakMapOf(Draft.Pages));
		token.ThrowIfCancellationRequested();
		_analysedKey = key;
		Draft.Cast = cast;
		ShowCast(cast);
	}

	private void ShowCast(IReadOnlyList<CastMember> cast)
	{
		SummaryLabel.Text = GalleryText.ImportSummary(Draft.PageDrafts.Count, cast.Count);
		NameChips.Children.Clear();
		foreach (CastMember member in cast)
		{
			NameChips.Children.Add(NameChip(member.Name));
		}

		CastCard.IsVisible = true;
	}

	private static Border NameChip(string name) => new()
	{
		BackgroundColor = Color.FromArgb("#1FFFFFFF"),
		StrokeThickness = 0,
		Padding = new Thickness(10, 4),
		Margin = new Thickness(0, 0, 6, 6),
		StrokeShape = new RoundRectangle { CornerRadius = 10 },
		Content = new Label { Text = name, FontSize = 12.5, TextColor = Colors.White },
	};

	private async void OnCheckCastClicked(object? sender, EventArgs e) => await _busy.RunAsync(OpenCastAsync);

	// The cast page works on the draft as it stands, in memory: nothing is saved until Save story.
	private async Task OpenCastAsync()
	{
		if (string.IsNullOrWhiteSpace(Story.JoinPages(Draft.Pages)))
		{
			return;
		}

		_analysis?.Cancel();
		Draft.Title = TitleOrSuggestion();
		var page = _session!.Services.GetRequiredService<CastPage>();
		page.Load(Draft.Current, story =>
		{
			Draft.Cast = story.Cast;
			Draft.LanguageCasts = story.LanguageCasts;
			Draft.AnimalSoundsOn = story.AnimalSoundsOn;
			return Task.CompletedTask;
		});
		await _session.Host.Navigation.PushAsync(page);
	}

	private string TitleOrSuggestion() =>
		string.IsNullOrWhiteSpace(TitleEntry.Text) ? SuggestTitle(Draft) : TitleEntry.Text.Trim();

	// A new book is counted once, after it is saved: how it came in (photo, pdf, scan, else typed) and its language, nothing the grown-up wrote (Slice 134).
	// Counting never decides whether the save worked: it has its own catch and runs after the save's.
	private void CountNewOwnBook(WizardSession session)
	{
		try
		{
			if (session.IsNew && session.TryMarkCounted())
			{
				Story own = Draft.Original with { Language = Draft.Language, SourceId = null };
				session.Services.GetRequiredService<UsageCounter>().Record(UsageEventName.OwnBookAdded, own, session.OwnBookKind ?? UsageDetail.KindTyped);
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not count a new book: {ex.Message}");
		}
	}

	/// <summary>Saves the story. False (with the reason shown) when there is nothing to read yet or the save failed.</summary>
	public async Task<bool> SaveAsync()
	{
		WizardSession session = _session!;
		if (string.IsNullOrWhiteSpace(Story.JoinPages(Draft.Pages)))
		{
			await session.Host.DisplayAlertAsync(AppResources.Wizard_NeedTextTitle, AppResources.Wizard_NeedTextBody, AppResources.Common_Ok);
			return false;
		}

		_analysis?.Cancel();
		if (!await TrySaveAsync(session))
		{
			return false;
		}

		CountNewOwnBook(session);
		await OfferTranslationAsync(session);
		return true;
	}

	// The saved book is translated on the phone when the parent wants it and this build can (Slice 159): the pack question, the download and the translation
	// run behind the page's wait with Cancel, and a failure never undoes the save.
	private async Task OfferTranslationAsync(WizardSession session)
	{
		if (_translator is null)
		{
			return; // iOS and Windows have no engine yet
		}

		try
		{
			var prompt = new TranslationPackPrompt(_translator, session.Store);
			await prompt.OfferAsync(session.Host, Draft.Original.Id, session.BeginWait, session.EndWait);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			DinoLog.Error(session.Services, "translating the saved book failed", ex);
		}
	}

	private async Task<bool> TrySaveAsync(WizardSession session)
	{
		try
		{
			var saver = new StorySaver(session.Store, session.Services.GetRequiredService<IPageImageKeeper>(), _voicing!);
			string title = TitleOrSuggestion();
			await saver.SaveAsync(Draft, session.IsNew, string.IsNullOrWhiteSpace(title) ? Draft.Original.Title : title);
			return true;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			DinoLog.Error(session.Services, "saving the story failed", ex);
			await session.Host.DisplayAlertAsync(AppResources.Wizard_SaveFailedTitle, AppResources.Wizard_SaveFailedBody, AppResources.Common_Ok);
			return false;
		}
	}
}
