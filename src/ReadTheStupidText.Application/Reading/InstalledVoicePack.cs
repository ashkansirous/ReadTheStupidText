namespace ReadTheStupidText.Application.Reading;

/// <summary>A voice pack that is installed and complete: its folder and its parsed <c>pack.json</c>.</summary>
public sealed record InstalledVoicePack(string Folder, PackManifest Manifest);
