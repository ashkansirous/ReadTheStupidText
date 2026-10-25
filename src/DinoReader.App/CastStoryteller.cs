using System.Diagnostics;
using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Reading;

namespace DinoReader;

/// <summary>
/// The "Storyteller voices" card at the top of the cast screen (Slice 114, mock frames 48 and 49): for an English book without the pack the
/// offer with Download and "Hear one"; once installed a green line, with "Use Dino's first voices" as a button the page puts at its foot.
/// It is empty for any other language and while the catalog is not known. Installing or removing the pack calls <c>changed</c>, so the page
/// casts the book again from the voices it has now. The page's busy guard does not wrap a download, so the grown-up can still leave: a download
/// that finishes after that installs the pack and does nothing else (the page casts the book when it is opened again).
/// </summary>
internal sealed class StorytellerCard : ContentView
{
	private const double ButtonHeight = 44;
	private const double UseFirstHeight = 48;
	private const double DownloadStrokeWidth = 2.6;
	private static readonly TimeSpan RemoveRetryDelay = TimeSpan.FromMilliseconds(600);

	private readonly StorytellerController _controller;
	private readonly Func<string, string, Task> _alert;
	private readonly Func<Task> _changed;
	private readonly Action _stopReading;
	private CancellationTokenSource _lookup = new();
	private readonly Border _useFirst;
	private StorytellerView _view = new(StorytellerState.Hidden, 0);
	private double? _downloading;
	private bool _sampleBusy;
	private bool _samplePlaying;
	private bool _left;
	private bool _shownInstalled;

	public StorytellerCard(StorytellerController controller, Func<string, string, Task> alert, Func<Task> changed, Action stopReading)
	{
		_controller = controller;
		_alert = alert;
		_changed = changed;
		_stopReading = stopReading;
		_useFirst = BuildUseFirst();
		IsVisible = false;
	}

	/// <summary>Whether the pack is installed as far as the card knows; the page folds its rows while it is (mock frame 49).</summary>
	public bool IsInstalled => _view.State == StorytellerState.Installed;

	/// <summary>Raised when <see cref="IsInstalled"/> changed after a lookup, so the page draws its rows again.</summary>
	public event EventHandler? InstalledChanged;

	/// <summary>The "Use Dino's first voices" pill, for the page to place at its foot; visible only while the pack is installed.</summary>
	public View UseFirstVoices => _useFirst;

	/// <summary>Looks the card up for <paramref name="language"/> and draws it. The catalog fetch can take a while, so the page does not wait for it.</summary>
	public async Task ShowAsync(string language)
	{
		_left = false;
		_lookup.Cancel();
		_lookup = new CancellationTokenSource();
		try
		{
			_view = await _controller.LoadAsync(language, _lookup.Token);
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader storyteller lookup failed: {ex}");
			_view = new StorytellerView(StorytellerState.Hidden, 0);
		}

		Draw();
		RaiseInstalledChanged();
		if (IsInstalled)
		{
			await UpdateQuietlyAsync(_lookup.Token);
		}
	}

	private void RaiseInstalledChanged()
	{
		if (IsInstalled == _shownInstalled)
		{
			return;
		}

		_shownInstalled = IsInstalled;
		InstalledChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Stops the sample and drops any lookup still running; a download in flight carries on and installs.</summary>
	public void Leave()
	{
		_left = true;
		_lookup.Cancel();
		StopSample();
	}

	// A newer version of the installed pack comes in quietly (unmetered connection only); a failure is never shown, the next open tries again.
	private async Task UpdateQuietlyAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _controller.UpdateInstalledAsync(cancellationToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Debug.WriteLine($"Dino Reader storyteller update failed: {ex}");
		}
		catch (OperationCanceledException)
		{
			// The page was left or opened again: nothing to do.
		}
	}

	// The player never reports an end after a stop, so the flag is cleared here or the next "Hear one" would only stop nothing.
	private void StopSample()
	{
		_controller.StopSample();
		_samplePlaying = false;
	}

	private void Draw()
	{
		IsVisible = _view.State != StorytellerState.Hidden;
		_useFirst.IsVisible = _view.State == StorytellerState.Installed;
		Content = _view.State switch
		{
			StorytellerState.Offer => BuildOffer(),
			StorytellerState.Installed => BuildInstalled(),
			_ => null,
		};
	}

	private Border BuildOffer()
	{
		var title = new Label { Text = CastText.StorytellerTitle, FontSize = 15, FontAttributes = FontAttributes.Bold };
		SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level2);
		var text = new Label
		{
			Text = CastText.StorytellerText(_view.Megabytes), FontSize = 12.5, LineHeight = 1.4, TextColor = DinoTokens.Get(DinoTokens.Body),
		};
		var buttons = new Grid { ColumnSpacing = 8, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		buttons.Add(DownloadButton(), 0);
		buttons.Add(HearOneButton(), 1);
		return new Border
		{
			BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
			StrokeThickness = 0,
			Padding = new Thickness(14, 12),
			StrokeShape = new RoundRectangle { CornerRadius = 18 },
			Content = new VerticalStackLayout { Spacing = 8, Children = { title, text, buttons } },
		};
	}

