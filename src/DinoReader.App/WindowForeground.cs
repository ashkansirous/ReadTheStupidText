namespace DinoReader;

/// <summary>
/// Whether the app's window is in front right now, read from the platform (Slice 322). The reading page asks only when it subscribes to a window (first watch or a swap), where no Stopped/Resumed event will arrive.
/// Never read it inside those handlers: MAUI raises Resumed before the platform state has caught up, so the event is trusted there. The halves are in <c>Platforms/&lt;OS&gt;/WindowForeground.*.cs</c>; call on the UI thread.
/// </summary>
public static partial class WindowForeground
{
	/// <summary>False while the window is stopped (screen locked, Home, another app in front), true while in front, null when the platform cannot tell (no activity yet): the caller then keeps what it knew.</summary>
	public static partial bool? IsInForeground();
}
