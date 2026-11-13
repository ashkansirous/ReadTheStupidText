using DinoReader.Core;

namespace DinoReader;

/// <summary>Which pill the reading page's sound line shows (Slice 290, Batch 50 frames 197-199).</summary>
public enum SoundStatusKind
{
	Getting,
	WaitingForInternet,
	Preparing,
	NoSound,
}

/// <summary>The sound line as data: the kind, and the run's position and total (only <see cref="SoundStatusKind.Getting"/> carries a count).</summary>
public sealed record SoundStatusPillState(SoundStatusKind Kind, int Position = 0, int Total = 0)
{
	/// <summary>The join between the "Getting ..." line and its count (the mock's middle dot with spaces).</summary>
	public const string CountSeparator = " · ";

	public bool HasCount => Kind == SoundStatusKind.Getting && Total > 0;

	/// <summary>The ring's fill, 0 to 1: the running book's position over the total, 0 without a count.</summary>
	public double RingProgress => HasCount ? Math.Clamp((double)Position / Total, 0, 1) : 0;

	/// <summary>The brand-tint pill (a run that is moving) against the neutral sand one (nothing moves).</summary>
	public bool IsBrandPill => Kind == SoundStatusKind.Getting;

	/// <summary>The state of a shelf run that covers the book; a book no run covers is <see cref="SoundStatusKind.NoSound"/>.</summary>
	public static SoundStatusPillState From(bool runIncludesBook, ShelfAudioState state)
	{
		if (!runIncludesBook)
		{
			return new SoundStatusPillState(SoundStatusKind.NoSound);
		}

		return state.Phase switch
		{
			ShelfAudioPhase.Running => new SoundStatusPillState(SoundStatusKind.Getting, state.Position, state.Total),
			ShelfAudioPhase.Preparing => new SoundStatusPillState(SoundStatusKind.Preparing),
			ShelfAudioPhase.WaitingForInternet => new SoundStatusPillState(SoundStatusKind.WaitingForInternet),
			_ => new SoundStatusPillState(SoundStatusKind.NoSound), // idle, done, cancelled, failed or the main language
		};
	}
}
