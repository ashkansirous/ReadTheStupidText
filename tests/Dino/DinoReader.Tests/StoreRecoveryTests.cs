using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json;
using DinoReader.Core;
using Sirous.Reading.Application.Usage;

namespace DinoReader.Tests;

/// <summary>Slice 460: the closed list of store failure causes and the store's automatic retry (1 s, 3 s, 7 s), including Cancel during a retry.</summary>
public sealed class StoreRecoveryTests
{
    [Fact]
    public void Every_cause_has_its_own_reason_on_the_closed_list_and_only_Other_is_the_old_store_failed()
    {
        string[] reasons = [.. Enum.GetValues<StoreCause>().Select(StoreCauses.Reason)];

        Assert.Equal(reasons.Length, reasons.Distinct().Count());
        Assert.All(reasons, reason => Assert.NotNull(FallbackUsage.Detail(FallbackArea.Store, reason)));
        Assert.Equal(FallbackReason.StoreFailed, StoreCauses.Reason(StoreCause.Other));
        Assert.DoesNotContain(FallbackReason.StoreFailed, Enum.GetValues<StoreCause>().Where(c => c != StoreCause.Other).Select(StoreCauses.Reason));
        Assert.DoesNotContain(FallbackReason.StoreUnreachable, reasons);
    }

