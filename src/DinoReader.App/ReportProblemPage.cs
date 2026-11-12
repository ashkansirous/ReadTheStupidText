using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared.Crashes;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>
/// "Report a problem" in the grown-ups' corner (Slice 249): says in plain words what is sent (the app's recent technical log, no names, titles or typed text), and has one
/// Send button. Sending is a wait with Cancel (the shared overlay after 500 ms). On success the page shows a code to quote when writing to us, with a Copy button; on failure it says
/// so, keeps the report for the crash queue's retry and offers Try again with the same code. Android and iOS; the row is not shown on Windows (crash reporting is off there).
/// </summary>
public sealed class ReportProblemPage : SettingsSubPage
{
	private const string SendId = "ReportProblemSend";
	private const string SentId = "ReportProblemSent";
	private const string CodeId = "ReportProblemCode";
	private const string CopyId = "ReportProblemCopy";
	private const string FailedId = "ReportProblemFailed";
	private const double CardRadius = 18;
	private const double CardPadding = 14;
	private const double BodySize = 14;
	private const double CodeSize = 26;
	private const double ButtonHeight = 48;
	private static readonly TimeSpan CopiedShownFor = TimeSpan.FromSeconds(3);

	private readonly WaitController _wait;
	private readonly Button _send;
	private readonly VerticalStackLayout _result = new() { Spacing = 10 };
	private readonly Border _resultCard;
	private string? _code;

	public ReportProblemPage()
		: base(AppResources.ReportProblem_Title)
	{
		WaitOverlay overlay = DinoWait.OverPage();
		AddOverlay(overlay);
		_wait = new WaitController(overlay);
		_send = SendButton();
		Body.Children.Add(Card(new Label { Text = AppResources.ReportProblem_Explain, FontSize = BodySize, TextColor = DinoTokens.Get(DinoTokens.Body) }));
		Body.Children.Add(_send);
		_resultCard = Card(_result);
		_resultCard.IsVisible = false;
		Body.Children.Add(_resultCard);
	}

	private Button SendButton()
	{
		var button = new Button
		{
			Text = AppResources.ReportProblem_Send,
			AutomationId = SendId,
			HeightRequest = ButtonHeight,
			CornerRadius = (int)(ButtonHeight / 2),
			BackgroundColor = DinoTokens.Get(DinoTokens.Brand),
			TextColor = Colors.White,
			FontAttributes = FontAttributes.Bold,
		};
		return UiUsage.Press(button, UiControls.Send, async () => await Busy.RunAsync(SendAsync)); // Slice 352: that Send was pressed, never what was written
	}

	private static Border Card(View content) => new()
	{
		BackgroundColor = Colors.White,
		StrokeThickness = 0,
		StrokeShape = new RoundRectangle { CornerRadius = CardRadius },
		Padding = CardPadding,
		Content = content,
	};

	// The code is made once per report: a retry after a failure sends the same code, so the one the person may already have seen stays the one to quote.
	private async Task SendAsync()
	{
		string code = _code ??= CrashReporter.NewReportCode();
		UserReportOutcome outcome = await _wait.RunAsync(
			WaitStage.Saving, AppResources.ReportProblem_Sending, canCancel: true,
			token => CrashReporter.SendUserReportAsync(code, StoreClient.BaseAddress, token));
		switch (outcome)
		{
			case UserReportOutcome.Sent:
				_code = null;
				ShowSent(code);
				break;
			case UserReportOutcome.FailedKept:
				ShowFailed(AppResources.ReportProblem_Failed);
				break;
			case UserReportOutcome.Failed:
				ShowFailed(AppResources.ReportProblem_FailedNotKept);
				break;
			case UserReportOutcome.Cancelled:
				ShowStart();
				break;
		}
	}

	// Cancel puts the page back as it was before the first press: no old failure card, the plain Send button.
	private void ShowStart()
	{
		_result.Children.Clear();
		_resultCard.IsVisible = false;
		_send.Text = AppResources.ReportProblem_Send;
		_send.IsVisible = true;
	}

	private void ShowSent(string code)
	{
		string shown = ReportCode.Display(code);
		var codeLabel = new Label
		{
			Text = shown,
			AutomationId = CodeId,
			FontSize = CodeSize,
			FontAttributes = FontAttributes.Bold,
			TextColor = DinoTokens.Get(DinoTokens.Brand),
			HorizontalOptions = LayoutOptions.Start,
		};
		_result.Children.Clear();
		_result.Children.Add(new Label { Text = AppResources.ReportProblem_Sent, AutomationId = SentId, FontSize = BodySize, FontAttributes = FontAttributes.Bold });
		_result.Children.Add(new Label { Text = AppResources.ReportProblem_QuoteCode, FontSize = BodySize, TextColor = DinoTokens.Get(DinoTokens.Body) });
		_result.Children.Add(codeLabel);
		_result.Children.Add(CopyButton(shown));
		ShowResult();
		_send.IsVisible = false;
		SemanticScreenReader.Announce($"{AppResources.ReportProblem_Sent} {AppResources.ReportProblem_QuoteCode} {string.Join(' ', shown.AsEnumerable())}");
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"REPORT_PROBLEM code-shown {shown}");
#endif
	}

	private Button CopyButton(string shown)
	{
		var copy = new Button
		{
			Text = AppResources.ReportProblem_Copy,
			AutomationId = CopyId,
			HeightRequest = ButtonHeight,
			CornerRadius = (int)(ButtonHeight / 2),
			BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
			TextColor = DinoTokens.Get(DinoTokens.Brand),
			FontAttributes = FontAttributes.Bold,
			HorizontalOptions = LayoutOptions.Start,
			Padding = new Thickness(22, 0),
		};
		return UiUsage.Press(copy, UiControls.CopyCode, async () => await CopyCodeAsync(copy, shown));
	}

	private static async Task CopyCodeAsync(Button copy, string shown)
	{
		try
		{
			await Clipboard.Default.SetTextAsync(shown);
		}
		catch (Exception)
		{
			// no-fallback: the clipboard refused; the code stays on screen to read and type
			return; // the clipboard can refuse (a locked-down device); the code stays on screen to read and type
		}
		copy.Text = AppResources.ReportProblem_Copied;
		await Task.Delay(CopiedShownFor);
		copy.Text = AppResources.ReportProblem_Copy;
	}

	private void ShowFailed(string text)
	{
		_result.Children.Clear();
		_result.Children.Add(new Label { Text = text, AutomationId = FailedId, FontSize = BodySize, TextColor = DinoTokens.Get(DinoTokens.Body) });
		ShowResult();
		_send.Text = AppResources.ReportProblem_TryAgain;
		SemanticScreenReader.Announce(text);
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write("REPORT_PROBLEM failed");
#endif
	}

	private void ShowResult() => _resultCard.IsVisible = true;
}
