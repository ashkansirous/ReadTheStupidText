namespace DinoReader.Tests;

/// <summary>Slice 395: <see cref="DinoReader.Core.PartTakes.Enabled"/> is one process-wide switch, so the tests that turn it off (it is on since Slice 443) run alone, after the parallel ones, and turn it back on.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PartTakesCollection
{
    public const string Name = "PartTakes";
}
