using System.Text.RegularExpressions;

namespace DinoReader.Tests;

/// <summary>
/// Every word a Dino screen shows comes from AppResources (Slice 159, Decision 164): a literal assigned to a visible text property in XAML or C# would stay English in
/// every other language. Names, symbols and numbers (no two letters in a row) pass.
/// </summary>
public partial class NoHardCodedUiTextTests
{
    private static readonly string AppFolder = Path.Combine("src", "DinoReader.App");

    [Fact]
    public void No_xaml_text_property_holds_a_literal_word()
    {
        List<string> found = [];
        foreach (string file in Files("*.xaml"))
        {
            found.AddRange(XamlLiteral().Matches(File.ReadAllText(file)).Select(m => $"{Path.GetFileName(file)}: {m.Value}"));
        }

        Assert.Empty(found);
    }

    [Fact]
    public void No_csharp_text_property_or_alert_holds_a_literal_word()
    {
        List<string> found = [];
        foreach (string file in Files("*.cs"))
        {
            string source = File.ReadAllText(file);
            found.AddRange(CsLiteral().Matches(source).Select(m => $"{Path.GetFileName(file)}: {m.Value}"));
            found.AddRange(AlertLiteral().Matches(source).Select(m => $"{Path.GetFileName(file)}: {m.Value}"));
        }

        Assert.Empty(found);
    }

    private static IEnumerable<string> Files(string pattern)
    {
        string root = Path.Combine(RepoRoot(), AppFolder);
        return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => !f.Contains(Path.Combine("Resources", "Strings")));
    }

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src", "DinoReader.App")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new DirectoryNotFoundException("Repo root not found.");
    }

    [GeneratedRegex("""\b(?:Text|Title|Placeholder|Description|Hint|Header|Detail)="[^"{&][^"]*?[A-Za-z]{2}""")]
    private static partial Regex XamlLiteral();

    [GeneratedRegex("""\b(?:Text|Title|Placeholder|Description|Hint|Detail|AccessibleName)\s*=\s*(?:"[^"]*?|\$"(?:\{[^}]*\}|[^"{}])*?)[A-Za-z]{2}""")]
    private static partial Regex CsLiteral();

    [GeneratedRegex("""DisplayAlertAsync\(\s*(?:"[^"]*?|\$"(?:\{[^}]*\}|[^"{}])*?)[A-Za-z]{2}""")]
    private static partial Regex AlertLiteral();
}
