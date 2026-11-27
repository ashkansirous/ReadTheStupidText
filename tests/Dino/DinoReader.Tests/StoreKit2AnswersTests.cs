using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 390: what the Swift StoreKit 2 wrapper's JSON answers become (prices, owned products, proofs, the subscription status, the buy outcome).</summary>
public class StoreKit2AnswersTests
{
    private const string Book = "uk.sirous.dinoreader.book.goldilocks";
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly long NowMs = Now.ToUnixTimeMilliseconds();
    private const long Day = 86_400_000;

    private static string Tx(string product, long purchased, long? expires = null, bool trial = false, string jws = "h.p.s") =>
        $$"""{"productId":"{{product}}","id":"1","originalId":"1","purchasedAt":{{purchased}},"expiresAt":{{(expires is { } e ? e.ToString() : "null")}},"trial":{{(trial ? "true" : "false")}},"revoked":false,"jws":"{{jws}}"}""";

    private static StoreKitEntitlements Read(string current, string latest = "") =>
        StoreKit2Answers.Entitlements($$"""{"ok":true,"transactions":[{{current}}],"latest":[{{latest}}]}""")!;

    [Fact]
    public void AReadWithAnUnverifiedEntry_IsFlagged_AndOwnsOnlyTheVerifiedOnes()
    {
        StoreKitEntitlements incomplete = StoreKit2Answers.Entitlements($$"""{"ok":true,"transactions":[{{Tx("new", NowMs)}}],"latest":[],"incomplete":true}""")!;

        Assert.True(incomplete.Incomplete);
        Assert.Equal(["new"], incomplete.Owned);
    }

    // Both plans carry the same flags (they share one subscription group); `eligible` null writes the JSON null, "absent" leaves the field out (an older wrapper).
    private static string Plans(string trial, string eligible)
    {
        string flags = "\"trial\":" + trial + (eligible == "absent" ? "" : ",\"eligible\":" + eligible);
        return "{\"ok\":true,\"products\":[{\"id\":\"" + SubscriptionProducts.Monthly + "\",\"price\":\"$1.99\"," + flags + "},{\"id\":\"" + SubscriptionProducts.Yearly + "\",\"price\":\"$14.99\"," + flags
            + "},{\"id\":\"" + Book + "\",\"price\":\"$3.99\",\"trial\":false,\"eligible\":null}],\"invalid\":[]}";
    }

    [Fact]
    public void TrialAnswer_IsTrue_WhenAPlanHasATrialThisAccountMayUse()
    {
        Assert.True(SubscriptionTrialAnswer.Read(Plans("true", "true")));
    }

    [Fact]
    public void TrialAnswer_IsFalse_WhenTheAccountUsedItsTrial()
    {
        Assert.False(SubscriptionTrialAnswer.Read(Plans("true", "false")));
    }

    [Fact]
    public void TrialAnswer_IsFalse_WhenEligibleButNoPlanHasATrialOffer()
    {
        Assert.False(SubscriptionTrialAnswer.Read(Plans("false", "true")));
    }

    [Fact]
    public void TrialAnswer_IsNull_WhenTheWrapperSaysNothingAboutEligibility()
    {
        Assert.Null(SubscriptionTrialAnswer.Read(Plans("true", "absent")));
        Assert.Null(SubscriptionTrialAnswer.Read(Plans("true", "null")));
    }

    [Fact]
    public void TrialAnswer_IsTrue_WhenOnePlanIsEligibleWithATrialAndTheOtherIsNot()
    {
        string json = "{\"ok\":true,\"products\":[{\"id\":\"" + SubscriptionProducts.Monthly + "\",\"trial\":true,\"eligible\":false},{\"id\":\"" + SubscriptionProducts.Yearly + "\",\"trial\":true,\"eligible\":true}]}";
        Assert.True(SubscriptionTrialAnswer.Read(json));
    }

    [Fact]
    public void TrialAnswer_IsNull_WhenEligibleIsNotABoolean()
    {
        string json = "{\"ok\":true,\"products\":[{\"id\":\"" + SubscriptionProducts.Monthly + "\",\"trial\":true,\"eligible\":\"true\"}]}";
        Assert.Null(SubscriptionTrialAnswer.Read(json));
    }

