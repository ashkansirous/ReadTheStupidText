namespace DinoReader.Core;

/// <summary>Portuguese: « » and “ ” quotes, travessão dialogue, ele/ela, and "-a"/"-o" name endings.</summary>
internal static class PortugueseRules
{
    public static LanguageRules Create() => new(
        StraightQuote: '"',
        CurlyOpenQuote: '«',
        CurlyCloseQuote: '»',
        SpeechVerbs: RuleSets.Set("disse, diz, respondeu, perguntou, gritou, sussurrou, exclamou, murmurou, acrescentou, replicou, "
            + "explicou, suspirou, riu, declarou, anunciou, sugeriu, pensou, chamou, berrou, continuou, insistiu, ordenou, "
            + "pediu, protestou, comentou, cantou, avisou, resmungou, afirmou"),
        AdverbSuffix: "mente",
        MaleTitles: RuleSets.Set("senhor, sr, dom, rei, príncipe, tio, avô, pai, papai, irmão, vovô, titio"),
        FemaleTitles: RuleSets.Set("senhora, sra, dona, rainha, princesa, tia, avó, mãe, mamãe, irmã, vovó, titia"),
        TitleAbbreviations: RuleSets.Set("sr, sra, dr, dra"),
        MaleNames: RuleSets.Set("pedro, joão, joao, josé, jose, luís, luis, carlos, miguel, antónio, antonio, manuel, paulo, "
            + "diego, mateus, lucas, tiago, tomás, daniel, rafael, gabriel, hugo, martim, pedrinho, zé, leo, marcos, bruno, "
            + "rui, nuno, ricardo, fernando, felipe, gustavo, henrique, davi, arthur, artur, vitor, victor, saci"),
        FemaleNames: RuleSets.Set("maria, ana, lúcia, lucia, laura, carmen, sofia, isabel, marta, helena, paula, júlia, julia, "
            + "clara, rosa, sara, valentina, camila, daniela, carla, andreia, inês, ines, teresa, alice, beatriz, mariana, "
            + "joana, catarina, leonor, matilde, luana, bia, luísa, luisa, cecília, eva, lara, manuela, emília"),
        MaleSubjectPronouns: RuleSets.Set("ele"),
        FemaleSubjectPronouns: RuleSets.Set("ela"),
        MalePronouns: RuleSets.Set("ele, eles"),
        FemalePronouns: RuleSets.Set("ela, elas"),
        NonNameWords: RuleSets.Set("o, a, os, as, um, uma, uns, umas, e, mas, ou, então, logo, finalmente, quando, como, depois, "
            + "antes, enquanto, sim, não, ah, oh, bem, eu, tu, você, nós, eles, elas, ele, ela, isso, isto, aqui, ali, se, "
            + "cada, com, em, de, para, por, seu, sua, meu, minha, teu, tua, olá, oi, obrigado, obrigada, ao, do, da, no, "
            + "na, sem, até, que, onde, quem, todo, todos"),
        IndefinitePronouns: RuleSets.Set("todos, todo mundo, ninguém, alguém, cada um, ambos, eles, nós, nenhum"),
        SentenceTerminators: ".!?…",
        DialogueGapChars: RuleSets.GapChars,
        ExtraOpenQuotes: "“",
        ExtraCloseQuotes: "”",
        DialogueDashes: "—–",
        MaleNameEndings: RuleSets.List("o"),
        FemaleNameEndings: RuleSets.List("a"),
        Flavours: FlavourWords.Portuguese);
}
