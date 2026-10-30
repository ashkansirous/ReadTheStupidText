using ReadTheStupidText.Mobile.Shared.Testing;

namespace ReadTheStupidText.Tests;

public class TestHookLaunchKeysTests
{
	[Fact]
	public void Build_PutsTheCommandNameFirst()
	{
		var keys = TestHookLaunchKeys.Build("cmd", ["lang", "text"], ["id"]);

		Assert.Equal("cmd", keys[0]);
	}

	[Fact]
	public void Build_ListsOwnKeysThenAppKeys_InOrder()
	{
		var keys = TestHookLaunchKeys.Build("cmd", ["lang", "text"], ["id", "page"]);

		Assert.Equal(["cmd", "lang", "text", "id", "page"], keys);
	}

	[Fact]
	public void Build_DropsDuplicates_KeepingTheFirst()
	{
		var keys = TestHookLaunchKeys.Build("cmd", ["lang", "text"], ["text", "cmd", "page", "page"]);

		Assert.Equal(["cmd", "lang", "text", "page"], keys);
	}

	[Fact]
	public void Build_WithNoAppKeys_ListsOnlyTheDispatcherKeys()
	{
		Assert.Equal(["cmd", "lang"], TestHookLaunchKeys.Build("cmd", ["lang"], []));
	}
}
