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
