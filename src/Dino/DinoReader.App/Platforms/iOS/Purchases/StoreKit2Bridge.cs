using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace DinoReader;

/// <summary>
/// The C# end of <c>Platforms/iOS/StoreKit2</c> (Slice 390): calls the C functions of <c>libDinoStoreKit.a</c> (linked into the app as a static library) through <c>DllImport("__Internal")</c>, passing the callback as a
/// function pointer. Each call hands over a request id and the one callback; the answer is a JSON string that completes that request's task. Answers come on a Swift thread, so completing is all the callback does.
/// <see cref="Load"/> is null when the library is not in the app (a build that skipped the XCFramework step): the caller reports a failed store, never throws.
/// </summary>
internal sealed unsafe class StoreKit2Bridge
{
    private static readonly object LoadGate = new();
    private static readonly ConcurrentDictionary<int, TaskCompletionSource<string>> Waiting = new();
    private static StoreKit2Bridge? s_bridge;
    private static bool s_tried;
    private static int s_lastRequest;

    // The Swift exports are linked into the app as a static library (DinoReader.App.csproj), so each one is an explicit __Internal reference the linker resolves at build time.
    [DllImport("__Internal", EntryPoint = "dino_sk_can_pay")]
    private static extern int NativeCanPay();

    [DllImport("__Internal", EntryPoint = "dino_sk_products")]
    private static extern void NativeProducts(byte* idsJson, int requestId, delegate* unmanaged<int, byte*, void> callback);

    [DllImport("__Internal", EntryPoint = "dino_sk_buy")]
    private static extern void NativeBuy(byte* productId, int requestId, delegate* unmanaged<int, byte*, void> callback);

    [DllImport("__Internal", EntryPoint = "dino_sk_entitlements")]
    private static extern void NativeEntitlements(byte* idsJson, int requestId, delegate* unmanaged<int, byte*, void> callback);

    [DllImport("__Internal", EntryPoint = "dino_sk_restore")]
    private static extern void NativeRestore(int requestId, delegate* unmanaged<int, byte*, void> callback);

    [DllImport("__Internal", EntryPoint = "dino_sk_redeem")]
    private static extern void NativeRedeem(int requestId, delegate* unmanaged<int, byte*, void> callback);

    [DllImport("__Internal", EntryPoint = "dino_sk_manage_subscriptions")]
    private static extern void NativeManageSubscriptions(int requestId, delegate* unmanaged<int, byte*, void> callback);

    [DllImport("__Internal", EntryPoint = "dino_sk_start_updates")]
    private static extern void NativeStartUpdates(delegate* unmanaged<int, byte*, void> callback);

    /// <summary>Raised with the product id when the transaction listener sees an update (a renewal, an approved Ask to Buy, a purchase made elsewhere); on a Swift thread.</summary>
    public static event Action<string>? ProductUpdated;

    private StoreKit2Bridge()
    {
    }

    /// <summary>The loaded wrapper, started once; null when the framework or one of its functions is missing.</summary>
    public static StoreKit2Bridge? Load()
    {
        lock (LoadGate)
        {
            if (!s_tried)
            {
                s_tried = true;
                s_bridge = TryCreate();
            }

            return s_bridge;
        }
    }

    public bool CanMakePayments => NativeCanPay() == 1;

    // A UTF-8, NUL-terminated copy of the input lives for the call only: the Swift side copies what it needs before it returns.
    public Task<string> ProductsAsync(IReadOnlyCollection<string> productIds)
    {
        (int id, Task<string> task) = Register();
        fixed (byte* text = Utf8(JsonSerializer.Serialize(productIds)))
        {
            NativeProducts(text, id, &OnAnswer);
        }

        return task;
    }

    public Task<string> BuyAsync(string productId)
    {
        (int id, Task<string> task) = Register();
        fixed (byte* text = Utf8(productId))
        {
            NativeBuy(text, id, &OnAnswer);
        }

        return task;
    }

