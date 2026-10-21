namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// How many operations (the original read, plus any Skip that continues its playback) still own a
/// read. The read counts as over only when the last owner leaves, so a Skip-continued playback
/// keeps the read alive after the original read's call has returned. Starts at one owner: the
/// original read.
/// </summary>
public sealed class ReadOwnership
{
    private int _owners = 1;

    /// <summary>Adds an owner. False if the last owner already left: the read is over.</summary>
    public bool TryJoin()
    {
        int current;
        do
        {
            current = Volatile.Read(ref _owners);
            if (current <= 0)
            {
                return false;
            }
        }
        while (Interlocked.CompareExchange(ref _owners, current + 1, current) != current);

        return true;
    }

    /// <summary>Drops an owner. True when this was the last one.</summary>
    public bool Leave() => Interlocked.Decrement(ref _owners) == 0;
}
