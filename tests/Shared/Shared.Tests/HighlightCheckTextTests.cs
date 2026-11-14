using System.Text.RegularExpressions;
using Sirous.Reading.Application.Reading;

namespace ReadTheStupidText.Tests;

// Slice 319 item 3: since Slice 214 the first piece of a read is cut to about 8 words, and the phone reader's early start (typing is gradual) prepares the first two
// sentences as the read's head. With the old 142-character check text the S21 log shows three chunks of one sentence unit each ("chunk 1 (50 chars, 1 sentence(s))"),
// so the highlight had no later sentence in a chunk to move to. The text the highlight check types (tools/phone/checks/highlight.ps1,
// .maestro/rtst/sentence-highlight.yaml) must leave at least one chunk of two or more sentence units, with and without that head.
public partial class HighlightCheckTextTests
{
    private const string OldText = "The first sentence is long enough to stand alone. The second sentence follows right after it. The third sentence closes this short paragraph.";
    private const string OldHead = "The first sentence is long enough to stand alone. The second sentence follows right after it.";

    private static readonly SpeechTextChunker.HeadPolicy ReadPolicy = SpeechTextChunker.HeadPolicy.Cached.WithShortFirstPiece();

    [Fact]
    public void The_text_the_phone_check_types_has_a_chunk_of_two_sentences_without_a_prepared_head()
    {
        string text = TypedTextOf("tools/phone/checks/highlight.ps1", PowerShellTypedText());
        Assert.Contains(UnitCounts(text, null), count => count >= 2);
    }

    [Fact]
    public void The_text_the_phone_check_types_has_a_chunk_of_two_sentences_with_the_two_sentence_head()
    {
        string text = TypedTextOf("tools/phone/checks/highlight.ps1", PowerShellTypedText());
        string head = string.Join(' ', SentenceTexts(text).Take(2));
        Assert.True(SpeechTextChunker.AcceptsHint(text, head, ReadPolicy), "the first two sentences are not an acceptable head");
        Assert.Contains(UnitCounts(text, head), count => count >= 2);
    }

    // The S21's uiautomator dump clips an element's text at 255 characters, so a longer typed text never matches what Assert-TypedTextShown reads back (Slice 319, the 263-character text failed it).
    private const int MaxTypedTextLength = 240;

    [Fact]
    public void The_text_the_phone_check_types_stays_under_the_uiautomator_dump_clip_of_255_characters()
    {
        string text = TypedTextOf("tools/phone/checks/highlight.ps1", PowerShellTypedText());
        Assert.True(text.Length <= MaxTypedTextLength, $"the typed text is {text.Length} characters; the S21 uiautomator dump clips text at 255, so keep it at {MaxTypedTextLength} or fewer");
    }

    [Fact]
    public void The_maestro_flow_types_the_same_text_as_the_phone_check()
    {
        string phone = TypedTextOf("tools/phone/checks/highlight.ps1", PowerShellTypedText());
        string flow = TypedTextOf(".maestro/rtst/sentence-highlight.yaml", MaestroTypedText());
        Assert.Equal(phone, flow);
    }

    [Fact]
    public void The_old_text_with_its_two_sentence_head_gave_one_unit_per_chunk_as_the_device_log_showed()
    {
        Assert.True(SpeechTextChunker.AcceptsHint(OldText, OldHead, ReadPolicy));
        Assert.Equal([1, 1, 1], UnitCounts(OldText, OldHead));
    }

    private static List<int> UnitCounts(string text, string? head) =>
        SpeechChunkPlanner.ForText(text, ReadPolicy, firstChunkHint: head).Select(c => SentenceUnits.Split(c.Text).Count).ToList();

    private static IEnumerable<string> SentenceTexts(string text) =>
        Regex.Split(text, @"(?<=\.)\s+");

    private static string TypedTextOf(string relativePath, Regex pattern)
    {
        string file = File.ReadAllText(Path.Combine(RepoRoot(), relativePath));
        Match m = pattern.Match(file);
        Assert.True(m.Success, $"no typed text found in {relativePath}");
        return m.Groups["text"].Value;
    }

    // Anchored to the start of the line and the exact variable / step, so a comment or another step cannot be read as the typed text.
    [GeneratedRegex(@"^\$script:TypedText = '(?<text>[^']+)'", RegexOptions.Multiline)]
    private static partial Regex PowerShellTypedText();

    [GeneratedRegex("^- inputText: \"(?<text>[^\"]+)\"", RegexOptions.Multiline)]
    private static partial Regex MaestroTypedText();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReadTheStupidText.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo root (ReadTheStupidText.slnx) not found.");
    }
}
