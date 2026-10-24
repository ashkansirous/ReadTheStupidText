namespace DinoReader.Core;

/// <summary>Danish: »…« and „…“ quotes, dash dialogue, "sagde Name" inversion, han/hun (Slice 105).</summary>
internal static class DanishRules
{
    public static LanguageRules Create() => new(
        StraightQuote: '"',
        CurlyOpenQuote: '»',
        CurlyCloseQuote: '«',
        SpeechVerbs: RuleSets.Set("sagde, siger, svarede, spurgte, råbte, hviskede, udbrød, mumlede, tilføjede, forklarede, sukkede, "
            + "grinede, fortsatte, erklærede, foreslog, tænkte, kaldte, piblede, brølede, snerrede, knurrede, gentog, "
            + "beordrede, anmærkede, kvidrede, sang, jublede, protesterede, indvendte, bemærkede"),
        AdverbSuffix: "ligt",
        MaleTitles: RuleSets.Set("hr, herr, onkel, bedstefar, farfar, morfar, far, kong, prins, bror"),
        FemaleTitles: RuleSets.Set("fru, frøken, tante, bedstemor, farmor, mormor, mor, dronning, prinsesse, søster"),
        TitleAbbreviations: RuleSets.Set("hr, fr, dr, st"),
        MaleNames: RuleSets.Set("peter, jens, lars, anders, søren, mads, niels, hans, jakob, thomas, mikkel, jonas, oliver, william, "
            + "noah, lucas, emil, oscar, victor, magnus, rasmus, morten, henrik, ole, kasper, frederik, christian, mathias, "
            + "tobias, simon, nikolaj, martin, jeppe, carl, karl, otto, ulrik, rolf, poul, knud, svend, torben, bo, tim, jack"),
        FemaleNames: RuleSets.Set("anna, maria, mette, karen, kirsten, hanne, lise, sofie, emma, ida, freja, laura, clara, julie, "
            + "marie, louise, camilla, signe, line, pernille, helle, nanna, astrid, ella, alma, agnes, josefine, maja, sara, "
            + "sarah, cecilie, katrine, rikke, birgitte, gitte, tina, lene, mille, liv, nora, olivia, ellen, thea, rosa"),
        MaleSubjectPronouns: RuleSets.Set("han"),
        FemaleSubjectPronouns: RuleSets.Set("hun"),
        MalePronouns: RuleSets.Set("han"),
        FemalePronouns: RuleSets.Set("hun"),
        NonNameWords: RuleSets.Set("den, det, de, en, et, og, men, eller, så, da, når, som, efter, før, mens, ja, nej, åh, nå, jeg, du, "
            + "han, hun, vi, i, man, dem, os, dig, mig, min, mit, mine, din, dit, dine, hans, hendes, vores, deres, hvad, hvem, "
            + "hvor, hvorfor, hvordan, hvis, at, af, på, til, med, fra, ved, for, om, over, under, her, der, dette, denne, "
            + "disse, engang, pludselig, snart, endelig, goddag, hej, tak, alle, alt, hver, ingen, nogen"),
        IndefinitePronouns: RuleSets.Set("alle, ingen, nogen, enhver, begge, de, vi, man, alt"),
        SentenceTerminators: ".!?…",
        DialogueGapChars: RuleSets.GapChars,
        ExtraOpenQuotes: "„“”«",
        ExtraCloseQuotes: "“”»",
        DialogueDashes: "–—",
        DashTagAfterQuote: true);
}
