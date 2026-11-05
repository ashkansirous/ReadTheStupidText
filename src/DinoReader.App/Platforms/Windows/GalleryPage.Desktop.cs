using System.Runtime.CompilerServices;
using DinoReader.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using CornerRadius = Microsoft.UI.Xaml.CornerRadius;
using Thickness = Microsoft.UI.Xaml.Thickness;

namespace DinoReader;

/// <summary>
/// The shelf's mouse and keyboard on Windows (Slice 235 part C, mock Batch 46 frames 151, 155 and 156), all on the native list the MAUI CollectionView renders (a WinUI <see cref="ListViewBase"/>: a GridView for the grid,
/// a ListView for the sideways row). A book's container is already a Tab stop with arrow-key navigation and a focus visual (styled by <see cref="DinoFocusRing"/>); here Enter or Space on it opens the book, a right-click opens
/// the options sheet in the corner, the hover and press plates are made transparent, and closing the sheet returns the keyboard to the book it came from.
/// Nothing is added to a book's template and no MAUI gesture recognizer sits on a tile, so a click reaches MAUI exactly as on other platforms (the two device strikes of part C came from such per-tile wiring).
/// </summary>
public partial class GalleryPage
{
	private static readonly string[] ItemKinds = ["ListViewItem", "GridViewItem"];
	private static readonly string[] ItemStates = ["", "PointerOver", "Pressed", "Selected", "SelectedPointerOver", "SelectedPressed", "Disabled", "SelectedDisabled"];

	private static readonly TimeSpan FocusReturnDelay = TimeSpan.FromMilliseconds(250);
	private const double HoverRingThickness = 3;
	private const double HoverRingRadius = 16;
	private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush HoverRingBrush = new(global::Windows.UI.Color.FromArgb(0xFF, 0x5B, 0x57, 0xE8));

	private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush ClearBrush = new(global::Windows.UI.Color.FromArgb(0, 0, 0, 0));

	private readonly ConditionalWeakTable<ListViewBase, object> _hookedLists = [];
	private readonly ConditionalWeakTable<SelectorItem, object> _hoverWired = [];
	private SelectorItem? _hoveredContainer;
	private WeakReference<Story>? _bookBeforeSheet;
	private bool _openingFromKeyboard;
	private bool _overlayArrowsHooked;

	/// <summary>True while any overlay (a sheet, the language menu) is open over the shelf: the arrow keys then stay out of the books behind its scrim.</summary>
	internal bool OverlayOpen => GalleryRoot.Children.OfType<IPageOverlay>().Any(overlay => ((View)overlay).IsVisible);

	partial void WireDesktopShelf()
	{
		HookOverlayArrows();
		StoryList.HandlerChanged += (_, _) => HookNativeList(); // a sideways row and a grid are different native lists, so a layout swap can bring a new one
		HookNativeList();
	}

	private void HookNativeList()
	{
		if (StoryList.Handler?.PlatformView is not ListViewBase list || !_hookedLists.TryAdd(list, new object()))
		{
			return;
		}

		StyleItemPlates(list);
		list.ContainerContentChanging += OnContainerContentChanging;
		// handledEventsToo: the list's own key handling marks Enter and Space handled before a plain KeyDown subscriber sees them (VM run 4: "Enter not used" and no open line).
		list.AddHandler(UIElement.KeyDownEvent, new Microsoft.UI.Xaml.Input.KeyEventHandler(OnListKeyDown), handledEventsToo: true);
		list.GotFocus += OnListGotFocus;
		list.RightTapped += OnListRightTapped;
		list.PointerMoved += OnListPointerMoved;
		list.PointerExited += OnListPointerLeft;
		list.PointerCanceled += OnListPointerLeft;
		foreach (SelectorItem realized in list.ItemsPanelRoot?.Children.OfType<SelectorItem>() ?? [])
		{
			WireContainer(realized); // containers realized before this hook never raise ContainerContentChanging for it
		}

		WindowTrace.Write($"Dino shelf: native list is {list.GetType().Name} (books are its tab stops; Enter, Space and right-click are wired on it)");
	}

