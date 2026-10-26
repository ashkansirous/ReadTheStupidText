using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText_App;

/// <summary>What the wait indicator shows: the stage line, an optional second line, a download percent when known, and whether Cancel is offered.</summary>
public sealed record WaitDisplay(string Line, string? Detail, int? Percent, bool CanCancel);

/// <summary>
/// Drives the Windows wait indicator (Slice 141, Decision 146). A read is waiting from <see cref="ReadAloudService.IsGeneratingAudio"/>
/// turning true until its first audio; the pure <see cref="WaitGate"/> keeps anything from showing for the first 500 ms. Once due, the
/// stage (pack download, translating, preparing the voice) is shown in the tray flyout, or in the control panel's row when the panel is
/// open (<see cref="Changed"/>), never both. One instance lives for the app; the panel creates it at startup. UI thread only.
/// </summary>
public sealed class WaitFlyoutController
{
    private static WaitFlyoutController? _instance;

    private readonly ReadAloudService _readAloud;
    private readonly ISettingsStore? _settings;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly DispatcherQueueTimer _timer;
    private readonly WaitGate _gate = new();
    private readonly WaitFlyoutWindow _flyout = new();
    private readonly Dictionary<string, TranslationPackProgress> _downloads = new();
    private bool _textReady;
    private int? _readId;

    private WaitFlyoutController(ReadAloudService readAloud, Action? cancelRead)
    {
        _readAloud = readAloud;
        _settings = ((App)Application.Current).Services.GetService<ISettingsStore>();
        CancelRead = cancelRead;
        _timer = _dispatcher.CreateTimer();
        _timer.IsRepeating = false;
        _timer.Tick += (_, _) => Publish();
        _flyout.CancelRequested += (_, _) => CancelRead?.Invoke();
        _readAloud.GeneratingChanged += (_, _) => _dispatcher.TryEnqueue(Refresh);
        _readAloud.ReadTextChanged += (_, _) =>
        {
            int? readId = _readAloud.CurrentReadId; // taken now, on the raising read: a queued call must not mark a newer read's text ready
            _dispatcher.TryEnqueue(() => OnReadText(readId));
        };
        _readAloud.WaitingOnEarlyVoiceChanged += (_, _) => _dispatcher.TryEnqueue(Refresh); // translation is done: the voice is what is left (Slice 142)
        _readAloud.TranslationPackProgress += (_, progress) => _dispatcher.TryEnqueue(() => OnPackProgress(progress));
    }

    /// <summary>The one controller, created on first use on the UI thread. <paramref name="cancelRead"/> stops the waiting read; without it no Cancel is offered.</summary>
    public static WaitFlyoutController Get(ReadAloudService readAloud, Action? cancelRead) =>
        _instance ??= new WaitFlyoutController(readAloud, cancelRead);

    /// <summary>Stops the read that is waiting; null when the service offers no such call.</summary>
    public Action? CancelRead { get; }

    /// <summary>Whether the control panel is open (it then shows the wait itself); set by the panel.</summary>
    public Func<bool>? IsPanelOpen { get; set; }

    /// <summary>What is showing now, null when nothing is (no wait, or still inside the first 500 ms).</summary>
    public WaitDisplay? Current { get; private set; }

    /// <summary>Raised on the UI thread whenever <see cref="Current"/> changes.</summary>
    public event EventHandler? Changed;

    /// <summary>The panel opened or closed: moves the wait between its row and the flyout.</summary>
    public void PanelVisibilityChanged() => ApplyToFlyout();

    private void Refresh()
    {
        bool generating = _readAloud.IsGeneratingAudio;
        if (generating && _gate.IsActive && _readAloud.CurrentReadId != _readId)
        {
            EndWait(); // a new read replaced the waiting one inside one dispatch: it earns its own 500 ms
        }

        if (generating && !_gate.IsActive)
        {
            BeginWait();
        }
        else if (!generating && _gate.IsActive)
        {
            EndWait();
        }
        else if (generating)
        {
            RestageAndPublish();
        }
    }

    private void BeginWait()
    {
        _textReady = false;
        _readId = _readAloud.CurrentReadId;
        _gate.Begin(CurrentStage(), DateTimeOffset.UtcNow);
        ArmTimer();
    }

