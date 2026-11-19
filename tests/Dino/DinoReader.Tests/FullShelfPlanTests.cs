using DinoReader.BookBuilder;
using DinoReader.Core;
using DinoReader.Testing;

namespace DinoReader.Tests;

public class FullShelfPlanTests
{
    private static StoreItem Item(string id, string? title = null, BookSale sale = BookSale.Free) =>
        new(id, title ?? id, "en", 1, 1, null, null, null, "/api/stories/" + id, Sale: sale);

    private static StoreItem[] AllChosen() => [.. FullShelfPlan.BookIds.Select(id => Item(id))];

    [Fact]
    public void The_shelf_is_the_eight_chosen_books_each_once_in_id_order_whatever_the_listing_order_or_title()
    {
        string first = FullShelfPlan.BookIds.Order(StringComparer.OrdinalIgnoreCase).First(id => id != FullShelfPlan.LastBookId);
        FullShelfPlan plan = FullShelfPlan.For([.. AllChosen().Reverse(), Item(first.ToUpperInvariant(), "Zebra")], []);

        Assert.Equal(8, plan.Download.Count);
        Assert.Equal(first, plan.Download[0].Id);
        Assert.Equal(plan.Download.Select(i => i.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count(), plan.Download.Count);
    }

    [Fact]
    public void A_listed_book_outside_the_chosen_eight_is_left_off_and_a_test_book_is_never_fetched()
    {
        string test = FullShelfPlan.BookIds[0];
        FullShelfPlan plan = FullShelfPlan.For([.. AllChosen(), Item("not-chosen")], [test]);

        Assert.DoesNotContain("not-chosen", plan.Download.Select(i => i.Id));
        Assert.DoesNotContain(test, plan.Download.Select(i => i.Id));
        Assert.Equal(FullShelfPlan.BookIds.Count - 1, plan.Download.Count);
    }

    [Fact]
    public void The_last_book_stays_last_whatever_the_ids()
    {
        FullShelfPlan plan = FullShelfPlan.For(AllChosen(), []);

        Assert.Equal(FullShelfPlan.LastBookId, plan.Download[^1].Id);
        Assert.Contains(FullShelfPlan.LastBookId, FullShelfPlan.BookIds);
    }

    [Fact]
    public void The_shelf_is_twelve_books_with_the_four_test_books_and_the_ids_are_the_fetch_scripts_list()
    {
        string json = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "tools", "dino", "full-shelf-books.json"));
        string[] listed = [.. System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("books").EnumerateArray().Select(book => book.GetProperty("id").GetString()!)];

        Assert.Equal(FullShelfPlan.BookIds.Order(), listed.Order());
        Assert.Equal(FullShelfPlan.BookIds.Count, FullShelfPlan.BookIds.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void A_chosen_book_missing_from_the_live_list_is_named_so_the_hook_fails_instead_of_filling_a_short_shelf()
    {
        string gone = FullShelfPlan.BookIds[3];
        FullShelfPlan plan = FullShelfPlan.For([.. FullShelfPlan.BookIds.Where(id => id != gone).Select(id => Item(id))], []);

        Assert.Equal([gone], plan.MissingBooks());
        Assert.Empty(FullShelfPlan.For(AllChosen(), []).MissingBooks());
    }

    [Fact]
    public void The_done_marker_text_lists_the_plan_book_ids_so_another_list_never_matches()
    {
        string[] ids = [.. FullShelfPlan.MarkerText.Split(',')];

        Assert.Equal(FullShelfPlan.BookIds.Order(StringComparer.Ordinal), ids);
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
    public void An_empty_list_plans_nothing_to_download()
    {
        FullShelfPlan plan = FullShelfPlan.For([], ["x"]);

        Assert.Empty(plan.Download);
    }

    [Fact]
    public void A_paid_book_without_its_package_file_is_missing_a_free_one_never_is()
    {
        string[] ids = [FullShelfPlan.BookIds[0], FullShelfPlan.BookIds[1], FullShelfPlan.BookIds[2]];
        FullShelfPlan plan = FullShelfPlan.For([Item(ids[0]), Item(ids[1], sale: BookSale.Paid), Item(ids[2], sale: BookSale.Paid)], []);
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { FullShelfPlan.PackageName(ids[1].ToUpperInvariant()) };

        Assert.Equal([ids[2]], plan.MissingPackages(present));
    }

    [Fact]
    public void The_package_file_is_named_by_the_book_id()
    {
        Assert.Equal("abc123.dino", FullShelfPlan.PackageName("abc123"));
    }
}
