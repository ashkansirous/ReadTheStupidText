namespace DinoReader.Core;

/// <summary>Finnish: ”…” and »…» quotes, dash dialogue, "sanoi Name" inversion; no gendered pronoun, so gender comes from titles and names (Slice 105).</summary>
internal static class FinnishRules
{
    public static LanguageRules Create() => new(
        StraightQuote: '"',
        CurlyOpenQuote: '”',
        CurlyCloseQuote: '”',
        SpeechVerbs: RuleSets.Set("sanoi, sanoivat, sanoo, vastasi, vastasivat, vastaa, kysyi, kysyy, huusi, huusivat, huutaa, kysyivät, kuiskasivat, nauroivat, kuiskasi, huudahti, mutisi, lisäsi, "
            + "selitti, huokaisi, nauroi, naurahti, jatkoi, julisti, ehdotti, ajatteli, kutsui, murisi, toisti, "
            + "pyysi, käski, huomautti, visersi, lauloi, riemuitsi, vastusti, tokaisi, parahti, älähti, puuskahti, "
            + "ihmetteli, myönsi, kiljaisi"),
        AdverbSuffix: "sti",
        MaleTitles: RuleSets.Set("herra, setä, eno, ukki, isoisä, vaari, isä, isi, kuningas, prinssi, veli, pappa"),
        FemaleTitles: RuleSets.Set("rouva, neiti, täti, mummo, isoäiti, äiti, kuningatar, prinsessa, sisko, mami, mummi"),
        TitleAbbreviations: RuleSets.Set("hra, rva, tri, pyh"),
        MaleNames: RuleSets.Set("pekka, matti, juha, mikko, jussi, antti, timo, jari, kari, ville, eetu, leo, oskari, elias, "
            + "onni, otto, väinö, aatu, lauri, risto, heikki, markku, tapio, aleksi, joonas, nooa, lenni, peter, jack, "
            + "tuomas, jaakko, santeri, ilmari, eino, urho, paavo, kalle, make, hessu, pertti"),
        FemaleNames: RuleSets.Set("maria, anna, liisa, helena, aino, emma, sofia, aada, olivia, ella, amanda, venla, pihla, ilona, "
            + "elsa, sara, sarah, tuuli, kaisa, riitta, leena, tiina, sanna, minna, hanna, katri, kerttu, saara, "
            + "marja, aili, lilja, ida, nea, iida, siiri, lumi, ruusu, hilda, selma"),
        MaleSubjectPronouns: RuleSets.Set(""),
        FemaleSubjectPronouns: RuleSets.Set(""),
        MalePronouns: RuleSets.Set(""),
        FemalePronouns: RuleSets.Set(""),
        NonNameWords: RuleSets.Set("ja, mutta, tai, sitten, kun, niin, se, hän, minä, sinä, me, te, he, ne, ei, kyllä, oi, hei, no, "
            + "tämä, tuo, nämä, nuo, että, jos, mikä, kuka, missä, miksi, miten, kerran, eräänä, äkkiä, pian, vihdoin, "
            + "kiitos, terve, päivää, kaikki, jokainen, kukaan, joku, tässä, tuolla, siellä, täällä, vaan, kuin, "
            + "koska, vaikka, kunnes, ennen, jälkeen"),
        IndefinitePronouns: RuleSets.Set("kaikki, kukaan, joku, jokainen, molemmat, he, me, ne"),
        SentenceTerminators: ".!?…",
        DialogueGapChars: RuleSets.GapChars,
        ExtraOpenQuotes: "»“",
        ExtraCloseQuotes: "»“",
        DialogueDashes: "–—",
        DashTagAfterQuote: true,
        Flavours: FlavourWords.Finnish);
}
