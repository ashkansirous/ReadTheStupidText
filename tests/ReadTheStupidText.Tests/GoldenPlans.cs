namespace ReadTheStupidText.Tests;

// Recorded from the chunker before Slice 139 touched it (no hint): the Windows and Dino plans.
internal static class GoldenPlans
{
    public static readonly string[][] Default =
    [
        ["Short text."],
        ["It was a bright cold day in April, and the clocks were striking thirteen.",
            "Winston Smith slipped quickly through the glass doors. The quick brown fox jumps over the lazy dog again and again until the evening comes.",
            "The quick brown fox jumps over the lazy dog again and again until the evening comes."],
        ["Mr. Smith went to Washington.",
            "He met a man there. The quick brown fox jumps over the lazy dog again and again until the evening comes. The quick brown fox jumps over the lazy dog again and again until the evening comes."],
        ["Chapter 1",
            "It was a bright cold day in April. The quick brown fox jumps over the lazy dog again and again until the evening comes. The quick brown fox jumps over the lazy dog again and again until the evening comes."],
        ["Hello there my friend, this is one very long first sentence that keeps going and going without any stop at all until it passes the two hundred character limit for sure, yes indeed it does. Next one."],
        ["今天天气非常好，我们一起去公园玩耍。我们在公园里开心地跑步和唱歌。然后我们一起回家吃好吃的晚饭，大家都很开心。明天我们还要去海边看日出，然后在沙滩上玩一整天，直到太阳下山。"],
    ];

    public static readonly string[][] Cached =
    [
        ["Short text."],
        ["It was a bright cold day in April, and the clocks were striking thirteen. Winston Smith slipped quickly through the glass doors.",
            "The quick brown fox jumps over the lazy dog again and again until the evening comes.",
            "The quick brown fox jumps over the lazy dog again and again until the evening comes."],
        ["Mr. Smith went to Washington.",
            "He met a man there. The quick brown fox jumps over the lazy dog again and again until the evening comes. The quick brown fox jumps over the lazy dog again and again until the evening comes."],
        ["Chapter 1",
            "It was a bright cold day in April. The quick brown fox jumps over the lazy dog again and again until the evening comes. The quick brown fox jumps over the lazy dog again and again until the evening comes."],
        ["Hello there my friend, this is one very long first sentence that keeps going and going without any stop at all until it passes the two hundred character limit for sure, yes indeed it does. Next one."],
        ["今天天气非常好，我们一起去公园玩耍。我们在公园里开心地跑步和唱歌。然后我们一起回家吃好吃的晚饭，大家都很开心。明天我们还要去海边看日出，然后在沙滩上玩一整天，直到太阳下山。"],
    ];

    public static readonly string[][] Unbiased =
    [
        ["Short text."],
        ["It was a bright cold day in April, and the clocks were striking thirteen. Winston Smith slipped quickly through the glass doors. The quick brown fox jumps over the lazy dog again and again until the evening comes.",
            "The quick brown fox jumps over the lazy dog again and again until the evening comes."],
        ["Mr. Smith went to Washington. He met a man there. The quick brown fox jumps over the lazy dog again and again until the evening comes. The quick brown fox jumps over the lazy dog again and again until the evening comes."],
        ["Chapter 1",
            "It was a bright cold day in April. The quick brown fox jumps over the lazy dog again and again until the evening comes. The quick brown fox jumps over the lazy dog again and again until the evening comes."],
        ["Hello there my friend, this is one very long first sentence that keeps going and going without any stop at all until it passes the two hundred character limit for sure, yes indeed it does. Next one."],
        ["今天天气非常好，我们一起去公园玩耍。我们在公园里开心地跑步和唱歌。然后我们一起回家吃好吃的晚饭，大家都很开心。明天我们还要去海边看日出，然后在沙滩上玩一整天，直到太阳下山。"],
    ];

}
