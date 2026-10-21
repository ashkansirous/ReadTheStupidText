using ReadTheStupidText.Application.Activity;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Sanitizing;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Domain.Activity;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Domain.Sanitizing;

namespace ReadTheStupidText.Tests;

public class ReadAloudServiceGeneratingTests
{
    [Fact]
    public async Task SpeakAsync_returning_without_ever_playing_fails_after_the_grace()
    {
        var harness = new Harness { Speak = (_, _) => Task.CompletedTask };
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(50);
        var raised = new List<bool>();
        harness.Service.GeneratingChanged += (_, value) => raised.Add(value);

        await harness.Service.ReadFileAsync("a.txt");

        ActivityEntry entry = Assert.Single(harness.Log.Entries);
        Assert.Equal(ActivityState.Failed, entry.State);
        Assert.Equal(ActivityReason.Error, entry.Reason);
        Assert.False(harness.Service.IsGeneratingAudio);
        Assert.Equal(new[] { true, false }, raised);
    }

    [Fact]
    public async Task Reader_that_returns_early_then_plays_within_the_grace_is_not_failed()
    {
        var harness = new Harness();
        harness.Service.FirstAudioGrace = TimeSpan.FromSeconds(10);
        harness.Speak = (_, _) =>
        {
            _ = Task.Delay(100, TestContext.Current.CancellationToken).ContinueWith(_ => harness.Reader.RaiseState(PlaybackState.Playing));
            return Task.CompletedTask;
        };

        Task read = harness.Service.ReadFileAsync("a.txt");
        await Task.Delay(30, TestContext.Current.CancellationToken);
        Assert.True(harness.Service.IsGeneratingAudio);
        await read;

        Assert.Equal(ActivityState.Reading, Assert.Single(harness.Log.Entries).State);
        Assert.False(harness.Service.IsGeneratingAudio);
    }

    [Fact]
    public async Task Translation_on_speaks_the_translation_and_shows_it_in_the_reading_box()
    {
        var spoken = new List<string>();
        var shown = new List<string>();
        var harness = new Harness(settings => TranslationTestKit.Translator(settings, "bonjour", "fr"))
        {
            Speak = (text, _) => { spoken.Add(text); return Task.CompletedTask; },
        };
        harness.Settings.Language = "en";
        harness.Settings.TranslateEnabled = true;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);
        ReadTranslationResult? finished = null;
        harness.Service.TranslationFinished += (_, result) => finished = result;
        harness.Service.ReadTextChanged += (_, read) => shown.Add(read.Text);

        await harness.Service.ReadFileAsync("a.txt");

