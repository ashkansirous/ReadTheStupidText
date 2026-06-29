namespace ReadTheStupidText.Application.Settings;

/// <summary>
/// The control panel's last on-screen position, in physical (device) pixels — the
/// coordinate space <c>AppWindow.Position</c>/<c>Move</c> use. Persisted so the panel
/// reopens where the user dragged it (Slice 24, Decision 31); on restore it is clamped
/// to the current work area so a now-offscreen point is pulled back on-screen.
/// </summary>
public readonly record struct PanelPosition(int X, int Y);
