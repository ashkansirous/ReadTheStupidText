namespace ReadTheStupidText.Mobile.Shared.Testing;

/// <summary>The launch-argument names a hook reads (Slice 199): pure, so a unit test reaches it without MAUI.</summary>
public static class TestHookLaunchKeys
{
	/// <summary>The command name (passed in, so this file has no Debug-only dependency) first, then the dispatcher's own keys, then the app's; each name once, in that order.</summary>
	public static IReadOnlyList<string> Build(string commandKey, IEnumerable<string> ownKeys, IEnumerable<string> appKeys) =>
		new[] { commandKey }.Concat(ownKeys).Concat(appKeys).Distinct().ToList();
}
