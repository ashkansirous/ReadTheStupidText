using System.Text.RegularExpressions;
using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 267: the closed detail values of the voice-used and pack-download counts.
public sealed class AudioPackUsageTests
{
    [Fact]
    public void A_read_whose_every_spoken_piece_had_a_clip_is_premade_whatever_the_reason()
    {
        ReadVoiceUse use = AudioPackUsage.ReadVoice(clipPieces: 12, devicePieces: 0, DeviceVoiceReason.NoPack);

        Assert.Equal("premade", use.Detail);
        Assert.Equal(12, use.ClipPieces);
        Assert.Null(use.FallbackReason);
    }

    [Fact]
    public void A_read_that_fell_back_part_way_is_partial_with_both_piece_counts_and_a_fallback_reason()
    {
        ReadVoiceUse use = AudioPackUsage.ReadVoice(clipPieces: 4, devicePieces: 8, DeviceVoiceReason.ClipMissing);

        Assert.Equal("partial", use.Detail);
        Assert.Equal((4, 8), (use.ClipPieces, use.DevicePieces));
        Assert.Equal("text_mismatch", use.FallbackReason);
    }

    [Theory]
    [InlineData(3, 1, "text_mismatch")]
    [InlineData(1, 3, "clip_missing")]
    [InlineData(2, 2, "text_mismatch")]
    [InlineData(0, 0, "text_mismatch")]
    public void A_partial_read_names_the_majority_cause_of_its_misses_and_a_tie_says_text_mismatch(int textMismatches, int filesMissing, string expected)
    {
        ReadVoiceUse use = AudioPackUsage.ReadVoice(clipPieces: 4, devicePieces: 4, DeviceVoiceReason.ClipMissing, textMismatches, filesMissing);

        Assert.Equal("partial", use.Detail);
        Assert.Equal(expected, use.FallbackReason);
    }

    [Fact]
    public void A_read_with_no_pack_to_use_is_not_a_fallback()
    {
        ReadVoiceUse use = AudioPackUsage.ReadVoice(clipPieces: 0, devicePieces: 5, DeviceVoiceReason.NoPack);

        Assert.Equal("device:no_pack", use.Detail);
        Assert.Null(use.FallbackReason);
    }

    [Theory]
    [InlineData(DeviceVoiceReason.PackFailed, "pack_failed")]
    [InlineData(DeviceVoiceReason.TextMismatch, "text_mismatch")]
    [InlineData(DeviceVoiceReason.ClipMissing, "clip_missing")]
    public void A_device_read_where_a_pack_was_due_is_a_fallback_with_its_reason(DeviceVoiceReason reason, string expected) =>
        Assert.Equal(expected, AudioPackUsage.ReadVoice(clipPieces: 0, devicePieces: 5, reason).FallbackReason);

    [Theory]
    [InlineData(DeviceVoiceReason.NoPack)]
    [InlineData(DeviceVoiceReason.ClipMissing)]
    public void A_read_with_no_spoken_piece_at_all_is_not_a_fallback(DeviceVoiceReason reason) =>
        Assert.Null(AudioPackUsage.ReadVoice(clipPieces: 0, devicePieces: 0, reason).FallbackReason);

    [Theory]
    [InlineData(DeviceVoiceReason.NoPack, "device:no_pack")]
    [InlineData(DeviceVoiceReason.PackFailed, "device:pack_failed")]
    [InlineData(DeviceVoiceReason.TextMismatch, "device:text_mismatch")]
    [InlineData(DeviceVoiceReason.ClipMissing, "device:clip_missing")]
    public void A_device_voice_read_names_its_reason(DeviceVoiceReason reason, string expected) =>
        Assert.Equal(expected, AudioPackUsage.ReadVoice(clipPieces: 0, devicePieces: 5, reason).Detail);

    [Theory]
    [InlineData(PackFailureKind.Network, "failed:network")]
    [InlineData(PackFailureKind.Size, "failed:size")]
    [InlineData(PackFailureKind.Unzip, "failed:unzip")]
    [InlineData(PackFailureKind.Clip, "failed:clip")]
    [InlineData(PackFailureKind.Missing, "failed:missing")]
    [InlineData(PackFailureKind.Storage, "failed:storage")]
    public void A_failed_pack_download_names_its_kind(PackFailureKind kind, string expected) =>
        Assert.Equal(expected, AudioPackUsage.PackFailed(kind));

    [Fact]
    public void The_Worker_takes_exactly_the_values_the_app_can_send()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "worker", "events.ts"));
        string[] voices = [AudioPackUsage.Premade, AudioPackUsage.PartialDetail, .. Enum.GetValues<DeviceVoiceReason>().Select(r => AudioPackUsage.ReadVoice(0, 1, r).Detail)];
        string[] packs = [AudioPackUsage.PackStarted, AudioPackUsage.PackDone, .. Enum.GetValues<PackFailureKind>().Select(AudioPackUsage.PackFailed)];

        Assert.Equal(voices.Order(), ListIn(source, "READ_VOICES").Order());
        Assert.Equal(packs.Order(), ListIn(source, "AUDIO_PACK_OUTCOMES").Order());
    }

    private static string[] ListIn(string source, string name)
    {
        Match list = Regex.Match(source, name + @" = \[(?<names>[^\]]*)\]");
        Assert.True(list.Success, name);
        return Regex.Matches(list.Groups["names"].Value, "\"(?<name>[a-z_:]+)\"").Select(m => m.Groups["name"].Value).ToArray();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReadTheStupidText.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo root not found.");
    }

    [Fact]
    public void Every_reason_and_kind_has_its_own_name()
    {
        Assert.Equal(Enum.GetValues<DeviceVoiceReason>().Length, Enum.GetValues<DeviceVoiceReason>().Select(AudioPackUsage.ReasonName).Distinct().Count());
        Assert.Equal(Enum.GetValues<PackFailureKind>().Length, Enum.GetValues<PackFailureKind>().Select(AudioPackUsage.KindName).Distinct().Count());
    }
}
