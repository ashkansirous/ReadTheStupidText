using System.Runtime.CompilerServices;
using DinoReader.Core;
using Windows.System;

namespace DinoReader;

/// <summary>
/// The shelf's mouse and keyboard on Windows (Slice 235 part C, mock Batch 46 frames 151, 155 and 156): a hovered book lifts 4 dp with a deeper shadow (and wears an indigo ring in the grown-ups' corner), the pointer
/// becomes a hand, a book is a Tab stop that Enter or Space opens, a right-click opens the book's options sheet in the corner, and closing the sheet returns the keyboard to the book it came from.
/// Reached only through the partial methods GalleryPage declares and only on Windows; a book's handlers read its binding context when they fire, because the shelf recycles its tiles.
/// </summary>
public partial class GalleryPage
{
	private const double HoverLift = -4;
	private const string HoverRingId = "HoverRing";
	private const string FocusRingId = "FocusRing";
	private static readonly Shadow LiftedShadow = new() { Brush = Color.FromArgb("#2B2540"), Opacity = 0.22f, Offset = new Point(0, 14), Radius = 13 };

	private readonly ConditionalWeakTable<Grid, object> _wiredCovers = [];
	private readonly ConditionalWeakTable<Microsoft.UI.Xaml.UIElement, VerticalStackLayout> _wiredElements = [];
	private WeakReference<Microsoft.UI.Xaml.UIElement>? _focusBeforeSheet;
	private Action? _clearHover;

	private bool _overlayArrowsHooked;

	/// <summary>True while any overlay (a sheet, the language menu) is open over the shelf: the arrow keys then stay out of the books behind its scrim.</summary>
	internal bool OverlayOpen => GalleryRoot.Children.OfType<IPageOverlay>().Any(overlay => ((View)overlay).IsVisible);

	private void HookOverlayArrows()
	{
		if (_overlayArrowsHooked)
		{
			return;
		}

		_overlayArrowsHooked = true;
		foreach (IPageOverlay overlay in GalleryRoot.Children.OfType<IPageOverlay>())
		{
			WatchOverlay((View)overlay);
		}

		GalleryRoot.ChildAdded += (_, e) =>
		{
			if (e.Element is IPageOverlay added)
			{
				WatchOverlay((View)added);
			}

			DinoFocusRing.SetArrowsFor(this);
		};
		GalleryRoot.ChildRemoved += (_, _) => DinoFocusRing.SetArrowsFor(this);
	}