    [Fact]
    public void TrialAnswer_IsNull_WhenTheProductsHoldNoPlan()
    {
        Assert.Null(SubscriptionTrialAnswer.Read("{\"ok\":true,\"products\":[{\"id\":\"" + Book + "\",\"trial\":false,\"eligible\":true}]}"));
    }

    [Theory]
    [InlineData("""{"ok":true,"products":[],"invalid":[]}""")]
    [InlineData("""{"ok":false,"error":"x:1"}""")]
    [InlineData("not json")]
    public void TrialAnswer_IsNull_ForNoPlansOrAnUnreadableAnswer(string json)
    {
        Assert.Null(SubscriptionTrialAnswer.Read(json));
    }

    [Fact]
    public void ACompleteRead_IsNotFlagged()
    {
        Assert.False(Read(Tx("new", NowMs)).Incomplete);
    }

    [Fact]
    public void Prices_AreTheAppStoresOwnText_AndUnknownIdsAreListed()
    {
        var read = StoreKit2Answers.Prices("""{"ok":true,"products":[{"id":"a","price":"$1.99","trial":false}],"invalid":["b"]}""");
        Assert.NotNull(read);
        Assert.Equal("$1.99", read.Value.Prices["a"]);
        Assert.Equal(["b"], read.Value.Invalid);
    }

    [Theory]
    [InlineData("""{"ok":false,"error":"SKErrorDomain:2"}""")]
    [InlineData("not json")]
    [InlineData("""{"ok":true}""")]
    [InlineData("""{"ok":true,"transactions":null,"latest":[]}""")]
    public void AFailedOrUnreadableAnswer_IsNullNeverEmpty(string json)
    {
        Assert.Null(StoreKit2Answers.Prices(json));
        Assert.Null(StoreKit2Answers.Entitlements(json));
        Assert.Null(StoreKit2Answers.Buy(json));
    }

    [Fact]
    public void ErrorOfAFailedAnswer_IsItsDomainAndCode()
    {
        Assert.Equal("SKErrorDomain:2", StoreKit2Answers.Error("""{"ok":false,"error":"SKErrorDomain:2"}"""));
        Assert.Null(StoreKit2Answers.Error("not json"));
        Assert.True(StoreKit2Answers.IsOk("""{"ok":true}"""));
        Assert.False(StoreKit2Answers.IsOk("""{"ok":false,"error":"unsupported"}"""));
    }

    [Fact]
    public void ManageSubscriptionsAnswers_AreOkOrTheNoSceneAndUnsupportedWords()
    {
        Assert.True(StoreKit2Answers.IsOk("""{"ok":true}"""));
        Assert.Equal(StoreKit2Answers.NoScene, StoreKit2Answers.Error("""{"ok":false,"error":"no-scene"}"""));
        Assert.Equal(StoreKit2Answers.Unsupported, StoreKit2Answers.Error("""{"ok":false,"error":"unsupported"}"""));
        Assert.Equal("no-scene", StoreKit2Answers.NoScene); // the Swift wrapper writes this word; a drift would send a missing scene to the failed branch instead of the web page
    }

    [Fact]
    public void OwnedProducts_AndTheirProofs_AreTheNewestTransactionsJws()
    {
        StoreKitEntitlements owned = Read($"{Tx(Book, NowMs - 2 * Day, jws: "old")},{Tx(Book, NowMs - Day, jws: "new")},{Tx("uk.sirous.dinoreader.book.other", NowMs, jws: "x")}");
        Assert.Equal([Book, "uk.sirous.dinoreader.book.other"], owned.Owned.Order());
        Assert.Equal("new", owned.Proofs[Book]);
        Assert.Empty(owned.PurchasedAt); // only the subscription plans carry a date
    }

    [Fact]
    public void ATransactionWithNoSignedProof_UnlocksNothing()
    {
        StoreKitEntitlements owned = Read("""{"productId":"p","jws":""},{"jws":"x"}""");
        Assert.Empty(owned.Owned);
    }