    [Fact]
    public void A_timeout_or_a_cancelled_call_is_a_timeout()
    {
        Assert.Equal(StoreCause.Timeout, StoreCauses.Of(new TimeoutException()));
        Assert.Equal(StoreCause.Timeout, StoreCauses.Of(new TaskCanceledException()));
        Assert.Equal(StoreCause.Timeout, StoreCauses.Of(new OperationCanceledException()));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, StoreCause.Http5xx)]
    [InlineData(HttpStatusCode.BadGateway, StoreCause.Http5xx)]
    [InlineData(HttpStatusCode.ServiceUnavailable, StoreCause.Http5xx)]
    [InlineData(HttpStatusCode.NotFound, StoreCause.Http4xx)]
    [InlineData(HttpStatusCode.TooManyRequests, StoreCause.Http4xx)]
    [InlineData(HttpStatusCode.RequestTimeout, StoreCause.Timeout)]
    [InlineData(HttpStatusCode.Redirect, StoreCause.Other)]
    public void An_HTTP_status_is_a_5xx_or_a_4xx_cause(HttpStatusCode status, StoreCause expected) =>
        Assert.Equal(expected, StoreCauses.Of(new HttpRequestException("x", null, status)));

    [Theory]
    [InlineData(HttpRequestError.NameResolutionError, StoreCause.Dns)]
    [InlineData(HttpRequestError.SecureConnectionError, StoreCause.Tls)]
    [InlineData(HttpRequestError.ConnectionError, StoreCause.NoNetwork)]
    [InlineData(HttpRequestError.Unknown, StoreCause.Other)]
    public void An_HTTP_request_error_names_the_cause(HttpRequestError error, StoreCause expected) =>
        Assert.Equal(expected, StoreCauses.Of(new HttpRequestException(error, "x")));

    [Fact]
    public void A_socket_or_TLS_failure_wrapped_in_another_exception_is_still_found()
    {
        Assert.Equal(StoreCause.Dns, StoreCauses.Of(new HttpRequestException("x", new SocketException((int)SocketError.HostNotFound))));
        Assert.Equal(StoreCause.NoNetwork, StoreCauses.Of(new IOException("x", new SocketException((int)SocketError.ConnectionReset))));
        Assert.Equal(StoreCause.Tls, StoreCauses.Of(new IOException("x", new AuthenticationException("x"))));
    }

    [Fact]
    public void A_bad_body_or_an_unknown_exception_is_other()
    {
        Assert.Equal(StoreCause.Other, StoreCauses.Of(new JsonException("x")));
        Assert.Equal(StoreCause.Other, StoreCauses.Of(new InvalidOperationException("x")));
    }

    [Fact]
    public void A_bad_body_wrapped_in_a_network_exception_stays_other()
    {
        Assert.Equal(StoreCause.Other, StoreCauses.Of(new HttpRequestException("x", new JsonException("x"))));
        Assert.Equal(StoreCause.Other, StoreCauses.Of(new IOException("x", new JsonException("x"))));
        Assert.True(StoreCauses.HasJsonException(new HttpRequestException("x", new JsonException("x"))));
    }

    [Fact]
    public void An_Android_Java_network_error_inside_the_HTTP_exception_names_the_cause()
    {
        Assert.Equal(StoreCause.Dns, StoreCauses.Of(Android(new Java.Net.UnknownHostException("Unable to resolve host"))));
        Assert.Equal(StoreCause.Timeout, StoreCauses.Of(Android(new Java.Net.SocketTimeoutException("timeout"))));
        Assert.Equal(StoreCause.NoNetwork, StoreCauses.Of(Android(new Java.Net.ConnectException("refused"))));
        Assert.Equal(StoreCause.NoNetwork, StoreCauses.Of(Android(new Java.IO.IOException("Connection reset"))));
        Assert.Equal(StoreCause.Tls, StoreCauses.Of(Android(new Javax.Net.Ssl.SSLHandshakeException("handshake"))));
    }

    [Fact]
    public void An_iOS_URL_error_inside_the_HTTP_exception_names_the_cause_from_its_properties_or_its_message()
    {
        Assert.Equal(StoreCause.NoNetwork, StoreCauses.Of(Ios(new Foundation.WithProperties.NSErrorExceptionWithProperties("NSURLErrorDomain", -1009))));
        Assert.Equal(StoreCause.Timeout, StoreCauses.Of(Ios(new Foundation.WithProperties.NSErrorExceptionWithProperties("NSURLErrorDomain", -1001))));
        Assert.Equal(StoreCause.Dns, StoreCauses.Of(Ios(new Foundation.NSErrorException("NSURLErrorDomain", -1003))));
        Assert.Equal(StoreCause.Tls, StoreCauses.Of(Ios(new Foundation.NSErrorException("NSURLErrorDomain", -1200))));
    }

    [Fact]
    public void An_iOS_error_whose_Code_is_a_native_integer_is_read_without_throwing()
    {
        Assert.Equal(StoreCause.NoNetwork, StoreCauses.Of(Ios(new Foundation.WithProperties.NSErrorExceptionWithNativeCode("NSURLErrorDomain", -1009))));
        Assert.Equal(StoreCause.Other, StoreCauses.Of(Ios(new Foundation.WithProperties.NSErrorExceptionWithNativeCode("NSURLErrorDomain", -1011))));
    }

    [Fact]
    public async Task A_hung_server_ends_the_retries_inside_the_budget()
    {
        Recorder recorder = new();
        TimeSpan now = TimeSpan.Zero;

        int result = await StoreRetry.RunAsync(
            last => { now += StoreRetry.AttemptTimeout(recorder.Attempts, TimeSpan.FromSeconds(20), now); return recorder.Attempt(last, failures: 99); },
            failed: value => value < 0,
            CancellationToken.None,
            (span, _) => { now += span; return Task.CompletedTask; },
            elapsed: () => now);

        Assert.Equal(-1, result);
        Assert.True(recorder.Attempts < 4);
        Assert.True(now <= StoreRetry.Budget);
    }

    [Fact]
    public async Task Quick_failures_still_get_all_four_attempts_inside_the_budget()
    {
        Recorder recorder = new();

        await StoreRetry.RunAsync(last => recorder.Attempt(last, failures: 99), failed: value => value < 0, CancellationToken.None, recorder.Delay, elapsed: () => TimeSpan.FromSeconds(2));

        Assert.Equal(4, recorder.Attempts);
    }

    [Fact]
    public void A_retried_attempt_waits_less_than_the_first_and_never_past_the_budget()
    {
        Assert.Equal(TimeSpan.FromSeconds(20), StoreRetry.AttemptTimeout(0, TimeSpan.FromSeconds(20), TimeSpan.Zero));
        Assert.Equal(StoreRetry.RetryAttemptTimeout, StoreRetry.AttemptTimeout(1, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(21)));
        Assert.Equal(TimeSpan.FromSeconds(3), StoreRetry.AttemptTimeout(2, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(27)));
        Assert.Equal(TimeSpan.FromSeconds(1), StoreRetry.AttemptTimeout(3, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(40)));
    }

    [Fact]
    public void An_iOS_error_outside_the_URL_domain_or_an_unlisted_code_stays_other()
    {
        Assert.Equal(StoreCause.Other, StoreCauses.Of(Ios(new Foundation.NSErrorException("NSCocoaErrorDomain", -1009))));
        Assert.Equal(StoreCause.Other, StoreCauses.Of(Ios(new Foundation.NSErrorException("NSURLErrorDomain", -1011))));
    }

    [Theory]
    [InlineData(StoreCause.Timeout, true)]
    [InlineData(StoreCause.NoNetwork, true)]
    [InlineData(StoreCause.Dns, true)]
    [InlineData(StoreCause.Tls, true)]
    [InlineData(StoreCause.Http5xx, true)]
    [InlineData(StoreCause.NoWindow, true)]
    [InlineData(StoreCause.Http4xx, false)]
    [InlineData(StoreCause.NotAssociated, false)]
    [InlineData(StoreCause.SettleTimeout, false)]
    [InlineData(StoreCause.Other, false)]
    public void Only_a_cause_that_can_pass_by_itself_is_retried(StoreCause cause, bool expected) =>
        Assert.Equal(expected, StoreCauses.IsTransient(cause));

    private static HttpRequestException Android(Exception java) => new("native", java);

    private static HttpRequestException Ios(Exception foundation) => new("native", foundation);

    [Fact]
    public void Only_the_Store_not_associated_error_is_its_own_cause()
    {
        Assert.Equal(StoreCause.NotAssociated, StoreCauses.OfStoreHResult(unchecked((int)0x803F6107)));
        Assert.Equal(StoreCause.Other, StoreCauses.OfStoreHResult(unchecked((int)0x80004005)));
    }

    [Fact]
    public void The_schedule_is_one_three_and_seven_seconds()
    {
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(7)], StoreRetry.Backoff);
    }

    [Fact]
    public async Task A_call_that_succeeds_at_once_waits_for_nothing()
    {
        Recorder recorder = new();

        int result = await StoreRetry.RunAsync(last => recorder.Attempt(last, failures: 0), failed: value => value < 0, CancellationToken.None, recorder.Delay);

        Assert.Equal(1, result);
        Assert.Equal(1, recorder.Attempts);
        Assert.Empty(recorder.Waits);
    }

    [Fact]
    public async Task A_call_that_recovers_is_tried_again_after_each_gap_and_never_marked_last()
    {
        Recorder recorder = new();
        List<int> retries = [];

        int result = await StoreRetry.RunAsync(last => recorder.Attempt(last, failures: 2), failed: value => value < 0, CancellationToken.None, recorder.Delay, retries.Add);

        Assert.Equal(3, result);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)], recorder.Waits);
        Assert.Equal([2, 3], retries);
        Assert.DoesNotContain(true, recorder.LastFlags);
    }

    [Fact]
    public async Task A_call_that_never_recovers_runs_four_times_and_only_the_fourth_is_last()
    {
        Recorder recorder = new();

        int result = await StoreRetry.RunAsync(last => recorder.Attempt(last, failures: 99), failed: value => value < 0, CancellationToken.None, recorder.Delay);

        Assert.Equal(-1, result);
        Assert.Equal(4, recorder.Attempts);
        Assert.Equal(StoreRetry.Backoff, recorder.Waits);
        Assert.Equal([false, false, false, true], recorder.LastFlags);
    }

    [Fact]
    public async Task Cancel_during_a_retry_wait_ends_the_run_at_once_with_no_further_attempt()
    {
        Recorder recorder = new();
        using CancellationTokenSource cancel = new();

        Task<int> run = StoreRetry.RunAsync(last => recorder.Attempt(last, failures: 99), failed: value => value < 0, cancel.Token, (_, token) => Task.Delay(Timeout.Infinite, token));
        await recorder.FirstAttempt.Task;
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(1, recorder.Attempts);
    }

    [Fact]
    public async Task Cancel_before_the_next_attempt_stops_it_from_starting()
    {
        Recorder recorder = new();
        using CancellationTokenSource cancel = new();

        Task<int> run = StoreRetry.RunAsync(last => recorder.Attempt(last, failures: 99), failed: value => value < 0, cancel.Token, async (_, _) => await cancel.CancelAsync());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(1, recorder.Attempts);
    }

    [Theory]
    [InlineData(HttpRequestError.ConnectionError, StoreCause.NoNetwork, true)]
    [InlineData(HttpRequestError.NameResolutionError, StoreCause.Dns, true)]
    [InlineData(HttpRequestError.SecureConnectionError, StoreCause.Tls, true)]
    [InlineData(HttpRequestError.Unknown, StoreCause.Other, true)]
    [InlineData(HttpRequestError.InvalidResponse, StoreCause.Other, false)]
    [InlineData(HttpRequestError.ProxyTunnelError, StoreCause.Other, false)]
    public void A_named_transport_error_maps_to_its_cause_and_an_unnamed_one_is_other_but_retried(HttpRequestError error, StoreCause cause, bool retried)
    {
        HttpRequestException ex = new(error, "x");

        Assert.Equal(cause, StoreCauses.Of(ex));
        Assert.Equal(retried, StoreCauses.IsRetryable(ex));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, StoreCause.Http5xx, true)]
    [InlineData(HttpStatusCode.NotFound, StoreCause.Http4xx, false)]
    [InlineData(HttpStatusCode.TooManyRequests, StoreCause.Http4xx, true)]
    public void An_http_status_maps_to_4xx_or_5xx(HttpStatusCode status, StoreCause cause, bool retried)
    {
        HttpRequestException ex = new("x", null, status);

        Assert.Equal(cause, StoreCauses.Of(ex));
        Assert.Equal(retried, StoreCauses.IsRetryable(ex));
    }

    [Fact]
    public void A_bare_io_failure_is_other_and_retried_and_a_bare_socket_failure_is_a_network_cause()
    {
        IOException io = new("x");
        SocketException socket = new((int)SocketError.ConnectionReset);

        Assert.Equal(StoreCause.Other, StoreCauses.Of(io));
        Assert.True(StoreCauses.IsRetryable(io));
        Assert.Equal(StoreCause.NoNetwork, StoreCauses.Of(socket));
        Assert.True(StoreCauses.IsRetryable(socket));
    }

    [Fact]
    public void A_json_or_bad_body_failure_is_other_and_not_retried_even_inside_a_transport_exception()
    {
        JsonException json = new("x");
        HttpRequestException wrapped = new(HttpRequestError.Unknown, "x", json);

        Assert.Equal(StoreCause.Other, StoreCauses.Of(json));
        Assert.False(StoreCauses.IsRetryable(json));
        Assert.Equal(StoreCause.Other, StoreCauses.Of(wrapped));
        Assert.False(StoreCauses.IsRetryable(wrapped));
    }

    [Fact]
    public void A_429_reports_store_http_4xx_and_is_still_retried()
    {
        HttpRequestException limited = new("x", null, HttpStatusCode.TooManyRequests);

        Assert.Equal(FallbackReason.StoreHttp4xx, StoreCauses.Reason(StoreCauses.Of(limited)));
        Assert.False(StoreCauses.IsTransient(StoreCauses.Of(limited)));
        Assert.True(StoreCauses.IsTooManyRequests(limited));
        Assert.True(StoreCauses.IsTooManyRequests(new IOException("x", limited)));
        Assert.False(StoreCauses.IsTooManyRequests(new HttpRequestException("x", null, HttpStatusCode.NotFound)));
    }

    // One first-run list sequence as StoreClient.ListWithRetryAsync runs it: retried, then its final failure goes through the session flag; a success or a Cancel reports nothing.
    private static async Task<bool> FirstRunSequence(FirstRunFailureOnce flag, List<string> rows, string name, bool succeeds, CancellationToken cancel)
    {
        bool failed = await StoreRetry.RunAsync(_ => Task.FromResult(!succeeds), failure => failure, cancel, (_, _) => Task.CompletedTask);
        if (failed)
        {
            flag.TryReport(() => { lock (rows) { rows.Add(name); } });
        }

        return !failed;
    }

    [Fact]
    public async Task A_failed_prefetch_and_a_failed_picker_report_once()
    {
        FirstRunFailureOnce flag = new();
        List<string> rows = [];

        await FirstRunSequence(flag, rows, "prefetch", succeeds: false, TestContext.Current.CancellationToken);
        await FirstRunSequence(flag, rows, "picker", succeeds: false, TestContext.Current.CancellationToken);

        Assert.Equal(["prefetch"], rows);
    }

    [Fact]
    public async Task A_failed_prefetch_and_a_picker_that_succeeds_report_the_prefetch_failure_once()
    {
        FirstRunFailureOnce flag = new();
        List<string> rows = [];

        await FirstRunSequence(flag, rows, "prefetch", succeeds: false, TestContext.Current.CancellationToken);
        await FirstRunSequence(flag, rows, "picker", succeeds: true, TestContext.Current.CancellationToken);

        Assert.Equal(["prefetch"], rows);
    }

    [Fact]
    public async Task A_picker_that_is_cancelled_reports_nothing_and_a_success_leaves_the_flag_free()
    {
        FirstRunFailureOnce flag = new();
        List<string> rows = [];
        using CancellationTokenSource cancel = new();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FirstRunSequence(flag, rows, "picker", succeeds: false, cancel.Token));
        await FirstRunSequence(flag, rows, "prefetch", succeeds: true, TestContext.Current.CancellationToken);
        Assert.Empty(rows);

        await FirstRunSequence(flag, rows, "later", succeeds: false, TestContext.Current.CancellationToken);
        Assert.Equal(["later"], rows);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(3, 7)]
    [InlineData(4, 0)]
    public void The_wait_after_an_attempt_follows_the_1_3_7_schedule_and_ends_after_the_last(int attempt, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), StoreRetry.NextWait(attempt, TimeSpan.Zero));
    }

    [Fact]
    public void No_wait_is_scheduled_when_it_would_start_after_the_budget()
    {
        Assert.Equal(TimeSpan.Zero, StoreRetry.NextWait(3, TimeSpan.FromSeconds(24)));
    }

    [Fact]
    public void The_attempt_and_outcome_lines_have_the_marker_form()
    {
        Assert.Equal("[Store] list attempt=2 source=prefetch cause=NoNetwork nextWait=3", StoreRetry.AttemptLine(2, "prefetch", "NoNetwork", TimeSpan.FromSeconds(3)));
        Assert.Equal("[Store] list failure source=picker outcome=suppressed", StoreRetry.OutcomeLine("picker", "suppressed"));
    }

    [Fact]
    public async Task Concurrent_failed_sequences_report_once()
    {
        FirstRunFailureOnce flag = new();
        List<string> rows = [];

        await Task.WhenAll(Enumerable.Range(0, 32).Select(n => Task.Run(() => FirstRunSequence(flag, rows, $"s{n}", succeeds: false, TestContext.Current.CancellationToken))));

        Assert.Single(rows);
    }

    private sealed class Recorder
    {
        public int Attempts { get; private set; }

        public List<TimeSpan> Waits { get; } = [];

        public List<bool> LastFlags { get; } = [];

        public TaskCompletionSource FirstAttempt { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Fails (-1) for the first <paramref name="failures"/> calls, then answers the number of the call.</summary>
        public Task<int> Attempt(bool last, int failures)
        {
            Attempts++;
            LastFlags.Add(last);
            FirstAttempt.TrySetResult();
            return Task.FromResult(Attempts <= failures ? -1 : Attempts);
        }

        public Task Delay(TimeSpan span, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            Waits.Add(span);
            return Task.CompletedTask;
        }
    }
}
