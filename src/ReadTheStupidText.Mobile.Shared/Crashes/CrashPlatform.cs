namespace ReadTheStupidText.Mobile.Shared.Crashes;

/// <summary>
/// The per-platform part of crash reporting (Slice 227), dispatched with the same <c>#if</c> blocks the other Mobile.Shared helpers use: the
/// system's own crash history (Android exit reasons, iOS MetricKit). Windows has neither (the tray app and the Windows head
/// are not part of Slice 227), so it does nothing there. Neither phone platform has an extra managed hook: <c>AppDomain.UnhandledException</c> (installed by the reporter)
/// fires only for exceptions nothing handled, while Android's <c>UnhandledExceptionRaiser</c> and iOS's <c>MarshalManagedException</c> also fire for ones that are handled later.
/// </summary>
internal static class CrashPlatform
{
    /// <summary>The crashes the system recorded since <paramref name="lastSeen"/> and not seen before (Android: exit reasons; iOS delivers its MetricKit ones through <see cref="StartNativeDelivery"/>).</summary>
    public static IReadOnlyList<NativeCrash> ReadHistory(DateTimeOffset lastSeen)
    {
#if ANDROID
        return AndroidCrashHooks.ReadExitReasons(lastSeen);
#else
        return [];
#endif
    }

    /// <summary>Starts the platform's own delivery of crash diagnostics (iOS MetricKit); <paramref name="deliver"/> is called on a background thread.</summary>
    public static void StartNativeDelivery(Action<NativeCrash> deliver)
    {
#if IOS
        IosCrashHooks.StartMetricKit(deliver);
#endif
    }
}
