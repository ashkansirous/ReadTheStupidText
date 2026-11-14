using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>The Animal sounds sub-page of Settings (Slice 172, mock frame 83's pattern): just <see cref="AnimalSoundsCard"/>, saved at once.</summary>
public sealed class AnimalSoundsPage : SettingsSubPage
{
	public AnimalSoundsPage()
		: base(AppResources.Settings_AnimalSounds_Title)
	{
		Body.Children.Add(AnimalSoundsCard.Build());
	}
}
