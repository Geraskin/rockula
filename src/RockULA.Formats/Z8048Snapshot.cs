using System.Buffers.Binary;
using RockULA.Core.Machines;

namespace RockULA.Formats;

/// <summary>Bounded ordinary 48K Z80 v1/v2/v3 import; owns RAM and never executes hardware.</summary>
public sealed class Z8048Snapshot : ISpectrum48Snapshot
{
    public const int MaximumFileSize = 200000;
    private readonly byte[] _ram;
    private Z8048Snapshot(Spectrum48CpuState state, byte border, byte[] ram, int version)
    {
        CpuState = state;
        BorderColor = border;
        _ram = ram;
        Version = version;
    }
    public Spectrum48CpuState CpuState { get; }
    public byte BorderColor { get; }
    public ReadOnlySpan<byte> Ram => _ram;
    public int Version { get; }
    public static Z8048Snapshot Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 30 || bytes.Length > MaximumFileSize) throw Invalid(0, "Invalid or oversized Z80 input.");
        byte flags = bytes[12] == 255 ? (byte)1 : bytes[12];
        byte mode = (byte)(bytes[29] & 3);
        if (mode > 2) throw Invalid(29, "Invalid interrupt mode.");
        ushort pc = Word(bytes, 6);
        byte[] ram = new byte[49152];
        int version = 1;
        if (pc != 0)
        {
            if ((flags & 0x10) != 0) throw new NotSupportedException("Z80 offset 12: SamROM is unsupported.");
            if ((flags & 0x20) == 0)
            {
                if (bytes.Length != 49182) throw Invalid(30, "Raw v1 requires exactly 49152 RAM bytes.");
                bytes[30..].CopyTo(ram);
            }
            else
            {
                if (bytes.Length < 34 || !bytes[^4..].SequenceEqual(new byte[] { 0, 0xED, 0xED, 0 }))
                    throw Invalid(bytes.Length, "Compressed v1 requires the final 00 ED ED 00 marker with no suffix.");
                Decode(bytes.Slice(30, bytes.Length - 34), ram, 30);
            }
        }
        else
        {
            if (bytes.Length < 32) throw Invalid(30, "Missing extended header length.");
            int extra = Word(bytes, 30);
            if (extra is not (23 or 54 or 55)) throw Invalid(30, "Unsupported extended header length.");
            int offset = 32 + extra;
            if (bytes.Length < offset) throw Invalid(32, "Truncated extended header.");
            version = extra == 23 ? 2 : 3;
            pc = Word(bytes, 32);
            if (bytes[34] != 0) throw new NotSupportedException("Z80 offset 34: only ordinary 48K hardware mode 0 is supported.");
            if ((bytes[37] & 0x84) != 0) throw new NotSupportedException("Z80 offset 37: 16K hardware modifiers and AY are unsupported.");
            if (bytes[36] != 0 || (version == 3 && (bytes[59] != 0 || bytes[60] != 0)))
                throw new NotSupportedException("Z80: paged Interface 1/MGT/Multiface ROM is unsupported.");
            int seen = 0;
            while (offset < bytes.Length)
            {
                if (bytes.Length - offset < 3) throw Invalid(offset, "Truncated page header.");
                int length = Word(bytes, offset);
                int page = bytes[offset + 2];
                int slot = page switch { 8 => 0, 4 => 1, 5 => 2, _ => -1 };
                if (slot < 0) throw new NotSupportedException($"Z80 offset {offset + 2}: unsupported page {page}.");
                if ((seen & (1 << slot)) != 0) throw Invalid(offset + 2, "Duplicate RAM page.");
                seen |= 1 << slot;
                offset += 3;
                bool raw = length == 65535;
                int encoded = raw ? 16384 : length;
                if (encoded == 0 || encoded > bytes.Length - offset) throw Invalid(offset, "Empty or truncated RAM page.");
                var destination = ram.AsSpan(slot * 16384, 16384);
                if (raw) bytes.Slice(offset, encoded).CopyTo(destination);
                else Decode(bytes.Slice(offset, encoded), destination, offset);
                offset += encoded;
            }
            if (seen != 7) throw Invalid(offset, "Missing required 48K RAM pages 8, 4 or 5.");
        }
        byte refresh = (byte)((bytes[11] & 0x7F) | ((flags & 1) << 7));
        var state = new Spectrum48CpuState((ushort)((bytes[0] << 8) | bytes[1]), Word(bytes, 2), Word(bytes, 13), Word(bytes, 4),
            (ushort)((bytes[21] << 8) | bytes[22]), Word(bytes, 15), Word(bytes, 17), Word(bytes, 19),
            Word(bytes, 25), Word(bytes, 23), pc, Word(bytes, 8), bytes[10], refresh, bytes[27] != 0, bytes[28] != 0, mode);
        return new(state, (byte)((flags >> 1) & 7), ram, version);
    }
    private static void Decode(ReadOnlySpan<byte> source, Span<byte> destination, int fileOffset)
    {
        int input = 0, output = 0;
        while (input < source.Length)
        {
            bool run = source[input] == 0xED && input + 1 < source.Length && source[input + 1] == 0xED;
            if (run)
            {
                if (source.Length - input < 4) throw Invalid(fileOffset + input, "Truncated ED ED run.");
                int count = source[input + 2];
                if (count == 0 || count > destination.Length - output) throw Invalid(fileOffset + input, "Zero or overflowing RLE run.");
                destination.Slice(output, count).Fill(source[input + 3]);
                output += count;
                input += 4;
            }
            else
            {
                if (output == destination.Length) throw Invalid(fileOffset + input, "Decoded RAM overflow / trailing data.");
                destination[output++] = source[input++];
            }
        }
        if (output != destination.Length) throw Invalid(fileOffset + input, "Decoded RAM length mismatch.");
    }
    private static ushort Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
    private static ArgumentException Invalid(int offset, string message) => new($"Z80 offset {offset}: {message}");
}
