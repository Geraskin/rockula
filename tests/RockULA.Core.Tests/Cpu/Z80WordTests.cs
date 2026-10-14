using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80WordTests
{
    // Zilog UM008011 pp.188–189,198,201; Young v0.90 §8.6 for high-byte X/Y.
    [Theory]
    [InlineData(0x03, "BC", 1)]
    [InlineData(0x13, "DE", 1)]
    [InlineData(0x23, "HL", 1)]
    [InlineData(0x33, "SP", 1)]
    [InlineData(0x0B, "BC", -1)]
    [InlineData(0x1B, "DE", -1)]
    [InlineData(0x2B, "HL", -1)]
    [InlineData(0x3B, "SP", -1)]
    public void WordIncrementDecrementExhaustValuesWithoutChangingFlags(int opcode, string pair, int delta)
    {
        var bus = new RecordingBus();
        bus.Memory[0x1000] = (byte)opcode;
        var r = new Z80Registers { A = 0x55, IX = 0x1234, IY = 0x5678, AlternateAF = 0xABCD };
        var cpu = new Z80Cpu(bus, r);
        var property = typeof(Z80Registers).GetProperty(pair)!;
        for (int value = 0; value < 65536; value++)
        {
            r.PC = 0x1000;
            r.R = 0x7F;
            r.BC = 0x1234;
            r.DE = 0x5678;
            r.HL = 0x9ABC;
            r.SP = 0xA000;
            r.F = (byte)value;
            property.SetValue(r, (ushort)value);
            bus.Events.Clear();
            ulong start = bus.TStates;
            ushort expected = unchecked((ushort)(value + delta));
            Assert.Equal(6UL, cpu.Step());
            Assert.Equal(pair == "BC" ? expected : (ushort)0x1234, r.BC);
            Assert.Equal(pair == "DE" ? expected : (ushort)0x5678, r.DE);
            Assert.Equal(pair == "HL" ? expected : (ushort)0x9ABC, r.HL);
            Assert.Equal(pair == "SP" ? expected : (ushort)0xA000, r.SP);
            Assert.Equal((byte)value, r.F);
            Assert.Equal((byte)0x55, r.A);
            Assert.Equal((ushort)0x1234, r.IX);
            Assert.Equal((ushort)0x5678, r.IY);
            Assert.Equal((ushort)0xABCD, r.AlternateAF);
            Assert.Equal((ushort)0x1001, r.PC);
            Assert.Equal((byte)0, r.R);
            Assert.Equal(2, bus.Events.Count);
            Assert.Equal(new CycleEvent(Z80BusCycleKind.Internal, 0x1000, start + 4, start + 4, start + 6, 0), bus.Events[1]);
        }
    }

    [Theory]
    [InlineData(0x09, "BC")]
    [InlineData(0x19, "DE")]
    [InlineData(0x29, "HL")]
    [InlineData(0x39, "SP")]
    public void WordAdditionSweepsHlAndCarryBoundariesWithSourceAliasing(int opcode, string pair)
    {
        var bus = new RecordingBus();
        bus.Memory[0x1000] = (byte)opcode;
        var r = new Z80Registers();
        var cpu = new Z80Cpu(bus, r);
        var property = typeof(Z80Registers).GetProperty(pair)!;
        int[] operands = pair == "HL" ? [0] : [0, 1, 0x0FFF, 0x1000, 0x7FFF, 0x8000, 0xFFFF];
        foreach (int operand in operands)
        {
            for (int hl = 0; hl < 65536; hl++)
            {
                r.PC = 0x1000;
                r.R = 0xFF;
                r.BC = 0x1234;
                r.DE = 0x5678;
                r.SP = 0xA000;
                r.HL = (ushort)hl;
                r.A = 0x55;
                r.F = (byte)hl;
                if (pair != "HL") property.SetValue(r, (ushort)operand);
                int source = pair == "HL" ? hl : operand;
                int sum = hl + source;
                ushort expected = unchecked((ushort)sum);
                // Mathematical carry tests, independent of production XOR identities.
                int expectedFlags = (hl & 0xC4) | ((expected / 256) & 0x28);
                if ((hl % 4096) + (source % 4096) > 4095) expectedFlags |= 0x10;
                if (sum > 65535) expectedFlags |= 1;
                bus.Events.Clear();
                ulong start = bus.TStates;
                Assert.Equal(11UL, cpu.Step());
                Assert.Equal(expected, r.HL);
                Assert.Equal((byte)expectedFlags, r.F);
                Assert.Equal(pair == "BC" ? (ushort)operand : (ushort)0x1234, r.BC);
                Assert.Equal(pair == "DE" ? (ushort)operand : (ushort)0x5678, r.DE);
                Assert.Equal(pair == "SP" ? (ushort)operand : (ushort)0xA000, r.SP);
                Assert.Equal((byte)0x55, r.A);
                Assert.Equal((ushort)0x1001, r.PC);
                Assert.Equal((byte)0x80, r.R);
                Assert.Equal(3, bus.Events.Count);
                Assert.Equal(new CycleEvent(Z80BusCycleKind.Internal, 0x1000, start + 4, start + 4, start + 8, 0), bus.Events[1]);
                Assert.Equal(new CycleEvent(Z80BusCycleKind.Internal, 0x1000, start + 8, start + 8, start + 11, 0), bus.Events[2]);
            }
        }
    }

    [Fact]
    public void AdditionPreservesSzPvForEveryFlagByteAndIgnoresInputCarry()
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0x09;
        var r = new Z80Registers();
        var cpu = new Z80Cpu(bus, r);
        for (int flags = 0; flags < 256; flags++)
        {
            r.PC = 0;
            r.HL = 0x0FFF;
            r.BC = 1;
            r.F = (byte)flags;
            bus.Events.Clear();
            Assert.Equal(11UL, cpu.Step());
            Assert.Equal((ushort)0x1000, r.HL);
            Assert.Equal((byte)((flags & 0xC4) | 0x10), r.F);
        }
    }

    [Fact]
    public void LoadSpFromHlWrapsPcPreservesFlagsAndAdvancesInternalWaits()
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFF] = 0xF9;
        bus.WaitStates = cycle => cycle.Kind == Z80BusCycleKind.Internal ? 3 : 0;
        var r = new Z80Registers { PC = 0xFFFF, HL = 0x1234, SP = 0xABCD, F = 0xFF, R = 0x7F };
        Assert.Equal(9UL, new Z80Cpu(bus, r).Step());
        Assert.Equal((ushort)0x1234, r.SP);
        Assert.Equal((ushort)0x1234, r.HL);
        Assert.Equal((byte)0xFF, r.F);
        Assert.Equal((ushort)0, r.PC);
        Assert.Equal((byte)0, r.R);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0xFFFF, 0, 3, 4, 0xF9),
            new(Z80BusCycleKind.Internal, 0xFFFF, 4, 7, 9, 0)
        }, bus.Events.ToArray());
    }
}
