using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>
/// The voice sample a grown-up page plays (cast screen, Narrator page). It stops its own sample on leaving, but only while that sample is still
/// the reader's current read: once it ended or stopped (after it was heard playing), leaving stops nothing else.
/// </summary>
internal sealed class VoicePreview(ISpeechReader reader)
{
	private bool _previewing;
	private bool _previewHeard;

	public void Attach()
	{
		reader.StateChanged += OnReaderStateChanged;
		reader.Completed += OnReaderCompleted;
	}

	public void Detach()
	{
		Stop();
		reader.StateChanged -= OnReaderStateChanged;
		reader.Completed -= OnReaderCompleted;
	}

	/// <summary>Plays <paramref name="line"/> in <paramref name="voiceId"/> shaped by <paramref name="flavour"/>, replacing any sample still playing. Throws when the read fails to start.</summary>
	public async Task PlayAsync(string line, string voiceId, VoiceFlavour flavour = VoiceFlavour.Normal)
	{
		Stop();
		var segment = new VoicedSegment(line, voiceId, 0, line.Length, 0, FlavourTransform.For(flavour));
		try
		{
			_previewing = true;
			_previewHeard = false;
			await (reader.SupportsSegments ? reader.SpeakSegmentsAsync([segment]) : reader.SpeakAsync(line));
		}
		catch
		{
			_previewing = false;
			throw;
		}
	}

	public void Stop()
	{
		if (_previewing)
		{
			_previewing = false;
			reader.Stop();
		}
	}

	private void OnReaderStateChanged(object? sender, PlaybackState state)
	{
		if (state == PlaybackState.Playing)
		{
			_previewHeard = true;
		}
		else if (state == PlaybackState.Idle && _previewHeard)
		{
			_previewing = false;
		}
	}

	private void OnReaderCompleted(object? sender, EventArgs e) => _previewing = false;
}
