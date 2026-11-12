#if DEBUG
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Mobile.Shared.Testing;

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
	private const string PaidKey = "paid";
	private const string OwnedKey = "owned";
	private const string PriceKey = "price";
	private const string BuyKey = "buy";
	private const string OfflineKey = "offline";
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
			var fake = new FakePurchases { Price = command.Get(PriceKey, DefaultPrice), Offline = command.Flag(OfflineKey), NextBuy = BuyOutcomeOf(command.Get(BuyKey, "ok")) };
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
