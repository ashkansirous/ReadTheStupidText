using Microsoft.Maui.Controls.Platform;
using UIKit;

namespace ReadTheStupidText.Mobile;

// iOS: a UILongPressGestureRecognizer on the native view. It does not cancel touches, so a scroll view
// under it keeps scrolling.
public partial class LongPressBehavior : PlatformBehavior<View, UIView>
{
    private UILongPressGestureRecognizer? _recognizer;

    protected override void OnAttachedTo(View bindable, UIView platformView)
    {
        base.OnAttachedTo(bindable, platformView);
        _recognizer = new UILongPressGestureRecognizer(gesture =>
        {
            if (gesture.State == UIGestureRecognizerState.Began)
            {
                RaiseLongPressed(bindable);
            }
        })
        {
            CancelsTouchesInView = false,
        };
        platformView.AddGestureRecognizer(_recognizer);
    }

    protected override void OnDetachedFrom(View bindable, UIView platformView)
    {
        base.OnDetachedFrom(bindable, platformView);
        if (_recognizer is not null)
        {
            platformView.RemoveGestureRecognizer(_recognizer);
            _recognizer.Dispose();
            _recognizer = null;
        }
    }
}
