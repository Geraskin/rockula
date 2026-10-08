namespace RockULA.Core.Cpu;

public sealed partial class Z80Cpu
{
    // A complete address-space-sized prefix chain is an explicit execution fault.
    private byte ExecuteIndex(byte prefix, ushort instructionAddress)
    {
        int prefixes = 1;
        while (true)
        {
            if (prefixes >= 65536)
                throw new InvalidOperationException("Index prefix chain exceeds the 65,536-prefix Step bound.");
            ushort address = Registers.PC;
            byte opcode = _bus.Execute(new Z80BusCycle(Z80BusCycleKind.OpcodeFetch, address, 4, 3));
            Registers.PC = Increment(address);
            IncrementRefresh();
            if (opcode is 0xDD or 0xFD)
            {
                prefix = opcode;
                prefixes++;
                continue;
            }
            int index = prefix == 0xDD ? 1 : 2;
            if (opcode == 0xCB) ExecuteIndexedBit(index);
            else ExecuteIndexed(opcode, instructionAddress, index, prefix);
            return opcode;
        }
    }

    private ushort ReadHl(int index) => index switch
    {
        0 => Registers.HL,
        1 => Registers.IX,
        2 => Registers.IY,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    private void WriteHl(int index, ushort value)
    {
        switch (index)
        {
            case 0: Registers.HL = value; break;
            case 1: Registers.IX = value; break;
            case 2: Registers.IY = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    private byte ReadIndexRegister(int code, int index) => code switch
    {
        4 => (byte)(ReadHl(index) >> 8),
        5 => unchecked((byte)ReadHl(index)),
        _ => ReadRegister(code)
    };

    private void WriteIndexRegister(int code, int index, byte value)
    {
        if (code == 4) WriteHl(index, (ushort)((ReadHl(index) & 0xFF) | (value << 8)));
        else if (code == 5) WriteHl(index, (ushort)((ReadHl(index) & 0xFF00) | value));
        else WriteRegister(code, value);
    }

    private ushort ReadIndexedAddress(int index)
    {
        ushort displacementAddress = Registers.PC;
        sbyte displacement = unchecked((sbyte)ReadNextByte());
        Internal(displacementAddress, 5);
        ushort effective = unchecked((ushort)(ReadHl(index) + displacement));
        Registers.WZ = effective;
        return effective;
    }

    private void ExecuteIndexed(byte opcode, ushort instructionAddress, int index, byte prefix)
    {
        if (opcode is >= 0x80 and <= 0xBF)
        {
            int source = opcode & 7;
            byte operand = source == 6 ? ReadMemory(ReadIndexedAddress(index)) : ReadIndexRegister(source, index);
            AccumulatorOperation((opcode >> 3) & 7, operand);
            return;
        }
        if (opcode is >= 0x40 and <= 0x7F && opcode != 0x76)
        {
            int source = opcode & 7;
            int target = (opcode >> 3) & 7;
            if (source == 6 || target == 6)
            {
                ushort effective = ReadIndexedAddress(index);
                if (source == 6) WriteRegister(target, ReadMemory(effective));
                else WriteMemory(effective, ReadRegister(source));
            }
            else WriteIndexRegister(target, index, ReadIndexRegister(source, index));
            return;
        }
        if (opcode is 0x26 or 0x2E or 0x36)
        {
            int target = (opcode >> 3) & 7;
            if (target == 6)
            {
                sbyte displacement = unchecked((sbyte)ReadNextByte());
                ushort immediateAddress = Registers.PC;
                byte value = ReadNextByte();
                Internal(immediateAddress, 2);
                ushort effective = unchecked((ushort)(ReadHl(index) + displacement));
                Registers.WZ = effective;
                WriteMemory(effective, value);
            }
            else WriteIndexRegister(target, index, ReadNextByte());
            return;
        }
        if (opcode is 0x24 or 0x25 or 0x2C or 0x2D or 0x34 or 0x35)
        {
            int target = (opcode >> 3) & 7;
            ushort effective = target == 6 ? ReadIndexedAddress(index) : (ushort)0;
            byte before = target == 6 ? ReadMemory(effective) : ReadIndexRegister(target, index);
            bool decrement = (opcode & 1) != 0;
            byte result = unchecked((byte)(before + (decrement ? -1 : 1)));
            byte flags = (byte)(ResultFlags(result) | (Registers.F & 1));
            if (((before ^ result) & 0x10) != 0) flags |= 0x10;
            if (before == (decrement ? 0x80 : 0x7F)) flags |= 4;
            if (decrement) flags |= 2;
            if (target == 6)
            {
                Internal(effective, 1);
                WriteMemory(effective, result);
            }
            else WriteIndexRegister(target, index, result);
            Registers.F = flags;
            return;
        }
        if (TryExecuteMisc(opcode, instructionAddress, index)) return;
        switch (opcode)
        {
            case 0x21:
                WriteHl(index, ReadNextWord());
                return;
            case 0x22:
            case 0x2A:
                ushort address = ReadNextWord();
                if (opcode == 0x22) WriteWord(address, ReadHl(index));
                else WriteHl(index, ReadWord(address));
                Registers.WZ = Increment(address);
                return;
            case 0xE1:
                WriteHl(index, Pop());
                return;
            case 0xE5:
                Internal(instructionAddress, 1);
                Push(ReadHl(index));
                return;
            case 0xE9:
                Registers.PC = ReadHl(index);
                return;
            default:
                Execute(opcode, instructionAddress, prefix);
                return;
        }
    }

    private void ExecuteIndexedBit(int index)
    {
        sbyte displacement = unchecked((sbyte)ReadNextByte());
        ushort payloadAddress = Registers.PC;
        byte opcode = ReadNextByte();
        Internal(payloadAddress, 2);
        ushort effective = unchecked((ushort)(ReadHl(index) + displacement));
        Registers.WZ = effective;
        byte value = ReadMemory(effective);
        Internal(effective, 1);
        (byte result, byte flags) = CalculateBitOperation(opcode, value, Registers.F, (byte)(effective >> 8));
        if ((opcode >> 6) != 1)
        {
            WriteMemory(effective, result);
            int target = opcode & 7;
            if (target != 6) WriteRegister(target, result);
        }
        Registers.F = flags;
    }
}
