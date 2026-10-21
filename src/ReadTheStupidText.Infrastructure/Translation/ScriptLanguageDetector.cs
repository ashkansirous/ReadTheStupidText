using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Infrastructure.Translation;

/// <summary>
/// A small on-device guess of which of the nine app languages a text is written in (Slice 57): the
/// script settles Russian, Chinese, Japanese, Arabic and Farsi, and common-word counts settle English,
/// French, Spanish and Portuguese. Returns null when unsure, so the original is read. Nothing leaves the device.
/// </summary>
public sealed class ScriptLanguageDetector : ILanguageDetector
{
    private const int MinLatinScore = 2;
    private const double KanaShare = 0.05;
    private const float ScriptConfidence = 0.9f;
    private const float LatinConfidence = 0.7f;

    // Letters only Farsi uses (peh, tcheh, jeh, gaf) and the Farsi forms of kaf and yeh, against the Arabic ones.
    private const string FarsiLetters = "پچژگکی";
    private const string ArabicLetters = "كيى";

    private static readonly char[] WordEdges =
        ['.', ',', '!', '?', ';', ':', '"', '\'', '“', '”', '«', '»', '—', '-', '(', ')'];

    private static readonly Dictionary<AppLanguage, HashSet<string>> StopWords = new()
    {
        [AppLanguage.English] = Words("the and was were that with for his her they said you not but had have this what there"),
        [AppLanguage.French] = Words("le la les des une est et il elle dans que qui pas pour avec sur sont mais très être"),
        [AppLanguage.Spanish] = Words("el los las una es y en que de un por con para su sus pero muy estaba dijo"),
        [AppLanguage.Portuguese] = Words("o os as uma é e em que de um por com para seu sua mas muito estava disse não"),
    };

    public Task<DetectedLanguage?> DetectAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult(Detect(text));

    /// <summary>The synchronous guess; separate so the pure logic is testable without a task.</summary>
    public static DetectedLanguage? Detect(string text)
    {
        var counts = new ScriptCounts(TranslationFlow.DetectionSample(text));
        if (counts.Letters == 0)
        {
            return null;
        }

        return counts.Dominant switch
        {
            Script.Cyrillic => Found(AppLanguage.Russian, ScriptConfidence),
            Script.Arabic => Found(counts.Farsi > counts.ArabicOnly ? AppLanguage.Persian : AppLanguage.Arabic, ScriptConfidence),
            Script.Cjk => Found(IsJapanese(counts) ? AppLanguage.Japanese : AppLanguage.Chinese, ScriptConfidence),
            _ => GuessLatin(text),
        };
    }

    private static bool IsJapanese(ScriptCounts counts) =>
        counts.Kana > 0 && counts.Kana >= (counts.Kana + counts.Han) * KanaShare;

    private static DetectedLanguage? GuessLatin(string text)
    {
        List<string> words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim(WordEdges).ToLowerInvariant())
            .ToList();
        var scores = StopWords
            .Select(l => (Language: l.Key, Score: words.Count(l.Value.Contains)))
            .OrderByDescending(s => s.Score)
            .ToList();
        bool clear = scores[0].Score >= MinLatinScore && scores[0].Score > scores[1].Score;
        return clear ? Found(scores[0].Language, LatinConfidence) : null;
    }

    private static DetectedLanguage Found(AppLanguage language, float confidence) =>
        new(AppLanguages.Code(language), confidence);

    private static HashSet<string> Words(string list) => new(list.Split(' '));

    private enum Script { Latin, Cyrillic, Arabic, Cjk }

    private sealed class ScriptCounts
    {
        public int Latin, Cyrillic, Arabic, Han, Kana, Farsi, ArabicOnly;

        public ScriptCounts(string sample)
        {
            foreach (char c in sample.Where(char.IsLetter))
            {
                Add(c);
            }
        }

        public int Letters => Latin + Cyrillic + Arabic + Han + Kana;

        public Script Dominant
        {
            get
            {
                int cjk = Han + Kana;
                int max = Math.Max(Math.Max(Latin, Cyrillic), Math.Max(Arabic, cjk));
                if (max == Cyrillic)
                {
                    return Script.Cyrillic;
                }

                if (max == Arabic)
                {
                    return Script.Arabic;
                }

                return max == cjk ? Script.Cjk : Script.Latin;
            }
        }

        private void Add(char c)
        {
            if (c is >= 'Ѐ' and <= 'ӿ')
            {
                Cyrillic++;
            }
            else if (c is >= '؀' and <= 'ۿ' or >= 'ݐ' and <= 'ݿ')
            {
                AddArabicScript(c);
            }
            else if (c is >= '぀' and <= 'ヿ')
            {
                Kana++;
            }
            else if (c is >= '一' and <= '鿿')
            {
                Han++;
            }
            else if (c < 'ɐ')
            {
                Latin++;
            }
        }

        private void AddArabicScript(char c)
        {
            Arabic++;
            if (FarsiLetters.Contains(c))
            {
                Farsi++;
            }
            else if (ArabicLetters.Contains(c))
            {
                ArabicOnly++;
            }
        }
    }
}
