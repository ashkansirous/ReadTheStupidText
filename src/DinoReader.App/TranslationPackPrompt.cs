using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Mobile.Shared.ModelDownload;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>How a parent's own book ended up after its translation was offered.</summary>
public enum OwnTranslationOutcome
{
	/// <summary>Nothing to do: translation is off, not available here, or the book is already translated.</summary>
	NotNeeded,

	/// <summary>The book was translated and the translation saved inside it.</summary>
	Saved,

	/// <summary>The parent declined or cancelled the download, or it failed: the book shows in its own language this time and its switch is left as it was.</summary>
	KeptOriginal,

	/// <summary>The pack was there but the translation was stopped part way (Cancel): the switch stays on and the reader translates when it can.</summary>
	Stopped,
}

/// <summary>
/// Offers the translation of a parent's own book on the phone (Slice 159, Decision 164): nothing downloads on its own. When the language pack is missing the
/// parent is told it is about 30 MB and asked; Download waits behind the page's overlay with Cancel; declining shows the book in its own language this time and changes nothing saved. Once the
/// pack is there the text and title are translated and saved inside the book. Only a grown-up screen offers it (the book save, Settings > Story languages);
/// the child's reading screen never starts or offers a download (Slice 173).
/// </summary>
public sealed class TranslationPackPrompt(StoryTranslator translator, IStoryLibrary library)
{
	private const int PackMegabytes = 30;

	/// <param name="host">The page the question is shown on.</param>
	/// <param name="beginWait">Starts the page's cancellable wait (the token is cancelled by its Cancel button).</param>
	/// <param name="endWait">Ends that wait.</param>
	/// <param name="only">The story languages to translate into (Settings asks for the one just ticked); null means every story language (the parent's save).</param>
	/// <param name="wait">The controller behind <paramref name="beginWait"/>; with it a pack download with no internet shows the waiting look and Retry (Slice 187), without it the download still waits but the overlay keeps its downloading line.</param>
	public async Task<OwnTranslationOutcome> OfferAsync(
		Page host, string storyId, Func<WaitStage, string, CancellationToken> beginWait, Action endWait, IReadOnlyCollection<string>? only = null, WaitController? wait = null)
	{
		if (await library.GetAsync(storyId) is not { } story || !translator.NeedsOwnTranslation(story, only))
		{
			return OwnTranslationOutcome.NotNeeded;
		}

		IReadOnlyList<string> missing = await translator.MissingPacksAsync(story, only);
		if (missing.Count > 0 && !await DownloadAsync(host, story, missing, only, beginWait, endWait, wait))
		{
			return OwnTranslationOutcome.KeptOriginal;
		}

		IReadOnlyList<string> ready = await translator.TranslatableTargetsAsync(story, only);
		return await TranslateAsync(story, ready, beginWait, endWait) ? OwnTranslationOutcome.Saved : OwnTranslationOutcome.Stopped;
	}

	/// <summary>
	/// Offers every own book that is still missing its table for <paramref name="language"/> (a language the grown-up just ticked) the pack and the translation. The pack
	/// question is asked once (the first book that needs it); once declined the other books are left alone.
	/// </summary>
	public async Task OfferForLanguageAsync(Page host, string language, Func<WaitStage, string, CancellationToken> beginWait, Action endWait, WaitController? wait = null)
	{
		IReadOnlyList<Story> stories = await library.ListAsync();
		foreach (Story story in stories.Where(story => translator.NeedsOwnTranslationIn(story, language)))
		{
			OwnTranslationOutcome outcome = await OfferAsync(host, story.Id, beginWait, endWait, [UiLanguage.Resolve(language)], wait);
			if (outcome is OwnTranslationOutcome.KeptOriginal or OwnTranslationOutcome.Stopped) // a Cancel on the wait ends the whole offer, not just this book
			{
				return;
			}
		}
	}

	// "English (30 MB), فارسی (30 MB)": every pack that will download, each with its size.
	private static string Described(IEnumerable<string> languages) =>
		string.Join(", ", languages.Select(language => $"{LanguageNames.NativeName(language)} ({PackMegabytes} MB)"));

	private async Task<bool> DownloadAsync(
		Page host, Story story, IReadOnlyList<string> missing, IReadOnlyCollection<string>? only, Func<WaitStage, string, CancellationToken> beginWait, Action endWait,
		WaitController? wait)
	{
		string targets = string.Join(", ", (only ?? StoryLanguages.Current().All).Select(LanguageNames.NativeName));
		bool agreed = await host.DisplayAlertAsync(
			AppResources.TranslationPack_TitleNamed, AppResources.Format(AppResources.TranslationPack_BodyNamed, targets, Described(missing)),
			AppResources.TranslationPack_Download, AppResources.TranslationPack_Keep);
		if (!agreed)
		{
			return false;
		}

		CancellationToken cancel = beginWait(WaitStage.DownloadingModel, AppResources.Format(AppResources.Wait_DownloadingPack, string.Join(", ", missing.Select(LanguageNames.NativeName))));
		bool ended = false;
		try
		{
			IReadOnlyList<string> failed = await translator.DownloadPacksAsync(missing, null, cancel, (language, session) => ShowWaiting(wait, language, session));
			if (failed.Count == 0 || (await translator.TranslatableTargetsAsync(story, only)).Count > 0)
			{
				return true; // a pack that failed only costs the languages that need it; the others carry on
			}

			endWait(); // the alert needs the overlay gone; the finally must not end a wait another screen began meanwhile
			ended = true;
			await host.DisplayAlertAsync(AppResources.App_Name, AppResources.TranslationPack_Failed, AppResources.Common_Ok);
			return false;
		}
		catch (OperationCanceledException)
		{
			return false;
		}
		finally
		{
			if (!ended)
			{
				endWait();
			}
		}
	}

	// The waiting look follows each pack's session; the display ends with the pack (disposed by the caller), before the overlay is ended.
	private static IDisposable? ShowWaiting(WaitController? wait, string language, TranslationModelDownloadSession session)
	{
		if (wait is null)
		{
			return null;
		}

		string name = LanguageNames.NativeName(language);
		var texts = new TranslationDownloadTexts(
			name, AppResources.Format(AppResources.Wait_DownloadingPack, name), null,
			AppResources.Wait_WaitingInternet, AppResources.Wait_WaitingWifi, AppResources.Wait_WaitingPercent, AppResources.Wait_WaitingNoPercent, AppResources.Wait_Retry);
		var view = new TranslationDownloadWait(wait, session, texts);
		view.Show();
		return view;
	}

	private async Task<bool> TranslateAsync(Story story, IReadOnlyList<string> targets, Func<WaitStage, string, CancellationToken> beginWait, Action endWait)
	{
		CancellationToken cancel = beginWait(WaitStage.Translating, AppResources.Wait_Translating);
		try
		{
			return await translator.SaveOwnTranslationAsync(story, targets, cancel);
		}
		catch (OperationCanceledException)
		{
			return false;
		}
		finally
		{
			endWait();
		}
	}
}
