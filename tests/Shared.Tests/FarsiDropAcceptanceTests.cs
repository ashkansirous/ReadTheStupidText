using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Infrastructure.Reading;
using Xunit;

namespace ReadTheStupidText.Tests;

/// <summary>
/// Slice 149 acceptance, not part of the normal run: reads the Slice 145 Farsi sound words with Piper mana
/// through the Windows app path and counts dropped words with and without the guard. Runs only when
/// RTST_FA_PACK_DIR names an installed piper-fa pack folder (the one holding espeak-ng-data and voices).
/// </summary>
public sealed class FarsiDropAcceptanceTests
{
    private const string PackDirVariable = "RTST_FA_PACK_DIR";
    private const int Takes = 5;
    private const int WavHeaderBytes = 44;

    private static readonly string[] Words =
    [
        "دنگ!", "دَنگ!", "دنگگگ!", "دنگ! دنگ! دنگ!", "دنگ… دنگ… دنگ!", "بوووم!", "ووش!", "ووووش!", "ووش! ووش!", "ووش… ووش!",
        "وووووش… ووش!", "ویژژژ!", "تق!", "تق تق تق!", "تققق!", "تق! تق!", "هاپ هاپ!", "هاااپ هاپ!", "میو میو!", "میوووو!", "بع بع!",
    ];

    private sealed class ConsoleLog : ISystemLog
    {
        public int Rescues { get; private set; }

        public void Info(string message, int? activityId = null) { }

        public void Debug(string message, int? activityId = null) { }

        public void Warning(string message, int? activityId = null, Exception? error = null) => Rescues++;

        public void Error(string message, int? activityId = null, Exception? error = null) { }
    }

    private static readonly string[] GluedSounds = ["دنگ!", "تق!", "ووش!", "میو میو!", "هاپ هاپ!"];

    private const string Carrier = "بچه خوک فرار کرد.";

    // A sound word glued to the carrier adds at least this much sound (Slice 145: +260 ms or more); a unit under
    // the carrier's own quietest take plus this lost its sound word.
    private const double GluedSoundMinMs = 100;

    [Fact]
    public async Task Mana_words_are_not_dropped_through_the_app_path()
    {
        string? pack = Environment.GetEnvironmentVariable(PackDirVariable);
        if (string.IsNullOrEmpty(pack))
        {
            return; // needs the installed piper-fa pack; set RTST_FA_PACK_DIR to run it
        }

        var adabi = new VoiceInfo("piper:fa-ganji-adabi", "Ganji Adabi", "fa", VoiceEngineKind.Vits, 0, "voices/ganji_adabi");
        var ganji = new VoiceInfo("piper:fa-ganji", "Ganji", "fa", VoiceEngineKind.Vits, 0, "voices/ganji");
        var voice = new VoiceInfo("piper:fa-mana", "Mana", "fa", VoiceEngineKind.Vits, 0, "voices/mana");
        var gyro = new VoiceInfo("piper:fa-gyro", "Gyro", "fa", VoiceEngineKind.Vits, 0, "voices/gyro");
        var paths = new VoiceModelPaths(pack, VoiceEngineKind.Vits, "fa", [adabi, ganji, voice, gyro], "2", voice.ModelDir);
        using var engines = new EngineCache<BuiltEngine>((_, p) => new BuiltEngine(new RefCountedTts(SherpaEngineFactory.Build(p, 2)), p));
        BuiltEngine engine = engines.Acquire(paths)!;
        string folder = Path.Combine(Path.GetTempPath(), "rtst-s149-" + Guid.NewGuid().ToString("N"));
        try
        {
            string summary = await RunAsync(engine, engines, voice, folder);
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "s149-acceptance.txt"), summary);
            Assert.Contains("guardedDropped=0 ", summary);
            Assert.Contains("guardedGluedDropped=0 ", summary);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private static async Task<string> RunAsync(BuiltEngine engine, EngineCache<BuiltEngine> engines, VoiceInfo voice, string folder)
    {
        double carrierMin = Enumerable.Range(0, Takes).Min(_ => RawSpanMs(engine, Carrier, voice));
        var units = Words.Select(w => (Text: w, Glued: false))
            .Concat(GluedSounds.SelectMany(s => new[] { $"{s} {Carrier}", $"{Carrier} {s}" }).Select(u => (Text: u, Glued: true))).ToList();
        int total = 0, rawDropped = 0, guardedDropped = 0, rawGlued = 0, guardedGlued = 0, warnings = 0;
        var still = new List<string>();
        foreach ((string text, bool glued) in units)
        {
            for (int take = 0; take < Takes; take++)
            {
                total++;
                bool rawDrop = Dropped(RawSpanMs(engine, text, voice), text, glued, carrierMin);
                var log = new ConsoleLog();
                var source = new NeuralSentenceSource(Path.Combine(folder, Guid.NewGuid().ToString("N")), log, 2, p => engines.Acquire(p));
                SpokenTake clip = Decode(await source.JoinChunkAsync(engine, text, voice, null, CancellationToken.None));
                bool drop = Dropped(SpanMs(clip), text, glued, carrierMin);
                rawDropped += rawDrop && !glued ? 1 : 0;
                rawGlued += rawDrop && glued ? 1 : 0;
                guardedDropped += drop && !glued ? 1 : 0;
                guardedGlued += drop && glued ? 1 : 0;
                warnings += log.Rescues;
                if (drop)
                {
                    still.Add(text);
                }
            }
        }

        return $"S149 fa mana: takes={total} carrierMinMs={carrierMin:F0} rawDropped={rawDropped} guardedDropped={guardedDropped} rawGluedDropped={rawGlued} guardedGluedDropped={guardedGlued} warnings={warnings} stillDropped=[{string.Join(" | ", still)}]";
    }

    // Lone units: the guard's own rule. Glued units: shorter than the carrier alone plus the least a sound word adds.
    private static bool Dropped(double spanMs, string text, bool glued, double carrierMinMs) =>
        glued ? spanMs < carrierMinMs + GluedSoundMinMs : SilentOutputGuard.IsSilent(SpanTake(spanMs), text);

    private static SpokenTake SpanTake(double ms) => new(new float[(int)(ms * 22.05) + 1].Select(_ => 0.3f).ToArray(), 22050);

    private static double RawSpanMs(BuiltEngine engine, string text, VoiceInfo voice)
    {
        using var scope = new AudioScope(SherpaEngineFactory.Generate(engine, text, voice.SpeakerId));
        return SpanMs(new SpokenTake(scope.Audio.Samples, scope.Audio.SampleRate));
    }

    private static double SpanMs(SpokenTake take)
    {
        int first = Array.FindIndex(take.Samples, v => Math.Abs(v) > 0.01f);
        if (first < 0)
        {
            return 0;
        }

        int last = Array.FindLastIndex(take.Samples, v => Math.Abs(v) > 0.01f);
        return (last - first + 1) * 1000.0 / take.SampleRate;
    }

    private static SpokenTake Decode(byte[] wav)
    {
        int rate = BitConverter.ToInt32(wav, 24);
        float[] samples = new float[(wav.Length - WavHeaderBytes) / 2];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = BitConverter.ToInt16(wav, WavHeaderBytes + i * 2) / 32768f;
        }

        return new SpokenTake(samples, rate);
    }

    private sealed class AudioScope(SherpaOnnx.OfflineTtsGeneratedAudio audio) : IDisposable
    {
        public SherpaOnnx.OfflineTtsGeneratedAudio Audio { get; } = audio;

        public void Dispose() => Audio.Dispose();
    }
}
