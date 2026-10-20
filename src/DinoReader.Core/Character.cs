namespace DinoReader.Core;

/// <summary>A speaking character; <see cref="Id"/> is the canonical name.</summary>
public sealed record Character(string Id, string Name, Gender Gender, int WordCount);