    /// <summary>Silent read of what the Apple ID owns; <paramref name="planIds"/> are the subscription plans whose newest transaction is wanted even when expired.</summary>
    public Task<string> EntitlementsAsync(IReadOnlyCollection<string> planIds)
    {
        (int id, Task<string> task) = Register();
        fixed (byte* text = Utf8(JsonSerializer.Serialize(planIds)))
        {
            NativeEntitlements(text, id, &OnAnswer);
        }

        return task;
    }

    /// <summary>The Restore purchases button: Apple may ask for the password.</summary>
    public Task<string> SyncAsync()
    {
        (int id, Task<string> task) = Register();
        NativeRestore(id, &OnAnswer);
        return task;
    }

    public Task<string> RedeemAsync()
    {
        (int id, Task<string> task) = Register();
        NativeRedeem(id, &OnAnswer);
        return task;
    }

    /// <summary>Apple's Manage Subscriptions sheet; the answer comes when the person closes it.</summary>
    public Task<string> ManageSubscriptionsAsync()
    {
        (int id, Task<string> task) = Register();
        NativeManageSubscriptions(id, &OnAnswer);
        return task;
    }

    // With __Internal a missing library is a link error, so on a normal build none of this throws. The catch only keeps a symbol the runtime cannot find (for example a trimmed one) from crashing the app at launch:
    // there is then no bridge, every caller answers a failed store, and BookPurchases reports it. The updates listener starts here too, inside the same guard, because this is called from the launch path.
    private static StoreKit2Bridge? TryCreate()
    {
        try
        {
            _ = NativeCanPay();
            var bridge = new StoreKit2Bridge();
            bridge.StartUpdates();
            return bridge;
        }
        catch (Exception ex)
        {
            // no-fallback: see the comment above; any failure here (a missing symbol or library, a native fault) means no bridge, never a crash at launch; the trace line is the report
            PurchaseTrace.Line($"storekit2 unavailable: {ex.GetType().Name}");
            return null;
        }
    }

    private void StartUpdates() => NativeStartUpdates(&OnAnswer);

    private static byte[] Utf8(string input) => Encoding.UTF8.GetBytes(input + "\0");

    private static (int Id, Task<string> Task) Register()
    {
        int id = Interlocked.Increment(ref s_lastRequest);
        var done = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Waiting[id] = done;
        return (id, done.Task);
    }

    /// <summary>Drops the waiting entry of a request the caller gave up on (a timeout or cancel); a late answer is then ignored.</summary>
    public static void Abandon(Task<string> request)
    {
        foreach (KeyValuePair<int, TaskCompletionSource<string>> entry in Waiting)
        {
            if (entry.Value.Task == request)
            {
                Waiting.TryRemove(entry.Key, out _);
            }
        }
    }

    // Runs on a Swift thread, and an exception must never unwind into native code: everything is caught here. The pointer is only valid during the call, so the string is copied first.
    [UnmanagedCallersOnly]
    private static void OnAnswer(int requestId, byte* json)
    {
        try
        {
            string text = json == null ? string.Empty : Marshal.PtrToStringUTF8((IntPtr)json) ?? string.Empty;
            if (requestId > 0)
            {
                CompleteRequest(requestId, text);
            }
            else
            {
                _ = Task.Run(() => HandleUpdate(text)); // off the native thread: subscribers, the log and a re-read run on the thread pool
            }
        }
        catch (Exception)
        {
            // no-fallback: nothing may unwind into Swift; a lost answer ends as that request's own timeout or cancel, which the caller already reports as a failed store
        }
    }

    private static void CompleteRequest(int requestId, string text)
    {
        if (Waiting.TryRemove(requestId, out TaskCompletionSource<string>? done))
        {
            done.TrySetResult(text);
        }
    }

    private static void HandleUpdate(string text)
    {
        try
        {
            if (DinoReader.Core.StoreKit2Answers.UpdatedProduct(text) is { } product)
            {
                PurchaseTrace.Line($"storekit2 update product={product}");
                ProductUpdated?.Invoke(product);
                PurchaseUpdates.Raise();
            }
        }
        catch (Exception ex)
        {
            // no-fallback: a failing subscriber only means this update is not shown now; the next refresh reads Apple's list again
            PurchaseTrace.Line($"storekit2 update handler failed: {ex.GetType().Name}");
        }
    }
}
