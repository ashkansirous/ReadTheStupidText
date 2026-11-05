namespace DinoReader.Tests;

// Slice 270: a fake native player throws if it is disposed while its own finished call runs, like AVAudioPlayer does on iOS.
public sealed class PlayerHandOffTests
{
	private sealed class FakePlayer
	{
		public bool InCallback;
		public bool Disposed;

		public void Dispose()
		{
			if (InCallback)
			{
				throw new ObjectDisposedException("the player was disposed during the callback");
			}

			Disposed = true;
		}
	}

	private readonly Queue<Action> _loop = new();
	private readonly PlayerHandOff _handOff = new();

	private void Post(Action action) => _loop.Enqueue(action);

	private void RunLoop()
	{
		while (_loop.Count > 0)
		{
			_loop.Dequeue()();
		}
	}

	private Action Defer(FakePlayer player, Action callback) => _handOff.Defer(_handOff.Begin(), callback, Post, player.Dispose);

	[Fact]
	public void The_next_clip_starts_after_the_native_call_returned_and_nothing_throws()
	{
		var first = new FakePlayer();
		var played = new List<string>();
		Action firstEnded = Defer(first, () => played.Add("1 ended"));

		first.InCallback = true;
		firstEnded(); // the native delegate call: only queues
		first.InCallback = false;
		RunLoop();

		Assert.True(first.Disposed);
		Assert.Equal(["1 ended"], played);
	}

	[Fact]
	public void The_callback_may_start_the_next_play_which_stops_the_old_one()
	{
		var first = new FakePlayer();
		var second = new FakePlayer();
		var events = new List<string>();
		Action? secondEnded = null;
		Action firstEnded = _handOff.Defer(_handOff.Begin(), () =>
		{
			events.Add(first.Disposed ? "callback after release" : "callback before release");
			_handOff.Stop();
			secondEnded = Defer(second, () => events.Add("2 ended"));
		}, Post, () => { first.Dispose(); events.Add("release"); });

		first.InCallback = true;
		firstEnded();
		first.InCallback = false;
		Assert.Empty(events);
		RunLoop();
		secondEnded!();
		RunLoop();

		Assert.Equal(["release", "callback after release", "2 ended"], events);
	}

	[Fact]
	public void A_finished_clip_is_marked_finishing_at_once_and_a_new_play_clears_it()
	{
		var player = new FakePlayer();
		Action ended = Defer(player, () => { });
		Assert.False(_handOff.IsFinishing);

		ended();
		Assert.True(_handOff.IsFinishing);

		_handOff.Begin();
		Assert.False(_handOff.IsFinishing);
	}

	[Fact]
	public void A_stale_callback_does_not_mark_the_current_play_finishing()
	{
		var player = new FakePlayer();
		Action stale = Defer(player, () => { });
		_handOff.Begin();

		stale();

		Assert.False(_handOff.IsFinishing);
	}

	[Fact]
	public void A_pause_after_the_clip_finished_does_not_touch_the_player()
	{
		var player = new FakePlayer();
		Action ended = Defer(player, () => { });
		int pauses = 0;

		_handOff.RunUnlessFinishing(() => pauses++);
		ended();
		_handOff.RunUnlessFinishing(() => pauses++);
		RunLoop();
		_handOff.RunUnlessFinishing(() => pauses++);

		Assert.Equal(1, pauses);
	}

	[Fact]
	public void A_failing_release_is_logged_and_the_callback_still_runs()
	{
		Exception? logged = null;
		var handOff = new PlayerHandOff(ex => logged = ex);
		bool ran = false;
		Action ended = handOff.Defer(handOff.Begin(), () => ran = true, Post, () => throw new ObjectDisposedException("release"));

		ended();
		RunLoop();

		Assert.True(ran);
		Assert.IsType<ObjectDisposedException>(logged);
	}

	[Fact]
	public void A_stop_before_the_queued_callback_runs_drops_it()
	{
		var player = new FakePlayer();
		bool ran = false;
		Action ended = Defer(player, () => ran = true);

		ended();
		_handOff.Stop();
		RunLoop();

		Assert.False(ran);
		Assert.False(player.Disposed);
	}

	[Fact]
	public void A_newer_play_drops_the_older_plays_callback()
	{
		var first = new FakePlayer();
		bool ran = false;
		Action ended = Defer(first, () => ran = true);
		_handOff.Begin();

		ended();
		RunLoop();

		Assert.False(ran);
	}

	[Fact]
	public void A_failed_finish_after_a_decoder_error_runs_only_once()
	{
		var player = new FakePlayer();
		int failures = 0;
		int generation = _handOff.Begin();
		Action failed = _handOff.Defer(generation, () => failures++, Post, player.Dispose);

		failed();
		failed();
		RunLoop();

		Assert.Equal(1, failures);
	}
}