	// Hover, press and selection show no grey plate on a book: the list keeps SelectionMode None and its containers' plates are made transparent through the list's own resources.
	private static void StyleItemPlates(ListViewBase list)
	{
		var clear = new Microsoft.UI.Xaml.Media.SolidColorBrush(global::Windows.UI.Color.FromArgb(0, 0, 0, 0));
		foreach (string kind in ItemKinds)
		{
			foreach (string state in ItemStates)
			{
				list.Resources[$"{kind}Background{state}"] = clear;
			}
		}
	}

	// Each realized container gets the hand cursor and the hover ring once (mock frames 151 and 156: the pointer becomes a hand, a hovered book in the corner wears a 3 dp #5B57E8 ring). They are native
	// pointer events on the container, so MAUI's tap is untouched; the ring is the container's own border, shown in the grown-ups' corner only, as it marks the book a right-click would open.
	private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
	{
		SelectorItem container = args.ItemContainer;
		if (args.InRecycleQueue)
		{
			container.BorderBrush = ClearBrush; // a recycled container never keeps a stuck ring
			if (ReferenceEquals(container, _hoveredContainer))
			{
				_hoveredContainer = null;
			}

			return;
		}

		WindowTrace.WriteSampled("Dino shelf: container realized");
		WireContainer(container);
	}

	// Every container the current native list has gets wired exactly once, whether it was realized before the hook (a list swap, a relayout) or after it.
	private void WireContainer(SelectorItem container)
	{
		container.BorderThickness = RingThickness(); // per mode, never per hover: the kids' shelf has no ring, so its covers keep their full size
		if (!_hoverWired.TryAdd(container, new object()))
		{
			return;
		}

		DinoCursor.Set(container, DinoCursorShape.OpenHand);
		container.CornerRadius = new CornerRadius(HoverRingRadius);
		container.BorderBrush = ClearBrush;
	}

	// Hover is tracked on the list, not per tile: a tile's own PointerEntered stopped firing after a resize re-laid the shelf out. Never Handled, never a pointer capture, so MAUI's tap is untouched.
	private void OnListPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		SelectorItem? container = ContainerUnder(sender, e.OriginalSource);
		if (ReferenceEquals(container, _hoveredContainer))
		{
			return;
		}

