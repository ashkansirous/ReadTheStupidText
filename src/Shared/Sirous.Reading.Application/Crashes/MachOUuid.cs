using System.Buffers.Binary;
using System.Text;

namespace Sirous.Reading.Application.Crashes;

/// <summary>
/// Reads the <c>LC_UUID</c> of a Mach-O executable (Slice 243 item 6): the id that ties a crash report to the dSYM of the same build. Pure and defensive: a file that is not a
/// thin 64-bit little-endian Mach-O (or the first slice of a fat one) gives null, never an exception. Only the load-command list is read, not the whole binary.
/// </summary>
public static class MachOUuid
{
    private const uint Magic64 = 0xFEEDFACF;
    private const uint FatMagic = 0xCAFEBABE; // big-endian on disk
    private const uint LcUuid = 0x1B;
    private const int HeaderBytes = 32;
    private const int UuidBytes = 16;
    private const int MaxLoadCommandBytes = 1 << 20;

    /// <summary>The UUID as 32 uppercase hex digits with no dashes, or null.</summary>
    public static string? Read(Stream file)
    {
        try
        {
            long start = FatSliceStart(file);
            return start < 0 ? null : ReadSlice(file, start);
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or NotSupportedException or ObjectDisposedException)
        {
            // no-fallback: an unreadable executable simply has no UUID in the report
            return null;
        }
    }

    // A fat file starts with a big-endian header listing slices; the first one is used (the iOS device build is a single arm64 slice, fat only appears for a universal binary).
    private static long FatSliceStart(Stream file)
    {
        file.Position = 0;
        Span<byte> head = stackalloc byte[8];
        if (!TryFill(file, head))
        {
            return -1;
        }

        if (BinaryPrimitives.ReadUInt32BigEndian(head) != FatMagic)
        {
            return 0;
        }

        Span<byte> arch = stackalloc byte[20];
        file.Position = 8;
        return TryFill(file, arch) ? BinaryPrimitives.ReadUInt32BigEndian(arch[8..]) : -1;
    }

    private static string? ReadSlice(Stream file, long start)
    {
        file.Position = start;
        Span<byte> header = stackalloc byte[HeaderBytes];
        if (!TryFill(file, header) || BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic64)
        {
            return null;
        }

        uint commandCount = BinaryPrimitives.ReadUInt32LittleEndian(header[16..]);
        uint commandsSize = BinaryPrimitives.ReadUInt32LittleEndian(header[20..]);
        if (commandsSize == 0 || commandsSize > MaxLoadCommandBytes)
        {
            return null;
        }

        byte[] commands = new byte[commandsSize];
        return TryFill(file, commands) ? FindUuid(commands, commandCount) : null;
    }

    private static string? FindUuid(byte[] commands, uint count)
    {
        int offset = 0;
        for (uint i = 0; i < count && offset + 8 <= commands.Length; i++)
        {
            uint command = BinaryPrimitives.ReadUInt32LittleEndian(commands.AsSpan(offset));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(commands.AsSpan(offset + 4));
            if (size < 8)
            {
                return null;
            }

            if (command == LcUuid && size >= 8 + UuidBytes && offset + 8 + UuidBytes <= commands.Length)
            {
                return Convert.ToHexString(commands.AsSpan(offset + 8, UuidBytes));
            }

            offset += (int)size;
        }

        return null;
    }

    private static bool TryFill(Stream file, Span<byte> buffer)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = file.Read(buffer[read..]);
            if (n == 0)
            {
                return false;
            }

            read += n;
        }

        return true;
    }
}
