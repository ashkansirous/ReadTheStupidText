namespace ReadTheStupidText.Domain.Activity;

/// <summary>
/// The foreground window a read originated from — its owning application and the
/// window title (e.g. <c>App = "Chrome"</c>, <c>Title = "Inbox — Gmail"</c>).
/// Pure data; the UI composes the displayed "App — Title" string. Either field
/// may be empty when the OS doesn't surface it.
/// </summary>
public sealed record WindowSource(string App, string Title);
