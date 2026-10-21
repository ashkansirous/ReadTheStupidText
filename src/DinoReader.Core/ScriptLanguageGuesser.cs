using ReadTheStupidText.Application.Reading;

namespace DinoReader.Core;

/// <summary>
/// A small on-device guess of which of the nine languages a text is written in, from its letters:
/// the script settles Russian, Chinese, Japanese, Arabic and Farsi, and common-word counts settle
/// English, French, Spanish and Portuguese. Used to pre-fill the story language at import ("· detected");
/// the grown-up can always change it. It never leaves the device and returns null when unsure.
/// </summary>
public static class ScriptLanguageGuesser
{
    private const int SampleLength = 2000;
    private const int MinLatinScore = 2;
    private const double KanaShare = 0.05;

    private static readonly Dictionary<AppLanguage, HashSet<string>> StopWords = new()
    {
        [AppLanguage.English] = Words("the and was were that with for his her they said you not but had have this what there"),
        [AppLanguage.French] = Words("le la les des une est et il elle dans que qui pas pour avec sur sont mais très être"),
        [AppLanguage.Spanish] = Words("el los las una es y en que de un por con para su sus pero muy estaba dijo"),
        [AppLanguage.Portuguese] = Words("o os as uma é e em que de um por com para seu sua mas muito estava disse não"),
    };

    // Letters only Farsi uses (peh, tcheh, jeh, gaf) and the Farsi forms of kaf and yeh, against the Arabic ones.
    private const string FarsiLetters = "پچژگکی";
    private const string ArabicLetters = "كيى";

    public static string? Guess(string text)
    {
        string sample = text.Length > SampleLength ? text[..SampleLength] : text;
        var counts = new ScriptCounts(sample);
        if (counts.Letters == 0) return null;
        AppLanguage? language = counts.Dominant switch
        {
            Script.Cyrillic => AppLanguage.Russian,
            Script.Arabic => counts.Farsi > counts.ArabicOnly ? AppLanguage.Persian : AppLanguage.Arabic,
            Script.Cjk => counts.Kana >= (counts.Kana + counts.Han) * KanaShare && counts.Kana > 0 ? AppLanguage.Japanese : AppLanguage.Chinese,
            _ => GuessLatin(sample),
        };
        return language is { } found ? AppLanguages.Code(found) : null;
    }

    private static AppLanguage? GuessLatin(string sample)
    {
        var words = sample.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('.', ',', '!', '?', ';', ':', '"', '\'', '“', '”', '«', '»', '—', '-', '(', ')').ToLowerInvariant())
            .ToList();
        var scores = StopWords.Select(l => (Language: l.Key, Score: words.Count(l.Value.Contains))).OrderByDescending(s => s.Score).ToList();
        bool clear = scores[0].Score >= MinLatinScore && scores[0].Score > scores[1].Score;
        return clear ? scores[0].Language : null;
    }

    private static HashSet<string> Words(string list) => new(list.Split(' '));

    private enum Script { Latin, Cyrillic, Arabic, Cjk }

    private sealed class ScriptCounts
    {
        public int Latin, Cyrillic, Arabic, Han, Kana, Farsi, ArabicOnly;

        public ScriptCounts(string sample)
        {
            foreach (char c in sample.Where(char.IsLetter)) Add(c);
        }

        public int Letters => Latin + Cyrillic + Arabic + Han + Kana;

        public Script Dominant
        {
            get
            {
                int cjk = Han + Kana;
                int max = Math.Max(Math.Max(Latin, Cyrillic), Math.Max(Arabic, cjk));
                if (max == Cyrillic) return Script.Cyrillic;
                if (max == Arabic) return Script.Arabic;
                return max == cjk ? Script.Cjk : Script.Latin;
            }
        }

        private void Add(char c)
        {
            if (c is >= 'Ѐ' and <= 'ӿ') Cyrillic++;
            else if (c is >= '؀' and <= 'ۿ' or >= 'ݐ' and <= 'ݿ') AddArabic(c);
            else if (c is >= '぀' and <= 'ヿ') Kana++;
            else if (c is >= '㐀' and <= '鿿') Han++;
            else if (c < 'ɐ') Latin++;
        }

        private void AddArabic(char c)
        {
            Arabic++;
            if (FarsiLetters.Contains(c)) Farsi++;
            else if (ArabicLetters.Contains(c)) ArabicOnly++;
        }
    }
}
