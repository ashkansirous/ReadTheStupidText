using DinoReader.Core;

namespace DinoReader.Tests;

public sealed class ShelfOrderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-order-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static Story Make(string id, int day) => new(
        id, id, "en", new DateTimeOffset(2026, 10, day, 0, 0, 0, TimeSpan.Zero),
        [new StoryPage("Text.")], [], CoverStyle.Colour, 0, "F1");

    [Fact]
    public void FiveDroppedBeforeThreeGivesOneTwoFiveThreeFourSix() =>
        Assert.Equal(["1", "2", "5", "3", "4", "6"], ShelfOrder.MoveBefore(["1", "2", "3", "4", "5", "6"], "5", "3"));

    [Fact]
    public void DroppingWithNoTargetPutsTheBookLast() =>
        Assert.Equal(["2", "3", "1"], ShelfOrder.MoveBefore(["1", "2", "3"], "1", null));

    [Fact]
    public void DroppingOnItselfChangesNothing() =>
        Assert.Equal(["1", "2", "3"], ShelfOrder.MoveBefore(["1", "2", "3"], "2", "2"));

    [Fact]
    public void NewBooksComeFirstThenStoredOrderAndGoneIdsAreDropped()
    {
        string[] shelf = ["new1", "b", "new2", "a"];

        List<string> ordered = ShelfOrder.Apply(shelf, id => id, ["a", "gone", "b"]);

        Assert.Equal(["new1", "new2", "a", "b"], ordered);
    }

    [Fact]
    public void NoStoredOrderKeepsTodaysOrder() =>
        Assert.Equal(["x", "y"], ShelfOrder.Apply(["x", "y"], id => id, []));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"a\":1}")]
    public void AnUnreadableFileIsAnEmptyOrderNotACrash(string? json) =>
        Assert.Empty(ShelfOrder.Parse(json));

    [Fact]
    public void SerializeThenParseRoundTrips() =>
        Assert.Equal(["a", "b"], ShelfOrder.Parse(ShelfOrder.Serialize(["a", "b"])));

    [Fact]
    public async Task TheLibraryListsInTheSavedOrderAndALaterBookGoesFirst()
    {
        var library = new FileStoryLibrary(_root);
        string a = StoryIds.New();
        string b = StoryIds.New();
        string c = StoryIds.New();
        await library.AddAsync(Make(a, 1));
        await library.AddAsync(Make(b, 2));
        await library.AddAsync(Make(c, 3));
        Assert.Equal([c, b, a], (await library.ListAsync()).Select(s => s.Id));

        await library.SaveOrderAsync([a, c, b]);
        Assert.Equal([a, c, b], (await library.ListAsync()).Select(s => s.Id));

        string d = StoryIds.New();
        await library.AddAsync(Make(d, 4));
        Assert.Equal([d, a, c, b], (await library.ListAsync()).Select(s => s.Id));
    }

    [Fact]
    public void MoveToSlotPutsTheBookAtTheSlotAmongTheOthers()
    {
        Assert.Equal(["b", "c", "d", "a"], ShelfOrder.MoveToSlot(["a", "b", "c", "d"], "a", 3));
        Assert.Equal(["b", "a", "c", "d"], ShelfOrder.MoveToSlot(["a", "b", "c", "d"], "a", 1));
        Assert.Equal(["d", "a", "b", "c"], ShelfOrder.MoveToSlot(["a", "b", "c", "d"], "d", -5));
    }

    [Fact]
    public async Task ARemovedBookThatComesBackGoesFirstNotToItsOldSlot()
    {
        var library = new FileStoryLibrary(_root);
        string a = StoryIds.New();
        string b = StoryIds.New();
        string c = StoryIds.New();
        await library.AddAsync(Make(a, 1));
        await library.AddAsync(Make(b, 2));
        await library.AddAsync(Make(c, 3));
        await library.SaveOrderAsync([a, b, c]);

        await library.DeleteAsync(a);
        await library.AddAsync(Make(a, 1));

        Assert.Equal([a, b, c], (await library.ListAsync()).Select(s => s.Id));
        await library.SaveOrderAsync([b, c, a]);
        await library.DeleteAsync(c);
        await library.AddAsync(Make(c, 3));
        Assert.Equal([c, b, a], (await library.ListAsync()).Select(s => s.Id));
    }

    [Fact]
    public async Task ACorruptOrderFileLeavesTodaysOrder()
    {
        var library = new FileStoryLibrary(_root);
        string a = StoryIds.New();
        string b = StoryIds.New();
        await library.AddAsync(Make(a, 1));
        await library.AddAsync(Make(b, 2));
        await File.WriteAllTextAsync(Path.Combine(_root, ShelfOrder.FileName), "}{");

        Assert.Equal([b, a], (await library.ListAsync()).Select(s => s.Id));
    }
}

