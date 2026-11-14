namespace ReadTheStupidText.Tests;

/// <summary>
/// Slice 368: PackDownloadHost (linked from Mobile.Shared) is static state, including its CancelRequested event.
/// Every class that touches it joins this collection, so none runs beside another (PackKeepAlivePolicyTests and
/// VoicePackSetupTests once raced on the event: plan.md Outstanding work, release run 42410230793).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PackDownloadHostCollection
{
    public const string Name = "PackDownloadHost";
}
