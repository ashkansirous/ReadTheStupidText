using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>
/// What the three wizard steps share (Slice 78): the draft, the folder the pictures being edited live in until Save, and a
/// library view that finds a draft page's current picture (its transformed copy, or the picked file) by the name
/// <see cref="StoryDraft.DraftImageName"/> gives it, so the cover picker and the cover preview work on drafts unchanged.
/// </summary>
public sealed class WizardSession
{
	private const string WorkFolderPrefix = "dino-wizard-";

	public WizardSession(StoryDraft draft, bool isNew, IStoryLibrary library, IServiceProvider services, Page host)
	{
		Draft = draft;
		IsNew = isNew;
		Services = services;
		Host = host;
		Store = library;
		Library = new DraftLibrary(library, PictureOfDraftName);
		WorkFolder = Path.Combine(FileSystem.CacheDirectory, WorkFolderPrefix + Guid.NewGuid().ToString("N"));
	}

	public StoryDraft Draft { get; }

	public bool IsNew { get; }

	private bool _counted;

	/// <summary>True the first time only: a new book is counted once per session, however many times it is saved.</summary>
	public bool TryMarkCounted()
	{
		bool first = !_counted;
		_counted = true;
		return first;
	}

	/// <summary>Ids of the pages that are picture + text pages (Slice 98): a rotate or crop clears their reading, but they are never read by OCR.</summary>
	public HashSet<string> PictureTextPages { get; } = [];

	/// <summary>The name of the file the grown-up imported pages from (with its extension), shown under the Pages title; null for scans, photos and typed text.</summary>
	public string? FileName { get; set; }

	public IServiceProvider Services { get; }

	/// <summary>The wizard page, for alerts and action sheets from the steps.</summary>
	public Page Host { get; }

	/// <summary>The real library (saves go here).</summary>
	public IStoryLibrary Store { get; }

	/// <summary>The library the previews and the cover picker read: a draft page name resolves to its current picture.</summary>
	public IStoryLibrary Library { get; }

	public string WorkFolder { get; }

	private readonly Dictionary<string, string> _editFiles = [];

	public bool LanguageChosen { get; set; }

	public bool LanguageDetected { get; set; }

	private WaitController? _wait;

	/// <summary>The page's wait controller, for a download that shows the waiting-for-internet look (Slice 187); null before it is attached.</summary>
	public WaitController? Wait => _wait;

	/// <summary>Cancelled when Cancel is tapped on the wait in progress; none when no wait runs.</summary>
	public CancellationToken WaitToken => _wait?.Token ?? CancellationToken.None;

	/// <summary>Hands the session the page's wait overlay (Slice 143).</summary>
	public void AttachWait(WaitController wait)
	{
		_wait = wait;
	}

	/// <summary>Starts a wait behind the page's overlay (shown after 500 ms, over the whole page); the token is cancelled by its Cancel button.</summary>
	public CancellationToken BeginWait(WaitStage stage, string line)
	{
		return _wait?.BeginCancellable(stage, line) ?? CancellationToken.None;
	}

	/// <summary>Counts the pages read so far: the overlay's second line ("Page 3 of 8").</summary>
	public void ReportWait(int current, int total)
	{
		_wait?.Report(AppResources.Format(AppResources.Wait_PageOf, current, total));
	}

	public void EndWait()
	{
		_wait?.End();
	}

	/// <summary>The PDF import's wait (Pages step): the reading line (with Cancel, see <see cref="WaitToken"/>), then the step's own count as the second line.
	/// Empty text starts the wait, other text is the second line, null ends it.</summary>
	public void ShowProgress(string? text)
	{
		if (text is null)
		{
			EndWait();
		}
		else if (_wait is { IsActive: true })
		{
			_wait.Report(text);
		}
		else
		{
			BeginWait(WaitStage.ReadingPictures, AppResources.Wait_ReadingPictures);
		}
	}

	/// <summary>Raised when the pages changed, so a step that shows them can rebuild.</summary>
	public event Action? PagesChanged;

	public void NotifyPagesChanged() => PagesChanged?.Invoke();

	private string? PictureOfDraftName(string draftName)
	{
		string id = draftName[StoryDraft.DraftImagePrefix.Length..];
		PageDraft? page = Draft.PageDrafts.FirstOrDefault(p => p.Id == id);
		return page?.PictureFile;
	}