	private Border DownloadButton()
	{
		Color brand = DinoTokens.Get(DinoTokens.Brand);
		string label = _downloading is { } progress ? progress.ToString("P0", CultureInfo.CurrentUICulture) : CastText.StorytellerDownload;
		var content = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		content.Children.Add(new IconView { Kind = IconKind.Download, Color = Colors.White, Size = 16, StrokeWidth = DownloadStrokeWidth, VerticalOptions = LayoutOptions.Center });
		content.Children.Add(new Label { Text = label, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center });
		var button = new Border
		{
			HeightRequest = ButtonHeight, BackgroundColor = brand, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = ButtonHeight / 2 },
			Content = content,
		};
		SemanticProperties.SetDescription(button, CastText.StorytellerDownload);
		CastViews.AddTap(button, DownloadAsync);
		return button;
	}

	private Border HearOneButton()
	{
		Color brand = DinoTokens.Get(DinoTokens.Brand);
		var content = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
		content.Children.Add(new IconView { Kind = IconKind.Play, Color = brand, Filled = true, Size = 14, VerticalOptions = LayoutOptions.Center });
		content.Children.Add(new Label { Text = CastText.StorytellerHearOne, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = brand, VerticalOptions = LayoutOptions.Center });
		var button = new Border
		{
			HeightRequest = ButtonHeight, Padding = new Thickness(14, 0), BackgroundColor = Colors.White, StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = ButtonHeight / 2 }, Content = content,
		};
		SemanticProperties.SetDescription(button, CastText.StorytellerHearOne);
		CastViews.AddTap(button, HearOneAsync);
		return button;
	}

	private static View BuildInstalled()
	{
		Color on = DinoTokens.Get(DinoTokens.StorytellerOn);
		var line = new HorizontalStackLayout { Spacing = 8, Padding = new Thickness(4, 2), VerticalOptions = LayoutOptions.Center };
		line.Children.Add(new IconView { Kind = IconKind.Check, Color = on, Size = 16, StrokeWidth = 3, VerticalOptions = LayoutOptions.Center });
		line.Children.Add(new Label { Text = CastText.StorytellerOn, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = on, VerticalOptions = LayoutOptions.Center });
		return line;
	}

	private Border BuildUseFirst()
	{
		var button = new Border
		{
			IsVisible = false,
			HeightRequest = UseFirstHeight,
			Margin = new Thickness(14, 0, 14, 14),
			BackgroundColor = Colors.Transparent,
			Stroke = DinoTokens.Get(DinoTokens.MergeBorder),
			StrokeThickness = 2,
			StrokeShape = new RoundRectangle { CornerRadius = UseFirstHeight / 2 },
			Content = new Label
			{
				Text = CastText.StorytellerUseFirst, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Brand),
				HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
			},
		};
		SemanticProperties.SetDescription(button, CastText.StorytellerUseFirst);
		CastViews.AddTap(button, UseFirstVoicesAsync);
		return button;
	}

	private async Task DownloadAsync()
	{
		if (_downloading is not null)
		{
			return;
		}

		StopSample();
		_downloading = 0;
		Draw();
		PackDownloadResult result = await RunDownloadAsync();
		_downloading = null;
		if (_left)
		{
			return;
		}

		if (result == PackDownloadResult.Ok)
		{
			await _changed();
			return;
		}

		Draw();
		await _alert(CastText.StorytellerTitle, CastText.StorytellerFailed);
	}

	private async Task<PackDownloadResult> RunDownloadAsync()
	{
		var progress = new Progress<double>(value =>
		{
			// Progress posts to the UI thread, so a report can land after the download ended: it must not bring the percentage back.
			if (_downloading is null)
			{
				return;
			}

			_downloading = value;
			Draw();
		});
		try
		{
			return await _controller.DownloadAsync(progress, CancellationToken.None);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader storyteller download failed: {ex}");
			return PackDownloadResult.Failed;
		}
	}

	private async Task HearOneAsync()
	{
		if (_samplePlaying)
		{
			StopSample();
			return;
		}

		if (_sampleBusy)
		{
			return;
		}

		_sampleBusy = true;
		try
		{
			string path = await _controller.PrepareSampleAsync(CancellationToken.None);

			// The flag is up before play starts: a clip that ends or fails at once then clears it, not leaves it stuck.
			_samplePlaying = true;
			_controller.PlaySample(path, () => _samplePlaying = false, () => MainThread.BeginInvokeOnMainThread(SampleFailed));
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader storyteller sample failed: {ex}");
			await _alert(CastText.StorytellerTitle, CastText.PreviewFailedBody);
		}
		finally
		{
			_sampleBusy = false;
		}
	}

	private void SampleFailed()
	{
		_samplePlaying = false;
		_ = _alert(CastText.StorytellerTitle, CastText.PreviewFailedBody);
	}

	private async Task UseFirstVoicesAsync()
	{
		StopSample();
		_stopReading();
		if (!await RemovePackAsync())
		{
			await _alert(CastText.StorytellerTitle, CastText.SaveFailedBody);
			return;
		}

		await _changed();
	}

	// The reader stops on its own thread and may still hold a pack file for a moment (a locked file fails the removal as a whole), so one retry follows a short wait.
	private async Task<bool> RemovePackAsync()
	{
		if (_controller.UseFirstVoices())
		{
			return true;
		}

		await Task.Delay(RemoveRetryDelay);
		return _controller.UseFirstVoices();
	}
}
