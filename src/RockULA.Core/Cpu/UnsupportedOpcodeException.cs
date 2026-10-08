namespace RockULA.Core.Cpu;

public sealed class UnsupportedOpcodeException : NotSupportedException
{
    public UnsupportedOpcodeException(ushort address, byte opcode)
        : base($"Unsupported opcode or prefix 0x{opcode:X2} at 0x{address:X4}.")
    {
        Address = address;
        Opcode = opcode;
    }

    public ushort Address { get; }

    public byte Opcode { get; }
}
