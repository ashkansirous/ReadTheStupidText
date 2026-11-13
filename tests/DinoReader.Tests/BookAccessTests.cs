using DinoReader.Core;

namespace DinoReader.Tests;

public class BookAccessTests
{
    private const string Id = "0123456789abcdef0123456789abcdef";
    private const string Product = "uk.sirous.dinoreader.book.goldilocks";
    private const string Bundle = "uk.sirous.dinoreader.bundle.classics";
    private static readonly HashSet<string> None = [];

    private static StoreItem Book(BookSale sale = BookSale.Paid, params string[] products) =>
        new(Id, "Goldilocks", "en", 1, 1, 5, 3, null, "/d", Sale: sale, ProductId: Product, UnlockedBy: products.Length == 0 ? [Product] : products);

    [Fact]
    public void WithTheSwitchOff_EveryBookIsFree_AndNeedsNoProof()
    {
        StoreItem paid = Book();
        Assert.Equal(BookState.Free, BookAccess.StateOf(false, paid, false, None, None));
        Assert.False(BookAccess.NeedsProof(false, paid));
        Assert.Equal(StoreAction.Get, BookAccess.Plan(false, paid, false, None, None, "$0.99").Action);
    }

    [Fact]
    public void AFreeBook_IsFree_EvenWithTheSwitchOn()
    {
        Assert.Equal(BookState.Free, BookAccess.StateOf(true, Book(BookSale.Free), false, None, None));
    }

    [Fact]
    public void ABookNotOwnedAndNotOnTheShelf_CanBeBought_AtTheStoresOwnPrice()
    {
        StoreCardPlan plan = BookAccess.Plan(true, Book(), false, None, None, "£0.89");
        Assert.Equal((BookState.Buyable, StoreAction.Buy, "£0.89"), (plan.State, plan.Action, plan.Price));
    }

    [Fact]
    public void WithoutAPrice_TheCardSaysConnectToBuy_AndShowsNoPrice()
    {
        foreach (string? price in new string?[] { null, "", "  " })
        {
            StoreCardPlan plan = BookAccess.Plan(true, Book(), false, None, None, price);
            Assert.Equal((StoreAction.ConnectToBuy, (string?)null), (plan.Action, plan.Price));
        }
    }

    [Fact]
    public void AnOwnedBook_IsGotLikeAFreeOne_AndMarkedDownloadedOnTheShelf()
    {
        HashSet<string> owned = [Product];
        Assert.Equal(StoreAction.Get, BookAccess.Plan(true, Book(), false, owned, None, null).Action);
        Assert.Equal(StoreAction.OnShelf, BookAccess.Plan(true, Book(), true, owned, None, null).Action);
        Assert.Equal(BookState.Owned, BookAccess.StateOf(true, Book(), true, owned, None));
    }

    [Fact]
    public void ABundleThatListsTheBook_UnlocksIt()
    {
        StoreItem inBundle = Book(BookSale.Paid, Product, Bundle);
        Assert.Equal(Bundle, BookAccess.OwnedProduct(inBundle, new HashSet<string> { Bundle }));
        Assert.Equal(BookState.Owned, BookAccess.StateOf(true, inBundle, false, new HashSet<string> { Bundle }, None));
        Assert.Null(BookAccess.OwnedProduct(inBundle, new HashSet<string> { "other" }));
    }

    [Fact]
    public void ABookOnTheShelfFromBefore_StaysReadable_IsNotBuyable_AndGetsNoUpdates()
    {
        StoreCardPlan plan = BookAccess.Plan(true, Book(), true, None, None, "$0.99");
        Assert.Equal((BookState.KeptFromBefore, StoreAction.OnShelf), (plan.State, plan.Action));
        Assert.False(BookAccess.MayUpdate(true, Book(), None));
        Assert.True(BookAccess.MayUpdate(true, Book(), new HashSet<string> { Product }));
        Assert.True(BookAccess.MayUpdate(true, Book(BookSale.Free), None));
        Assert.True(BookAccess.MayUpdate(false, Book(), None));
    }

