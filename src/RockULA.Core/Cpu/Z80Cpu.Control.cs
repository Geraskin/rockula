namespace RockULA.Core.Cpu;

public sealed partial class Z80Cpu
{
    private bool TryExecuteControl(byte opcode, ushort instructionAddress)
    {
        if ((opcode & 0xC7) == 0xC2)
        {
            ushort target = ReadNextWord();
            if (Condition((opcode >> 3) & 7)) Registers.PC = target;
            return true;
        }

        if ((opcode & 0xC7) == 0xC4)
        {
            ushort target = ReadNextWord();
            if (Condition((opcode >> 3) & 7)) Call(target);
            return true;
        }

        if ((opcode & 0xC7) == 0xC0)
        {
            Internal(instructionAddress, 1);
            if (Condition((opcode >> 3) & 7)) Registers.PC = Pop();
            return true;
        }

        if ((opcode & 0xC7) == 0xC7)
        {
            Internal(instructionAddress, 1);
            Push(Registers.PC);
            Registers.PC = (ushort)(opcode & 0x38);
            return true;
        }

        if ((opcode & 0xCF) == 0xC5)
        {
            ushort value = ((opcode >> 4) & 3) switch
            {
                0 => Registers.BC,
                1 => Registers.DE,
                2 => Registers.HL,
                _ => Registers.AF
            };
            Internal(instructionAddress, 1);
            Push(value);
            return true;
        }

        if ((opcode & 0xCF) == 0xC1)
        {
            ushort value = Pop();
            switch ((opcode >> 4) & 3)
            {
                case 0: Registers.BC = value; break;
                case 1: Registers.DE = value; break;
                case 2: Registers.HL = value; break;
                case 3: Registers.AF = value; break;
            }
            return true;
        }

        if (opcode is 0x18 or 0x20 or 0x28 or 0x30 or 0x38 or 0x10)
        {
            if (opcode == 0x10) Internal(instructionAddress, 1);
            ushort operandAddress = Registers.PC;
            sbyte displacement = unchecked((sbyte)ReadNextByte());
            bool taken;
            if (opcode == 0x10)
            {
                Registers.B = unchecked((byte)(Registers.B - 1));
                taken = Registers.B != 0;
            }
            else
            {
                taken = opcode == 0x18 || Condition((opcode >> 3) & 3);
            }
            if (taken)
            {
                Internal(operandAddress, 5);
                Registers.PC = unchecked((ushort)(Registers.PC + displacement));
            }
            return true;
        }

        switch (opcode)
        {
            case 0xC3:
                Registers.PC = ReadNextWord();
                return true;
            case 0xE9:
                Registers.PC = Registers.HL;
                return true;
            case 0xCD:
                Call(ReadNextWord());
                return true;
            case 0xC9:
                Registers.PC = Pop();
                return true;
            default:
                return false;
        }
    }

    private bool Condition(int condition)
    {
        int mask = (condition >> 1) switch
        {
            0 => 0x40,
            1 => 0x01,
            2 => 0x04,
            _ => 0x80
        };
        return ((Registers.F & mask) != 0) == ((condition & 1) != 0);
    }

    private void Call(ushort target)
    {
        Internal(unchecked((ushort)(Registers.PC - 1)), 1);
        Push(Registers.PC);
        Registers.PC = target;
    }

    private void Push(ushort value)
    {
        Registers.SP = unchecked((ushort)(Registers.SP - 1));
        WriteMemory(Registers.SP, (byte)(value >> 8));
        Registers.SP = unchecked((ushort)(Registers.SP - 1));
        WriteMemory(Registers.SP, unchecked((byte)value));
    }

    private ushort Pop()
    {
        byte low = ReadMemory(Registers.SP);
        Registers.SP = Increment(Registers.SP);
        byte high = ReadMemory(Registers.SP);
        Registers.SP = Increment(Registers.SP);
        return (ushort)(low | (high << 8));
    }

    // Address is a logical label only; see ADR 0005 before modeling contention.
    private void Internal(ushort address, int duration)
    {
        _bus.Execute(new Z80BusCycle(Z80BusCycleKind.Internal, address, duration, 0));
    }
}
