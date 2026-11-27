namespace DinoReader.Tests;

/// <summary>Slice 395: <see cref="DinoReader.Core.PartTakes.Enabled"/> is one process-wide switch, so the tests that turn it on run alone, after the parallel ones, and turn it off again.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PartTakesCollection
{
    public const string Name = "PartTakes";
}
