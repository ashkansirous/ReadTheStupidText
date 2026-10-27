using System.Globalization;
using DinoReader.Resources.Strings;

namespace DinoReader.Tests;

/// <summary>The app speaks one culture, whatever thread or async flow asks (Slice 159, Decision 164).</summary>
[Collection("AppCulture")]
public class AppCultureTests
{
    private static readonly CultureInfo Farsi = CultureInfo.GetCultureInfo("fa");
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de");

    [Fact]
    public async Task A_fresh_thread_sees_the_language_just_picked()
    {
        AppResources.UseCulture(Farsi);

        CultureInfo seen = await Task.Run(() => AppResources.Culture);

        Assert.Equal("fa", seen.Name);
    }

    [Fact]
    public async Task The_language_survives_an_await_on_another_flow()
    {
        AppResources.UseCulture(German);
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        await Task.Yield();
        CultureInfo afterAwait = await Task.Run(async () =>
        {
            await Task.Delay(1);
            return AppResources.Culture;
        });

        Assert.Equal("de", afterAwait.Name);
    }

    [Fact]
    public void Switching_the_language_changes_the_formatting_culture_too()
    {
        AppResources.UseCulture(German);
        string german = AppResources.Format("{0}", 1.5);
        AppResources.UseCulture(CultureInfo.GetCultureInfo("en"));

        Assert.Equal("1,5", german);
        Assert.Equal("1.5", AppResources.Format("{0}", 1.5));
    }
}
