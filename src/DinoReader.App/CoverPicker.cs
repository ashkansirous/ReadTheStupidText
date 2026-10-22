using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>
/// The cover chooser (Slice 77): a strip of the story's page pictures, a "+" tile for a gallery photo and the
/// flat colours. It edits a <see cref="StoryDraft"/> and calls <c>changed</c> after every pick; nothing is saved
/// until the host saves. Reusable, so the add wizard can host it too.
/// </summary>
public sealed class CoverPicker : ContentView
{
	private const double TileWidth = 54;
	private const double TileHeight = 72;
	private const double TileRadius = 10;
	private const double SelectedStroke = 3;
	private const double GlyphSize = 24;
	private const double CaptionSize = 11;
	private const string AddGlyph = "+";

	private readonly StoryDraft _draft;
	private readonly IStoryLibrary _library;
	private readonly ExternalActivityGuard _external;
	private readonly Action _changed;
	private readonly BusyGuard _busy = new();
	private readonly List<(Border Tile, Func<bool> IsSelected)> _tiles = [];
	private List<string?> _builtFor = [];
	private Border? _photoTile;
	private readonly HorizontalStackLayout _strip = new() { Spacing = 8, Padding = new Thickness(0, 4) };

	public CoverPicker(StoryDraft draft, IStoryLibrary library, ExternalActivityGuard external, Action changed)
	{
		_draft = draft;
		_library = library;
		_external = external;
		_changed = changed;
		Content = new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = _strip };
		Rebuild();
	}

	/// <summary>Brings the picker up to date with the draft. The page pictures are decoded once: while the pages are
	/// the same only the selection outlines (and the photo tile) change; a changed page list builds the strip again.</summary>
	public void Refresh()
	{
		if (_builtFor.SequenceEqual(PageFiles()))
		{
			UpdateSelection();
			return;
		}

		Rebuild();
	}

	private List<string?> PageFiles() => _draft.Pages.Select(p => (string?)p.ImageFile).ToList();

	private void Rebuild()
	{
		_builtFor = PageFiles();
		_tiles.Clear();
		_strip.Children.Clear();
		for (int i = 0; i < _draft.Pages.Count; i++)
		{
			int page = i;
			if (PageTile(page) is { } tile)
			{
				Add(tile, () => IsPageSelected(page));
			}
		}

		_photoTile = PhotoTile();
		Add(_photoTile, IsPhotoSelected);
		for (int i = 0; i < CoverPalette.Colours.Count; i++)
		{
			int colour = i;
			Add(ColourTile(colour), () => IsColourSelected(colour));
		}
	}

	private void Add(Border tile, Func<bool> isSelected)
	{
		_tiles.Add((tile, isSelected));
		_strip.Children.Add(tile);
		SetSelected(tile, isSelected());
	}

	// The photo tile shows the chosen photo, so it is the one tile whose face changes with a pick.
	private void UpdateSelection()
	{
		foreach ((Border tile, Func<bool> isSelected) in _tiles)
		{
			SetSelected(tile, isSelected());
		}

		if (_photoTile is not null)
		{
			_photoTile.Content = PhotoFace(IsPhotoSelected());
		}
	}

	private bool IsPageSelected(int index) => _draft.CoverStyle == CoverStyle.Picture && _draft.CoverPageIndex == index;

	private bool IsPhotoSelected() =>
		_draft.CoverStyle == CoverStyle.Picture && _draft.CoverPageIndex is null && (_draft.CoverFile is not null || _draft.PendingCoverPhoto is not null);

	private bool IsColourSelected(int index) => _draft.CoverStyle == CoverStyle.Colour && _draft.CoverColourIndex % CoverPalette.Colours.Count == index;

	private Border? PageTile(int index)
	{
		string? path = _library.TryResolveFile(_draft.Original.Id, _draft.Pages[index].ImageFile);
		if (path is null)
		{
			return null;
		}

		return Tile(new Image { Source = path, Aspect = Aspect.AspectFill }, () => PickPage(index));
	}

	private Border PhotoTile()
	{
		Border tile = Tile(PhotoFace(IsPhotoSelected()), OnPhotoTapped);
		tile.BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint);
		return tile;
	}

	private View PhotoFace(bool selected)
	{
		var plus = new VerticalStackLayout
		{
			VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new Label
				{
					Text = AddGlyph, FontSize = GlyphSize, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Brand),
					HorizontalOptions = LayoutOptions.Center,
				},
				new Label
				{
					Text = AppResources.Edit_CoverPhoto, FontSize = CaptionSize, TextColor = DinoTokens.Get(DinoTokens.Brand),
					HorizontalOptions = LayoutOptions.Center,
				},
			},
		};
		string? chosen = selected ? ChosenPhotoPath() : null;
		return chosen is null ? plus : new Image { Source = chosen, Aspect = Aspect.AspectFill };
	}

	private string? ChosenPhotoPath() =>
		_draft.PendingCoverPhoto ?? _library.TryResolveFile(_draft.Original.Id, _draft.CoverFile);

	private Border ColourTile(int index)
	{
		var fill = new BoxView { Color = Color.FromArgb(CoverPalette.Colours[index]) };
		return Tile(fill, () => PickColour(index));
	}

	private static Border Tile(View content, Action tapped)
	{
		var tile = new Border
		{
			WidthRequest = TileWidth, HeightRequest = TileHeight, Padding = 0, Content = content,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = TileRadius },
		};
		tile.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(tapped) });
		return tile;
	}

	private static void SetSelected(Border tile, bool selected)
	{
		tile.Stroke = selected ? DinoTokens.Get(DinoTokens.Brand) : Colors.Transparent;
		tile.StrokeThickness = selected ? SelectedStroke : 0;
	}

	private void PickPage(int index)
	{
		PickedPhotoCache.Delete(_draft.PendingCoverPhoto);
		_draft.CoverStyle = CoverStyle.Picture;
		_draft.CoverPageIndex = index;
		_draft.CoverFile = null;
		_draft.PendingCoverPhoto = null;
		Picked();
	}

	private void PickColour(int index)
	{
		PickedPhotoCache.Delete(_draft.PendingCoverPhoto);
		_draft.ChooseCover(CoverStyle.Colour);
		_draft.CoverColourIndex = index;
		Picked();
	}

	private async void OnPhotoTapped() => await _busy.RunAsync(PickPhotoAsync);

	private async Task PickPhotoAsync()
	{
		try
		{
			FileResult? photo = await _external.RunAsync(() => MediaPicker.Default.PickPhotoAsync());
			if (photo is not null)
			{
				RememberCoverPhoto(photo.FullPath);
			}
		}
		catch (Exception ex) when (ex is PermissionException or FeatureNotSupportedException)
		{
			await ShowPermissionAlertAsync();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader cover photo pick failed: {ex}");
		}
	}

	private Task ShowPermissionAlertAsync() =>
		Window?.Page?.DisplayAlertAsync(AppResources.Add_PermissionTitle, AppResources.Add_PermissionBody, AppResources.Common_Ok) ?? Task.CompletedTask;

	// The photo is only remembered here; Save copies it into the story folder (so a cancelled edit leaves nothing behind).
	private void RememberCoverPhoto(string source)
	{
		PickedPhotoCache.Delete(_draft.PendingCoverPhoto);
		_draft.CoverStyle = CoverStyle.Picture;
		_draft.PendingCoverPhoto = source;
		_draft.CoverFile = null;
		_draft.CoverPageIndex = null;
		Picked();
	}

	private void Picked()
	{
		UpdateSelection();
		_changed();
	}
}
