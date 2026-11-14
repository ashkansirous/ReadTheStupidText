namespace DinoReader;

/// <summary>Marks a page that only a grown-up reaches (add, edit, cast): leaving the app closes them,
/// and <see cref="DinoApp"/> then drops <see cref="GrownUpMode"/> back to kids' mode.</summary>
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
