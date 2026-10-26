using ReadTheStupidText.Mobile.Shared.Waiting;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Keeps a wait overlay in the colours of the theme now showing: it restyles when the overlay appears and follows a theme
/// change only while the overlay is on screen, so no page needs OnAppearing to be called and nothing stays subscribed.
/// </summary>
internal sealed class WaitThemeFollower
{
    private readonly WaitController _wait;
    private readonly WaitOverlay _overlay;
    private bool _following;

    public WaitThemeFollower(WaitController wait, WaitOverlay overlay)
    {
        _wait = wait;
        _overlay = overlay;
        _wait.ShownChanged += OnShownChanged;
    }

    /// <summary>Stops following the theme (the page is being swapped out); the next time the overlay shows it follows again.</summary>
    public void Detach()
    {
        SetFollowing(false);
    }

    private void OnShownChanged(bool shown)
    {
        if (shown)
        {
            _overlay.Restyle(ReaderPage.CreateWaitStyle());
        }

        SetFollowing(shown);
    }

    private void SetFollowing(bool follow)
    {
        if (follow == _following || Microsoft.Maui.Controls.Application.Current is not { } app)
        {
            return;
        }

        _following = follow;
        if (follow)
        {
            app.RequestedThemeChanged += OnThemeChanged;
            return;
        }

        app.RequestedThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        _overlay.Restyle(ReaderPage.CreateWaitStyle());
    }
}
