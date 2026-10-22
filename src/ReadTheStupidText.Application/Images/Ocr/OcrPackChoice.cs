using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Application.Images;

/// <summary>
/// Chooses between the built-in recognizer and the installed non-Latin OCR packs for one scan
/// (Slice 84, Decision 91). The built-in reading is final only when it holds enough Latin letters
/// and they dominate; otherwise packs are offered one by one, and the pack with the most letters of
/// its own script wins, provided its letters beat everything the built-in recognizer read by a margin (so real short Latin text is never replaced by a hallucination). Once a
/// pack's own script clearly dominates its reading, the scan stops. Pure.
/// </summary>
public sealed class OcrPackChoice
{
    /// <summary>Fewer Latin letters than this is too little to call the page Latin (the recognizer may be reading noise).</summary>
    public const int MinLatinLetters = 12;

    /// <summary>A pack reading wins outright when this share of its letters is in its own script.</summary>
    public const double ClearWinShare = 0.8;

    /// <summary>A clear win also needs at least this many letters of the pack's own script.</summary>
    public const int MinClearWinLetters = 4;

    /// <summary>A Latin verdict is final only when at least this share of the letters is Latin.</summary>
    public const double LatinFinalShare = 0.85;

    /// <summary>A pack's own-script letters must exceed ALL the built-in letters by this many to win.</summary>
    public const int PackWinMargin = 2;

    private readonly ScriptShare _builtIn;
    private int _bestScore;

    public OcrPackChoice(ScriptShare builtIn) => _builtIn = builtIn;

    /// <summary>The pack whose reading is the best so far, or null.</summary>
    public OcrModelPaths? Winner { get; private set; }

    /// <summary>True once a pack has clearly won: the remaining packs need not run.</summary>
    public bool IsDecided { get; private set; }

    /// <summary>A built-in reading with enough letters, nearly all of them Latin, is the answer; no pack is consulted.</summary>
    public static bool BuiltInIsFinal(ScriptShare share) =>
        share.Latin >= MinLatinLetters && (double)share.Latin / share.Total >= LatinFinalShare;

    /// <summary>
    /// The installed non-Latin packs in the order to try them: the pack for the script the built-in
    /// reading mostly found first, then the one serving the hint language, then the rest. The pack
    /// whose engine is already loaded (<paramref name="loaded"/>) goes before all of them, so a scan
    /// does not reload models it holds.
    /// </summary>
    public static IReadOnlyList<OcrModelPaths> Candidates(
        IEnumerable<InstalledVoicePack> installed, string hint, ScriptShare builtIn, OcrModelPaths? loaded = null) =>
        InstalledOcrCatalog.NonLatinCandidates(installed, hint)
            .OrderByDescending(paths => paths.IsSamePack(loaded))
            .ThenByDescending(paths => ScriptDetector.ScriptOf(paths.Language) == builtIn.Dominant)
            .ToList();

    /// <summary>
    /// After a Latin-final verdict, the one installed pack that serves a non-Latin hint language, or
    /// null. It is read anyway and must still beat the built-in letters by <see cref="PackWinMargin"/>,
    /// which catches a non-Latin page the Latin recognizer filled with junk Latin letters.
    /// </summary>
    public static IReadOnlyList<OcrModelPaths> HintPackOnly(IEnumerable<InstalledVoicePack> installed, string hint) =>
        InstalledOcrCatalog.NonLatinCandidates(installed, hint)
            .Where(paths => string.Equals(paths.Language, hint, StringComparison.OrdinalIgnoreCase))
            .Take(1)
            .ToList();

    /// <summary>Offers one pack's reading; true when it is now the best one.</summary>
    public bool Offer(OcrModelPaths paths, ScriptShare reading)
    {
        TextScript script = ScriptDetector.ScriptOf(paths.Language);
        int score = reading.Count(script);
        if (score <= _bestScore || score <= _builtIn.Total + PackWinMargin)
        {
            return false;
        }

        _bestScore = score;
        Winner = paths;
        IsDecided = score >= MinClearWinLetters && (double)score / reading.Total >= ClearWinShare;
        return true;
    }
}
