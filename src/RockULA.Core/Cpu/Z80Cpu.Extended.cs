namespace RockULA.Core.Cpu;

public sealed partial class Z80Cpu
{
    private void ExecuteExtended(ushort instructionAddress)
    {
        ushort payloadAddress = Registers.PC;
        byte opcode = _bus.Execute(new Z80BusCycle(Z80BusCycleKind.OpcodeFetch, payloadAddress, 4, 3));
        Registers.PC = Increment(payloadAddress);
        IncrementRefresh();
        switch (opcode)
        {
            case 0x46:
                Registers.InterruptMode = 0;
                return;
            case 0x56:
                Registers.InterruptMode = 1;
                return;
            case 0x5E:
                Registers.InterruptMode = 2;
                return;
            case 0x45:
            case 0x4D:
                Registers.PC = Pop();
                Registers.Iff1 = Registers.Iff2;
                if (opcode == 0x4D) _bus.NotifyReti();
                return;
            default:
                throw new UnsupportedOpcodeException(instructionAddress, 0xED, opcode);
        }
    }
}
