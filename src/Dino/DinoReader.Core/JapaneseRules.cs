namespace DinoReader.Core;

/// <summary>
/// Japanese: 「 」 quotes, no spaces and no capitals. A name is a listed name or a katakana run
/// (ピーター); the tag follows the quote as <c>とピーターは言った</c> or <c>と彼は言った</c>.
/// </summary>
internal static class JapaneseRules
{
    public static LanguageRules Create() => new(
        StraightQuote: '"',
        CurlyOpenQuote: '「',
        CurlyCloseQuote: '」',
        SpeechVerbs: RuleSets.Set("言いました, 答えました, 言った, 言う, 答えた, 尋ねた, 聞いた, 叫んだ, 呟いた, つぶやいた, ささやいた, "
            + "囁いた, 笑った, 付け加えた, 続けた, 説明した, 提案した, 思った, 呼んだ, 怒鳴った, 話した"),
        AdverbSuffix: "",
        MaleTitles: RuleSets.Set("お父さん, お兄さん, おじいさん, 王様, 王子, おじさん, 先生, お父様"),
        FemaleTitles: RuleSets.Set("お母さん, お姉さん, おばあさん, 女王, お姫様, おばさん, お母様"),
        TitleAbbreviations: new HashSet<string>(),
        MaleNames: RuleSets.Set("太郎, 次郎, 健太, 大輔, 翔太, 拓也, ピーター, トム, ジョン, ジャック, ケン, タロウ, ジロウ"),
        FemaleNames: RuleSets.Set("花子, 美咲, 由美, さくら, 結衣, ユキ, ミキ, メアリー, ルーシー, アリス, エマ, アンナ, ハナコ"),
        MaleSubjectPronouns: RuleSets.Set("彼"),
        FemaleSubjectPronouns: RuleSets.Set("彼女"),
        MalePronouns: RuleSets.Set("彼"),
        FemalePronouns: RuleSets.Set("彼女"),
        NonNameWords: new HashSet<string>(),
        IndefinitePronouns: new HashSet<string>(),
        SentenceTerminators: ".!?…。！？",
        DialogueGapChars: RuleSets.GapChars,
        ExtraOpenQuotes: "“",
        ExtraCloseQuotes: "”",
        Script: ScriptStyle.Unspaced,
        NameVerbGap: @"(?:は|が)?\s*",
        NameLead: "(?:と|って)?",
        ExtraNamePattern: @"[ァ-ヺー]{2,}",
        Flavours: FlavourWords.Japanese);
}
