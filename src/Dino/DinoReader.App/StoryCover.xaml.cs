using DinoReader.Resources.Strings;

namespace DinoReader;

public partial class StoryCover : ContentView
{
	private const double DefaultShadowDepth = 4;

	/// <summary>How far down the cover's hard shadow falls, in dp: 4, and 5 on a tablet held upright (kids' shelf mock KT1). The gallery sets the page resource <c>TileShadowOffset</c> and the tile binds it here.</summary>
	public static readonly BindableProperty ShadowDepthProperty = BindableProperty.Create(
		nameof(ShadowDepth), typeof(double), typeof(StoryCover), DefaultShadowDepth, propertyChanged: (cover, _, depth) => ((StoryCover)cover).ApplyDepth((double)depth));

	private const int WaitShownAfterMs = 500;
	private IDispatcherTimer? _waitTimer;
	private long _loadStartedMs;

	public StoryCover()
	{
		InitializeComponent();
		CoverPicture.PropertyChanged += OnPictureChanged;
		HandlerChanged += (_, _) =>
		{
			if (Handler is null)
			{
				StopWaitTimer(); // a cover that left the tree must not wake its spinner 500 ms later
			}
		};
	}

	/// <summary>Loads the cover picture again, at the size the cover has now (the platform loader decodes to the view's size; a resized tile would otherwise keep the old, blurred decode).</summary>
	public void ReloadPicture()
	{
		object? context = BindingContext;
		BindingContext = null;
		BindingContext = context;
	}

	private void StopWaitTimer()
	{
		_waitTimer?.Stop();
		_waitTimer = null;
	}

	private void OnPictureChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName != Image.IsLoadingProperty.PropertyName)
		{
			return;
		}

		if (CoverPicture.IsLoading)
		{
			BeginWait();
		}
		else
		{
			EndWait();
		}
	}

	private void BeginWait()
	{
		_loadStartedMs = Environment.TickCount64;
		_waitTimer?.Stop();
		IDispatcherTimer timer = Dispatcher.CreateTimer();
		timer.Interval = TimeSpan.FromMilliseconds(WaitShownAfterMs);
		timer.IsRepeating = false;
		timer.Tick += (_, _) =>
		{
			timer.Stop();
			if (CoverPicture.IsLoading)
			{
				SemanticProperties.SetDescription(CoverWait, AppResources.Cover_Waiting); // what the spinner waits for, read by a screen reader (Slice 382)
				CoverWait.IsVisible = true;
				CoverWait.IsRunning = true;
			}
		};
		_waitTimer = timer;
		timer.Start();
	}

	private void EndWait()
	{
		StopWaitTimer();
		bool waited = CoverWait.IsVisible;
		CoverWait.IsRunning = false;
		CoverWait.IsVisible = false;
		if (CoverPicture.Source is not null)
		{
			LogShown(waited); // a cleared source is not a shown picture
		}
	}

	[System.Diagnostics.Conditional("DEBUG")]
	private void LogShown(bool waited)
	{
#if DEBUG
		string id = (BindingContext as StoryTile)?.Story.Id ?? "?";
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Cover] shown {id} in {Environment.TickCount64 - _loadStartedMs} ms waited={waited}"); // Slice 382: the shelf-reorder flow reads how long a cover took
#endif
	}

	public double ShadowDepth
	{
		get => (double)GetValue(ShadowDepthProperty);
		set => SetValue(ShadowDepthProperty, value);
	}

	private void ApplyDepth(double depth) => CoverShadow.Offset = new Point(0, depth);
}
