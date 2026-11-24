#if DEBUG || DINO_TEST_BOOKS
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DinoReader.Testing;

/// <summary>
/// Slice 434: <c>open-book</c> for a Release test package (<c>-p:DinoTestBooks=true</c>, the Windows VM). The store-shot check cannot find a book through the shelf tiles (the zoomed window shows one row), so it writes the
/// story id into the Windows head's <c>open-book.request</c> file (<c>Platforms/Windows/FullShelfRequest.cs</c>) and this opens that book's reading page the way a tile tap does (<c>StoryActions.ReadAsync</c>).
/// Outcome: <c>ok &lt;id&gt;</c> or <c>failed &lt;why&gt;</c>, written by the head as <c>HOOK open-book ...</c>. Must run on the UI thread.
/// </summary>
internal static class OpenBookRequestHook
{
	internal const string Name = "open-book";

	private const int WaitMilliseconds = 20000;
	private const int RetryMilliseconds = 250;

	/// <summary>Opens the book whose package id, story id or title slug matches <paramref name="id"/>; returns the outcome text that follows <c>HOOK open-book</c>.</summary>
	internal static async Task<string> OpenAsync(string id)
	{
		IServiceProvider services = IPlatformApplication.Current!.Services;
		NavigationPage? root = await WaitForAsync(GalleryRoot);
		if (root is null)
		{
			return "failed no gallery";
		}

		if (services.GetRequiredService<GrownUpMode>().IsGrownUp)
		{
			return "failed grown-up";
		}

		Story? story = await WaitForAsync(async () => BookMatch.Find(await services.GetRequiredService<IStoryLibrary>().ListAsync(), id));
		if (story is null)
		{
			return $"failed no book {id}";
		}

		if (root.Navigation.NavigationStack.Any(p => p is ReadingPage))
		{
			await root.Navigation.PopToRootAsync(); // a second reading page is never pushed on top of another
		}

		await services.GetRequiredService<StoryActions>().ReadAsync(root.Navigation, story);
		return root.Navigation.NavigationStack.LastOrDefault() is ReadingPage ? $"ok {story.Id}" : "failed no reading screen";
	}

	private static NavigationPage? GalleryRoot() =>
		Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page is NavigationPage { RootPage: GalleryPage } nav ? nav : null;

	private static async Task<T?> WaitForAsync<T>(Func<Task<T?>> probe) where T : class
	{
		for (int waited = 0; waited < WaitMilliseconds; waited += RetryMilliseconds)
		{
			if (await probe() is { } found)
			{
				return found;
			}

			await Task.Delay(RetryMilliseconds);
		}

		return null;
	}

	private static Task<T?> WaitForAsync<T>(Func<T?> probe) where T : class => WaitForAsync(() => Task.FromResult(probe()));
}
#endif
