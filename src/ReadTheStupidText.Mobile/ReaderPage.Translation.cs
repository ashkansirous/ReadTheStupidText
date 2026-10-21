using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Mobile.Resources.Strings;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The "translate to my language" half of the reader (Slice 54): the card under the editor, the
/// debounced language detection, and the translate-before-read step. Only shown where the platform
/// supplies a detector and a translator (Android today).
/// </summary>
public partial class ReaderPage
{
    private const int AutoChoiceIndex = 0;
    private static readonly TimeSpan DetectDebounce = TimeSpan.FromMilliseconds(600);

    private ILanguageDetector? _detector;
    private ITranslator? _translator;
    private IReadOnlyList<TranslationLanguageChoice> _sourceChoices = [];
    private CancellationTokenSource? _detectCts;
    private CancellationTokenSource? _translateCts;
    private bool _syncingSourcePicker;
    private const int PreviewLength = 600;

    // The read id whose "Downloading" note is on screen, so a superseded read never clears a newer one's.
    private int _downloadNoteReadId;

    // The detector's latest confident guess (a primary language code), or null.
    private string? _detectedSource;

    private void InitTranslation(ILanguageDetector? detector, ITranslator? translator)
    {
        if (detector is null || translator is null)
        {
            return;
        }

        _detector = detector;
        _translator = translator;
        TranslateCard.IsVisible = true;
        TranslateNoteLabel.Text = AppResources.Translate_QualityNote;
        TranslateToggleLabel.Text = AppResources.Format(AppResources.Translate_ToggleLabel, LanguageChoices.NativeName(CurrentLanguage()));
        if (!translator.SupportedLanguages.Contains(TargetCode(), StringComparer.OrdinalIgnoreCase))
        {
            ShowTargetUnsupported();
            return;
        }

        _sourceChoices = TranslationLanguageChoices.Build(translator.SupportedLanguages);
        FillSourcePicker();
        TranslateSwitch.IsToggled = _settings.TranslateEnabled;
        RestoreSourceChoice();
    }

    private string TargetCode() => AppLanguages.Code(CurrentLanguage());

    // A gap, not a silent no-op: the controls go and the note says why.
    private void ShowTargetUnsupported()
    {
        TranslateControls.IsVisible = false;
        TranslateNoteLabel.Text = AppResources.Format(AppResources.Translate_UnsupportedNote, LanguageChoices.NativeName(CurrentLanguage()));
    }

    private void FillSourcePicker()
    {
        SourcePicker.Items.Add(AppResources.Translate_AutoDetect);
        foreach (TranslationLanguageChoice choice in _sourceChoices)
        {
            SourcePicker.Items.Add(choice.Name);
        }

        _syncingSourcePicker = true;
        SourcePicker.SelectedIndex = AutoChoiceIndex;
        _syncingSourcePicker = false;
    }

    // A rebuilt page (language change) keeps its text, so keep the user's pick too; a fresh start
    // with an empty editor forgets it.
    private void RestoreSourceChoice()
    {
        if (string.IsNullOrWhiteSpace(TextEditor.Text))
        {
            _settings.TranslateSourceLanguage = null;
            return;
        }

        SelectSource(_settings.TranslateSourceLanguage);
        ScheduleDetection(TextEditor.Text);
    }

    private void SelectSource(string? code)
    {
        int index = code is null ? -1 : IndexOfChoice(code);
        ResetNote();
        _syncingSourcePicker = true;
        SourcePicker.SelectedIndex = index < 0 ? AutoChoiceIndex : index + 1;
        _syncingSourcePicker = false;
    }

    private int IndexOfChoice(string code)
    {
        for (int i = 0; i < _sourceChoices.Count; i++)
        {
            if (_sourceChoices[i].Code == code)
            {
                return i;
            }
        }

        return -1;
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_detector is null)
        {
            return;
        }

        TranslatedTextLabel.IsVisible = false;
        _detectedSource = null;
        if (string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            ResetSource();
            return;
        }

