using System.Net;
using System.Text;
using Sirous.Reading.Application.Net;
using Sirous.Reading.Application.Reading;
using Xunit;

namespace ReadTheStupidText.Tests.Net;

public class HttpErrorClassifierTests
{
    [Theory]
    [InlineData(null, HttpErrorKind.Offline)]
    [InlineData(400, HttpErrorKind.BadRequest)]
    [InlineData(401, HttpErrorKind.Unauthorized)]
    [InlineData(402, HttpErrorKind.PaymentRequired)]
    [InlineData(403, HttpErrorKind.Forbidden)]
    [InlineData(404, HttpErrorKind.NotFound)]
    [InlineData(429, HttpErrorKind.TooManyRequests)]
    [InlineData(500, HttpErrorKind.ServerError)]
    [InlineData(502, HttpErrorKind.ServerError)]
    [InlineData(503, HttpErrorKind.ServerError)]
    [InlineData(599, HttpErrorKind.ServerError)]
    [InlineData(405, HttpErrorKind.OtherClientError)]
    [InlineData(410, HttpErrorKind.OtherClientError)]
    [InlineData(451, HttpErrorKind.OtherClientError)]
    public void Every_status_maps_to_its_kind(int? status, HttpErrorKind expected) =>
        Assert.Equal(expected, HttpErrorClassifier.Classify(status));

    private static ReadOnlyMemory<byte> Body(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void A_400_carries_the_trimmed_message()
    {
        HttpError error = HttpErrorClassifier.Classify(400, Body("{\"message\":\"  The id is not valid.  \"}"));

        Assert.Equal(HttpErrorKind.BadRequest, error.Kind);
        Assert.Equal("The id is not valid.", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"message\":5}")]
    [InlineData("{\"message\":\"   \"}")]
    [InlineData("{\"code\":\"bad\"}")]
    [InlineData("{\"message\":\"Blocked by a proxy.\",\"ray\":\"abc\"}")]
    public void A_400_with_no_or_bad_body_has_no_message(string body)
    {
        HttpError error = HttpErrorClassifier.Classify(400, Body(body));

        Assert.Equal(HttpErrorKind.BadRequest, error.Kind);
        Assert.Null(error.Message);
    }

    [Fact]
    public void A_long_message_is_cut()
    {
        string text = new('x', HttpErrorClassifier.MaxMessageLength + 50);

        HttpError error = HttpErrorClassifier.Classify(400, Body($"{{\"message\":\"{text}\"}}"));

        Assert.Equal(HttpErrorClassifier.MaxMessageLength, error.Message!.Length);
    }

    [Theory]
    [InlineData(402)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(503)]
    public void A_body_on_any_other_status_is_ignored(int status)
    {
        HttpError error = HttpErrorClassifier.Classify(status, Body("{\"message\":\"Do not show this.\"}"));

        Assert.Null(error.Message);
    }

    [Fact]
    public void An_exception_with_a_status_is_classified_by_it()
    {
        var refused = new HttpRequestException("x", null, HttpStatusCode.TooManyRequests);

        Assert.Equal(HttpErrorKind.TooManyRequests, HttpErrorClassifier.Classify(refused));
    }

    [Fact]
    public void A_connection_failure_or_timeout_is_offline()
    {
        Assert.Equal(HttpErrorKind.Offline, HttpErrorClassifier.Classify(new HttpRequestException("no route")));
        Assert.Equal(HttpErrorKind.Offline, HttpErrorClassifier.Classify(new TimeoutException()));
        Assert.Equal(HttpErrorKind.Offline, HttpErrorClassifier.Classify(new TaskCanceledException()));
    }

    [Fact]
    public void A_status_wrapped_in_another_exception_is_still_found()
    {
        var wrapped = new AggregateException(new IOException("outer", new HttpRequestException("x", null, HttpStatusCode.ServiceUnavailable)));

        Assert.Equal(HttpErrorKind.ServerError, HttpErrorClassifier.Classify(wrapped));
    }

    [Fact]
    public void A_failure_that_is_not_about_the_server_has_no_kind()
    {
        Assert.Null(HttpErrorClassifier.Classify(new InvalidDataException("bad zip")));
        Assert.Null(HttpErrorClassifier.Classify(new UnauthorizedAccessException()));
    }

    [Fact]
    public void A_pack_failure_finds_the_status_in_the_chain()
    {
        var wrapped = new InvalidOperationException("outer", new HttpRequestException("x", null, HttpStatusCode.NotFound));

        Assert.Equal(HttpErrorKind.NotFound, PackErrorClassifier.ClassifyHttp(wrapped));
        Assert.Null(PackErrorClassifier.ClassifyHttp(new TimeoutException()));
        Assert.Null(PackErrorClassifier.ClassifyHttp(new HttpRequestException("no route")));
    }
}
