namespace RockULA.Core.Cpu;

public sealed partial class Z80Cpu
{
    private bool TryExecuteAlu(byte opcode)
    {
        if (opcode is >= 0x80 and <= 0xBF)
        {
            AccumulatorOperation((opcode >> 3) & 7, ReadRegister(opcode & 7));
            return true;
        }

        if ((opcode & 0xC7) == 0xC6)
        {
            AccumulatorOperation((opcode >> 3) & 7, ReadNextByte());
            return true;
        }

        if ((opcode & 0xC6) == 0x04)
        {
            int register = (opcode >> 3) & 7;
            bool decrement = (opcode & 1) != 0;
            byte before = ReadRegister(register);
            byte result = unchecked((byte)(before + (decrement ? -1 : 1)));
            byte flags = (byte)(ResultFlags(result) | (Registers.F & 1));
            if (((before ^ result) & 0x10) != 0) flags |= 0x10;
            if (before == (decrement ? 0x80 : 0x7F)) flags |= 0x04;
            if (decrement) flags |= 0x02;
            if (register == 6) Internal(Registers.HL, 1);
            WriteRegister(register, result);
            Registers.F = flags;
            return true;
        }

        return false;
    }

    private void AccumulatorOperation(int operation, byte operand)
    {
        byte a = Registers.A;
        int carry = operation is 1 or 3 ? Registers.F & 1 : 0;
        bool subtract = operation is 2 or 3 or 7;
        int full;
        byte flags;
        if (operation is 4 or 5 or 6)
        {
            byte result = operation switch
            {
                4 => (byte)(a & operand),
                5 => (byte)(a ^ operand),
                _ => (byte)(a | operand)
            };
            flags = ResultFlags(result);
            // Fold to one parity bit, distinct from signed arithmetic overflow.
            int parity = result ^ (result >> 4);
            parity ^= parity >> 2;
            parity ^= parity >> 1;
            if ((parity & 1) == 0) flags |= 0x04;
            if (operation == 4) flags |= 0x10;
            Registers.A = result;
        }
        else
        {
            full = subtract ? a - operand - carry : a + operand + carry;
            byte result = unchecked((byte)full);
            flags = ResultFlags(result);
            if (((a ^ operand ^ result) & 0x10) != 0) flags |= 0x10;
            int overflow = subtract ? (a ^ operand) & (a ^ result) : ~(a ^ operand) & (a ^ result);
            if ((overflow & 0x80) != 0) flags |= 0x04;
            if (full < 0 || full > 0xFF) flags |= 1;
            if (subtract) flags |= 2;
            if (operation == 7)
            {
                // NMOS CP copies undocumented X/Y from the operand, not A-operand.
                flags = (byte)((flags & ~0x28) | (operand & 0x28));
            }
            else
            {
                Registers.A = result;
            }
        }

        Registers.F = flags;
    }

    private static byte ResultFlags(byte result) => (byte)((result & 0xA8) | (result == 0 ? 0x40 : 0));
}
