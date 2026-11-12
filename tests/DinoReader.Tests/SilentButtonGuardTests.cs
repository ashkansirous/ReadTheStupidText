using System.Text.RegularExpressions;
using DinoReader.BookBuilder;

namespace DinoReader.Tests;

/// <summary>
/// Slice 352 item C: no button is silent. Every place in <c>src/DinoReader.App</c> that wires a press (a <c>Clicked</c> or <c>Toggled</c> handler, a <c>Taps.AddTap</c>, a
/// <c>TapGestureRecognizer</c>, and their XAML forms) must count it, with <c>UiUsage.</c> or <c>UiControls.</c> within a few lines, or say why not with
/// <c>// no-tap-count: &lt;reason&gt;</c>. Files that still hold older unwired presses are in SilentButtonBaseline.txt with their count; the count may only go down.
/// Like FallbackGuardTests: regenerate the baseline with SILENT_BUTTON_WRITE_BASELINE=1 only after lowering counts, never to hide a new button.
/// </summary>
public sealed class SilentButtonGuardTests
{
    private const string BaselineRelativePath = "tests/DinoReader.Tests/SilentButtonBaseline.txt";
    private const string WriteBaselineVariable = "SILENT_BUTTON_WRITE_BASELINE";
    private const int LinesBefore = 3;
    private const int LinesAfter = 8;
    private static readonly string[] SkippedFolders = ["obj", "bin", "Testing", "Platforms"];
    private static readonly string[] HelperFiles = ["UiUsage.cs", "Taps.cs"];
    private static readonly Regex CodePress = new(@"\.(Clicked|Toggled)\s*\+=|Taps\.AddTap\(|new\s+TapGestureRecognizer", RegexOptions.Compiled);
    private static readonly Regex XamlPress = new(@"<TapGestureRecognizer|\sClicked=""", RegexOptions.Compiled);
    // A handler counts itself (UiUsage.Tap / Screen / ScreenOf / Setting inside it) or is wrapped (UiUsage.Press); Taps.AddTap also counts through its UiControls argument. The old form, a UiControls name registered apart from
    // the handler (UiUsage.Counted after a Clicked +=), is no longer accepted: the count and its handler are one call, so the count is read before the handler runs.
    private static readonly Regex Counted = new(@"UiUsage\.(Press|Tap|Screen|ScreenOf|Setting)\(|no-tap-count:\s*\S", RegexOptions.Compiled);
    private static readonly Regex AddTapControl = new(@"UiControls\.", RegexOptions.Compiled);
    private static readonly Regex AddTapCall = new(@"Taps\.AddTap\(", RegexOptions.Compiled);

