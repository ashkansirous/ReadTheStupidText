using Microsoft.Maui.Controls.Shapes;

namespace ReadTheStupidText.Mobile.Shared.Waiting;

/// <summary>
/// An image that hops with a squash and a shadow (Slice 143, Batch 30 mock): a 130x130 box, content bottom-aligned, a 96x120
/// column holding the picture (84x84, radius 22, pivot at its feet) over a 64x10 shadow. Driven by MAUI's own
/// <see cref="Animation"/> (no Lottie), one loop of <see cref="HopFrame.LoopMilliseconds"/>. Stands still when the OS asks
/// for reduced motion.
/// </summary>
public sealed class HoppingImage : Grid, IWaitAnimation
{
    private const string AnimationName = "WaitHop";
    private const double BoxSize = 130;
    private const double ColumnWidth = 96;
    private const double ColumnHeight = 120;
    private const double PictureSize = 84;
    private const double PictureRadius = 22;
    private const double ShadowWidth = 64;
    private const double ShadowHeight = 10;
    private const double ShadowGap = 6;
    private const uint FrameMilliseconds = 16;

    private readonly Border _picture;
    private readonly BoxView _shadow;

    public HoppingImage(ImageSource source, Color shadowColor)
    {
        WidthRequest = BoxSize;
        HeightRequest = BoxSize;
        _picture = new Border
        {
            WidthRequest = PictureSize, HeightRequest = PictureSize, StrokeThickness = 0, AnchorX = 0.5, AnchorY = 1,
            StrokeShape = new RoundRectangle { CornerRadius = PictureRadius },
            Content = new Image { Source = source, Aspect = Aspect.AspectFill },
        };
        _shadow = new BoxView
        {
            WidthRequest = ShadowWidth, HeightRequest = ShadowHeight, CornerRadius = ShadowHeight / 2, Color = shadowColor,
            HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, ShadowGap, 0, 0),
        };
        var column = new VerticalStackLayout
        {
            WidthRequest = ColumnWidth, HeightRequest = ColumnHeight, VerticalOptions = LayoutOptions.End,
            HorizontalOptions = LayoutOptions.Center, Children = { _picture, _shadow },
        };
        Children.Add(column);
        InputTransparent = true;
        Apply(HopFrame.Rest);
    }

    public void Start()
    {
        Stop();
        if (ReduceMotion.IsOn)
        {
            return;
        }

        var loop = new Animation(t => Apply(HopFrame.At(t)));
        loop.Commit(this, AnimationName, FrameMilliseconds, HopFrame.LoopMilliseconds, Easing.Linear, repeat: () => true);
    }

    public void Stop()
    {
        this.AbortAnimation(AnimationName);
        Apply(HopFrame.Rest);
    }

    private void Apply(HopFrame frame)
    {
        _picture.TranslationY = frame.Lift;
        _picture.ScaleX = frame.ScaleX;
        _picture.ScaleY = frame.ScaleY;
        _shadow.ScaleX = frame.ShadowScaleX;
        _shadow.Opacity = frame.ShadowOpacity;
    }
}
