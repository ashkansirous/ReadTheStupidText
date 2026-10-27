using Foundation;
using NaturalLanguage;
using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Identifies a text's language with Apple's Natural Language framework
/// (<see cref="NLLanguageRecognizer"/>, Slice 55): on the device, no network, available on every iOS
/// version the app supports. It is the iOS counterpart of Android's ML Kit language identification
/// and is paired with the on-device ML Kit translator on iOS (Slice 160).
/// </summary>
public sealed class NlLanguageDetector : ILanguageDetector
{
    // Only the top hypothesis is needed, for the confidence of the dominant language.
    private const int MaxHypotheses = 1;

    public Task<DetectedLanguage?> DetectAsync(string text, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<DetectedLanguage?>(cancellationToken);
        }

        return Task.Run(() => Identify(text, cancellationToken), cancellationToken);
    }

    private static DetectedLanguage? Identify(string text, CancellationToken cancellationToken)
    {
        using var recognizer = new NLLanguageRecognizer();
        recognizer.Process(text);
        cancellationToken.ThrowIfCancellationRequested();
        NLLanguage dominant = recognizer.DominantLanguage;
        if (dominant is NLLanguage.Unevaluated or NLLanguage.Undetermined)
        {
            return null;
        }

        return new DetectedLanguage(PrimarySubtag(dominant.GetConstant().ToString()), ConfidenceOf(recognizer, dominant));
    }

    // "zh-Hans" -> "zh", "pt-BR" -> "pt": the same primary-subtag form Android reports.
    private static string PrimarySubtag(string tag)
    {
        int dash = tag.IndexOfAny(['-', '_']);
        return dash > 0 ? tag[..dash] : tag;
    }

    // The framework reports the dominant language without its probability; the top hypothesis is that language.
    private static float ConfidenceOf(NLLanguageRecognizer recognizer, NLLanguage dominant)
    {
        NSDictionary<NSString, NSNumber> hypotheses = recognizer.GetNativeLanguageHypotheses(MaxHypotheses);
        return hypotheses[dominant.GetConstant()]?.FloatValue ?? 0;
    }
}
