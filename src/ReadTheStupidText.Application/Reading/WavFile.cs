using System.Buffers.Binary;
using System.Text;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Reads and writes the one WAV layout the readers use: mono, 16-bit PCM, a canonical
/// 44-byte header (Slice 47). Pure byte work, so the sentence cache can join cached
/// sentences into one playable chunk without touching the engine.
/// </summary>
internal static class WavFile
{
    public const int HeaderBytes = 44;

    private const int BitsPerSample = 16;
    private const short ChannelCount = 1;
    private const int BytesPerSample = BitsPerSample / 8;
    private const int RiffSizeOffset = 4;
    private const int SampleRateOffset = 24;
    private const int DataSizeOffset = 40;
    private const int FmtChunkSize = 16;
    private const short PcmFormat = 1;
    private const string RiffTag = "RIFF";
    private const string WaveTag = "WAVE";
    private const string FmtTag = "fmt ";
    private const string DataTag = "data";

    /// <summary>Encodes mono float samples (clamped to -1..1) as a 16-bit PCM WAV.</summary>
    public static byte[] Encode(float[] samples, int sampleRate)
    {
        byte[] wav = new byte[HeaderBytes + samples.Length * BytesPerSample];
        WriteHeader(wav, sampleRate, samples.Length * BytesPerSample);
        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(HeaderBytes + i * BytesPerSample), value);
        }

        return wav;
    }

    /// <summary>The sample rate and PCM byte count a header declares, or null when the
    /// bytes are not a WAV this class wrote (a corrupt or foreign cache file).</summary>
    public static (int SampleRate, int DataBytes)? TryParseHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length < HeaderBytes || !HasTags(header))
        {
            return null;
        }

        int sampleRate = BinaryPrimitives.ReadInt32LittleEndian(header[SampleRateOffset..]);
        int dataBytes = BinaryPrimitives.ReadInt32LittleEndian(header[DataSizeOffset..]);
        return sampleRate > 0 && dataBytes >= 0 ? (sampleRate, dataBytes) : null;
    }

    /// <summary>True when <paramref name="header"/> is a valid header and a file of
    /// <paramref name="totalLength"/> bytes holds all the PCM it declares (a truncated clip,
    /// from a killed write or a full disk, is not).</summary>
    public static bool IsComplete(ReadOnlySpan<byte> header, long totalLength) =>
        TryParseHeader(header) is { } info && totalLength >= HeaderBytes + (long)info.DataBytes;

    /// <summary>How long the clip plays, or null when the header is not valid.</summary>
    public static TimeSpan? TryGetDuration(ReadOnlySpan<byte> header) =>
        TryParseHeader(header) is { } info
            ? TimeSpan.FromSeconds((double)info.DataBytes / (info.SampleRate * BytesPerSample))
            : null;

    /// <summary>Joins clips end to end with <paramref name="gap"/> of silence between
    /// them. All clips must share one sample rate and be valid.</summary>
    public static byte[] Concat(IReadOnlyList<byte[]> clips, TimeSpan gap)
    {
        int sampleRate = ParseOrThrow(clips[0]).SampleRate;
        int gapBytes = (int)(sampleRate * gap.TotalSeconds) * BytesPerSample;
        int dataBytes = clips.Sum(c => ParseOrThrow(c).DataBytes) + gapBytes * (clips.Count - 1);

        byte[] joined = new byte[HeaderBytes + dataBytes];
        WriteHeader(joined, sampleRate, dataBytes);
        int offset = HeaderBytes;
        for (int i = 0; i < clips.Count; i++)
        {
            offset += CopyPcm(clips[i], sampleRate, joined, offset);
            offset += i < clips.Count - 1 ? gapBytes : 0;
        }

        return joined;
    }

    private static int CopyPcm(byte[] clip, int sampleRate, byte[] target, int offset)
    {
        (int clipRate, int dataBytes) = ParseOrThrow(clip);
        if (clipRate != sampleRate)
        {
            throw new InvalidDataException($"Cannot join clips of {clipRate} Hz and {sampleRate} Hz.");
        }

        Buffer.BlockCopy(clip, HeaderBytes, target, offset, dataBytes);
        return dataBytes;
    }

    private static (int SampleRate, int DataBytes) ParseOrThrow(byte[] clip)
    {
        if (TryParseHeader(clip) is not { } info || HeaderBytes + info.DataBytes > clip.Length)
        {
            throw new InvalidDataException("Not a WAV written by this app.");
        }

        return info;
    }

    private static bool HasTags(ReadOnlySpan<byte> header) =>
        header[..4].SequenceEqual(Encoding.ASCII.GetBytes(RiffTag))
        && header[8..12].SequenceEqual(Encoding.ASCII.GetBytes(WaveTag))
        && header[12..16].SequenceEqual(Encoding.ASCII.GetBytes(FmtTag))
        && BinaryPrimitives.ReadInt32LittleEndian(header[16..]) == FmtChunkSize
        && BinaryPrimitives.ReadInt16LittleEndian(header[20..]) == PcmFormat
        && BinaryPrimitives.ReadInt16LittleEndian(header[22..]) == ChannelCount
        && BinaryPrimitives.ReadInt16LittleEndian(header[34..]) == BitsPerSample
        && header[36..40].SequenceEqual(Encoding.ASCII.GetBytes(DataTag));

    private static void WriteHeader(byte[] wav, int sampleRate, int dataBytes)
    {
        Span<byte> h = wav;
        Encoding.ASCII.GetBytes(RiffTag, h);
        BinaryPrimitives.WriteInt32LittleEndian(h[RiffSizeOffset..], HeaderBytes - 8 + dataBytes);
        Encoding.ASCII.GetBytes(WaveTag, h[8..]);
        Encoding.ASCII.GetBytes(FmtTag, h[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(h[16..], FmtChunkSize);
        BinaryPrimitives.WriteInt16LittleEndian(h[20..], PcmFormat);
        BinaryPrimitives.WriteInt16LittleEndian(h[22..], ChannelCount);
        BinaryPrimitives.WriteInt32LittleEndian(h[SampleRateOffset..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(h[28..], sampleRate * ChannelCount * BytesPerSample);
        BinaryPrimitives.WriteInt16LittleEndian(h[32..], ChannelCount * BytesPerSample);
        BinaryPrimitives.WriteInt16LittleEndian(h[34..], BitsPerSample);
        Encoding.ASCII.GetBytes(DataTag, h[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(h[DataSizeOffset..], dataBytes);
    }
}
