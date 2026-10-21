using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DinoReader;

public partial class GalleryPage : ContentPage
{
	private readonly IStoryLibrary _library;
	private readonly GrownUpGate _gate;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();

	public GalleryPage(IStoryLibrary library, GrownUpGate gate, IServiceProvider services)
	{
		InitializeComponent();
		_library = library;
		_gate = gate;
		_services = services;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		IReadOnlyList<Story> stories = await _library.ListAsync();
		StoryList.ItemsSource = stories.Select(s => StoryTile.From(s, _library)).ToList();
	}

	private async void OnStoryTapped(object? sender, TappedEventArgs e)
	{
		if (((BindableObject)sender!).BindingContext is StoryTile tile)
		{
			await _busy.RunAsync(() => OpenStoryAsync(tile.Story));
		}
	}

	private async Task OpenStoryAsync(Story story)
	{
		var reading = _services.GetRequiredService<ReadingPage>();
		reading.Load(story);
		await Navigation.PushAsync(reading);
	}

	private async void OnLockTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(OpenManageAsync);

	private async Task OpenManageAsync()
	{
		if (await _gate.RequestAsync(Navigation))
		{
			await Navigation.PushAsync(_services.GetRequiredService<ManagePage>());
		}
	}
}
