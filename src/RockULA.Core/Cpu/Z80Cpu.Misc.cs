namespace RockULA.Core.Cpu;

public sealed partial class Z80Cpu
{
    private bool TryExecuteMisc(byte opcode, ushort instructionAddress)
    {
        if ((opcode & 0xC7) == 0x03)
        {
            int pair = (opcode >> 4) & 3;
            int delta = (opcode & 8) == 0 ? 1 : -1;
            ushort result = unchecked((ushort)(ReadWordPair(pair) + delta));
            Internal(instructionAddress, 2);
            WriteWordPair(pair, result);
            return true;
        }

        if ((opcode & 0xCF) == 0x09)
        {
            ushort before = Registers.HL;
            ushort operand = ReadWordPair((opcode >> 4) & 3);
            int sum = before + operand;
            ushort result = unchecked((ushort)sum);
            byte flags = (byte)((Registers.F & 0xC4) | ((result >> 8) & 0x28));
            if (((before ^ operand ^ result) & 0x1000) != 0) flags |= 0x10;
            if (sum > 0xFFFF) flags |= 1;
            Internal(instructionAddress, 4);
            Internal(instructionAddress, 3);
            Registers.HL = result;
            Registers.F = flags;
            return true;
        }

        switch (opcode)
        {
            case 0x07:
            case 0x0F:
            case 0x17:
            case 0x1F:
                RotateAccumulator(opcode);
                return true;
            case 0x27:
                DecimalAdjust();
                return true;
            case 0x2F:
                Registers.A = unchecked((byte)~Registers.A);
                Registers.F = (byte)((Registers.F & 0xC5) | (Registers.A & 0x28) | 0x12);
                return true;
            case 0x08:
                (Registers.AF, Registers.AlternateAF) = (Registers.AlternateAF, Registers.AF);
                return true;
            case 0xEB:
                (Registers.DE, Registers.HL) = (Registers.HL, Registers.DE);
                return true;
            case 0xD9:
                (Registers.BC, Registers.AlternateBC) = (Registers.AlternateBC, Registers.BC);
                (Registers.DE, Registers.AlternateDE) = (Registers.AlternateDE, Registers.DE);
                (Registers.HL, Registers.AlternateHL) = (Registers.AlternateHL, Registers.HL);
                return true;
            case 0xE3:
                ExchangeStackWord();
                return true;
            case 0xF9:
                Internal(instructionAddress, 2);
                Registers.SP = Registers.HL;
                return true;
            default:
                return false;
        }
    }

    private ushort ReadWordPair(int pair) => pair switch
    {
        0 => Registers.BC,
        1 => Registers.DE,
        2 => Registers.HL,
        3 => Registers.SP,
        _ => throw new ArgumentOutOfRangeException(nameof(pair))
    };

    private void WriteWordPair(int pair, ushort value)
    {
        switch (pair)
        {
            case 0: Registers.BC = value; break;
            case 1: Registers.DE = value; break;
            case 2: Registers.HL = value; break;
            case 3: Registers.SP = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(pair));
        }
    }

    private void RotateAccumulator(byte opcode)
    {
        byte before = Registers.A;
        bool right = (opcode & 8) != 0;
        int carry = right ? before & 1 : before >> 7;
        int incoming = (opcode & 0x10) != 0 ? Registers.F & 1 : carry;
        byte result = unchecked((byte)(right ? (before >> 1) | (incoming << 7) : (before << 1) | incoming));
        Registers.A = result;
        Registers.F = (byte)((Registers.F & 0xC4) | (result & 0x28) | carry);
    }

    private void DecimalAdjust()
    {
        byte before = Registers.A;
        bool subtract = (Registers.F & 2) != 0;
        bool carry = (Registers.F & 1) != 0 || before > 0x99;
        int correction = carry ? 0x60 : 0;
        if ((Registers.F & 0x10) != 0 || (before & 0x0F) > 9) correction |= 6;
        byte result = unchecked((byte)(before + (subtract ? -correction : correction)));
        byte flags = (byte)(ResultFlags(result) | ParityFlag(result) | (Registers.F & 2) | (carry ? 1 : 0));
        if (((before ^ result) & 0x10) != 0) flags |= 0x10;
        Registers.A = result;
        Registers.F = flags;
    }

    private static byte ParityFlag(byte value)
    {
        int folded = value ^ (value >> 4);
        folded ^= folded >> 2;
        folded ^= folded >> 1;
        return (byte)((folded & 1) == 0 ? 4 : 0);
    }

    private void ExchangeStackWord()
    {
        ushort lowAddress = Registers.SP;
        ushort highAddress = Increment(lowAddress);
        ushort previous = Registers.HL;
        byte low = ReadMemory(lowAddress);
        byte high = ReadMemory(highAddress);
        Internal(highAddress, 1);
        WriteMemory(highAddress, (byte)(previous >> 8));
        WriteMemory(lowAddress, unchecked((byte)previous));
        Internal(lowAddress, 2);
        Registers.HL = (ushort)(low | (high << 8));
    }
}
