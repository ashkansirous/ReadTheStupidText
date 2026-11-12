namespace ReadTheStupidText.Mobile.Shared.Usage;

/// <summary>
/// Slice 352: how a full usage queue makes room. Pure. When the queue is over its cap the oldest rows the app marks as noise (taps, screens, setting changes) go first, so an offline stretch of button presses
/// cannot push out the rows that matter (first run, intro steps, book opened); only when no noise is left does the oldest row of any kind go.
/// </summary>
public static class UsageQueueTrim
{
    /// <summary>Removes rows from the front of <paramref name="queue"/> until it holds at most <paramref name="max"/>: oldest noise first, then oldest of the rest. Keeps the order of what stays.</summary>
    public static void ToCap<TEvent>(List<TEvent> queue, int max, Func<TEvent, bool>? isNoise)
    {
        int excess = queue.Count - max;
        if (excess <= 0)
        {
            return;
        }

        if (isNoise is not null)
        {
            excess -= RemoveOldestNoise(queue, excess, isNoise);
        }

        if (excess > 0)
        {
            queue.RemoveRange(0, excess);
        }
    }

    private static int RemoveOldestNoise<TEvent>(List<TEvent> queue, int excess, Func<TEvent, bool> isNoise)
    {
        int removed = 0;
        for (int i = 0; i < queue.Count && removed < excess;)
        {
            if (isNoise(queue[i]))
            {
                queue.RemoveAt(i);
                removed++;
            }
            else
            {
                i++;
            }
        }

        return removed;
    }
}
