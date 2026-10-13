namespace RockULA.Core.Cpu;

public sealed partial class Z80Cpu
{
    private bool TryExecuteBlock(byte opcode, ushort instructionAddress)
    {
        if ((opcode & 0xE4) != 0xA0) return false;
        int direction = (opcode & 8) == 0 ? 1 : -1;
        int operation = opcode & 3;
        bool repeating = (opcode & 0x10) != 0;
        ushort source = Registers.HL;
        ushort nextHl = unchecked((ushort)(source + direction));
        if (operation < 2)
        {
            ushort destination = Registers.DE;
            ushort count = unchecked((ushort)(Registers.BC - 1));
            byte accumulator = Registers.A;
            byte value = ReadMemory(source);
            byte flags;
            if (operation == 0)
            {
                WriteMemory(destination, value);
                Internal(destination, 2);
                int sum = accumulator + value;
                flags = (byte)((Registers.F & 0xC1) | (sum & 8) | ((sum & 2) << 4) | (count != 0 ? 4 : 0));
                Registers.DE = unchecked((ushort)(destination + direction));
            }
            else
            {
                byte result = unchecked((byte)(accumulator - value));
                bool half = ((accumulator ^ value ^ result) & 0x10) != 0;
                byte adjusted = unchecked((byte)(result - (half ? 1 : 0)));
                flags = (byte)((ResultFlags(result) & 0xC0) | (Registers.F & 1) | 2
                    | (half ? 0x10 : 0) | (count != 0 ? 4 : 0) | (adjusted & 8) | ((adjusted & 2) << 4));
                Internal(source, 5);
                Registers.WZ = unchecked((ushort)(Registers.WZ + direction));
            }
            Registers.HL = nextHl;
            Registers.BC = count;
            Registers.F = flags;
            if (repeating && count != 0 && (operation == 0 || accumulator != value))
            {
                Internal(operation == 0 ? destination : source, 5);
                ushort repeatedPc = unchecked((ushort)(Registers.PC - 2));
                Registers.PC = repeatedPc;
                Registers.WZ = Increment(repeatedPc);
                Registers.F = (byte)((flags & ~0x28) | ((repeatedPc >> 8) & 0x28));
            }
            return true;
        }

        ushort originalPort = Registers.BC;
        byte afterB = unchecked((byte)(Registers.B - 1));
        ushort outputPort = (ushort)((afterB << 8) | Registers.C);
        Internal(instructionAddress, 1);
        byte data;
        ushort port;
        int sumIo;
        if (operation == 2)
        {
            port = originalPort;
            data = ReadPort(port);
            WriteMemory(source, data);
            sumIo = data + unchecked((byte)(Registers.C + direction));
            Registers.B = afterB;
        }
        else
        {
            port = outputPort;
            data = ReadMemory(source);
            Registers.B = afterB;
            WritePort(port, data);
            sumIo = data + (nextHl & 0xFF);
        }
        byte ioFlags = (byte)(ResultFlags(afterB) | ((data & 0x80) >> 6)
            | (sumIo > 0xFF ? 0x11 : 0) | ParityFlag((byte)((sumIo & 7) ^ afterB)));
        Registers.HL = nextHl;
        Registers.F = ioFlags;
        Registers.WZ = unchecked((ushort)(port + direction));
        if (repeating && afterB != 0)
        {
            Internal(source, 5);
            ushort repeatedPc = unchecked((ushort)(Registers.PC - 2));
            Registers.PC = repeatedPc;
            ioFlags = (byte)((ioFlags & ~0x28) | ((repeatedPc >> 8) & 0x28));
            int parityOperand = afterB;
            if ((ioFlags & 1) != 0)
            {
                bool negative = (data & 0x80) != 0;
                parityOperand += negative ? -1 : 1;
                bool half = (afterB & 0x0F) == (negative ? 0 : 0x0F);
                ioFlags = (byte)((ioFlags & ~0x10) | (half ? 0x10 : 0));
            }
            // Banks' intermediate-iteration correction; B is already decremented.
            Registers.F = (byte)(ioFlags ^ ParityFlag((byte)(parityOperand & 7)) ^ 4);
        }
        return true;
    }
}
