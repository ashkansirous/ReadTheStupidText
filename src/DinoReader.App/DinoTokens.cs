namespace DinoReader;

/// <summary>Keys of the design-token colours declared in App.xaml (Dino Reader v2 mock), read
/// through <see cref="Get"/> so a typo is a compile error instead of a runtime KeyNotFound.</summary>
public static class DinoTokens
{
	public const string Cream = "Cream";
	public const string Sand = "Sand";
	public const string Ink = "Ink";
	public const string Body = "Body";
	public const string Muted = "Muted";
	public const string Brand = "Brand";
	public const string BrandTint = "BrandTint";
	public const string Danger = "Danger";
	public const string Coral = "Coral";
	public const string Hairline = "Hairline";
	public const string Scrim = "Scrim";
	public const string ControlFill = "ControlFill";

	public static Color Get(string key) => (Color)Microsoft.Maui.Controls.Application.Current!.Resources[key];
}
