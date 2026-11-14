#if DEBUG
namespace Sirous.Reading.Mobile.Shared.Testing;

public static partial class TestHookLog
{
	static partial void WritePlatform(string marker) => Console.WriteLine($"{Tag}: {marker}");
}
#endif