    private void EndWait()
    {
        _gate.End(DateTimeOffset.UtcNow);
        _timer.Stop();
        Publish();
    }

    private void ArmTimer()
    {
        _timer.Interval = _gate.TimeUntilVisible(DateTimeOffset.UtcNow) + TimeSpan.FromMilliseconds(1);
        _timer.Start();
    }

    private void OnReadText(int? readId)
    {
        if (readId != _readId)
        {
            return;
        }

        _textReady = true;
        if (_gate.IsActive)
        {
            RestageAndPublish();
        }
    }

    private void OnPackProgress(TranslationPackProgress progress)
    {
        string key = $"{progress.Source}>{progress.Target}";
        if (progress.State == TranslationPackState.Downloading)
        {
            _downloads[key] = progress;
        }
        else
        {
            _downloads.Remove(key);
        }

        if (_gate.IsActive)
        {
            RestageAndPublish();
        }
    }

    private WaitStage CurrentStage() =>
        ReadWaitStages.Pick(DownloadForThisRead() is not null, _readAloud.TranslateEnabled && !_textReady && !_readAloud.WaitingOnEarlyVoice);

    // A read does not wait for a pack download: it reads the original at once and the pack arrives in the background. The download
    // stage therefore shows only for a download of this read's own pair while the read is still resolving its text, never for another
    // pair's download (for example the one started by switching Translate on).
    private TranslationPackProgress? DownloadForThisRead()
    {
        if (!_readAloud.TranslateEnabled || _textReady || _settings is null)
        {
            return null;
        }

        foreach (TranslationPackProgress download in _downloads.Values)
        {
            if (IsThisReadsPair(download))
            {
                return download;
            }
        }

        return null;
    }

    private bool IsThisReadsPair(TranslationPackProgress download)
    {
        bool sameTarget = string.Equals(download.Target, _settings!.Language, StringComparison.OrdinalIgnoreCase);
        string? source = _settings.TranslateSourceLanguage;
        bool sameSource = string.IsNullOrEmpty(source) || string.Equals(download.Source, source, StringComparison.OrdinalIgnoreCase);
        return sameTarget && sameSource;
    }

    private void RestageAndPublish()
    {
        _gate.Begin(CurrentStage(), DateTimeOffset.UtcNow); // a second Begin only changes the stage: the clock keeps running
        Publish();
    }

    private void Publish()
    {
        WaitDisplay? next = _gate.IsActive && _gate.IsVisible(DateTimeOffset.UtcNow) ? Compose(_gate.Stage) : null;
        if (next == Current)
        {
            return;
        }

        Current = next;
        ApplyToFlyout();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyToFlyout()
    {
        if (Current is { } display && IsPanelOpen?.Invoke() != true)
        {
            _flyout.ShowFor(display);
        }
        else
        {
            _flyout.HideFlyout();
        }
    }

    private WaitDisplay Compose(WaitStage stage)
    {
        bool canCancel = CancelRead is not null;
        return stage switch
        {
            WaitStage.DownloadingModel => ComposeDownload(canCancel),
            WaitStage.Translating => new WaitDisplay(UiStrings.Get("Wait_Translating"), PairText(_settings?.TranslateSourceLanguage, _settings?.Language), null, canCancel),
            _ => new WaitDisplay(UiStrings.Get("Wait_PreparingVoice"), null, null, canCancel),
        };
    }

    // The download names the language that is arriving (the pair's non-English side) and a percent, since the installer reports a fraction.
    private WaitDisplay ComposeDownload(bool canCancel)
    {
        TranslationPackProgress? download = DownloadForThisRead();
        if (download is null)
        {
            return new WaitDisplay(UiStrings.Get("Wait_PreparingVoice"), null, null, canCancel);
        }

        string code = download.Target == SupertonicVoiceTable.EnglishCode ? download.Source : download.Target;
        int percent = ReadWaitStages.Percent(download.Fraction);
        string line = UiStrings.Format("Wait_DownloadingPercent", LanguageChoices.NativeName(code), percent);
        return new WaitDisplay(line, PairText(download.Source, download.Target), percent, canCancel);
    }

    private static string? PairText(string? source, string? target) =>
        string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)
            ? null
            : UiStrings.Format("Wait_Pair", LanguageChoices.NativeName(source), LanguageChoices.NativeName(target));
}
