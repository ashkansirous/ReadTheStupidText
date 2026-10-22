using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DinoReader;

/// <summary>The "Add a story" tile's place in the manage grid's item list.</summary>
public sealed class AddStoryEntry
{
	public static readonly AddStoryEntry Instance = new();

	private AddStoryEntry()
	{
	}
}

/// <summary>Picks the add tile's template for <see cref="AddStoryEntry"/> and the story template for every story.</summary>
public sealed class ManageTemplateSelector : DataTemplateSelector
{
	public DataTemplate? AddTemplate { get; set; }

	public DataTemplate? StoryTemplate { get; set; }

	protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
		(item is AddStoryEntry ? AddTemplate : StoryTemplate)!;
}

public partial class ManagePage : ContentPage, IGrownUpPage
{
	private const double TileAspect = 4.0 / 3.0;

	private readonly IStoryLibrary _library;
	private readonly StoryActions _actions;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();

	public ManagePage(IStoryLibrary library, StoryActions actions, IServiceProvider services)
	{
		InitializeComponent();
		_library = library;
		_actions = actions;
		_services = services;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await RefreshAsync();
	}

	private async Task RefreshAsync()
	{
		IReadOnlyList<Story> stories = await _library.ListAsync();
		var items = new List<object> { AddStoryEntry.Instance };
		items.AddRange(stories.Select(s => StoryTile.From(s, _library)));
		StoryList.ItemsSource = items;
	}

	private static StoryTile? TileOf(object? sender) => (sender as BindableObject)?.BindingContext as StoryTile;

	private void OnAddTileSizeChanged(object? sender, EventArgs e)
	{
		var tile = (VisualElement)sender!;
		double height = tile.Width * TileAspect;
		if (tile.Width > 0 && Math.Abs(tile.HeightRequest - height) > 0.5)
		{
			tile.HeightRequest = height;
		}
	}

	private async void OnAddTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(() => OpenWizardAsync(null));

	private async void OnEditTapped(object? sender, TappedEventArgs e)
	{
		if (TileOf(sender) is { } tile)
		{
			await _busy.RunAsync(() => OpenWizardAsync(tile.Story));
		}
	}

	// One wizard adds a story (null) or edits one (Slice 78).
	private async Task OpenWizardAsync(Story? story)
	{
		var wizard = _services.GetRequiredService<StoryWizardPage>();
		await wizard.LoadAsync(story);
		await Navigation.PushAsync(wizard);
	}

	private async void OnCastTapped(object? sender, TappedEventArgs e)
	{
		if (TileOf(sender) is { } tile)
		{
			await _busy.RunAsync(() => OpenCastAsync(tile.Story));
		}
	}

	private async Task OpenCastAsync(Story story)
	{
		var cast = _services.GetRequiredService<CastPage>();
		cast.Load(story, _actions.SaveCastAsync);
		await Navigation.PushAsync(cast);
	}

	private async void OnDeleteTapped(object? sender, TappedEventArgs e)
	{
		if (TileOf(sender) is { } tile)
		{
			await _busy.RunAsync(() => DeleteAsync(tile.Story));
		}
	}

	private async Task DeleteAsync(Story story)
	{
		// Refresh even after a failed delete: a half-deleted story no longer lists.
		await _actions.ConfirmDeleteAsync(this, story);
		await RefreshAsync();
	}

	private async void OnDoneClicked(object? sender, EventArgs e) => await _busy.RunAsync(() => Navigation.PopAsync());
}
