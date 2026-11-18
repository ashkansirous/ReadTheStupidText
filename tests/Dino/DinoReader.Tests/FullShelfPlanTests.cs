using DinoReader.BookBuilder;
using DinoReader.Core;
using DinoReader.Testing;

namespace DinoReader.Tests;

public class FullShelfPlanTests
{
    private static StoreItem Item(string id, string? title = null, BookSale sale = BookSale.Free) =>
        new(id, title ?? id, "en", 1, 1, null, null, null, "/api/stories/" + id, Sale: sale);

    [Fact]
    public void Every_listed_book_is_planned_once_in_id_order_whatever_its_title()
    {
        FullShelfPlan plan = FullShelfPlan.For([Item("c", "Alpha"), Item("a", "Zebra"), Item("A", "Ant"), Item("b", "Mouse")], []);

        Assert.Equal(["a", "b", "c"], plan.Download.Select(i => i.Id));
    }

    [Fact]
    public void New_books_come_first_then_the_ones_replacing_a_test_copy_then_the_pinned_last_book()
    {
        FullShelfPlan plan = FullShelfPlan.For([Item(FullShelfPlan.LastBookId), Item("a-test"), Item("c-new"), Item("b-new")], ["a-test"]);

        Assert.Equal(["b-new", "c-new", "a-test", FullShelfPlan.LastBookId], plan.Download.Select(i => i.Id));
    }

    [Fact]
    public void The_last_book_stays_last_when_a_new_book_has_a_higher_id()
    {
        FullShelfPlan plan = FullShelfPlan.For([Item("zzz-new"), Item(FullShelfPlan.LastBookId)], []);

        Assert.Equal(FullShelfPlan.LastBookId, plan.Download[^1].Id);
    }

    [Fact]
    public void Every_flow_that_waits_for_the_shelf_waits_for_the_last_book_title()
    {
        string root = StarterBuilder.RepoRoot();
        string[] flows = [.. Directory.GetFiles(Path.Combine(root, ".maestro", "dino"), "store-shot-*.yaml"), .. Directory.GetFiles(Path.Combine(root, ".maestro", "dino", "needs-file"), "store-shot-*.yaml")];
        string[] waiting = [.. flows.Where(file => File.ReadAllText(file).Contains("full-shelf", StringComparison.Ordinal))];

        Assert.NotEmpty(waiting);
        Assert.All(waiting, file => Assert.Contains(".*" + FullShelfPlan.LastBookTitle + ".*", File.ReadAllText(file)));
    }

    [Fact]
    public void Only_the_test_books_are_cleared_never_a_book_about_to_be_fetched()
    {
        FullShelfPlan plan = FullShelfPlan.For([Item("pigs"), Item("wolf")], ["pigs", "test-only"]);

        Assert.Equal(["pigs", "test-only"], plan.ClearFirst.Order());
    }

    [Fact]
    public void An_empty_list_plans_nothing_to_download()
    {
        FullShelfPlan plan = FullShelfPlan.For([], ["x"]);

        Assert.Empty(plan.Download);
        Assert.Equal(["x"], plan.ClearFirst);
    }

    [Fact]
    public void A_paid_book_without_its_package_file_is_missing_a_free_one_never_is()
    {
        FullShelfPlan plan = FullShelfPlan.For([Item("free"), Item("paid-a", sale: BookSale.Paid), Item("paid-b", sale: BookSale.Paid)], []);
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { FullShelfPlan.PackageName("PAID-A") };

        Assert.Equal(["paid-b"], plan.MissingPackages(present));
    }

    [Fact]
    public void The_package_file_is_named_by_the_book_id()
    {
        Assert.Equal("abc123.dino", FullShelfPlan.PackageName("abc123"));
    }
}
