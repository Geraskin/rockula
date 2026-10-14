using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80BitTests
{
    // Sources and BIT X/Y discrepancy: work order 001h. Expected values never use CPU helpers.
    public static IEnumerable<object[]> Encodings()
    {
        // Literal row starts from the CB opcode table; targets B,C,D,E,H,L,(HL),A.
        int[] rows = [0x00, 0x08, 0x10, 0x18, 0x20, 0x28, 0x30, 0x38,
            0x40, 0x48, 0x50, 0x58, 0x60, 0x68, 0x70, 0x78,
            0x80, 0x88, 0x90, 0x98, 0xA0, 0xA8, 0xB0, 0xB8,
            0xC0, 0xC8, 0xD0, 0xD8, 0xE0, 0xE8, 0xF0, 0xF8];
        foreach (int row in rows)
            for (int target = 0; target < 8; target++)
                yield return [row + target];
    }

    [Theory]
    [MemberData(nameof(Encodings))]
    public void EveryCbEncodingChecksAllOperandsAndCarriesAndAllIncomingFlags(int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xCB;
        bus.Memory[1] = (byte)opcode;
        var cpu = new Z80Cpu(bus);
        for (int value = 0; value < 256; value++)
            for (int carry = 0; carry < 2; carry++)
                Check(value, 0xFE | carry);
        for (int flags = 0; flags < 256; flags++) Check(0xA5, flags);

        void Check(int value, int flags)
        {
            cpu.Reset();
            var r = cpu.Registers;
            r.AF = 0x11FF;
            r.BC = 0x2233;
            r.DE = 0x4455;
            r.HL = 0x6677;
            r.IX = 0x1234;
            r.IY = 0x5678;
            r.AlternateAF = 0xABCD;
            r.AlternateBC = 0x1357;
            r.AlternateDE = 0x2468;
            r.AlternateHL = 0x9ABC;
            r.WZ = 0x2800;
            r.I = 0x80;
            r.Iff1 = true;
            r.Iff2 = true;
            r.InterruptMode = 2;
            int target = opcode % 8;
            SetOperand(bus, r, target, (byte)value);
            r.F = (byte)flags;
            byte[] before = [r.B, r.C, r.D, r.E, r.H, r.L, bus.Memory[r.HL], r.A];
            ushort memoryAddress = r.HL;
            bus.Events.Clear();
            (int result, int expectedFlags) = Expected(opcode, value, flags, 0x28);
            ulong expectedTime = target != 6 ? 8UL : opcode is >= 0x40 and < 0x80 ? 12UL : 15UL;
            Assert.Equal(expectedTime, cpu.Step());
            Assert.Equal((byte)expectedFlags, r.F);
            byte[] after = [r.B, r.C, r.D, r.E, r.H, r.L, bus.Memory[memoryAddress], r.A];
            for (int index = 0; index < 8; index++)
                Assert.Equal(index == target ? (byte)result : before[index], after[index]);
            Assert.Equal((ushort)2, r.PC);
            Assert.Equal((byte)2, r.R);
            Assert.Equal((ushort)0x2800, r.WZ);
            Assert.Equal(new ushort[] { 0x1234, 0x5678, 0xABCD, 0x1357, 0x2468, 0x9ABC, 0xFFFF },
                new[] { r.IX, r.IY, r.AlternateAF, r.AlternateBC, r.AlternateDE, r.AlternateHL, r.SP });
            Assert.Equal((byte)0x80, r.I);
            Assert.True(r.Iff1);
            Assert.True(r.Iff2);
            Assert.Equal((byte)2, r.InterruptMode);
            Assert.Equal(target == 6 ? opcode is >= 0x40 and < 0x80 ? 4 : 5 : 2, bus.Events.Count);
            Assert.False(cpu.IsFaulted);
        }
    }

    [Theory]
    [InlineData(0x46)]
    [InlineData(0x4E)]
    [InlineData(0x56)]
    [InlineData(0x5E)]
    [InlineData(0x66)]
    [InlineData(0x6E)]
    [InlineData(0x76)]
    [InlineData(0x7E)]
    public void MemoryBitChecksEveryWzHighByteOperandAndCarry(int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xCB;
        bus.Memory[1] = (byte)opcode;
        var cpu = new Z80Cpu(bus);
        cpu.Registers.HL = 0x4000;
        for (int high = 0; high < 256; high++)
            for (int value = 0; value < 256; value++)
                for (int carry = 0; carry < 2; carry++)
                {
                    cpu.Registers.PC = 0;
                    cpu.Registers.WZ = (ushort)((high << 8) | 0xFF);
                    cpu.Registers.F = (byte)(0xFE | carry);
                    bus.Memory[0x4000] = (byte)value;
                    bus.Events.Clear();
                    (_, int flags) = Expected(opcode, value, 0xFE | carry, high);
                    Assert.Equal(12UL, cpu.Step());
                    Assert.Equal((byte)flags, cpu.Registers.F);
                    Assert.Equal((ushort)((high << 8) | 0xFF), cpu.Registers.WZ);
                    Assert.Equal((byte)value, bus.Memory[0x4000]);
                }
    }

    [Theory]
    [InlineData(0x06, 0x03, 0x05)] // RLC (HL)
    [InlineData(0x46, 0x81, 0x10)] // BIT 0,(HL)
    [InlineData(0x86, 0x80, 0xFF)] // RES 0,(HL)
    [InlineData(0xC6, 0x81, 0xFF)] // SET 0,(HL)
    public void MemoryCyclesWrapPrefixAndReadIdleBeforeOptionalWrite(int opcode, int result, int flags)
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFF] = 0xCB;
        bus.Memory[0] = (byte)opcode;
        bus.Memory[0x4000] = 0x81;
        var cpu = new Z80Cpu(bus, new Z80Registers { PC = 0xFFFF, HL = 0x4000, R = 0xFF, F = 0xFF });
        bool bit = opcode == 0x46;
        Assert.Equal(bit ? 12UL : 15UL, cpu.Step());
        Assert.Equal((ushort)1, cpu.Registers.PC);
        Assert.Equal((byte)0x81, cpu.Registers.R);
        Assert.Equal((byte)result, bus.Memory[0x4000]);
        Assert.Equal((byte)(bit ? flags | 1 : flags), cpu.Registers.F);
        var events = new List<CycleEvent>
        {
            new(Z80BusCycleKind.OpcodeFetch, 0xFFFF, 0, 3, 4, 0xCB),
            new(Z80BusCycleKind.OpcodeFetch, 0, 4, 7, 8, (byte)opcode),
            new(Z80BusCycleKind.MemoryRead, 0x4000, 8, 11, 11, 0x81),
            new(Z80BusCycleKind.Internal, 0x4000, 11, 11, 12, 0)
        };
        if (!bit) events.Add(new(Z80BusCycleKind.MemoryWrite, 0x4000, 12, 15, 15, (byte)result));
        Assert.Equal(events.ToArray(), bus.Events.ToArray());
    }

    [Fact]
    public void WaitsShiftFetchReadIdleWriteAndReadSamplesLiveData()
    {
        var bus = new RecordingBus { WaitStates = _ => 1 };
        bus.Memory[0] = 0xCB;
        bus.Memory[1] = 0x06;
        bus.Advancing = time =>
        {
            if (time == 14) bus.Memory[0x4000] = 0x81;
        };
        var cpu = new Z80Cpu(bus, new Z80Registers { HL = 0x4000 });
        Assert.Equal(20UL, cpu.Step());
        Assert.Equal((byte)3, bus.Memory[0x4000]);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0, 0, 4, 5, 0xCB),
            new(Z80BusCycleKind.OpcodeFetch, 1, 5, 9, 10, 0x06),
            new(Z80BusCycleKind.MemoryRead, 0x4000, 10, 14, 14, 0x81),
            new(Z80BusCycleKind.Internal, 0x4000, 14, 15, 16, 0),
            new(Z80BusCycleKind.MemoryWrite, 0x4000, 16, 20, 20, 3)
        }, bus.Events.ToArray());
    }

    [Theory]
    [InlineData(Z80BusCycleKind.OpcodeFetch, 1, 4, 1, 1)]
    [InlineData(Z80BusCycleKind.MemoryRead, 0x4000, 8, 2, 2)]
    [InlineData(Z80BusCycleKind.Internal, 0x4000, 11, 2, 2)]
    [InlineData(Z80BusCycleKind.MemoryWrite, 0x4000, 12, 2, 2)]
    public void FailedCycleKeepsOperandAndFlagsAndRequiresReset(Z80BusCycleKind kind, int address, int time, int pc, int r)
    {
        var bus = new RecordingBus
        {
            WaitStates = cycle => cycle.Kind == kind && cycle.Address == address
                ? throw new InvalidOperationException("CB cycle failure") : 0
        };
        bus.Memory[0] = 0xCB;
        bus.Memory[1] = 0x06;
        bus.Memory[0x4000] = 0x81;
        var cpu = new Z80Cpu(bus, new Z80Registers { HL = 0x4000, F = 0xFF, WZ = 0x2800 });
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
        Assert.True(cpu.IsFaulted);
        Assert.Equal((ulong)time, bus.TStates);
        Assert.Equal((ushort)pc, cpu.Registers.PC);
        Assert.Equal((byte)r, cpu.Registers.R);
        Assert.Equal((byte)0xFF, cpu.Registers.F);
        Assert.Equal((ushort)0x2800, cpu.Registers.WZ);
        Assert.Equal((byte)0x81, bus.Memory[0x4000]);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
    }

    [Fact]
    public void PrefixIsAtomicAndWholeCbInstructionRetiresEiDelay()
    {
        var bus = new RecordingBus();
        byte[] program = [0xFB, 0xCB, 0x00];
        program.CopyTo(bus.Memory, 0);
        var cpu = new Z80Cpu(bus, new Z80Registers { B = 0x80, InterruptMode = 1 });
        cpu.SetInterruptLine(true);
        bus.Advancing = time =>
        {
            if (time == 7) cpu.SetNmiLine(true);
        };
        Assert.Equal(4UL, cpu.Step());
        Assert.Equal(8UL, cpu.Step());
        Assert.Equal((byte)1, cpu.Registers.B);
        Assert.Equal((ushort)3, cpu.Registers.PC);
        Assert.False(cpu.IsEiDelayActive);
        Assert.True(cpu.IsNmiPending);
        Assert.Equal(11UL, cpu.Step());
        Assert.Equal((ushort)0x66, cpu.Registers.PC);
        Assert.Equal((byte)3, bus.Memory[0xFFFD]);
        Assert.Equal(0, bus.InterruptAcknowledgements);
    }

    [Fact]
    public void SyntheticProgramManipulatesMemoryAndBranchesOnBit()
    {
        var bus = new RecordingBus();
        byte[] program = [0x21, 0x00, 0x40, 0x36, 0x80, 0xCB, 0x06, 0xCB, 0x7E,
            0x28, 0x02, 0x3E, 0xFF, 0xCB, 0xC6, 0xCB, 0x86, 0x7E, 0x76];
        program.CopyTo(bus.Memory, 0);
        var cpu = new Z80Cpu(bus);
        int steps = 0;
        while (!cpu.IsHalted && steps < 20)
        {
            cpu.Step();
            steps++;
        }
        Assert.True(cpu.IsHalted);
        Assert.Equal(9, steps);
        Assert.Equal(100UL, bus.TStates);
        Assert.Equal((byte)0, bus.Memory[0x4000]);
        Assert.Equal((byte)0, cpu.Registers.A);
        Assert.Equal((ushort)19, cpu.Registers.PC);
        Assert.Equal((byte)13, cpu.Registers.R);
        Assert.Equal((byte)0x55, cpu.Registers.F);
    }

    private static void SetOperand(RecordingBus bus, Z80Registers r, int target, byte value)
    {
        switch (target)
        {
            case 0: r.B = value; break;
            case 1: r.C = value; break;
            case 2: r.D = value; break;
            case 3: r.E = value; break;
            case 4: r.H = value; break;
            case 5: r.L = value; break;
            case 6: bus.Memory[r.HL] = value; break;
            case 7: r.A = value; break;
        }
    }

    private static (int Result, int Flags) Expected(int opcode, int value, int flags, int wzHigh)
    {
        int row = opcode / 8;
        if (row >= 16)
        {
            int mask = 1 << (row % 8);
            return (row < 24 ? value & (255 - mask) : value | mask, flags);
        }
        if (row >= 8)
        {
            bool set = (value & (1 << (row - 8))) != 0;
            int f = (flags & 1) | 0x10 | ((opcode % 8 == 6 ? wzHigh : value) & 0x28);
            if (!set) f |= 0x44;
            if (row == 15 && set) f |= 0x80;
            return (value, f);
        }
        int result = row switch
        {
            0 => (value * 2) % 256 + value / 128,
            1 => value / 2 + (value % 2) * 128,
            2 => (value * 2) % 256 + flags % 2,
            3 => value / 2 + (flags % 2) * 128,
            4 => (value * 2) % 256,
            5 => value / 2 + (value / 128) * 128,
            6 => (value * 2) % 256 + 1,
            7 => value / 2,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode))
        };
        int expected = result & 0xA8;
        if (result == 0) expected |= 0x40;
        int bits = 0;
        for (int bit = 0; bit < 8; bit++) bits += (result >> bit) & 1;
        if (bits % 2 == 0) expected |= 4;
        expected |= row is 1 or 3 or 5 or 7 ? value % 2 : value / 128;
        return (result, expected);
    }
}
