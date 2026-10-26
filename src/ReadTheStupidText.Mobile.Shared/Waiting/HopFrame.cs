namespace ReadTheStupidText.Mobile.Shared.Waiting;

/// <summary>
/// One moment of the hop in the wait overlay (Slice 143, Batch 30 mock frames 64-65): the keyframes of <c>dinoHop</c> and
/// <c>dinoShadow</c>, 1.1 s a loop, as pure numbers so the animation is easy to check.
/// </summary>
public readonly record struct HopFrame(double Lift, double ScaleX, double ScaleY, double ShadowScaleX, double ShadowOpacity)
{
    public const uint LoopMilliseconds = 1100;

    private const double SquashAt = 0.10;
    private const double ApexAt = 0.45;
    private const double LandingAt = 0.80;
    private const double ApexLift = -34;

    private static readonly (double At, double Lift, double ScaleX, double ScaleY)[] Keys =
    [
        (0, 0, 1, 1),
        (SquashAt, 0, 1.08, 0.9),
        (ApexAt, ApexLift, 0.96, 1.05),
        (LandingAt, 0, 1.06, 0.92),
        (1, 0, 1, 1),
    ];

    private const double ShadowRestScale = 1;
    private const double ShadowRestOpacity = 0.28;
    private const double ShadowApexScale = 0.6;
    private const double ShadowApexOpacity = 0.12;

    /// <summary>The standing dino (also what reduce motion shows).</summary>
    public static HopFrame Rest { get; } = new(0, 1, 1, ShadowRestScale, ShadowRestOpacity);

    /// <summary>The frame at <paramref name="t"/> (0 to 1 through the loop), each stretch eased like the mock's cubic-bezier(.45,0,.55,1).</summary>
    public static HopFrame At(double t)
    {
        t = Math.Clamp(t, 0, 1);
        int i = 1;
        while (i < Keys.Length - 1 && t > Keys[i].At)
        {
            i++;
        }

        var (fromAt, fromLift, fromX, fromY) = Keys[i - 1];
        var (toAt, toLift, toX, toY) = Keys[i];
        double e = Ease((t - fromAt) / (toAt - fromAt));
        return new HopFrame(
            Mix(fromLift, toLift, e), Mix(fromX, toX, e), Mix(fromY, toY, e),
            Mix(ShadowRestScale, ShadowApexScale, Shadow(t)), Mix(ShadowRestOpacity, ShadowApexOpacity, Shadow(t)));
    }

    // How far the shadow is toward its apex state: 0 at the ends, 1 at the apex.
    private static double Shadow(double t) =>
        t <= ApexAt ? Ease(t / ApexAt) : Ease((1 - t) / (1 - ApexAt));

    private static double Ease(double x) => 0.5 - Math.Cos(Math.PI * x) / 2;

    private static double Mix(double from, double to, double amount) => from + (to - from) * amount;
}