	private void WatchOverlay(View overlay)
	{
		overlay.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(IsVisible))
			{
				DinoFocusRing.SetArrowsFor(this);
			}
		};
	}

	partial void WireDesktopTile(VerticalStackLayout? tile)
	{
		HookOverlayArrows();
		if (tile is not { Children.Count: > 0 } || tile.Children[0] is not Grid cover || !_wiredCovers.TryAdd(cover, new object()))
		{
			return;
		}

		if (RingOf(cover, HoverRingId) is not { } hoverRing || RingOf(cover, FocusRingId) is not { } focusRing)
		{
			return; // the rings are part of the tile template; hover and keyboard focus each own one, so neither undoes the other
		}

		AddHover(tile, cover, hoverRing);
		AddRightClick(tile, cover);
		DesktopPointer.UseHand(cover);
		MakeTabStop(tile, cover, focusRing);
	}

	// The rings are declared in the tile template (GalleryPage.xaml, hidden); no child is ever added to a live tile, which is what a layout cycle on the VM was traced to as a candidate.
	private static Border? RingOf(Grid cover, string automationId) =>
		cover.Children.OfType<Border>().FirstOrDefault(border => border.AutomationId == automationId);

	// The one place a book's hover (lift, shadow, ring) is painted and undone. The first enter records the cover's own shadow (null included) and exit puts it back; the hover is also cleared when the
	// tile is unloaded or recycled for another book and when the options sheet opens, since the pointer may never report an exit then.
	private void AddHover(VerticalStackLayout tile, Grid cover, Border ring)
	{
		bool hovering = false;
		Shadow? restShadow = null;
		void Clear()
		{
			if (!hovering)
			{
				return;
			}

			hovering = false;
			tile.TranslationY = 0;
			cover.Shadow = restShadow;
			ring.IsVisible = false;
		}

		var pointer = new PointerGestureRecognizer();
		pointer.PointerEntered += (_, _) =>
		{
			if (hovering)
			{
				return;
			}

			ClearHover(); // only one book is hovered at a time
			hovering = true;
			restShadow = cover.Shadow;
			_clearHover = Clear;
			WindowTrace.WriteSampled($"Dino shelf: a book is hovered (lifted, ring {_mode.IsGrownUp})");
			tile.TranslationY = HoverLift;
			cover.Shadow = LiftedShadow;
			ring.IsVisible = _mode.IsGrownUp; // the ring marks the book a right-click or the dots would open
		};
		pointer.PointerExited += (_, _) => Clear();
		cover.GestureRecognizers.Add(pointer);
		cover.Unloaded += (_, _) => Clear();
		tile.BindingContextChanged += (_, _) => Clear();
	}

	private void ClearHover()
	{
		_clearHover?.Invoke();
		_clearHover = null;
	}

	// A right-click opens the same sheet as the dots button, in the grown-ups' corner only (the kids' shelf has no sheet and never asks for the gate here).
	private void AddRightClick(VerticalStackLayout tile, Grid cover)
	{
		var rightClick = new TapGestureRecognizer { Buttons = ButtonsMask.Secondary };
		rightClick.Tapped += async (_, _) =>
		{
			if (tile.BindingContext is StoryTile story && _mode.IsGrownUp)
			{
				WindowTrace.Write("Dino shelf: right-click opened the book sheet");
				await RunGrownUpAsync(() => OpenBookSheetAsync(story.Story));
			}
		};
		cover.GestureRecognizers.Add(rightClick);
	}

	// A book is a Tab stop; Enter or Space on it opens it as a click does. The cover is a layout panel, so it is made focusable once it has its native view.
	private void MakeTabStop(VerticalStackLayout tile, Grid cover, Border ring)
	{
		void Apply()
		{
			if (cover.Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement element || !_wiredElements.TryAdd(element, tile))
			{
				return;
			}

			element.IsTabStop = true;
			element.GotFocus += (_, _) =>
			{
				ring.IsVisible = element.FocusState == Microsoft.UI.Xaml.FocusState.Keyboard; // the focus ring of a book: shown for keys only (frame 159)
				WindowTrace.WriteSampled($"Dino shelf: a book has the keyboard (ring {ring.IsVisible})");
			};
			element.LostFocus += (_, _) => ring.IsVisible = false;
			element.KeyDown += (_, e) =>
			{
				if (e.Key is VirtualKey.Enter or VirtualKey.Space && tile.BindingContext is StoryTile story)
				{
					e.Handled = true;
					WindowTrace.Write("Dino shelf: Enter or Space opened a book");
					OpenFromKeyboard(story);
				}
			};
		}

		Apply();
		cover.HandlerChanged += (_, _) => Apply();
	}

	private void OpenFromKeyboard(StoryTile tile)
	{
		_ = ObserveAsync(OpenTileAsync(tile));
	}

	private static async Task ObserveAsync(Task work)
	{
		try
		{
			await work;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			WindowTrace.Write($"Dino shelf: opening a book from the keyboard failed: {ex.GetType().Name}: {ex.Message}");
		}
	}

	// The sheet opens: remember the book tile it came from (the keyboard returns only there, never to a header or a back button) and clear the hover, which the sheet's scrim would otherwise leave stuck.
	partial void RememberTileFocus(Story opened)
	{
		ClearHover();
		_focusBeforeSheet = null;
		foreach (KeyValuePair<Microsoft.UI.Xaml.UIElement, VerticalStackLayout> wired in _wiredElements)
		{
			if (wired.Value.BindingContext is StoryTile tile && tile.Story.Id == opened.Id)
			{
				_focusBeforeSheet = new WeakReference<Microsoft.UI.Xaml.UIElement>(wired.Key);
				return;
			}
		}
	}

	partial void RestoreTileFocus()
	{
		if (_focusBeforeSheet is null || !_focusBeforeSheet.TryGetTarget(out Microsoft.UI.Xaml.UIElement? element))
		{
			return;
		}

		_focusBeforeSheet = null;
		Dispatcher.Dispatch(() =>
		{
			if (element.XamlRoot is not null && IsVisible)
			{
				bool focused = element.Focus(Microsoft.UI.Xaml.FocusState.Keyboard);
				WindowTrace.Write($"Dino shelf: the book has the keyboard back after the sheet closed: {focused}");
			}
		});
	}
}
