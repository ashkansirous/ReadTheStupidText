#if DEBUG
namespace ReadTheStupidText.Mobile.Shared.Testing;

public static partial class TestHookLog
{
	static partial void WritePlatform(string marker) => global::Android.Util.Log.Info(Tag, marker);
}
#endif