        ScheduleDetection(e.NewTextValue);
    }

    // The override sticks while the user edits; it goes when the text is cleared or replaced wholesale.
    private void ResetSource()
    {
        _detectCts?.Cancel();
        _detectedSource = null;
        _settings.TranslateSourceLanguage = null;
        ResetNote();
        SelectSource(null);
    }

    // Paste, file import and scan replace the text for good, so the old text's language pick goes too.
    private void ReplaceEditorText(string text)
    {
        if (_detector is not null)
        {
            ResetSource();
        }

        TextEditor.Text = text;
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
            if (_settings.TranslateSourceLanguage is not null)
            {
                return;
            }

            string? detected = TranslationFlow.SourceFromDetection(
                await _detector!.DetectAsync(TranslationFlow.DetectionSample(text), token));
            if (token.IsCancellationRequested || _retired || _settings.TranslateSourceLanguage is not null)
            {
                return;
            }

            _detectedSource = detected;
            SelectSource(detected);
        }
        catch (OperationCanceledException)
        {
            // Superseded by newer text: the newer pass decides.
        }
        catch (Exception)
        {
            // A failed detection leaves the source unknown; it never blocks reading.
            _detectedSource = null;
        }
    }

    private void OnSourcePickerChanged(object? sender, EventArgs e)
    {
        if (_syncingSourcePicker)
        {
            return;
        }

        _lastSpokenText = null;
        ResetNote();
        int index = SourcePicker.SelectedIndex;
        _settings.TranslateSourceLanguage = index > AutoChoiceIndex ? _sourceChoices[index - 1].Code : null;
        if (index == AutoChoiceIndex && !string.IsNullOrWhiteSpace(TextEditor.Text))
        {
            ScheduleDetection(TextEditor.Text);
        }
    }

    private void OnTranslateToggled(object? sender, ToggledEventArgs e)
    {
        _settings.TranslateEnabled = e.Value;
        _lastSpokenText = null;
    }

    /// <summary>
    /// What to hand the reader: the translation when the toggle is on and the pair differs, otherwise
    /// the text itself. Null means the read was superseded while translating. A failure (or an empty
    /// translation) never blocks reading: it is announced and the original is read.
    /// </summary>
    private async Task<string?> TextToSpeakAsync(string text, int readId)
    {
        CancelTranslationRead();
        if (_translator is null || !_settings.TranslateEnabled)
        {
            return text;
        }

        CancellationTokenSource cts = _translateCts = new CancellationTokenSource();
        try
        {
            return await TranslateOrOriginalAsync(text, readId, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return null;
        }
        catch (TranslationModelUnavailableException)
        {
            await AlertAsync(AppResources.Translate_ModelUnavailableTitle, AppResources.Translate_ModelUnavailableMessage);
        }
        catch (Exception)
        {
            await AlertAsync(AppResources.Translate_FailedTitle, AppResources.Translate_FailedMessage);
        }
        finally
        {
            ClearDownloadingNote(readId);
        }

        return text;
    }

    private async Task<string> TranslateOrOriginalAsync(string text, int readId, CancellationToken token)
    {
        string? source = await ResolveSourceAsync(text, token);
        if (!TranslationFlow.ShouldTranslate(_settings.TranslateEnabled, source, TargetCode()))
        {
            return text;
        }

        string translated = await TranslateAsync(text, source!, readId, token);
        if (string.IsNullOrWhiteSpace(translated))
        {
            await AlertAsync(AppResources.Translate_FailedTitle, AppResources.Translate_FailedMessage);
            return text;
        }

        TranslatedTextLabel.Text = TranslationFlow.Prefix(translated, PreviewLength);
        TranslatedTextLabel.IsVisible = true;
        return translated;
    }

    // The override, else the last confident detection. If the text changed since (or detection has not
    // run), a fresh detection on this text decides, so a Play inside the debounce never uses a stale language.
    private async Task<string?> ResolveSourceAsync(string text, CancellationToken token)
    {
        string? source = _settings.TranslateSourceLanguage ?? _detectedSource;
        if (source is not null)
        {
            return source;
        }

        _detectCts?.Cancel();
        string? detected = null;
        try
        {
            detected = TranslationFlow.SourceFromDetection(
                await _detector!.DetectAsync(TranslationFlow.DetectionSample(text), token));
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            // A failed detection means an unknown source: the original is read.
        }

        token.ThrowIfCancellationRequested();
        _detectedSource = detected;
        SelectSource(detected);
        return detected;
    }

    private async Task<string> TranslateAsync(string text, string source, int readId, CancellationToken token)
    {
        string target = TargetCode();
        if (!TranslationFlow.IsPairSupported(_translator!.SupportedLanguages.ToArray(), source, target))
        {
            TranslateNoteLabel.Text = AppResources.Format(AppResources.Translate_SourceUnsupportedNote, SourceName(source));
            return text;
        }

        if (!await ModelReadyAsync(source, target))
        {
            TranslateNoteLabel.Text = AppResources.Translate_DownloadingStatus;
            _downloadNoteReadId = readId;
        }

        return await TranslationFlow.TranslateAsync(_translator, text, source, target, token);
    }

    private async Task<bool> ModelReadyAsync(string source, string target)
    {
        try
        {
            return await _translator!.IsModelReadyAsync(source, target);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void ClearDownloadingNote(int readId)
    {
        if (_downloadNoteReadId == readId)
        {
            _downloadNoteReadId = 0;
            ResetNote();
        }
    }

    // Back to the quality note: drops a transient note (pair unsupported), but never an in-flight
    // read's "Downloading" note, which its own read clears.
    private void ResetNote()
    {
        if (_downloadNoteReadId != 0)
        {
            return;
        }

        TranslateNoteLabel.Text = AppResources.Translate_QualityNote;
    }

    private string SourceName(string code) =>
        _sourceChoices.FirstOrDefault(c => c.Code == code)?.Name ?? code;

    private void CancelTranslation()
    {
        _detectCts?.Cancel();
        CancelTranslationRead();
    }

    // Supersedes any translation in flight (a new read, Stop, a rebuild) and releases its token source.
    private void CancelTranslationRead()
    {
        CancellationTokenSource? old = _translateCts;
        _translateCts = null;
        old?.Cancel();
        old?.Dispose();
    }
}