	/// <summary>Writes the page's rotated and cropped picture (from the original, never over it) when it has one to write.</summary>
	public async Task<PageDraft> RenderAsync(PageDraft page)
	{
		if (page.SourceFile is not { } source || page.PictureFile is not null || !page.NeedsTransform)
		{
			return page;
		}

		string target = Path.Combine(WorkFolder, $"edit-{Guid.NewGuid():N}.jpg");
		CropRect? crop = page.Crop is { } c ? new CropRect(c.X, c.Y, c.Width, c.Height) : null;
		await ImageTransform.ApplyAsync(source, target, page.RotationQuarterTurns, crop);
		ReplaceEditFile(page.Id, target);
		return page.WithTransformedFile(target);
	}

	// Each page keeps only its latest rendered copy; the one before it is deleted so a page turned many times leaves one file.
	private void ReplaceEditFile(string pageId, string target)
	{
		if (_editFiles.TryGetValue(pageId, out string? previous))
		{
			ScannedImageFiles.TryDelete(previous);
		}

		_editFiles[pageId] = target;
	}

	/// <summary>Forgets the pictures this session made. Run on every exit path.</summary>
	public void Cleanup()
	{
		PickedPhotoCache.Delete(Draft.PendingCoverPhoto);
		try
		{
			if (Directory.Exists(WorkFolder))
			{
				Directory.Delete(WorkFolder, recursive: true);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not clear the wizard folder: {ex.Message}");
		}
	}
}

/// <summary>The library with one difference: a draft page's picture name resolves to its current file.</summary>
internal sealed class DraftLibrary(IStoryLibrary inner, Func<string, string?> resolveDraft) : IStoryLibrary
{
	public Task<IReadOnlyList<Story>> ListAsync() => inner.ListAsync();

	public Task<Story?> GetAsync(string id) => inner.GetAsync(id);

	public Task AddAsync(Story story) => inner.AddAsync(story);

	public Task UpdateAsync(Story story) => inner.UpdateAsync(story);

	public Task DeleteAsync(string id) => inner.DeleteAsync(id);

	public string FolderOf(string id) => inner.FolderOf(id);

	public string? TryResolveFile(string storyId, string? fileName) =>
		fileName is not null && fileName.StartsWith(StoryDraft.DraftImagePrefix, StringComparison.Ordinal)
			? resolveDraft(fileName)
			: inner.TryResolveFile(storyId, fileName);

	public Task<string> StoreFileAsync(string storyId, string prefix, string extension, Func<string, Task> write) =>
		inner.StoreFileAsync(storyId, prefix, extension, write);

	public Task DeleteFileAsync(string storyId, string fileName) => inner.DeleteFileAsync(storyId, fileName);
}

/// <summary>
/// Adds or edits a story in three steps (Slice 78): Pages (a grid you reorder, rotate, crop and add to), Cover, Text. OCR runs when
/// the Pages step is confirmed, only on pages that changed. Replaces the add and edit screens.
/// </summary>
public partial class StoryWizardPage : ContentPage, IGrownUpPage
{
	private const int PagesStepIndex = WizardSteps.Pages;
	private const int CoverStepIndex = WizardSteps.Cover;
	private const int TextStepIndex = WizardSteps.Text;
	private const double PillRadius = 14;

	private readonly IStoryLibrary _library;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();
	private readonly List<(Border Pill, Label Label)> _pills = [];
	private WizardSession? _session;
	private int _step;
	private int _shownVersion = -1;
	private readonly WaitController _wait;

	public StoryWizardPage(IStoryLibrary library, IServiceProvider services)
	{
		InitializeComponent();
		WaitOverlay overlay = DinoWait.OverPage();
		Grid.SetRowSpan(overlay, WizardRoot.RowDefinitions.Count);
		WizardRoot.Children.Add(overlay);
		_wait = new WaitController(overlay);
		_wait.ActiveChanged += active => PagesView.InputTransparent = active; // from Begin, not from when the overlay shows
		_library = library;
		_services = services;
		BuildStepBar();
	}