    [Fact]
    public void APendingPurchase_ShowsWaiting_NotBuy()
    {
        StoreCardPlan plan = BookAccess.Plan(true, Book(), false, None, new HashSet<string> { Product }, "$0.99");
        Assert.Equal((BookState.Pending, StoreAction.Waiting, (string?)null), (plan.State, plan.Action, plan.Price));
    }

    [Fact]
    public void ThePicker_OffersOnlyBooksThatAreNotSold_InListingOrder()
    {
        StoreItem a = Book(BookSale.Free) with { Id = new string('a', 32) };
        StoreItem b = Book() with { Id = new string('b', 32) };
        StoreItem c = Book(BookSale.Free) with { Id = new string('c', 32) };
        Assert.Equal([a.Id, c.Id], BookAccess.FirstRunOffer(true, [a, b, c]).Select(i => i.Id));
        Assert.Equal(3, BookAccess.FirstRunOffer(false, [a, b, c]).Count);
    }

    [Fact]
    public void WhileTheSwitchIsUnread_TheFirstRunPicker_OffersOnlyBooksTheListingSaysAreFree()
    {
        StoreItem free = Book(BookSale.Free) with { Id = new string('a', 32) };
        StoreItem paid = Book() with { Id = new string('b', 32) };
        Assert.True(BookAccess.AssumeSold(configKnown: false, paidBooksOn: false)); // no config read yet: the unread default (off) is not trusted
        Assert.False(BookAccess.AssumeSold(configKnown: true, paidBooksOn: false));
        Assert.True(BookAccess.AssumeSold(configKnown: true, paidBooksOn: true));
        Assert.Equal([free.Id], BookAccess.FirstRunOffer(BookAccess.AssumeSold(false, false), [free, paid]).Select(i => i.Id));
        Assert.Equal(2, BookAccess.FirstRunOffer(BookAccess.AssumeSold(true, false), [free, paid]).Count);
    }

    [Theory]
    [InlineData(BookSale.Free, false, true, LegacyBookSource.Store)]
    [InlineData(BookSale.Free, false, false, LegacyBookSource.File)]
    [InlineData(BookSale.Free, true, true, LegacyBookSource.Store)]
    [InlineData(BookSale.Free, true, false, LegacyBookSource.File)]
    [InlineData(BookSale.Paid, true, true, LegacyBookSource.Store)]
    [InlineData(BookSale.Paid, true, false, LegacyBookSource.File)]
    [InlineData(BookSale.Paid, false, true, LegacyBookSource.ListOnly)]
    [InlineData(BookSale.Paid, false, false, LegacyBookSource.ListOnly)]
    public void AnOldBackupsBook_ComesFromTheStore_ElseItsFile_AndASoldUnownedBookIsOnlyListed(BookSale access, bool owned, bool storeCanSupply, LegacyBookSource expected) =>
        Assert.Equal(expected, BookAccess.LegacyBookChoice(access, owned, storeCanSupply));

    [Theory]
    [InlineData(true, true, BookSale.Paid)]
    [InlineData(true, false, BookSale.Free)] // the listing says free: free
    [InlineData(false, false, BookSale.Free)] // not listed (delisted, or the listing could not be read): free, so the backup's own file restores it
    public void ABookTheListingDoesNotName_CountsAsFree_SoItsFileRestoresIt(bool listed, bool soldPerListing, BookSale expected) =>
        Assert.Equal(expected, BookAccess.LegacyAccess(listed, soldPerListing));

    [Fact]
    public void AnUnlistedBooksBackupFile_RestoresIt_WhenTheStoreCannotSupplyIt() =>
        Assert.Equal(LegacyBookSource.File, BookAccess.LegacyBookChoice(BookAccess.LegacyAccess(listed: false, soldPerListing: false), owned: false, storeCanSupply: false));

