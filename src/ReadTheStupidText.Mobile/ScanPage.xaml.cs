using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Transient capture screen (Slice 32, Decision 40; design refresh post-Slice 34
/// — plan.md Decision 43's second resync). Pushed from <see cref="ReaderPage"/>'s
/// Scan action, never a tab: it captures a photo, extracts its text with
/// <see cref="IImageTextExtractor"/>, and on "Use text" hands the result back to
/// the reader via <see cref="PendingScanResult"/> before popping itself — it no
/// longer speaks the text itself, since there is only ever one player, on the
/// reader screen. See <see cref="PendingScanResult"/>'s remarks for why this
/// isn't a Shell query parameter.
/// </summary>
public partial class ScanPage : ContentPage
{
    private readonly IImageTextExtractor _extractor;
    private readonly PendingScanResult _pendingScan;
    private string? _extractedText;

    public ScanPage(IImageTextExtractor extractor, PendingScanResult pendingScan)
    {
        InitializeComponent();
        _extractor = extractor;
        _pendingScan = pendingScan;
    }

    private async void OnCaptureTapped(object? sender, TappedEventArgs e)
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            await DisplayAlertAsync("No camera", "This device has no camera to capture with.", "OK");
            return;
        }

        FileResult? photo;
        try
        {
            photo = await MediaPicker.Default.CapturePhotoAsync();
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlertAsync("No camera", "This device has no camera to capture with.", "OK");
            return;
        }
        catch (PermissionException)
        {
            await DisplayAlertAsync("Camera permission needed", "Grant camera access to photograph text.", "OK");
            return;
        }

        if (photo is null)
        {
            return; // user cancelled the system camera
        }

        ShowProcessing();
        try
        {
            string text = await _extractor.ExtractTextAsync(photo.FullPath);
            ShowResult(text);
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Couldn't read that photo", "Try capturing the text again with better lighting or focus.", "OK");
            ShowIdle();
        }
    }

    private void OnRetakeClicked(object? sender, EventArgs e) => ShowIdle();

    private async void OnUseTextClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_extractedText))
        {
            return;
        }

        _pendingScan.Set(_extractedText);
        await Shell.Current.GoToAsync("..");
    }

    private void ShowIdle()
    {
        _extractedText = null;
        IdleState.IsVisible = true;
        ProcessingState.IsVisible = false;
        ResultState.IsVisible = false;
    }

    private void ShowProcessing()
    {
        IdleState.IsVisible = false;
        ProcessingState.IsVisible = true;
        ResultState.IsVisible = false;
    }

    private void ShowResult(string text)
    {
        _extractedText = text;
        ExtractedTextLabel.Text = string.IsNullOrWhiteSpace(text)
            ? "No text found in that photo."
            : text;
        UseTextButton.IsEnabled = !string.IsNullOrWhiteSpace(text);

        IdleState.IsVisible = false;
        ProcessingState.IsVisible = false;
        ResultState.IsVisible = true;
    }
}
