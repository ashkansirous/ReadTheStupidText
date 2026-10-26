namespace ReadTheStupidText.Application.Usage;

/// <summary>Turns a measured number into one of a few coarse buckets, so a count never holds an exact length or time.</summary>
public static class UsageBuckets
{
    private const string OverTwenty = ">20";

    private const string ShortText = "<200";
    private const string MediumText = "200-1000";
    private const string LongText = "1000-5000";
    private const string VeryLongText = ">5000";

    private const int ShortTextLimit = 200;
    private const int MediumTextLimit = 1000;
    private const int LongTextLimit = 5000;

    private static readonly (double Seconds, string Bucket)[] DurationLimits =
    [
        (0.5, "<0.5"),
        (1, "0.5-1"),
        (2, "1-2"),
        (5, "2-5"),
        (10, "5-10"),
        (20, "10-20"),
    ];

    /// <summary>The bucket of <paramref name="elapsed"/>: a limit belongs to the bucket above it (exactly 1 s is "1-2"); over 20 s is ">20".</summary>
    public static string Duration(TimeSpan elapsed)
    {
        foreach ((double seconds, string bucket) in DurationLimits)
        {
            if (elapsed.TotalSeconds < seconds)
            {
                return bucket;
            }
        }

        return OverTwenty;
    }

    /// <summary>The bucket of a text of <paramref name="characters"/> characters.</summary>
    public static string TextLength(int characters) => characters switch
    {
        < ShortTextLimit => ShortText,
        < MediumTextLimit => MediumText,
        < LongTextLimit => LongText,
        _ => VeryLongText,
    };
}
