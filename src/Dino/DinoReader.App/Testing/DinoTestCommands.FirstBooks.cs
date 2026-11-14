#if DEBUG
using Microsoft.Extensions.DependencyInjection;
using Sirous.Reading.Mobile.Shared.Testing;

namespace DinoReader.Testing;

/// <summary>
/// Slice 261: the test books and the test store for the first-run book picker. <c>import-test-book id=&lt;stem|id|a,b|all&gt;</c> puts those former starter books (Debug-only assets, <see cref="TestStore"/>)
/// on the shelf through the store client's own download and logs <c>HOOK import-test-book ok &lt;ids&gt;</c> or <c>failed ...</c>; a flow that opens a book calls it first.
/// <c>test-store mode=ok|slow|stall|offline|off</c> switches the store list the picker and the Store page read (off, the default, is the live store); slow makes each book take about three seconds, so
/// the download wait can be seen; stall holds each book for 30 s per chunk, so a flow can cancel while it is in flight. Outcome: <c>HOOK test-store ok &lt;mode&gt;</c> or <c>failed unknown mode</c>.
/// </summary>
internal static partial class DinoTestCommands
{
	private const string ImportTestBookName = "import-test-book";
	private const string TestStoreName = "test-store";
	private const string ModeKey = "mode";

	private static async Task ImportTestBookAsync(string names)
	{
		try
		{
			StoreClient store = IPlatformApplication.Current!.Services.GetRequiredService<StoreClient>();
			string? failure = names.Trim().Length == 0 ? "no book" : await TestBooks.Start(store, names);
			Log(failure is null ? $"{ImportTestBookName} ok {names.Trim()}" : $"{ImportTestBookName} failed {failure}");
		}
		catch (Exception ex)
		{
			// no-fallback: Debug-only test hook; the fault is written to the hook log
			Log($"{ImportTestBookName} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	private static string SetTestStore(string mode)
	{
		if (!Enum.TryParse(mode, ignoreCase: true, out TestStoreMode parsed) || !Enum.IsDefined(parsed))
		{
			return $"{TestHookDispatcher.Failed} {TestHookDispatcher.Unknown} mode {mode}";
		}

		TestStore.Mode = parsed;
		return $"{TestHookDispatcher.Ok} {parsed.ToString().ToLowerInvariant()}";
	}
}
#endif
