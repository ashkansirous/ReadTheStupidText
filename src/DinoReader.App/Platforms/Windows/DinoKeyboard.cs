using DinoReader.Core;
using Microsoft.Maui.Platform;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using CoreVirtualKeyStates = Windows.UI.Core.CoreVirtualKeyStates;

namespace DinoReader;

/// <summary>
/// The one keyboard hook of Dino Reader's Windows window (Slice 235 part B, mock Batch 46 frame 160). It listens on the window's root content with the tunnelling PreviewKeyDown/PreviewKeyUp
/// events, so it sees a key before the focused control does, maps it to a <see cref="DinoKey"/> and gives it to <see cref="DinoKeyTargets"/>, which asks the page on screen. A key the page
/// used is marked handled; any other key goes on to the focused control as usual. Phones and tablets never get this class.
/// </summary>
internal sealed class DinoKeyboard
{
	private readonly Microsoft.Maui.Controls.Window _window;
	private readonly HashSet<VirtualKey> _used = [];

	private DinoKeyboard(Microsoft.Maui.Controls.Window window)
	{
		_window = window;
	}

	/// <summary>Hooks the keys of <paramref name="native"/>'s content once; if the content does not exist yet, waits for the window's first activation.</summary>
	public static void Attach(MauiWinUIWindow native, Microsoft.Maui.Controls.Window window)
	{
		var keyboard = new DinoKeyboard(window);
		if (native.Content is UIElement root)
		{
			keyboard.Hook(root);
			return;
		}

		WindowTrace.Write("Dino keyboard: no content yet, waiting for the first activation");
		void OnActivated(object sender, WindowActivatedEventArgs args)
		{
			if (native.Content is UIElement late)
			{
				native.Activated -= OnActivated;
				keyboard.Hook(late);
			}
		}

		native.Activated += OnActivated;
	}

	private void Hook(UIElement root)
	{
		root.PreviewKeyDown += OnKeyDown;
		root.PreviewKeyUp += OnKeyUp;
		DinoFocusRing.Attach(root); // Slice 235 part C: the focus ring and arrow-key focus moves share this one root hook
		_window.Deactivated += (_, _) => ReleaseHeldKeys();
		root.LostFocus += (sender, _) =>
		{
			if (sender is UIElement { XamlRoot: { } xamlRoot } && FocusManager.GetFocusedElement(xamlRoot) is null)
			{
				ReleaseHeldKeys();
			}
		};
		WindowTrace.Write("Dino keyboard: hooked");
	}

	// Slice 279 (mock Batch 48 frame 184): on the Record screen R records and stops, Space plays a recorded piece, S skips and Right goes on. The page decides whether the key does anything now.
	private bool TryRecordKey(object sender, KeyRoutedEventArgs e)
	{
		if (RecordKeyOf(e.Key) is not { } key || DinoKeyTargets.TopPage(_window) is not IRecordKeys page)
		{
			return false;
		}

		if (HasShortcutModifier(Map(e.Key)) || TextFieldHasFocus(sender))
		{
			return false;
		}

		if (e.KeyStatus.WasKeyDown && _used.Contains(e.Key))
		{
			e.Handled = true; // a held key acts once
			return true;
		}

		if (!page.HandleRecordKey(key))
		{
			return false;
		}

		_used.Add(e.Key);
		e.Handled = true;
		WindowTrace.Write($"Dino keyboard: {e.Key} used (Record screen)");
		return true;
	}

	private static RecordKey? RecordKeyOf(VirtualKey key) => key switch
	{
		VirtualKey.R => RecordKey.Record,
		VirtualKey.S => RecordKey.Skip,
		VirtualKey.Space => RecordKey.Play,
		VirtualKey.Right => RecordKey.Next,
		VirtualKey.Escape => RecordKey.Leave,
		_ => null,
	};

	private void OnKeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (TryRecordKey(sender, e))
		{
			return;
		}

		DinoKey key = Map(e.Key);
		if (key == DinoKey.Other)
		{
			return;
		}

		if (e.KeyStatus.WasKeyDown && IsOneShotKey(key) && _used.Contains(e.Key))
		{
			e.Handled = true; // Windows repeats a held key: a repeat of Space, Enter, Esc (or Right on Settings, which opens a row) never acts a second time (the gate hold must not restart, a held Esc must not pop several pages)
			return;
		}

		if (HasShortcutModifier(key))
		{
			return; // Ctrl or Alt (or Shift on a digit) makes it a shortcut or a symbol, not ours: Alt+Space opens the system menu, Shift+1 is not the digit 1
		}

