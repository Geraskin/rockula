namespace RockULA.Core.Cpu;

/// <summary>Instruction-boundary stepping for the explicitly supported opcode pages.</summary>
public sealed partial class Z80Cpu
{
    private readonly IZ80Bus _bus;

    public Z80Cpu(IZ80Bus bus, Z80Registers? registers = null)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        Registers = registers ?? new Z80Registers();
    }

    public Z80Registers Registers { get; }

    public bool IsFaulted { get; private set; }

    /// <summary>HALT latch; Step remains bounded and performs one ignored M1 read.</summary>
    public bool IsHalted { get; private set; }

    /// <summary>EI inhibition through the following successfully completed instruction.</summary>
    public bool IsEiDelayActive { get; private set; }

    /// <summary>Owner-supplied active INT level.</summary>
    public bool IsInterruptLineAsserted { get; private set; }

    /// <summary>Owner-supplied active NMI level, used to detect assertion edges.</summary>
    public bool IsNmiLineAsserted { get; private set; }

    /// <summary>Latched assertion edge; multiple edges before service coalesce.</summary>
    public bool IsNmiPending { get; private set; }

    public void SetInterruptLine(bool asserted) => IsInterruptLineAsserted = asserted;

    public void SetNmiLine(bool asserted)
    {
        if (asserted && !IsNmiLineAsserted)
        {
            IsNmiPending = true;
        }

        IsNmiLineAsserted = asserted;
    }

    /// <summary>Runs one interrupt response, instruction or halted fetch and returns actual cycles, including bus waits.</summary>
    public ulong Step()
    {
        if (IsFaulted)
        {
            throw new InvalidOperationException("The CPU is faulted. Reset it explicitly before further execution.");
        }

        ulong start = _bus.TStates;
        ushort address = Registers.PC;
        try
        {
            if (TryAcceptInterrupt())
            {
                return checked(_bus.TStates - start);
            }

            byte opcode = _bus.Execute(
                new Z80BusCycle(Z80BusCycleKind.OpcodeFetch, address, 4, 3));
            if (!IsHalted)
            {
                Registers.PC = Increment(address);
            }

            IncrementRefresh();
            if (!IsHalted)
            {
                Execute(opcode, address);
                // Retire only after successful execution. Repeated EI renews inhibition.
                IsEiDelayActive = opcode == 0xFB;
            }
            return checked(_bus.TStates - start);
        }
        catch
        {
            // A failure may already have advanced time or transferred data. Never skip it.
            IsFaulted = true;
            throw;
        }
    }

    /// <summary>Resets CPU state only; the owner must coordinate any whole-machine reset.</summary>
    public void Reset()
    {
        Registers.Reset();
        IsFaulted = false;
        IsHalted = false;
        IsEiDelayActive = false;
        IsInterruptLineAsserted = false;
        IsNmiLineAsserted = false;
        IsNmiPending = false;
    }

    private void Execute(byte opcode, ushort address)
    {
        if (TryExecuteAlu(opcode) || TryExecuteControl(opcode, address) || TryExecuteMisc(opcode, address))
        {
            return;
        }

        if (opcode >= 0x40 && opcode <= 0x7F && opcode != 0x76)
        {
            byte value = ReadRegister(opcode & 7);
            WriteRegister((opcode >> 3) & 7, value);
            return;
        }

        if ((opcode & 0xC7) == 0x06)
        {
            byte value = ReadNextByte();
            WriteRegister((opcode >> 3) & 7, value);
            return;
        }

        if ((opcode & 0xCF) == 0x01)
        {
            ushort value = ReadNextWord();
            switch ((opcode >> 4) & 3)
            {
                case 0:
                    Registers.BC = value;
                    break;
                case 1:
                    Registers.DE = value;
                    break;
                case 2:
                    Registers.HL = value;
                    break;
                case 3:
                    Registers.SP = value;
                    break;
            }

            return;
        }

        switch (opcode)
        {
            case 0x00:
                return;
            case 0xCB:
                ExecuteBitOperations();
                return;
            case 0xED:
                ExecuteExtended(address);
                return;
            case 0x76:
                IsHalted = true;
                return;
            case 0xF3:
                Registers.Iff1 = false;
                Registers.Iff2 = false;
                return;
            case 0xFB:
                Registers.Iff1 = true;
                Registers.Iff2 = true;
                return;
            case 0x02:
            case 0x12:
                ushort destination = opcode == 0x02 ? Registers.BC : Registers.DE;
                WriteMemory(destination, Registers.A);
                Registers.WZ = (ushort)((Registers.A << 8) | (Increment(destination) & 0xFF));
                return;
            case 0x0A:
            case 0x1A:
                ushort source = opcode == 0x0A ? Registers.BC : Registers.DE;
                Registers.A = ReadMemory(source);
                Registers.WZ = Increment(source);
                return;
            case 0x22:
            case 0x2A:
                ushort wordAddress = ReadNextWord();
                if (opcode == 0x22) WriteWord(wordAddress, Registers.HL);
                else Registers.HL = ReadWord(wordAddress);
                Registers.WZ = Increment(wordAddress);
                return;
            case 0x32:
            case 0x3A:
                ushort byteAddress = ReadNextWord();
                if (opcode == 0x32)
                {
                    WriteMemory(byteAddress, Registers.A);
                    Registers.WZ = (ushort)((Registers.A << 8) | (Increment(byteAddress) & 0xFF));
                }
                else
                {
                    Registers.A = ReadMemory(byteAddress);
                    Registers.WZ = Increment(byteAddress);
                }
                return;
            default:
                throw new UnsupportedOpcodeException(address, opcode);
        }
    }

    private byte ReadRegister(int code)
    {
        return code switch
        {
            0 => Registers.B,
            1 => Registers.C,
            2 => Registers.D,
            3 => Registers.E,
            4 => Registers.H,
            5 => Registers.L,
            6 => ReadMemory(Registers.HL),
            7 => Registers.A,
            _ => throw new ArgumentOutOfRangeException(nameof(code))
        };
    }

    private void WriteRegister(int code, byte value)
    {
        switch (code)
        {
            case 0:
                Registers.B = value;
                break;
            case 1:
                Registers.C = value;
                break;
            case 2:
                Registers.D = value;
                break;
            case 3:
                Registers.E = value;
                break;
            case 4:
                Registers.H = value;
                break;
            case 5:
                Registers.L = value;
                break;
            case 6:
                WriteMemory(Registers.HL, value);
                break;
            case 7:
                Registers.A = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(code));
        }
    }

    private byte ReadNextByte()
    {
        ushort address = Registers.PC;
        byte value = ReadMemory(address);
        Registers.PC = Increment(address);
        return value;
    }

    private ushort ReadNextWord()
    {
        byte low = ReadNextByte();
        byte high = ReadNextByte();
        return (ushort)(low | (high << 8));
    }

    private ushort ReadWord(ushort address)
    {
        byte low = ReadMemory(address);
        byte high = ReadMemory(Increment(address));
        return (ushort)(low | (high << 8));
    }

    private void WriteWord(ushort address, ushort value)
    {
        WriteMemory(address, unchecked((byte)value));
        WriteMemory(Increment(address), (byte)(value >> 8));
    }

    private byte ReadMemory(ushort address)
    {
        return _bus.Execute(new Z80BusCycle(Z80BusCycleKind.MemoryRead, address, 3, 3));
    }

    private void WriteMemory(ushort address, byte value)
    {
        _bus.Execute(new Z80BusCycle(Z80BusCycleKind.MemoryWrite, address, 3, 3, value));
    }

    private static ushort Increment(ushort address) => unchecked((ushort)(address + 1));
}
