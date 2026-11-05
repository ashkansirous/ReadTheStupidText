namespace DinoReader;

/// <summary>
/// Writes a WinUI unhandled exception (message, exception type, HResult, stack) to window-trace.log before the app dies
/// (Slice 235 part C: the VM run died with a native 0xc000027b stowed exception and no managed trace). It never sets
/// <c>Handled</c>, so the app still ends exactly as before. Hooked once.
/// </summary>
internal static class WinUiCrashTrace
{
	private static bool _hooked;

	internal static void Hook()
	{
		if (_hooked || Microsoft.UI.Xaml.Application.Current is null)
		{
			return;
		}

		_hooked = true;
		Microsoft.UI.Xaml.Application.Current.UnhandledException += OnUnhandled;
		WindowTrace.Write("WinUI UnhandledException trace hooked");
	}

	private static void OnUnhandled(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
	{
		Exception? ex = e.Exception;
		WindowTrace.Write($"WinUI UNHANDLED: message={e.Message}; type={ex?.GetType().FullName ?? "none"}; HResult=0x{ex?.HResult ?? 0:X8}; stack={ex?.StackTrace ?? "no stack"}");
	}
}
