namespace DinoReader.Core;

/// <summary>German: „…“ and »…« quotes, "sagte Name" inversion, er (Slice 168); "sie" is she, they and formal you; it is a woman only next to a singular verb (sagte sie), a group next to a plural one (sagten sie).</summary>
internal static class GermanRules
{
    // German capitalises every noun, so a capital letter alone does not mark a name: the title, pronoun and non-name lists carry more weight than in Dutch.
    private static FlavourWords Flavours { get; } = FlavourWords.Of(
        "lamm, lämmchen, maus, mäuschen, hase, häschen, kaninchen, vogel, vögelchen, küken, entchen, entlein, kätzchen, welpe, frosch, fröschlein, biene, schmetterling, eichhörnchen, ferkel",
        "wolf, bär, riese, elefant, oger, troll, drache, nilpferd, flusspferd, wal, stier, groß, große, großer",
        "eule, opa, großvater, schildkröte, faultier",
        "klein, kleine, kleiner, kleines, winzig, winzige, baby",
        "groß, große, großer, großes, riese")
        .With("brummig, brummige, brummiger, mürrisch, mürrische, mürrischer, hexe, kobold, krabbe", "fuchs, katze, kater, schlange, gespenst, geist, spion", "fee, elf, elfe, grille");

    // Every quote style is one open/close pair („…“, »…«, ›…‹ and the English “…”); “ closes the first and opens the last, so a
    // quote is closed only by the mark of the pair that opened it.
    private const string QuotePairs = "„“»«›‹“”";

    public static LanguageRules Create() => new(
        StraightQuote: '"',
        CurlyOpenQuote: '„',
        CurlyCloseQuote: '“',
        SpeechVerbs: RuleSets.Set("sagte, sagten, sagt, fragte, fragten, fragt, rief, riefen, ruft, antwortete, antworteten, antwortet, flüsterte, flüsterten, flüstert, "
            + "murmelte, murmelten, meinte, meinten, erwiderte, erwiderten, entgegnete, erklärte, erklärten, seufzte, seufzten, lachte, lachten, schrie, schrien, "
            + "dachte, brummte, brummten, knurrte, piepste, jubelte, wiederholte, befahl, protestierte, stöhnte, sang, kicherte, schluchzte, jammerte, "
            + "sprach, rief, bat, bettelte, grinste, zischte, brüllte, antwortete"),
        AdverbSuffix: "lich",
        MaleTitles: RuleSets.Set("herr, onkel, opa, großvater, papa, vater, vati, papi, könig, prinz, bruder, ritter"),
        FemaleTitles: RuleSets.Set("frau, fräulein, tante, oma, großmutter, mama, mutter, mutti, mami, königin, prinzessin, schwester"),
        TitleAbbreviations: RuleSets.Set("hr, fr, dr, prof, st"),
        MaleNames: RuleSets.Set("peter, hans, karl, fritz, max, paul, felix, lukas, jonas, leon, finn, ben, tim, tom, jan, nico, lars, emil, otto, anton, "
            + "jakob, moritz, elias, noah, luis, david, daniel, thomas, stefan, michael, klaus, wilhelm, heinrich, georg, johannes, matthias, philipp, "
            + "florian, sebastian, tobias, simon, julian, niklas, oskar, theo, henry, rudi, hänsel"),
        FemaleNames: RuleSets.Set("anna, maria, emma, sophie, lena, lea, mia, hanna, lisa, laura, julia, sara, sarah, lotte, greta, clara, klara, emily, marie, "
            + "nina, ida, frieda, mila, paula, lina, johanna, katharina, heidi, gretel, liese, ella, luisa, charlotte, amelie, leonie, nele, pia, "
            + "sabine, petra, monika, erika, ute, rosa, elsa"),
        MaleSubjectPronouns: RuleSets.Set("er"),
        FemaleSubjectPronouns: RuleSets.Set("sie"),
        MalePronouns: RuleSets.Set("er"),
        FemalePronouns: RuleSets.Set("sie"),
        NonNameWords: RuleSets.Set("der, die, das, den, dem, des, ein, eine, einen, einem, einer, und, aber, oder, doch, dann, als, nach, vor, während, ja, nein, o, oh, na, "
            + "ich, du, er, sie, es, wir, ihr, man, ihn, ihm, ihnen, uns, euch, mich, dich, sich, mein, meine, dein, deine, sein, seine, ihre, unser, unsere, "
            + "was, wer, wo, warum, wie, wann, wohin, woher, dass, dies, dieser, diese, dieses, da, dort, hier, plötzlich, endlich, bald, hallo, danke, bitte, "
            + "alle, alles, jeder, jede, jedes, niemand, jemand, nur, auch, noch, schon, so, nun, jetzt, wenn, weil, denn, zu, in, an, auf, bei, mit, von, aus, "
            + "durch, um, über, unter, am, im, zum, zur, vom, beim, für, gegen, ohne, bis, seit, doch, mal, nicht, kein, keine, am, ins, ans"),
        IndefinitePronouns: RuleSets.Set("alle, jeder, jede, niemand, jemand, man, alles, beide, wir, sie"),
        SentenceTerminators: ".!?…",
        DialogueGapChars: RuleSets.GapChars,
        ExtraOpenQuotes: "»›“",
        ExtraCloseQuotes: "«‹”",
        Flavours: Flavours,
        QuotePairs: QuotePairs,
        PluralVerbSuffix: "en");
}
