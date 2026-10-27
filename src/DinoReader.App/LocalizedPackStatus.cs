using DinoReader.Resources.Strings;
using ReadTheStupidText.Mobile.Shared;

namespace DinoReader;

/// <summary>
/// The localized voice-pack status line (Slice 66). Mirrors <c>PackStatusText</c> in Mobile.Shared, whose
/// literals are English-only and shared with the other app (Slice 53 made the same copy for Read The Stupid Text).
/// </summary>
public static class LocalizedPackStatus
{
	/// <summary>The status line, or null when there is nothing to show.</summary>
	public static string? Compose(PackInstallState state, double progress) => state switch
	{
		PackInstallState.Resolving => AppResources.PackStatus_Resolving,
		PackInstallState.Downloading => AppResources.Format(AppResources.PackStatus_Downloading, progress.ToString("P0", AppResources.Culture)),
		PackInstallState.Offline => AppResources.PackStatus_Offline,
		PackInstallState.HashMismatch => AppResources.PackStatus_HashMismatch,
		PackInstallState.InvalidPack => AppResources.PackStatus_InvalidPack,
		PackInstallState.Failed => AppResources.PackStatus_Failed,
		PackInstallState.Unavailable => AppResources.PackStatus_Unavailable,
		_ => null,
	};
}
