using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>
/// Gives the Windows title bar the colour of the page on screen and, on the reading screen, the book's name (Slice 235 part B, mock Batch 46 frame 155). The colour rule is pure
/// (<see cref="TitleBarTones"/>); this class only works out which kind of page it is and hands the result to <see cref="DinoWindowFrame"/>. Windows only.
/// </summary>
internal static class DinoTitleBar
{
	/// <summary>Called when <paramref name="page"/> appears (and again when it changes what it is, like the shelf entering the grown-ups' corner).</summary>
	public static void ForPage(Page page)
	{
		if (page is GatePage || DinoWindowFrame.Current is not { } frame)
		{
			return; // the gate is a scrim over the page below, which keeps its colour
		}

		TitleBarPage kind = KindOf(page);
		frame.ApplyTone(TitleBarTones.For(kind));
		if (page.Window is { } window)
		{
			string? book = (page as ReadingPage)?.BookTitle;
			window.Title = TitleBarTones.WindowTitle(kind, book, AppResources.App_Name);
		}
	}

	/// <summary>
	/// Makes the title bar follow the top-most visible page, modal included: a page appearing sets it (<see cref="ForPage"/>), and a modal opening or closing sets it to whatever is on top now,
	/// so closing the intro or the gate gives the page below its colour back.
	/// </summary>
	public static void Watch(Microsoft.Maui.Controls.Window window)
	{
		window.ModalPushed += (_, _) => ForTop(window);
		window.ModalPopped += (_, _) => ForTop(window);
	}

	/// <summary>Sets the title bar for the page on top of <paramref name="window"/> right now.</summary>
	public static void ForTop(Microsoft.Maui.Controls.Window window)
	{
		if (DinoKeyTargets.TopPage(window) is { } top)
		{
			ForPage(top);
		}
	}

	/// <summary>Reapplies the colour when a page changed what it is while on screen; ignored while another page covers it.</summary>
	public static void Refresh(Page page)
	{
		if (DinoKeyTargets.TopPage(page.Window) == page)
		{
			ForPage(page);
		}
	}

	private static TitleBarPage KindOf(Page page) => page switch
	{
		ReadingPage => TitleBarPage.Reading,
		GalleryPage { InCorner: true } => TitleBarPage.Corner,
		IGrownUpPage => TitleBarPage.GrownUp,
		_ => TitleBarPage.Kids,
	};
}
