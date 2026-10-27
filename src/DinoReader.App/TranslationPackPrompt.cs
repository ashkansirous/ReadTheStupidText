using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;

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
/// pack is there the text and title are translated and saved inside the book.
/// </summary>
public sealed class TranslationPackPrompt(StoryTranslator translator, IStoryLibrary library)
{
	/// <param name="host">The page the question is shown on.</param>
	/// <param name="beginWait">Starts the page's cancellable wait (the token is cancelled by its Cancel button).</param>
	/// <param name="endWait">Ends that wait.</param>
	public async Task<OwnTranslationOutcome> OfferAsync(Page host, string storyId, Func<WaitStage, string, CancellationToken> beginWait, Action endWait)
	{
		if (await library.GetAsync(storyId) is not { } story || !translator.NeedsOwnTranslation(story))
		{
			return OwnTranslationOutcome.NotNeeded;
		}

		IReadOnlyList<string> missing = await translator.MissingPacksAsync(story);
		if (missing.Count > 0 && !await DownloadAsync(host, story, missing, beginWait, endWait))
		{
			return OwnTranslationOutcome.KeptOriginal;
		}

		return await TranslateAsync(story, beginWait, endWait) ? OwnTranslationOutcome.Saved : OwnTranslationOutcome.Stopped;
	}

	private async Task<bool> DownloadAsync(Page host, Story story, IReadOnlyList<string> missing, Func<WaitStage, string, CancellationToken> beginWait, Action endWait)
	{
		string name = LanguageNames.NativeName(StoryTranslator.MyLanguage);
		bool agreed = await host.DisplayAlertAsync(
			AppResources.Format(AppResources.TranslationPack_Title, name), AppResources.Format(AppResources.TranslationPack_Body, name),
			AppResources.TranslationPack_Download, AppResources.TranslationPack_Keep);
		if (!agreed)
		{
			return false;
		}

		CancellationToken cancel = beginWait(WaitStage.DownloadingModel, AppResources.Format(AppResources.Wait_DownloadingPack, name));
		bool ended = false;
		try
		{
			await translator.DownloadPacksAsync(missing, null, cancel);
			return true;
		}
		catch (OperationCanceledException)
		{
			return false;
		}
		catch (TranslationModelUnavailableException ex)
		{
			Debug.WriteLine($"Dino Reader could not get the translation pack: {ex.Message}");
			endWait(); // the alert needs the overlay gone; the finally must not end a wait another screen began meanwhile
			ended = true;
			await host.DisplayAlertAsync(AppResources.App_Name, AppResources.TranslationPack_Failed, AppResources.Common_Ok);
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

	private async Task<bool> TranslateAsync(Story story, Func<WaitStage, string, CancellationToken> beginWait, Action endWait)
	{
		CancellationToken cancel = beginWait(WaitStage.Translating, AppResources.Wait_Translating);
		try
		{
			return await translator.SaveOwnTranslationAsync(story, cancel);
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
