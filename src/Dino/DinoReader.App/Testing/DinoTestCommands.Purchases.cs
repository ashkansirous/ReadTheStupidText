#if DEBUG
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using Sirous.Reading.Mobile.Shared.Testing;

namespace DinoReader.Testing;

/// <summary>
/// Slice 346 (Debug only, Android broadcast and iOS URL alike through the shared dispatcher): <c>fake-purchases mode=on|off [paid=&lt;stem|id&gt;,...] [owned=&lt;stem|id&gt;,...] [price=&lt;text&gt;]
/// [buy=ok|cancel|pending|fail] [offline=1]</c> turns paid books on with a pretend store, so a flow can buy, restore and see the first-run picker with only free books without money or a real store account.
/// <c>paid</c> marks those test-store books as sold (the test store must be on: <c>test-store mode=ok</c>), <c>owned</c> says which the account already owns, <c>buy</c> is what the next Buy does. <c>mode=off</c>
/// puts the real store and the remote switch back. Outcome: <c>HOOK fake-purchases ok on|off</c> or <c>failed ...</c> (started: the outcome line comes when the test books are read). The purchase code logs
/// <c>[Purchases] ...</c> markers (<c>snapshot</c>, <c>buy</c>, <c>redeem</c>).
/// </summary>
internal static partial class DinoTestCommands
{
	private const string FakePurchasesName = "fake-purchases";
	private const string FakeSubscriptionName = "fake-subscription";
	private const string StateKey = "state";
	private const string DaysKey = "days";
	private const string PaidKey = "paid";
	private const string OwnedKey = "owned";
	private const string PriceKey = "price";
	private const string BuyKey = "buy";
	private const string OfflineKey = "offline";
	private const string TrialKey = "trial"; // Slice 397: trial=used makes the pretend store say the account has used the free trial (the plain-price card); fake-subscription takes it too
	private const string UsedValue = "used";
	private const string OnMode = "on";
	private const string DefaultPrice = "$0.99";

	private static async Task FakePurchasesAsync(TestHookCommand command)
	{
		try
		{
			IServiceProvider services = IPlatformApplication.Current!.Services;
			var switchable = services.GetRequiredService<SwitchablePurchases>();
			var sales = services.GetRequiredService<BookSales>();
			sales.Purchases.MarkStale(); // the next listing asks the (new) store again
			if (command.Get(ModeKey, string.Empty) != OnMode)
			{
				switchable.Override = null;
				sales.ForcedEnabled = null;
				FakePurchases.SetPaid([]);
				Log($"{FakePurchasesName} ok off");
				return;
			}

			// All state is set before the first await, so the next hook (skip-first-run) always sees the sold books.
			var fake = new FakePurchases { Price = command.Get(PriceKey, DefaultPrice), Offline = command.Flag(OfflineKey), NextBuy = BuyOutcomeOf(command.Get(BuyKey, "ok")), TrialAvailable = command.Get(TrialKey, string.Empty) != UsedValue };
			string[] paid = command.Get(PaidKey, string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			FakePurchases.SetPaid(paid);
			switchable.Override = fake;
			sales.ForcedEnabled = true;
			foreach (string id in await IdsAsync(command.Get(OwnedKey, string.Empty)))
			{
				fake.OwnedProducts.Add(FakePurchases.ProductOf(id));
			}

			Log($"{FakePurchasesName} ok on paid={paid.Length} owned={fake.OwnedProducts.Count}");
		}
		catch (Exception ex)
		{
			// no-fallback: Debug-only test hook; the fault is written to the hook log
			Log($"{FakePurchasesName} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	/// <summary>
	/// Slice 384 (Debug only): <c>fake-subscription state=none|trial|active|grace|expired [days=&lt;n&gt;]</c> changes the pretend store's subscription (the fake store must be on: <c>fake-purchases mode=on</c>) and reads the store at once,
	/// so a flow can lapse and renew it with no money. <c>days</c> is how far from now the paid period ends (default 30, may be negative). Outcome: <c>HOOK fake-subscription ok &lt;state&gt;</c> or <c>failed ...</c>;
	/// the purchase code logs <c>[Purchases] subscription state=... grants=...</c> after the read.
	/// </summary>
	private static async Task FakeSubscriptionAsync(TestHookCommand command)
	{
		try
		{
			IServiceProvider services = IPlatformApplication.Current!.Services;
			if (services.GetRequiredService<SwitchablePurchases>().Override is not FakePurchases fake)
			{
				Log($"{FakeSubscriptionName} failed the fake store is off");
				return;
			}

			string state = command.Get(StateKey, "none");
			int days = int.TryParse(command.Get(DaysKey, "30"), out int parsed) ? parsed : 30;
			fake.Subscription = FakeSubscriptionOf(state, days);
			if (command.Get(TrialKey, string.Empty) is { Length: > 0 } trial)
			{
				fake.TrialAvailable = trial != UsedValue;
			}

			var sales = services.GetRequiredService<BookSales>();
			sales.Purchases.MarkStale();
			StoreListing listing = await services.GetRequiredService<StoreClient>().ListAsync(CancellationToken.None); // reads the fake store (the purchase read)
			await sales.Purchases.RefreshAsync(listing.Items, CancellationToken.None, interactive: true); // ListAsync waits only a budget for the read; this one is awaited
			Log($"{FakeSubscriptionName} ok {state}");
		}
		catch (Exception ex)
		{
			// no-fallback: Debug-only test hook; the fault is written to the hook log
			Log($"{FakeSubscriptionName} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	private static SubscriptionStatus? FakeSubscriptionOf(string state, int days)
	{
		DateTimeOffset ends = DateTimeOffset.UtcNow.AddDays(days);
		return state switch
		{
			"trial" => new SubscriptionStatus(SubscriptionState.Trial, ends, SubscriptionProducts.Monthly),
			"active" => new SubscriptionStatus(SubscriptionState.Active, ends, SubscriptionProducts.Monthly),
			"grace" => new SubscriptionStatus(SubscriptionState.InGrace, ends, SubscriptionProducts.Monthly),
			"expired" => new SubscriptionStatus(SubscriptionState.Expired, ends, SubscriptionProducts.Monthly),
			_ => null,
		};
	}

	private static BuyOutcome BuyOutcomeOf(string text) => text switch
	{
		"cancel" => BuyOutcome.Cancelled,
		"pending" => BuyOutcome.Pending,
		"fail" => BuyOutcome.Failed,
		_ => BuyOutcome.Bought,
	};

	// Test-store stems or ids (comma separated) as the ids the store lists.
	private static async Task<IReadOnlyList<string>> IdsAsync(string names)
	{
		var ids = new List<string>();
		foreach (string name in names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (await TestStore.FindAsync(name) is { } item)
			{
				ids.Add(item.Id);
			}
		}

		return ids;
	}
}
#endif
