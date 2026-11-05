using DinoReader.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace DinoReader;

/// <summary>
/// The keyboard focus ring and the arrow-key focus moves of Dino Reader's Windows window (Slice 235 part C, mock Batch 46 frames 157 and 159). WinUI shows its focus visual only after a key press; this class only
/// styles it: a 3 dp indigo ring with a 3 dp white gap on paper pages, the shelf yellow on ink pages (the corner). It also switches on WinUI's arrow-key focus navigation for the whole window, so up and down move
/// through Settings rows and the shelf, and right goes into the open pane; Reading, the gate and the sheet take their arrows through <see cref="DinoKeyboard"/> first, and a slider or text field keeps its own.
/// Windows only: phones and tablets never see it.
/// </summary>
internal static class DinoFocusRing
{
	private const double RingThickness = 3;
	private const string IndigoHex = "#5B57E8";
	private const string ShelfYellowHex = "#FFC24D";

	private static readonly global::Windows.UI.Color Indigo = global::Windows.UI.Color.FromArgb(0xFF, 0x5B, 0x57, 0xE8);
	private static readonly global::Windows.UI.Color Yellow = global::Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xC2, 0x4D);
	private static readonly global::Windows.UI.Color White = global::Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
	private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Microsoft.Maui.Controls.Page, object> PagesWaitingForHandler = [];

	/// <summary>Hooks the window's root once: arrows move the focus, and a focused element gets the ring in the colour of the page on screen.</summary>
	public static void Attach(UIElement root)
	{
		root.GotFocus += OnGotFocus;
		WindowTrace.Write($"Dino focus ring: hooked, arrow keys move focus on the shelf and Settings ({IndigoHex} on paper, {ShelfYellowHex} on ink)");
	}

	/// <summary>
	/// Arrow keys move the focus only inside the shelf and Settings pages. Every other page (Reading, where arrows turn pages, and the editors with their text fields) keeps WinUI's default of no arrow
	/// navigation, so a declined arrow never throws the focus onto a toolbar button.
	/// </summary>
	public static void SetArrowsFor(Page page)
	{
		if (page.Handler?.PlatformView is not UIElement element)
		{
			// No native view yet (the first call can come before the handler exists): set it as soon as it appears. One waiting subscription per page, however often this is called meanwhile.
			if (!PagesWaitingForHandler.TryAdd(page, new object()))
			{
				return;
			}

			void OnHandler(object? sender, EventArgs e)
			{
				if (page.Handler is not null)
				{
					page.HandlerChanged -= OnHandler;
					PagesWaitingForHandler.Remove(page);
					SetArrowsFor(page);
				}
			}

			page.HandlerChanged += OnHandler;
			return;
		}

		// The shelf's arrows are off while any overlay is open on it, so they cannot move the focus to a book behind the scrim.
		bool wanted = page is SettingsPage || page is GalleryPage { OverlayOpen: false };
		element.XYFocusKeyboardNavigation = wanted ? XYFocusKeyboardNavigationMode.Enabled : XYFocusKeyboardNavigationMode.Auto;
	}

	private static void OnGotFocus(object sender, RoutedEventArgs e)
	{
		if (e.OriginalSource is Microsoft.UI.Xaml.Controls.TextBox or Microsoft.UI.Xaml.Controls.PasswordBox or Microsoft.UI.Xaml.Controls.RichEditBox)
		{
			((UIElement)e.OriginalSource).XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Disabled; // a text field never gives the focus away at the end of its text
		}

		if (e.OriginalSource is not Microsoft.UI.Xaml.Controls.Control control)
		{
			return; // a layout panel has no focus visual; a book on the shelf is a native list item (a Control) and gets the ring above
		}

		bool onInk = DinoWindowFrame.Current?.Tone == TitleBarTone.Ink;
		control.FocusVisualPrimaryBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(onInk ? Yellow : Indigo);
		control.FocusVisualSecondaryBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(onInk ? global::Windows.UI.Color.FromArgb(0, 0, 0, 0) : White);
		control.FocusVisualPrimaryThickness = new Microsoft.UI.Xaml.Thickness(RingThickness);
		control.FocusVisualSecondaryThickness = new Microsoft.UI.Xaml.Thickness(onInk ? 0 : RingThickness);
		control.FocusVisualMargin = new Microsoft.UI.Xaml.Thickness(-2 * RingThickness);
	}
}
