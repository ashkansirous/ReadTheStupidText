using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText_App;

/// <summary>
/// The panel's "Help improve the app" switch (Slice 140, Decision 143, Batch 29 frame W2): whether anonymous usage counts may be sent.
/// On by default; turning it off also forgets the counts waiting to be sent. The recorder is the app's one <see cref="IUsageRecorder"/>.
/// </summary>
public sealed partial class ControlPanelWindow
{
    private IUsageRecorder? _usage;

    // Runs in the constructor; _refreshing is set around the load, so loading the stored value does not echo back as a change.
    private void InitUsageSwitch()
    {
        _usage = ((App)Application.Current).Services.GetService<IUsageRecorder>();
        if (_usage is null)
        {
            CountsRow.Visibility = Visibility.Collapsed;
            return;
        }

        bool wasRefreshing = _refreshing;
        _refreshing = true;
        CountsSwitch.IsOn = _usage.Enabled;
        _refreshing = wasRefreshing;
    }

    private void OnCountsToggled(object sender, RoutedEventArgs e)
    {
        if (_refreshing || _usage is null)
        {
            return;
        }

        _usage.Enabled = CountsSwitch.IsOn;
    }
}
