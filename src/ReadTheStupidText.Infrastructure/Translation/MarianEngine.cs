using Microsoft.ML.OnnxRuntime;

namespace ReadTheStupidText.Infrastructure.Translation;

/// <summary>
/// One loaded translation leg (Slice 75): the Marian encoder and decoder as onnxruntime sessions, the
/// tokenizer and the decoding spec of an installed pack folder. <see cref="Translate"/> encodes a single
/// sentence once, then decodes it greedily (the merged decoder with a key/value cache, so each step costs the same). Not thread-safe: <see cref="PackTranslator"/> serialises calls.
/// </summary>
public sealed class MarianEngine : IDisposable
{
    private const string EncoderInputIds = "input_ids";
    private const string EncoderMask = "attention_mask";
    private const string EncoderOutput = "last_hidden_state";
    private const string DecoderInputIds = "input_ids";
    private const string DecoderMask = "encoder_attention_mask";
    private const string DecoderHidden = "encoder_hidden_states";
    private const string DecoderOutput = "logits";

    private const int MinThreads = 1;
    private const int MaxThreads = 4;

    private readonly InferenceSession _encoder;
    private readonly InferenceSession _decoder;
    private readonly MarianTokenizer _tokenizer;
    private readonly TranslationModelSpec _spec;

    private MarianEngine(InferenceSession encoder, InferenceSession decoder, MarianTokenizer tokenizer, TranslationModelSpec spec)
    {
        _encoder = encoder;
        _decoder = decoder;
        _tokenizer = tokenizer;
        _spec = spec;
    }

    public TranslationModelSpec Spec => _spec;

    /// <summary>Loads the leg stored in <paramref name="packFolder"/> (see <see cref="TranslationPackFiles"/>).</summary>
    public static MarianEngine Load(string packFolder)
    {
        var spec = TranslationModelSpec.Parse(File.ReadAllText(Path.Combine(packFolder, TranslationPackFiles.Spec)));
        MarianTokenizer tokenizer = LoadTokenizer(packFolder);
        using var options = CreateSessionOptions();
        var encoder = new InferenceSession(Path.Combine(packFolder, TranslationPackFiles.Encoder), options);
        try
        {
            var decoder = new InferenceSession(Path.Combine(packFolder, TranslationPackFiles.Decoder), options);
            return new MarianEngine(encoder, decoder, tokenizer, spec);
        }
        catch
        {
            encoder.Dispose();
            throw;
        }
    }

    /// <summary>Translates one sentence (no more than a few hundred tokens).</summary>
    public string Translate(string sentence, CancellationToken cancellationToken)
    {
        int[] source = _tokenizer.Encode(sentence, _spec.TargetToken);
        if (source.Length > _spec.MaxLength)
        {
            source = [.. source.Take(_spec.MaxLength - 1), _tokenizer.EndId];
        }

        using var session = new OnnxDecoderSession(this, source);
        IReadOnlyList<int> generated = GreedyDecoder.Decode(session, _spec, GreedyDecoder.MaxNewTokensFor(source.Length), cancellationToken);
        return _tokenizer.Decode(generated);
    }

    public void Dispose()
    {
        _encoder.Dispose();
        _decoder.Dispose();
    }

    private static MarianTokenizer LoadTokenizer(string packFolder)
    {
        using FileStream vocab = File.OpenRead(Path.Combine(packFolder, TranslationPackFiles.Vocabulary));
        using FileStream model = File.OpenRead(Path.Combine(packFolder, TranslationPackFiles.SourceModel));
        return new MarianTokenizer(model, MarianTokenizer.ParseVocabulary(vocab));
    }

