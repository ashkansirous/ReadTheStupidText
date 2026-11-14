using DinoReader.Core;
using Sirous.Reading.Mobile.Shared.Crashes;

namespace DinoReader;

/// <summary>Slice 250: the trace text of a stop reason. <see cref="ReadingSession.Stop"/> takes the closed <see cref="ReadStopReason"/>; this keeps the rolling log's wording (<see cref="TraceLines.ReadingReason"/>) the same as before.</summary>
public static class ReadStopTrace
{
	/// <summary>The fixed trace reason for <paramref name="reason"/>.</summary>
	public static string ReasonOf(ReadStopReason reason) => reason switch
	{
		ReadStopReason.LeftBook => TraceLines.ReadingReason.PageLeft,
		ReadStopReason.Finished => TraceLines.ReadingReason.Finished,
		ReadStopReason.Cancelled => TraceLines.ReadingReason.User,
		ReadStopReason.LanguageChanged => TraceLines.ReadingReason.LanguageChange,
		ReadStopReason.Restarted => TraceLines.ReadingReason.Superseded,
		ReadStopReason.WindowStopped => TraceLines.ReadingReason.AppBackground,
		ReadStopReason.Skipped => TraceLines.ReadingReason.Skipped,
		ReadStopReason.Failed => TraceLines.ReadingReason.Other,
		_ => TraceLines.ReadingReason.Other,
	};
}
