using System.Diagnostics;
using AVFoundation;
using Foundation;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile;

// The playback half of the iOS neural reader: one AVAudioPlayer per chunk, pause/resume, the
// audio-session interruptions and the poll loop that reports progress and timing.
public sealed partial class IosSupertonicSpeechReader
{
    // Plays one chunk's WAV file and awaits its natural end (true) or a stop/supersede (false).
    // The previous chunk's temp file is deleted once this one has taken over the player (it's no
    // longer open by then).
    private async Task<bool> PlayChunkAsync(string wavPath, ReadChunk readChunk, CancellationToken token)
    {
        var ended = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _chunkEnded = ended;

        await MainThread.InvokeOnMainThreadAsync(() => StartPlayer(wavPath, readChunk, ended, token));

        using (token.Register(() => ended.TrySetResult(false)))
        {
            return await ended.Task;
        }
    }

    // Runs on the main thread. Replaces the previous chunk's player, then starts this chunk. A
    // chunk that cannot be decoded is skipped (treated as ended) rather than stalling the read.
    private void StartPlayer(string wavPath, ReadChunk readChunk, TaskCompletionSource<bool> ended, CancellationToken token)
    {
        ReleasePlayer();
        _unannouncedChunk = null;
        DeletePreviousChunkFile();
        _previousChunkFile = _currentChunkFile;
        _currentChunkFile = wavPath;
        if (token.IsCancellationRequested)
        {
            return;
        }

        AVAudioPlayer? player = CreatePlayer(wavPath, ended);
        if (player is null)
        {
            ended.TrySetResult(true);
            return;
        }

        _player = player;
        _playerFinished = false;
        Volatile.Write(ref _positionTicks, 0);
        Volatile.Write(ref _durationTicks, TimeSpan.FromSeconds(player.Duration).Ticks);

        if (!BeginPlaying(player, readChunk))
        {
            ReleasePlayer();
            ended.TrySetResult(true);
            return;
        }

        StartPolling(token);
    }

    private AVAudioPlayer? CreatePlayer(string wavPath, TaskCompletionSource<bool> ended)
    {
        using var url = NSUrl.FromFilename(wavPath);
        AVAudioPlayer? player = AVAudioPlayer.FromUrl(url, out NSError? error);
        if (player is null || error is not null)
        {
            return null;
        }

        player.EnableRate = true;
        player.Rate = (float)_playbackRate;
        player.FinishedPlaying += (_, _) =>
        {
            if (ReferenceEquals(_player, player))
            {
                _playerFinished = true;
            }

            ended.TrySetResult(true);
        };
        player.PrepareToPlay();
        return player;
    }

    // Paused (by the user or an interruption) between chunks: the next chunk is prepared but
    // stays silent, and the interruption flag is kept; Resume plays it. False when the player
    // refuses to start.
    private bool BeginPlaying(AVAudioPlayer player, ReadChunk readChunk)
    {
        if (_state == PlaybackState.Paused)
        {
            _unannouncedChunk = readChunk;
            return true;
        }

        if (!player.Play())
        {
            return false;
        }

        _pausedByInterruption = false;
        AnnounceChunk(readChunk);
        UpdateState(PlaybackState.Playing);
        return true;
    }

    private void AnnounceChunk(ReadChunk readChunk)
    {
        _unannouncedChunk = null;
        LogFirstAudio();
        ChunkChanged?.Invoke(this, readChunk);
    }

    private void LogFirstAudio()
    {
        if (Interlocked.Exchange(ref _firstAudioPending, 0) == 1)
        {
            _log.Debug($"first audio after {Stopwatch.GetElapsedTime(_readStartTicks).TotalMilliseconds:F0} ms");
        }
    }

    public void Pause() => MainThread.BeginInvokeOnMainThread(PauseNow);

    // Main thread only.
    private void PauseNow()
    {
        if (_state != PlaybackState.Playing)
        {
            return;
        }

        _player?.Pause();
        UpdateState(PlaybackState.Paused);
    }

    public void Resume() => MainThread.BeginInvokeOnMainThread(ResumeNow);

    // Main thread only. A player that refuses to play leaves the state Paused. If the paused chunk
    // had already finished, replaying it would repeat audio: just flip the state, and the next
    // StartPlayer (state no longer Paused) plays the next chunk.
    private void ResumeNow()
    {
        if (_state != PlaybackState.Paused)
        {
            return;
        }

        if (_playerFinished || _player is null)
        {
            _pausedByInterruption = false;
            UpdateState(PlaybackState.Playing);
            return;
        }

        if (_player.Play())
        {
            _pausedByInterruption = false;
            if (_unannouncedChunk is { } waiting)
            {
                AnnounceChunk(waiting);
            }

            UpdateState(PlaybackState.Playing);
        }
    }

    // The audio session reports a call, an alarm or Siri taking the audio: pause, and carry on
    // when the system says it is fine to (a call ending). A pause the user made is never undone.
    private void OnInterruptionBegan()
    {
        if (_state == PlaybackState.Playing)
        {
            PauseNow();
            _pausedByInterruption = _state == PlaybackState.Paused;
        }
    }

    private void OnInterruptionEnded(bool shouldResume)
    {
        if (_pausedByInterruption && shouldResume && _state == PlaybackState.Paused && IosAudioSession.Activate(this))
        {
            ResumeNow();
        }
    }

    // Main thread only. Stops and frees the current chunk's player.
    private void ReleasePlayer()
    {
        if (_player is { } player)
        {
            player.Stop();
            player.Dispose();
            _player = null;
            _playerFinished = false;
        }
    }

    private void StartPolling(CancellationToken stopToken)
    {
        StopPolling();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
        _pollCts = cts;
        _ = PollLoopAsync(cts.Token);
    }

    private void StopPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(PollInterval, token);
                await MainThread.InvokeOnMainThreadAsync(SamplePosition);
                ReportProgress();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on stop/skip/supersede.
        }
    }

    // Main thread only: the one place the native player's position is read.
    private void SamplePosition()
    {
        if (_player is { } player)
        {
            Volatile.Write(ref _positionTicks, TimeSpan.FromSeconds(player.CurrentTime).Ticks);
            Volatile.Write(ref _durationTicks, TimeSpan.FromSeconds(player.Duration).Ticks);
        }
    }

    private void ReportProgress()
    {
        int chunkCount = Volatile.Read(ref _chunkCount);
        int chunkIndex = Volatile.Read(ref _currentChunkIndex);
        if (chunkCount == 0)
        {
            return;
        }

        long duration = Volatile.Read(ref _durationTicks);
        double withinChunk = duration > 0 ? (double)Volatile.Read(ref _positionTicks) / duration : 0;
        double fraction = (chunkIndex + Math.Clamp(withinChunk, 0, 1)) / chunkCount;
        ProgressChanged?.Invoke(this, Math.Clamp(fraction, 0, 1));

        if (Stopwatch.GetElapsedTime(_lastTimingRaiseTicks) >= TimingRaiseInterval)
        {
            RaiseTiming(CurrentChunkPosition());
        }
    }

    private TimeSpan CurrentChunkPosition() =>
        Volatile.Read(ref _durationTicks) > 0 ? TimeSpan.FromTicks(Volatile.Read(ref _positionTicks)) : TimeSpan.Zero;
}
