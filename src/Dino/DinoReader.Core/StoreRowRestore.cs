namespace DinoReader.Core;

/// <summary>What the result line under the Store's "Bought books before?" card says after a restore (Slice 364, mock S3).</summary>
public enum RestoreLine
{
    /// <summary>No line: the restore found books but none reached the shelf, and each failed row already says why.</summary>
    None,

    /// <summary>The green line: one book restored ("It's on the shelf").</summary>
    RestoredOne,

    /// <summary>The green line: several books restored ("They're on the shelf").</summary>
    RestoredMany,

    /// <summary>The sand line: the account owns no book that is missing from the shelf.</summary>
    NothingToRestore,
}

/// <summary>Pure rules for a restore that puts the bought books on the shelf (Slice 364).</summary>
public static class StoreRowRestore
{
    /// <summary>Whether a restored book still has to be fetched: owned by the account and not on the shelf.</summary>
    public static bool NeedsFetch(StoreCardPlan plan) => plan.State == BookState.Owned && plan.Action == StoreAction.Get;

    /// <summary>
    /// The one place that decides the line, once the run has finished: <paramref name="fetched"/> books the restore fetched itself, <paramref name="alreadyDownloading"/> bought books that were
    /// already arriving when it started (awaited like the fetched ones), <paramref name="arrived"/> of those two groups that reached the shelf. Nothing owned off the shelf and nothing arriving
    /// is the sand line; every book failed is no line (each row's own strip says why).
    /// </summary>
    public static RestoreLine LineFor(int fetched, int arrived, int alreadyDownloading)
    {
        if (arrived > 1)
        {
            return RestoreLine.RestoredMany;
        }

        if (arrived == 1)
        {
            return RestoreLine.RestoredOne;
        }

        return fetched + alreadyDownloading == 0 ? RestoreLine.NothingToRestore : RestoreLine.None;
    }
}
