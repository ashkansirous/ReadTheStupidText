using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Foundation;

namespace DinoReader;

/// <summary>
/// The C# end of <c>Platforms/iOS/StoreKit2</c> (Slice 390): loads <c>DinoStoreKit.framework</c> from the app's Frameworks folder (the same way IosSherpaLibrary finds its framework) and calls its C functions through
/// function pointers. Each call hands over a request id and the one callback; the answer is a JSON string that completes that request's task. Answers come on a Swift thread, so completing is all the callback does.
/// <see cref="Load"/> is null when the framework is not in the app (a build that skipped the XCFramework step): the caller reports a failed store, never throws.
/// </summary>
internal sealed unsafe class StoreKit2Bridge
{
    private const string FrameworkName = "DinoStoreKit";

    private static readonly object LoadGate = new();
    private static readonly ConcurrentDictionary<int, TaskCompletionSource<string>> Waiting = new();
    private static StoreKit2Bridge? s_bridge;
    private static bool s_tried;
    private static int s_lastRequest;

    private readonly delegate* unmanaged<int> _canPay;
    private readonly delegate* unmanaged<byte*, int, delegate* unmanaged<int, byte*, void>, void> _products;
    private readonly delegate* unmanaged<byte*, int, delegate* unmanaged<int, byte*, void>, void> _buy;
    private readonly delegate* unmanaged<byte*, int, delegate* unmanaged<int, byte*, void>, void> _entitlements;
    private readonly delegate* unmanaged<int, delegate* unmanaged<int, byte*, void>, void> _restore;
    private readonly delegate* unmanaged<int, delegate* unmanaged<int, byte*, void>, void> _redeem;
    private readonly delegate* unmanaged<delegate* unmanaged<int, byte*, void>, void> _startUpdates;

    /// <summary>Raised with the product id when the transaction listener sees an update (a renewal, an approved Ask to Buy, a purchase made elsewhere); on a Swift thread.</summary>
    public static event Action<string>? ProductUpdated;

    private StoreKit2Bridge(IntPtr library)
    {
        _canPay = (delegate* unmanaged<int>)Export(library, "dino_sk_can_pay");
        _products = (delegate* unmanaged<byte*, int, delegate* unmanaged<int, byte*, void>, void>)Export(library, "dino_sk_products");
        _buy = (delegate* unmanaged<byte*, int, delegate* unmanaged<int, byte*, void>, void>)Export(library, "dino_sk_buy");
        _entitlements = (delegate* unmanaged<byte*, int, delegate* unmanaged<int, byte*, void>, void>)Export(library, "dino_sk_entitlements");
        _restore = (delegate* unmanaged<int, delegate* unmanaged<int, byte*, void>, void>)Export(library, "dino_sk_restore");
        _redeem = (delegate* unmanaged<int, delegate* unmanaged<int, byte*, void>, void>)Export(library, "dino_sk_redeem");
        _startUpdates = (delegate* unmanaged<delegate* unmanaged<int, byte*, void>, void>)Export(library, "dino_sk_start_updates");
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
                s_bridge?.StartUpdates();
            }

            return s_bridge;
        }
    }

    public bool CanMakePayments => _canPay() == 1;

    // A UTF-8, NUL-terminated copy of the input lives for the call only: the Swift side copies what it needs before it returns.
    public Task<string> ProductsAsync(IReadOnlyCollection<string> productIds)
    {
        (int id, Task<string> task) = Register();
        fixed (byte* text = Utf8(JsonSerializer.Serialize(productIds)))
        {
            _products(text, id, &OnAnswer);
        }

        return task;
    }

    public Task<string> BuyAsync(string productId)
    {
        (int id, Task<string> task) = Register();
        fixed (byte* text = Utf8(productId))
        {
            _buy(text, id, &OnAnswer);
        }

        return task;
    }

    /// <summary>Silent read of what the Apple ID owns; <paramref name="planIds"/> are the subscription plans whose newest transaction is wanted even when expired.</summary>
    public Task<string> EntitlementsAsync(IReadOnlyCollection<string> planIds)
    {
        (int id, Task<string> task) = Register();
        fixed (byte* text = Utf8(JsonSerializer.Serialize(planIds)))
        {
            _entitlements(text, id, &OnAnswer);
        }

        return task;
    }

    /// <summary>The Restore purchases button: Apple may ask for the password.</summary>
    public Task<string> SyncAsync()
    {
        (int id, Task<string> task) = Register();
        _restore(id, &OnAnswer);
        return task;
    }

    public Task<string> RedeemAsync()
    {
        (int id, Task<string> task) = Register();
        _redeem(id, &OnAnswer);
        return task;
    }

    private static StoreKit2Bridge? TryCreate()
    {
        string path = Path.Combine(NSBundle.MainBundle.BundlePath, "Frameworks", $"{FrameworkName}.framework", FrameworkName);
        if (!NativeLibrary.TryLoad(path, out IntPtr library))
        {
            PurchaseTrace.Line("storekit2 framework not loaded");
            return null;
        }

        try
        {
            return new StoreKit2Bridge(library);
        }
        catch (EntryPointNotFoundException)
        {
            // no-fallback: a framework without the expected functions is the same as none; every call then answers a failed store, which BookPurchases reports
            PurchaseTrace.Line("storekit2 framework lacks a function");
            return null;
        }
    }

    private static IntPtr Export(IntPtr library, string name) => NativeLibrary.GetExport(library, name);

    private void StartUpdates() => _startUpdates(&OnAnswer);

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
