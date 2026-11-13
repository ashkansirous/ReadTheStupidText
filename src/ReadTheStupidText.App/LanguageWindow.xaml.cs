using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Usage;
using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace ReadTheStupidText_App;

/// <summary>
/// The first-run language window (Slice 56, Decision 68): fifteen languages named in themselves, the same set and
/// persistence as the mobile apps (<see cref="ISettingsStore.Language"/>). Choosing one starts
/// <see cref="VoicePackSetup"/>; the window shows its progress and closes once the natural voice is ready.
/// </summary>
public sealed partial class LanguageWindow : Window
{
    private readonly ISettingsStore _settings;
    private readonly VoicePackSetup _setup;
    private readonly ISystemLog _log;
    private readonly Action _restart;
    private readonly IFallbackSink _fallbacks;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private bool _populating;
    private bool _closed;
    private bool _languageChanged;
    private readonly string? _initialLanguage;
    private readonly List<LanguageRowItem> _rows = [.. LanguageChoices.All.Select(c => new LanguageRowItem(c))];
    private HashSet<string>? _installed; // language codes whose pack is installed; null until the first good read; a failed read keeps the last good set (UI thread only)
    private bool _escapedFailure; // an exception left the installer: the row shows the general failure, as a failed download
    private string? _runLanguage; // the language the latest run (StartSetup) serves: the run row, the only row that shows the run's state, error and Retry
    private bool _starting; // a run was just started and has not published yet: the run row shows Resolving, never a state the previous run left behind
    private PackSetupState _lastState = PackSetupState.Idle; // dispatcher thread only
    private int _installedScan; // sequence number of the latest installed-state scan; an older scan never overwrites a newer one

    public LanguageWindow(ISettingsStore settings, VoicePackSetup setup, ISystemLog log, Action restart, IFallbackSink fallbacks)
    {
        _fallbacks = fallbacks;
        _settings = settings;
        _setup = setup;
        _initialLanguage = settings.Language;
        _runLanguage = setup.CurrentLanguage;
        _log = log;
        _restart = restart;
        InitializeComponent();
        RootGrid.FlowDirection = FlowDirection.LeftToRight; // layout never mirrors (Slice 88)
        Title = $"{ProductInfo.Name} — {UiStrings.Get("Window_Language")}";
        AppWindow.Resize(new SizeInt32(460, 640));
        AppWindow.SetIcon("Assets/AppIcon.ico");

        TextFlow.ForUi(IntroText, PackSetupText.Intro);
        PopulateChoices();
        ShowSetupState();
        _ = RefreshInstalledAsync();
        _setup.Changed += OnSetupChanged;
        Closed += OnWindowClosed;
    }

    private void PopulateChoices()
    {
        _populating = true;
        LanguageList.ItemsSource = _rows;
        bool known = AppLanguages.TryParse(_settings.Language, out AppLanguage current);
        LanguageList.SelectedItem = known ? _rows.First(r => r.Choice.Language == current) : null;
        _populating = false;
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populating || LanguageList.SelectedItem is not LanguageRowItem row)
        {
            return;
        }

