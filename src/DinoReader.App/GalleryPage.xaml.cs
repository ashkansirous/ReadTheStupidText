using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DinoReader;

public partial class GalleryPage : ContentPage
{
	private const uint TileFadeMs = 180;
	private const double TileShrinkTo = 0.85;
	private const string TileTitleHeightKey = "TileTitleHeight";

	private readonly IStoryLibrary _library;
	private readonly GrownUpGate _gate;
	private readonly GrownUpMode _mode;
	private readonly StoryActions _actions;
	private readonly StarterStories _starters;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();
	private readonly ObservableCollection<object> _items = [];

	public GalleryPage(IStoryLibrary library, GrownUpGate gate, GrownUpMode mode, StoryActions actions, StarterStories starters, IServiceProvider services)
	{
		InitializeComponent();
		_library = library;
		_gate = gate;
		_mode = mode;
		_actions = actions;
		_starters = starters;
		_services = services;
		StoryList.ItemsSource = _items;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		_mode.PropertyChanged -= OnModeChanged;
		_mode.PropertyChanged += OnModeChanged;
		StoreClient store = _services.GetRequiredService<StoreClient>();
		store.LibraryChanged -= OnLibraryChanged;
		store.LibraryChanged += OnLibraryChanged;
		ApplyMode();
		try
		{
			// The starters are on the shelf before the first list is read (Slice 91); a later call is a no-op.
			await _starters.InstallOnceAsync();
			await RefreshAsync();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader gallery refresh failed: {ex}");
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_mode.PropertyChanged -= OnModeChanged;
		_services.GetRequiredService<StoreClient>().LibraryChanged -= OnLibraryChanged;
	}

	// A store download can finish on a background thread while the gallery shows; reload the list on the UI thread (Slice 92).
	private void OnLibraryChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(async () =>
	{
		try
		{
			await RefreshAsync();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader gallery refresh failed: {ex}");
		}
	});

	// The tiles follow the mode through their binding; only the Add entry and the chrome change here.
	private void OnModeChanged(object? sender, PropertyChangedEventArgs e) => MainThread.BeginInvokeOnMainThread(ApplyMode);

	private void ApplyMode()
	{
		bool grownUp = _mode.IsGrownUp;
		bool hasAdd = _items.Count > 0 && _items[0] is AddStoryEntry;
		if (grownUp && !hasAdd)
		{
			_items.Insert(0, AddStoryEntry.Instance);
		}
		else if (!grownUp && hasAdd)
		{
			_items.RemoveAt(0);
		}

		ApplyModeChrome(grownUp);
	}

	private async Task RefreshAsync()
	{
		IReadOnlyList<Story> stories = await _library.ListAsync();
		_items.Clear();
		foreach (object item in GalleryItems.Build(stories, _library, _mode))
		{
			_items.Add(item);
		}

		ApplyModeChrome(_mode.IsGrownUp);
	}

	private void ApplyModeChrome(bool grownUp)
	{
		LockButton.IsVisible = !grownUp;
		DoneButton.IsVisible = grownUp;
		GrownUpActions.IsVisible = grownUp;
		BackgroundColor = DinoTokens.Get(grownUp ? DinoTokens.Sand : DinoTokens.Cream);
	}

	private static StoryTile? TileOf(object? sender) => (sender as BindableObject)?.BindingContext as StoryTile;

	private async void OnStoryTapped(object? sender, TappedEventArgs e)
	{
		if (TileOf(sender) is { } tile)
		{
			await _busy.RunAsync(() => _mode.IsGrownUp ? _actions.EditAsync(Navigation, tile.Story) : _actions.ReadAsync(Navigation, tile.Story));
		}
	}

	private void OnTitleProbeSizeChanged(object? sender, EventArgs e)
	{
		if (TitleProbe.Height > 0 && Math.Abs(TitleProbe.Height - (double)Resources[TileTitleHeightKey]) > 0.5)
		{
			Resources[TileTitleHeightKey] = Math.Ceiling(TitleProbe.Height);
		}
	}

	// Every grown-up action re-checks the mode right before it runs: a queued tap must not outlive the mode.
	private Task RunGrownUpAsync(Func<Task> work) => _busy.RunAsync(() => _mode.IsGrownUp ? work() : Task.CompletedTask);

	// Free stories (Slice 92): a grown-up page, so it is only reachable in grown-up mode and closes with it.
	private async void OnStoreTapped(object? sender, TappedEventArgs e) => await RunGrownUpAsync(OpenStoreAsync);

	private async Task OpenStoreAsync()
	{
		var store = _services.GetRequiredService<StorePage>();
		if (_mode.IsGrownUp)
		{
			await Navigation.PushAsync(store);
		}
	}

	private async void OnAddTapped(object? sender, TappedEventArgs e) => await RunGrownUpAsync(() => _actions.EditAsync(Navigation, null));

	private async void OnEditRequested(object? sender, EventArgs e)
	{
		if (TileOf(sender) is { } tile)
		{
			await RunGrownUpAsync(() => _actions.EditAsync(Navigation, tile.Story));
		}
	}

	private async void OnCastRequested(object? sender, EventArgs e)
	{
		if (TileOf(sender) is { } tile)
		{
			await RunGrownUpAsync(() => _actions.CastAsync(Navigation, tile.Story));
		}
	}

	private async void OnExportRequested(object? sender, EventArgs e)
	{
		if (TileOf(sender) is { } tile)
		{
			await RunGrownUpAsync(() => _actions.ExportAsync(tile.Story));
		}
	}

	// Seven taps on the title in grown-up mode flip the developer switch (Slice 91); the tiles are rebuilt to show or hide Export.
	private async void OnTitleTapped(object? sender, TappedEventArgs e)
	{
		if (!_mode.IsGrownUp)
		{
			DeveloperMode.ResetTaps();
			return;
		}

		try
		{
			if (DeveloperMode.RegisterTap())
			{
				await RefreshAsync();
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader gallery refresh failed: {ex}");
		}
	}

	private async void OnDeleteRequested(object? sender, EventArgs e)
	{
		if (TileOf(sender) is { } tile && sender is Element source)
		{
			await RunGrownUpAsync(() => DeleteAsync(tile, TileRootOf(source)));
		}
	}

	private async Task DeleteAsync(StoryTile tile, VisualElement? root)
	{
		DeleteOutcome outcome = await _actions.ConfirmDeleteAsync(this, tile.Story);
		if (outcome == DeleteOutcome.Deleted)
		{
			await AnimateOutAsync(root);
			if (!_items.Remove(tile))
			{
				// A refresh replaced the list during the fade: re-read so no deleted story lingers.
				await RefreshAsync();
			}
		}
		else if (outcome == DeleteOutcome.Failed)
		{
			// A half-deleted story no longer lists, so re-read after a failure.
			await RefreshAsync();
		}
	}

	// The tile's root is the story template's VerticalStackLayout; a recycled view must come back opaque.
	private static VisualElement? TileRootOf(Element source)
	{
		for (Element? e = source; e is not null; e = e.Parent)
		{
			if (e is VerticalStackLayout root)
			{
				return root;
			}
		}

		return null;
	}

	private static async Task AnimateOutAsync(VisualElement? root)
	{
		if (root is null)
		{
			return;
		}

		await Task.WhenAll(root.FadeToAsync(0, TileFadeMs), root.ScaleToAsync(TileShrinkTo, TileFadeMs));
		root.Opacity = 1;
		root.Scale = 1;
	}

	private async void OnLockTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(EnterGrownUpAsync);

	private async Task EnterGrownUpAsync()
	{
		if (await _gate.RequestAsync(Navigation))
		{
			_mode.Enter();
		}
	}

	private void OnDoneClicked(object? sender, EventArgs e) => _mode.Leave();
}
