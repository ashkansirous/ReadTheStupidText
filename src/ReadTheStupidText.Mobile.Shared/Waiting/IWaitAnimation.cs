namespace ReadTheStupidText.Mobile.Shared.Waiting;

/// <summary>
/// An app's own animation for the wait overlay (Slice 143, Decision 149): the overlay starts it when it shows and stops it
/// when it hides, so nothing keeps drawing behind a hidden overlay. A view that implements this goes in
/// <see cref="WaitOverlayStyle.CreateAnimation"/>.
/// </summary>
public interface IWaitAnimation
{
    void Start();

    void Stop();
}
