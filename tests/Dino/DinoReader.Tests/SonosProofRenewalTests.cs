using System.Text.Json;
using DinoReader;
using DinoReader.Core;
using Xunit;

namespace DinoReader.Tests;

// Slice 439 C4: keeping a linked Sonos home's subscription proof current at the Worker.
public class SonosProofRenewalTests
{
    private const string Old = "ios:uk.sirous.dinoreader.sub.monthly:txn-100001";
    private const string Renewed = "ios:uk.sirous.dinoreader.sub.monthly:txn-100002";

    [Fact]
    public void Body_NamesTheRenewOperationAndBothProofs()
    {
        using JsonDocument body = JsonDocument.Parse(SonosProofRenewal.Body(Old, Renewed));
        Assert.Equal("renew", body.RootElement.GetProperty("op").GetString());
        Assert.Equal(Old, body.RootElement.GetProperty("oldProof").GetString());
        Assert.Equal(Renewed, body.RootElement.GetProperty("newProof").GetString());
    }

    [Fact]
    public void Needed_OnlyWhenALinkedProofDiffersFromTheOneTheStoreGivesNow()
    {
        Assert.True(SonosProofRenewal.Needed(Old, Renewed));
        Assert.False(SonosProofRenewal.Needed(Old, Old));
    }

    [Fact]
    public void ProofIsDead_OnlyWhenAReachableReadSaysNotEntitled()
    {
        SubscriptionStatus lapsed = new(SubscriptionState.Expired, null, null);
        SubscriptionStatus active = new(SubscriptionState.Active, null, null);
        Assert.True(SonosProofRenewal.ProofIsDead(true, true, lapsed));
        Assert.False(SonosProofRenewal.ProofIsDead(true, true, null)); // no subscription answer: keep the proof
        Assert.False(SonosProofRenewal.ProofIsDead(false, true, lapsed)); // store not reached
        Assert.False(SonosProofRenewal.ProofIsDead(true, false, lapsed)); // offline
        Assert.False(SonosProofRenewal.ProofIsDead(true, true, active));
    }

    [Theory]
    [InlineData(null, Renewed)]
    [InlineData("", Renewed)]
    [InlineData(Old, null)]
    [InlineData(Old, "")]
    [InlineData(null, null)]
    public void Needed_NothingWhenNoHomeIsLinkedWithAProofOrTheStoreHasNoSubscription(string? linked, string? current) =>
        Assert.False(SonosProofRenewal.Needed(linked, current));

    [Fact]
    public void Settled_TakenPaidBooksOffAndRefusedAreNotRetriedForTheSameProof()
    {
        Assert.True(SonosProofRenewal.Settled(SonosProofRenewal.Outcome.ProofTaken));
        Assert.True(SonosProofRenewal.Settled(SonosProofRenewal.Outcome.PaidBooksOff));
        Assert.True(SonosProofRenewal.Settled(SonosProofRenewal.Outcome.Refused)); // review 3, finding 3
        Assert.False(SonosProofRenewal.Settled(SonosProofRenewal.Outcome.TryLater));
        Assert.False(SonosProofRenewal.Settled(SonosProofRenewal.Outcome.Renewed));
    }

    [Fact]
    public void Needed_ASettledProofWaitsOnlyUntilTheProofChangesOrTheSettledTimePasses()
    {
        DateTimeOffset at = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);
        Assert.False(SonosProofRenewal.Needed(Old, Renewed, Renewed, at, at.AddMinutes(5))); // answered for this proof a moment ago: wait
        Assert.True(SonosProofRenewal.Needed(Old, Renewed, "ios:uk.sirous.dinoreader.sub.monthly:txn-1", at, at.AddMinutes(5))); // the proof changed: ask again
        Assert.True(SonosProofRenewal.Needed(Old, Renewed, null, null, at));
        Assert.True(SonosProofRenewal.Needed(Old, Renewed, Renewed, at, at + SonosProofRenewal.SettledFor)); // review 3, finding 4: never settled forever (the Worker may be switched on again)
        Assert.False(SonosProofRenewal.Needed(Old, Renewed, Renewed, at, at + SonosProofRenewal.SettledFor - TimeSpan.FromSeconds(1)));
        Assert.True(SonosProofRenewal.Needed(Old, Renewed, Renewed, at, at.AddHours(-1))); // a clock set back does not hold it
    }

    [Theory]
    [InlineData(429, true)]
    [InlineData(503, true)]
    [InlineData(400, false)]
    [InlineData(403, false)]
    [InlineData(200, false)]
    [InlineData(404, false)]
    [InlineData(null, false)]
    public void StoreBusy_OnlyForARateLimitOrAStoreThatCannotBeAsked(int? status, bool expected) =>
        Assert.Equal(expected, SonosProofRenewal.StoreBusy(status)); // review 3, finding 1

    [Theory]
    [InlineData(200,"{\"renewed\":1}", SonosProofRenewal.Outcome.Renewed)]
    [InlineData(404, "{\"error\":\"unknown_home\"}", SonosProofRenewal.Outcome.HomeGone)]
    [InlineData(404, "Not found", SonosProofRenewal.Outcome.TryLater)] // a Worker not deployed yet, or a proxy: not "the home is gone"
    [InlineData(404, null, SonosProofRenewal.Outcome.TryLater)]
    [InlineData(400, "{\"error\":\"bad_request\"}", SonosProofRenewal.Outcome.Refused)] // review 3, finding 3
    [InlineData(403, "{\"error\":\"subscription_inactive\"}", SonosProofRenewal.Outcome.Refused)]
    [InlineData(409, "{\"error\":\"proof_taken\"}", SonosProofRenewal.Outcome.ProofTaken)] // review 2, finding 7
    [InlineData(409, "conflict", SonosProofRenewal.Outcome.TryLater)]
    [InlineData(200, "{\"renewed\":0,\"status\":\"paid_books_off\"}", SonosProofRenewal.Outcome.PaidBooksOff)] // review 2, finding 2
    [InlineData(429, "{\"error\":\"rate_limited\"}", SonosProofRenewal.Outcome.TryLater)]
    [InlineData(503, "{\"error\":\"unavailable\"}", SonosProofRenewal.Outcome.TryLater)]
    [InlineData(null, null, SonosProofRenewal.Outcome.TryLater)]
    public void OutcomeOf_KeepsTheOldProofUnlessTheWorkerAnswersOkOrNamesTheHomeAsGone(int? status, string? body, SonosProofRenewal.Outcome expected) =>
        Assert.Equal(expected, SonosProofRenewal.OutcomeOf(status, body));
}
