using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80WzTests
{
    // Boo-boo/Kladov physical-chip MEMPTR research, 2006; ADR 0009 scopes logical commits.
    [Theory]
    [InlineData(0x0A, 0x28FF, 0x2900)]
    [InlineData(0x1A, 0xFFFF, 0)]
    [InlineData(0x3A, 0xFFFF, 0)]
    [InlineData(0x02, 0x28FF, 0xA500)]
    [InlineData(0x12, 0xFFFF, 0xA500)]
    [InlineData(0x32, 0x28FF, 0xA500)]
    [InlineData(0x22, 0xFFFF, 0)]
    [InlineData(0x2A, 0x28FF, 0x2900)]
    public void MemoryLoadsSetWzFromAddressAndAccumulatorWithoutHighCarry(int opcode, int address, int expected)
    {
        var bus = new RecordingBus();
        bus.Memory[0x1000] = (byte)opcode;
        bus.Memory[0x1001] = (byte)(address & 255);
        bus.Memory[0x1002] = (byte)(address >> 8);
        bus.Memory[address] = 0x34;
        bus.Memory[(address + 1) % 65536] = 0x12;
        var cpu = new Z80Cpu(bus, new Z80Registers
        {
            PC = 0x1000,
            A = 0xA5,
            BC = (ushort)address,
            DE = (ushort)address,
            HL = 0x4567,
            WZ = 0x1111
        });
        cpu.Step();
        Assert.Equal((ushort)expected, cpu.Registers.WZ);
    }

    [Theory]
    [InlineData(0x09)]
    [InlineData(0x19)]
    [InlineData(0x29)]
    [InlineData(0x39)]
    public void WordAddSetsWzFromOriginalHlIncludingWrap(int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)opcode;
        var cpu = new Z80Cpu(bus);
        foreach (ushort initial in new ushort[] { 0x27FF, 0xFFFF })
        {
            cpu.Reset();
            cpu.Registers.HL = initial;
            cpu.Registers.BC = cpu.Registers.DE = cpu.Registers.SP = 0x1000;
            cpu.Registers.WZ = 0x1111;
            cpu.Step();
            Assert.Equal(initial == 0xFFFF ? (ushort)0 : (ushort)0x2800, cpu.Registers.WZ);
        }
    }

    [Theory]
    [InlineData(0xC3)]
    [InlineData(0xCD)]
    [InlineData(0xC2)]
    [InlineData(0xCA)]
    [InlineData(0xD2)]
    [InlineData(0xDA)]
    [InlineData(0xE2)]
    [InlineData(0xEA)]
    [InlineData(0xF2)]
    [InlineData(0xFA)]
    [InlineData(0xC4)]
    [InlineData(0xCC)]
    [InlineData(0xD4)]
    [InlineData(0xDC)]
    [InlineData(0xE4)]
    [InlineData(0xEC)]
    [InlineData(0xF4)]
    [InlineData(0xFC)]
    public void ImmediateJumpAndCallSetWzEvenWhenConditionFails(int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)opcode;
        bus.Memory[1] = 0x34;
        bus.Memory[2] = 0x28;
        var cpu = new Z80Cpu(bus);
        for (int flags = 0; flags < 256; flags++)
        {
            cpu.Reset();
            cpu.Registers.F = (byte)flags;
            cpu.Registers.WZ = 0x1111;
            bus.Events.Clear();
            cpu.Step();
            Assert.Equal((ushort)0x2834, cpu.Registers.WZ);
        }
    }

    [Theory]
    [InlineData(0x18, 0, 2, 0x1001)]
    [InlineData(0x20, 0, 2, 0x1001)]
    [InlineData(0x20, 0x40, 2, 0x2800)]
    [InlineData(0x28, 0x40, 2, 0x1001)]
    [InlineData(0x28, 0, 2, 0x2800)]
    [InlineData(0x30, 0, 2, 0x1001)]
    [InlineData(0x30, 1, 2, 0x2800)]
    [InlineData(0x38, 1, 2, 0x1001)]
    [InlineData(0x38, 0, 2, 0x2800)]
    [InlineData(0x10, 0, 2, 0x1001)]
    [InlineData(0x10, 0, 1, 0x2800)]
    public void RelativeBranchesChangeWzOnlyWhenTaken(int opcode, int flags, int b, int expected)
    {
        var bus = new RecordingBus();
        bus.Memory[0x1000] = (byte)opcode;
        bus.Memory[0x1001] = 0xFF;
        var cpu = new Z80Cpu(bus, new Z80Registers { PC = 0x1000, F = (byte)flags, B = (byte)b, WZ = 0x2800 });
        cpu.Step();
        Assert.Equal((ushort)expected, cpu.Registers.WZ);
    }

    [Theory]
    [InlineData(0xC9)]
    [InlineData(0xC0)]
    [InlineData(0xC8)]
    [InlineData(0xD0)]
    [InlineData(0xD8)]
    [InlineData(0xE0)]
    [InlineData(0xE8)]
    [InlineData(0xF0)]
    [InlineData(0xF8)]
    public void ReturnsChangeWzOnlyWhenTaken(int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)opcode;
        bus.Memory[0x8000] = 0x34;
        bus.Memory[0x8001] = 0x28;
        var cpu = new Z80Cpu(bus);
        for (int flags = 0; flags < 256; flags++)
        {
            cpu.Reset();
            cpu.Registers.SP = 0x8000;
            cpu.Registers.F = (byte)flags;
            cpu.Registers.WZ = 0x1111;
            bus.Events.Clear();
            cpu.Step();
            bool[] conditions = [(flags & 0x40) == 0, (flags & 0x40) != 0,
                (flags & 1) == 0, (flags & 1) != 0, (flags & 4) == 0, (flags & 4) != 0,
                (flags & 0x80) == 0, (flags & 0x80) != 0];
            bool taken = opcode == 0xC9 || conditions[(opcode - 0xC0) / 8];
            Assert.Equal(taken ? (ushort)0x2834 : (ushort)0x1111, cpu.Registers.WZ);
        }
    }

    [Theory]
    [InlineData(0xC7, 0)]
    [InlineData(0xCF, 8)]
    [InlineData(0xD7, 16)]
    [InlineData(0xDF, 24)]
    [InlineData(0xE7, 32)]
    [InlineData(0xEF, 40)]
    [InlineData(0xF7, 48)]
    [InlineData(0xFF, 56)]
    public void RestartSetsWzToLiteralVector(int opcode, int expected)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)opcode;
        var cpu = new Z80Cpu(bus, new Z80Registers { WZ = 0x2800 });
        cpu.Step();
        Assert.Equal((ushort)expected, cpu.Registers.WZ);
    }

    [Theory]
    [InlineData(0x45)]
    [InlineData(0x4D)]
    public void ExtendedReturnsSetWzToPoppedPc(int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xED;
        bus.Memory[1] = (byte)opcode;
        bus.Memory[0x8000] = 0x34;
        bus.Memory[0x8001] = 0x28;
        var cpu = new Z80Cpu(bus, new Z80Registers { SP = 0x8000, WZ = 0x1111 });
        cpu.Step();
        Assert.Equal((ushort)0x2834, cpu.Registers.WZ);
    }

    [Fact]
    public void ExchangeSetsWzToPoppedWord()
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xE3;
        bus.Memory[0x8000] = 0x34;
        bus.Memory[0x8001] = 0x28;
        var cpu = new Z80Cpu(bus, new Z80Registers { SP = 0x8000, HL = 0x1234, WZ = 0x1111 });
        cpu.Step();
        Assert.Equal((ushort)0x2834, cpu.Registers.WZ);
    }

    [Theory]
    [InlineData(-1, 0x66)]
    [InlineData(1, 0x38)]
    [InlineData(2, 0x2834)]
    public void InterruptResponsesSetWzToHandlerPc(int mode, int expected)
    {
        var bus = new RecordingBus { InterruptAcknowledging = _ => 0x20 };
        bus.Memory[0x1220] = 0x34;
        bus.Memory[0x1221] = 0x28;
        var cpu = new Z80Cpu(bus, new Z80Registers { I = 0x12, Iff1 = true, InterruptMode = (byte)Math.Max(mode, 0), WZ = 0x1111 });
        if (mode == -1) cpu.SetNmiLine(true);
        else cpu.SetInterruptLine(true);
        cpu.Step();
        Assert.Equal((ushort)expected, cpu.Registers.WZ);
    }

    [Fact]
    public void ExistingNonWritersPreserveWzAndResetClearsIt()
    {
        // Literal single-byte encodings that preserve WZ; ranges are literal LD/byte-ALU table rows.
        int[] singles = [0x00, 0x01, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
            0x11, 0x13, 0x14, 0x15, 0x16, 0x17, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
            0x21, 0x23, 0x24, 0x25, 0x26, 0x27, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F,
            0x31, 0x33, 0x34, 0x35, 0x36, 0x3B, 0x3C, 0x3D, 0x3E,
            0xC1, 0xC5, 0xC6, 0xCE, 0xD1, 0xD5, 0xD6, 0xD9, 0xDE,
            0xE1, 0xE5, 0xE6, 0xE9, 0xEB, 0xEE, 0xF1, 0xF3, 0xF5, 0xF6, 0xF9, 0xFB, 0xFE];
        foreach (int opcode in singles.Concat(Enumerable.Range(0x40, 128)))
        {
            var bus = new RecordingBus();
            bus.Memory[0] = (byte)opcode;
            var cpu = new Z80Cpu(bus, new Z80Registers { HL = 0x4000, SP = 0x8000, WZ = 0x2800 });
            cpu.Step();
            Assert.Equal((ushort)0x2800, cpu.Registers.WZ);
            cpu.Reset();
            Assert.Equal((ushort)0, cpu.Registers.WZ);
        }
        foreach (byte payload in new byte[] { 0x44, 0x46, 0x56, 0x5E })
        {
            var bus = new RecordingBus();
            bus.Memory[0] = 0xED;
            bus.Memory[1] = payload;
            var cpu = new Z80Cpu(bus, new Z80Registers { WZ = 0x2800 });
            cpu.Step();
            Assert.Equal((ushort)0x2800, cpu.Registers.WZ);
        }
    }

    [Fact]
    public void FollowingBitReadsWzHistoryThroughNonWritingInstructions()
    {
        var bus = new RecordingBus();
        byte[] program = [0x3A, 0xFF, 0x27, 0x21, 0x00, 0x40, 0xCB, 0x46];
        program.CopyTo(bus.Memory, 0);
        bus.Memory[0x4000] = 1;
        var cpu = new Z80Cpu(bus);
        Assert.Equal(13UL, cpu.Step());
        Assert.Equal(10UL, cpu.Step());
        Assert.Equal(12UL, cpu.Step());
        Assert.Equal((ushort)0x2800, cpu.Registers.WZ);
        Assert.Equal((byte)0x38, cpu.Registers.F);
    }

    [Theory]
    [InlineData(0xCD, Z80BusCycleKind.MemoryWrite, 0x7FFF, 0x2834)]
    [InlineData(0xC9, Z80BusCycleKind.MemoryRead, 0x8001, 0x1111)]
    [InlineData(0x22, Z80BusCycleKind.MemoryWrite, 0x2835, 0x1111)]
    public void FailuresRetainWzAtTheDeclaredCommitPoint(int opcode, Z80BusCycleKind kind, int address, int expected)
    {
        var bus = new RecordingBus
        {
            WaitStates = cycle => cycle.Kind == kind && cycle.Address == address
                ? throw new InvalidOperationException("WZ transfer failure") : 0
        };
        bus.Memory[0] = (byte)opcode;
        bus.Memory[1] = 0x34;
        bus.Memory[2] = 0x28;
        bus.Memory[0x8000] = 0x34;
        var cpu = new Z80Cpu(bus, new Z80Registers { SP = 0x8000, WZ = 0x1111 });
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
        Assert.True(cpu.IsFaulted);
        Assert.Equal((ushort)expected, cpu.Registers.WZ);
    }
}
