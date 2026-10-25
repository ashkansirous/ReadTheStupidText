namespace DinoReader.Core;

/// <summary>Dutch: “…” and „…” quotes, dash dialogue, "zei Name" inversion, hij/zij (Slice 105).</summary>
internal static class DutchRules
{
    public static LanguageRules Create() => new(
        StraightQuote: '"',
        CurlyOpenQuote: '“',
        CurlyCloseQuote: '”',
        SpeechVerbs: RuleSets.Set("zei, zeiden, zegt, zeide, antwoordde, antwoordden, antwoordt, vroeg, vroegen, vraagt, riep, riepen, roept, schreeuwde, schreeuwden, fluisterde, fluisterden, "
            + "mompelde, mompelden, vervolgde, verklaarde, zuchtte, zuchtten, lachten, lachte, dacht, gilde, brulde, bromde, snauwde, "
            + "piepte, juichte, herhaalde, beval, protesteerde, kreunde, zong, grinnikte, "
            + "reageerde, snikte, jammerde"),
        AdverbSuffix: "lijk",
        MaleTitles: RuleSets.Set("meneer, mijnheer, dhr, oom, opa, grootvader, papa, vader, koning, prins, broer, heer"),
        FemaleTitles: RuleSets.Set("mevrouw, mevr, mw, juffrouw, tante, oma, grootmoeder, mama, moeder, koningin, prinses, zus, zuster"),
        TitleAbbreviations: RuleSets.Set("dhr, mw, mevr, dr, st"),
        MaleNames: RuleSets.Set("peter, jan, piet, klaas, kees, henk, willem, pieter, johan, sander, daan, sem, lucas, luuk, finn, "
            + "milan, noah, liam, bram, thijs, tim, tom, jeroen, mark, bas, joost, hendrik, gerrit, cor, dirk, maarten, "
            + "jasper, robin, max, jack, victor, hugo, simon, david, daniel, thomas, ruben, jelle"),
        FemaleNames: RuleSets.Set("anna, maria, marie, emma, sophie, julia, lisa, eva, sara, sarah, lotte, fleur, anouk, femke, "
            + "tess, sanne, noor, saar, mila, lieke, iris, nina, laura, katrien, annemarie, marieke, els, ineke, "
            + "willemijn, roos, lena, olivia, ella, nora, liv, floor, joke, truus, griet, rosa"),
        MaleSubjectPronouns: RuleSets.Set("hij"),
        FemaleSubjectPronouns: RuleSets.Set("zij"),
        MalePronouns: RuleSets.Set("hij"),
        FemalePronouns: RuleSets.Set("zij"),
        NonNameWords: RuleSets.Set("de, het, een, en, maar, of, dus, toen, dan, als, na, voor, terwijl, ja, nee, o, oh, nou, ik, jij, "
            + "je, u, hij, zij, ze, wij, we, jullie, men, hen, hun, ons, onze, mij, mijn, jouw, zijn, haar, wat, wie, waar, "
            + "waarom, hoe, dat, dit, die, deze, daar, hier, ineens, plotseling, spoedig, eindelijk, hallo, hoi, dank, "
            + "iedereen, alles, elk, elke, niemand, iemand, er, eens, op, in, aan, bij, met, naar, van, uit, door, om, over"),
        IndefinitePronouns: RuleSets.Set("iedereen, niemand, iemand, ieder, beiden, ze, wij, men, alles"),
        SentenceTerminators: ".!?…",
        DialogueGapChars: RuleSets.GapChars,
        ExtraOpenQuotes: "„",
        ExtraCloseQuotes: "“",
        DialogueDashes: "–—",
        DashTagAfterQuote: true,
        Flavours: FlavourWords.Dutch);
}
