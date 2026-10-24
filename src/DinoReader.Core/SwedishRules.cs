namespace DinoReader.Core;

/// <summary>Swedish: ”…” and »…» quotes, dash dialogue, "sa Name" inversion, han/hon (Slice 105).</summary>
internal static class SwedishRules
{
    public static LanguageRules Create() => new(
        StraightQuote: '"',
        CurlyOpenQuote: '”',
        CurlyCloseQuote: '”',
        SpeechVerbs: RuleSets.Set("sa, säger, svarade, frågade, ropade, viskade, utbrast, mumlade, tillade, förklarade, suckade, "
            + "skrattade, fortsatte, förkunnade, föreslog, tänkte, kallade, pep, rytade, morrade, upprepade, beordrade, "
            + "anmärkte, kvittrade, sjöng, jublade, protesterade, invände, fnissade, utropade, genmälde"),
        AdverbSuffix: "ligt",
        MaleTitles: RuleSets.Set("herr, farbror, morbror, farfar, morfar, far, pappa, kung, prins, bror"),
        FemaleTitles: RuleSets.Set("fru, fröken, tant, faster, moster, farmor, mormor, mor, mamma, drottning, prinsessa, syster"),
        TitleAbbreviations: RuleSets.Set("hr, fru, dr, st"),
        MaleNames: RuleSets.Set("peter, lars, anders, erik, karl, johan, nils, per, olof, mats, jonas, oscar, oskar, william, lucas, "
            + "liam, hugo, elias, noah, leo, viktor, axel, gustav, emil, filip, simon, david, daniel, thomas, martin, ola, "
            + "sven, bengt, gunnar, kalle, pelle, jakob, albin, alfred, otto, tom, jack, max"),
        FemaleNames: RuleSets.Set("anna, maria, eva, karin, kristina, lena, sara, sarah, emma, ida, ebba, alice, maja, elsa, wilma, "
            + "julia, klara, linnea, astrid, sofia, sofie, lisa, lotta, johanna, helena, birgitta, ingrid, hanna, elin, "
            + "agnes, alma, saga, olivia, ella, nora, stina, greta, rosa"),
        MaleSubjectPronouns: RuleSets.Set("han"),
        FemaleSubjectPronouns: RuleSets.Set("hon"),
        MalePronouns: RuleSets.Set("han"),
        FemalePronouns: RuleSets.Set("hon"),
        NonNameWords: RuleSets.Set("den, det, de, en, ett, och, men, eller, så, då, när, som, efter, före, medan, ja, nej, åh, nå, jag, du, "
            + "han, hon, vi, ni, man, dem, oss, dig, mig, min, mitt, mina, din, ditt, dina, hans, hennes, vår, vårt, deras, vad, "
            + "vem, var, varför, hur, om, att, av, på, i, till, med, från, vid, för, över, under, här, där, detta, denna, "
            + "dessa, plötsligt, snart, äntligen, hej, tack, alla, allt, varje, ingen, någon"),
        IndefinitePronouns: RuleSets.Set("alla, ingen, någon, varje, båda, de, vi, man, allt"),
        SentenceTerminators: ".!?…",
        DialogueGapChars: RuleSets.GapChars,
        ExtraOpenQuotes: "»“",
        ExtraCloseQuotes: "»“«",
        DialogueDashes: "–—",
        DashTagAfterQuote: true);
}
