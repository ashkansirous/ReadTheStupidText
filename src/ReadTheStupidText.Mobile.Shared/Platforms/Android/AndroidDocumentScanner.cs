using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Gms.Extensions;
using Android.Gms.Common.Apis;
using GmsModuleInstall = Android.Gms.Common.ModuleInstall;
using Google.MLKit.Vision.Documentscanner;
using Xamarin.Google.MLKit.Common;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// <see cref="IDocumentScanner"/> over Google's ML Kit Document Scanner (Slice 45, Decision 64).
/// The scanner is a Play-services module with its own UI (edge detection, crop, multi-page,
/// reorder, gallery import) and needs no camera permission. Its result comes back through
/// the app's MainActivity.OnActivityResult, which calls <see cref="HandleActivityResult"/>;
/// one scan is pending at a time (a newer one supersedes an older).
/// </summary>
public sealed class AndroidDocumentScanner : IDocumentScanner
{
    private const int FirstRequestCode = 45000;
    private const int RequestCodeSpan = 256;
    private const int MaxPages = 20;

    // The one pending scan, swapped atomically so a stale scan can never be completed by (or
    // block) a newer one.
    private static PendingScan? s_pending;
    private static int s_requestCounter;

    private sealed record PendingScan(int RequestCode, TaskCompletionSource<IReadOnlyList<string>> Completion);

    public async Task<IReadOnlyList<string>> ScanAsync()
    {
        Activity activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
            ?? throw new DocumentScannerStartFailedException();
        var request = new PendingScan(
            FirstRequestCode + (Interlocked.Increment(ref s_requestCounter) & (RequestCodeSpan - 1)),
            new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously));

        // The user can only tap Scan while our activity is in front, so any earlier scanner is
        // gone: finish its waiter as a cancel and take over.
        Interlocked.Exchange(ref s_pending, request)?.Completion.TrySetResult([]);

        try
        {
            IntentSender sender = await GetStartIntentAsync(activity);
            Launch(activity, sender, request.RequestCode);
            return await request.Completion.Task;
        }
        finally
        {
            Interlocked.CompareExchange(ref s_pending, null, request);
        }
    }

    public async Task PrepareAsync()
    {
        try
        {
            Context context = Microsoft.Maui.ApplicationModel.Platform.AppContext;
            IOptionalModuleApi scanner = GmsDocumentScanning.GetClient(BuildOptions()).JavaCast<IOptionalModuleApi>();
            GmsModuleInstall.ModuleInstallRequest request = GmsModuleInstall.ModuleInstallRequest.NewBuilder().AddApi(scanner).Build();
            await GmsModuleInstall.ModuleInstall.GetClient(context).InstallModules(request);
        }
        catch (Exception)
        {
            // Best effort: a failed warm-up just means the first scan may report "preparing".
        }
    }

    public static void HandleActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (Volatile.Read(ref s_pending) is not { } pending || pending.RequestCode != requestCode)
        {
            return;
        }

        pending.Completion.TrySetResult(resultCode == Result.Ok ? ReadPageUris(data) : []);
    }

    private static void Launch(Activity activity, IntentSender sender, int requestCode)
    {
        try
        {
            activity.StartIntentSenderForResult(sender, requestCode, null, 0, 0, 0);
        }
        catch (Java.Lang.Exception ex)
        {
            throw new DocumentScannerUnavailableException(ex);
        }
    }

    private static async Task<IntentSender> GetStartIntentAsync(Activity activity)
    {
        try
        {
            IGmsDocumentScanner scanner = GmsDocumentScanning.GetClient(BuildOptions());
            Java.Lang.Object sender = await scanner.GetStartScanIntent(activity);
            return (IntentSender)sender;
        }
        catch (Exception ex)
        {
            throw IsModuleStillPreparing(ex)
                ? new DocumentScannerPreparingException(ex)
                : new DocumentScannerUnavailableException(ex);
        }
    }

    // ML Kit reports a Play-services module that is still downloading as UNAVAILABLE;
    // NOT_SUPPORTED (low RAM, no Play services) and everything else (including a failed cast)
    // is permanent. The Task wrapper may bury the MlKitException under inner exceptions or
    // a Java cause chain, so look through all of them.
    private static bool IsModuleStillPreparing(Exception ex) =>
        EnumerateCauses(ex).OfType<MlKitException>().Any(e => e.ErrorCode == MlKitException.Unavailable);

    private static IEnumerable<Exception> EnumerateCauses(Exception? ex)
    {
        if (ex is null)
        {
            yield break;
        }

        yield return ex;
        IEnumerable<Exception> next = ex is AggregateException aggregate
            ? aggregate.InnerExceptions
            : new[] { ex.InnerException, (ex as Java.Lang.Throwable)?.Cause as Exception }.OfType<Exception>();
        foreach (Exception cause in next.SelectMany(EnumerateCauses))
        {
            yield return cause;
        }
    }

    private static GmsDocumentScannerOptions BuildOptions() =>
        new GmsDocumentScannerOptions.Builder()
            .SetGalleryImportAllowed(true)!
            .SetPageLimit(MaxPages)!
            .SetResultFormats(GmsDocumentScannerOptions.ResultFormatJpeg)!
            .SetScannerMode(GmsDocumentScannerOptions.ScannerModeFull)!
            .Build()!;

    // Uri strings are passed through whole (file:// or content://); the extractor parses them.
    private static List<string> ReadPageUris(Intent? data)
    {
        IList<GmsDocumentScanningResult.Page>? pages = GmsDocumentScanningResult.FromActivityResultIntent(data)?.Pages;
        return pages?
            .Select(page => page.ImageUri?.ToString())
            .OfType<string>()
            .ToList() ?? [];
    }
}