    [Fact]
    public void ARunningPlan_IsActive_AndATrialIsATrial_WithTheStoresOwnEnd()
    {
        SubscriptionStatus active = Read(Tx(SubscriptionProducts.Monthly, NowMs - Day, NowMs + 29 * Day)).Subscription(Now);
        Assert.Equal(SubscriptionState.Active, active.State);
        Assert.Equal(Now + TimeSpan.FromDays(29), active.EndsAt);
        Assert.Equal(DeviceReadKind.Store, active.Kind);
        Assert.Equal(SubscriptionProducts.Monthly, active.ProductId);

        SubscriptionStatus trial = Read(Tx(SubscriptionProducts.Yearly, NowMs - Day, NowMs + 6 * Day, trial: true)).Subscription(Now);
        Assert.Equal(SubscriptionState.Trial, trial.State);
    }

    [Fact]
    public void AnEntitlementPastItsEnd_IsAppleBillingGrace_NotALapse()
    {
        SubscriptionStatus status = Read(Tx(SubscriptionProducts.Monthly, NowMs - 31 * Day, NowMs - Day)).Subscription(Now);
        Assert.Equal(SubscriptionState.InGrace, status.State);
    }

    [Fact]
    public void WithTwoPlans_TheLaterEndDecides()
    {
        SubscriptionStatus status = Read($"{Tx(SubscriptionProducts.Monthly, NowMs - Day, NowMs + 10 * Day)},{Tx(SubscriptionProducts.Yearly, NowMs - Day, NowMs + 300 * Day)}").Subscription(Now);
        Assert.Equal(SubscriptionProducts.Yearly, status.ProductId);
    }

    [Fact]
    public void ALapsedPlan_SeenOnlyAsTheLatestTransaction_IsExpired()
    {
        SubscriptionStatus status = Read(string.Empty, Tx(SubscriptionProducts.Monthly, NowMs - 40 * Day, NowMs - 10 * Day)).Subscription(Now);
        Assert.Equal(SubscriptionState.Expired, status.State);
        Assert.Equal(DeviceReadKind.Store, status.Kind);
    }

    [Fact]
    public void ARefundedPlan_IsNotOwned_AndKeepsNoFutureEndDate()
    {
        string refunded = Tx(SubscriptionProducts.Monthly, NowMs - 5 * Day, NowMs + 25 * Day).Replace("\"revoked\":false", "\"revoked\":true");
        StoreKitEntitlements read = Read(refunded, refunded); // even if the wrapper listed it as current, a revoked transaction owns nothing
        Assert.Empty(read.Owned);
        SubscriptionStatus status = read.Subscription(Now);
        Assert.Equal(SubscriptionState.Expired, status.State);
        Assert.Null(status.EndsAt);

        string lapsed = Tx(SubscriptionProducts.Yearly, NowMs - 400 * Day, NowMs - 35 * Day);
        Assert.Equal(SubscriptionProducts.Yearly, Read(string.Empty, refunded + "," + lapsed).Subscription(Now).ProductId); // a refunded plan never outranks a plain lapsed one
    }

    [Fact]
    public void NoPlanAtAll_IsNoInformation_SoTheHeldRecordIsLeftAlone()
    {
        SubscriptionStatus status = Read(Tx(Book, NowMs)).Subscription(Now);
        Assert.Equal(SubscriptionState.Unknown, status.State);
        Assert.Equal(DeviceReadKind.Estimated, status.Kind);
        Assert.Null(SubscriptionAccess.Merge(null, new SubscriptionAnswer.DeviceRead(status), Now));
    }

    [Theory]
    [InlineData("bought", true)]
    [InlineData("cancelled", false)]
    [InlineData("pending", false)]
    [InlineData("failed", false)]
    public void ABuyAnswer_CarriesItsOutcome_AndTheTransactionOnlyWhenBought(string outcome, bool withTransaction)
    {
        string transaction = withTransaction ? $",\"transaction\":{Tx(Book, NowMs)}" : string.Empty;
        StoreKitBuyAnswer? answer = StoreKit2Answers.Buy($$"""{"ok":true,"outcome":"{{outcome}}"{{transaction}}}""");
        Assert.Equal(outcome, answer?.Outcome);
        Assert.Equal(withTransaction, answer?.Transaction is not null);
    }

    [Fact]
    public void AnUpdateEvent_NamesItsProduct()
    {
        Assert.Equal(Book, StoreKit2Answers.UpdatedProduct($$"""{"ok":true,"event":"updated","productId":"{{Book}}"}"""));
        Assert.Null(StoreKit2Answers.UpdatedProduct("""{"ok":true}"""));
    }
}
