using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The File tab (Slice 33, Decisions 34/35/41/43) — no dedicated screen mock yet
/// (Decision 43), so it reuses the card/list visual language of the other
/// screens. Wires a <see cref="FilePicker"/> to the *existing*
/// <see cref="IDocumentTextExtractor"/> from Batch 5 (no new extraction logic —
/// see Slice 33's plan note on promoting the extractors to the portable
/// `ReadTheStupidText.Documents` library) and reads the result through the same
/// <see cref="ISpeechReader"/> singleton every other screen uses.
/// </summary>
public partial class FilePage : ContentPage
{
    private static readonly FilePickerFileType SupportedTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.Android, ["text/plain", "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"] },
    });

    private readonly ISpeechReader _reader;
    private readonly IDocumentTextExtractor _extractor;
    private string? _extractedText;

    public FilePage(ISpeechReader reader, IDocumentTextExtractor extractor)
    {
        InitializeComponent();
        _reader = reader;
        _extractor = extractor;
    }

    private async void OnChooseFileClicked(object? sender, EventArgs e)
    {
        FileResult? file;
        try
        {
            file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose a .txt, .pdf or .docx file",
                FileTypes = SupportedTypes,
            });
        }
        catch (PermissionException)
        {
            await DisplayAlertAsync("Permission needed", "Grant file access to read a document.", "OK");
            return;
        }

        if (file is null)
        {
            return; // user cancelled the picker
        }

        ShowProcessing();
        try
        {
            string text = await _extractor.ExtractTextAsync(file.FullPath);
            ShowResult(file.FileName, text);
        }
        catch (DocumentTooLargeException ex)
        {
            await DisplayAlertAsync("File too large", $"That file has {ex.Actual} pages; the limit is {ex.Limit}.", "OK");
            ShowIdle();
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Couldn't read that file", "The file may be corrupted or in an unsupported format.", "OK");
            ShowIdle();
        }
    }

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

    private void ShowResult(string fileName, string text)
    {
        _extractedText = text;
        FileNameLabel.Text = fileName;
        ExtractedTextLabel.Text = string.IsNullOrWhiteSpace(text)
            ? "No text found in that file."
            : text;
        ReadButton.IsEnabled = !string.IsNullOrWhiteSpace(text);

        IdleState.IsVisible = false;
        ProcessingState.IsVisible = false;
        ResultState.IsVisible = true;
    }
}