        Assert.Equal(new[] { "bonjour" }, spoken);
        Assert.Equal(new[] { "bonjour" }, shown);
        Assert.Equal(TranslationOutcome.Translated, finished?.Outcome);
    }

    [Fact]
    public async Task Translation_is_inert_while_the_engine_is_unavailable()
    {
        var spoken = new List<string>();
        var harness = new Harness(settings => TranslationTestKit.Translator(settings, "bonjour", "fr", available: false))
        {
            Speak = (text, _) => { spoken.Add(text); return Task.CompletedTask; },
        };
        harness.Settings.Language = "en";
        harness.Settings.TranslateEnabled = true;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);

        await harness.Service.ReadFileAsync("a.txt");

        Assert.False(harness.Service.CanTranslate);
        Assert.False(harness.Service.TranslateEnabled);
        Assert.Equal(new[] { "some text to read" }, spoken);
    }

    [Fact]
    public async Task Translation_off_speaks_the_original()
    {
        var spoken = new List<string>();
        var harness = new Harness(settings => TranslationTestKit.Translator(settings, "bonjour", "fr"))
        {
            Speak = (text, _) => { spoken.Add(text); return Task.CompletedTask; },
        };
        harness.Settings.Language = "en";
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);

        await harness.Service.ReadFileAsync("a.txt");

        Assert.Single(spoken);
        Assert.NotEqual("bonjour", spoken[0]);
    }

    [Fact]
    public async Task A_read_superseded_while_translating_never_speaks()
    {
        var gate = new TaskCompletionSource<string>();
        var spoken = new List<string>();
        var harness = new Harness(settings => TranslationTestKit.Translator(settings, gate.Task, "fr"))
        {
            Speak = (text, _) => { spoken.Add(text); return Task.CompletedTask; },
        };
        harness.Settings.Language = "en";
        harness.Settings.TranslateEnabled = true;

        Task read = harness.Service.ReadFileAsync("a.txt");
        harness.Monitor.RaiseCleared();
        gate.SetResult("late translation");
        await read;

        Assert.Empty(spoken);
        Assert.Equal(ActivityState.Interrupted, Assert.Single(harness.Log.Entries).State);
    }

    [Fact]
    public async Task A_missing_translation_pack_reads_the_original_and_says_why()
    {
        var spoken = new List<string>();
        var harness = new Harness(settings => TranslationTestKit.Translator(settings, "x", "fr", modelReady: false))
        {
            Speak = (text, _) => { spoken.Add(text); return Task.CompletedTask; },
        };
        harness.Settings.Language = "en";
        harness.Settings.TranslateEnabled = true;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);
        ReadTranslationResult? finished = null;
        harness.Service.TranslationFinished += (_, result) => finished = result;

        await harness.Service.ReadFileAsync("a.txt");

        Assert.Single(spoken);
        Assert.NotEqual("x", spoken[0]);
        Assert.Equal(TranslationOutcome.ModelMissing, finished?.Outcome);
    }

    [Fact]
    public async Task Deselect_during_generating_raises_false()
    {
        var gate = new TaskCompletionSource();
        var harness = new Harness { Speak = (_, _) => gate.Task };
        var raised = new List<bool>();
        harness.Service.GeneratingChanged += (_, value) => raised.Add(value);

        Task read = harness.Service.ReadFileAsync("a.txt");
        harness.Monitor.RaiseCleared();

        Assert.False(harness.Service.IsGeneratingAudio);
        Assert.Equal(new[] { true, false }, raised);
        Assert.Equal(ActivityState.Interrupted, Assert.Single(harness.Log.Entries).State);

        gate.SetResult();
        await read;
    }

    [Fact]
    public async Task SpeakAsync_throwing_clears_generating_and_fails_the_entry()
    {
        var harness = new Harness { Speak = (_, _) => throw new InvalidOperationException("boom") };
        var raised = new List<bool>();
        harness.Service.GeneratingChanged += (_, value) => raised.Add(value);

        await harness.Service.ReadFileAsync("a.txt");

        Assert.Equal(ActivityState.Failed, Assert.Single(harness.Log.Entries).State);
        Assert.False(harness.Service.IsGeneratingAudio);
        Assert.Equal(new[] { true, false }, raised);
    }

    [Fact]
    public async Task First_Playing_transition_ends_generating_and_moves_the_entry_to_reading()
    {
        var harness = new Harness();
        harness.Speak = (_, _) =>
        {
            harness.Reader.RaiseState(PlaybackState.Playing);
            return Task.CompletedTask;
        };

        await harness.Service.ReadFileAsync("a.txt");

        Assert.False(harness.Service.IsGeneratingAudio);
        Assert.Equal(ActivityState.Reading, Assert.Single(harness.Log.Entries).State);
    }

    [Fact]
    public async Task PlayPauseOrReadAsync_is_a_noop_while_generating()
    {
        var gate = new TaskCompletionSource();
        var harness = new Harness { Speak = (_, _) => gate.Task };

        Task read = harness.Service.ReadFileAsync("a.txt");
        Assert.True(harness.Service.IsGeneratingAudio);

        await harness.Service.PlayPauseOrReadAsync();

        Assert.Single(harness.Log.Entries);
        Assert.Equal(0, harness.Clipboard.Reads);

        gate.SetResult();
        await read;
    }

    [Fact]
    public async Task Auto_read_prepares_the_head_start_before_the_settle_delay_ends()
    {
        var spoken = new TaskCompletionSource();
        var harness = new Harness { Speak = (_, _) => { spoken.TrySetResult(); return Task.CompletedTask; } };
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(10);

        harness.Monitor.RaiseChanged("Some selected text.");

        await harness.HeadStart.FirstPrepare.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(spoken.Task.IsCompleted); // the read waits for its settle; the prepare did not
        Assert.Equal(["Some selected text."], harness.HeadStart.Prepared);
        await spoken.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_drag_burst_prepares_only_the_text_the_selection_settles_on()
    {
        var spoken = new TaskCompletionSource();
        var harness = new Harness { Speak = (_, _) => { spoken.TrySetResult(); return Task.CompletedTask; } };
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(10);

        harness.Monitor.RaiseChanged("Some");
        harness.Monitor.RaiseChanged("Some selected");
        harness.Monitor.RaiseChanged("Some selected text.");

        await spoken.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(["Some selected text."], harness.HeadStart.Prepared);
    }

    [Fact]
    public async Task Deselect_before_the_selection_is_stable_prepares_nothing()
    {
        var harness = new Harness();

        harness.Monitor.RaiseChanged("Some selected text.");
        harness.Monitor.RaiseCleared();
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Empty(harness.HeadStart.Prepared);
    }

    [Fact]
    public void Deselect_cancels_the_head_start_prepare()
    {
        var harness = new Harness();

        harness.Monitor.RaiseCleared();

        Assert.Equal(1, harness.HeadStart.Cancels);
    }

    [Fact]
    public async Task A_failing_head_start_never_affects_the_read()
    {
        var spoken = new TaskCompletionSource();
        var harness = new Harness { Speak = (_, _) => { spoken.TrySetResult(); return Task.CompletedTask; } };
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(10);
        harness.HeadStart.Fail = true;

        harness.Monitor.RaiseChanged("Some selected text.");

        await spoken.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    private sealed class FakeHeadStart : IHeadStart
    {
        private readonly TaskCompletionSource _firstPrepare = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Prepared { get; } = [];
        public Task FirstPrepare => _firstPrepare.Task;
        public int Cancels { get; private set; }
        public bool Fail { get; set; }
        public bool ReadInProgress => false;

        public Task PrepareAsync(string text)
        {
            Prepared.Add(text);
            _firstPrepare.TrySetResult();
            return Fail ? Task.FromException(new InvalidOperationException("boom")) : Task.CompletedTask;
        }

        public void CancelPrepare() => Cancels++;
    }

    private sealed class Harness
    {
        public Func<string, int?, Task> Speak { get; set; } = (_, _) => Task.CompletedTask;

        public FakeReader Reader { get; }

        public FakeClipboard Clipboard { get; } = new();

        public ActivityLog Log { get; } = new();

        public ReadAloudService Service { get; }

        public FakeSelectionMonitor Monitor { get; } = new();

        public FakeHeadStart HeadStart { get; } = new();

        public TestSettings Settings { get; } = new();

        public Harness(Func<TestSettings, ReadTranslator>? translation = null)
        {
            Reader = new FakeReader(this);
            Service = new ReadAloudService(
                Reader, Clipboard, new FakeHotkey(), new FakeCopier(), Monitor,
                new FakeClipboardMonitor(), new FakeForeground(), new FakeVoices(), new FakeModel(),
                new FakeDocuments(), Log, Settings, new FakeSanitizer(), new SilentLog(), HeadStart, translation?.Invoke(Settings));
        }
    }

    private sealed class FakeReader(Harness harness) : ISpeechReader
    {
        public event EventHandler<PlaybackState>? StateChanged;
        public event EventHandler? Completed { add { } remove { } }
        public event EventHandler<double>? ProgressChanged { add { } remove { } }
        public event EventHandler<ReadTiming>? TimingChanged { add { } remove { } }
        public event EventHandler<ReadChunk>? ChunkChanged { add { } remove { } }

        public PlaybackState State { get; private set; } = PlaybackState.Idle;

        public void RaiseState(PlaybackState state)
        {
            State = state;
            StateChanged?.Invoke(this, state);
        }

        public Task SpeakAsync(string text, int? activityId = null) => harness.Speak(text, activityId);
        public Task WarmUpAsync() => Task.CompletedTask;
        public void Pause() { }
        public void Resume() { }
        public void Stop() { }
        public void SetSpeed(PlaybackRate speed) { }
        public void SetVoice(string voiceId) { }
        public Task SkipForward() => Task.CompletedTask;
        public Task SkipBackward() => Task.CompletedTask;
    }

    private sealed class FakeClipboard : IClipboardReader
    {
        public int Reads { get; private set; }

        public Task<string?> GetTextAsync()
        {
            Reads++;
            return Task.FromResult<string?>("clipboard text");
        }
    }

    private sealed class FakeHotkey : IHotkeyService
    {
        public event EventHandler? Pressed { add { } remove { } }
        public bool Register(nint windowHandle) => true;
        public void Unregister() { }
        public void Dispose() { }
    }

    private sealed class FakeCopier : ISelectionCopier
    {
        public Task CopyAsync() => Task.CompletedTask;
    }

    private sealed class FakeSelectionMonitor : ISelectionMonitor
    {
        public event EventHandler<string>? SelectionChanged;
        public event EventHandler? SelectionCleared;
        public void RaiseChanged(string text) => SelectionChanged?.Invoke(this, text);
        public void RaiseCleared() => SelectionCleared?.Invoke(this, EventArgs.Empty);
        public bool IsRunning => false;
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class FakeClipboardMonitor : IClipboardMonitor
    {
        public event EventHandler? ContentChanged { add { } remove { } }
        public bool IsRegistered => false;
        public void Register(nint windowHandle) { }
        public void Unregister() { }
        public void Dispose() { }
    }

    private sealed class FakeForeground : IForegroundWindow
    {
        public WindowSource? Capture() => null;
    }

    private sealed class FakeVoices : IVoiceCatalog
    {
        public IReadOnlyList<VoiceInfo> InstalledVoices => [];
        public VoiceInfo? DefaultVoice => null;
    }

    private sealed class FakeModel : IVoiceModelService
    {
        public bool IsReady => false;
        public VoiceModelPaths? Paths => null;
        public event EventHandler? ReadyChanged { add { } remove { } }
        public Task InitializeAsync(IProgress<double>? progress = null) => Task.CompletedTask;
    }

    private sealed class FakeDocuments : IDocumentTextExtractor
    {
        public bool CanHandle(string extension) => true;
        public Task<string> ExtractTextAsync(string filePath) => Task.FromResult("some text to read");
    }

    private sealed class FakeSanitizer : ITextSanitizer
    {
        public string Sanitize(string text) => text;
    }
}
