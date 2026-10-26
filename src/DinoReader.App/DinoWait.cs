using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>
/// Dino Reader's look for the shared wait overlay (Slice 143, Batch 30 mock frames 64 and 65): the app icon hopping with a
/// shadow, a cream veil, one line for grown-ups and a white outlined Cancel. Over a picture the veil is a little warmer and
/// the padding smaller; over a whole page it is the page cream.
/// </summary>
public static class DinoWait
{
	private const string IconImage = "dino_wait";
	private const string ShadowColour = "#2b2540";
	private const string LineColour = "#2b2540";
	private const string DetailColour = "#5a5470";
	private const string CancelBorderColour = "#e3dccd";
	private const string PictureVeil = "#DBFFF6E6"; // rgba(255,246,230,.86)
	private const string PageVeil = "#E6F3EFE6"; // rgba(243,239,230,.9)
	private const double PicturePadding = 20;
	private const double PagePadding = 24;

	/// <summary>The overlay for the picture box of the reading page (frame 64).</summary>
	public static WaitOverlay OverPicture()
	{
		return Build(PictureVeil, PicturePadding);
	}

	/// <summary>The overlay for a whole grown-up page (frame 65).</summary>
	public static WaitOverlay OverPage()
	{
		return Build(PageVeil, PagePadding);
	}

	private static WaitOverlay Build(string veil, double padding)
	{
		var style = new WaitOverlayStyle
		{
			Fill = Color.FromArgb(veil),
			LineColor = Color.FromArgb(LineColour),
			DetailColor = Color.FromArgb(DetailColour),
			CancelFill = Colors.White,
			CancelBorder = Color.FromArgb(CancelBorderColour),
			CancelText = Color.FromArgb(DetailColour),
			Padding = padding,
			CreateAnimation = () => new HoppingImage(ImageSource.FromFile(IconImage), Color.FromArgb(ShadowColour)),
		};
		return new WaitOverlay(style, Resources.Strings.AppResources.Common_Cancel);
	}
}
