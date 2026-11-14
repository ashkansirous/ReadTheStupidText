using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Slice 313: the store version for which one book's newer text was given up because the store has no usable package for it (see <see cref="BookTextGiveUp"/>). A book given up for the store's
/// version is no longer retried and no longer holds its audio pack back; a different store version is not given up. Network and storage failures never land here.
/// </summary>
internal static class BookTextFailures
{
	private const string KeyPrefix = "dino.book.textgaveup.";

	public static bool IsGivenUp(string id, int storeVersion) => BookTextGiveUp.IsGivenUp(Preferences.Default.Get(KeyPrefix + id, (string?)null), storeVersion);

	public static void GiveUp(string id, int storeVersion) => Preferences.Default.Set(KeyPrefix + id, BookTextGiveUp.Note(storeVersion));
}

/// <summary>
/// Slice 313: brings the text of every shelf book that is behind the store's up to date before its audio pack is planned. <paramref name="beforeDownload"/> runs once, only when something is behind
/// and a download is about to start, and waits for the network rule the pack downloads follow (Wi-Fi unless the grown-up allowed mobile data). Returns the ids whose text is still older, so their
/// packs wait for it.
/// </summary>
public interface IBookTextSync
{
	Task<ISet<string>> SyncTextAsync(IReadOnlyList<DinoReader.Core.StoreItem> items, Func<CancellationToken, Task> beforeDownload, CancellationToken cancel);
}
