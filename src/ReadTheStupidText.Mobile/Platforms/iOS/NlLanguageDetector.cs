using Foundation;
using NaturalLanguage;
using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Identifies a text's language with Apple's Natural Language framework
/// (<see cref="NLLanguageRecognizer"/>, Slice 55): on the device, no network, available on every iOS
/// version the app supports. It is the iOS counterpart of Android's ML Kit language identification
/// and only becomes visible once an <c>ITranslator</c> is registered too; see the Slice 55 decisions
/// for why none is on iOS yet.
/// </summary>
public sealed class NlLanguageDetector : ILanguageDetector
{
    // A language hypothesis list this long is plenty to find the best guess.
    private const int MaxHypotheses = 3;

    public Task<DetectedLanguage?> DetectAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => Identify(text, cancellationToken), cancellationToken);
    }

    private static DetectedLanguage? Identify(string text, CancellationToken cancellationToken)
    {
        using var recognizer = new NLLanguageRecognizer();
        recognizer.Process(text);
        NSDictionary<NSString, NSNumber> hypotheses = recognizer.GetNativeLanguageHypotheses(MaxHypotheses);
        cancellationToken.ThrowIfCancellationRequested();
        return BestGuess(hypotheses);
    }

    private static DetectedLanguage? BestGuess(NSDictionary<NSString, NSNumber> hypotheses)
    {
        DetectedLanguage? best = null;
        foreach (NSString tag in hypotheses.Keys)
        {
            float confidence = hypotheses[tag]?.FloatValue ?? 0;
            if (best is null || confidence > best.Confidence)
            {
                best = new DetectedLanguage(tag.ToString(), confidence);
            }
        }

        return best;
    }
}
