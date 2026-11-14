using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>
/// The Reading speed sub-page of Settings (Slice 172, mock frame 83): just <see cref="ReadingSpeedCard"/>, saved at once. Slice 278 part B: the pre-made clips and the family's
/// recordings are played at this speed (the clip player takes the rate for each play), so the setting stays; there is no voice to play a sample with.
/// </summary>
public sealed class ReadingSpeedPage : SettingsSubPage
{
	public ReadingSpeedPage()
		: base(AppResources.Speed_Title)
	{
		Body.Children.Add(ReadingSpeedCard.Build());
	}
}