    [Fact]
    public void EveryPressInTheApp_IsCountedIsMarkedOrIsWithinItsBaseline()
    {
        string root = StarterBuilder.RepoRoot();
        Dictionary<string, int> current = UnmarkedByFile(root);
        if (Environment.GetEnvironmentVariable(WriteBaselineVariable) == "1")
        {
            File.WriteAllLines(Path.Combine(root, BaselineRelativePath), current.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key} {p.Value}"));
        }

        Dictionary<string, int> baseline = ReadBaseline(root);
        List<string> problems = [];
        foreach ((string file, int count) in current)
        {
            int allowed = baseline.GetValueOrDefault(file);
            if (count > allowed)
            {
                problems.Add($"{file}: {count} presses with no UiUsage call or `// no-tap-count: <reason>` (baseline {allowed}). Wire the control (UiUsage.Press(button, UiControls.X, handler) / Taps.AddTap(view, handler, UiControls.X) / UiUsage.Tap inside the handler).");
            }
        }

        foreach ((string file, int allowed) in baseline)
        {
            if (current.GetValueOrDefault(file) < allowed)
            {
                problems.Add($"{file}: only {current.GetValueOrDefault(file)} unwired presses now, baseline says {allowed}. Lower the baseline (the count may only go down).");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void TheScannerReallyReadsTheApp_AndFindsManyPresses()
    {
        string app = Path.Combine(StarterBuilder.RepoRoot(), "src", "DinoReader.App");
        int presses = Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories).Where(IsScanned).Sum(file => File.ReadAllLines(file).Count(line => CodePress.IsMatch(line)));

        Assert.True(presses > 50, $"only {presses} presses found: the scan is not looking at the app");
    }

    [Fact]
    public void ACountedPress_Passes()
    {
        Assert.Empty(UnmarkedLines("button.Clicked += (_, _) => Go(); // no-tap-count: Go counts screen:next"));
        Assert.Empty(UnmarkedLines("button.Clicked += (_, _) =>\n{\n    UiUsage.Tap(UiControls.Next);\n    Go();\n};"));
        Assert.Empty(UnmarkedLines("Taps.AddTap(card, () => Task.CompletedTask, UiControls.Pick);"));
        Assert.Empty(UnmarkedLines("var tap = new TapGestureRecognizer();\ntap.Tapped += (_, _) =>\n{\n    UiUsage.Tap(UiControls.Back);\n    Go();\n};"));
    }

    [Fact]
    public void ACountRegisteredApartFromItsHandler_Fails()
    {
        Assert.Single(UnmarkedLines("button.Clicked += (_, _) => Go();\nUiUsage.Counted(button, UiControls.Next);"));
        Assert.Single(UnmarkedLines("button.Clicked += (_, _) => Go();\nvar control = UiControls.Next;"));
    }

    [Fact]
    public void AMarkedPress_Passes()
    {
        Assert.Empty(UnmarkedLines("Taps.AddTap(sheet, () => Task.CompletedTask); // no-tap-count: swallows the tap"));
    }

    [Fact]
    public void AMarkerWithNoReason_Fails()
    {
        Assert.Single(UnmarkedLines("Taps.AddTap(sheet, () => Task.CompletedTask); // no-tap-count:"));
    }

    [Theory]
    [InlineData("button.Clicked += (_, _) => Go();")]
    [InlineData("toggle.Toggled += (_, _) => Save();")]
    [InlineData("Taps.AddTap(row, () => Open());")]
    [InlineData("var tap = new TapGestureRecognizer();\ntap.Tapped += (_, _) => Go();")]
    public void ASilentPress_Fails(string source)
    {
        Assert.Single(UnmarkedLines(source));
    }

    [Fact]
    public void ACountFarAwayDoesNotCoverAPress()
    {
        string source = "UiUsage.Tap(UiControls.Play);\n" + string.Join('\n', Enumerable.Repeat("var x = 1;", 12)) + "\nbutton.Clicked += (_, _) => Go();";

        Assert.Single(UnmarkedLines(source));
    }

    [Fact]
    public void AXamlPress_NeedsItsHandlerToCount_OrAMarkerBesideIt()
    {
        string xaml = "<Button Text=\"Go\" Clicked=\"OnGoClicked\" />";

        Assert.Single(UnmarkedXaml(xaml, "private void OnGoClicked(object? sender, EventArgs e)\n{\n    Go();\n}"));
        Assert.Empty(UnmarkedXaml(xaml, "private void OnGoClicked(object? sender, EventArgs e)\n{\n    UiUsage.Tap(UiControls.Save);\n    Go();\n}"));
        Assert.Empty(UnmarkedXaml("<!-- no-tap-count: developer switch -->\n" + xaml, "private void OnGoClicked(object? sender, EventArgs e) => Go();"));
    }

    private static Dictionary<string, int> UnmarkedByFile(string root)
    {
        string app = Path.Combine(root, "src", "DinoReader.App");
        Dictionary<string, int> result = [];
        foreach (string file in Directory.EnumerateFiles(app, "*.*", SearchOption.AllDirectories).Where(IsScanned))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            string text = File.ReadAllText(file);
            int count = file.EndsWith(".xaml", StringComparison.Ordinal) ? UnmarkedXaml(text, CodeBehindOf(file)).Count : UnmarkedLines(text).Count;
            if (count > 0)
            {
                result[relative] = count;
            }
        }

        return result;
    }

    private static bool IsScanned(string file)
    {
        string name = Path.GetFileName(file);
        bool wanted = name.EndsWith(".cs", StringComparison.Ordinal) || name.EndsWith(".xaml", StringComparison.Ordinal);
        string[] parts = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return wanted && !HelperFiles.Contains(name) && !name.EndsWith(".g.cs", StringComparison.Ordinal) && !parts.Any(part => SkippedFolders.Contains(part));
    }

    private static string CodeBehindOf(string xamlPath) => File.Exists(xamlPath + ".cs") ? File.ReadAllText(xamlPath + ".cs") : string.Empty;

    // The 1-based line of each press in C# source with no count in the lines around it.
    private static List<int> UnmarkedLines(string source)
    {
        string[] lines = source.Split('\n');
        List<int> result = [];
        for (int i = 0; i < lines.Length; i++)
        {
            if (CodePress.IsMatch(lines[i]) && !CountedAround(lines, i))
            {
                result.Add(i + 1);
            }
        }

        return result;
    }

    // A XAML press is counted when its handler (named in the attribute) calls UiUsage in the code-behind, or a marker sits within a few lines of it.
    private static List<int> UnmarkedXaml(string xaml, string codeBehind)
    {
        string[] lines = xaml.Split('\n');
        string[] code = codeBehind.Split('\n');
        List<int> result = [];
        for (int i = 0; i < lines.Length; i++)
        {
            if (!XamlPress.IsMatch(lines[i]))
            {
                continue;
            }

            bool marked = lines.Skip(Math.Max(0, i - 4)).Take(6).Any(line => Regex.IsMatch(line, @"no-tap-count:\s*\S"));
            if (!marked && !HandlerCounts(lines[i], code))
            {
                result.Add(i + 1);
            }
        }

        return result;
    }

    private static bool HandlerCounts(string xamlLine, string[] code)
    {
        Match handler = Regex.Match(xamlLine, @"(Clicked|Tapped)=""(?<name>\w+)""");
        if (!handler.Success)
        {
            return false;
        }

        int start = Array.FindIndex(code, line => Regex.IsMatch(line, @"\b" + handler.Groups["name"].Value + @"\s*\("));
        return start >= 0 && code.Skip(start).Take(LinesAfter + 4).Any(line => Counted.IsMatch(line));
    }

    private static bool CountedAround(string[] lines, int index)
    {
        string[] around = lines.Skip(Math.Max(0, index - LinesBefore)).Take(LinesBefore + LinesAfter + 1).ToArray();
        bool addTapWithControl = AddTapCall.IsMatch(lines[index]) && around.Any(line => AddTapControl.IsMatch(line));
        return addTapWithControl || around.Any(line => Counted.IsMatch(line));
    }

    private static Dictionary<string, int> ReadBaseline(string root)
    {
        string path = Path.Combine(root, BaselineRelativePath);
        if (!File.Exists(path))
        {
            return [];
        }

        return File.ReadAllLines(path)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split(' '))
            .ToDictionary(parts => parts[0], parts => int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }
}
