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

	[Theory]
	[InlineData("set-language", "lang", "sv")]
	[InlineData("set-voice", "id", "F3")]
	[InlineData("stage-book-update", "id", "three-little-pigs")]
	public void Parse_SettingsAndBookUpdateCommandsKeepTheirArgument(string name, string key, string value)
	{
		var command = TestHookCommand.Parse(name, [new("cmd", name), new(key, value)]);

		Assert.Equal(name, command!.Name);
		Assert.Equal(value, command.Get(key, ""));
	}

	[Fact]
	public void FromUrl_SetLanguageReadsTheCode()
	{
		var command = TestHookCommand.FromUrl("dino-test://set-language?lang=fa&voice=0", "dino-test");

		Assert.Equal("set-language", command!.Name);
		Assert.Equal("fa", command.Get("lang", ""));
		Assert.Equal("0", command.Get("voice", ""));
	}

	[Fact]
	public void FromUrl_StageBookUpdateReadsTheId() =>
		Assert.Equal("some-book", TestHookCommand.FromUrl("dino-test://stage-book-update?id=some-book", "dino-test")!.Get("id", ""));

	[Theory]
	[InlineData("dino-test://stage-book-update?id=b&force=true", true)]
	[InlineData("dino-test://stage-book-update?id=b&force=TRUE", true)]
	[InlineData("dino-test://stage-book-update?id=b&force=1", true)]
	[InlineData("dino-test://stage-book-update?id=b&force=false", false)]
	[InlineData("dino-test://stage-book-update?id=b&force=", false)]
	[InlineData("dino-test://stage-book-update?id=b", false)]
	public void Flag_ForceIsOnOnlyForTrueOrOne(string url, bool expected) =>
		Assert.Equal(expected, TestHookCommand.FromUrl(url, "dino-test")!.Flag("force"));

	[Fact]
	public void FromUrl_NoQueryHasNoArgs() => Assert.Empty(TestHookCommand.FromUrl("rtst-test://state", "rtst-test")!.Args);
}
#endif
