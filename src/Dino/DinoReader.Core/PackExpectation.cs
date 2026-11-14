namespace DinoReader.Core;

/// <summary>Whether a book's audio pack in a language is coming (Slice 360, "Expectation rule after review").</summary>
public enum PackExpectationKind
{
    /// <summary>No pack is coming (an own book, the book's own language, a listing that offers none): the page says nothing about one.</summary>
    NotExpected,

    /// <summary>A pack is coming: while it is not on the phone the page shows the getting, no-internet or failed state.</summary>
    Expected,
}

/// <summary>
/// The one rule for "is a pack expected" (Slice 360, after three review rounds drew findings on its scattered forms). Used by the reading page's view, by the open-book trigger, by the repair log's prune and
/// by the "lacks a pack" check, so they can never disagree. Pure.
/// </summary>
public static class PackExpectation
{
    /// <summary>
    /// With a known listing (this launch's, else the last one saved on the phone): Expected exactly when it lists a pack for <paramref name="language"/>. A live listing read this launch (<paramref name="liveListingRead"/>)
    /// that does not list the book means nothing is coming: NotExpected. With no listing at all known: Expected exactly when the book is a store book
    /// and <paramref name="language"/> is not the book's own language (the store makes a pack for every other family language). The book's own language is never Expected; when it is unknown (null) and there is no listing,
    /// the child's <paramref name="mainLanguage"/> is not Expected either. Anything else: NotExpected. Languages are compared by <see cref="BundledTranslations.Key"/>.
    /// </summary>
    public static PackExpectationKind For(StoreItem? knownListedItem, bool liveListingRead, bool isStoreBook, string language, string? bookOwnLanguage, string mainLanguage)
    {
        string key = BundledTranslations.Key(language);
        if (bookOwnLanguage is not null && BundledTranslations.Key(bookOwnLanguage) == key)
        {
            return PackExpectationKind.NotExpected; // the book's own language is never expected
        }

        if (knownListedItem is not null)
        {
            return knownListedItem.AudioIn(language) is null ? PackExpectationKind.NotExpected : PackExpectationKind.Expected;
        }

        if (liveListingRead)
        {
            return PackExpectationKind.NotExpected; // the store's own list this launch leaves the book out: it is not in the store now, nothing is coming
        }

        bool mainStandsIn = bookOwnLanguage is null && BundledTranslations.Key(mainLanguage) == key; // own language unknown and no listing: the main family language is not expected
        return isStoreBook && !mainStandsIn ? PackExpectationKind.Expected : PackExpectationKind.NotExpected;
    }
}
