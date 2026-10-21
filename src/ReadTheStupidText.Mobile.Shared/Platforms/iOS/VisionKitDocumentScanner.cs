using Foundation;
using UIKit;
using VisionKit;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// <see cref="IDocumentScanner"/> over iOS's own document camera
/// (<see cref="VNDocumentCameraViewController"/>, Slice 49): edge detection, perspective
/// correction and multi-page capture, with the system's camera-permission prompt (the purpose
/// string is <c>NSCameraUsageDescription</c> in Info.plist). Each scanned page is written to the
/// cache folder as a JPEG and its path returned in page order; the Reader deletes them after OCR.
/// One scan is on screen at a time.
/// </summary>
public sealed class VisionKitDocumentScanner : IDocumentScanner
{
    private const float JpegQuality = 0.9f;

    private bool _scanning;

    public Task<IReadOnlyList<string>> ScanAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(GuardedPresentAsync);
    }

    // Nothing to download or warm up: the document camera is part of the OS.
    public Task PrepareAsync() => Task.CompletedTask;

    // Checked and set on the main thread with no await between, so two callers can never
    // both see "not scanning" and present twice.
    private async Task<IReadOnlyList<string>> GuardedPresentAsync()
    {
        if (_scanning)
        {
            return [];
        }

        _scanning = true;
        try
        {
            return await PresentAsync();
        }
        finally
        {
            _scanning = false;
        }
    }

    private async Task<IReadOnlyList<string>> PresentAsync()
    {
        if (!VNDocumentCameraViewController.Supported)
        {
            throw new DocumentScannerUnavailableException();
        }

        UIViewController presenter = Microsoft.Maui.ApplicationModel.Platform.GetCurrentUIViewController()
            ?? throw new DocumentScannerStartFailedException();

        var completion = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var scanner = new VNDocumentCameraViewController { Delegate = new ScanDelegate(completion) };
        // Presenting over a controller that is already presented is refused silently, which
        // would leave the waiter hanging; fail instead.
        if (presenter.PresentedViewController is not null)
        {
            throw new DocumentScannerStartFailedException();
        }

        presenter.PresentViewController(scanner, true, null);
        return await completion.Task;
    }

    private static IReadOnlyList<string> SavePages(VNDocumentCameraScan scan)
    {
        var paths = new List<string>((int)scan.PageCount);
        for (nuint i = 0; i < scan.PageCount; i++)
        {
            using UIImage image = scan.GetImage(i);
            using NSData? jpeg = image.AsJPEG(JpegQuality);
            if (jpeg is null)
            {
                continue;
            }

            string path = Path.Combine(FileSystem.Current.CacheDirectory, $"scan-{Guid.NewGuid():N}.jpg");
            using var url = NSUrl.FromFilename(path);
            if (jpeg.Save(url, NSDataWritingOptions.Atomic, out NSError? _))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    // The controller is dismissed here, then the waiter is released, so a caller that starts
    // another scan straight after never presents over a controller that is still leaving.
    private sealed class ScanDelegate(TaskCompletionSource<IReadOnlyList<string>> completion)
        : VNDocumentCameraViewControllerDelegate
    {
        public override void DidFinish(VNDocumentCameraViewController controller, VNDocumentCameraScan scan)
        {
            // Encoding full-resolution pages as JPEG is slow: keep it off the main thread.
            Task.Run(() => SavePages(scan)).ContinueWith(
                saved => MainThread.BeginInvokeOnMainThread(() => controller.DismissViewController(true, () =>
                {
                    if (saved.IsFaulted)
                    {
                        completion.TrySetException(saved.Exception!.GetBaseException());
                    }
                    else
                    {
                        completion.TrySetResult(saved.Result);
                    }
                })),
                TaskScheduler.Default);
        }

        public override void DidCancel(VNDocumentCameraViewController controller) =>
            controller.DismissViewController(true, () => completion.TrySetResult([]));

        public override void DidFail(VNDocumentCameraViewController controller, NSError error) =>
            controller.DismissViewController(true, () =>
                completion.TrySetException(new DocumentScannerStartFailedException(new NSErrorException(error))));
    }
}
