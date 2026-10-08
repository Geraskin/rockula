namespace RockULA.Core.Cpu;

public sealed class UnsupportedOpcodeException : NotSupportedException
{
    public UnsupportedOpcodeException(ushort address, byte opcode)
        : base($"Unsupported opcode or prefix 0x{opcode:X2} at 0x{address:X4}.")
    {
        Address = address;
        Opcode = opcode;
    }

    public UnsupportedOpcodeException(ushort address, byte prefix, byte opcode)
        : base($"Unsupported opcode 0x{prefix:X2} 0x{opcode:X2} at 0x{address:X4}.")
    {
        Address = address;
        Prefix = prefix;
        Opcode = opcode;
    }

    public byte? Prefix { get; }

    public ushort Address { get; }

    public byte Opcode { get; }
}
