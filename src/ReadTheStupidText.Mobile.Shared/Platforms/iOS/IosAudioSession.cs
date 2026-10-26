using AVFoundation;
using Foundation;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// The shared <see cref="AVAudioSession"/> setup for every iOS reader (Slice 49): the playback
/// category (so audio plays with the silent switch on), activation around a read, and the
/// interruption notification (a call, an alarm, Siri) that pauses and resumes a read.
/// </summary>
public static class IosAudioSession
{
    private static readonly object Gate = new();
    private static readonly HashSet<object> Owners = [];
    private static bool _deactivatePending;

    /// <summary>
    /// Takes the session for <paramref name="owner"/> (a reader). Returns false when iOS refused
    /// the category or the activation, so a caller can skip resuming into silence. Several
    /// owners can hold the session at once; it is released when the last one lets go.
    /// </summary>
    public static bool Activate(object owner)
    {
        lock (Gate)
        {
            Owners.Add(owner);
            _deactivatePending = false;
        }

        AVAudioSession session = AVAudioSession.SharedInstance();
        // Playback otherwise stays silent when the ringer switch is on; reading aloud must not.
        // The string overload is the one that takes just a category; the enum overloads need a
        // mode and options too, and add nothing here.
#pragma warning disable CS0618
        bool categorySet = session.SetCategory(AVAudioSession.CategoryPlayback, out NSError? categoryError);
#pragma warning restore CS0618
        bool active = session.SetActive(true, out NSError? activeError);
        Report("SetCategory", categorySet, categoryError);
        Report("SetActive", active, activeError);
        return categorySet && active;
    }

    /// <summary>
    /// Lets go of the session for <paramref name="owner"/>. It is deactivated (so other apps'
    /// music comes back) only when no other reader still holds it. A failed deactivation is
    /// remembered and retried by <see cref="RetryPendingDeactivate"/>.
    /// </summary>
    public static void Deactivate(object owner)
    {
        lock (Gate)
        {
            Owners.Remove(owner);
            if (Owners.Count > 0)
            {
                return;
            }
        }

        TryDeactivate();
    }

    /// <summary>
    /// Retries a deactivation that failed because audio was still running; call it when that
    /// audio has stopped (a synthesizer's cancel callback).
    /// </summary>
    public static void RetryPendingDeactivate()
    {
        bool pending;
        lock (Gate)
        {
            pending = _deactivatePending && Owners.Count == 0;
        }

        if (pending)
        {
            TryDeactivate();
        }
    }

    private static void TryDeactivate()
    {
        bool done = AVAudioSession.SharedInstance()
            .SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out NSError? error);
        lock (Gate)
        {
            _deactivatePending = !done;
        }

        // Expected while a player or utterance is still stopping; retried by the caller.
        Report("SetActive(false)", done, error);
    }

    private static void Report(string call, bool succeeded, NSError? error)
    {
        if (!succeeded)
        {
            System.Diagnostics.Debug.WriteLine($"AVAudioSession.{call} failed: {error?.LocalizedDescription}");
        }
    }

    /// <summary>
    /// Calls <paramref name="began"/> when the system takes the audio and
    /// <paramref name="ended"/> (with whether it says playback should resume) when it gives it
    /// back. Both run on the main thread. Dispose the result to stop observing.
    /// </summary>
    public static NSObject ObserveInterruptions(Action began, Action<bool> ended) =>
        AVAudioSession.Notifications.ObserveInterruption((_, e) =>
        {
            if (e.InterruptionType == AVAudioSessionInterruptionType.Began)
            {
                MainThread.BeginInvokeOnMainThread(began);
                return;
            }

            // Option is only meaningful on an Ended notification.
            bool shouldResume = e.Option.HasFlag(AVAudioSessionInterruptionOptions.ShouldResume);
            MainThread.BeginInvokeOnMainThread(() => ended(shouldResume));
        });
}
