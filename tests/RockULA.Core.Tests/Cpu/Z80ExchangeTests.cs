using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80ExchangeTests
{
    [Theory]
    [InlineData(0x08, 0xA5FF, 0x1234, 0x5678, 0x9ABC, 0x1122, 0x3344, 0x5566, 0x7788)]
    [InlineData(0xEB, 0x1122, 0x1234, 0x9ABC, 0x5678, 0xA5FF, 0x3344, 0x5566, 0x7788)]
    [InlineData(0xD9, 0x1122, 0x3344, 0x5566, 0x7788, 0xA5FF, 0x1234, 0x5678, 0x9ABC)]
    public void RegisterExchangesChangeOnlyTheirDeclaredPairs(int opcode, int af, int bc, int de, int hl,
        int alternateAf, int alternateBc, int alternateDe, int alternateHl)
    {
        // Literal full-state vectors from Zilog UM008011 pp.124–126.
        var bus = new RecordingBus();
        bus.Memory[0xFFFF] = (byte)opcode;
        var r = new Z80Registers
        {
            AF = 0x1122,
            BC = 0x1234,
            DE = 0x5678,
            HL = 0x9ABC,
            AlternateAF = 0xA5FF,
            AlternateBC = 0x3344,
            AlternateDE = 0x5566,
            AlternateHL = 0x7788,
            IX = 0x1357,
            IY = 0x2468,
            SP = 0x8000,
            PC = 0xFFFF,
            I = 0xAB,
            R = 0xFF,
            Iff1 = true,
            Iff2 = false,
            InterruptMode = 2
        };
        Assert.Equal(4UL, new Z80Cpu(bus, r).Step());
        Assert.Equal(new ushort[] { (ushort)af, (ushort)bc, (ushort)de, (ushort)hl,
                (ushort)alternateAf, (ushort)alternateBc, (ushort)alternateDe, (ushort)alternateHl },
            new[] { r.AF, r.BC, r.DE, r.HL, r.AlternateAF, r.AlternateBC, r.AlternateDE, r.AlternateHL });
        Assert.Equal((ushort)0x1357, r.IX);
        Assert.Equal((ushort)0x2468, r.IY);
        Assert.Equal((ushort)0x8000, r.SP);
        Assert.Equal((ushort)0, r.PC);
        Assert.Equal((byte)0x80, r.R);
        Assert.Equal((byte)0xAB, r.I);
        Assert.True(r.Iff1);
        Assert.False(r.Iff2);
        Assert.Equal((byte)2, r.InterruptMode);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0xFFFF, 0, 3, 4, (byte)opcode)
        }, bus.Events.ToArray());
    }

    [Theory]
    [InlineData(0x4000, 0)]
    [InlineData(0xFFFF, 0)]
    [InlineData(0xFFFF, 2)]
    public void StackExchangeReadsLowHighThenWritesHighLowWithWrappedAddress(int sp, int wait)
    {
        // Zilog p.127 total; Weissflog 2021 netlist EX (SP),HL trace for transfer order.
        var bus = new RecordingBus();
        bus.Memory[0x1000] = 0xE3;
        ushort highAddress = unchecked((ushort)(sp + 1));
        bus.Memory[sp] = 0x34;
        bus.Memory[highAddress] = 0x12;
        bus.WaitStates = cycle => cycle.Kind == Z80BusCycleKind.MemoryWrite ? wait : 0;
        var r = new Z80Registers { PC = 0x1000, SP = (ushort)sp, HL = 0xABCD, F = 0xFF, BC = 0x5678 };
        Assert.Equal((ulong)(19 + wait * 2), new Z80Cpu(bus, r).Step());
        Assert.Equal((ushort)0x1234, r.HL);
        Assert.Equal((ushort)sp, r.SP);
        Assert.Equal((ushort)0x5678, r.BC);
        Assert.Equal((byte)0xFF, r.F);
        Assert.Equal((ushort)0x1001, r.PC);
        Assert.Equal((byte)1, r.R);
        Assert.Equal((byte)0xCD, bus.Memory[sp]);
        Assert.Equal((byte)0xAB, bus.Memory[highAddress]);
        ulong firstEnd = (ulong)(14 + wait);
        ulong secondEnd = (ulong)(17 + wait * 2);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0x1000, 0, 3, 4, 0xE3),
            new(Z80BusCycleKind.MemoryRead, (ushort)sp, 4, 7, 7, 0x34),
            new(Z80BusCycleKind.MemoryRead, highAddress, 7, 10, 10, 0x12),
            new(Z80BusCycleKind.Internal, highAddress, 10, 10, 11, 0),
            new(Z80BusCycleKind.MemoryWrite, highAddress, 11, firstEnd, firstEnd, 0xAB),
            new(Z80BusCycleKind.MemoryWrite, (ushort)sp, firstEnd, secondEnd, secondEnd, 0xCD),
            new(Z80BusCycleKind.Internal, (ushort)sp, secondEnd, secondEnd, secondEnd + 2, 0)
        }, bus.Events.ToArray());
    }

    [Fact]
    public void StackExchangeRetainsPartialMemoryWriteAndLatchesFailure()
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xE3;
        bus.Memory[0x4000] = 0x34;
        bus.Memory[0x4001] = 0x12;
        bus.WaitStates = cycle => cycle.Kind == Z80BusCycleKind.MemoryWrite && cycle.Address == 0x4000
            ? throw new InvalidOperationException("Synthetic second-write failure") : 0;
        var r = new Z80Registers { SP = 0x4000, HL = 0xABCD, F = 0xFF };
        var cpu = new Z80Cpu(bus, r);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
        Assert.True(cpu.IsFaulted);
        Assert.Equal((byte)0xAB, bus.Memory[0x4001]);
        Assert.Equal((byte)0x34, bus.Memory[0x4000]);
        Assert.Equal((ushort)0x4000, r.SP);
        Assert.Equal((ushort)0xABCD, r.HL);
        Assert.Equal(14UL, bus.TStates);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
        Assert.Equal(14UL, bus.TStates);
    }
}
