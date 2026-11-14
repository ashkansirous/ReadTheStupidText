namespace DinoReader;

public static partial class WindowForeground
{
	// The Windows head reports unknown and so never overrides what Window.Stopped / Window.Resumed set. Not verified on the VM.
	public static partial bool? IsInForeground() => null;
}
