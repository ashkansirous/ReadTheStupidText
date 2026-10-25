namespace DinoReader.Core;

/// <summary>French: « guillemets », dash dialogue, "dit-il" inversion, il/elle.</summary>
internal static class FrenchRules
{
    public static LanguageRules Create() => new(
        StraightQuote: '"',
        CurlyOpenQuote: '«',
        CurlyCloseQuote: '»',
        SpeechVerbs: RuleSets.Set("dit, dis, répondit, répond, demanda, cria, murmura, chuchota, ajouta, reprit, continua, "
            + "expliqua, soupira, rit, s'écria, lança, déclara, annonça, répliqua, gronda, pensa, proposa, suggéra, "
            + "grogna, souffla, hurla, s'exclama, fit, interrogea, s'étonna, appela, geignit"),
        AdverbSuffix: "ment",
        MaleTitles: RuleSets.Set("monsieur, m, papa, oncle, grand-père, roi, prince, seigneur, père, frère, maître, papi, tonton"),
        FemaleTitles: RuleSets.Set("madame, mme, mademoiselle, mlle, maman, tante, grand-mère, reine, princesse, mère, soeur, sœur, mamie, tata"),
        TitleAbbreviations: RuleSets.Set("m, mme, mlle, dr, st"),
        MaleNames: RuleSets.Set("pierre, jean, paul, jacques, louis, lucas, hugo, léo, gabriel, arthur, antoine, thomas, nicolas, "
            + "marc, luc, michel, alain, françois, henri, victor, tom, max, théo, noé, raphaël, jules, adam, ethan, simon, "
            + "mathieu, olivier, charles, philippe, robert, david, daniel, alex, éric, émile, maxime, nathan, baptiste, "
            + "clément, julien, pinocchio, babar, tintin, astérix, obélix"),
        FemaleNames: RuleSets.Set("marie, anne, sophie, julie, emma, léa, lucie, camille, chloé, laura, claire, alice, louise, "
            + "jeanne, manon, sarah, clara, inès, juliette, charlotte, élise, isabelle, sylvie, nathalie, catherine, céline, "
            + "lola, zoé, eva, rose, mia, lina, marion, agnès, hélène, margot, pauline, amélie, élodie, mathilde, adèle, lili, fleur"),
        MaleSubjectPronouns: RuleSets.Set("il"),
        FemaleSubjectPronouns: RuleSets.Set("elle"),
        MalePronouns: RuleSets.Set("il, ils"),
        FemalePronouns: RuleSets.Set("elle, elles"),
        NonNameWords: RuleSets.Set("le, la, les, l, un, une, des, du, de, d, et, mais, ou, donc, or, ni, car, puis, alors, soudain, "
            + "bientôt, enfin, quand, comme, après, avant, pendant, oui, non, oh, eh, ben, bien, je, tu, il, elle, on, nous, "
            + "vous, ils, elles, ce, cette, ces, mon, ma, mes, ton, ta, tes, son, sa, ses, notre, votre, leur, leurs, qui, que, "
            + "quoi, où, pourquoi, comment, si, en, à, au, aux, dans, sur, sous, avec, pour, par, chez, voici, voilà, bonjour, "
            + "salut, merci, tout, tous, toute, toutes, c, j, n, s, qu, y"),
        IndefinitePronouns: RuleSets.Set("tout le monde, personne, quelqu'un, chacun, tous, ils, nous, on, rien"),
        SentenceTerminators: ".!?…",
        DialogueGapChars: RuleSets.GapChars,
        ExtraOpenQuotes: "“",
        ExtraCloseQuotes: "”",
        DialogueDashes: "—–",
        Flavours: FlavourWords.French);
}
