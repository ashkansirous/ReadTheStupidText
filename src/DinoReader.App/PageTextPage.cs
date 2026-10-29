using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>Edits one page's text (or writes a new page when the index is -1) inside the shared <see cref="StoryDraft"/>.</summary>
public sealed class PageTextPage : ContentPage, IGrownUpPage
{
	/// <summary>The index that means "write a new page".</summary>
	public const int NewPage = -1;

	private readonly StoryDraft _draft;
	private readonly BusyGuard _busy = new();
	private readonly int _index;
	private readonly Editor _editor;
	private readonly RegionOverlay? _overlay;

	// The page as this screen edits it: region choices live here until Save, so Back drops them with the typed text.
	private PageDraft? _page;
	private readonly List<int> _toggled = [];

	public PageTextPage(StoryDraft draft, int index)
	{
		_draft = draft;
		_index = index;
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container | SafeAreaRegions.SoftInput);
		BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		_editor = new Editor
		{
			Text = index == NewPage ? string.Empty : draft.PageDrafts[index].Text,
			FontFamily = DinoFonts.Reading,
			FontSize = 18,
			AutoSize = EditorAutoSizeOption.Disabled,
			BackgroundColor = Colors.White,
		};
		var body = new Border { Content = _editor, Padding = 12, Margin = new Thickness(14, 0, 14, 14), BackgroundColor = Colors.White }.Row(2);
		_page = index == NewPage ? null : draft.PageDrafts[index];
		_overlay = OverlayFor(index);
		Content = new Grid
		{
			RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
			Children = { Header(), PicturePart().Row(1), body },
		};
	}

	// The page picture with its regions outlined (Slice 79), for a scanned page with regions worth choosing between.
	private RegionOverlay? OverlayFor(int index)
	{
		if (_page is null || !RegionPicker.Offers(_page) || _page.PictureFile is not { } file)
		{
			return null;
		}

		return new RegionOverlay(file, _page.Ocr!.Regions!, region => _ = _busy.RunAsync(() => ToggleRegionAsync(region)));
	}

	private View PicturePart()
	{
		if (_overlay is null)
		{
			return new BoxView { HeightRequest = 0, Color = Colors.Transparent };
		}

		return new VerticalStackLayout { Spacing = 6, Margin = new Thickness(14, 0, 14, 10), Children = { _overlay, RegionPicker.Helper() } };
	}

	// The text is composed from the regions again, so what is typed in the editor is asked about first (Keep changes nothing).
	// Nothing reaches the draft before Save, so Back cancels the toggle along with the typing.
	private async Task ToggleRegionAsync(int region)
	{
		if (!await RegionPicker.MayReplaceAsync(this, _page!.WouldReplace(_editor.Text ?? string.Empty)))
		{
			return;
		}

		_page = _page.WithRegionToggled(region);
		_toggled.Add(region);
		_editor.Text = _page.Text;
		_overlay!.Show(_page.Ocr!.Regions!);
	}

	private Grid Header()
	{
		var back = new Border
		{
			WidthRequest = 44, HeightRequest = 44, StrokeThickness = 0, BackgroundColor = Colors.Transparent,
			Content = new IconView { Kind = IconKind.Back, Color = DinoTokens.Get(DinoTokens.Ink), Size = 22, StrokeWidth = 2.6 },
		};
		SemanticProperties.SetDescription(back, AppResources.Common_Back);
		var backTap = new TapGestureRecognizer();
		backTap.Tapped += async (_, _) => await _busy.RunAsync(() => Navigation.PopAsync());
		back.GestureRecognizers.Add(backTap);
		var save = new Button
		{
			Text = AppResources.Common_Save, HeightRequest = 38, CornerRadius = 19, Padding = new Thickness(14, 0), FontSize = 13.5,
			BackgroundColor = DinoTokens.Get(DinoTokens.Brand), TextColor = Colors.White,
		};
		save.Clicked += async (_, _) => await _busy.RunAsync(SaveAsync);
		return new Grid
		{
			Padding = new Thickness(8, 10, 16, 10),
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
			Children = { back, save.Column(2) },
		};
	}

	private async Task SaveAsync()
	{
		string text = LineBreaks.ToLf(_editor.Text ?? string.Empty);
		Store(text);
		await Navigation.PopAsync();
	}

	// The page as the draft holds it now (a reading or re-analysis may have landed while this screen was open) with only this
	// screen's region toggles applied; the toggles are dropped when the draft's regions are not the ones they were made on.
	private PageDraft CurrentPage()
	{
		PageDraft current = _draft.PageDrafts[_index];
		bool sameRegions = current.Ocr?.Regions?.Count == _page!.Ocr?.Regions?.Count && current.SamePictureAs(_page);
		return sameRegions ? _toggled.Aggregate(current, (page, region) => page.WithRegionToggled(region)) : current;
	}

	// A new page needs some text. A picture page may be emptied (it stays a picture-only page); a text page emptied has nothing
	// left to show, so it goes.
	private void Store(string text)
	{
		bool blank = string.IsNullOrWhiteSpace(text);
		if (_index == NewPage)
		{
			if (!blank)
			{
				_draft.AddPageDrafts(PictureTextBook.ImportPages(text).Select(part => PageDraft.ForText(part)));
			}

			return;
		}

		PageDraft page = CurrentPage();
		if (blank && !page.HasPicture)
		{
			_draft.RemovePageDraft(_index);
			return;
		}

		_draft.ReplacePageDraft(page.WithText(text));
	}
}

/// <summary>Layout shorthands for the code-built pages.</summary>
internal static class GridPlacement
{
	public static T Row<T>(this T view, int row) where T : BindableObject
	{
		Grid.SetRow(view, row);
		return view;
	}

	public static T Column<T>(this T view, int column) where T : BindableObject
	{
		Grid.SetColumn(view, column);
		return view;
	}
}
