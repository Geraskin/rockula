using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80InterruptEnableTests
{
    // Zilog UM008011-0816, printed pp.14, 181–183. Logical bus convention: 001d.
    [Theory]
    [InlineData(0xF3, false)]
    [InlineData(0xFB, true)]
    public void EnableInstructionsSetBothIffsPreserveFlagsAndTakeOneFetch(int opcode, bool enabled)
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFF] = (byte)opcode;
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
                registers.BC = 0x1234;
                registers.SP = 0x5678;
                registers.Iff1 = (iff & 1) != 0;
                registers.Iff2 = (iff & 2) != 0;
                registers.InterruptMode = 2;
                ulong start = bus.TStates;
                bus.Events.Clear();

                Assert.Equal(4UL, cpu.Step());
                Assert.Equal(enabled, registers.Iff1);
                Assert.Equal(enabled, registers.Iff2);
                Assert.Equal(enabled, cpu.IsEiDelayActive);
                Assert.False(cpu.IsHalted);
                Assert.Equal((ushort)(0xA500 | flags), registers.AF);
                Assert.Equal((ushort)0x1234, registers.BC);
                Assert.Equal((ushort)0x5678, registers.SP);
                Assert.Equal((byte)2, registers.InterruptMode);
                Assert.Equal((ushort)0, registers.PC);
                Assert.Equal((byte)0x80, registers.R);
                Assert.Equal(new CycleEvent(Z80BusCycleKind.OpcodeFetch, 0xFFFF,
                    start, start + 3, start + 4, (byte)opcode), Assert.Single(bus.Events));
            }
        }
    }

    [Theory]
    [InlineData(0x00, 4)]
    [InlineData(0x01, 10)]
    [InlineData(0xCD, 17)]
    [InlineData(0x76, 4)]
    public void EiDelayLastsThroughTheWholeFollowingInstruction(int nextOpcode, int nextCycles)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xFB;
        bus.Memory[1] = (byte)nextOpcode;
        bus.Memory[2] = 0x34;
        bus.Memory[3] = 0x12;
        var cpu = new Z80Cpu(bus);
        Assert.Equal(4UL, cpu.Step());
        Assert.True(cpu.IsEiDelayActive);
        bus.Advancing = _ => Assert.True(cpu.IsEiDelayActive);

        Assert.Equal((ulong)nextCycles, cpu.Step());

        Assert.False(cpu.IsEiDelayActive);
        Assert.True(cpu.Registers.Iff1);
        Assert.True(cpu.Registers.Iff2);
        Assert.Equal(nextOpcode == 0x76, cpu.IsHalted);
    }

    [Fact]
    public void RepeatedEiRenewsDelayAndDiCancelsIt()
    {
        var bus = new RecordingBus();
        byte[] program = [0xFB, 0xFB, 0x00, 0xFB, 0xF3, 0x00];
        program.CopyTo(bus.Memory, 0);
        var cpu = new Z80Cpu(bus);
        bool[] delay = [true, true, false, true, false, false];
        bool[] iff = [true, true, true, true, false, false];
        for (int step = 0; step < program.Length; step++)
        {
            Assert.Equal(4UL, cpu.Step());
            Assert.Equal(delay[step], cpu.IsEiDelayActive);
            Assert.Equal(iff[step], cpu.Registers.Iff1);
            Assert.Equal(iff[step], cpu.Registers.Iff2);
        }
    }

    [Theory]
    [InlineData(0xD3)]
    [InlineData(0xFB)]
    [InlineData(0xF3)]
    [InlineData(0x32)]
    public void HaltRepeatsBoundedFetchesIgnoresDataAndWrapsPcAndRefresh(int ignoredOpcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFF] = 0x76;
        bus.Memory[0] = (byte)ignoredOpcode;
        var registers = new Z80Registers
        {
            PC = 0xFFFF,
            R = 0xFE,
            AF = 0xA5FF,
            BC = 0x1234,
            Iff1 = true,
            Iff2 = false
        };
        var cpu = new Z80Cpu(bus, registers);
        Assert.Equal(4UL, cpu.Step());
        Assert.True(cpu.IsHalted);
        Assert.Equal((ushort)0, registers.PC);
        Assert.Equal((byte)0xFF, registers.R);
        Assert.Equal(4UL, cpu.Step());
        Assert.Equal(4UL, cpu.Step());
        Assert.True(cpu.IsHalted);
        Assert.False(cpu.IsFaulted);
        Assert.False(cpu.IsEiDelayActive);
        Assert.Equal((ushort)0, registers.PC);
        Assert.Equal((byte)0x81, registers.R);
        Assert.Equal((ushort)0xA5FF, registers.AF);
        Assert.Equal((ushort)0x1234, registers.BC);
        Assert.True(registers.Iff1);
        Assert.False(registers.Iff2);
        Assert.Equal((byte)ignoredOpcode, bus.Memory[0]);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0xFFFF, 0, 3, 4, 0x76),
            new(Z80BusCycleKind.OpcodeFetch, 0, 4, 7, 8, (byte)ignoredOpcode),
            new(Z80BusCycleKind.OpcodeFetch, 0, 8, 11, 12, (byte)ignoredOpcode)
        }, bus.Events.ToArray());
    }

    [Fact]
    public void HaltFetchUsesWaitsAndSamplesLiveMemoryOnEveryStep()
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0x76;
        var cpu = new Z80Cpu(bus);
        cpu.Step();
        bus.WaitStates = _ => 2;
        bus.Advancing = time => bus.Memory[1] = time == 9 ? (byte)0xD3 : (byte)0xFB;
        Assert.Equal(6UL, cpu.Step());
        Assert.Equal(6UL, cpu.Step());
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0, 0, 3, 4, 0x76),
            new(Z80BusCycleKind.OpcodeFetch, 1, 4, 9, 10, 0xD3),
            new(Z80BusCycleKind.OpcodeFetch, 1, 10, 15, 16, 0xFB)
        }, bus.Events.ToArray());
        Assert.True(cpu.IsHalted);
        Assert.False(cpu.Registers.Iff1);
        Assert.Equal((ushort)1, cpu.Registers.PC);
    }

    [Fact]
    public void FailedFollowingInstructionDoesNotRetireEiDelay()
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xFB;
        bus.Memory[1] = 0xD3;
        var cpu = new Z80Cpu(bus);
        cpu.Step();
        Assert.Throws<UnsupportedOpcodeException>(() => cpu.Step());
        Assert.True(cpu.IsFaulted);
        Assert.True(cpu.IsEiDelayActive);
        Assert.True(cpu.Registers.Iff1);
        Assert.Equal((ushort)2, cpu.Registers.PC);
        Assert.Equal(8UL, bus.TStates);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
    }

    [Fact]
    public void HaltBusFailureFaultsWithoutLeavingHaltAndResetClearsControlState()
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xFB;
        bus.Memory[1] = 0x76;
        var cpu = new Z80Cpu(bus);
        cpu.Step();
        cpu.Step();
        bus.WaitStates = _ => throw new InvalidOperationException("test bus failure");
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
        Assert.True(cpu.IsHalted);
        Assert.True(cpu.IsFaulted);
        Assert.Equal(8UL, bus.TStates);
        cpu.Reset();
        Assert.False(cpu.IsHalted);
        Assert.False(cpu.IsEiDelayActive);
        Assert.False(cpu.IsFaulted);
        Assert.False(cpu.Registers.Iff1);
        Assert.False(cpu.Registers.Iff2);
        Assert.Equal((ushort)0, cpu.Registers.PC);
        Assert.Equal(8UL, bus.TStates);
        Assert.Equal((byte)0x76, bus.Memory[1]);
        bus.WaitStates = null;
        Assert.Equal(4UL, cpu.Step());
        Assert.True(cpu.IsEiDelayActive);
        cpu.Reset();
        Assert.False(cpu.IsEiDelayActive);
    }
}
