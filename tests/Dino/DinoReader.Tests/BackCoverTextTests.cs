using System.Globalization;
using System.Xml.Linq;
using DinoReader.BookBuilder;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 405: the back cover's lines in a reading language, built by the app's own <see cref="BackCoverText"/> over the real strings files and each book's own names.</summary>
public class BackCoverTextTests
{
    private const string StringsFolder = "src/Dino/DinoReader.App/Resources/Strings";

    private static readonly BackCoverText Text = new(Lookup);

    private static string Lookup(string key, CultureInfo culture)
    {
        string file = culture.Name.Length == 0 ? "AppResources.resx" : $"AppResources.{culture.Name}.resx";
        return ValueOf(file, key) ?? ValueOf("AppResources.resx", key) ?? key;
    }

    private static string? ValueOf(string file, string key)
    {
        string path = Path.Combine(Books.RepoRoot(), StringsFolder, file);
        return !File.Exists(path) ? null : XDocument.Load(path).Descendants("data").FirstOrDefault(d => (string?)d.Attribute("name") == key)?.Element("value")?.Value;
    }

    private static IReadOnlyList<string> LinesOf(string book, string language) => Text.LinesIn(Books.Folder(book).BackCover!, language);

    [Fact]
    public void A_farsi_back_cover_writes_the_names_in_persian_letters()
    {
        IReadOnlyList<string> lines = LinesOf("lion-and-mouse", "fa");

        Assert.Equal("داستان اصلی از ازوپ", lines[0]);
        Assert.Equal("بازنویسی برای کودکان توسط اشکان سیروس", lines[1]);
    }

    [Fact]
    public void A_german_back_cover_uses_the_german_spellings()
    {
        IReadOnlyList<string> lines = LinesOf("lion-and-mouse", "de");

        Assert.Equal("Originalgeschichte von Äsop", lines[0]);
        Assert.Equal("Für junge Leser nacherzählt von Ashkan Sirous", lines[1]);
    }

    [Fact]
    public void A_japanese_back_cover_writes_the_names_in_japanese()
    {
        IReadOnlyList<string> lines = LinesOf("lion-and-mouse", "ja");

        Assert.Equal("原作：イソップ", lines[0]);
        Assert.Equal("こどもむけに書きなおし：アシュカン・シルース", lines[1]);
    }

    [Fact]
    public void Two_writers_are_joined_with_the_languages_own_and()
    {
        Assert.Equal("داستان اصلی از شارل پرو و برادران گریم", LinesOf("red-riding-hood", "fa")[0]);
        Assert.Equal("Originalgeschichte von Charles Perrault und den Brüdern Grimm", LinesOf("red-riding-hood", "de")[0]);
    }

    [Theory]
    [InlineData("fr", "Histoire originale de Charles Perrault, Jacob et Wilhelm Grimm")]
    [InlineData("pt", "Charles Perrault, Jacob e Wilhelm Grimm")]
    public void A_spelling_that_holds_its_own_and_makes_the_join_a_list_so_it_never_reads_as_three_people(string language, string expectedEnd)
    {
        Assert.EndsWith(expectedEnd, LinesOf("red-riding-hood", language)[0]);
    }

    [Fact]
    public void A_name_or_language_the_book_does_not_spell_stays_as_written_and_an_unknown_code_reads_english()
    {
        var cover = new BackCover("Aesop", true, BackCover.Maker, 2026, 11);

        Assert.Equal("Original story by Aesop", Text.LinesIn(cover, "xx")[0]);
        Assert.Equal("Originalgeschichte von Aesop", Text.LinesIn(cover, "de")[0]);
    }
}
