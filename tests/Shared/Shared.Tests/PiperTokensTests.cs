using System.Text;
using Sirous.Reading.Application.Reading;
using Sirous.Reading.Domain.Reading;

namespace ReadTheStupidText.Tests;

// Slice 201: the Norwegian pack's tokens.txt held "aɪ 161" and sherpa-onnx's Piper reader aborts the process on it.
public class PiperTokensTests
{
    private const string Good = "_ 0\n^ 1\n$ 2\n  3\na 4\nɪ 5\n";

    [Fact]
    public void A_file_of_single_code_point_tokens_is_valid() => Assert.True(PiperTokens.IsValid(Good));

    [Fact]
    public void Windows_line_endings_are_accepted() => Assert.True(PiperTokens.IsValid(Good.Replace("\n", "\r\n")));

    [Fact]
    public void A_line_holding_only_an_id_is_the_space_token() =>
        Assert.True(PiperTokens.IsValid("_ 0\n^ 1\n$ 2\n3\na 4\n"));

    [Fact]
    public void A_two_code_point_token_is_rejected_with_its_line()
    {
        string? problem = PiperTokens.Problem(Good + "aɪ 161\n");

        Assert.NotNull(problem);
        Assert.Contains("line 7", problem);
    }

    [Fact]
    public void A_token_outside_the_basic_plane_counts_as_one_code_point() =>
        Assert.True(PiperTokens.IsValid(Good + "\U0001D11E 6\n"));

    [Fact]
    public void A_missing_or_non_numeric_id_is_rejected()
    {
        Assert.False(PiperTokens.IsValid(Good + "b x\n"));
        Assert.False(PiperTokens.IsValid(Good + "b 1x\n"));
    }

    [Fact]
    public void A_blank_line_is_rejected() => Assert.False(PiperTokens.IsValid("_ 0\n\n^ 1\n$ 2\n"));

    [Fact]
    public void A_byte_order_mark_in_the_raw_bytes_is_rejected_because_it_joins_the_first_token()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Good)];

        Assert.NotNull(PiperTokens.ProblemInBytes(bytes));
        Assert.Null(PiperTokens.ProblemInBytes(Encoding.UTF8.GetBytes(Good)));
    }

    [Fact]
    public void Invalid_utf8_is_rejected() =>
        Assert.NotNull(PiperTokens.ProblemInBytes([.. Encoding.UTF8.GetBytes(Good), 0xC3, 0x28, 0x20, 0x39, 0x0A]));

    [Fact]
    public void A_blank_token_line_is_skipped_as_sherpa_skips_it() =>
        Assert.True(PiperTokens.IsValid(Good + "<BLNK> 6\n<BLNK> 7\n"));

    [Fact]
    public void A_negative_id_is_accepted_as_sherpa_accepts_it() => Assert.True(PiperTokens.IsValid(Good + "b -1\n"));

    // Slice 303: the same verdicts as the packs.yml checker's fixtures (only space and tab are trimmed; ids are int32).
    [Fact]
    public void A_no_break_space_token_is_not_the_space_token() => Assert.True(PiperTokens.IsValid("_ 0\n^ 1\n$ 2\n  3\n4\n"));

    [Fact]
    public void An_id_beyond_int32_is_rejected() => Assert.False(PiperTokens.IsValid(Good + "b 2147483648\n"));

    [Fact]
    public void A_duplicate_token_is_rejected() => Assert.False(PiperTokens.IsValid(Good + "a 9\n"));

    [Theory]
    [InlineData("^ 1\n$ 2\n")]
    [InlineData("_ 0\n$ 2\n")]
    [InlineData("_ 0\n^ 1\n")]
    public void A_file_without_the_pad_bos_or_eos_token_is_rejected(string content) =>
        Assert.False(PiperTokens.IsValid(content));

    [Fact]
    public void The_broken_norwegian_tokens_are_found_through_the_manifest()
    {
        PackManifest manifest = PiperPack();
        var files = new Dictionary<string, byte[]> { ["voices/nvcc/tokens.txt"] = Encoding.UTF8.GetBytes(Good + "aʊ 162\n") };

        IReadOnlyList<string> broken = manifest.BrokenTokenFolders(p => files.GetValueOrDefault(p));

        Assert.Equal(["voices/nvcc"], broken);
    }

    [Fact]
    public void A_sound_or_absent_tokens_file_is_not_reported_by_the_manifest()
    {
        PackManifest manifest = PiperPack();

        Assert.Empty(manifest.BrokenTokenFolders(_ => Encoding.UTF8.GetBytes(Good)));
        Assert.Empty(manifest.BrokenTokenFolders(_ => null));
    }

    [Fact]
    public void Packs_of_other_engines_are_not_checked()
    {
        PackManifest melo = PiperPack() with { Model = VoiceEngineKind.Melo };

        Assert.Empty(melo.BrokenTokenFolders(_ => Encoding.UTF8.GetBytes("ab 1\n")));
    }

    [Fact]
    public void A_vits_pack_that_is_not_piper_keeps_its_multi_character_tokens()
    {
        PackManifest other = PiperPack() with { Id = "other-vits" };

        Assert.Empty(other.BrokenTokenFolders(_ => Encoding.UTF8.GetBytes("AA0 1\nab 2\n")));
    }

    // Slice 303 item 2: lines split on \n only (a trailing \r is stripped), the same as the packs.yml checker; a Unicode line separator never splits.
    [Theory]
    [InlineData(0x2028)]
    [InlineData(0x2029)]
    [InlineData(0x0085)]
    public void A_unicode_line_separator_does_not_split_a_line(int separator)
    {
        string content = Good + "b 6" + (char)separator + "c 7\n";

        Assert.False(PiperTokens.IsValid(content));
        Assert.Contains("line 7", PiperTokens.Problem(content));
    }

    [Fact]
    public void A_carriage_return_ends_no_line_but_is_stripped_before_a_line_feed()
    {
        Assert.True(PiperTokens.IsValid(Good.Replace("\n", "\r\n")));
        Assert.False(PiperTokens.IsValid(Good.Replace("a 4\n", "a 4\rb 8\n")));
    }

    private static PackManifest PiperPack() => new(
        "piper-nb", PackKind.Voice, "nb", "2", "sherpa-onnx", ["voices/nvcc/tokens.txt"],
        VoiceEngineKind.Vits, [new PackVoice("piper:nb-knn", "KNN", 0, "voices/nvcc")]);
}
