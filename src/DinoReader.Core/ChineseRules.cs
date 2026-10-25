namespace DinoReader.Core;

/// <summary>
/// Chinese: “ ” and 「 」 quotes, no spaces and no capitals. Names come from the name list and the
/// 小/阿/老 + one character shape (小明, 阿强), the speaker from 他/她 or a listed name before a
/// speech verb (彼得说).
/// </summary>
internal static class ChineseRules
{
    public static LanguageRules Create() => new(
        StraightQuote: '"',
        CurlyOpenQuote: '“',
        CurlyCloseQuote: '”',
        SpeechVerbs: RuleSets.Set("说道, 回答说, 笑着说, 低声说, 大声说, 小声说, 轻声说, 补充说, 解释说, 继续说, 喊道, 问道, 答道, "
            + "叫道, 笑道, 叹道, 大叫, 惊呼, 低语, 嘀咕, 回答, 说, 问, 喊, 叫, 答, 道, 嚷"),
        AdverbSuffix: "",
        MaleTitles: RuleSets.Set("爸爸, 叔叔, 爷爷, 哥哥, 国王, 王子, 先生, 父亲, 舅舅, 弟弟"),
        FemaleTitles: RuleSets.Set("妈妈, 阿姨, 奶奶, 姐姐, 王后, 公主, 女士, 母亲, 姑姑, 妹妹"),
        TitleAbbreviations: new HashSet<string>(),
        MaleNames: RuleSets.Set("彼得, 小明, 小刚, 小华, 小强, 明明, 大卫, 约翰, 汤姆, 杰克, 小伟, 阿强, 老王, 小宝, 乐乐, 豆豆"),
        FemaleNames: RuleSets.Set("小红, 小丽, 小美, 丽丽, 莉莉, 露西, 玛丽, 爱丽丝, 艾玛, 安娜, 小花, 阿花, 小雪, 妞妞, 小兰"),
        MaleSubjectPronouns: RuleSets.Set("他"),
        FemaleSubjectPronouns: RuleSets.Set("她"),
        MalePronouns: RuleSets.Set("他"),
        FemalePronouns: RuleSets.Set("她"),
        NonNameWords: new HashSet<string>(),
        IndefinitePronouns: new HashSet<string>(),
        SentenceTerminators: ".!?…。！？",
        DialogueGapChars: RuleSets.GapChars,
        ExtraOpenQuotes: "「",
        ExtraCloseQuotes: "」",
        Script: ScriptStyle.Unspaced,
        NameVerbGap: @"\s*",
        ExtraNamePattern: @"[小阿老]\p{Lo}",
        Flavours: FlavourWords.Chinese);
}
