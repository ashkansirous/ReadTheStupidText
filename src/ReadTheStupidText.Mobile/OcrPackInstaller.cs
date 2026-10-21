using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Downloads the OCR pack of the chosen language in the background, after the voice pack
/// (Slice 52, Decision 65). It is silent on purpose: until the pack arrives scans still work through
/// ML Kit, and a failure (offline, hash mismatch) is simply retried the next time the app starts
/// (the app calls <see cref="EnsureForCurrentLanguageAsync"/> at every start, whatever the voice
/// installer does), so no screen needs a state for it. The same start-up check installs a newer
/// version of an installed pack when the catalogue lists one; the old pack stays in use until the
/// new one has verified. Voice downloads go first: a run waits, after a short delay that lets the
/// voice installer begin, until the voice installer is no longer Resolving or Downloading. One download runs at a time; a
/// language change during a run is picked up by a second run for the new language when it ends, and
/// a language change while idle starts one straight away. The folder scan and the catalog fetch both
/// run off the calling (UI) thread, and the catalog reuses the voice installer's recent fetch.
/// </summary>
public sealed class OcrPackInstaller
{
    private static readonly TimeSpan CatalogMaxAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VoiceHeadStart = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan VoicePollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan VoiceMaxWait = TimeSpan.FromMinutes(30);

    private readonly IVoicePackService _packs;
    private readonly ISettingsStore _settings;
    private readonly VoicePackInstaller _voice;
    private int _running;

    public OcrPackInstaller(IVoicePackService packs, ISettingsStore settings, ILanguageChangeNotifier languages, VoicePackInstaller voice)
    {
        _packs = packs;
        _settings = settings;
        _voice = voice;
        languages.LanguageChanged += (_, _) => _ = EnsureForCurrentLanguageAsync();
    }

    public async Task EnsureForCurrentLanguageAsync()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            return;
        }

        string? handled = _settings.Language;
        try
        {
            await Task.Run(() => RunAsync(handled)).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }

        if (LanguageChangedSince(handled))
        {
            await EnsureForCurrentLanguageAsync().ConfigureAwait(false);
        }
    }

    private bool LanguageChangedSince(string? handled) =>
        _settings.Language is { } current && !string.Equals(current, handled, StringComparison.OrdinalIgnoreCase);

    private async Task RunAsync(string? language)
    {
        if (language is null)
        {
            return;
        }

        try
        {
            await WaitForVoiceDownloadsAsync().ConfigureAwait(false);
            await InstallAsync(language).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OCR pack install for '{language}' failed: {ex.Message}");
        }
    }

    // The voice pack is the one the user is waiting for, so OCR never competes with it for bandwidth.
    private async Task WaitForVoiceDownloadsAsync()
    {
        await Task.Delay(VoiceHeadStart).ConfigureAwait(false);
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (_voice.State is PackInstallState.Resolving or PackInstallState.Downloading && waited.Elapsed < VoiceMaxWait)
        {
            await Task.Delay(VoicePollInterval).ConfigureAwait(false);
        }
    }

    private async Task InstallAsync(string language)
    {
        InstalledVoicePack? installed = PackSelection.Serving(_packs.GetInstalledOcrPacks(), PackKind.Ocr, language).FirstOrDefault();
        PackCatalogResult catalog = await _packs.GetCatalogCachedAsync(CatalogMaxAge, CancellationToken.None).ConfigureAwait(false);
        if (catalog.Status != PackDownloadResult.Ok
            || OcrPackSelector.Select(catalog.Packs, language) is not { } pack
            || !PackVersions.ShouldInstall(pack, installed))
        {
            return;
        }

        PackDownloadResult result = await _packs.DownloadAsync(pack.Id, null, CancellationToken.None).ConfigureAwait(false);
        System.Diagnostics.Debug.WriteLine($"OCR pack '{pack.Id}': {result}");
    }
}
