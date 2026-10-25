namespace DinoReader.Core;

/// <summary>The English rule tables behind <see cref="LanguageRules.English"/>.</summary>
internal static class EnglishRules
{
    private const int DialogueGapChars = 160;

    public static LanguageRules Create() => new(
        StraightQuote: '"',
        CurlyOpenQuote: '“',
        CurlyCloseQuote: '”',
        SpeechVerbs: Set(
            "said", "says", "replied", "asked", "shouted", "whispered", "cried", "called",
            "answered", "yelled", "exclaimed", "murmured", "muttered", "laughed", "sighed",
            "added", "continued", "declared", "suggested", "announced", "responded",
            "gasped", "giggled", "cheered", "screamed", "squeaked", "sang", "explained",
            "wondered", "thought", "snapped", "grumbled", "demanded", "begged", "urged"),
        AdverbSuffix: "ly",
        MaleTitles: Set("mr", "sir", "king", "prince", "lord", "uncle", "grandpa", "grandfather",
            "father", "papa", "daddy", "brother"),
        FemaleTitles: Set("mrs", "ms", "miss", "lady", "queen", "princess", "aunt", "auntie",
            "grandma", "grandmother", "mother", "mama", "mommy", "mummy", "sister"),
        TitleAbbreviations: Set("mr", "mrs", "ms", "dr", "st"),
        MaleNames: Set("peter", "tom", "tim", "jack", "john", "james", "william", "henry", "george",
            "charlie", "harry", "oliver", "max", "sam", "ben", "joe", "luke", "adam", "alex",
            "daniel", "david", "edward", "frank", "fred", "jim", "leo", "matt", "michael",
            "nick", "paul", "robert", "ron", "simon", "steve", "ted", "thomas", "victor",
            "walter", "arthur", "oscar", "felix", "hugo", "noah", "ethan", "liam", "jake",
            "ralph", "billy", "bob", "dan", "dave", "eric", "gus", "ike", "kevin", "larry",
            "mark", "neil", "pete", "sid", "tony", "wally", "benjamin"),
        FemaleNames: Set("mary", "anna", "emma", "lucy", "alice", "lily", "sarah", "jane", "kate",
            "emily", "sophie", "sophia", "olivia", "ella", "amy", "grace", "molly", "polly",
            "susan", "rose", "ruby", "daisy", "eva", "eve", "fiona", "hannah", "helen", "holly",
            "isabel", "julia", "laura", "lola", "maggie", "mia", "nancy", "nina", "rachel",
            "rosie", "sally", "sara", "tina", "wendy", "zoe", "ava", "betty", "clara",
            "dorothy", "ellie", "flora", "gemma", "ivy", "jenny", "katie", "lottie", "maria",
            "penny", "peggy", "suzy", "victoria", "flopsy", "mopsy", "cottontail"),
        MaleSubjectPronouns: Set("he"),
        FemaleSubjectPronouns: Set("she"),
        MalePronouns: Set("he", "him", "his", "himself"),
        FemalePronouns: Set("she", "her", "hers", "herself"),
        NonNameWords: Set("the", "a", "an", "then", "but", "and", "so", "suddenly", "soon",
            "finally", "just", "now", "when", "as", "after", "before", "while", "once", "later",
            "meanwhile", "yes", "no", "oh", "well", "i", "it", "they", "we", "you", "what",
            "where", "why", "how", "who", "this", "that", "there", "here", "if", "one", "every",
            "each", "with", "at", "in", "on", "to", "from", "by", "of", "for", "his", "her",
            "their", "our", "my", "your", "its", "all", "some", "no", "not", "oh", "hello", "hi"),
        IndefinitePronouns: Set("everyone", "everybody", "everything", "nobody", "no one", "someone",
            "somebody", "anyone", "anybody", "all", "both", "they", "we", "none", "others"),
        SentenceTerminators: ".!?…",
        DialogueGapChars: DialogueGapChars,
        Flavours: FlavourWords.English);

    private static HashSet<string> Set(params string[] items) =>
        new(items, StringComparer.OrdinalIgnoreCase);
}
