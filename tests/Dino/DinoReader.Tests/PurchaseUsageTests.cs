using System.Text.RegularExpressions;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 455: the closed detail of the <c>purchase</c> count, its match with the Worker's lists, and the offer an iOS transaction carries.</summary>
public class PurchaseUsageTests
{
    private static string[] WorkerList(string name)
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "worker", "events.ts"));
        Match list = Regex.Match(source, name + @" = \[(?<names>[^\]]*)\]");
        Assert.True(list.Success, name);
        return [.. Regex.Matches(list.Groups["names"].Value, "\"([a-z_]+)\"").Select(m => m.Groups[1].Value)];
    }

    [Fact]
    public void The_Worker_lists_exactly_the_whats_outcomes_and_offers_the_app_can_name()
    {
        // The Worker spells the what list as the book name plus the two subscription lists; PURCHASE_SUBSCRIPTIONS holds the plans.
        string[] subscriptions = WorkerList("PURCHASE_SUBSCRIPTIONS");
        string[] whats = [.. WorkerList("PURCHASE_WHATS"), .. subscriptions];

        Assert.Equal([PurchaseDetail.SubMonthly, PurchaseDetail.SubYearly], subscriptions.Order());
        Assert.Equal(PurchaseDetail.Whats.Where(w => w != PurchaseDetail.Book && !PurchaseDetail.IsSubscription(w)).Order(), whats.Where(w => !subscriptions.Contains(w)).Order());
        Assert.Equal(PurchaseDetail.Outcomes.Order(), WorkerList("PURCHASE_OUTCOMES").Order());
        Assert.Equal(PurchaseDetail.Offers.Order(), WorkerList("PURCHASE_OFFERS").Order());
        Assert.Contains("PURCHASE_BOOK = \"" + PurchaseDetail.Book + "\"", File.ReadAllText(Path.Combine(RepoRoot(), "worker", "events.ts")));
    }

    [Theory]
    [InlineData(PurchaseDetail.Book, PurchaseDetail.Bought, null, "book.bought")]
    [InlineData(PurchaseDetail.Book, PurchaseDetail.Started, PurchaseDetail.OfferTrial, "book.started")] // a book never carries an offer
    [InlineData(PurchaseDetail.SubMonthly, PurchaseDetail.Shown, PurchaseDetail.OfferTrial, "sub_monthly.shown.trial")]
    [InlineData(PurchaseDetail.SubYearly, PurchaseDetail.AlreadyOwned, null, "sub_yearly.already_owned.paid")] // a plan with no offer given is paid
    [InlineData(PurchaseDetail.Restore, PurchaseDetail.NothingToRestore, null, "restore.nothing_to_restore")]
    [InlineData(PurchaseDetail.Code, PurchaseDetail.Bought, PurchaseDetail.OfferCode, "code.bought")]
    public void The_detail_is_what_dot_outcome_and_an_offer_only_for_a_plan(string what, string outcome, string? offer, string expected)
    {
        Assert.Equal(expected, PurchaseDetail.Of(what, outcome, offer));
    }

    [Fact]
    public void Every_closed_combination_has_one_of_the_three_shapes_the_Worker_accepts()
    {
        var shape = new Regex(@"^(book|restore|code)\.[a-z_]+$|^sub_(monthly|yearly)\.[a-z_]+\.(trial|paid|code)$");
        foreach (string what in PurchaseDetail.Whats)
        {
            foreach (string outcome in PurchaseDetail.Outcomes)
            {
                Assert.Matches(shape, PurchaseDetail.Of(what, outcome));
            }
        }
    }

    [Theory]
    [InlineData(SubscriptionProducts.Monthly, PurchaseDetail.SubMonthly)]
    [InlineData(SubscriptionProducts.Yearly, PurchaseDetail.SubYearly)]
    [InlineData("uk.sirous.dinoreader.book.goldilocks", PurchaseDetail.Book)]
    public void A_product_is_a_plan_or_a_book(string product, string expected)
    {
        Assert.Equal(expected, PurchaseDetail.WhatOf(product));
    }

    [Fact]
    public void The_offer_follows_what_the_card_promised()
    {
        Assert.Equal(PurchaseDetail.OfferTrial, PurchaseDetail.OfferOf(true));
        Assert.Equal(PurchaseDetail.OfferPaid, PurchaseDetail.OfferOf(false));
    }

    [Theory]
    [InlineData(SubscriptionState.Trial, PurchaseDetail.OfferPaid, PurchaseDetail.OfferTrial)]
    [InlineData(SubscriptionState.Active, PurchaseDetail.OfferTrial, PurchaseDetail.OfferPaid)]
    [InlineData(SubscriptionState.InGrace, PurchaseDetail.OfferTrial, PurchaseDetail.OfferTrial)]
    [InlineData(SubscriptionState.Expired, PurchaseDetail.OfferPaid, PurchaseDetail.OfferPaid)]
    [InlineData(SubscriptionState.Unknown, PurchaseDetail.OfferTrial, PurchaseDetail.OfferTrial)]
    [InlineData(null, PurchaseDetail.OfferPaid, PurchaseDetail.OfferPaid)]
    public void The_offer_after_a_buy_trusts_only_a_trial_or_an_active_read(SubscriptionState? state, string asked, string expected)
    {
        Assert.Equal(expected, PurchaseDetail.OfferAfterBuy(state, asked));
    }

    [Theory]
    [InlineData(false, true, true, true, PurchaseDetail.Failed)]
    [InlineData(true, false, true, true, PurchaseDetail.Failed)]
    [InlineData(true, true, true, false, PurchaseDetail.Restored)]
    [InlineData(true, true, false, true, PurchaseDetail.Restored)]
    [InlineData(true, true, false, false, PurchaseDetail.NothingToRestore)]
    public void A_restore_is_judged_from_its_own_read(bool succeeded, bool internet, bool book, bool subscription, string expected)
    {
        Assert.Equal(expected, PurchaseDetail.RestoreOutcome(succeeded, internet, book, subscription));
    }

    [Fact]
    public void A_code_redemption_is_counted_once_per_subscription()
    {
        Assert.Equal(["b", "c"], PurchaseDetail.NotYetLogged(["a", "x"], ["a", "b", "b", "", "c"]));
        Assert.Empty(PurchaseDetail.NotYetLogged(["a"], ["a"]));
    }

    private static string Tx(string product, string original, string? offer) =>
        $$"""{"productId":"{{product}}","id":"9","originalId":"{{original}}","purchasedAt":1,"expiresAt":2,"trial":false,{{(offer is null ? "" : $"\"offer\":\"{offer}\",")}}"revoked":false,"jws":"h.p.s"}""";

    [Fact]
    public void An_offer_code_subscription_shows_as_a_code_redemption_and_nothing_else_does()
    {
        string current = string.Join(",", Tx(SubscriptionProducts.Monthly, "100", "code"), Tx(SubscriptionProducts.Yearly, "200", "trial"), Tx("uk.sirous.dinoreader.book.x", "300", "code"), Tx(SubscriptionProducts.Yearly, "400", null));
        StoreKitEntitlements read = StoreKit2Answers.Entitlements($$"""{"ok":true,"transactions":[{{current}}],"latest":[]}""")!;

        Assert.Equal(["100"], read.CodeRedemptions);
    }

    [Fact]
    public void An_offer_outside_the_closed_list_is_ignored()
    {
        StoreKitEntitlements read = StoreKit2Answers.Entitlements($$"""{"ok":true,"transactions":[{{Tx(SubscriptionProducts.Monthly, "1", "free-for-friends")}}],"latest":[]}""")!;

        Assert.Null(read.Current[0].Offer);
        Assert.Empty(read.CodeRedemptions);
    }

    [Fact]
    public void A_buy_answer_carries_the_offer_apple_applied()
    {
        StoreKitBuyAnswer? bought = StoreKit2Answers.Buy($$"""{"ok":true,"outcome":"bought","transaction":{{Tx(SubscriptionProducts.Monthly, "1", "trial")}}}""");

        Assert.Equal(PurchaseDetail.OfferTrial, bought!.Transaction!.Offer);
    }

    [Fact]
    public void The_event_name_is_purchase()
    {
        Assert.Equal("purchase", UsageEventName.Purchase);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReadTheStupidText.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo root (ReadTheStupidText.slnx) not found.");
    }
}
