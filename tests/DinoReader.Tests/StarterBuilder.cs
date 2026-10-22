using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>
/// Builds the bundled starter <c>.dino</c> files (Slice 91) from <see cref="StarterTexts"/>, casting voices the way the app does
/// (segment, attribute, cast over the ten Supertonic styles). Run once with <c>DINO_BUILD_STARTERS=1</c> to rewrite
/// <c>src/DinoReader.App/Resources/Raw/starters/</c>; the other tests then check the shipped files.
/// </summary>
internal static class StarterBuilder
{
    public const string BuildSwitch = "DINO_BUILD_STARTERS";
    public const string NarratorVoice = "supertonic:M1";

    public static readonly DateTimeOffset Created = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<VoiceOption> Voices =
        Enumerable.Range(1, 5).Select(i => new VoiceOption($"supertonic:F{i}", Gender.Female))
            .Concat(Enumerable.Range(1, 5).Select(i => new VoiceOption($"supertonic:M{i}", Gender.Male))).ToList();

    public static IReadOnlyList<(string Id, string Title, string Text, int ColourIndex, string FileName)> Starters { get; } =
    [
        (StarterTexts.PigsId, "The Three Little Pigs", StarterTexts.Pigs, 3, "three-little-pigs.dino"),
        (StarterTexts.GingerbreadId, "The Gingerbread Man", StarterTexts.Gingerbread, 1, "gingerbread-man.dino"),
        (StarterTexts.TortoiseId, "The Tortoise and the Hare", StarterTexts.Tortoise, 0, "tortoise-and-hare.dino"),
    ];

    public static string StartersFolder() =>
        Path.Combine(RepoRoot(), "src", "DinoReader.App", "Resources", "Raw", "starters");

    public static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ReadTheStupidText.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Repository root not found.");
    }

    public static Story Build(string id, string title, string text, int colourIndex)
    {
        IReadOnlyList<StoryPage> pages = text.ReplaceLineEndings("\n").Split("\n---\n").Select(p => new StoryPage(p.Trim())).ToList();
        var bare = new Story(id, title, "en", Created, pages, [], CoverStyle.Colour, colourIndex, NarratorVoice);
        return bare with { Cast = CastOf(bare) };
    }

    private static IReadOnlyList<CastMember> CastOf(Story story)
    {
        var rules = LanguageRules.English;
        AttributionResult attribution = new SpeakerAttributor(rules).Attribute(story.Text, new StorySegmenter(rules).Split(story.Text, story.BreakMap()));
        VoiceCast cast = new VoiceCaster().Cast(attribution.Characters, Voices, NarratorVoice);
        return StoryCast.ToMembers(attribution.Characters, cast);
    }

    public static async Task WriteAllAsync()
    {
        Directory.CreateDirectory(StartersFolder());
        string scratch = Path.Combine(Path.GetTempPath(), "dino-starters-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var (id, title, text, colour, fileName) in Starters)
            {
                await WriteOneAsync(Build(id, title, text, colour), Path.Combine(StartersFolder(), fileName), scratch);
            }
        }
        finally
        {
            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, recursive: true);
            }
        }
    }

    private static async Task WriteOneAsync(Story story, string target, string scratch)
    {
        var library = new FileStoryLibrary(scratch);
        await library.AddAsync(story);
        await using FileStream output = File.Create(target);
        await StoryPackageWriter.WriteAsync(story, library, output);
    }
}
