namespace RockULA.Core.Cpu;

public sealed partial class Z80Cpu
{
    private bool TryExecuteExtendedData(byte opcode, ushort instructionAddress)
    {
        if ((opcode & 0xC7) == 0x42)
        {
            ushort before = Registers.HL;
            ushort operand = ReadWordPair((opcode >> 4) & 3);
            int carry = Registers.F & 1;
            bool subtract = (opcode & 8) == 0;
            int full = subtract ? before - operand - carry : before + operand + carry;
            ushort result = unchecked((ushort)full);
            byte flags = (byte)((result >> 8) & 0xA8);
            if (result == 0) flags |= 0x40;
            if (((before ^ operand ^ result) & 0x1000) != 0) flags |= 0x10;
            int overflow = subtract ? (before ^ operand) & (before ^ result) : ~(before ^ operand) & (before ^ result);
            if ((overflow & 0x8000) != 0) flags |= 4;
            if (full < 0 || full > 0xFFFF) flags |= 1;
            if (subtract) flags |= 2;
            Internal(instructionAddress, 4);
            Internal(instructionAddress, 3);
            Registers.HL = result;
            Registers.F = flags;
            Registers.WZ = Increment(before);
            return true;
        }

        if ((opcode & 0xC7) == 0x43)
        {
            int pair = (opcode >> 4) & 3;
            ushort address = ReadNextWord();
            if ((opcode & 8) == 0) WriteWord(address, ReadWordPair(pair));
            else WriteWordPair(pair, ReadWord(address));
            Registers.WZ = Increment(address);
            return true;
        }

        if (opcode is 0x47 or 0x4F or 0x57 or 0x5F)
        {
            Internal(instructionAddress, 1);
            switch (opcode)
            {
                case 0x47:
                    Registers.I = Registers.A;
                    break;
                case 0x4F:
                    Registers.R = Registers.A;
                    break;
                default:
                    Registers.A = opcode == 0x57 ? Registers.I : Registers.R;
                    Registers.F = (byte)(ResultFlags(Registers.A) | (Registers.Iff2 ? 4 : 0) | (Registers.F & 1));
                    break;
            }
            return true;
        }

        if (opcode is 0x67 or 0x6F)
        {
            ushort address = Registers.HL;
            byte value = ReadMemory(address);
            byte before = Registers.A;
            bool right = opcode == 0x67;
            byte accumulator = (byte)((before & 0xF0) | (right ? value & 0x0F : value >> 4));
            byte memory = unchecked((byte)(right ? ((before & 0x0F) << 4) | (value >> 4) : (value << 4) | (before & 0x0F)));
            byte flags = (byte)(ResultFlags(accumulator) | ParityFlag(accumulator) | (Registers.F & 1));
            Internal(address, 4);
            WriteMemory(address, memory);
            // Even a ROM that ignores this write commits the computed accumulator.
            Registers.A = accumulator;
            Registers.F = flags;
            Registers.WZ = Increment(address);
            return true;
        }

        return false;
    }
}
