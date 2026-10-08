namespace RockULA.Core.Cpu;

public sealed partial class Z80Cpu
{
    private void ExecuteBitOperations()
    {
        ushort payloadAddress = Registers.PC;
        byte opcode = _bus.Execute(new Z80BusCycle(Z80BusCycleKind.OpcodeFetch, payloadAddress, 4, 3));
        Registers.PC = Increment(payloadAddress);
        IncrementRefresh();

        int group = opcode >> 6;
        int target = opcode & 7;
        byte value = ReadRegister(target);
        if (target == 6) Internal(Registers.HL, 1);

        (byte result, byte flags) = CalculateBitOperation(opcode, value, Registers.F,
            target == 6 ? (byte)(Registers.WZ >> 8) : value);
        if (group == 1)
        {
            Registers.F = flags;
            return;
        }

        WriteRegister(target, result);
        // A failed write can retain memory effects, but does not commit these flags.
        if (group == 0) Registers.F = flags;
    }

    private static (byte Result, byte Flags) CalculateBitOperation(byte opcode, byte value, byte incomingFlags, byte xy)
    {
        int group = opcode >> 6;
        int operation = (opcode >> 3) & 7;
        if (group == 1)
        {
            int tested = value & (1 << operation);
            return (value, (byte)((incomingFlags & 1) | (xy & 0x28) | 0x10
                | (tested == 0 ? 0x44 : 0) | (operation == 7 ? tested : 0)));
        }

        byte result;
        byte flags = incomingFlags;
        if (group == 0)
        {
            int incomingCarry = flags & 1;
            int carry = (operation & 1) == 0 ? value >> 7 : value & 1;
            result = unchecked((byte)(operation switch
            {
                0 => (value << 1) | carry,
                1 => (value >> 1) | (carry << 7),
                2 => (value << 1) | incomingCarry,
                3 => (value >> 1) | (incomingCarry << 7),
                4 => value << 1,
                5 => (value >> 1) | (value & 0x80),
                6 => (value << 1) | 1,
                7 => value >> 1,
                _ => throw new InvalidOperationException("Invalid CB operation.")
            }));
            flags = (byte)(ResultFlags(result) | ParityFlag(result) | carry);
        }
        else
        {
            int mask = 1 << operation;
            result = (byte)(group == 2 ? value & ~mask : value | mask);
        }

        return (result, flags);
    }
}
