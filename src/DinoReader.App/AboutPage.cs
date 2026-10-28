using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>
/// The About &amp; updates sub-page of Settings (Slice 172, mock frame 84): <see cref="VersionCard"/> with the "Show the intro again" row
/// (Slice 155), which now ends back on this page. The card is built again whenever the page appears or a fetched config changes
/// (Slice 132), so the "A new version is ready" row follows the config while the page is open.
/// </summary>
public sealed class AboutPage : SettingsSubPage
{
	private readonly AppConfigClient _config;
	private readonly IServiceProvider _services;
	private View? _card;

	public AboutPage(AppConfigClient config, IServiceProvider services)
		: base(AppResources.Settings_About)
	{
		_config = config;
		_services = services;
		Unloaded += (_, _) => _config.Changed -= OnConfigChanged;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_config.Changed += OnConfigChanged;
		ShowVersionCard();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_config.Changed -= OnConfigChanged;
	}

	private void OnConfigChanged(object? sender, EventArgs e) => ShowVersionCard();

	private void ShowVersionCard()
	{
		if (_card is not null)
		{
			Body.Children.Remove(_card);
		}

		View introRow = SettingsRows.IntroReplayRow(Navigation, _services, Busy);
		_card = VersionCard.Build(_config, Busy, introRow);
		Body.Children.Add(_card);
	}
}
