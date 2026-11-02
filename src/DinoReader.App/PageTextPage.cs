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

	// The page as this screen edits it: region choices live here until Done saves them with (Back leaves without saving, as before) the typed text.
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
		_header = Header();
		_picture = PicturePart();
		_body = body;
		_root.Children.Add(_header);
		_root.Children.Add(_picture);
		_root.Children.Add(_body);
		Content = _root;
		_editor.Focused += (_, _) => SetEditorFocused(true);
		_editor.Unfocused += (_, _) => SetEditorFocused(false);
		ApplyShape(EditorLayout.For(0, 0, _overlay is not null, editorFocused: false));
	}

	private readonly Grid _root = new();
	private readonly GrownUpHeader _header;
	private readonly View _picture;
	private readonly View _body;
	private Label? _helper;
	private Size _windowSize;
	private EditorShape _shape;
	private bool _editorFocused;

	// Slice 232 part C (mock Batch 44 frame 120): held sideways the picture is a left column, and a thumbnail on a phone while the text editor has focus (EditorLayout.For). The same editor, header
	// and picture views are moved between the layouts, never rebuilt, so the text typed so far and the region choices survive a rotation; the editor itself never changes parent, so a focus
	// change that swaps Left and Thumbnail only resizes the picture column.
	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		if (width <= 0 || height <= 0 || (width == _windowSize.Width && height == _windowSize.Height))
		{
			return;
		}

		_windowSize = new Size(width, height);
		Relayout();
	}

	private void SetEditorFocused(bool focused)
	{
		if (_editorFocused != focused)
		{
			_editorFocused = focused;
			Relayout();
		}
	}

	private void Relayout()
	{
		double width = _windowSize.Width;
		double height = _windowSize.Height;
		if (width <= 0 || height <= 0)
		{
			return;
		}

		EditorShape shape = EditorLayout.For(width, height, _overlay is not null, _editorFocused);
		// A page with no picture has no left column to keep the camera cut-out clear, so held sideways the whole page takes the start inset instead.
		_root.Padding = shape.Picture == EditorPicture.None && LandscapeLayout.IsLandscape(width, height) ? new Thickness(SideInset, 0, 0, 0) : new Thickness(0);
		if (shape != _shape)
		{
			ApplyShape(shape);
#if DEBUG
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] editor picture={shape.Picture} chars={_editor.Text?.Length ?? 0}");
#endif
		}
	}

	private void ApplyShape(EditorShape shape)
	{
		_shape = shape;
		bool side = shape.Picture is EditorPicture.Left or EditorPicture.Thumbnail;
		_helper?.SetValue(IsVisibleProperty, shape.Picture != EditorPicture.Thumbnail);
		_picture.IsVisible = shape.Picture != EditorPicture.None;
		// The root's children are added once, in the constructor; a shape change only sets definitions and attached properties, so the editor's native view is never detached.
		if (side)
		{
			ApplySideBySide(shape);
			return;
		}

		ApplyStacked();
	}

	private void ApplyStacked()
	{
		_root.ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(0)];
		_root.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)];
		_picture.WidthRequest = -1;
		_picture.Margin = _pictureMargin;
		Place(_header, row: 0, column: 0, rowSpan: 1);
		Place(_picture, row: 1, column: 0, rowSpan: 1);
		Place(_body, row: 2, column: 0, rowSpan: 1);
	}

	private void ApplySideBySide(EditorShape shape)
	{
		_root.ColumnDefinitions = [new ColumnDefinition(shape.PictureWidth + SideInset), new ColumnDefinition(GridLength.Star)];
		_root.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Star)];
		_picture.WidthRequest = shape.PictureWidth;
		_picture.Margin = new Thickness(SideInset, 0, 0, PictureBottomMargin);
		Place(_picture, row: 0, column: 0, rowSpan: 3);
		Place(_header, row: 0, column: 1, rowSpan: 1);
		Place(_body, row: 1, column: 1, rowSpan: 2);
	}

	private static void Place(View view, int row, int column, int rowSpan)
	{
		Grid.SetRow(view, row);
		Grid.SetColumn(view, column);
		Grid.SetRowSpan(view, rowSpan);
	}

	private const double SideInset = LandscapeLayout.SidewaysInset;
	private const double PictureBottomMargin = 10;
	private Thickness _pictureMargin;

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

		_helper = RegionPicker.Helper();
		_pictureMargin = new Thickness(14, 0, 14, PictureBottomMargin);
		// The one picture view, created once: it scrolls when the picture and helper are taller than the column.
		return new ScrollView { VerticalOptions = LayoutOptions.Start, Content = new VerticalStackLayout { Spacing = 6, Children = { _overlay, _helper } } };
	}

	// The text is composed from the regions again, so what is typed in the editor is asked about first (Keep changes nothing).
	// Nothing reaches the draft before Done saves.
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

	// Back (header, system, iOS swipe) leaves without saving, as before Slice 229; only Done saves (the old Save button).
	private GrownUpHeader Header()
	{
		var header = new GrownUpHeader();
		header.BackRequested += async (_, _) => await _busy.RunAsync(() => Navigation.PopAsync());
		header.DoneRequested += async (_, _) => await _busy.RunAsync(SaveAsync);
		return header;
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