        _settings.Language = AppLanguages.Code(row.Choice.Language);
        UiStrings.ApplyLanguage(_settings.Language);
        // Restart only when the language differs from the one the window opened with (picking it back is a no-op).
        _languageChanged = !string.Equals(_settings.Language, _initialLanguage, StringComparison.OrdinalIgnoreCase);
        TextFlow.ForUi(IntroText, PackSetupText.Intro);
        StartSetup();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _closed = true;
        _setup.Changed -= OnSetupChanged;
        if (_languageChanged)
        {
            _restart(); // the other windows were built in the old language; the settings are already stored
        }
    }

    // Retry sits only on the run row, so the row it is on is the language to retry.
    private void OnRetryClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LanguageRowItem row } && !string.Equals(AppLanguages.Code(row.Choice.Language), _settings.Language, StringComparison.OrdinalIgnoreCase))
        {
            _settings.Language = AppLanguages.Code(row.Choice.Language);
        }

        StartSetup();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private async void StartSetup()
    {
        _escapedFailure = false; // the strip goes away when a try starts
        _runLanguage = _setup.CurrentLanguage;
        _starting = true;
        ShowSetupStateIfOpen();
        try
        {
            await _setup.StartAsync();
            _starting = false;
            ShowSetupStateIfOpen();
            CloseWhenReady();
        }
        catch (Exception ex)
        {
            // An exception escaping the installer is not one of its result paths, so it is not counted there.
            _fallbacks.Report(FallbackArea.Pack, FallbackReason.PackFailed, _settings.Language); // Slice 273 part D
            _log.Error("voice pack setup failed", null, ex);
            _escapedFailure = true;
            _starting = false;
            ShowSetupStateIfOpen();
        }
    }

    private void OnSetupChanged(object? sender, EventArgs e) => _dispatcher.TryEnqueue(OnSetupChangedOnUi);

    // All state the edge detection reads or writes is touched here, on the dispatcher thread.
    private void OnSetupChangedOnUi()
    {
        ApplyPublish(_setup.State);
        PackSetupState state = _setup.State;
        bool becameReady = state == PackSetupState.Ready && _lastState != PackSetupState.Ready;
        _lastState = state;
        ShowSetupStateIfOpen();
        if (becameReady && !_closed)
        {
            _ = RefreshInstalledAsync(); // a finished download turns its row to installed; once per change to Ready
        }
    }

    // A live state (Resolving, Downloading, Ready) ends the start and clears an escaped failure; a stale Failed or Idle changes nothing.
    private void ApplyPublish(PackSetupState state)
    {
        RunFlags flags = RunStateRule.OnPublish(new RunFlags(_starting, _escapedFailure), state);
        _starting = flags.Starting;
        _escapedFailure = flags.EscapedFailure;
    }

    private void ShowSetupStateIfOpen()
    {
        if (!_closed)
        {
            ShowSetupState();
        }
    }

    // Closes only after a run this window started (StartAsync returns once its language is served)
    // ends with the natural voice ready. A background run or a re-published state never closes it.
    private void CloseWhenReady()
    {
        if (!_closed && _setup.State == PackSetupState.Ready)
        {
            Close();
        }
    }

    // Mirrors the setup state: progress while downloading and Retry when it failed. The list is locked
    // while a download is in flight, because a second choice could not start another run until the
    // first one finishes.
    private void ShowSetupState()
    {
        RunView run = CurrentRun();
        foreach (LanguageRowItem row in _rows)
        {
            row.Show(PackSetupText.ForRow(IsRunRow(row), run, IsInstalled(row)));
        }

        DownloadBar.Value = run.Progress;
        DownloadBar.Visibility = run.State == PackSetupState.Downloading ? Visibility.Visible : Visibility.Collapsed;
        LanguageList.IsEnabled = run.State is not (PackSetupState.Resolving or PackSetupState.Downloading);
    }

    // A run just started shows as Resolving until it publishes: the installer still holds the previous run's state (maybe Failed) until then.
    private RunView CurrentRun()
    {
        return RunStateRule.Decide(new RunFlags(_starting, _escapedFailure)) switch
        {
            RunShown.Resolving => new RunView(PackSetupState.Resolving, 0, null, false),
            RunShown.EscapedFailure => new RunView(PackSetupState.Failed, 0, null, true),
            _ => new RunView(_setup.State, _setup.Progress, _setup.FailureKind, _setup.CanRetry),
        };
    }

    private bool IsRunRow(LanguageRowItem row) =>
        string.Equals(AppLanguages.Code(row.Choice.Language), _runLanguage, StringComparison.OrdinalIgnoreCase);

    // null = not read yet or the read failed: the row claims nothing.
    private bool? IsInstalled(LanguageRowItem row) => _installed?.Contains(AppLanguages.Code(row.Choice.Language));

    // Reads which language packs are installed (one read of the pack folders, off the UI thread) and redraws the rows; run at open and when a download ends.
    private async Task RefreshInstalledAsync()
    {
        int scan = ++_installedScan; // UI thread only
        string[] codes = [.. _rows.Select(r => AppLanguages.Code(r.Choice.Language))];
        HashSet<string>? read = await Task.Run(() => ScanInstalled(codes)); // null = the scan failed: keep the last good set
        _dispatcher.TryEnqueue(() =>
        {
            if (scan != _installedScan)
            {
                return; // a newer scan was started; its result is the one to show
            }

            _installed = read ?? _installed;
            ShowSetupStateIfOpen();
        });
    }

    private HashSet<string>? ScanInstalled(string[] codes)
    {
        try
        {
            return _setup.InstalledLanguages(codes);
        }
        catch (Exception ex)
        {
            _fallbacks.Report(FallbackArea.Pack, FallbackReason.PackFailed, _settings.Language); // the rows then show no "not downloaded" line, as before the scan
            _log.Error("installed voice pack scan failed", null, ex);
            return null;
        }
    }
}

/// <summary>One row of the language list (Slice 373, Missing States mock RT2): a language with its second line, error line and Retry, updated in place.</summary>
public sealed class LanguageRowItem(LanguageChoice choice) : INotifyPropertyChanged
{
    private string _detail = string.Empty;
    private string _errorText = string.Empty;
    private bool _showRetry;

    public event PropertyChangedEventHandler? PropertyChanged;

    public LanguageChoice Choice { get; } = choice;

    public string Name => Choice.NativeName;

    public string AutomationId => $"LanguageRow_{AppLanguages.Code(Choice.Language)}";

    public string Detail => _detail;

    public string ErrorText => _errorText;

    public Visibility DetailVisibility => _detail.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorVisibility => _errorText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RetryVisibility => _showRetry ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The row's UI Automation name (the language's own name), as <c>DisplayMemberPath</c> gave it before the row template.</summary>
    public override string ToString() => Name;

    internal void Show(RowText text)
    {
        _detail = text.Detail;
        _errorText = text.Error;
        _showRetry = text.ShowRetry;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty)); // an empty name refreshes every bound property
    }
}