		bool used = DinoKeyTargets.Route(_window, key, keyDown: true, TextFieldHasFocus(sender));
		if (used)
		{
			_used.Add(e.Key);
			e.Handled = true;
		}

		string line = $"Dino keyboard: {e.Key} {(used ? "used" : "not used")} ({DescribeTarget()})";
		if (key is DinoKey.Up or DinoKey.Down)
		{
			WindowTrace.WriteSampled(line); // a held arrow repeats dozens of times a second
		}
		else
		{
			WindowTrace.Write(line);
		}
	}

	// One short line of where a key went: the page DinoKeyTargets.Route picked and the screen it reports (the VM check reads this when a key it sent is not used).
	private string DescribeTarget()
	{
		Microsoft.Maui.Controls.Page? top =DinoKeyTargets.TopPage(_window);
		string screen = top is IDinoKeyTarget target ? target.Screen.ToString() : "no target";
		return $"{top?.GetType().Name ?? "no page"}, {screen}";
	}

	private bool IsOneShotKey(DinoKey key) =>
		key is DinoKey.Space or DinoKey.Enter or DinoKey.Escape || (key == DinoKey.Right && DinoKeyTargets.TopPage(_window) is SettingsPage);

	private static bool HasShortcutModifier(DinoKey key) =>
		IsDown(VirtualKey.Control) || IsDown(VirtualKey.Menu) || (DinoKeyMap.DigitOf(key) is not null && IsDown(VirtualKey.Shift));

	private static bool IsDown(VirtualKey modifier) =>
		InputKeyboardSource.GetKeyStateForCurrentThread(modifier).HasFlag(CoreVirtualKeyStates.Down);

	// The window lost the keyboard (Alt-Tab, a click elsewhere): no key-up may arrive for a Space that is down, so its gate hold ends. "Used" is never cleared here, only by the key-up itself:
	// a press is one action, so a held key's repeats stay swallowed even when the focused element goes away (a held Esc must not pop a second page).
	private void ReleaseHeldKeys()
	{
		if (!_used.Contains(VirtualKey.Space))
		{
			return;
		}

		DinoKeyTargets.Route(_window, DinoKey.Space, keyDown: false, textFieldFocused: false);
		WindowTrace.Write("Dino keyboard: window lost the keyboard, the Space hold ended");
	}

	private void OnKeyUp(object sender, KeyRoutedEventArgs e)
	{
		if (RecordKeyOf(e.Key) is not null && DinoKeyTargets.TopPage(_window) is IRecordKeys && _used.Remove(e.Key))
		{
			e.Handled = true; // the Record screen used this key going down: a focused button must not click as it comes up
			return;
		}

		DinoKey key = Map(e.Key);
		if (key == DinoKey.Other)
		{
			return;
		}

		bool used = DinoKeyTargets.Route(_window, key, keyDown: false, TextFieldHasFocus(sender));
		if (_used.Remove(e.Key) || used)
		{
			e.Handled = true; // the key was ours going down, so a focused button must not click as it comes up
		}
	}

	// The focus is in a text field (WinUI's TextBox, PasswordBox, RichEditBox, AutoSuggestBox behind MAUI's Entry, Editor and SearchBar).
	private static bool TextFieldHasFocus(object sender) =>
		sender is UIElement { XamlRoot: { } xamlRoot } && FocusManager.GetFocusedElement(xamlRoot) is TextBox or PasswordBox or RichEditBox or AutoSuggestBox;

	// The only place a Windows key becomes a DinoKey. The number row and the number pad both type digits.
	private static DinoKey Map(VirtualKey key) => key switch
	{
		VirtualKey.Space => DinoKey.Space,
		VirtualKey.Left => DinoKey.Left,
		VirtualKey.Right => DinoKey.Right,
		VirtualKey.Up => DinoKey.Up,
		VirtualKey.Down => DinoKey.Down,
		VirtualKey.Escape => DinoKey.Escape,
		VirtualKey.Enter => DinoKey.Enter,
		VirtualKey.Back => DinoKey.Backspace,
		>= VirtualKey.Number0 and <= VirtualKey.Number9 => DinoKey.Digit0 + (key - VirtualKey.Number0),
		>= VirtualKey.NumberPad0 and <= VirtualKey.NumberPad9 => DinoKey.Digit0 + (key - VirtualKey.NumberPad0),
		_ => DinoKey.Other,
	};
}
