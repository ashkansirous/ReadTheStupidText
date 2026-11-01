using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared;

namespace ReadTheStupidText.Tests;

// PackDownloadHost is static state: these tests share it, so they run one after another and put back what they change.
[Collection("PackDownloadHost")]
public class PackDownloadHostLabelTests : IDisposable
{
    private const long OneMegabyte = 1_000_000; // StorytellerOffer.Megabytes counts decimal megabytes

    private readonly Func<string?> _currentLanguage = PackDownloadHost.CurrentLanguage;
    private readonly Func<string, string> _languageName = PackDownloadHost.LanguageName;

    public PackDownloadHostLabelTests()
    {
        PackDownloadHost.LanguageName = code => code.ToUpperInvariant();
        PackDownloadHost.CurrentLanguage = () => "fr";
        PackDownloadHost.NoteCatalog(
        [
            new PackInfo("piper-fr", ["fr"], PackKind.Voice, "1", 124 * OneMegabyte, "x", "MIT", "https://example.test/a"),
            new PackInfo("supertonic-3", ["en", "fr", "ja"], PackKind.Voice, "1", 63 * OneMegabyte, "x", "MIT", "https://example.test/b"),
            new PackInfo("multi", ["de", "nl"], PackKind.Voice, "1", 10 * OneMegabyte, "x", "MIT", "https://example.test/c"),
            new PackInfo("ocr-fr", ["fr"], PackKind.Ocr, "1", 10 * OneMegabyte, "x", "MIT", "https://example.test/d"),
        ]);
    }

    public void Dispose()
    {
        PackDownloadHost.CurrentLanguage = _currentLanguage;
        PackDownloadHost.LanguageName = _languageName;
        PackDownloadHost.NoteCatalog([]);
        PackDownloadHost.NamedNotificationText = null;
        PackDownloadHost.PausedNotifier = null;
    }

    [Fact]
    public void A_single_language_voice_pack_is_named_with_its_size_in_megabytes()
    {
        Assert.Equal(new PackLabel("FR", 124), PackDownloadHost.LabelOf("piper-fr"));
    }

    [Fact]
    public void A_multilingual_pack_is_named_by_the_apps_language()
    {
        Assert.Equal(new PackLabel("FR", 63), PackDownloadHost.LabelOf("supertonic-3"));
    }

    [Fact]
    public void A_multilingual_pack_the_app_is_not_set_to_is_not_named_at_random()
    {
        Assert.Null(PackDownloadHost.LabelOf("multi"));
    }

    [Fact]
    public void A_scan_pack_or_an_unknown_pack_has_no_label()
    {
        Assert.Null(PackDownloadHost.LabelOf("ocr-fr"));
        Assert.Null(PackDownloadHost.LabelOf("nothing"));
    }

    [Fact]
    public void The_download_text_is_the_named_one_when_the_pack_can_be_named_else_the_plain_status_line()
    {
        PackDownloadHost.NamedNotificationText = (label, fraction) => $"Getting {label.Language} {label.Megabytes} {(int)(fraction * 100)}";

        Assert.Equal("Getting FR 124 45", PackDownloadHost.DownloadingText("piper-fr", 0.45));
        Assert.Equal(PackDownloadHost.NotificationText(0.45), PackDownloadHost.DownloadingText("ocr-fr", 0.45));
        Assert.Equal(PackDownloadHost.NotificationText(0.45), PackDownloadHost.DownloadingText(null, 0.45));
    }

    [Fact]
    public void The_default_paused_line_gives_the_name_size_and_percent_when_known()
    {
        Assert.Equal("FR voice (124 MB) · 45% done", PackDownloadHost.PausedNotificationText(PackDownloadHost.LabelOf("piper-fr"), 0.45));
        Assert.Equal("FR voice (124 MB)", PackDownloadHost.PausedNotificationText(PackDownloadHost.LabelOf("piper-fr"), null));
    }

    [Fact]
    public void Paused_and_cleared_reach_the_platform_notifier_and_a_throwing_notifier_is_swallowed()
    {
        var notifier = new RecordingNotifier();
        PackDownloadHost.PausedNotifier = notifier;

        PackDownloadHost.Paused("piper-fr", 0.45);
        PackDownloadHost.ClearPaused("piper-fr");
        notifier.Throw = true;
        PackDownloadHost.Paused("piper-fr", null);
        PackDownloadHost.ClearPaused("piper-fr");

        Assert.Equal(["post piper-fr 0.45", "clear piper-fr", "post piper-fr -", "clear piper-fr"], notifier.Calls);
    }

    private sealed class RecordingNotifier : IPackPausedNotifier
    {
        public List<string> Calls { get; } = [];

        public bool Throw { get; set; }

        public void Post(string packId, double? fraction, string reason)
        {
            Calls.Add($"post {packId} {fraction?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "-"}");
            ThrowIfAsked();
        }

        public void Clear(string packId)
        {
            Calls.Add($"clear {packId}");
            ThrowIfAsked();
        }

        private void ThrowIfAsked()
        {
            if (Throw)
            {
                throw new InvalidOperationException();
            }
        }
    }
}