public class ShelfOrderGridTests
{
    private static readonly ShelfOrderGrid Grid = new(3, 100, 150, 10, 20, 5, 7);

    [Theory]
    [InlineData(0, 5, 7)]
    [InlineData(2, 225, 7)]
    [InlineData(3, 5, 177)]
    [InlineData(4, 115, 177)]
    public void OriginIsRowAndColumnFromTheTopLeft(int index, double x, double y) =>
        Assert.Equal((x, y), Grid.Origin(index));

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(120, 20, 1)]
    [InlineData(300, 20, 2)]
    [InlineData(60, 200, 3)]
    [InlineData(900, 900, 4)]
    [InlineData(-50, -50, 0)]
    public void SlotAtFindsTheSpotUnderAPointAndClampsToTheLastBook(double x, double y, int slot) =>
        Assert.Equal(slot, Grid.SlotAt(x, y, 5));

    [Fact]
    public void FitSplitsTheWidthIntoEqualTiles()
    {
        ShelfOrderGrid fit = ShelfOrderGrid.Fit(360, 3, 14, 12, 12, 36, 7);

        Assert.Equal(102.67, fit.TileWidth, 2);
        Assert.Equal((14, 12), fit.Origin(0));
    }

    [Fact]
    public void ContentHeightEndsAtTheLastRow() =>
        Assert.Equal(7 + (2 * 170) - 20, Grid.ContentHeight(5));
}

public class ShelfOrderKeysTests
{
    [Theory]
    [InlineData(DinoKey.Escape, DinoKeyAction.ReorderCancel)]
    [InlineData(DinoKey.Enter, DinoKeyAction.ReorderDone)]
    [InlineData(DinoKey.Space, DinoKeyAction.ReorderToggle)]
    [InlineData(DinoKey.Left, DinoKeyAction.ReorderLeft)]
    [InlineData(DinoKey.Right, DinoKeyAction.ReorderRight)]
    [InlineData(DinoKey.Up, DinoKeyAction.ReorderUp)]
    [InlineData(DinoKey.Down, DinoKeyAction.ReorderDown)]
    [InlineData(DinoKey.Digit1, DinoKeyAction.None)]
    public void ReorderScreenKeys(DinoKey key, DinoKeyAction expected) =>
        Assert.Equal(expected, DinoKeyMap.Resolve(key, DinoScreen.Reorder, textFieldFocused: false, keyDown: true));

    [Theory]
    [InlineData(4, DinoKeyAction.ReorderLeft, 3)]
    [InlineData(0, DinoKeyAction.ReorderLeft, 0)]
    [InlineData(4, DinoKeyAction.ReorderRight, 5)]
    [InlineData(5, DinoKeyAction.ReorderRight, 5)]
    [InlineData(4, DinoKeyAction.ReorderUp, 1)]
    [InlineData(1, DinoKeyAction.ReorderUp, 1)]
    [InlineData(2, DinoKeyAction.ReorderDown, 5)]
    [InlineData(4, DinoKeyAction.ReorderDown, 4)]
    [InlineData(4, DinoKeyAction.ReorderToggle, 4)]
    public void ArrowsStepOneSpotOrOneRowAndStayOnTheGrid(int index, DinoKeyAction arrow, int expected) =>
        Assert.Equal(expected, ShelfOrderKeys.Target(index, 6, 3, arrow));
}
