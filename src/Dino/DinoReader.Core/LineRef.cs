namespace DinoReader.Core;

/// <summary>A line of a positioned story: its 0-based page and its 0-based line within that page.</summary>
public readonly record struct LineRef(int Page, int Line);

/// <summary>A line and the story range [Start, End) its text was found at (<see cref="RangeToBoxMapper.Locate"/>).</summary>
public readonly record struct LocatedLine(LineRef Ref, int Start, int End);
