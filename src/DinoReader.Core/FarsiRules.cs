namespace DinoReader.Core;

/// <summary>
/// Farsi: « » quotes, no capital letters, and subject pronouns that carry no gender (او), so a
/// character's name comes from a speech tag or the name list and its gender from the name or
/// title lists. A character the lists cannot place is Unknown and is fixed on the cast screen.
/// </summary>
internal static class FarsiRules
{
    public static LanguageRules Create() => new(
        StraightQuote: '"',
        CurlyOpenQuote: '«',
        CurlyCloseQuote: '»',
        SpeechVerbs: RuleSets.Set("گفت, گفتند, پرسید, پرسیدند, فریاد زد, داد زد, جواب داد, پاسخ داد, زمزمه کرد, نجوا کرد, "
            + "اضافه کرد, ادامه داد, توضیح داد, صدا زد, فکر کرد, خندید, غرید, نالید, آه کشید, می‌گوید, گفتم"),
        AdverbSuffix: "",
        MaleTitles: RuleSets.Set("آقای, پدر, عمو, دایی, پادشاه, شاهزاده, پدربزرگ, بابا, برادر, بابابزرگ"),
        FemaleTitles: RuleSets.Set("خانم, مادر, عمه, خاله, ملکه, شاهدخت, مادربزرگ, مامان, خواهر, مامان‌بزرگ"),
        TitleAbbreviations: new HashSet<string>(),
        MaleNames: RuleSets.Set("پیتر, علی, محمد, رضا, حسین, امیر, مهدی, حسن, احمد, سعید, کامران, بهرام, داریوش, کیوان, پویا, "
            + "آرش, سیاوش, رستم, سهراب, فرهاد, کاوه, بابک, سینا, آرمان, پارسا, ماهان, نیما, پوریا, کیان, دانیال, یوسف, "
            + "ابراهیم, موسی, نوید, شهرام, تام, جک, جان"),
        FemaleNames: RuleSets.Set("مریم, فاطمه, زهرا, سارا, نرگس, لیلی, شیرین, مینا, سمیرا, ریحانه, الهام, نگار, پریسا, آیدا, "
            + "ترانه, مهسا, نازنین, سحر, شادی, ستاره, گلی, رز, آنا, ماری, لوسی, آلیس, اما, سوسن, پروین, فریبا, شقایق, "
            + "یاسمن, نسترن, هستی, باران"),
        MaleSubjectPronouns: new HashSet<string>(),
        FemaleSubjectPronouns: new HashSet<string>(),
        MalePronouns: new HashSet<string>(),
        FemalePronouns: new HashSet<string>(),
        NonNameWords: RuleSets.Set("و, در, به, از, که, این, آن, را, با, برای, تا, یا, اما, ولی, هم, هر, همه, یک, چند, چه, چرا, "
            + "کجا, کی, چگونه, من, تو, او, ما, شما, آنها, ایشان, پس, سپس, ناگهان, بعد, قبل, وقتی, اگر, بله, نه, آه, اوه, "
            + "خب, حالا, اینجا, آنجا, آرام, آهسته, بلند, سریع, تند, شاد, غمگین, خوشحال, ناراحت, دوباره, همیشه, هرگز, بسیار, خیلی"),
        IndefinitePronouns: RuleSets.Set("همه, هیچکس, کسی, همگی, هر دو"),
        SentenceTerminators: ".!?…؟",
        DialogueGapChars: RuleSets.GapChars,
        ExtraOpenQuotes: "“",
        ExtraCloseQuotes: "”",
        Script: ScriptStyle.CaselessSpaced);
}
