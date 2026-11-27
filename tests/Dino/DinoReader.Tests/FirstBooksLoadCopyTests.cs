using DinoReader.Resources.Strings;
using Sirous.Reading.Application.Net;

namespace DinoReader.Tests;

/// <summary>Slice 453: the choice of copy for a first-books list load that did not finish.</summary>
public class FirstBooksLoadChoiceTests
{
    [Fact]
    public void The_grown_ups_cancel_shows_the_cancelled_copy_without_the_offline_badge()
    {
        FirstBooksLoadCopy copy = FirstBooksLoadCopy.ForCancellation(stoppedByGrownUp: true);

        Assert.Equal(FirstBooksLoadKind.Cancelled, copy.Kind);
        Assert.Equal(AppResources.FirstBooks_CancelledTitle, copy.Title);
        Assert.Equal(FirstBooksLoadCopy.CancelledMarker, copy.Marker);
        Assert.False(copy.ShowsOfflineBadge);
    }

    [Fact]
    public void A_cancellation_nobody_pressed_shows_the_failed_copy()
    {
        FirstBooksLoadCopy copy = FirstBooksLoadCopy.ForCancellation(stoppedByGrownUp: false);

        Assert.Equal(FirstBooksLoadKind.Failed, copy.Kind);
        Assert.Equal(FirstBooksLoadCopy.FailedMarker, copy.Marker);
    }

    [Fact]
    public void A_list_call_that_throws_shows_the_failed_copy_not_the_offline_copy()
    {
        FirstBooksLoadCopy copy = FirstBooksLoadCopy.ForFailure();

        Assert.Equal(AppResources.FirstBooks_FailedTitle, copy.Title);
        Assert.Equal(AppResources.FirstBooks_FailedBody, copy.Body);
        Assert.NotEqual(AppResources.FirstBooks_OfflineTitle, copy.Title);
        Assert.False(copy.ShowsOfflineBadge);
    }

    [Fact]
    public void No_answer_from_the_server_is_offline_with_the_badge()
    {
        foreach (HttpErrorKind? kind in new HttpErrorKind?[] { null, HttpErrorKind.Offline })
        {
            FirstBooksLoadCopy copy = FirstBooksLoadCopy.ForOfflineListing(kind);

            Assert.Equal(FirstBooksLoadKind.Offline, copy.Kind);
            Assert.Equal(AppResources.FirstBooks_OfflineTitle, copy.Title);
            Assert.Equal(FirstBooksLoadCopy.OfflineMarker, copy.Marker);
            Assert.True(copy.ShowsOfflineBadge);
        }
    }

    [Theory]
    [InlineData(HttpErrorKind.ServerError)]
    [InlineData(HttpErrorKind.NotFound)]
    [InlineData(HttpErrorKind.TooManyRequests)]
    public void A_server_that_answered_with_an_error_is_a_failed_load_not_offline(HttpErrorKind kind)
    {
        FirstBooksLoadCopy copy = FirstBooksLoadCopy.ForOfflineListing(kind);

        Assert.Equal(FirstBooksLoadKind.Failed, copy.Kind);
        Assert.False(copy.ShowsOfflineBadge);
    }
}

/// <summary>Slice 453: which error kind the first-books list call reports (the real mapping the store client uses).</summary>
public class StoreListErrorTests
{
    [Fact]
    public void A_listing_body_that_does_not_parse_is_a_server_error()
    {
        Assert.Equal(HttpErrorKind.ServerError, StoreListError.KindOf(new StoreListFormatException(new System.Text.Json.JsonException("bad"))));
    }

    [Fact]
    public void A_json_error_from_a_local_read_is_not_a_server_error()
    {
        Assert.Null(StoreListError.KindOf(new System.Text.Json.JsonException("local purchase file")));
    }

    [Fact]
    public void A_503_is_a_server_error_and_no_answer_is_offline()
    {
        Assert.Equal(HttpErrorKind.ServerError, StoreListError.KindOf(new HttpRequestException("x", null, System.Net.HttpStatusCode.ServiceUnavailable)));
        Assert.Equal(HttpErrorKind.Offline, StoreListError.KindOf(new HttpRequestException("no network")));
        Assert.Equal(HttpErrorKind.Offline, StoreListError.KindOf(new TimeoutException()));
    }
}

/// <summary>Slice 453: a cancelled or failed first-books list load has its own words in every language, never the offline copy.</summary>
public partial class LocalizedUiTests
{
    private static readonly string[] LoadCopyKeys =
    [
        "FirstBooks_CancelledTitle", "FirstBooks_CancelledBody", "FirstBooks_FailedTitle", "FirstBooks_FailedBody",
    ];

    [Fact]
    public void A_cancelled_or_failed_book_list_load_has_its_own_words_in_every_language()
    {
        foreach (string file in LanguageFiles().Append(NeutralFile))
        {
            var strings = Load(file);
            string[] offline = ["FirstBooks_OfflineTitle", "FirstBooks_OfflineBody"];
            foreach (string key in LoadCopyKeys)
            {
                Assert.True(strings.TryGetValue(key, out string? text) && !string.IsNullOrWhiteSpace(text), $"{file}: {key} is missing");
                Assert.DoesNotContain(text!, offline.Select(k => strings[k]));
            }

            Assert.NotEqual(strings["FirstBooks_CancelledTitle"], strings["FirstBooks_FailedTitle"]);
        }
    }

    [Fact]
    public void The_cancelled_and_failed_words_are_translated_not_left_in_english()
    {
        var neutral = Load(NeutralFile);
        foreach (string file in LanguageFiles())
        {
            var strings = Load(file);
            foreach (string key in LoadCopyKeys)
            {
                Assert.NotEqual(neutral[key], strings[key]);
            }
        }
    }
}
