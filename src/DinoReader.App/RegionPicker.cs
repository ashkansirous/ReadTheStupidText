using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>When the region picker (Slice 79) is worth showing for a page, and what it says and asks.</summary>
public static class RegionPicker
{
	private const double HelperFontSize = 12;

	/// <summary>A page with a picture and regions that is either split into several blocks or has a left-out one.</summary>
	public static bool Offers(PageDraft page) =>
		page.HasPicture && page.Ocr?.Regions is { Count: > 0 } regions && (regions.Count > 1 || regions.Any(r => !r.IsIncluded));

	/// <summary>The line of helper copy shown with the overlay.</summary>
	public static Label Helper() => new() { Text = AppResources.Regions_Helper, FontSize = HelperFontSize, TextColor = DinoTokens.Get(DinoTokens.Muted) };

	/// <summary>Whether a toggle may go ahead: always when nothing typed would be lost, else only if the grown-up agrees to replace it.</summary>
	public static async Task<bool> MayReplaceAsync(Page host, bool typedTextWouldBeLost) =>
		!typedTextWouldBeLost
		|| await host.DisplayAlertAsync(AppResources.Regions_ReplaceTitle, AppResources.Regions_ReplaceBody, AppResources.Regions_Replace, AppResources.Regions_Keep);
}
