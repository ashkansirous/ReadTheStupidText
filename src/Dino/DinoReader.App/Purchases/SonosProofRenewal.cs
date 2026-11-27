using System.Text.Json;
using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// The pure part of keeping a linked Sonos home's subscription proof current (Slice 439, C4): the proof the store gives changes when the subscription renews (an iOS transaction id) or its plan changes, so the app
/// sends the Worker the old and the new proof and the Worker swaps the one it holds. No network and no Preferences here, so it is unit-tested; <see cref="SonosLinkedProof"/> does the sending.
/// </summary>
public static class SonosProofRenewal
{
    private const string OperationField = "op";
    private const string RenewOperation = "renew";
    private const string OldProofField = "oldProof";
    private const string NewProofField = "newProof";
    private const int StatusOk = 200;
    private const int StatusBadRequest = 400;
    private const int StatusForbidden = 403;
    private const int StatusNotFound = 404;
    private const int StatusTooManyRequests = 429;
    private const int StatusUnavailable = 503;
    private const int StatusConflict = 409;
    private const string ProofTakenReason = "proof_taken";
    private const string PaidBooksOffReason = "paid_books_off";

    // The Worker's own reason for a 404 on renew (worker/sonos-link.ts NO_HOME_ERROR). A 404 from anywhere else (a Worker not deployed yet, a proxy) is not "the home is gone".
    private const string UnknownHomeReason = "unknown_home";

    /// <summary>What the app does with the Worker's answer to a renew call.</summary>
    public enum Outcome
    {
        /// <summary>The Worker holds the new proof now: remember it as the linked one.</summary>
        Renewed,

        /// <summary>The Worker said no home holds the old proof any more (<c>unknown_home</c>: unlinked, replaced by a newer link, or never linked with it): stop sending renewals for it.</summary>
        HomeGone,

        /// <summary>No answer, or an error: keep the old proof and send again after the next store read.</summary>
        TryLater,

        /// <summary>Another home already holds the new proof (<c>proof_taken</c>, 409): sending it again changes nothing, so do not retry until the store gives a different proof.</summary>
        ProofTaken,

        /// <summary>The Worker's paid books are switched off (<c>paid_books_off</c>): nothing to swap, and the old proof stays the linked one. Not retried until the store gives a different proof.</summary>
        PaidBooksOff,

        /// <summary>The Worker refused this proof (400 not a subscription's, 403 the store does not confirm it): the same proof gets the same answer, so it is not retried until <see cref="SettledFor"/> has passed or the proof changes.</summary>
        Refused,
    }

    /// <summary>True for an outcome the app must not repeat for the same current proof until <see cref="SettledFor"/> has passed (the answer would be the same, so the renew is not posted on every store read).</summary>
    public static bool Settled(Outcome outcome) => outcome is Outcome.ProofTaken or Outcome.PaidBooksOff or Outcome.Refused;

    /// <summary>How long a settled answer holds for the same proof (review 3, finding 4): a different proof ends it at once, and so does this time, so a Worker switched on again or a store that confirms later is asked once more.</summary>
    public static readonly TimeSpan SettledFor = TimeSpan.FromHours(6);

    /// <summary>As <see cref="Needed(string?, string?)"/>, and false when the Worker answered this very proof with a settled outcome (<paramref name="settledProof"/>) less than <see cref="SettledFor"/> before <paramref name="now"/>.</summary>
    public static bool Needed(string? linkedProof, string? currentProof, string? settledProof, DateTimeOffset? settledAt, DateTimeOffset now) =>
        Needed(linkedProof, currentProof) && !(currentProof == settledProof && settledAt is { } at && now - at < SettledFor && now >= at);

    /// <summary>Slice 439 (review 3, finding 2): only a read that reached the store and answered about the subscription with "not entitled" kills the kept proof; a read with no subscription answer (offline, error, no reply) keeps it.</summary>
    public static bool ProofIsDead(bool reachable, bool hasInternet, SubscriptionStatus? answer) =>
        reachable && hasInternet && answer is { } status && !SubscriptionAccess.IsEntitled(status.State);

    /// <summary>True when the Worker's confirm call answered that the store is busy or cannot be asked (429, 503): the Link screen asks the grown-up to try again, it does not link without the proof (review 3, finding 1).</summary>
    public static bool StoreBusy(int? statusCode) => statusCode is StatusTooManyRequests or StatusUnavailable;

    /// <summary>The JSON the app posts to the Worker's Sonos endpoint: the proof the Worker holds for the home and the one the store gives now.</summary>
    public static string Body(string oldProof, string newProof) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [OperationField] = RenewOperation,
            [OldProofField] = oldProof,
            [NewProofField] = newProof,
        });

    /// <summary>True when a home is linked with a subscription proof and the store now gives a different one. No linked proof (never linked, or linked without a subscription) or no current proof (no subscription) needs nothing.</summary>
    public static bool Needed(string? linkedProof, string? currentProof) =>
        !string.IsNullOrEmpty(linkedProof) && !string.IsNullOrEmpty(currentProof) && linkedProof != currentProof;

    /// <summary>
    /// The outcome by the HTTP status and body of the renew call (null status: the Worker could not be reached). Only a 404 whose body names <c>unknown_home</c> means the home is gone; every refusal
    /// keeps the old proof: 400 and 403 (not a subscription's, or the store does not confirm it) and 409 (held by another home) settle that proof for <see cref="SettledFor"/>; 429, 503 and no answer try again after the next store read.
    /// </summary>
    public static Outcome OutcomeOf(int? statusCode, string? body) => statusCode switch
    {
        StatusOk when Names(body, PaidBooksOffReason) => Outcome.PaidBooksOff,
        StatusOk => Outcome.Renewed,
        StatusConflict when Names(body, ProofTakenReason) => Outcome.ProofTaken,
        StatusNotFound when Names(body, UnknownHomeReason) => Outcome.HomeGone,
        StatusBadRequest or StatusForbidden => Outcome.Refused,
        _ => Outcome.TryLater,
    };

    private static bool Names(string? body, string reason) => body is not null && body.Contains(reason, StringComparison.Ordinal);
}
