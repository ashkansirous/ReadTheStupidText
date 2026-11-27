using System.Net;
using System.Text;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Sirous.Reading.Application.Reading;
using Sirous.Reading.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>
/// The Link screen (Slice 438): Sonos opens the app with a link code, the grown-up picks the story language (the reading language first) and presses
/// Connect; the app tells the Worker, then opens the Sonos address the Worker returns so Sonos can finish. Nothing about the child is sent: the code and the language.
/// </summary>
public partial class SonosLinkPage : ContentPage
{
	private const string SonosEndpoint = StoreClient.BaseAddress + "/sonos";
	private const string JsonMime = "application/json";
	private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(20);
	private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };

	private readonly IReadOnlyList<string> _languages = SonosLink.LanguagesFor(LanguageChoice.Current());
	private readonly WaitController _wait;
	private string _code = string.Empty;
	private bool _done;
	private bool _connecting;

	public SonosLinkPage()
	{
		InitializeComponent();
		LanguagePicker.ItemsSource = _languages.Select(code => LanguageNames.NativeName(code)).ToList();
		LanguagePicker.SelectedIndex = 0;
		WaitOverlay overlay = DinoWait.OverPage();
		LinkRoot.Children.Add(overlay);
		_wait = new WaitController(overlay);
	}

	/// <summary>Sets the link code this screen connects (the router calls it before the page is shown).</summary>
	public void Load(string code)
	{
		if (_connecting)
		{
			return;
		}

		_code = code;
		_done = false;
		ErrorLabel.IsVisible = false;
	}

	private async void OnConnectClicked(object? sender, EventArgs e)
	{
		if (_done || _code.Length == 0)
		{
			return;
		}

		ErrorLabel.IsVisible = false;
		string language = _languages[Math.Max(0, LanguagePicker.SelectedIndex)];
		ConnectButton.IsEnabled = false;
		_connecting = true;
		try
		{
			await ConnectAsync(language);
		}
		catch (Exception ex)
		{
			// no-fallback: an unexpected failure in the connect flow is shown to the grown-up as the could-not-reach line instead of ending the app
			ShowError(AppResources.SonosLink_Failed, $"unexpected {ex.GetType().Name}");
		}
		finally
		{
			_connecting = false;
			ConnectButton.IsEnabled = true;
		}
	}

	private async Task ConnectAsync(string language)
	{
		(HttpStatusCode? status, string body) answer;
		try
		{
			answer = await _wait.RunAsync(WaitStage.GettingFile, AppResources.SonosLink_Connecting, canCancel: true, token => PostAsync(language, token));
		}
		catch (OperationCanceledException)
		{
			// no-fallback: Cancel on the wait; the grown-up stays on the screen and can press Connect again
			Log("cancelled");
			return;
		}

		if (answer.status == HttpStatusCode.NotFound)
		{
			ShowError(AppResources.SonosLink_Expired, "expired");
		}
		else if (answer.status != HttpStatusCode.OK || SonosLink.CallbackOf(answer.body) is not { } callback)
		{
			ShowError(AppResources.SonosLink_Failed, "failed");
		}
		else
		{
			await HandBackAsync(callback, language);
		}
	}

	private async Task<(HttpStatusCode? Status, string Body)> PostAsync(string language, CancellationToken cancel)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
		timeout.CancelAfter(CallTimeout);
		using var content = new StringContent(SonosLink.ConfirmBody(_code, language), Encoding.UTF8, JsonMime);
		try
		{
			using HttpResponseMessage response = await Http.PostAsync(SonosEndpoint, content, timeout.Token);
			return (response.StatusCode, await response.Content.ReadAsStringAsync(timeout.Token));
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancel.IsCancellationRequested)
		{
			// no-fallback: an unreachable server or a timeout is shown to the grown-up as "could not reach", nothing is counted or sent
			return (null, ex.GetType().Name);
		}
	}

	private async Task HandBackAsync(Uri callback, string language)
	{
		_done = true;
		Log($"connected language={language}");
		try
		{
			await Launcher.Default.OpenAsync(callback);
		}
		catch (Exception ex)
		{
			// no-fallback: Sonos could not be opened (the platform throws different types when no app handles the link); the grown-up sees the could-not-reach line and stays on the screen
			ShowError(AppResources.SonosLink_Failed, $"sonos not opened {ex.GetType().Name}");
			return;
		}

		if (Navigation.NavigationStack.Contains(this))
		{
			await Navigation.PopAsync();
		}
	}

	private void ShowError(string text, string marker)
	{
		ErrorLabel.Text = text;
		ErrorLabel.IsVisible = true;
		Log(marker);
	}

	private static void Log(string line)
	{
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[SonosLink] {line}");
#endif
	}
}

/// <summary>
/// Where a Sonos link goes (Slice 438): the platform handlers (Android intent, iOS URL scene) call <see cref="Receive"/> with whatever URL opened the app; a link code
/// reaches the window once it exists (a cold start waits for <see cref="Attach"/>), anything else is ignored.
/// </summary>
public static class SonosLinkRouter
{
	private static Action<string>? _open;
	private static string? _pending;

	public static void Receive(string? link)
	{
		if (SonosLink.CodeOf(link) is not { } code)
		{
			return;
		}

		MainThread.BeginInvokeOnMainThread(() =>
		{
			if (_open is { } open)
			{
				open(code);
			}
			else
			{
				_pending = code;
			}
		});
	}

	/// <summary>The window is up: sends a link that arrived before it, and every later one, to <paramref name="open"/>.</summary>
	public static void Attach(Action<string> open)
	{
		_open = open;
		if (_pending is { } code)
		{
			_pending = null;
			open(code);
		}
	}

	/// <summary>The window is going away: a link that arrives now waits in <c>_pending</c> for the next window (only if <paramref name="open"/> is still the attached one, so a newer window is not detached).</summary>
	public static void Detach(Action<string> open)
	{
		if (_open == open)
		{
			_open = null;
		}
	}
}
