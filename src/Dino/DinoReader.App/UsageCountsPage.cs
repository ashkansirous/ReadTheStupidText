using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>The Usage counts sub-page of Settings (Slice 172, mock frame 83's pattern): just <see cref="UsageCountsCard"/>, saved at once.</summary>
public sealed class UsageCountsPage : SettingsSubPage
{
	public UsageCountsPage(UsageCounter usage)
		: base(AppResources.Usage_Title)
	{
		Body.Children.Add(UsageCountsCard.Build(usage));
	}
}
