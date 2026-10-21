namespace DinoReader.Core;

/// <summary>Spanish: « » and “ ” quotes, raya dialogue, él/ella, and "-a"/"-o" name endings.</summary>
internal static class SpanishRules
{
    public static LanguageRules Create() => new(
        StraightQuote: '"',
        CurlyOpenQuote: '«',
        CurlyCloseQuote: '»',
        SpeechVerbs: RuleSets.Set("dijo, dice, respondió, preguntó, gritó, susurró, exclamó, contestó, añadió, murmuró, replicó, "
            + "explicó, suspiró, rió, rio, declaró, anunció, sugirió, pensó, llamó, chilló, canturreó, musitó, observó, "
            + "continuó, insistió, ordenó, pidió, protestó, aseguró, comentó, cantó, advirtió, gruñó, balbuceó"),
        AdverbSuffix: "mente",
        MaleTitles: RuleSets.Set("señor, don, sr, rey, príncipe, tío, abuelo, padre, papá, hermano, papa, abuelito"),
        FemaleTitles: RuleSets.Set("señora, doña, sra, señorita, srta, reina, princesa, tía, abuela, madre, mamá, hermana, mami, abuelita"),
        TitleAbbreviations: RuleSets.Set("sr, sra, srta, dr, dra"),
        MaleNames: RuleSets.Set("pedro, juan, luis, carlos, josé, jose, miguel, antonio, manuel, pablo, diego, mateo, lucas, "
            + "santiago, tomás, daniel, javier, alejandro, andrés, fernando, rafael, sergio, jorge, hugo, martín, pepe, paco, "
            + "nacho, leo, marcos, álvaro, adrián, david, óscar, ramón, enrique, felipe, gabriel, sebastián, emilio, quijote, sancho"),
        FemaleNames: RuleSets.Set("maría, ana, lucía, laura, carmen, sofía, isabel, marta, elena, paula, julia, clara, rosa, sara, "
            + "valentina, camila, daniela, carla, andrea, inés, pilar, teresa, rocío, alicia, lola, pepa, mía, emma, luna, "
            + "flor, ximena, gabriela, patricia, silvia, beatriz, cristina, olivia, irene"),
        MaleSubjectPronouns: RuleSets.Set("él"),
        FemaleSubjectPronouns: RuleSets.Set("ella"),
        MalePronouns: RuleSets.Set("él, ellos"),
        FemalePronouns: RuleSets.Set("ella, ellas"),
        NonNameWords: RuleSets.Set("el, la, los, las, un, una, unos, unas, y, pero, o, entonces, pronto, cuando, como, después, "
            + "antes, mientras, luego, sí, no, oh, bueno, yo, tú, usted, nosotros, vosotros, ellos, ellas, él, ella, lo, qué, "
            + "dónde, cómo, quién, este, esta, eso, esto, aquí, allí, si, uno, cada, con, en, a, de, desde, por, para, su, sus, "
            + "mi, mis, tu, tus, nuestro, todo, todos, hola, buenos, buenas, gracias, al, del, sin, hasta"),
        IndefinitePronouns: RuleSets.Set("todos, todo el mundo, nadie, alguien, cada uno, ambos, ellos, nosotros, ninguno"),
        SentenceTerminators: ".!?…",
        DialogueGapChars: RuleSets.GapChars,
        ExtraOpenQuotes: "“",
        ExtraCloseQuotes: "”",
        DialogueDashes: "—―–",
        MaleNameEndings: RuleSets.List("o"),
        FemaleNameEndings: RuleSets.List("a"));
}
