using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The text's language and the text holder's changes. Translation lives on <see cref="ReviewPage"/> (Slice 101):
/// this page reads the text as it is. The language detector still runs quietly, so the voice follows the
/// text's own language (Slice 90). Runs only where the platform supplies a detector.
/// </summary>
public partial class ReaderPage
{
    internal static readonly TimeSpan DetectDebounce = TimeSpan.FromMilliseconds(600);

    private ILanguageDetector? _detector;
    private CancellationTokenSource? _detectCts;

    // The detector's latest confident guess (a primary language code), or null.
    private string? _detectedSource;

    private void InitTranslation(ILanguageDetector? detector)
    {
        TranslateSourceMigration.RunOnce(_settings);
        if (detector is null)
        {
            return;
        }

        _detector = detector;
        if (!string.IsNullOrWhiteSpace(TextEditor.Text))
        {
            ScheduleDetection(TextEditor.Text);
        }
    }

    private string TargetCode() => AppLanguages.Code(CurrentLanguage());

    // The layout never mirrors (Slice 85); only the text follows its own direction. Empty, it follows the
    // UI language, so the placeholder sits on the side it reads from.
    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        TextEditor.FlowDirection = TextFlow.ContentDirection(e.NewTextValue);
        _highlight = null;
        RenderReading();

        ScheduleReadingLanguageRefresh();
        if (_detector is null)
        {
            return;
        }

        _detectedSource = null;
        if (string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            ResetSource();
            return;
        }

        ScheduleDetection(e.NewTextValue);
    }

    private void ResetSource()
    {
        _detectCts?.Cancel();
        _detectedSource = null;
    }

    // Scan and file import replace the text for good, so the old text's detected language goes too,
    // and the head start prepares at once (the TextChanged this raises must not also schedule a debounced one).
    private void ReplaceEditorText(string text)
    {
        // Whatever is playing (or generating) belongs to the old text.
        _reader.Stop();
        EndFirstAudioClock(UsageOutcome.Cancelled);
        ClearPendingRead();

        if (_detector is not null)
        {
            ResetSource();
        }

        _settingEditorText = true;
        try
        {
            TextEditor.Text = text;
        }
        finally
        {
            _settingEditorText = false;
        }

        StartHeadStart(TimeSpan.Zero);
    }

    private void ScheduleDetection(string text)
    {
        _detectCts?.Cancel();
        _detectCts = new CancellationTokenSource();
        _ = DetectAfterDelayAsync(text, _detectCts.Token);
    }

    private async Task DetectAfterDelayAsync(string text, CancellationToken token)
    {
        try
        {
            await Task.Delay(DetectDebounce, token);
            string? detected = TranslationFlow.SourceFromDetection(
                await _detector!.DetectAsync(TranslationFlow.DetectionSample(text), token));
            if (token.IsCancellationRequested || _retired)
            {
                return;
            }

            _detectedSource = detected;
            OnReadingLanguageMayHaveChanged();
        }
        catch (OperationCanceledException)
        {
            // Superseded by newer text: the newer pass decides.
        }
        catch (Exception)
        {
            // A failed detection leaves the language unknown; it never blocks reading.
            _detectedSource = null;
            OnReadingLanguageMayHaveChanged();
        }
    }

    private static string SourceName(string code) => TranslationLanguageChoices.Build([code])[0].Name;

    private void CancelDetection() => _detectCts?.Cancel();
}
