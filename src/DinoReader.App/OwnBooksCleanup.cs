using DinoReader.Core;
using ReadTheStupidText.Application.Usage;

namespace DinoReader;

/// <summary>
/// Slice 278 A3 (design 2): the first start after the update deletes every book a family added themselves (the app now holds store books only; the user decided on 2026-11-06 that they go, no message).
/// One owner, started by the gallery (nothing waits on it, so no loading state). The library never lists an own book (<see cref="FileStoryLibrary.ListAsync"/>), so the delete is invisible and no book can be opened mid-delete.
/// One pass per app run (<see cref="OwnBooksCleanupFlight"/>). "Own" is decided in one place, <see cref="OwnBook.IsOwn"/>; no preference is read, so a store book mid-install (already carrying its id, or with no story.json yet) is never touched.
/// States: not done / done, kept in <see cref="DoneKey"/>; a failed start is counted in <see cref="FailedStartsKey"/> and the pass is marked done after <see cref="OwnBooksCleanupRun.MaxFailedStarts"/> failed starts. Local files only, so offline does not matter.
/// </summary>
public sealed class OwnBooksCleanup(IStoryLibrary library, IFallbackSink sink)
{
	/// <summary>Set once a pass finished clean (or gave up after repeated failures).</summary>
	public const string DoneKey = "dino.ownBooksCleanupDone";

	/// <summary>How many starts ended with a failed delete.</summary>
	public const string FailedStartsKey = "dino.ownBooksCleanupFailedStarts";

	private readonly OwnBooksCleanupFlight _flight = new(library, new PreferenceKeys(), sink);

	/// <summary>Runs the cleanup unless an earlier start finished it or one already ran in this app run; returns how many books it deleted. Never throws.</summary>
	public Task<int> RunOnceAsync() => _flight.RunOnceAsync();

	/// <summary>Lets the next app start run the cleanup again (a backup restore that could not remove a book it brought in).</summary>
	public static void Rearm()
	{
		var keys = new PreferenceKeys();
		keys.FailedStarts = 0;
		keys.Done = false;
	}

	private sealed class PreferenceKeys : IOwnBooksCleanupKeys
	{
		public bool Done
		{
			get => Preferences.Default.Get(DoneKey, false);
			set => Preferences.Default.Set(DoneKey, value);
		}

		public int FailedStarts
		{
			get => Preferences.Default.Get(FailedStartsKey, 0);
			set => Preferences.Default.Set(FailedStartsKey, value);
		}
	}
}
