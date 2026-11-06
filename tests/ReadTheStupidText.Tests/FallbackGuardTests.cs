using System.Text;

namespace ReadTheStupidText.Tests;

/// <summary>
/// Slice 273 part E: a ratchet over every <c>catch</c> in src/**/*.cs. A catch must report a fallback, carry
/// <c>// no-fallback: &lt;reason&gt;</c> on the first line of its block, or only rethrow. Files that still hold
/// older unmarked catches are listed in FallbackGuardBaseline.txt with their count; the count may only go down.
/// Regenerate the baseline with FALLBACK_GUARD_WRITE_BASELINE=1 (only after lowering counts, never to hide a new catch).
/// </summary>
public class FallbackGuardTests
{
    private const string BaselineRelativePath = "tests/ReadTheStupidText.Tests/FallbackGuardBaseline.txt";
    private const string WriteBaselineVariable = "FALLBACK_GUARD_WRITE_BASELINE";
    private static readonly string[] SkippedDirectories = ["obj", "bin", "node_modules"];
    private static readonly string[] GeneratedSuffixes = [".g.cs", ".g.i.cs", ".designer.cs", ".generated.cs"];

    [Fact]
    public void EveryCatchInSrc_ReportsAFallbackIsMarkedOrIsWithinItsBaseline()
    {
        var root = RepoRoot();
        var current = UnmarkedByFile(root);
        if (Environment.GetEnvironmentVariable(WriteBaselineVariable) == "1")
        {
            WriteBaseline(root, current);
        }

        var baseline = ReadBaseline(root);
        var problems = Compare(root, current, baseline);

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Marker_WithReason_Passes()
    {
        Assert.Equal(0, Unmarked("class C { void M() { try { } catch (Exception) {\n // no-fallback: user cancelled\n } } }"));
    }

    [Fact]
    public void MissingMarker_Fails()
    {
        Assert.Equal(1, Unmarked("class C { void M() { try { } catch { } } }"));
    }

    [Fact]
    public void MarkerWithEmptyReason_Fails()
    {
        Assert.Equal(1, Unmarked("class C { void M() { try { } catch {\n // no-fallback:   \n } } }"));
    }

    [Fact]
    public void MarkerNotFirstInBlock_Fails()
    {
        Assert.Equal(1, Unmarked("class C { void M() { try { } catch {\n var x = 1; // no-fallback: late\n } } }"));
    }

    [Fact]
    public void RethrowAsLastStatement_Passes()
    {
        Assert.Equal(0, Unmarked("class C { void M() { try { } catch (Exception e) { Log(e); throw; } } }"));
        Assert.Equal(0, Unmarked("class C { void M() { try { } catch (Exception e) { throw new InvalidOperationException(\"x\", e); } } }"));
    }

    [Fact]
    public void ThrowThatIsNotLast_Fails()
    {
        Assert.Equal(1, Unmarked("class C { void M() { try { } catch (Exception e) { if (a) { throw; } return; } } }"));
    }

    [Theory]
    [InlineData("sink.Report(FallbackArea.Pack, FallbackReason.PackFailed);")]
    [InlineData("usage.RecordFallback(story, area, reason);")]
    [InlineData("usage.RecordGeneralFallback(area, reason);")]
    [InlineData("DinoFallbacks.Report(FallbackArea.Config, FallbackReason.IntroStateReset);")]
    [InlineData("ReportGeneralFallback(area, reason);")]
    [InlineData("ReportFontFallbackOnce();")]
    public void FallbackReportCall_Passes(string call)
    {
        Assert.Equal(0, Unmarked("class C { void M() { try { } catch (Exception) { " + call + " return; } } }"));
    }

    [Fact]
    public void BracesInStringsCharsAndComments_AreIgnored()
    {
        const string source = "class C { void M() { try { } catch (Exception) {\n // no-fallback: ok } {\n var s = \"}\"; var c = '}'; /* } */ var v = @\"}\"\"}\"; var i = $\"{{ {x} }}\"; } M2(); } }";
        var findings = FallbackGuardScanner.Scan(source);

        Assert.Single(findings);
        Assert.True(findings[0].Passes);
    }

    [Fact]
    public void CatchWordInsideStringOrComment_IsNotACatch()
    {
        Assert.Empty(FallbackGuardScanner.Scan("class C { string s = \"catch { }\"; // catch { }\n /* catch { } */ }"));
    }

    [Fact]
    public void NestedBlocksInCatch_AreOneCatchBlock()
    {
        const string source = "class C { void M() { try { } catch (Exception) {\n // no-fallback: nested\n if (a) { b(); } else { c(); } } } }";

        Assert.Equal(0, Unmarked(source));
    }

    [Fact]
    public void CatchWithWhenFilter_IsScanned()
    {
        Assert.Equal(1, Unmarked("class C { void M() { try { } catch (IOException e) when (e.HResult == 5) { } } }"));
        Assert.Equal(0, Unmarked("class C { void M() { try { } catch (IOException e) when (e.HResult == 5) {\n // no-fallback: locked file\n } } }"));
        Assert.Equal(1, Unmarked("class C { void M() { try { } catch when (x) { } } }"));
    }

    [Fact]
    public void ReportedFindingLine_IsTheCatchLine()
    {
        var findings = FallbackGuardScanner.Scan("class C {\nvoid M() {\ntry { }\ncatch { }\n}\n}");

        Assert.Equal(4, findings.Single().Line);
    }

    [Theory]
    [InlineData("progress.Report(0.5);")]
    [InlineData("voice = GetFallbackVoice();")]
    [InlineData("var l = ForceFallback(1);")]
    [InlineData("kitchenSink.Report(a, b);")]
    [InlineData("_fallbacks.Report(a, b);")]
    [InlineData("ReportFallbacks(list);")]
    [InlineData("ReportScannerFallbacks(services);")]
    public void NonSinkCallsWithLooseNames_DoNotPass(string call)
    {
        Assert.Equal(1, Unmarked("class C { void M() { try { } catch (Exception e) { " + call + " } } }"));
    }

    [Theory]
    [InlineData("_fallbacks.Report(FallbackArea.Pack, reason);")]
    [InlineData("_fallbacks?.Report(FallbackArea.Pack, reason);")]
    [InlineData("store.Fallbacks.Report(FallbackArea.Pack, reason);")]
    [InlineData("sink.Report(\n FallbackArea.Pack, reason);")]
    [InlineData("ReportRecordingUnplayable();")]
    [InlineData("_session.Services.GetService<IFallbackSink>()?.Report(FallbackArea.Pack, reason);")]
    [InlineData("Services.GetRequiredService<IFallbackSink>().Report(FallbackArea.Pack, reason);")]
    [InlineData("Report(bookId, language, AudioPackUsage.PackFailed(kind));")]
    public void SinkReceiversAndNamedHelpers_Pass(string call)
    {
        Assert.Equal(0, Unmarked("class C { void M() { try { } catch (Exception e) { " + call + " } } }"));
    }

    [Fact]
    public void ReportInsideNestedCatchOrLambda_DoesNotCount()
    {
        Assert.Equal(1, Unmarked("class C { void M() { try { } catch (Exception e) { try { } catch (Exception) { _fallbacks.Report(FallbackArea.X, b); } } } }"));
        Assert.Equal(1, Unmarked("class C { void M() { try { } catch (Exception e) { var f = () => _fallbacks.Report(FallbackArea.X, b); } } }"));
        Assert.Equal(1, Unmarked("class C { void M() { try { } catch (Exception e) { Run(() => { _fallbacks.Report(FallbackArea.X, b); }); } } }"));
        Assert.Equal(0, Unmarked("class C { void M() { try { } catch (Exception e) { if (x) { _fallbacks.Report(FallbackArea.X, b); } } } }"));
    }

    [Fact]
    public void ReturnBeforeFinalThrow_IsASwallowingPath()
    {
        Assert.Equal(1, Unmarked("class C { void M() { try { } catch (Exception e) { if (a) return; throw; } } }"));
    }

    [Fact]
    public void ConditionalThrow_IsNotCountedAsARethrow()
    {
        Assert.Equal(1, Unmarked("class C { void M() { try { } catch (Exception e) { if (fatal) { throw; } } } }"));
    }

    [Fact]
    public void EmptyStatementCatch_IsReportedNotCrashed()
    {
        Assert.Equal(1, Unmarked("class C { void M() { try { } catch { ; } } }"));
    }

    [Theory]
    [InlineData("string r = @\"\"\"[^\"\"]*\"\"|y\";")]
    [InlineData("string r = @\"\"\"\"\"\";")]
    [InlineData("string r = @\"\"\"x\"\"\";")]
    [InlineData("string r = \"\"\"\n  raw \" text { \n\"\"\";")]
    public void QuoteHeavyStrings_DoNotSwallowLaterCatches(string declaration)
    {
        Assert.Equal(1, Unmarked("class C { " + declaration + " void M() { try { } catch { } } }"));
    }

    [Theory]
    [InlineData("$\"{(c == '\"' ? 1 : 2)}\"")]
    [InlineData("$\"{(a ? @\"x\"\"y\" : \"z\")}\"")]
    [InlineData("$\"{(c == '}' ? 1 : 2)}\"")]
    [InlineData("$\"{$\"{x}\"}\"")]
    public void InterpolationHolesWithStringsAndChars_DoNotDriftTheMask(string literal)
    {
        Assert.Equal(1, Unmarked("class C { string s = " + literal + "; void M() { try { } catch { } } }"));
    }

    [Fact]
    public void CrlfSource_MarkerAndLinesWork()
    {
        const string source = "class C {\r\nvoid M() {\r\ntry { }\r\ncatch (Exception) {\r\n // no-fallback: ok\r\n}\r\ntry { }\r\ncatch { }\r\n}\r\n}";
        var findings = FallbackGuardScanner.Scan(source);

        Assert.True(findings[0].Passes);
        Assert.False(findings[1].Passes);
        Assert.Equal(8, findings[1].Line);
    }

    [Fact]
    public void PreprocessorBranchesOnBothSides_AreBothScanned()
    {
        Assert.Equal(2, Unmarked("class C { void M() {\n#if A\n try { } catch { }\n#else\n try { } catch { }\n#endif\n} }"));
    }

    private static int Unmarked(string source) => FallbackGuardScanner.UnmarkedCount(source);

    private static List<string> Compare(string root, Dictionary<string, List<int>> current, Dictionary<string, int> baseline)
    {
        var problems = new List<string>();
        foreach (var (file, lines) in current)
        {
            var allowed = baseline.GetValueOrDefault(file);
            if (lines.Count > allowed)
            {
                problems.Add($"{file}: {lines.Count} catch(es) with no fallback report and no marker, baseline is {allowed}. "
                    + $"Lines {string.Join(", ", lines)}. Report a fallback (Slice 273) or add '// no-fallback: <reason>' on the first line inside the catch block.");
            }
        }

        foreach (var (file, allowed) in baseline)
        {
            var count = current.TryGetValue(file, out var lines) ? lines.Count : 0;
            if (count < allowed)
            {
                problems.Add($"{file}: {count} unmarked catch(es), baseline says {allowed}. Lower only this file's number in {BaselineRelativePath} "
                    + $"to '{file} {count}' (remove the line when it is 0). Never regenerate the whole file: that would hide a new swallowed catch elsewhere.");
            }
        }

        return problems;
    }

    private static Dictionary<string, List<int>> UnmarkedByFile(string root)
    {
        var result = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (var path in SourceFiles(Path.Combine(root, "src")))
        {
            var lines = FallbackGuardScanner.Scan(File.ReadAllText(path)).Where(f => !f.Passes).Select(f => f.Line).ToList();
            if (lines.Count > 0)
            {
                result[Path.GetRelativePath(root, path).Replace('\\', '/')] = lines;
            }
        }

        return result;
    }

    private static IEnumerable<string> SourceFiles(string srcRoot)
    {
        var files = TrackedSourceFiles(srcRoot) ?? WalkedSourceFiles(srcRoot);
        return files
            .Where(p => !IsSkipped(srcRoot, p))
            .OrderBy(p => p, StringComparer.Ordinal);
    }

    private static IEnumerable<string> WalkedSourceFiles(string srcRoot)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true };
        return Directory.EnumerateFiles(srcRoot, "*.cs", options);
    }

    /// <summary>Git-tracked *.cs files under src (what CI sees); null when git is unavailable or fails.</summary>
    private static List<string>? TrackedSourceFiles(string srcRoot)
    {
        try
        {
            var repoRoot = Path.GetDirectoryName(srcRoot)!;
            var info = new System.Diagnostics.ProcessStartInfo("git", ["-C", repoRoot, "ls-files", "-z", "--", "src"])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = System.Diagnostics.Process.Start(info)!;
            var output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 || output.Length == 0)
            {
                return null;
            }

            return output.Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .Select(p => Path.Combine(repoRoot, p.Replace('/', Path.DirectorySeparatorChar)))
                .Where(File.Exists)
                .ToList();
        }
        catch (Exception)
        {
            // no-fallback: test helper; without git the guard walks the directory instead
            return null;
        }
    }

    private static bool IsSkipped(string srcRoot, string path)
    {
        var parts = Path.GetRelativePath(srcRoot, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Take(parts.Length - 1).Any(p => SkippedDirectories.Contains(p, StringComparer.OrdinalIgnoreCase))
            || GeneratedSuffixes.Any(s => path.EndsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, int> ReadBaseline(string root)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(Path.Combine(root, BaselineRelativePath)))
        {
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith('#'))
            {
                continue;
            }

            var space = text.LastIndexOf(' ');
            result[text[..space]] = int.Parse(text[(space + 1)..]);
        }

        return result;
    }

    private static void WriteBaseline(string root, Dictionary<string, List<int>> current)
    {
        var sb = new StringBuilder();
        sb.Append("# Slice 273 part E: unmarked catches per file at the time of the guard. The count may only go down.\n");
        sb.Append("# Format: <repo-relative path> <count>. Regenerate with FALLBACK_GUARD_WRITE_BASELINE=1.\n");
        foreach (var (file, lines) in current.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            sb.Append(file).Append(' ').Append(lines.Count).Append('\n');
        }

        File.WriteAllText(Path.Combine(root, BaselineRelativePath), sb.ToString());
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReadTheStupidText.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo root (ReadTheStupidText.slnx) not found.");
    }
}
