namespace DinoReader.Core;

/// <summary>
/// Arabic: « » quotes, no capital letters, هو/هي pronouns, and speech verbs whose "-ت" ending
/// shows a female speaker (قالت). A leading و or ف ("and he said") is allowed before a verb.
/// </summary>
internal static class ArabicRules
{
    private static readonly HashSet<string> MaleVerbs = RuleSets.Set("قال, سأل, أجاب, صاح, همس, صرخ, تمتم, أضاف, تنهد, ضحك, "
        + "هتف, نادى, تساءل, أكمل, شرح, اقترح, أعلن, فكر, رد");

    private static readonly HashSet<string> FemaleVerbs = RuleSets.Set("قالت, سألت, أجابت, صاحت, همست, صرخت, تمتمت, أضافت, "
        + "تنهدت, ضحكت, هتفت, نادت, تساءلت, أكملت, شرحت, اقترحت, أعلنت, فكرت, ردت");

    private static readonly HashSet<string> PluralAndPresent = RuleSets.Set("قالوا, قلت, يقول, تقول");

    public static LanguageRules Create() => new(
        StraightQuote: '"',
        CurlyOpenQuote: '«',
        CurlyCloseQuote: '»',
        SpeechVerbs: RuleSets.Union(RuleSets.Union(MaleVerbs, FemaleVerbs), PluralAndPresent),
        AdverbSuffix: "",
        MaleTitles: RuleSets.Set("السيد, الأب, العم, الخال, الملك, الأمير, الجد, الأخ, أبو, والد"),
        FemaleTitles: RuleSets.Set("السيدة, الأم, العمة, الخالة, الملكة, الأميرة, الجدة, الأخت, أم, والدة"),
        TitleAbbreviations: new HashSet<string>(),
        MaleNames: RuleSets.Set("بيتر, محمد, أحمد, علي, عمر, خالد, يوسف, إبراهيم, حسن, حسين, سامي, كريم, طارق, زياد, أمير, "
            + "رامي, ياسر, سعيد, مصطفى, عثمان, آدم, نوح, توم, جاك"),
        FemaleNames: RuleSets.Set("مريم, فاطمة, عائشة, سارة, ليلى, نور, سلمى, هدى, منى, رنا, دينا, لينا, ريم, أميرة, زينب, "
            + "خديجة, ياسمين, هالة, سلوى, ندى, لمى, جميلة, أمل, رحمة, ماري, ليلي"),
        MaleSubjectPronouns: RuleSets.Set("هو"),
        FemaleSubjectPronouns: RuleSets.Set("هي"),
        MalePronouns: RuleSets.Set("هو, له"),
        FemalePronouns: RuleSets.Set("هي, لها"),
        NonNameWords: RuleSets.Set("و, في, على, من, إلى, عن, مع, ثم, لكن, أو, أن, إن, هذا, هذه, ذلك, تلك, هنا, هناك, أنا, أنت, "
            + "هو, هي, نحن, هم, هن, كل, بعض, لا, نعم, يا, آه, أهلا, شكرا, فجأة, بعد, قبل, عندما, إذا, ماذا, لماذا, أين, كيف, "
            + "هل, بهدوء, بسرعة, ببطء, مرة, كان, كانت"),
        IndefinitePronouns: RuleSets.Set("الجميع, لا أحد, أحد, كلهم, كلاهما"),
        SentenceTerminators: ".!?…؟",
        DialogueGapChars: RuleSets.GapChars,
        ExtraOpenQuotes: "“",
        ExtraCloseQuotes: "”",
        Script: ScriptStyle.CaselessSpaced,
        MaleVerbs: MaleVerbs,
        FemaleVerbs: FemaleVerbs,
        FemaleNameEndings: RuleSets.List("ة"),
        VerbPrefix: "[وف]?");
}
