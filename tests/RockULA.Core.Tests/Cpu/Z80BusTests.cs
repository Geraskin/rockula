using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80BusTests
{
    [Fact]
    public void FetchSamplesAfterAdvancingAndThenCompletesItsRemainingTime()
    {
        var bus = new RecordingBus();
        bus.Memory[0x1234] = 0x11;
        var advances = new List<ulong>();
        bus.Advancing = time =>
        {
            advances.Add(time);
            if (time == 3)
            {
                bus.Memory[0x1234] = 0x42;
            }
        };

        byte value = bus.Execute(new Z80BusCycle(Z80BusCycleKind.OpcodeFetch, 0x1234, 4, 3));

        Assert.Equal((byte)0x42, value);
        Assert.Equal(4UL, bus.TStates);
        Assert.Equal(new ulong[] { 3, 4 }, advances.ToArray());
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0x1234, 0, 3, 4, 0x42)
        }, bus.Events.ToArray());
    }

    [Fact]
    public void WriteBecomesVisibleAtTransferRatherThanAtTransactionStart()
    {
        var bus = new RecordingBus();
        bus.Memory[0x8000] = 0x11;
        var observed = new List<(ulong Time, byte Value)>();
        bus.Advancing = time => observed.Add((time, bus.Memory[0x8000]));

        bus.Execute(new Z80BusCycle(Z80BusCycleKind.MemoryWrite, 0x8000, 3, 3, 0x42));

        Assert.Equal(new (ulong Time, byte Value)[] { (3, 0x11) }, observed.ToArray());
        Assert.Equal((byte)0x42, bus.Memory[0x8000]);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.MemoryWrite, 0x8000, 0, 3, 3, 0x42)
        }, bus.Events.ToArray());
    }

    [Fact]
    public void WaitStatesChangeSampledDataAndNotOnlyTheFinalCycleCount()
    {
        var bus = new RecordingBus
        {
            WaitStates = cycle => cycle.Kind == Z80BusCycleKind.MemoryRead ? 2 : 0
        };
        bus.Memory[0] = 0x06;
        bus.Memory[1] = 0x42;
        bus.Advancing = time =>
        {
            if (time >= 8)
            {
                bus.Memory[1] = 0x77;
            }
        };
        var cpu = new Z80Cpu(bus);

        Assert.Equal(9UL, cpu.Step());
        Assert.Equal((byte)0x77, cpu.Registers.B);
        Assert.Equal((byte)1, cpu.Registers.R);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0, 0, 3, 4, 0x06),
            new(Z80BusCycleKind.MemoryRead, 1, 4, 9, 9, 0x77)
        }, bus.Events.ToArray());
    }

    [Fact]
    public void FetchWaitStatesShiftBothTransferAndCompletion()
    {
        var bus = new RecordingBus { WaitStates = _ => 2 };
        bus.Memory[0] = 0;
        var cpu = new Z80Cpu(bus);

        Assert.Equal(6UL, cpu.Step());
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0, 0, 5, 6, 0)
        }, bus.Events.ToArray());
    }

    [Fact]
    public void InternalCyclesAdvanceTimeWithoutReadingOrWritingMemory()
    {
        var bus = new RecordingBus();
        bus.Memory[0x8000] = 0x42;

        byte value = bus.Execute(new Z80BusCycle(Z80BusCycleKind.Internal, 0x8000, 2, 0));

        Assert.Equal((byte)0, value);
        Assert.Equal((byte)0x42, bus.Memory[0x8000]);
        Assert.Equal(2UL, bus.TStates);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.Internal, 0x8000, 0, 0, 2, 0)
        }, bus.Events.ToArray());
    }

    [Fact]
    public void UnwiredAcknowledgeReturnsHighDataWithoutAccessingMemory()
    {
        var bus = new NoMemoryBus();
        Assert.Equal((byte)0xFF, bus.Execute(
            new Z80BusCycle(Z80BusCycleKind.InterruptAcknowledge, 0x1234, 6, 5)));
        Assert.Equal(6UL, bus.TStates);
    }

    private sealed class NoMemoryBus : Z80Bus
    {
        protected override byte ReadMemory(ushort address) => throw new InvalidOperationException("Unexpected read");

        protected override void WriteMemory(ushort address, byte value) => throw new InvalidOperationException("Unexpected write");
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 4, -1)]
    [InlineData(0, 4, 5)]
    [InlineData(99, 4, 3)]
    public void InvalidTransactionsDoNotAdvanceTimeOrTouchMemory(int kind, int duration, int offset)
    {
        var bus = new RecordingBus();
        var cycle = new Z80BusCycle((Z80BusCycleKind)kind, 0x8000, duration, offset, 0x42);

        Assert.Throws<ArgumentOutOfRangeException>(() => bus.Execute(cycle));
        Assert.Equal(0UL, bus.TStates);
        Assert.Equal((byte)0, bus.Memory[0x8000]);
        Assert.Empty(bus.Events);
    }

    [Fact]
    public void NegativeWaitStatesAreRejectedBeforeAnyEffect()
    {
        var bus = new RecordingBus { WaitStates = _ => -1 };

        Assert.Throws<InvalidOperationException>(() =>
            bus.Execute(new Z80BusCycle(Z80BusCycleKind.MemoryWrite, 0x8000, 3, 3, 0x42)));
        Assert.Equal(0UL, bus.TStates);
        Assert.Equal((byte)0, bus.Memory[0x8000]);
        Assert.Empty(bus.Events);
    }
}
