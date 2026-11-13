using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class WavFileTests
{
    [Fact]
    public void Encode_writes_a_canonical_mono_16_bit_header_and_duration()
    {
        byte[] wav = WavFile.Encode(new float[500], 1000);

        Assert.Equal(WavFile.HeaderBytes + 1000, wav.Length);
        Assert.Equal((1000, 1000), WavFile.TryParseHeader(wav));
        Assert.Equal(TimeSpan.FromMilliseconds(500), WavFile.TryGetDuration(wav));
    }

    [Theory]
    [InlineData(12, (byte)'x')]   // fmt tag
    [InlineData(16, (byte)18)]    // fmt chunk size
    [InlineData(20, (byte)3)]     // float, not PCM
    [InlineData(22, (byte)2)]     // stereo
    [InlineData(34, (byte)8)]     // 8-bit
    public void A_header_that_is_not_mono_16_bit_pcm_is_rejected(int offset, byte value)
    {
        byte[] wav = WavFile.Encode(new float[10], 1000);
        wav[offset] = value;

        Assert.Null(WavFile.TryParseHeader(wav));
        Assert.False(WavFile.IsComplete(wav, wav.Length));
    }

    [Fact]
    public void Encode_clamps_samples_to_16_bit_range()
    {
        byte[] wav = WavFile.Encode([2f, -2f, 0f], 1000);

        Assert.Equal(short.MaxValue, BitConverter.ToInt16(wav, WavFile.HeaderBytes));
        Assert.Equal(-short.MaxValue, BitConverter.ToInt16(wav, WavFile.HeaderBytes + 2));
        Assert.Equal(0, BitConverter.ToInt16(wav, WavFile.HeaderBytes + 4));
    }

    // The encoder the Windows reader carried before it shared WavFile.Encode.
    private static byte[] LegacyEncodeWav(float[] samples, int sampleRate)
    {
        int dataBytes = samples.Length * sizeof(short);
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(dataBytes);
        foreach (float sample in samples)
        {
            writer.Write((short)(Math.Clamp(sample, -1f, 1f) * short.MaxValue));
        }

        writer.Flush();
        return memory.ToArray();
    }

    [Fact]
    public void Encode_is_byte_identical_to_the_windows_readers_former_encoder()
    {
        float[] samples = [0f, 0.25f, -0.25f, 1f, -1f, 3f, -3f, 0.123456f];

        Assert.Equal(LegacyEncodeWav(samples, 44100), WavFile.Encode(samples, 44100));
    }

    [Fact]
    public void IsComplete_requires_all_the_pcm_the_header_declares()
    {
        byte[] wav = WavFile.Encode(new float[100], 1000);

        Assert.True(WavFile.IsComplete(wav, wav.Length));
        Assert.False(WavFile.IsComplete(wav, wav.Length - 1));
        Assert.False(WavFile.IsComplete(new byte[10], 10));
    }

    [Fact]
    public void A_foreign_or_short_header_does_not_parse()
    {
        Assert.Null(WavFile.TryParseHeader(new byte[10]));
        Assert.Null(WavFile.TryParseHeader(new byte[WavFile.HeaderBytes]));
        Assert.Null(WavFile.TryGetDuration(new byte[WavFile.HeaderBytes]));
    }

    [Fact]
    public void Concat_joins_clips_with_silence_between_them()
    {
        byte[] a = WavFile.Encode(Enumerable.Repeat(0.5f, 100).ToArray(), 1000);
        byte[] b = WavFile.Encode(Enumerable.Repeat(0.25f, 200).ToArray(), 1000);

        byte[] joined = WavFile.Concat([a, b], TimeSpan.FromMilliseconds(50));

        Assert.Equal(TimeSpan.FromMilliseconds(100 + 50 + 200), WavFile.TryGetDuration(joined));
        Assert.Equal(BitConverter.ToInt16(a, WavFile.HeaderBytes), BitConverter.ToInt16(joined, WavFile.HeaderBytes));
        Assert.Equal(0, BitConverter.ToInt16(joined, WavFile.HeaderBytes + 100 * 2 + 2)); // inside the gap
        Assert.Equal(
            BitConverter.ToInt16(b, WavFile.HeaderBytes),
            BitConverter.ToInt16(joined, WavFile.HeaderBytes + (100 + 50) * 2));
    }

    [Fact]
    public void Concat_of_one_clip_is_that_clip()
    {
        byte[] a = WavFile.Encode(new float[40], 1000);

        Assert.Equal(a, WavFile.Concat([a], TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public void Concat_rejects_clips_of_different_sample_rates()
    {
        byte[] a = WavFile.Encode(new float[10], 1000);
        byte[] b = WavFile.Encode(new float[10], 2000);

        Assert.Throws<InvalidDataException>(() => WavFile.Concat([a, b], TimeSpan.Zero));
    }

    [Fact]
    public void Concat_rejects_a_truncated_clip()
    {
        byte[] a = WavFile.Encode(new float[100], 1000);

        Assert.Throws<InvalidDataException>(() => WavFile.Concat([a[..(a.Length - 10)]], TimeSpan.Zero));
    }
}
