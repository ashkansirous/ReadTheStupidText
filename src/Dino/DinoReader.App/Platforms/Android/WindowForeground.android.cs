using AndroidX.Lifecycle;

namespace DinoReader;

public static partial class WindowForeground
{
	// The current activity is a lifecycle owner (MauiAppCompatActivity is an AppCompatActivity): in front means at least Started; a stopped activity (a lock) is Created.
	public static partial bool? IsInForeground()
	{
		if (Platform.CurrentActivity is not ILifecycleOwner owner)
		{
			return null;
		}

		return owner.Lifecycle.CurrentState.IsAtLeast(Lifecycle.State.Started!);
	}
}
