using DinoReader.BookBuilder;
using DinoReader.Core;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DinoReader.Tests;

/// <summary>Slice 154: Farsi vowel marks come only from the user's approved pronunciations (the *.fa.marks.json list), never from a guess.</summary>
public partial class FarsiMarksTests
{
    private const string PigsStem = "three-little-pigs";

    // SHA-256 of the pigs Farsi values (joined by a newline) with every mark stripped: the plain text before the marks were added.
    private const string PlainPigsHash = "638C0D96D3D9FC37A1BDF711F3E971D38EF7FE473153EDD0C0375D4C6E84B1BA";

    [GeneratedRegex("[ً-ْ]")]
    private static partial Regex Marks();

    [GeneratedRegex("[ء-غف-يپچژکگیً-ْ‌]+")]
    private static partial Regex Words();

    private sealed record Spoken(string Replaces, string Text);

    private sealed record Ezafe(string Sentence, int Word, string Plain, string Marked);

    private sealed record MarksFile(Dictionary<string, string> Marks, List<Ezafe> Ezafe, List<Spoken> Spoken);

    private static MarksFile LoadMarks() =>
        JsonSerializer.Deserialize<MarksFile>(
            File.ReadAllText(Path.Combine(StarterBuilder.TranslationsFolder(PigsStem), "fa.marks.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static string UndoReplacements(string text, MarksFile file) =>
        file.Spoken.Aggregate(text, (current, s) => current.Replace(s.Text, s.Replaces));

    private static Dictionary<string, string> Load(string suffix) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(StarterBuilder.TranslationsFolder(PigsStem), suffix)))!;

    [Fact]
    public void The_shown_pigs_text_carries_no_marks_but_the_one_tanwin()
    {
        var marks = Load("fa.json").Values.SelectMany(v => Marks().Matches(v).Select(m => m.Value)).ToList();

        Assert.Equal(["ً"], marks);
    }

    [Fact]
    public void The_spoken_pigs_text_stripped_of_marks_is_the_shown_text_for_every_piece()
    {
        var shown = Load("fa.json");
        var spoken = Load("fa.speak.json");
        MarksFile file = LoadMarks();

        Assert.Equal(shown.Keys.Order(), spoken.Keys.Order());
        foreach ((string piece, string text) in spoken)
        {
            Assert.Equal(Marks().Replace(shown[piece], ""), UndoReplacements(Marks().Replace(text, ""), file));
        }
    }

    [Fact]
    public void Every_spoken_pigs_word_is_its_approved_marking_or_an_ezafe_approved_for_that_very_place()
    {
        MarksFile file = LoadMarks();
        var shown = Load("fa.json");
        var spoken = Load("fa.speak.json");
        var wrong = new List<string>();

        foreach ((string piece, string text) in shown)
        {
            var plainWords = Words().Matches(text).Select(m => m.Value).ToList();
            var spokenWords = Words().Matches(spoken[piece]).Select(m => m.Value).ToList();
            if (plainWords.Count != spokenWords.Count)
            {
                continue; // a piece with one of the user's spoken replacements
            }

            for (int i = 0; i < plainWords.Count; i++)
            {
                string expected = file.Ezafe.FirstOrDefault(e => e.Sentence == text && e.Word == i)?.Marked ?? file.Marks.GetValueOrDefault(plainWords[i], plainWords[i]);
                if (spokenWords[i] != expected)
                {
                    wrong.Add($"{plainWords[i]} in {text}");
                }
            }
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public void An_ezafe_is_approved_for_one_sentence_and_word_only_and_the_plain_word_has_none()
    {
        MarksFile file = LoadMarks();
        var shown = Load("fa.json").Values.ToHashSet();

        Assert.NotEmpty(file.Ezafe);
        foreach (Ezafe e in file.Ezafe)
        {
            Assert.Contains(e.Sentence, shown);
            Assert.Equal(e.Plain, Words().Matches(e.Sentence)[e.Word].Value);
            Assert.NotEqual(file.Marks[e.Plain], e.Marked);
            Assert.EndsWith("ِ", e.Marked.TrimEnd('ی'));
            Assert.DoesNotMatch("ِ$", file.Marks[e.Plain]);
        }
    }

    [Fact]
    public void The_wolf_said_something_carries_no_ezafe_because_nothing_follows_it()
    {
        var spoken = Load("fa.speak.json");
        string piece = Load("fa.json").Single(p => p.Value == "گرگ گفت.").Key;

        Assert.StartsWith(LoadMarks().Marks["گرگ"] + " ", spoken[piece]);
        Assert.DoesNotContain("گُرْگِ گُ", spoken[piece]);
    }

    [Fact]
    public void Every_spoken_replacement_is_used_and_its_phrase_is_in_the_shown_text()
    {
        MarksFile file = LoadMarks();
        string shown = string.Join("\n", Load("fa.json").Values);
        string spoken = Marks().Replace(string.Join("\n", Load("fa.speak.json").Values), "");

        Assert.NotEmpty(file.Spoken);
        Assert.All(file.Spoken, s => Assert.True(shown.Contains(s.Replaces) && spoken.Contains(s.Text), s.Replaces));
    }

    [Fact]
    public async Task The_shipped_pigs_package_speaks_the_marked_text_and_shows_the_plain_text()
    {
        await using FileStream asset = File.OpenRead(Path.Combine(StarterBuilder.StartersFolder(), "three-little-pigs.dino"));
        Story shipped = await StoryPackageReader.ReadStoryOnlyAsync(asset);
        var shown = Load("fa.json");
        var spoken = Load("fa.speak.json");

        Assert.Equal(spoken, shipped.Translations![BundledTranslations.SpeakKey("fa")]);
        Assert.Equal(shown, shipped.Translations["fa"]);
        foreach ((string piece, string plain) in shown)
        {
            Assert.Equal(plain, BundledTranslations.Lookup(shipped, "fa", piece));
            Assert.Equal(spoken[piece], BundledTranslations.LookupSpoken(shipped, "fa", piece));
        }
    }

    [Fact]
    public async Task The_speech_translator_uses_the_marked_text_and_the_display_translator_the_plain_one()
    {
        await using FileStream asset = File.OpenRead(Path.Combine(StarterBuilder.StartersFolder(), "three-little-pigs.dino"));
        Story shipped = await StoryPackageReader.ReadStoryOnlyAsync(asset);
        string piece = Load("fa.json").Single(p => p.Value == "گرگ گفت.").Key;

        string spoken = await BundledTranslations.ComposeSpoken(shipped, "fa", null)(piece, TestContext.Current.CancellationToken);
        string shown = await BundledTranslations.Compose(shipped, "fa", null)(piece, CancellationToken.None);

        Assert.Equal("گرگ گفت.", shown);
        Assert.NotEqual(shown, spoken);
        Assert.Equal("گرگ گفت.", Marks().Replace(spoken, ""));
    }

    [Fact]
    public void Every_approved_mark_strips_back_to_its_plain_word()
    {
        foreach ((string plain, string marked) in LoadMarks().Marks)
        {
            Assert.Equal(Marks().Replace(plain, ""), Marks().Replace(marked, ""));
        }
    }

    [Fact]
    public void Stripping_the_marks_gives_back_the_earlier_plain_pigs_text()
    {
        string stripped = string.Join("\n", Load("fa.json").Values.Select(v => Marks().Replace(v, "")));

        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stripped)));

        Assert.Equal(PlainPigsHash, hash);
    }
}
