namespace DinoReader;

/// <summary>Marks a page that only a grown-up reaches (manage, add, edit): leaving the app closes them.</summary>
public interface IGrownUpPage
{
}

/// <summary>Asks a grown-up to pass the gate (hold, then a sum) before anything that changes the library.</summary>
public sealed class GrownUpGate
{
	/// <summary>Shows the gate over the current page and returns true only if the sum was answered right.</summary>
	public async Task<bool> RequestAsync(INavigation navigation)
	{
		var page = new GatePage(ScreenReader.IsOn());
		await navigation.PushModalAsync(page, animated: false);
		return await page.Result;
	}
}
