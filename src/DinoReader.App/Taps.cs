namespace DinoReader;

/// <summary>The one way a view in this app gets a tap handler (moved out of the cast screen's helpers in Slice 278 part B).</summary>
internal static class Taps
{
	public static void AddTap(View view, Func<Task> tapped)
	{
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) => await tapped();
		view.GestureRecognizers.Add(tap);
	}
}
