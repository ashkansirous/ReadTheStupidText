using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Resources.Strings;

namespace ReadTheStupidText.Mobile;

// Slice 217 (mock R15): the quiet "A new version is ready" card above the text box. It shows only while a newer release exists, the person has not
// closed it for that version, and nothing is being read; Get it opens the store page, the cross hides it until the next latestVersion.
public partial class ReaderPage
{
    private void InitUpdateCard()
    {
        TextFlow.ForUi(UpdateCardLabel, AppResources.Update_Available);
        TextFlow.ForUi(UpdateGetLabel, AppResources.Update_Get);
        SemanticProperties.SetDescription(UpdateGetButton, AppResources.Update_Get);
        SemanticProperties.SetDescription(UpdateCloseButton, AppResources.Update_Dismiss);
        RefreshUpdateCard();
    }

    // Subscribed while the page is on screen, next to the reader's own handlers; a fetch that brings a newer release, a closed card and a read
    // starting or ending all re-decide whether the card shows.
    private void AttachUpdateCard()
    {
        AppUpdate.Shared.Changed += OnUpdateChanged;
        _reader.StateChanged += OnReaderStateForUpdateCard;
        RefreshUpdateCard();
    }

    private void DetachUpdateCard()
    {
        AppUpdate.Shared.Changed -= OnUpdateChanged;
        _reader.StateChanged -= OnReaderStateForUpdateCard;
    }

    private void OnUpdateChanged(object? sender, EventArgs e) => RefreshUpdateCard();

    private void OnReaderStateForUpdateCard(object? sender, PlaybackState state) => MainThread.BeginInvokeOnMainThread(RefreshUpdateCard);

    private void RefreshUpdateCard()
    {
        bool show = AppUpdate.Shared.ShowAvailableCard && _reader.State != PlaybackState.Playing;
        if (UpdateCard.IsVisible == show)
        {
            return;
        }

        UpdateCard.IsVisible = show;
#if DEBUG
        ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"UPDATE_CARD {(show ? "shown" : "hidden")}"); // the scripted check asserts on this line
#endif
    }

    private void OnUpdateGetTapped(object? sender, TappedEventArgs e) => _ = AppUpdate.OpenStoreAsync();

    private void OnUpdateCloseTapped(object? sender, TappedEventArgs e) => AppUpdate.Shared.DismissAvailable();
}
