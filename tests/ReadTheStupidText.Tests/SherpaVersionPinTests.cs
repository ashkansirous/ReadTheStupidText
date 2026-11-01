using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ReadTheStupidText.Tests;

/// <summary>
/// Slice 245: sherpa-onnx and onnxruntime are one version on Android, iOS and Windows, written once in
/// src/SherpaOnnxVersion.props. The repo-wide pin check is .github/scripts/check-sherpa-pins.mjs (node);
/// this runs it inside the test suite so the Windows, Android and iOS gates fail on a stray pin too.
/// </summary>
public class SherpaVersionPinTests
{
    private const string PropsPath = "src/SherpaOnnxVersion.props";
    private const string CheckScriptPath = ".github/scripts/check-sherpa-pins.mjs";
    private const int CheckTimeoutMilliseconds = 60_000;
    private const int PendingReadDrainMilliseconds = 5_000;

    [Fact]
    public void VersionProps_DefinesBothVersions()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), PropsPath));

        Assert.Matches(@"<SherpaOnnxVersion>\d+\.\d+\.\d+</SherpaOnnxVersion>", text);
        Assert.Matches(@"<OnnxRuntimeVersion>\d+\.\d+\.\d+</OnnxRuntimeVersion>", text);
    }

    [Fact]
    public void EveryPinInTheRepo_EqualsTheVersionProps()
    {
        var (exitCode, output) = RunPinCheck(RepoRoot());

        Assert.True(exitCode == 0, output);
    }

    [Fact]
    public void AndroidX64Tarball_IsPinnedToTheSherpaVersion()
    {
        var root = RepoRoot();
        var version = Regex.Match(File.ReadAllText(Path.Combine(root, PropsPath)), @"<SherpaOnnxVersion>([^<]+)<").Groups[1].Value;
        var android = File.ReadAllText(Path.Combine(root, "src", "AndroidSherpaPackaging.props"));

        Assert.Contains($"<SherpaX64PinnedVersion>{version}</SherpaX64PinnedVersion>", android);
        Assert.Matches(@"<SherpaX64Sha256>[0-9A-Fa-f]{64}</SherpaX64Sha256>", android);
    }

    private static (int ExitCode, string Output) RunPinCheck(string root)
    {
        var start = new ProcessStartInfo("node", CheckScriptPath)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("node did not start");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(CheckTimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            Task.WaitAll([output, error], PendingReadDrainMilliseconds);
            throw new TimeoutException("check-sherpa-pins.mjs did not finish");
        }

        return (process.ExitCode, output.Result + error.Result);
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
