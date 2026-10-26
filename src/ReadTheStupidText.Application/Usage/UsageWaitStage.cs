using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Application.Usage;

/// <summary>The wire names of <see cref="WaitStage"/> in a wait count (Slice 148); the Worker's <c>RTST_WAIT_STAGES</c> and <c>DINO_WAIT_STAGES</c> must match.</summary>
public static class UsageWaitStage
{
    public const string PreparingVoice = "preparing_voice";
    public const string Stalled = "stalled";
    public const string DownloadingModel = "downloading_model";
    public const string Translating = "translating";
    public const string ReadingPictures = "reading_pictures";
    public const string Saving = "saving";
    public const string GettingFile = "getting_file";

    /// <summary>Every name a count may carry.</summary>
    public static IReadOnlyList<string> All { get; } =
        [PreparingVoice, Stalled, DownloadingModel, Translating, ReadingPictures, Saving, GettingFile];

    /// <summary>The wire name of <paramref name="stage"/>.</summary>
    public static string Of(WaitStage stage) => stage switch
    {
        WaitStage.PreparingVoice => PreparingVoice,
        WaitStage.Stalled => Stalled,
        WaitStage.DownloadingModel => DownloadingModel,
        WaitStage.Translating => Translating,
        WaitStage.ReadingPictures => ReadingPictures,
        WaitStage.Saving => Saving,
        WaitStage.GettingFile => GettingFile,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Add the wait stage's wire name here and to the Worker's stage list."),
    };
}
