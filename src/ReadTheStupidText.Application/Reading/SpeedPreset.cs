using System.Globalization;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>The speed-preset chips on both apps' settings (Slice 164 follow-up): a chip carries its rate as
/// invariant-culture text (a WinUI Tag or MAUI ClassId); this parses it and says which chip is the current one.</summary>
public static class SpeedPreset
{
    private const double MatchTolerance = 0.001;

    public static bool TryParse(string? tag, out PlaybackRate rate)
    {
        rate = PlaybackRate.Default;
        if (!double.TryParse(tag, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            return false;
        }

        rate = new PlaybackRate(value);
        return true;
    }

    public static bool IsCurrent(string? tag, PlaybackRate current) =>
        TryParse(tag, out PlaybackRate rate) && Math.Abs(rate.Value - current.Value) < MatchTolerance;
}
