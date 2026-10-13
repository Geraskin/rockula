using System.Buffers.Binary;
using RockULA.Core.Machines;

namespace RockULA.Formats;

/// <summary>Strict 48K SNA import; owns its RAM, does not execute/mutate a machine.</summary>
public sealed class Sna48Snapshot
{
    public const int FileSize = 49179;
    private readonly byte[] _ram;
    private Sna48Snapshot(Spectrum48CpuState state, byte border, byte[] ram)
    {
        CpuState = state;
        BorderColor = border;
        _ram = ram;
    }
    public Spectrum48CpuState CpuState { get; }
    public byte BorderColor { get; }
    public ReadOnlySpan<byte> Ram => _ram;
    public static Sna48Snapshot Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != FileSize) throw new ArgumentException("Only 48K SNA files of exactly 49179 bytes are supported.", nameof(bytes));
        ushort sp = Word(bytes, 23);
        if (sp < 0x4000 || sp == 0xFFFF) throw new ArgumentException("SNA offset 23: saved SP must point to two RAM bytes (4000-FFFE).");
        if (bytes[25] > 2) throw new ArgumentException("SNA offset 25: invalid interrupt mode.");
        if (bytes[26] > 7) throw new ArgumentException("SNA offset 26: invalid border / unsupported Interface 1 variant.");
        ushort pc = Word(bytes, 27 + sp - 0x4000);
        bool iff = (bytes[19] & 4) != 0;
        var state = new Spectrum48CpuState(Word(bytes, 21), Word(bytes, 13), Word(bytes, 11), Word(bytes, 9),
            Word(bytes, 7), Word(bytes, 5), Word(bytes, 3), Word(bytes, 1), Word(bytes, 17), Word(bytes, 15),
            pc, unchecked((ushort)(sp + 2)), bytes[0], bytes[20], iff, iff, bytes[25]);
        return new Sna48Snapshot(state, bytes[26], bytes[27..].ToArray());
    }
    private static ushort Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
}