    [Fact]
    public void AFailedPriceQuery_KeepsTheLastKnownPrices_AndANewAnswerOverlaysThem()
    {
        var before = new Dictionary<string, string> { ["a"] = "$1", ["b"] = "$2" };
        Assert.Equal(before, BookAccess.KeepPrices(before, new Dictionary<string, string>()));
        IReadOnlyDictionary<string, string> merged = BookAccess.KeepPrices(before, new Dictionary<string, string> { ["b"] = "$3", ["c"] = "$4" });
        Assert.Equal(new Dictionary<string, string> { ["a"] = "$1", ["b"] = "$3", ["c"] = "$4" }, merged);
    }

    [Fact]
    public void TheProofHeader_NamesTheStoreProductAndToken_ExceptOnWindows()
    {
        var proofs = new Dictionary<string, string> { [Product] = "tok" };
        Assert.Equal($"android:{Product}:tok", BookAccess.ProofHeader(true, Book(), proofs, PurchaseProofs.AndroidStore));
        Assert.Equal("windows::tok", BookAccess.ProofHeader(true, Book(), proofs, PurchaseProofs.WindowsStore));
        Assert.Null(BookAccess.ProofHeader(false, Book(), proofs, PurchaseProofs.AndroidStore));
        Assert.Null(BookAccess.ProofHeader(true, Book(BookSale.Free), proofs, PurchaseProofs.AndroidStore));
        Assert.Null(BookAccess.ProofHeader(true, Book(), new Dictionary<string, string>(), PurchaseProofs.AndroidStore));
    }

    [Fact]
    public void TheListing_ReadsAccessAndProducts_AndAnOlderListingIsAllFree()
    {
        const string Entry = """{"id":"0123456789abcdef0123456789abcdef","title":"B","language":"en","schemaVersion":1,"version":1,"downloadUrl":"/d","access":"paid","productId":"uk.sirous.dinoreader.book.b","unlockedBy":["uk.sirous.dinoreader.book.b",7,"uk.sirous.dinoreader.bundle.c"]}""";
        StoreItem paid = Assert.Single(StoreCatalog.Parse($$"""{"stories":[{{Entry}}]}"""));
        Assert.Equal((BookSale.Paid, "uk.sirous.dinoreader.book.b"), (paid.Sale, paid.ProductId));
        Assert.Equal(["uk.sirous.dinoreader.book.b", "uk.sirous.dinoreader.bundle.c"], paid.Products);
        StoreItem old = Assert.Single(StoreCatalog.Parse("""{"stories":[{"id":"0123456789abcdef0123456789abcdef","title":"B","language":"en","schemaVersion":1,"version":1,"downloadUrl":"/d"}]}"""));
        Assert.Equal((BookSale.Free, 0), (old.Sale, old.Products.Count));
    }

    [Theory]
    [InlineData(false, false, LegacyBookFiles.Restore)]
    [InlineData(false, true, LegacyBookFiles.Restore)]
    [InlineData(true, false, LegacyBookFiles.Restore)]
    [InlineData(true, true, LegacyBookFiles.StoreFirst)]
    public void ASchema1Restore_UsesTheStoreFirstOnlyWhenTheSwitchIsKnownToBeOn(bool known, bool on, LegacyBookFiles expected)
    {
        Assert.Equal(expected, BookAccess.LegacyFilesMode(known, on));
    }

    [Fact]
    public void ThePaidBooksSwitch_IsOnOnlyForALiteralTrue()
    {
        Assert.False(AppConfig.Default.PaidBooksEnabled);
        Assert.True(AppConfig.Parse("""{"paidBooks":{"enabled":true}}""").PaidBooksEnabled);
        foreach (string json in new[] { "{}", """{"paidBooks":{"enabled":"true"}}""", """{"paidBooks":{"enabled":1}}""", """{"paidBooks":true}""", """{"paidBooks":{"enabled":false}}""", "not json" })
        {
            Assert.False(AppConfig.Parse(json).PaidBooksEnabled, json);
        }
    }
}
