using System.Text.RegularExpressions;

namespace DinoReader.Tests;

/// <summary>
/// "AutomationId may only be set one time" (MAUI throws on the second assignment) crashed Settings on a device in Slice 232 part D (HostIn set it on a view it was handed, and ran twice), and one wave
/// later Slice 232E repeated it. A statement-form `x.AutomationId = ...` is allowed only in a constructor, or on a local the same method created. Anything else (a field, `this`, a parameter) can run
/// twice on one view, so it fails here. An object initializer (`new Grid { AutomationId = ... }`) always sets it once and is not scanned. (Slice 253 item 4: docs/plan/slices/253-final-loop-leftovers-night-2026-11-01-2355.md)
/// </summary>
public partial class AutomationIdAssignmentTests
{
    /// <summary>Methods that take a freshly built view and name it; every caller passes a view it just created. "Type.Method:parameter".</summary>
    private static readonly string[] FreshParameterAllowList = ["BookSheet.QuietRow:row"];

    [Fact]
    public void No_app_source_sets_an_existing_views_AutomationId_outside_a_constructor()
    {
        List<string> found = [];
        string root = Path.Combine(RepoRoot(), "src", "DinoReader.App");
        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Where(IsSource))
        {
            found.AddRange(FindRepeatableAssignments(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file)));
        }

        Assert.Empty(found);
    }

    [Fact]
    public void The_scan_fails_on_a_copy_of_the_232E_bug()
    {
        const string bug = """
            public sealed class SettingsPane
            {
            	private Label _title = new();

            	private void HostIn(Grid pane)
            	{
            		pane.AutomationId = "SettingsPane";
            	}

            	private void Refresh()
            	{
            		_title.AutomationId = "Title";
            		this.AutomationId = "Again";
            	}
            }
            """;

        List<string> found = FindRepeatableAssignments(bug, "SettingsPane");

        Assert.Equal(3, found.Count);
        Assert.Contains(found, f => f.Contains("pane.AutomationId") && f.Contains("HostIn"));
        Assert.Contains(found, f => f.Contains("_title.AutomationId") && f.Contains("Refresh"));
    }

    [Fact]
    public void The_scan_passes_constructors_locals_and_object_initializers()
    {
        const string fine = """
            public sealed class Row
            {
            	private readonly Border _hit = new();

            	public Row(string id)
            	{
            		_hit.AutomationId = id;
            	}

            	private Border Build()
            	{
            		var row = new Border();
            		row.AutomationId = "RowId";
            		Label label = new();
            		label.AutomationId = "LabelId";
            		return new Border { AutomationId = "Other", Content = row };
            	}
            }
            """;

        Assert.Empty(FindRepeatableAssignments(fine, "Row"));
    }

    private static List<string> FindRepeatableAssignments(string source, string typeName)
    {
        string[] lines = source.Replace("\r\n", "\n").Split('\n');
        List<string> found = [];
        for (int i = 0; i < lines.Length; i++)
        {
            Match assign = StatementAssignment().Match(lines[i]);
            if (!assign.Success)
            {
                continue;
            }

            (string method, bool isConstructor, int start) = EnclosingMember(lines, i);
            string target = assign.Groups["target"].Value;
            string rootName = target.Split('.')[0];
            bool local = start >= 0 && IsDeclaredLocally(string.Join('\n', lines[start..i]), rootName);
            bool allowed = FreshParameterAllowList.Contains($"{typeName}.{method}:{rootName}");
            if (!isConstructor && !local && !allowed)
            {
                found.Add($"{typeName}.{method} line {i + 1}: {target}.AutomationId = ... sets the id on a view this method did not create; it throws when the method runs twice. Set it in the constructor or on the new view.");
            }
        }

        return found;
    }

    /// <summary>The member whose body holds line <paramref name="index"/>: the nearest earlier line at the member indent (one tab) that looks like a method or constructor signature.</summary>
    private static (string Name, bool IsConstructor, int StartLine) EnclosingMember(string[] lines, int index)
    {
        for (int i = index; i >= 0; i--)
        {
            Match member = MemberSignature().Match(lines[i]);
            if (member.Success)
            {
                bool constructor = !member.Groups["returns"].Success;
                return (member.Groups["name"].Value, constructor, i);
            }
        }

        return ("(none)", false, -1);
    }

    private static bool IsDeclaredLocally(string methodSoFar, string name) =>
        Regex.IsMatch(methodSoFar, @"(?:\bvar|\b[A-Z]\w*(?:<[^>\n]+>)?\??)\s+" + Regex.Escape(name) + @"\s*(?:=|\bin\b)");

    private static bool IsSource(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}");

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src", "DinoReader.App")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new DirectoryNotFoundException("Repo root not found.");
    }

    [GeneratedRegex(@"^\s*(?<target>(?:this|[_A-Za-z]\w*)(?:\.\w+)*?)\.AutomationId\s*=(?!=)")]
    private static partial Regex StatementAssignment();

    /// <summary>A member line at one tab (or four spaces): optional modifiers, an optional return type, a name and "(". A line with "returns" is a method; without it, a constructor.</summary>
    [GeneratedRegex(@"^(?:\t| {4})(?:(?:public|private|internal|protected|static|override|async|sealed|virtual|partial)\s+)*(?:(?<returns>[\w<>\[\]?,.() ]+?)\s+)?(?<name>[A-Z_]\w*)\s*\([^;]*$")]
    private static partial Regex MemberSignature();
}
