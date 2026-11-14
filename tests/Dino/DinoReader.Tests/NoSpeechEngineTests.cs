using System.Text.RegularExpressions;
using DinoReader.BookBuilder;
using DinoReader.Core;
using Sirous.Reading.Application.Reading;
using Xunit;

namespace DinoReader.Tests;

/// <summary>
/// Slice 278 part B: no phone voice ever reads anything in Dino Reader. A piece with no clip and no recording is shown for a beat and the read goes on, and no code path in the app
/// resolves a speech engine, a voice pack service, a voice model service or the voice pack installer. The first half tests the pure plan; the second scans the app's sources
/// (the reading session needs the MAUI main thread, so it cannot run here).
/// </summary>
public partial class NoSpeechEngineTests
{
    private static VoicedSegment Piece(string text, int start) => new(text, string.Empty, start, start + text.Length, start);

    private static VoicedSegment WithClip(string text, int start) => PremadeClipMarker.Mark(Piece(text, start), "clip-" + start + ".m4a");

    private static VoicedSegment Slot(AnimalKind kind, int at) => new("Oink!", string.Empty, at, at, at, new AnimalClipMarker(kind));

    [Fact]
    public void APieceWithNoClip_IsOneSilentStep_NeverASpokenRun()
    {
        var steps = PlaybackSteps.Plan([Piece("One.", 0), Piece("Two.", 5), Piece("Three.", 10)]);

        Assert.Equal(3, steps.Count);
        Assert.All(steps, step => Assert.True(PlaybackSteps.IsSilentPiece(step)));
        Assert.All(steps, step => Assert.Single(step.Run));
        Assert.Equal(["One.", "Two.", "Three."], steps.Select(s => s.Run[0].Text));
    }

    [Fact]
    public void ClipsAnimalSlotsAndSilentPieces_KeepTheirOrder()
    {
        var steps = PlaybackSteps.Plan([WithClip("Clip.", 0), Piece("Bare.", 6), Slot(AnimalKind.Pig, 12), WithClip("Last.", 13)]);

        Assert.Equal(4, steps.Count);
        Assert.NotNull(steps[0].Premade);
        Assert.True(PlaybackSteps.IsSilentPiece(steps[1]));
        Assert.Equal(AnimalKind.Pig, steps[2].Animal);
        Assert.NotNull(steps[3].Premade);
    }

    [Fact]
    public void APieceWithNoLetterOrDigit_HasNothingToSayOrShow_SoItIsDropped()
    {
        var steps = PlaybackSteps.Plan([Piece("—", 0), Piece("Real words.", 2), Piece("...", 14)]);

        Assert.Single(steps);
        Assert.Equal("Real words.", steps[0].Run[0].Text);
    }

    [Fact]
    public void ARead_WithNothingToPlay_IsAnEmptyPlan_NotOneEmptySpeakStep()
    {
        Assert.Empty(PlaybackSteps.Plan([]));
        Assert.Empty(PlaybackSteps.Plan([Piece("!", 0)]));
    }

    [Fact]
    public void AClipThatCannotPlay_BecomesASilentPiece_NotAVoice()
    {
        var steps = PlaybackSteps.Plan([WithClip("Clip.", 0)]);

        var silent = PlaybackSteps.AsSilentPiece(steps[0]);

        Assert.True(PlaybackSteps.IsSilentPiece(silent));
        Assert.Null(silent.Premade);
        Assert.Equal("Clip.", silent.Run[0].Text);
    }

    [Fact]
    public void TheBeat_IsShortAndFixed()
    {
        Assert.InRange(PlaybackSteps.BeatMilliseconds, 500, 3000);
    }

    // The words that name a speech engine or a voice download. "SpokenLanguage" is also the translator's method for the language a read is played in, so only the old class (used as a type) counts.
    private static readonly string[] Forbidden =
    [
        "ISpeechReader", "IVoicePackService", "IVoiceModelService", "VoicePackInstaller", "CompositeSpeechReader", "IReadScoped", "IReadingModelResolver",
        "StorytellerDefault", "NarratorChoice", "SpeechStartup", "StoryLanguagePacks", "VoicePreview", "SpeakSegmentsAsync", "PackNotificationAsk",
    ];

    [GeneratedRegex(@"\bSpokenLanguage\b(?!\()(?<!\.SpokenLanguage)")]
    private static partial Regex OldSpokenLanguageClass();

    private static IEnumerable<string> AppSources()
    {
        string app = Path.Combine(StarterBuilder.RepoRoot(), "src", "Dino", "DinoReader.App");
        return Directory.GetFiles(app, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void NoCodeInTheApp_ResolvesASpeechEngineOrAVoicePack()
    {
        var found = new List<string>();
        foreach (string file in AppSources())
        {
            string text = File.ReadAllText(file);
            foreach (string word in Forbidden.Where(w => Regex.IsMatch(text, @"\b" + w + @"\b")))
            {
                found.Add($"{Path.GetFileName(file)}: {word}");
            }

            if (OldSpokenLanguageClass().IsMatch(text.Replace("StoryTranslator.SpokenLanguage", string.Empty)))
            {
                found.Add($"{Path.GetFileName(file)}: SpokenLanguage");
            }
        }

        Assert.Empty(found);
    }

    [Fact]
    public void TheReadingSession_TakesNoSpeechReader_AndReportsOneFallbackForAPieceItDoesNotPlay()
    {
        string session = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "src", "Dino", "DinoReader.App", "ReadingSession.cs"));

        Assert.DoesNotContain("_reader", session);
        Assert.DoesNotContain("SpeakAsync", session);
        Assert.Contains("PlaybackSteps.Plan(segments)", session);
        Assert.Contains("FallbackReason.ClipMissing", session);
        Assert.Contains("read.Reported.Add(index)", session); // once per step, however often a pause starts it again
        Assert.Contains("PlaybackSteps.BeatMilliseconds", session);
    }

    [Fact]
    public void ALanguageChange_NeverReachesTheSharedVoiceInstaller_SoNoVoicePackDownloads()
    {
        string root = StarterBuilder.RepoRoot();
        string program = File.ReadAllText(Path.Combine(root, "src", "Dino", "DinoReader.App", "MauiProgram.cs"));
        string notifier = File.ReadAllText(Path.Combine(root, "src", "Dino", "DinoReader.App", "InertLanguageNotifier.cs"));

        // The shared VoicePackInstaller starts a download on ILanguageChangeNotifier.LanguageChanged (skip-first-run sets the language and the Debug hooks build the installer).
        // Dino registers its own notifier after the shared services (last registration wins) and it never raises the event.
        int shared = program.IndexOf("AddMobileSharedServices()", StringComparison.Ordinal);
        int inert = program.IndexOf("AddSingleton<ILanguageChangeNotifier, InertLanguageNotifier>", StringComparison.Ordinal);
        Assert.True(shared >= 0 && inert > shared);
        Assert.DoesNotContain("?.Invoke", notifier);
        Assert.DoesNotContain("EventHandler? _", notifier);
    }

    [Fact]
    public void TheDinoProject_NamesNoSherpaOrOnnxPackageItself()
    {
        string csproj = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "src", "Dino", "DinoReader.App", "DinoReader.App.csproj"));

        // The packaging imports stay (they neutralise what Mobile.Shared brings in, see the csproj comment); the project itself asks for no engine package.
        Assert.DoesNotContain("PackageReference Include=\"org.k2fsa", csproj);
        Assert.DoesNotContain("PackageReference Include=\"Microsoft.ML.OnnxRuntime", csproj);
    }
}