	/// <summary>Opens the wizard on a new story (<paramref name="existing"/> null) or on an existing one. Called once, right after the page is created.</summary>
	public async Task LoadAsync(Story? existing)
	{
		bool isNew = existing is null;
		Story story = existing ?? await NewStoryAsync();
		var draft = new StoryDraft(story);
		_session = new WizardSession(draft, isNew, _library, _services, this);
		_session.AttachWait(_wait);
		if (!isNew)
		{
			draft.LoadPageDrafts(ExistingPicture(story));
			for (int i = 0; i < draft.PageDrafts.Count; i++)
			{
				if (PictureTextBook.IsPictureText(story.Pages[i]))
				{
					_session.PictureTextPages.Add(draft.PageDrafts[i].Id);
				}
			}

			_session.LanguageChosen = true;
		}

		TitleLabel.Text = isNew ? AppResources.Story_Add : AppResources.Manage_EditStory;
		PagesView.Attach(_session, OnPagesEdited);
		CoverView.Attach(_session);
		TextView.Attach(_session);
		ShowStep(PagesStepIndex);
	}

	private async Task<Story> NewStoryAsync()
	{
		int existing = (await _library.ListAsync()).Count;
		string language = StoryTranslator.MyLanguage;
		return Story.NewOwnBook(StoryIds.New(), language, DateTimeOffset.UtcNow, existing);
	}

	private Func<string?, string?> ExistingPicture(Story story) => name =>
	{
		string? path = _library.TryResolveFile(story.Id, name);
		return path is not null && File.Exists(path) ? path : null;
	};

	private void BuildStepBar()
	{
		string[] names = [AppResources.Edit_Pages, AppResources.Edit_Cover, AppResources.Wizard_StepText];
		for (int i = 0; i < names.Length; i++)
		{
			var label = new Label
			{
				Text = $"{i + 1} · {names[i]}", FontSize = 12.5, FontAttributes = FontAttributes.Bold,
				HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center,
			};
			var pill = new Border
			{
				Padding = new Thickness(6, 7), StrokeThickness = 0, Content = label,
				StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = PillRadius },
			};
			StepBar.Add(pill, i);
			_pills.Add((pill, label));
		}
	}

	private void ShowStep(int step)
	{
		_step = step;
		PagesView.IsVisible = step == PagesStepIndex;
		CoverView.IsVisible = step == CoverStepIndex;
		TextView.IsVisible = step == TextStepIndex;
		BackButton.IsVisible = WizardSteps.ShowsBack(step);
		Grid.SetColumn(NextButton, WizardSteps.ShowsBack(step) ? 1 : 0);
		Grid.SetColumnSpan(NextButton, WizardSteps.NextColumnSpan(step));
		NextButton.Text = NextLabel(WizardSteps.NextActionFor(step));
		RenderHeader();
		for (int i = 0; i < _pills.Count; i++)
		{
			bool current = i == step;
			_pills[i].Pill.BackgroundColor = current ? DinoTokens.Get(DinoTokens.BrandTint) : Colors.Transparent;
			_pills[i].Label.TextColor = current ? DinoTokens.Get(DinoTokens.Brand) : DinoTokens.Get(DinoTokens.Muted);
		}

		RefreshStep();
	}

	private void RefreshStep()
	{
		_shownVersion = _session!.Draft.Version;
		switch (_step)
		{
			case PagesStepIndex:
				PagesView.Refresh();
				break;
			case CoverStepIndex:
				CoverView.Show();
				break;
			default:
				TextView.Show();
				break;
		}

		RefreshNextState();
	}

	private void OnPagesEdited() => RefreshNextState();

	// Slice 98 (mock screens 31, 32): the Pages step is titled "Pages" with the story's name and page count under it.
	private void RenderHeader()
	{
		if (_session is null)
		{
			return;
		}

		StoryDraft? draft = _session.Draft;
		bool pages = _step == PagesStepIndex;
		TitleLabel.Text = pages ? AppResources.Edit_Pages : _session.IsNew ? AppResources.Story_Add : AppResources.Manage_EditStory;
		bool named = pages && draft is { PageDrafts.Count: > 0 } && !string.IsNullOrWhiteSpace(_session!.FileName);
		SubtitleLabel.IsVisible = named;
		SubtitleLabel.Text = named ? SubtitleOf(_session!.FileName!, draft!.PageDrafts.Count) : string.Empty;
	}

	private static string SubtitleOf(string fileName, int pages) =>
		AppResources.Format(pages == 1 ? AppResources.Pictures_Subtitle_One : AppResources.Pictures_Subtitle_Other, fileName, pages);

	private static string NextLabel(NextAction action) => action switch
	{
		NextAction.Save => AppResources.Add_Save,
		NextAction.GoToCover => AppResources.Pictures_NextCover,
		_ => AppResources.Wizard_Next,
	};

