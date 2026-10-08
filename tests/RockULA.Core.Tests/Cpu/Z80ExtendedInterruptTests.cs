using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80ExtendedInterruptTests
{
    // Zilog UM008011-0816 pp.184–186, 288–291; netlist RETI/RETN and IM2 traces.
    [Theory]
    [InlineData(0x46, 0)]
    [InlineData(0x56, 1)]
    [InlineData(0x5E, 2)]
    public void ModeSelectionPreservesFlagsAndIffsAndFetchesTwoBytes(int opcode, int mode)
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFF] = 0xED;
        bus.Memory[0] = (byte)opcode;
        var registers = new Z80Registers();
        var cpu = new Z80Cpu(bus, registers);
        for (int flags = 0; flags < 256; flags++)
        {
            for (int iff = 0; iff < 4; iff++)
            {
                cpu.Reset();
                registers.PC = 0xFFFF;
                registers.R = 0xFF;
                registers.AF = (ushort)(0xA500 | flags);
                registers.Iff1 = (iff & 1) != 0;
                registers.Iff2 = (iff & 2) != 0;
                registers.InterruptMode = 255;
                bus.Events.Clear();
                ulong start = bus.TStates;
                Assert.Equal(8UL, cpu.Step());
                Assert.Equal((byte)mode, registers.InterruptMode);
                Assert.Equal((ushort)1, registers.PC);
                Assert.Equal((byte)0x81, registers.R);
                Assert.Equal((ushort)(0xA500 | flags), registers.AF);
                Assert.Equal((iff & 1) != 0, registers.Iff1);
                Assert.Equal((iff & 2) != 0, registers.Iff2);
                Assert.Equal(new CycleEvent[]
                {
                    new(Z80BusCycleKind.OpcodeFetch, 0xFFFF, start, start + 3, start + 4, 0xED),
                    new(Z80BusCycleKind.OpcodeFetch, 0, start + 4, start + 7, start + 8, (byte)opcode)
                }, bus.Events.ToArray());
            }
        }
    }

    [Theory]
    [InlineData(0x45)]
    [InlineData(0x4D)]
    public void ReturnsRestoreIff1FromIff2PreserveFlagsAndWrapStack(int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0x1000] = 0xED;
        bus.Memory[0x1001] = (byte)opcode;
        bus.Memory[0xFFFF] = 0x34;
        bus.Memory[0] = 0x12;
        var registers = new Z80Registers();
        var cpu = new Z80Cpu(bus, registers);
        for (int flags = 0; flags < 256; flags++)
        {
            for (int iff = 0; iff < 4; iff++)
            {
                cpu.Reset();
                registers.PC = 0x1000;
                registers.SP = 0xFFFF;
                registers.AF = (ushort)(0x5A00 | flags);
                registers.Iff1 = (iff & 1) != 0;
                registers.Iff2 = (iff & 2) != 0;
                bus.Events.Clear();
                bus.RetiNotifications.Clear();
                ulong start = bus.TStates;
                Assert.Equal(14UL, cpu.Step());
                Assert.Equal((ushort)0x1234, registers.PC);
                Assert.Equal((ushort)1, registers.SP);
                Assert.Equal((byte)2, registers.R);
                Assert.Equal((ushort)(0x5A00 | flags), registers.AF);
                Assert.Equal((iff & 2) != 0, registers.Iff1);
                Assert.Equal((iff & 2) != 0, registers.Iff2);
                Assert.Equal(opcode == 0x4D ? new[] { start + 14 } : Array.Empty<ulong>(), bus.RetiNotifications.ToArray());
                Assert.Equal(new CycleEvent[]
                {
                    new(Z80BusCycleKind.OpcodeFetch, 0x1000, start, start + 3, start + 4, 0xED),
                    new(Z80BusCycleKind.OpcodeFetch, 0x1001, start + 4, start + 7, start + 8, (byte)opcode),
                    new(Z80BusCycleKind.MemoryRead, 0xFFFF, start + 8, start + 11, start + 11, 0x34),
                    new(Z80BusCycleKind.MemoryRead, 0, start + 11, start + 14, start + 14, 0x12)
                }, bus.Events.ToArray());
            }
        }
    }

    [Fact]
    public void AllOtherEdPayloadsFaultAfterBothFetchesWithOriginalAddressAndBytes()
    {
        for (int opcode = 0; opcode < 256; opcode++)
        {
            if (opcode is 0x44 or 0x46 or 0x56 or 0x5E or 0x45 or 0x4D) continue;
            var bus = new RecordingBus();
            bus.Memory[0xFFFF] = 0xED;
            bus.Memory[0] = (byte)opcode;
            var registers = new Z80Registers { PC = 0xFFFF, F = 0xFF };
            var cpu = new Z80Cpu(bus, registers);
            var error = Assert.Throws<UnsupportedOpcodeException>(() => cpu.Step());
            Assert.Equal((ushort)0xFFFF, error.Address);
            Assert.Equal((byte)0xED, error.Prefix);
            Assert.Equal((byte)opcode, error.Opcode);
            Assert.Equal((ushort)1, registers.PC);
            Assert.Equal((byte)2, registers.R);
            Assert.Equal((byte)0xFF, registers.F);
            Assert.Equal(8UL, bus.TStates);
            Assert.Equal(2, bus.Events.Count);
            Assert.True(cpu.IsFaulted);
        }
    }

    [Fact]
    public void NmiArrivingDuringEdPrefixWaitsForTheWholeInstruction()
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xED;
        bus.Memory[1] = 0x56;
        var cpu = new Z80Cpu(bus);
        bus.Advancing = time =>
        {
            if (time == 3) cpu.SetNmiLine(true);
        };
        Assert.Equal(8UL, cpu.Step());
        Assert.Equal((byte)1, cpu.Registers.InterruptMode);
        Assert.Equal((ushort)2, cpu.Registers.PC);
        Assert.True(cpu.IsNmiPending);
        Assert.Equal(11UL, cpu.Step());
        Assert.Equal((byte)2, bus.Memory[0xFFFD]);
    }

    [Theory]
    [InlineData(0x45)]
    [InlineData(0x4D)]
    public void FailedHighStackReadRetainsLowPopButDoesNotRestoreIffOrNotify(int opcode)
    {
        var bus = new RecordingBus
        {
            WaitStates = cycle => cycle.Kind == Z80BusCycleKind.MemoryRead && cycle.Address == 0x8001
                ? throw new InvalidOperationException("test read failure") : 0
        };
        bus.Memory[0] = 0xED;
        bus.Memory[1] = (byte)opcode;
        bus.Memory[0x8000] = 0x34;
        var registers = new Z80Registers { SP = 0x8000, Iff1 = false, Iff2 = true };
        var cpu = new Z80Cpu(bus, registers);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
        Assert.Equal((ushort)0x8001, registers.SP);
        Assert.Equal((ushort)2, registers.PC);
        Assert.False(registers.Iff1);
        Assert.True(registers.Iff2);
        Assert.Empty(bus.RetiNotifications);
        Assert.Equal(11UL, bus.TStates);
        Assert.True(cpu.IsFaulted);
    }

    [Fact]
    public void ReturnWaitsShiftBothFetchesAndStackReads()
    {
        var bus = new RecordingBus { WaitStates = _ => 1 };
        bus.Memory[0] = 0xED;
        bus.Memory[1] = 0x4D;
        bus.Memory[0x8000] = 0x34;
        bus.Memory[0x8001] = 0x12;
        var cpu = new Z80Cpu(bus, new Z80Registers { SP = 0x8000 });
        Assert.Equal(18UL, cpu.Step());
        Assert.Equal(new ulong[] { 18 }, bus.RetiNotifications.ToArray());
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0, 0, 4, 5, 0xED),
            new(Z80BusCycleKind.OpcodeFetch, 1, 5, 9, 10, 0x4D),
            new(Z80BusCycleKind.MemoryRead, 0x8000, 10, 14, 14, 0x34),
            new(Z80BusCycleKind.MemoryRead, 0x8001, 14, 18, 18, 0x12)
        }, bus.Events.ToArray());
    }

    [Theory]
    [InlineData(0x12, 0x34, 0x1234, 0x1235)]
    [InlineData(0xFF, 0xFF, 0xFFFF, 0)]
    [InlineData(0x00, 0x01, 1, 2)]
    public void Im2UsesFullVectorBytePushesBeforeReadsAndWrapsVector(int i, int vector, int lowAddress, int highAddress)
    {
        var bus = new RecordingBus { InterruptAcknowledging = _ => (byte)vector };
        bus.Memory[lowAddress] = 0x78;
        bus.Memory[highAddress] = 0x56;
        var registers = new Z80Registers
        {
            PC = 0xABCD,
            SP = 0x8000,
            I = (byte)i,
            R = 0xFF,
            F = 0xFF,
            Iff1 = true,
            Iff2 = true,
            InterruptMode = 2
        };
        var cpu = new Z80Cpu(bus, registers);
        cpu.SetInterruptLine(true);
        Assert.Equal(19UL, cpu.Step());
        Assert.Equal((ushort)0x5678, registers.PC);
        Assert.Equal((ushort)0x7FFE, registers.SP);
        Assert.Equal((byte)0x80, registers.R);
        Assert.Equal((byte)0xFF, registers.F);
        Assert.False(registers.Iff1);
        Assert.False(registers.Iff2);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.InterruptAcknowledge, 0xABCD, 0, 5, 6, (byte)vector),
            new(Z80BusCycleKind.Internal, 0xABCD, 6, 6, 7, 0),
            new(Z80BusCycleKind.MemoryWrite, 0x7FFF, 7, 10, 10, 0xAB),
            new(Z80BusCycleKind.MemoryWrite, 0x7FFE, 10, 13, 13, 0xCD),
            new(Z80BusCycleKind.MemoryRead, (ushort)lowAddress, 13, 16, 16, 0x78),
            new(Z80BusCycleKind.MemoryRead, (ushort)highAddress, 16, 19, 19, 0x56)
        }, bus.Events.ToArray());
    }

    [Fact]
    public void Im2VectorReadsSeeStackWritesWhenTableOverlapsStack()
    {
        var bus = new RecordingBus { InterruptAcknowledging = _ => 0xFE };
        var cpu = new Z80Cpu(bus, new Z80Registers { PC = 0x1234, SP = 0x8000, I = 0x7F, Iff1 = true, InterruptMode = 2 });
        cpu.SetInterruptLine(true);
        Assert.Equal(19UL, cpu.Step());
        Assert.Equal((ushort)0x1234, cpu.Registers.PC);
    }

    [Fact]
    public void Im2WaitsSampleLiveAckAndVectorData()
    {
        byte vector = 0;
        var bus = new RecordingBus { WaitStates = cycle => cycle.Kind == Z80BusCycleKind.Internal ? 0 : 1 };
        bus.InterruptAcknowledging = _ => vector;
        bus.Advancing = time =>
        {
            if (time == 6) vector = 0x20;
            if (time == 20) bus.Memory[0x1220] = 0x78;
            if (time == 24) bus.Memory[0x1221] = 0x56;
        };
        var cpu = new Z80Cpu(bus, new Z80Registers { I = 0x12, SP = 0x8000, Iff1 = true, InterruptMode = 2 });
        cpu.SetInterruptLine(true);
        Assert.Equal(24UL, cpu.Step());
        Assert.Equal((ushort)0x5678, cpu.Registers.PC);
    }

    [Fact]
    public void Im2HighVectorReadFailureKeepsStackButDoesNotCommitTarget()
    {
        var bus = new RecordingBus
        {
            InterruptAcknowledging = _ => 0x20,
            WaitStates = cycle => cycle.Kind == Z80BusCycleKind.MemoryRead && cycle.Address == 0x1221
                ? throw new InvalidOperationException("vector failure") : 0
        };
        var registers = new Z80Registers { PC = 0xABCD, SP = 0x8000, I = 0x12, Iff1 = true, InterruptMode = 2 };
        var cpu = new Z80Cpu(bus, registers);
        cpu.SetInterruptLine(true);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
        Assert.True(cpu.IsFaulted);
        Assert.Equal((ushort)0xABCD, registers.PC);
        Assert.Equal((ushort)0x7FFE, registers.SP);
        Assert.Equal((byte)0xCD, bus.Memory[0x7FFE]);
        Assert.Equal((byte)0xAB, bus.Memory[0x7FFF]);
        Assert.Equal(16UL, bus.TStates);
        Assert.False(registers.Iff1);
    }

    [Fact]
    public void NmiRetnResumesInterruptedCodeAndRestoresMaskableEnable()
    {
        var bus = new RecordingBus();
        bus.Memory[0x66] = 0xED;
        bus.Memory[0x67] = 0x45;
        var registers = new Z80Registers { PC = 0x1234, Iff1 = true, Iff2 = true };
        var cpu = new Z80Cpu(bus, registers);
        cpu.SetNmiLine(true);
        Assert.Equal(11UL, cpu.Step());
        Assert.Equal(14UL, cpu.Step());
        Assert.Equal((ushort)0x1234, registers.PC);
        Assert.True(registers.Iff1);
        Assert.Equal((ushort)0xFFFF, registers.SP);
        Assert.Empty(bus.RetiNotifications);
    }

    [Fact]
    public void ProgramSelectsIm2HaltsAndReturnsThroughEiRetiWithHeldInt()
    {
        var bus = new RecordingBus { InterruptAcknowledging = _ => 0x20 };
        byte[] program = [0xED, 0x5E, 0xFB, 0x76];
        program.CopyTo(bus.Memory, 0);
        bus.Memory[0x1220] = 0x00;
        bus.Memory[0x1221] = 0x40;
        bus.Memory[0x4000] = 0xFB;
        bus.Memory[0x4001] = 0xED;
        bus.Memory[0x4002] = 0x4D;
        var registers = new Z80Registers { I = 0x12 };
        var cpu = new Z80Cpu(bus, registers);
        cpu.SetInterruptLine(true);
        Assert.Equal(8UL, cpu.Step());
        Assert.Equal(4UL, cpu.Step());
        Assert.Equal(4UL, cpu.Step());
        Assert.True(cpu.IsHalted);
        Assert.Equal(19UL, cpu.Step());
        Assert.False(cpu.IsHalted);
        Assert.Equal((byte)4, bus.Memory[0xFFFD]);
        Assert.Equal(4UL, cpu.Step());
        Assert.Equal(14UL, cpu.Step());
        Assert.Equal((ushort)4, registers.PC);
        Assert.False(cpu.IsEiDelayActive);
        Assert.Equal(new ulong[] { 53 }, bus.RetiNotifications.ToArray());
        Assert.Equal(19UL, cpu.Step());
        Assert.Equal(2, bus.InterruptAcknowledgements);
    }
}
