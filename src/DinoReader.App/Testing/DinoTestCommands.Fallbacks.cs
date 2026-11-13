#if DEBUG
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using Sirous.Reading.Mobile.Shared.Testing;

namespace DinoReader.Testing;

/// <summary>
/// Slice 320 item 7 (L78): <c>corrupt-shelf-order</c> writes text that is not a JSON list over the stored shelf order file and lists the shelf, so the library really meets an unreadable order file, falls back to the default order and
/// reports <c>library:shelf_order_unreadable</c> through its own sink (<c>[Usage] fallback library:shelf_order_unreadable ...</c>), instead of a flow sending that fallback by hand (force-fallback only proves the sink). The file is
/// left damaged; <c>shelf-order-reset</c> deletes it, and the next real reorder rewrites it. Outcome: <c>HOOK corrupt-shelf-order written</c> (the file was written and the list call returned; it does not prove the library read the file, the flow's <c>[Usage] fallback</c> line does) or <c>fault ...</c>. Android and iOS alike (the shared dispatcher routes both).
/// </summary>
internal static partial class DinoTestCommands
{
	private const string CorruptShelfOrderName = "corrupt-shelf-order";
	private const string UnreadableOrderText = "this is not a list of book ids";

	private static async Task CorruptShelfOrderAsync()
	{
		try
		{
			string path = Path.Combine(FileSystem.AppDataDirectory, StoriesFolderName, ShelfOrder.FileName);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			await File.WriteAllTextAsync(path, UnreadableOrderText);
			await IPlatformApplication.Current!.Services.GetRequiredService<IStoryLibrary>().ListAsync();
			Log($"{CorruptShelfOrderName} written");
		}
		catch (Exception ex)
		{
			// no-fallback: Debug-only test hook; the fault is written to the hook log
			Log($"{CorruptShelfOrderName} fault {TestHookLog.FaultSummary(ex)}");
		}
	}
}
#endif
