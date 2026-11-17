namespace DinoReader.Core;

/// <summary>What the dark message above the tab bar says after a restore (Slice 396, mock RR2 and RR3; was the result line under the "Bought books before?" card of Slice 364).</summary>
public enum RestoreLine
{
    /// <summary>No message: the restore found books but none reached the shelf, and each failed row already says why.</summary>
    None,

    /// <summary>One book restored ("It's on the shelf").</summary>
    RestoredOne,

    /// <summary>Several books restored ("They're on the shelf").</summary>
    RestoredMany,

    /// <summary>No book came back but a running subscription did ("You are subscribed. Every book is open.").</summary>
    SubscriptionBack,

    /// <summary>The account owns no book and no subscription: "Nothing bought with this ... account yet." with a Redeem a code action.</summary>
    NothingFound,

    /// <summary>The account owns books but every one is already on the shelf and no subscription runs: the green "On the shelf" message (nothing was missing, so nothing was fetched).</summary>
    AllOnShelf,
}

/// <summary>Pure rules for a restore that puts the bought books on the shelf (Slice 364).</summary>
public static class StoreRowRestore
{
    /// <summary>Whether a restored book still has to be fetched: owned by the account and not on the shelf.</summary>
    public static bool NeedsFetch(StoreCardPlan plan) => plan.State == BookState.Owned && plan.Action == StoreAction.Get;

    /// <summary>
    /// The one place that decides the line, once the run has finished: <paramref name="fetched"/> books the restore fetched itself, <paramref name="alreadyDownloading"/> bought books that were
    /// already arriving when it started (awaited like the fetched ones), <paramref name="arrived"/> of those two groups that reached the shelf. Nothing owned off the shelf and nothing arriving
    /// is the subscription line when a subscription runs, else the all-on-shelf line when <paramref name="ownsBooks"/> (the account owns a book, which is already on the shelf), else the nothing-found
    /// line; every book failed is no line (each row's own strip says why).
    /// </summary>
    public static RestoreLine LineFor(int fetched, int arrived, int alreadyDownloading, bool subscriptionActive = false, bool ownsBooks = false)
    {
        if (arrived > 1)
        {
            return RestoreLine.RestoredMany;
        }

        if (arrived == 1)
        {
            return RestoreLine.RestoredOne;
        }

        if (fetched + alreadyDownloading != 0)
        {
            return RestoreLine.None;
        }

        if (subscriptionActive)
        {
            return RestoreLine.SubscriptionBack;
        }

        return ownsBooks ? RestoreLine.AllOnShelf : RestoreLine.NothingFound;
    }
}
