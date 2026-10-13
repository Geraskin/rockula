namespace RockULA.Core.Cpu;

public sealed partial class Z80Cpu
{
    private void ExecuteExtended(ushort instructionAddress)
    {
        ushort payloadAddress = Registers.PC;
        byte opcode = _bus.Execute(new Z80BusCycle(Z80BusCycleKind.OpcodeFetch, payloadAddress, 4, 3));
        Registers.PC = Increment(payloadAddress);
        IncrementRefresh();
        if (TryExecuteBlock(opcode, instructionAddress) || TryExecuteExtendedPort(opcode) || TryExecuteExtendedData(opcode, instructionAddress)) return;
        switch (opcode)
        {
            case 0x44:
            case 0x4C:
            case 0x54:
            case 0x5C:
            case 0x64:
            case 0x6C:
            case 0x74:
            case 0x7C:
                byte accumulator = Registers.A;
                Registers.A = unchecked((byte)-accumulator);
                SetFlags((byte)(ResultFlags(Registers.A) | 2
                    | ((accumulator & 0x0F) != 0 ? 0x10 : 0)
                    | (accumulator == 0x80 ? 4 : 0)
                    | (accumulator != 0 ? 1 : 0)));
                return;
            case 0x46:
            case 0x4E:
            case 0x66:
            case 0x6E:
                Registers.InterruptMode = 0;
                return;
            case 0x56:
            case 0x76:
                Registers.InterruptMode = 1;
                return;
            case 0x5E:
            case 0x7E:
                Registers.InterruptMode = 2;
                return;
            case 0x45:
            case 0x55:
            case 0x5D:
            case 0x65:
            case 0x6D:
            case 0x75:
            case 0x7D:
            case 0x4D:
                Registers.PC = Pop();
                Registers.WZ = Registers.PC;
                Registers.Iff1 = Registers.Iff2;
                if (opcode == 0x4D) _bus.NotifyReti();
                return;
            default:
                // Undefined ED bytes physically execute as two-fetch NOPs.
                return;
        }
    }
}
