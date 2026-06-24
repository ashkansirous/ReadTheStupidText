namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// The file names inside the Supertonic-3 model bundle. Shared by the downloader
/// (to verify presence) and the reader (to construct the engine), so the layout
/// is defined in exactly one place.
/// </summary>
internal static class SupertonicFiles
{
    public const string DurationPredictor = "duration_predictor.int8.onnx";
    public const string TextEncoder = "text_encoder.int8.onnx";
    public const string VectorEstimator = "vector_estimator.int8.onnx";
    public const string Vocoder = "vocoder.int8.onnx";
    public const string TtsJson = "tts.json";
    public const string UnicodeIndexer = "unicode_indexer.bin";
    public const string VoiceStyle = "voice.bin";
}
