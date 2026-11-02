using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>
/// A page that answers the Windows window's keyboard (Slice 235 part B): it says which screen it is and what each <see cref="DinoKeyAction"/> does there. Shared code with no platform types;
/// only the Windows head feeds it keys (<c>Platforms/Windows/DinoKeyboard.cs</c>), so phones and tablets never call it.
/// </summary>
internal interface IDinoKeyTarget
{
	/// <summary>Which kind of screen this is right now (a shelf shows <see cref="DinoScreen.Sheet"/> while its options sheet is open).</summary>
	DinoScreen Screen { get; }

	/// <summary>Runs <paramref name="action"/> (made by <paramref name="key"/>, which a gate digit needs); false when it does not apply right now, so the key goes on to the focused control as usual.</summary>
	bool Handle(DinoKeyAction action, DinoKey key);
}

/// <summary>Finds the page that has the keyboard and runs a key press through the key map.</summary>
internal static class DinoKeyTargets
{
	/// <summary>The page the user is looking at: the top modal (the gate, the intro), else the navigation stack's current page.</summary>
	public static Page? TopPage(Microsoft.Maui.Controls.Window? window)
	{
		if (window?.Page is not NavigationPage navigation)
		{
			return window?.Page;
		}

		Page? modal = navigation.Navigation.ModalStack.LastOrDefault();
		return modal is NavigationPage modalNavigation ? modalNavigation.CurrentPage : modal ?? navigation.CurrentPage;
	}

	private static readonly BackInFlight BackGate = new();
	private static readonly TimeSpan HandledBackWait = TimeSpan.FromSeconds(1);

	// Esc on a page that is not a key target goes back one page, when it is the top of a stack with more than one page (the shelf is a target, so it never gets here).
	// One back at a time: while one is in flight every further Esc is swallowed, so a held or repeated Esc can never pop a second page.
	private static bool PopOne(Page? page)
	{
		if (page is null || page.Navigation.NavigationStack.Count <= 1 || page.Navigation.NavigationStack[^1] != page)
		{
			return false;
		}

		if (BackGate.TryBegin())
		{
			_ = GoBackAsync(page);
		}

		return true;
	}

	private static async Task GoBackAsync(Page page)
	{
		try
		{
			// The same path the system Back takes: the page's own OnBackButtonPressed (language commit, picker close, Settings CloseAsync, unsaved edits) runs first, and we pop only if it did not handle it.
			var left = new TaskCompletionSource();
			page.Disappearing += (_, _) => left.TrySetResult();
			if (!page.SendBackButtonPressed())
			{
				await page.Navigation.PopAsync();
				return;
			}

			await Task.WhenAny(left.Task, Task.Delay(HandledBackWait)); // the page handled it itself (its own async close): stay in flight until it has left, or a second back would pop the page below
		}
		finally
		{
			BackGate.End();
		}
	}

	/// <summary>True when the key was used and must not reach the focused control.</summary>
	public static bool Route(Microsoft.Maui.Controls.Window? window, DinoKey key, bool keyDown, bool textFieldFocused)
	{
		Page? top = TopPage(window);
		if (top is not IDinoKeyTarget target)
		{
			return keyDown && DinoKeyMap.Resolve(key, DinoScreen.Other, textFieldFocused, keyDown) == DinoKeyAction.Back && PopOne(top);
		}

		DinoKeyAction action = DinoKeyMap.Resolve(key, target.Screen, textFieldFocused, keyDown);
		return action != DinoKeyAction.None && target.Handle(action, key);
	}
}

/// <summary>Desktop-only hints (Slice 235 part B): tooltips with the key in brackets. Every call is a no-op off Windows, so phones and tablets keep what they have.</summary>
internal static class DesktopTips
{
	private const string WithKey = "{0} ({1})";

	/// <summary>True on the Windows head, the only place with a mouse pointer and a keyboard reaching the pages.</summary>
	public static bool IsWindows => DeviceInfo.Platform == DevicePlatform.WinUI;

	/// <summary>Sets the tooltip of an icon-only control to <paramref name="text"/>, followed by <paramref name="key"/> in brackets when there is one.</summary>
	public static void Set(BindableObject element, string text, DinoKey? key = null)
	{
		if (!IsWindows)
		{
			return;
		}

		ToolTipProperties.SetText(element, key is { } shortcut ? string.Format(AppResources.Culture, WithKey, text, KeyName(shortcut)) : text);
	}

	/// <summary>The key as written in the tooltip, in the app language.</summary>
	public static string KeyName(DinoKey key) => key switch
	{
		DinoKey.Space => AppResources.Key_Space,
		DinoKey.Left => AppResources.Key_Left,
		DinoKey.Right => AppResources.Key_Right,
		DinoKey.Escape => AppResources.Key_Esc,
		_ => key.ToString(),
	};
}
