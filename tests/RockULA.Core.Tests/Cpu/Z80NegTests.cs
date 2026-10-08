using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80NegTests
{
    // Zilog UM008011-0816 pp.176–177; Young/Jan v0.90 §2.2 for result X/Y.
    [Fact]
    public void NegChecksEveryAccumulatorAndIncomingFlags()
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xED;
        bus.Memory[1] = 0x44;
        var cpu = new Z80Cpu(bus);
        for (int value = 0; value < 256; value++)
        {
            int result = (256 - value) % 256;
            int expected = (result & 0xA8) | 2;
            if (result == 0) expected |= 0x40;
            if (value % 16 != 0) expected |= 0x10;
            if (value == 128) expected |= 4;
            if (value != 0) expected |= 1;
            for (int flags = 0; flags < 256; flags++)
            {
                cpu.Reset();
                cpu.Registers.AF = (ushort)((value << 8) | flags);
                cpu.Registers.BC = 0x1234;
                cpu.Registers.Iff1 = true;
                cpu.Registers.Iff2 = true;
                cpu.Registers.InterruptMode = 2;
                bus.Events.Clear();
                Assert.Equal(8UL, cpu.Step());
                Assert.Equal((ushort)((result << 8) | expected), cpu.Registers.AF);
                Assert.Equal((ushort)0x1234, cpu.Registers.BC);
                Assert.Equal((ushort)0xFFFF, cpu.Registers.SP);
                Assert.True(cpu.Registers.Iff1);
                Assert.True(cpu.Registers.Iff2);
                Assert.Equal((byte)2, cpu.Registers.InterruptMode);
                Assert.Equal(2, bus.Events.Count);
            }
        }
    }

    [Fact]
    public void NegWrapsFetchAndRefreshAndAppliesWaits()
    {
        var bus = new RecordingBus { WaitStates = _ => 1 };
        bus.Memory[0xFFFF] = 0xED;
        bus.Memory[0] = 0x44;
        var cpu = new Z80Cpu(bus, new Z80Registers { PC = 0xFFFF, R = 0xFF, A = 1 });
        Assert.Equal(10UL, cpu.Step());
        Assert.Equal((ushort)1, cpu.Registers.PC);
        Assert.Equal((byte)0x81, cpu.Registers.R);
        Assert.Equal((ushort)0xFFBB, cpu.Registers.AF);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0xFFFF, 0, 4, 5, 0xED),
            new(Z80BusCycleKind.OpcodeFetch, 0, 5, 9, 10, 0x44)
        }, bus.Events.ToArray());
    }

    [Fact]
    public void FailedPayloadFetchDoesNotChangeAccumulatorOrFlags()
    {
        var bus = new RecordingBus
        {
            WaitStates = cycle => cycle.Address == 1 ? throw new InvalidOperationException("payload failure") : 0
        };
        bus.Memory[0] = 0xED;
        bus.Memory[1] = 0x44;
        var cpu = new Z80Cpu(bus, new Z80Registers { AF = 0x80FF });
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
        Assert.Equal((ushort)0x80FF, cpu.Registers.AF);
        Assert.Equal((ushort)1, cpu.Registers.PC);
        Assert.Equal((byte)1, cpu.Registers.R);
        Assert.Equal(4UL, bus.TStates);
        Assert.True(cpu.IsFaulted);
    }
}
