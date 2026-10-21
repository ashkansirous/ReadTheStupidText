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
	public const string ReadDim = "ReadDim";
	public const string SentenceFill = "SentenceFill";
	public const string BoxFill = "BoxFill";
	public const string BoxStroke = "BoxStroke";
	public const string DotIdle = "DotIdle";
	public const string BrandEdge = "BrandEdge";
	public const string CoralEdge = "CoralEdge";
	public const string CoralHalo = "CoralHalo";
	public const string NightPage = "NightPage";
	public const string NightCard = "NightCard";
	public const string NightInk = "NightInk";
	public const string NightSoft = "NightSoft";
	public const string NightMuted = "NightMuted";
	public const string NightFill = "NightFill";
	public const string NightBrand = "NightBrand";
	public const string NightBrandEdge = "NightBrandEdge";
	public const string NightBoxFill = "NightBoxFill";
	public const string NightBoxStroke = "NightBoxStroke";
	public const string NightDot = "NightDot";
	public const string NightCoral = "NightCoral";
	public const string NightCoralEdge = "NightCoralEdge";
	public const string NightCoralGlyph = "NightCoralGlyph";
	public const string NightCoralHalo = "NightCoralHalo";

	public static Color Get(string key) => (Color)Microsoft.Maui.Controls.Application.Current!.Resources[key];
}
