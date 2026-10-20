using ReadTheStupidText.Application.Activity;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Sanitizing;
using ReadTheStupidText.Application.Settings;
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

    private sealed class Harness
    {
        public Func<string, int?, Task> Speak { get; set; } = (_, _) => Task.CompletedTask;

        public FakeReader Reader { get; }

        public FakeClipboard Clipboard { get; } = new();

        public ActivityLog Log { get; } = new();

        public ReadAloudService Service { get; }

        public FakeSelectionMonitor Monitor { get; } = new();

        public Harness()
        {
            Reader = new FakeReader(this);
            Service = new ReadAloudService(
                Reader, Clipboard, new FakeHotkey(), new FakeCopier(), Monitor,
                new FakeClipboardMonitor(), new FakeForeground(), new FakeVoices(), new FakeModel(),
                new FakeDocuments(), Log, new FakeSettings(), new FakeSanitizer(), new FakeSystemLog());
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
        public event EventHandler<string>? SelectionChanged { add { } remove { } }
        public event EventHandler? SelectionCleared;
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

    private sealed class FakeSettings : ISettingsStore
    {
        public PlaybackRate Speed { get; set; } = PlaybackRate.Default;
        public bool AutoReadOnSelection { get; set; }
        public bool AutoReadOnCopy { get; set; }
        public string? VoiceId { get; set; }
        public SanitizerCategory EnabledSanitizers { get; set; }
        public PanelPosition? PanelPosition { get; set; }
        public string? Language { get; set; }
    }

    private sealed class FakeSanitizer : ITextSanitizer
    {
        public string Sanitize(string text) => text;
    }

    private sealed class FakeSystemLog : ISystemLog
    {
        public void Info(string message, int? activityId = null) { }
        public void Debug(string message, int? activityId = null) { }
        public void Warning(string message, int? activityId = null, Exception? error = null) { }
        public void Error(string message, int? activityId = null, Exception? error = null) { }
    }
}
