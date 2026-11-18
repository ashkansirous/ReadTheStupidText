#if DEBUG
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using Sirous.Reading.Mobile.Shared.Testing;

namespace DinoReader.Testing;

/// <summary>
/// Slice 398 (Debug only): <c>fake-offline mode=on|off [ended-hours=&lt;n&gt;]</c> makes the app believe the device is offline (<see cref="NetworkProbe"/>) and, with <c>ended-hours</c>, moves the kept subscription's end date
/// to that many hours ago (the store's own answer, as the Worker gives it), so a flow can reach the calm "can't check your subscription" notice: offline, past the last end date, inside the 3 days. The fake store must be on
/// with a running subscription (<c>fake-purchases mode=on</c>, then <c>fake-subscription state=active</c>). <c>mode=off</c> puts the real network answer back. Outcome: <c>HOOK fake-offline ok on|off</c> or <c>fault ...</c>;
/// the gallery logs <c>[Subscription] banners locked=N offline=shown|none</c> each time its banners are worked out.
/// </summary>
internal static partial class DinoTestCommands
{
	private const string FakeOfflineName = "fake-offline";
	private const string EndedHoursKey = "ended-hours";

	private static void FakeOffline(TestHookCommand command)
	{
		try
		{
			if (command.Get(ModeKey, string.Empty) != OnMode)
			{
				NetworkProbe.SetOverride(null);
				Log($"{FakeOfflineName} ok off");
				return;
			}

			if (double.TryParse(command.Get(EndedHoursKey, string.Empty), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double hours))
			{
				if (!MoveSubscriptionEnd(DateTimeOffset.UtcNow.AddHours(-hours)))
				{
					return;
				}
			}

			NetworkProbe.SetOverride(() => true);
			Log($"{FakeOfflineName} ok on");
		}
		catch (Exception ex)
		{
			// no-fallback: Debug-only test hook; the fault is written to the hook log
			Log($"{FakeOfflineName} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	// The store's own answer (the Worker's) is the one that can put the end date in the past: the same Merge a real answer goes through, with the record's own read number.
	private static bool MoveSubscriptionEnd(DateTimeOffset ends)
	{
		SubscriptionStore store = IPlatformApplication.Current!.Services.GetRequiredService<StoreClient>().Sales.Purchases.Subscription;
		if (store.Record is not { } record)
		{
			Log($"{FakeOfflineName} fault no subscription record to move");
			return false;
		}

		store.Merge(new SubscriptionAnswer.Worker(record.ReadNumber, SubscriptionAccess.WorkerStates.Active, ends));
		return true;
	}
}
#endif