		ClearHover();
		_hoveredContainer = container;
		if (container is not null)
		{
			ShowHoverRing(container);
		}
	}

	private void OnListPointerLeft(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => ClearHover();

	private void ClearHover()
	{
		if (_hoveredContainer is not null)
		{
			_hoveredContainer.BorderBrush = ClearBrush;
		}

		_hoveredContainer = null;
	}

	private static SelectorItem? ContainerUnder(object sender, object source)
	{
		if (source is not DependencyObject element)
		{
			return null;
		}

		for (DependencyObject? at = element; at is not null && !ReferenceEquals(at, sender); at = VisualTreeHelper.GetParent(at))
		{
			if (at is SelectorItem container)
			{
				return container;
			}
		}

		return null;
	}

	private Thickness RingThickness() => new(_mode.IsGrownUp ? HoverRingThickness : 0);

	// The mode changed: every realized container takes the new thickness (3 dp in the grown-ups' corner, 0 on the kids' shelf).
	partial void ApplyShelfRing()
	{
		if (StoryList.Handler?.PlatformView is not ListViewBase { ItemsPanelRoot: { } panel })
		{
			return;
		}

		Thickness thickness = RingThickness();
		foreach (SelectorItem container in panel.Children.OfType<SelectorItem>())
		{
			container.BorderThickness = thickness;
		}
	}

	private void ShowHoverRing(SelectorItem container)
	{
		WindowTrace.WriteSampled("Dino shelf: a book is hovered"); // on the kids' shelf too (the hand cursor shows there); only the ring is the grown-ups'
		if (!_mode.IsGrownUp || !CanActOnShelf())
		{
			return;
		}

		container.BorderBrush = HoverRingBrush;
	}

	private void OnListKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
	{
		// Only the book container itself opens on Enter or Space; a control inside it (the options button) keeps its own key handling.
		if (e.Key is not (VirtualKey.Enter or VirtualKey.Space))
		{
			return;
		}

		if (e.OriginalSource is not SelectorItem || e.KeyStatus.WasKeyDown || !CanActOnShelf() || BookAt(sender, e.OriginalSource) is not { } tile)
		{
			// a held key's repeats never open the book a second time; the line says which rule stopped it (VM run 4: Enter on a focused book did not open it)
			WindowTrace.WriteSampled($"Dino shelf: {e.Key} not opened: source={e.OriginalSource?.GetType().Name} repeat={e.KeyStatus.WasKeyDown} canAct={CanActOnShelf()} handled={e.Handled}");
			return;
		}

		WindowTrace.Write("Dino shelf: Enter or Space opened a book");
		_ = ObserveAsync(OpenFromKeyboardAsync(tile));
	}

	// The same open path a tap takes; the flag tells the sheet it came from the keyboard, so closing it returns the keyboard to the book (a mouse open never does).
	private async Task OpenFromKeyboardAsync(StoryTile tile)
	{
		_openingFromKeyboard = true;
		try
		{
			await OpenTileAsync(tile);
		}
		finally
		{
			_openingFromKeyboard = false;
		}
	}

	// A book takes the keyboard (Tab or an arrow): the VM check waits for this line.
	private void OnListGotFocus(object sender, RoutedEventArgs e)
	{
		if (e.OriginalSource is SelectorItem { FocusState: FocusState.Keyboard })
		{
			WindowTrace.WriteSampled("Dino shelf: a book has the keyboard");
		}
	}

	private void OnListRightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
	{
		bool pointerDevice = e.PointerDeviceType is Microsoft.UI.Input.PointerDeviceType.Mouse or Microsoft.UI.Input.PointerDeviceType.Pen; // a touch long-press also raises RightTapped, and the tap path already handles touch
		if (!pointerDevice || !_mode.IsGrownUp || _busy.IsBusy || !CanActOnShelf() || BookAt(sender, e.OriginalSource) is not { } tile)
		{
			return; // the kids' shelf has no sheet and never asks for the gate here; a second click while the first is opening does nothing
		}

		WindowTrace.Write("Dino shelf: right-click opened the book sheet");
		_ = ObserveAsync(RunGrownUpAsync(() => OpenBookSheetAsync(tile.Story))); // the same call the book's options button makes
	}

	private bool CanActOnShelf() => !_reorder.IsVisible && !OverlayOpen;

	// The book under a native event source: climb to its container, take its index in the list and read the same row of the shelf's items.
	private StoryTile? BookAt(object sender, object source)
	{
		if (sender is not ListViewBase list || source is not DependencyObject element)
		{
			return null;
		}

		for (DependencyObject? at = element; at is not null && !ReferenceEquals(at, list); at = VisualTreeHelper.GetParent(at))
		{
			if (at is SelectorItem container)
			{
				int index = list.IndexFromContainer(container);
				return index >= 0 && index < _items.Count ? _items[index] as StoryTile : null;
			}
		}

		return null;
	}

	private static async Task ObserveAsync(Task work)
	{
		try
		{
			await work;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			WindowTrace.Write($"Dino shelf: a keyboard or right-click action failed: {ex.GetType().Name}: {ex.Message}");
		}
	}

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

	// The sheet opens: remember which book it came from, only when Enter or Space opened it (the keyboard returns only there, never to a header or a back button, and a mouse user gets no focus ring).
	partial void RememberTileFocus(Story opened)
	{
		_bookBeforeSheet = _openingFromKeyboard ? new WeakReference<Story>(opened) : null;
	}

	partial void RestoreTileFocus()
	{
		WeakReference<Story>? remembered = _bookBeforeSheet;
		_bookBeforeSheet = null; // used or not, a later dismissal never reuses it
		if (remembered is null || !remembered.TryGetTarget(out Story? opened))
		{
			return;
		}

		// Read now, Cast and Record close the sheet and then push a page: wait for that to land, and focus only if this page is still the one on screen.
		Dispatcher.DispatchDelayed(FocusReturnDelay, () => FocusBook(opened.Id));
	}

	private void FocusBook(string id)
	{
		int index = _items.ToList().FindIndex(item => item is StoryTile tile && tile.Story.Id == id);
		Microsoft.Maui.Controls.Page? top = Navigation.NavigationStack.LastOrDefault();
		bool onScreen = IsVisible && (top is null || ReferenceEquals(top, this)) && Navigation.ModalStack.Count == 0;
		if (index < 0 || !onScreen || StoryList.Handler?.PlatformView is not ListViewBase list || list.ContainerFromIndex(index) is not Control container)
		{
			return;
		}

		bool focused = container.Focus(FocusState.Keyboard);
		WindowTrace.Write($"Dino shelf: the book has the keyboard back after the sheet closed: {focused}");
	}
}