	private void RefreshNextState()
	{
		RenderHeader();
		NextButton.IsEnabled = WizardSteps.NextEnabled(_step, _session?.Draft.PageDrafts.Count ?? 0);
		NextButton.Opacity = NextButton.IsEnabled ? 1 : 0.45;
	}

	// Coming back from the page editor, the text editor or the cast page: what they changed must show. Nothing is rebuilt when the
	// pages are as they were (the scroll position and the cast analysis stay); the cast page only changes the cast.
	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (_session is null)
		{
			return;
		}

		if (_session.Draft.Version != _shownVersion)
		{
			RefreshStep();
		}
		else if (_step == TextStepIndex)
		{
			TextView.ShowCurrentCast();
		}
	}

	private async void OnNextClicked(object? sender, EventArgs e) => await _busy.RunAsync(NextAsync);

	private async Task NextAsync()
	{
		switch (_step)
		{
			case PagesStepIndex:
				await ConfirmPagesAsync();
				break;
			case CoverStepIndex:
				ShowStep(TextStepIndex);
				break;
			default:
				await SaveAsync();
				break;
		}
	}

	private void OnBackClicked(object? sender, EventArgs e)
	{
		if (_step > PagesStepIndex)
		{
			ShowStep(_step - 1);
		}
	}

	private async void OnLeaveTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(LeaveAsync);

	private async Task LeaveAsync()
	{
		_session?.Cleanup();
		await Navigation.PopAsync();
	}

	// The page is gone for good (popped, system back), not just covered by the editor or the cast page: its pictures go too.
	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		if (!Navigation.NavigationStack.Contains(this))
		{
			_session?.Cleanup();
		}
	}

	// OCR is run here, after crop and rotate (which change what it reads), and only for the pages that changed.
	private async Task ConfirmPagesAsync()
	{
		WizardSession session = _session!;
		StoryDraft draft = session.Draft;
		if (draft.PageDrafts.Count == 0)
		{
			return;
		}

		ReadResult? read = await ReadPagesWithProgressAsync(session);
		if (read is null)
		{
			return;
		}

		// A page rotated, cropped or deleted while it was being read keeps its newer state; the next Next reads it again.
		int stale = read.Pages.Count(page => !draft.ReplaceIfSamePicture(page));
		if (stale > 0)
		{
			RefreshStep();
			return;
		}

		ShowStep(CoverStepIndex);
		if (read.Failed > 0)
		{
			await DisplayAlertAsync(AppResources.Wizard_ReadFailedTitle,
				AppResources.Format(AppResources.Wizard_ReadFailedBody, read.Failed), AppResources.Common_Ok);
		}
	}

	// A picture + text page keeps its text and no line boxes: an empty reading stands in for the OCR a rotate or crop would ask for.
	private static PageDraft KeepPictureText(WizardSession session, PageDraft page) =>
		page is { HasPicture: true, Ocr: null } && session.PictureTextPages.Contains(page.Id) && !string.IsNullOrWhiteSpace(page.Text)
			? page.WithOcr(new PageReading([]))
			: page;

	private async Task<ReadResult?> ReadPagesWithProgressAsync(WizardSession session)
	{
		var pages = new List<PageDraft>();
		CancellationToken cancel = session.BeginWait(WaitStage.ReadingPictures, AppResources.Wait_ReadingPictures);
		try
		{
			foreach (PageDraft page in session.Draft.PageDrafts)
			{
				cancel.ThrowIfCancellationRequested();
				pages.Add(KeepPictureText(session, await session.RenderAsync(page)));
			}

			var ocr = _services.GetRequiredService<OcrPositionedTextExtractor>();
			var progress = new Progress<ReadProgress>(p => session.ReportWait(p.Current, p.Total));
			return await StoryImport.ReadPagesAsync(ocr, pages, progress, cancel);
		}
		catch (OperationCanceledException)
		{
			return null; // Cancel in the overlay: nothing is kept, the Pages step stays as it was
		}
		catch (Exception ex)
		{
			DinoLog.Error(_services, "reading the pages failed", ex);
			await DisplayAlertAsync(AppResources.Add_ReadFailedTitle, AppResources.Add_ReadFailedBody, AppResources.Common_Ok);
			return null;
		}
		finally
		{
			session.EndWait();
		}
	}

	private async Task SaveAsync()
	{
		if (await TextView.SaveAsync())
		{
			_session!.Cleanup();
			await Navigation.PopAsync();
		}
	}
}
