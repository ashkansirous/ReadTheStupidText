namespace DinoReader.Core;

/// <summary>Norwegian (Bokmål): «…» and „…“ quotes, dash dialogue, "sa Name" inversion, han/hun (Slice 105).</summary>
internal static class NorwegianRules
{
    public static LanguageRules Create() => new(
        StraightQuote: '"',
        CurlyOpenQuote: '«',
        CurlyCloseQuote: '»',
        SpeechVerbs: RuleSets.Set("sa, sier, svarte, spurte, ropte, hvisket, utbrøt, mumlet, tilføyde, forklarte, sukket, "
            + "fortsatte, erklærte, foreslo, tenkte, kalte, pep, brølte, knurret, gjentok, ba, befalte, bemerket, kvitret, "
            + "sang, jublet, protesterte, innvendte, humret, freste, gryntet"),
        AdverbSuffix: "lig",
        MaleTitles: RuleSets.Set("herr, onkel, bestefar, farfar, morfar, far, pappa, kong, prins, bror"),
        FemaleTitles: RuleSets.Set("fru, frøken, tante, bestemor, farmor, mormor, mor, mamma, dronning, prinsesse, søster"),
        TitleAbbreviations: RuleSets.Set("hr, fru, dr, st"),
        MaleNames: RuleSets.Set("peter, per, ole, lars, jon, kristian, anders, erik, magnus, jonas, oscar, oskar, william, lucas, "
            + "emil, noah, liam, jakob, filip, henrik, thomas, martin, sander, mathias, sondre, håkon, haakon, olav, "
            + "knut, trygve, tobias, simon, daniel, david, hans, tom, jack, max, leo, viktor"),
        FemaleNames: RuleSets.Set("anna, maria, ingrid, kari, nora, emma, sara, sarah, sofie, sofia, ida, ella, olivia, maja, "
            + "astrid, hanne, marit, silje, thea, julie, emilie, amalie, live, line, camilla, mette, kristin, lise, "
            + "solveig, signe, eva, agnes, alma, tuva, iben, rosa, liv"),
        MaleSubjectPronouns: RuleSets.Set("han"),
        FemaleSubjectPronouns: RuleSets.Set("hun"),
        MalePronouns: RuleSets.Set("han"),
        FemalePronouns: RuleSets.Set("hun"),
        NonNameWords: RuleSets.Set("den, det, de, en, et, ei, og, men, eller, så, da, når, som, etter, før, mens, ja, nei, å, nå, jeg, du, "
            + "han, hun, vi, dere, man, dem, oss, deg, meg, min, mitt, mine, din, ditt, dine, hans, hennes, vår, vårt, deres, hva, "
            + "hvem, hvor, hvorfor, hvordan, hvis, at, av, på, i, til, med, fra, ved, for, om, over, under, her, der, dette, "
            + "denne, disse, plutselig, snart, endelig, hei, takk, alle, alt, hver, ingen, noen, engang"),
        IndefinitePronouns: RuleSets.Set("alle, ingen, noen, enhver, begge, de, vi, man, alt"),
        SentenceTerminators: ".!?…",
        DialogueGapChars: RuleSets.GapChars,
        ExtraOpenQuotes: "„“”",
        ExtraCloseQuotes: "“”",
        DialogueDashes: "–—",
        DashTagAfterQuote: true,
        Flavours: FlavourWords.Norwegian);
}
