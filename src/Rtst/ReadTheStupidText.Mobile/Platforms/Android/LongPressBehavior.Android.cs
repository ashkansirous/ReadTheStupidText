using Microsoft.Maui.Controls.Platform;
using AView = Android.Views.View;
using MauiView = Microsoft.Maui.Controls.View;

namespace ReadTheStupidText.Mobile;

// Android: a GestureDetector fed from the native view's Touch event. The handler never marks the event
// handled, so a ScrollView (which does not run View.OnLongClick itself) still scrolls.
public partial class LongPressBehavior : PlatformBehavior<MauiView, AView>
{
    private Android.Views.GestureDetector? _detector;

    protected override void OnAttachedTo(MauiView bindable, AView platformView)
    {
        base.OnAttachedTo(bindable, platformView);
        _detector = new Android.Views.GestureDetector(platformView.Context!, new Listener(() => RaiseLongPressed(bindable)));
        platformView.Touch += OnTouch;
    }

    protected override void OnDetachedFrom(MauiView bindable, AView platformView)
    {
        base.OnDetachedFrom(bindable, platformView);
        platformView.Touch -= OnTouch;
        _detector?.Dispose();
        _detector = null;
    }

    private void OnTouch(object? sender, AView.TouchEventArgs e)
    {
        if (e.Event is not null)
        {
            _detector?.OnTouchEvent(e.Event);
        }

        e.Handled = false;
    }

    private sealed class Listener(Action onLongPress) : Android.Views.GestureDetector.SimpleOnGestureListener
    {
        public override void OnLongPress(Android.Views.MotionEvent? e) => onLongPress();
    }
}
