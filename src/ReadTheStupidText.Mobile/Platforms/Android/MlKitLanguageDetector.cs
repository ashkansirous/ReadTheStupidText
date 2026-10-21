using Android.Runtime;
using Android.Gms.Extensions;
using ReadTheStupidText.Application.Translation;
using Xamarin.Google.MLKit.NL.LanguageId;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Identifies a text's language with ML Kit Language Identification (Slice 54). The model ships
/// inside the APK (the bundled artifact, not the Play-services one), so detection is on-device from
/// first launch with no network and no Play services.
/// </summary>
public sealed class MlKitLanguageDetector : ILanguageDetector
{
    // One client for the process, like the OCR recognizer: creating one loads the native model.
    private readonly Lazy<ILanguageIdentifier> _identifier = new(
        () => LanguageIdentification.GetClient(new LanguageIdentificationOptions.Builder().Build()),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public async Task<DetectedLanguage?> DetectAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Java.Lang.Object? result = await _identifier.Value.IdentifyPossibleLanguages(text);
        cancellationToken.ThrowIfCancellationRequested();
        IdentifiedLanguage? best = BestGuess(result);
        return best?.LanguageTag is { } tag ? new DetectedLanguage(tag, best.Confidence) : null;
    }

    private static IdentifiedLanguage? BestGuess(Java.Lang.Object? result) =>
        result?.JavaCast<Java.Util.IList>()?.ToEnumerable<IdentifiedLanguage>().MaxBy(g => g.Confidence);
}
