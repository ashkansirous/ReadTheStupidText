namespace DinoReader.BookBuilder;

/// <summary>Reads a JPEG's pixel size from its start-of-frame marker (Slice 359: <c>check</c> needs the back cover's shape and the builder has no image library).</summary>
internal static class JpegSize
{
    private const byte Marker = 0xFF;
    private const byte EndOfImage = 0xD9;
    private const byte StartOfScan = 0xDA;
    private const byte DefineHuffman = 0xC4;
    private const byte JpegExtension = 0xC8;
    private const byte ArithmeticConditioning = 0xCC;
    private const byte FirstFrame = 0xC0;
    private const byte LastFrame = 0xCF;
    private const int SegmentHeader = 2;
    private const int SizeOffset = 3; // after the length (2 bytes) and the sample precision (1 byte): height, then width

    /// <summary>The (width, height) of the JPEG at <paramref name="path"/>, or null when no frame header is found.</summary>
    public static (int Width, int Height)? Of(string path) => Of(File.ReadAllBytes(path));

    /// <summary>The (width, height) of the JPEG in <paramref name="bytes"/>; fill bytes (extra 0xFF) and standalone markers (0x01, RSTn) before the frame header are skipped.</summary>
    public static (int Width, int Height)? Of(byte[] bytes)
    {
        int at = SegmentHeader; // past the start-of-image marker
        while (at + SegmentHeader + SegmentHeader <= bytes.Length && bytes[at] == Marker)
        {
            byte kind = bytes[at + 1];
            if (kind == Marker)
            {
                at++; // a fill byte before the marker
                continue;
            }

            if (IsStandalone(kind))
            {
                at += SegmentHeader;
                continue;
            }

            if (kind is EndOfImage or StartOfScan)
            {
                return null; // no frame header before the image data
            }

            int length = (bytes[at + SegmentHeader] << 8) | bytes[at + SegmentHeader + 1];
            if (IsFrame(kind) && at + SegmentHeader + SizeOffset + 4 <= bytes.Length)
            {
                int height = (bytes[at + SegmentHeader + SizeOffset] << 8) | bytes[at + SegmentHeader + SizeOffset + 1];
                int width = (bytes[at + SegmentHeader + SizeOffset + 2] << 8) | bytes[at + SegmentHeader + SizeOffset + 3];
                return (width, height);
            }

            at += SegmentHeader + length;
        }

        return null;
    }

    private static bool IsStandalone(byte kind) => kind is 0x00 or 0x01 or (>= 0xD0 and <= 0xD8);

    private static bool IsFrame(byte kind) =>
        kind is >= FirstFrame and <= LastFrame && kind != DefineHuffman && kind != JpegExtension && kind != ArithmeticConditioning;
}
