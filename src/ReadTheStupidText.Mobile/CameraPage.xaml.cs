using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Screen 2 — camera capture → OCR → read (Slice 32, Decision 40, 43). Single-shot:
/// launches the system camera app via <see cref="MediaPicker"/> (no embedded live
/// viewfinder in this pass — a disclosed simplification of the design mock, the
/// same allowance Slice 30 used for Screen 1), extracts the photo's text with
/// <see cref="IImageTextExtractor"/>, and reads it through the exact same
/// <see cref="ISpeechReader"/> singleton <c>TypePage</c> uses — OCR is just
/// another text source feeding the one read pipeline, not new reading logic.
/// </summary>
public partial class CameraPage : ContentPage
{
    private readonly ISpeechReader _reader;
    private readonly IImageTextExtractor _extractor;
    private string? _extractedText;

    public CameraPage(ISpeechReader reader, IImageTextExtractor extractor)
    {
        InitializeComponent();
        _reader = reader;
        _extractor = extractor;
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

    private async void OnReadClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_extractedText))
        {
            await _reader.SpeakAsync(_extractedText);
        }
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
        ReadButton.IsEnabled = !string.IsNullOrWhiteSpace(text);

        IdleState.IsVisible = false;
        ProcessingState.IsVisible = false;
        ResultState.IsVisible = true;
    }
}
