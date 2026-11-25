#if DEBUG
using Sirous.Reading.Mobile.Shared.Testing;

namespace DinoReader.Testing;

/// <summary>
/// Slice 433 item 6, the platform half of the <c>scroll-shelf</c> hook: moves the shelf's native list by a number of pixels, one frame at a time, so the scroll looks like a finger drag
/// (Maestro's <c>swipe</c> moves nothing on iOS 27.2). Android drives the RecyclerView, iOS the UICollectionView; the Windows head has no recorded clip, so it reports no list.
/// </summary>
internal static class ShelfScroller
{
	/// <summary>Scrolls <paramref name="list"/> down by <paramref name="pixels"/> (up when negative) over <paramref name="milliseconds"/>; false when the list has no native view yet. Main thread only.</summary>
	internal static async Task<bool> ScrollAsync(CollectionView list, int pixels, int milliseconds)
	{
		if (list.Handler?.PlatformView is not { } platformView)
		{
			return false;
		}

		if (Resolve(platformView) is not { } native)
		{
			TestHookLog.Write($"[ShelfScroll] no scrollable list in {platformView.GetType()}");
			return false;
		}

		foreach (int delta in ShelfScrollPlan.FrameDeltas((int)Math.Round(pixels * Density), milliseconds))
		{
			ScrollBy(native, delta);
			await Task.Delay(ShelfScrollPlan.FrameMilliseconds);
		}

		return true;
	}

#if ANDROID
	// The caller passes device-independent pixels; RecyclerView.ScrollBy takes real ones.
	private static double Density => DeviceDisplay.Current.MainDisplayInfo.Density;

	private static object? Resolve(object platformView) => platformView as AndroidX.RecyclerView.Widget.RecyclerView;

	private static void ScrollBy(object native, int delta) => ((AndroidX.RecyclerView.Widget.RecyclerView)native).ScrollBy(0, delta);
#elif IOS
	private static double Density => 1; // UIKit points are device-independent pixels

	private static object? Resolve(object platformView) => FindCollection(platformView as UIKit.UIView);

	private static void ScrollBy(object native, int delta)
	{
		var collection = (UIKit.UICollectionView)native;
		nfloat top = -collection.AdjustedContentInset.Top;
		nfloat bottom = (nfloat)Math.Max(top, collection.ContentSize.Height - collection.Bounds.Height + collection.AdjustedContentInset.Bottom);
		nfloat y = (nfloat)Math.Clamp((double)(collection.ContentOffset.Y + delta), (double)top, (double)bottom);
		collection.SetContentOffset(new CoreGraphics.CGPoint(collection.ContentOffset.X, y), false);
	}

	private static UIKit.UICollectionView? FindCollection(UIKit.UIView? view)
	{
		if (view is UIKit.UICollectionView found)
		{
			return found;
		}

		foreach (UIKit.UIView child in view?.Subviews ?? [])
		{
			if (FindCollection(child) is { } inner)
			{
				return inner;
			}
		}

		return null;
	}
#else
	private static double Density => 1;

	private static object? Resolve(object platformView) => null;

	private static void ScrollBy(object native, int delta)
	{
		TestHookLog.Write($"[ShelfScroll] no native list on this head, delta={delta}");
	}
#endif
}
#endif
