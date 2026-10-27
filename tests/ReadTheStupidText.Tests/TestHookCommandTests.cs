#if DEBUG
using ReadTheStupidText.Mobile.Shared.Testing;

namespace ReadTheStupidText.Tests;

public class TestHookCommandTests
{
	[Fact]
	public void Parse_LowercasesNameAndDropsTheNameKey()
	{
		var command = TestHookCommand.Parse(" Skip-First-Run ", [new("cmd", "x"), new("lang", "fa")]);

		Assert.Equal("skip-first-run", command!.Name);
		Assert.Equal("fa", command.Get("LANG", "en"));
		Assert.Equal("en", command.Get("cmd", "en"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("  ")]
	public void Parse_BlankNameIsNull(string? name) => Assert.Null(TestHookCommand.Parse(name, null));

	[Fact]
	public void FromUrl_ReadsNameAndQuery()
	{
		var command = TestHookCommand.FromUrl("rtst-test://skip-first-run?lang=fa&x=a%20b", "rtst-test");

		Assert.Equal("skip-first-run", command!.Name);
		Assert.Equal("fa", command.Get("lang", ""));
		Assert.Equal("a b", command.Get("x", ""));
	}

	[Fact]
	public void FromUrl_OtherSchemeIsNull() => Assert.Null(TestHookCommand.FromUrl("dino-test://state", "rtst-test"));

	[Fact]
	public void FromUrl_NoQueryHasNoArgs() => Assert.Empty(TestHookCommand.FromUrl("rtst-test://state", "rtst-test")!.Args);
}
#endif
