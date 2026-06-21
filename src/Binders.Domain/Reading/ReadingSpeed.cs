namespace Binders.Domain.Reading;

/// <summary>
/// The closed set of reading speeds the user can choose. Each maps to a
/// pitch-corrected playback rate; the speed is the domain concept, the rate
/// is its numeric expression (see <see cref="ReadingSpeedExtensions"/>).
/// </summary>
public enum ReadingSpeed
{
    OneX,
    OneAndQuarterX,
    OneAndHalfX,
    OneAndThreeQuarterX,
    TwoX,
}
