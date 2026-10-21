namespace DinoReader.Core;

/// <summary>A line of a positioned story: its 0-based page and its 0-based line within that page.</summary>
public readonly record struct LineRef(int Page, int Line);
