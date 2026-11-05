using System.Text.RegularExpressions;
using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 267: the closed detail values of the voice-used and pack-download counts.
public sealed class AudioPackUsageTests
{
    [Fact]
    public void A_read_with_any_clip_is_premade_whatever_the_reason()
    {
        Assert.Equal("premade", AudioPackUsage.ReadVoice(anyClip: true, DeviceVoiceReason.NoPack));
    }

    [Theory]
    [InlineData(DeviceVoiceReason.NoPack, "device:no_pack")]
    [InlineData(DeviceVoiceReason.PackFailed, "device:pack_failed")]
    [InlineData(DeviceVoiceReason.TextMismatch, "device:text_mismatch")]
    [InlineData(DeviceVoiceReason.ClipMissing, "device:clip_missing")]
    public void A_device_voice_read_names_its_reason(DeviceVoiceReason reason, string expected) =>
        Assert.Equal(expected, AudioPackUsage.ReadVoice(anyClip: false, reason));

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
        string[] voices = [AudioPackUsage.Premade, .. Enum.GetValues<DeviceVoiceReason>().Select(r => AudioPackUsage.ReadVoice(false, r))];
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
