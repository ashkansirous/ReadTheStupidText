namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Read-through elapsed/total timing of the current utterance (Decision 33).
/// <see cref="Total"/> is null until every chunk of the current read has finished
/// synthesizing (it becomes known the instant the last one lands, regardless of how
/// much has actually played).
/// </summary>
public sealed record ReadTiming(TimeSpan Elapsed, TimeSpan? Total);