    // Fewer threads than the neural voice's: translation finishes, then the voice takes the cores.
    private static SessionOptions CreateSessionOptions() => new()
    {
        IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 2, MinThreads, MaxThreads),
        GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
    };

    // Decodes with the merged decoder and its key/value cache: step 0 (no cache) computes the encoder
    // cross-attention cache and the first decoder cache; later steps feed one token and the cache back,
    // so each step costs the same instead of growing with the sentence.
    private sealed class OnnxDecoderSession : IMarianDecoderSession, IDisposable
    {
        private const string PastPrefix = "past_key_values.";
        private const string PresentPrefix = "present.";
        private const string CacheBranch = "use_cache_branch";
        private const string EncoderCacheMarker = ".encoder.";

        private readonly MarianEngine _engine;
        private readonly long[] _mask;
        private readonly OrtValue _maskValue;
        private readonly OrtValue _hidden;
        private readonly string[] _pastNames;
        private readonly Dictionary<string, OrtValue> _cache = new();
        private bool _started;

        public OnnxDecoderSession(MarianEngine engine, int[] source)
        {
            _engine = engine;
            _mask = Enumerable.Repeat(1L, source.Length).ToArray();
            _maskValue = OrtValue.CreateTensorValueFromMemory(_mask, [1, source.Length]);
            _hidden = Encode(source);
            _pastNames = engine._decoder.InputMetadata.Keys.Where(k => k.StartsWith(PastPrefix, StringComparison.Ordinal)).ToArray();
        }

        public float[] NextTokenScores(IReadOnlyList<int> decoderIds)
        {
            // Without a cache the whole prefix is the input; with one, only the newest token.
            long[] ids = _started ? [decoderIds[^1]] : decoderIds.Select(i => (long)i).ToArray();
            bool[] useCache = [_started];
            using OrtValue idsValue = OrtValue.CreateTensorValueFromMemory(ids, [1, ids.Length]);
            using OrtValue cacheFlag = OrtValue.CreateTensorValueFromMemory(useCache, [1]);
            var emptyPast = new List<OrtValue>();
            try
            {
                var inputs = new Dictionary<string, OrtValue>
                {
                    [DecoderMask] = _maskValue,
                    [DecoderInputIds] = idsValue,
                    [DecoderHidden] = _hidden,
                    [CacheBranch] = cacheFlag,
                };
                foreach (string name in _pastNames)
                {
                    inputs[name] = _cache.TryGetValue(name, out OrtValue? cached) ? cached : NewEmptyPast(name, emptyPast);
                }

                return RunStep(inputs);
            }
            finally
            {
                emptyPast.ForEach(v => v.Dispose());
            }
        }

        public void Dispose()
        {
            foreach (OrtValue value in _cache.Values)
            {
                value.Dispose();
            }

            _hidden.Dispose();
            _maskValue.Dispose();
        }

        private float[] RunStep(Dictionary<string, OrtValue> inputs)
        {
            string[] outputNames = [DecoderOutput, .. PresentNamesToKeep()];
            using var run = new RunOptions();
            using IDisposableReadOnlyCollection<OrtValue> outputs = _engine._decoder.Run(run, inputs, outputNames);
            float[] scores = LastPosition(outputs[0]);
            for (int i = 1; i < outputNames.Length; i++)
            {
                Store(PastPrefix + outputNames[i][PresentPrefix.Length..], outputs[i]);
            }

            _started = true;
            return scores;
        }

        // The first step keeps every present tensor; later steps only the decoder ones (the encoder cache never changes).
        private IEnumerable<string> PresentNamesToKeep() =>
            _pastNames
                .Where(n => !_started || !n.Contains(EncoderCacheMarker, StringComparison.Ordinal))
                .Select(n => PresentPrefix + n[PastPrefix.Length..]);

        private void Store(string pastName, OrtValue value)
        {
            if (_cache.Remove(pastName, out OrtValue? old))
            {
                old.Dispose();
            }

            _cache[pastName] = CopyOf(value);
        }

        // The cache needs a [1, heads, 0, headDim] tensor before the first step.
        private OrtValue NewEmptyPast(string name, List<OrtValue> owner)
        {
            long[] dims = _engine._decoder.InputMetadata[name].Dimensions.Select(d => (long)d).ToArray();
            dims[0] = 1;
            dims[2] = 0;
            OrtValue empty = OrtValue.CreateTensorValueFromMemory(Array.Empty<float>(), dims);
            owner.Add(empty);
            return empty;
        }

        private OrtValue Encode(int[] source)
        {
            long[] ids = source.Select(i => (long)i).ToArray();
            using OrtValue idsValue = OrtValue.CreateTensorValueFromMemory(ids, [1, ids.Length]);
            var inputs = new Dictionary<string, OrtValue> { [EncoderInputIds] = idsValue, [EncoderMask] = _maskValue };
            using var run = new RunOptions();
            using IDisposableReadOnlyCollection<OrtValue> outputs = _engine._encoder.Run(run, inputs, [EncoderOutput]);
            return CopyOf(outputs[0]);
        }

        // The run's outputs are disposed with the collection, so tensors that must outlive it are copied out.
        private static OrtValue CopyOf(OrtValue value)
        {
            long[] shape = value.GetTensorTypeAndShape().Shape;
            float[] data = value.GetTensorDataAsSpan<float>().ToArray();
            return OrtValue.CreateTensorValueFromMemory(data, shape);
        }

        // Logits are [1, steps, vocab]; only the last step predicts the next token.
        private static float[] LastPosition(OrtValue logits)
        {
            long[] shape = logits.GetTensorTypeAndShape().Shape;
            int steps = (int)shape[1];
            int vocab = (int)shape[2];
            return logits.GetTensorDataAsSpan<float>()[((steps - 1) * vocab)..].ToArray();
        }
    }
}
