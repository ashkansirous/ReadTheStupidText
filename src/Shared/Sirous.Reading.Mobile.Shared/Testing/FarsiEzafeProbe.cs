#if DEBUG
namespace Sirous.Reading.Mobile.Shared.Testing;

/// <summary>
/// Slice 376 item 3: a Debug-only marker that the Farsi ezafe override (<c>FarsiEzafe</c>, Slice 342) changed the text sent to the engine, so the RTST Farsi flow
/// (<c>.maestro/rtst/farsi-ezafe-reads.yaml</c>) can prove the path ran on a device. It carries the two lengths only, never the text.
/// </summary>
internal static class FarsiEzafeProbe
{
	public const string Marker = "FARSI_EZAFE applied";

	/// <summary>Writes <see cref="Marker"/> when <paramref name="spoken"/> differs from the <paramref name="original"/> text.</summary>
	public static void Note(string original, string spoken)
	{
		if (!string.Equals(original, spoken, StringComparison.Ordinal))
		{
			TestHookLog.Write($"{Marker} chars={original.Length}->{spoken.Length}");
		}
	}
}
#endif
