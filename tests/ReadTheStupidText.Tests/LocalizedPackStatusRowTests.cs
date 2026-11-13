using ReadTheStupidText.Application.Net;
using ReadTheStupidText.Mobile;
using ReadTheStupidText.Mobile.Resources.Strings;
using ReadTheStupidText.Mobile.Shared;

namespace ReadTheStupidText.Tests;

/// <summary>Slice 373 (Missing States mock RT1): what a language row shows for each installer state.</summary>
public class LocalizedPackStatusRowTests
{
    private const string Size = "Voice pack · 38 MB";

    [Fact]
    public void Idle_row_shows_only_its_size()
    {
        LocalizedPackStatus.RowView view = LocalizedPackStatus.ForRow(PackInstallState.Idle, 0, null, false, Size);

        Assert.Equal(new LocalizedPackStatus.RowView(Size, null, null, false), view);
    }

    [Fact]
    public void Downloading_row_shows_the_percent_at_its_end_and_no_strip()
    {
        LocalizedPackStatus.RowView view = LocalizedPackStatus.ForRow(PackInstallState.Downloading, 0.4, null, false, Size);

        Assert.Equal(Size, view.Detail);
        Assert.Equal(0.4.ToString("P0", System.Globalization.CultureInfo.CurrentUICulture), view.EndText);
        Assert.Null(view.ErrorText);
        Assert.False(view.EndIsRetry);
    }

    [Fact]
    public void Resolving_row_shows_its_status_text_and_no_percent()
    {
        LocalizedPackStatus.RowView view = LocalizedPackStatus.ForRow(PackInstallState.Resolving, 0, null, false, Size);

        Assert.Equal(AppResources.PackStatus_Resolving, view.Detail);
        Assert.Null(view.EndText);
        Assert.False(view.EndIsRetry);
    }

    [Fact]
    public void Failed_404_row_shows_retry_and_the_voice_gone_strip()
    {
        LocalizedPackStatus.RowView view = LocalizedPackStatus.ForRow(PackInstallState.Failed, 0.2, HttpErrorKind.NotFound, true, Size);

        Assert.Equal(Size, view.Detail);
        Assert.Equal(AppResources.Wait_Retry, view.EndText);
        Assert.Equal(AppResources.Error_VoiceGone, view.ErrorText);
        Assert.True(view.EndIsRetry);
    }

    [Theory]
    [InlineData(HttpErrorKind.ServerError)]
    [InlineData(HttpErrorKind.OtherClientError)]
    [InlineData(HttpErrorKind.BadRequest)]
    public void Failed_row_with_any_other_answer_shows_the_general_server_strip(HttpErrorKind kind)
    {
        LocalizedPackStatus.RowView view = LocalizedPackStatus.ForRow(PackInstallState.Failed, 0, kind, true, Size);

        Assert.Equal(AppResources.Error_Server, view.ErrorText);
    }

    [Fact]
    public void Failed_row_without_an_answer_keeps_the_general_failed_text_in_the_strip()
    {
        LocalizedPackStatus.RowView view = LocalizedPackStatus.ForRow(PackInstallState.Failed, 0, null, true, Size);

        Assert.Equal(AppResources.PackStatus_Failed, view.ErrorText);
    }

    [Fact]
    public void Invalid_pack_row_offers_no_retry_when_the_installer_cannot_retry()
    {
        LocalizedPackStatus.RowView view = LocalizedPackStatus.ForRow(PackInstallState.InvalidPack, 0, null, false, Size);

        Assert.Null(view.EndText);
        Assert.False(view.EndIsRetry);
        Assert.Equal(AppResources.PackStatus_InvalidPack, view.ErrorText);
    }

    [Fact]
    public void Stopped_row_says_so_on_its_second_line_and_offers_download()
    {
        LocalizedPackStatus.RowView view = LocalizedPackStatus.ForRow(PackInstallState.Stopped, 0, null, true, Size);

        Assert.Equal(AppResources.PackStatus_Stopped, view.Detail);
        Assert.Equal(AppResources.Language_Download, view.EndText);
        Assert.Null(view.ErrorText);
    }

    [Fact]
    public void Size_line_rounds_to_megabytes_and_is_null_when_unknown()
    {
        Assert.Equal(AppResources.Format(AppResources.Pack_RowSize, "38"), LocalizedPackStatus.SizeLine(38_400_000));
        Assert.Equal(AppResources.Format(AppResources.Pack_RowSize, "1"), LocalizedPackStatus.SizeLine(10));
        Assert.Null(LocalizedPackStatus.SizeLine(null));
    }
}
