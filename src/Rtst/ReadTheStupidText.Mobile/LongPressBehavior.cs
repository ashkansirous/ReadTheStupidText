namespace ReadTheStupidText.Mobile;

/// <summary>
/// Raises <see cref="LongPressed"/> when the user presses and holds the view it is attached to (Slice 99).
/// MAUI has no long-press gesture. Each platform attaches its own recogniser in
/// <c>Platforms/&lt;OS&gt;/LongPressBehavior.&lt;OS&gt;.cs</c>; the recogniser must not consume the touch, so
/// a scroll view under it keeps scrolling.
/// </summary>
public partial class LongPressBehavior
{
    public event EventHandler? LongPressed;

    private void RaiseLongPressed(View view) => LongPressed?.Invoke(view, EventArgs.Empty);
}
