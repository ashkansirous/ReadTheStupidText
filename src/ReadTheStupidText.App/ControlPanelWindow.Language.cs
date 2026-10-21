using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ReadTheStupidText_App;

/// <summary>
/// The panel's language picker and its "translate to my language" controls (Slice 57): the UI and
/// voice language row, the translate toggle in the <c>CONTROLS</c> row, and the source-language
/// picker plus note it reveals.
/// </summary>
public sealed partial class ControlPanelWindow
{
    private const int AutoSourceIndex = 0;

    private IReadOnlyList<TranslateSourceChoice> _sourceChoices = [];

    /// <summary>One entry of the source picker: a language code (null for auto-detect) and its name.</summary>
    public sealed record TranslateSourceChoice(string? Code, string Name);

    private void InitLanguageAndTranslation()
    {
        LanguageCombo.ItemsSource = LanguageChoices.All;
        _sourceChoices = BuildSourceChoices();
        TranslateSourceCombo.ItemsSource = _sourceChoices;
        TranslateNote.Text = TranslationNoteText.Default;
        if (!_readAloud.CanTranslate)
        {
            HideTranslateControls();
            return;
        }

        _readAloud.TranslationFinished += OnTranslationFinished;
        _readAloud.TranslationPackProgress += OnTranslationPackProgress;
        _readAloud.TranslateEnabledChanged += OnTranslateEnabledChanged;
        Closed += OnLanguagePartClosed;
    }

    private void OnLanguagePartClosed(object sender, WindowEventArgs args)
    {
        _readAloud.TranslationFinished -= OnTranslationFinished;
        _readAloud.TranslationPackProgress -= OnTranslationPackProgress;
        _readAloud.TranslateEnabledChanged -= OnTranslateEnabledChanged;
    }

    // Slice 75: the pack download runs on a background thread, so the note is set on the dispatcher.
    private void OnTranslationPackProgress(object? sender, TranslationPackProgress progress) =>
        _dispatcher.TryEnqueue(() => TranslateNote.Text = TranslationNoteText.For(progress));

    // A tray toggle changed the setting: mirror it here without echoing back into the service.
    private void OnTranslateEnabledChanged(object? sender, bool enabled) =>
        _dispatcher.TryEnqueue(() =>
        {
            _refreshing = true;
            SetTranslateToggle(enabled);
            _refreshing = false;
        });

    // No translation engine in this build: the toggle (and, with it, the source picker and note) is
    // invisible, so nothing suggests a feature that cannot run.
    private void HideTranslateControls()
    {
        TranslateToggle.Visibility = Visibility.Collapsed;
        TranslateColumn.Width = new GridLength(0);
        TranslatePanel.Visibility = Visibility.Collapsed;
    }

    private static IReadOnlyList<TranslateSourceChoice> BuildSourceChoices()
    {
        var choices = new List<TranslateSourceChoice> { new(null, UiStrings.Get("Translate_AutoDetect")) };
        choices.AddRange(LanguageChoices.All.Select(c => new TranslateSourceChoice(AppLanguages.Code(c.Language), c.NativeName)));
        return choices;
    }

    // Runs inside RefreshState (_refreshing is set), so selecting items here does not echo back.
    private void RefreshLanguageAndTranslation()
    {
        bool known = AppLanguages.TryParse(_settings.Language, out AppLanguage current);
        LanguageCombo.SelectedItem = known ? LanguageChoices.All.First(c => c.Language == current) : null;
        SetTranslateToggle(_readAloud.TranslateEnabled);
        SelectSourceChoice(_settings.TranslateSourceLanguage);
    }

    private void SelectSourceChoice(string? code)
    {
        int index = code is null ? -1 : _sourceChoices.ToList().FindIndex(c => c.Code == code);
        TranslateSourceCombo.SelectedIndex = index < 0 ? AutoSourceIndex : index;
    }

    // Picking a language stores it, starts the voice pack setup the way the language window does (the
    // picker is locked and the voice row shows the progress meanwhile), then restarts the app, because
    // x:Uid text is applied once when a window is built.
    private async void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || LanguageCombo.SelectedItem is not LanguageChoice choice)
        {
            return;
        }

        string code = AppLanguages.Code(choice.Language);
        if (string.Equals(code, _settings.Language, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _settings.Language = code;
        UiStrings.ApplyLanguage(code);
        LanguageCombo.IsEnabled = false;
        await StartVoicePackSetupAsync();
        RestartRequested?.Invoke(this, EventArgs.Empty);
    }

    // A failed setup is not fatal here: the startup quiet resume retries it after the restart.
    private async Task StartVoicePackSetupAsync()
    {
        try
        {
            await _packSetup.StartAsync();
        }
        catch (Exception)
        {
            // the restart's startup resume retries the setup
        }
    }

    /// <summary>Raised after the user picks a new language; the host restarts the app to apply it everywhere.</summary>
    public event EventHandler? RestartRequested;

    private void OnTranslateToggled(object sender, RoutedEventArgs e)
    {
        bool on = TranslateToggle.IsChecked == true;
        ApplyToggleVisual(TranslateToggle, TranslateIcon, on);
        SetTranslateTooltip(on);
        ShowTranslatePanel(on);
        if (!_refreshing)
        {
            _readAloud.TranslateEnabled = on;
        }
    }

    private void SetTranslateToggle(bool on)
    {
        TranslateToggle.IsChecked = on;
        ApplyToggleVisual(TranslateToggle, TranslateIcon, on);
        SetTranslateTooltip(on);
        ShowTranslatePanel(on);
    }

    private void SetTranslateTooltip(bool on) =>
        ToolTipService.SetToolTip(TranslateToggle, UiStrings.Format("Tip_Translate", OnOff(on)));

    private void ShowTranslatePanel(bool on)
    {
        Visibility visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (TranslatePanel.Visibility == visibility)
        {
            return;
        }

        TranslatePanel.Visibility = visibility;
        RefitToContent();
    }

    private void OnTranslateSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || TranslateSourceCombo.SelectedItem is not TranslateSourceChoice choice)
        {
            return;
        }

        _settings.TranslateSourceLanguage = choice.Code;
        TranslateNote.Text = TranslationNoteText.Default;
    }

    // May fire on a non-UI thread (the read pipeline), so the note is set on the dispatcher.
    private void OnTranslationFinished(object? sender, ReadTranslationResult result) =>
        _dispatcher.TryEnqueue(() => TranslateNote.Text = TranslationNoteText.For(result));
}
