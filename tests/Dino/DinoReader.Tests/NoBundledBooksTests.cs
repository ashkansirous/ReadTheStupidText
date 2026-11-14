namespace DinoReader.Tests;

/// <summary>
/// Slice 261: no book ships in the app. The four former starters are Debug-only test books; the project file must carry them as an asset only for a Debug build, so a Release package
/// (the Play and App Store builds) holds no book and is about 13 MB smaller.
/// </summary>
public class NoBundledBooksTests
{
    private static string AppFolder() => Path.Combine(RepoRoot(), "src", "Dino", "DinoReader.App");

    [Fact]
    public void The_test_books_are_an_asset_only_in_a_Debug_build()
    {
        string[] assetLines = File.ReadAllLines(Path.Combine(AppFolder(), "DinoReader.App.csproj"))
            .Where(line => line.Contains("<MauiAsset", StringComparison.Ordinal) && line.Contains(".dino", StringComparison.Ordinal))
            .ToArray();

        string testBooks = Assert.Single(assetLines);
        Assert.Contains(@"Resources\Raw\testbooks\", testBooks);
        Assert.Contains("Condition=\"'$(Configuration)' == 'Debug' Or '$(DinoTestBooks)' == 'true'\"", testBooks);
    }

    [Fact]
    public void No_CI_workflow_or_store_build_script_sets_DinoTestBooks()
    {
        // Only the Windows VM test package (tools/desktop-vm/Build-TestPackage.ps1) may set it; a Store or CI build must never carry the test books.
        string root = RepoRoot();
        string[] shipping = Directory.GetFiles(Path.Combine(root, ".github"), "*.*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith("CLAUDE.md", StringComparison.Ordinal) && !f.Contains(@"\scripts\", StringComparison.Ordinal) && !f.Contains("/scripts/", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("DinoTestBooks", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(shipping);
    }

    [Fact]
    public void The_test_store_constants_are_set_only_for_a_Debug_build_or_DinoTestBooks()
    {
        string project = File.ReadAllText(Path.Combine(AppFolder(), "DinoReader.App.csproj"));

        Assert.Contains("<PropertyGroup Condition=\"'$(Configuration)' == 'Debug' Or '$(DinoTestBooks)' == 'true'\">", project);
        Assert.Contains("<PropertyGroup Condition=\"'$(DinoTestBooks)' == 'true'\">", project);
    }

    [Fact]
    public void The_app_has_no_starters_folder_and_its_project_names_none()
    {
        Assert.False(Directory.Exists(Path.Combine(AppFolder(), "Resources", "Raw", "starters")));
        Assert.DoesNotContain(@"Raw\starters", File.ReadAllText(Path.Combine(AppFolder(), "DinoReader.App.csproj")));
    }

    [Fact]
    public void The_four_test_books_are_in_the_Debug_asset_folder()
    {
        string[] files = Directory.GetFiles(Path.Combine(AppFolder(), "Resources", "Raw", "testbooks"), "*.dino").Select(Path.GetFileName).OfType<string>().Order().ToArray();

        Assert.Equal(["gingerbread-man.dino", "good-enough.dino", "three-little-pigs.dino", "tortoise-and-hare.dino"], files);
    }

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src", "Dino", "DinoReader.App")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new DirectoryNotFoundException("Repo root not found.");
    }
}
