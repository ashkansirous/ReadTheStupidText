using System.Text.RegularExpressions;

namespace DinoReader.Tests;

/// <summary>
/// Slice 274: on iOS 17 and older, a MAUI view whose platform view is not a MovedToWindow-raising type (UIButton, UISlider, UISwitch, UIStepper, UIProgressView, UIImageView, UIScrollView,
/// a web view, the NavigationPage's UINavigationController view) gets managed KVO observers when anyone subscribes to its Loaded or Unloaded. Discarded while unloaded, such a view crashes in
/// `-[UIView dealloc]` (dotnet/maui #38662, fixed after 10.0.120). Pages (their view is a MauiView) and ContentView, Border, Grid and the other layouts (LayoutView is a MauiView) are safe,
/// so a subscription is allowed only on the receivers listed here, each checked against MAUI 10.0.120 source (docs/plan/slices/274-dino-ios-crash-after-leaving-parent-page.md).
/// </summary>
public partial class LifecycleSubscriptionTests
{
    /// <summary>"file:receiver" ("this" for a bare `Loaded +=`), with the reason it is safe.</summary>
    private static readonly Dictionary<string, string> Allowed = new()
    {
        ["AboutPage.cs:this"] = "a ContentPage: PageHandler's platform view is a MauiView",
        ["SettingsPage.cs:this"] = "a ContentPage",
        ["TileOverlay.xaml.cs:this"] = "a ContentView: platform view is the MauiView ContentView",
        ["WaitOverlay.cs:this"] = "a Grid: platform view is LayoutView, a MauiView",
        ["SettingsRows.cs:host"] = "a ContentView wrapping the Slider (the UISlider itself is not safe)",
        ["DinoKeyTarget.cs:button"] = "a Border: BorderHandler's platform view is the MauiView ContentView (and Windows only)",
    };

    private static readonly string[] Roots =
    [
        Path.Combine("src", "DinoReader.App"),
        Path.Combine("src", "ReadTheStupidText.Mobile.Shared"),
        Path.Combine("src", "ReadTheStupidText.Mobile"),
    ];

    [Fact]
    public void Every_Loaded_or_Unloaded_subscription_is_on_a_receiver_known_to_be_safe_on_iOS()
    {
        Assert.Empty(AllSubscriptions().Where(key => !Allowed.ContainsKey(key)).Distinct());
    }

    [Fact]
    public void The_scan_finds_a_subscription_on_a_slider()
    {
        const string bug = """
            slider.Unloaded += (_, _) => Clear();
            Loaded += (_, _) => Start();
            """;

        Assert.Equal(["Bug.cs:slider", "Bug.cs:this"], Subscriptions(bug, "Bug.cs").ToArray());
    }

    [Fact]
    public void The_scan_finds_a_subscription_on_a_dotted_receiver()
    {
        const string bug = """
            row.Slider.Unloaded += (_, _) => Clear();
            this.Slider.Loaded += OnLoaded;
            ItemsView.Loaded += OnLoaded;
            this.Loaded += OnLoaded;
            items[0].Unloaded += OnUnloaded;
            """;

        Assert.Equal(["Bug.cs:row.Slider", "Bug.cs:this.Slider", "Bug.cs:ItemsView", "Bug.cs:this", "Bug.cs:items[0]"], Subscriptions(bug, "Bug.cs").ToArray());
    }

    [Fact]
    public void The_scan_finds_a_loaded_attribute_in_xaml()
    {
        const string bug = """
            <ContentPage Title="x"
                         Loaded="OnPageLoaded">
              <Slider x:Name="S" Unloaded="OnSliderUnloaded" />
              <Button Text="no event" />
            </ContentPage>
            """;

        Assert.Equal(["Bug.xaml:ContentPage", "Bug.xaml:Slider"], Subscriptions(bug, "Bug.xaml").ToArray());
    }

    [Fact]
    public void The_allow_list_has_no_stale_entries()
    {
        HashSet<string> present = [.. AllSubscriptions()];

        Assert.Empty(Allowed.Keys.Where(key => !present.Contains(key)));
    }

    private static IEnumerable<string> AllSubscriptions()
    {
        foreach (string root in Roots)
        {
            string dir = Path.Combine(RepoRoot(), root);
            foreach (string file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories).Where(IsSource))
            {
                foreach (string key in Subscriptions(File.ReadAllText(file), Path.GetFileName(file)))
                {
                    yield return key;
                }
            }
        }
    }

    private static IEnumerable<string> Subscriptions(string source, string fileName)
    {
        if (fileName.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
        {
            return XamlAttributeRegex().Matches(source).Select(m => $"{fileName}:{m.Groups["element"].Value}");
        }

        return SubscribeRegex().Matches(source).Select(m => $"{fileName}:{(m.Groups["receiver"].Success ? m.Groups["receiver"].Value : "this")}");
    }

    private static bool IsSource(string path) =>
        (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}");

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src", "DinoReader.App")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new DirectoryNotFoundException("Repo root not found.");
    }

    // A dotted or indexed receiver ("row.Slider", "this.Slider", "items[0]") is kept whole; a bare "Loaded +=" is the class itself ("this").
    [GeneratedRegex(@"(?:(?<receiver>[_A-Za-z][\w.\[\]()?!]*?)\.|(?<![\w.)\]]))(?:Loaded|Unloaded)\s*\+=")]
    private static partial Regex SubscribeRegex();

    // An event attribute on an element: "<Slider x:Name=... Unloaded=\"...\"" gives "Slider" (the tag name is the last "<Name" before the attribute).
    [GeneratedRegex(@"<(?<element>[\w:.]+)(?:\s+[^<>]*?)?\s(?:Loaded|Unloaded)\s*=\s*""")]
    private static partial Regex XamlAttributeRegex();
}
