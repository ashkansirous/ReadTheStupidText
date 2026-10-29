using Xamarin.Google.MLKit.Common.SDKInternal;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Starts Google ML Kit by hand (the 2026-10-29 ML Kit-only-for-grown-ups fix). ML Kit's own <c>MlKitInitProvider</c> would start it when the process starts; Dino Reader (a children's
/// app) removes that provider in its manifest so nothing of ML Kit runs until a grown-up asks for a translation or a scan. Whatever is about to use ML Kit
/// calls <see cref="EnsureInitialized"/> first. Read The Stupid Text keeps the provider, so there the call finds ML Kit already started and does nothing.
/// </summary>
public static class MlKitInit
{
    /// <summary>Starts ML Kit if it has not started yet; safe to call from any thread, any number of times.</summary>
    public static void EnsureInitialized()
    {
        MlKitContext.InitializeIfNeeded(global::Android.App.Application.Context);
#if DEBUG
        Testing.TestHookLog.Write("MLKIT_INIT");
#endif
    }
}
