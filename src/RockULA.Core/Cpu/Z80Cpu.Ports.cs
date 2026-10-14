namespace RockULA.Core.Cpu;

public sealed partial class Z80Cpu
{
    private byte ReadPort(ushort address) =>
        _bus.Execute(new Z80BusCycle(Z80BusCycleKind.PortRead, address, 4, 4));

    private void WritePort(ushort address, byte value) =>
        _bus.Execute(new Z80BusCycle(Z80BusCycleKind.PortWrite, address, 4, 4, value));

    private bool TryExecuteImmediatePort(byte opcode)
    {
        if (opcode is not (0xDB or 0xD3)) return false;
        byte before = Registers.A;
        byte low = ReadNextByte();
        ushort port = (ushort)((before << 8) | low);
        if (opcode == 0xDB)
        {
            Registers.A = ReadPort(port);
            Registers.WZ = Increment(port);
        }
        else
        {
            WritePort(port, before);
            Registers.WZ = (ushort)((before << 8) | ((low + 1) & 0xFF));
        }
        return true;
    }

    private bool TryExecuteExtendedPort(byte opcode)
    {
        if ((opcode & 0xC7) is not (0x40 or 0x41)) return false;
        int register = (opcode >> 3) & 7;
        ushort port = Registers.BC;
        if ((opcode & 1) == 0)
        {
            byte value = ReadPort(port);
            if (register != 6) WriteRegister(register, value);
            SetFlags((byte)(ResultFlags(value) | ParityFlag(value) | (Registers.F & 1)));
        }
        else WritePort(port, register == 6 ? (byte)0 : ReadRegister(register));
        Registers.WZ = Increment(port);
        return true;
    }
}
