namespace DinoReader;

/// <summary>
/// Slice 390: the platform store's own news that the account's purchases changed while the app was open (iOS: an Ask to Buy approval, a renewal, a refund or a purchase made on another device, seen by the StoreKit 2
/// transaction listener). <see cref="BookPurchases"/> listens and reads the store again; a platform with no such listener never raises it.
/// </summary>
public static class PurchaseUpdates
{
    /// <summary>Raised on a thread-pool thread, never on the platform's native thread; subscribers must not throw (<see cref="Raise"/> catches anyway).</summary>
    public static event Action? StoreChanged;

    private static int s_missed;

    /// <summary>True once when an update was raised while nobody listened yet (the listener runs from launch, the first read subscribes later); the new subscriber then marks its last read stale.</summary>
    public static bool TakeMissed() => Interlocked.Exchange(ref s_missed, 0) == 1;

    public static void Raise()
    {
        try
        {
            if (StoreChanged is null)
            {
                Volatile.Write(ref s_missed, 1);
            }

            StoreChanged?.Invoke();
        }
        catch (Exception ex)
        {
            // no-fallback: a failing subscriber only means this update is not re-read now; the next refresh reads the store's list again
            PurchaseTrace.Line($"store update subscriber failed: {ex.GetType().Name}");
        }
    }
}
