using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80ControlTests
{
    public static IEnumerable<object[]> ConditionalEncodings()
    {
        // Zilog UM008011 pp.263–264,283–287. Literal opcodes/flag masks, not decoder output.
        byte[][] rows =
        [
            [0xC2, 0xCA, 0xD2, 0xDA, 0xE2, 0xEA, 0xF2, 0xFA],
            [0xC4, 0xCC, 0xD4, 0xDC, 0xE4, 0xEC, 0xF4, 0xFC],
            [0xC0, 0xC8, 0xD0, 0xD8, 0xE0, 0xE8, 0xF0, 0xF8]
        ];
        string[] kinds = ["JP", "CALL", "RET"];
        byte[] masks = [0x40, 0x40, 1, 1, 4, 4, 0x80, 0x80];
        bool[] set = [false, true, false, true, false, true, false, true];
        for (int family = 0; family < rows.Length; family++)
        {
            for (int condition = 0; condition < 8; condition++)
            {
                yield return [rows[family][condition], kinds[family], masks[condition], set[condition]];
            }
        }
        yield return [0x20, "JR", (byte)0x40, false];
        yield return [0x28, "JR", (byte)0x40, true];
        yield return [0x30, "JR", (byte)1, false];
        yield return [0x38, "JR", (byte)1, true];
    }

    [Theory]
    [MemberData(nameof(ConditionalEncodings))]
    public void ConditionsObserveOnlyTheirFlagAndSequenceBothPaths(byte opcode, string kind, byte mask, bool whenSet)
    {
        for (int flags = 0; flags < 256; flags++)
        {
            var bus = new RecordingBus();
            bus.Memory[0x1234] = opcode;
            bus.Memory[0x1235] = 0xFE;
            bus.Memory[0x1236] = 0x56;
            bus.Memory[0xFFFF] = 0x78;
            bus.Memory[0] = 0x9A;
            var r = new Z80Registers { PC = 0x1234, SP = 0xFFFF, F = (byte)flags, A = 0xAB, BC = 0xCDEF };
            bool taken = ((flags & mask) != 0) == whenSet;
            ulong time = kind switch
            {
                "JP" => 10,
                "CALL" => taken ? 17UL : 10UL,
                "RET" => taken ? 11UL : 5UL,
                "JR" => taken ? 12UL : 7UL,
                _ => throw new InvalidOperationException()
            };
            ushort pc = kind switch
            {
                "JP" or "CALL" => taken ? (ushort)0x56FE : (ushort)0x1237,
                "RET" => taken ? (ushort)0x9A78 : (ushort)0x1235,
                "JR" => taken ? (ushort)0x1234 : (ushort)0x1236,
                _ => throw new InvalidOperationException()
            };
            Assert.Equal(time, new Z80Cpu(bus, r).Step());
            Assert.Equal(pc, r.PC);
            Assert.Equal((byte)flags, r.F);
            Assert.Equal((byte)0xAB, r.A);
            Assert.Equal((ushort)0xCDEF, r.BC);
            Assert.Equal((byte)1, r.R);
            Assert.Equal(kind == "CALL" && taken ? (ushort)0xFFFD
                : kind == "RET" && taken ? (ushort)1 : (ushort)0xFFFF, r.SP);
            var expected = new List<CycleEvent>
            {
                new(Z80BusCycleKind.OpcodeFetch, 0x1234, 0, 3, 4, opcode)
            };
            if (kind == "RET")
            {
                expected.Add(new(Z80BusCycleKind.Internal, 0x1234, 4, 4, 5, 0));
                if (taken)
                {
                    expected.Add(new(Z80BusCycleKind.MemoryRead, 0xFFFF, 5, 8, 8, 0x78));
                    expected.Add(new(Z80BusCycleKind.MemoryRead, 0, 8, 11, 11, 0x9A));
                }
            }
            else
            {
                expected.Add(new(Z80BusCycleKind.MemoryRead, 0x1235, 4, 7, 7, 0xFE));
                if (kind == "JR")
                {
                    if (taken) expected.Add(new(Z80BusCycleKind.Internal, 0x1235, 7, 7, 12, 0));
                }
                else
                {
                    expected.Add(new(Z80BusCycleKind.MemoryRead, 0x1236, 7, 10, 10, 0x56));
                    if (kind == "CALL" && taken)
                    {
                        expected.Add(new(Z80BusCycleKind.Internal, 0x1236, 10, 10, 11, 0));
                        expected.Add(new(Z80BusCycleKind.MemoryWrite, 0xFFFE, 11, 14, 14, 0x12));
                        expected.Add(new(Z80BusCycleKind.MemoryWrite, 0xFFFD, 14, 17, 17, 0x37));
                    }
                }
            }
            Assert.Equal(expected.ToArray(), bus.Events.ToArray());
            Assert.Equal(taken && kind == "CALL" ? (byte)0x12 : (byte)0, bus.Memory[0xFFFE]);
            Assert.Equal(taken && kind == "CALL" ? (byte)0x37 : (byte)0, bus.Memory[0xFFFD]);
        }
    }

    [Theory]
    [InlineData(0x18, 0xFFFF, 0x80, 0xFF81, 12, 0, 0)]
    [InlineData(0x18, 0xFFFE, 0x7F, 0x007F, 12, 0, 0)]
    [InlineData(0x18, 0, 0xFF, 1, 12, 0, 0)]
    [InlineData(0x10, 0xFFFF, 0x80, 0xFF81, 13, 0, 255)]
    [InlineData(0x10, 0, 0xFE, 2, 8, 1, 0)]
    [InlineData(0x10, 0, 0xFE, 0, 13, 2, 1)]
    public void RelativeBranchesUseSignedDisplacementAndDjnzWrapsB(
        int opcode, int start, int displacement, int expectedPc, int expectedTime, int b, int expectedB)
    {
        var bus = new RecordingBus();
        bus.Memory[start] = (byte)opcode;
        ushort operandAddress = unchecked((ushort)(start + 1));
        bus.Memory[operandAddress] = (byte)displacement;
        var r = new Z80Registers { PC = (ushort)start, B = (byte)b, F = 0xFF, R = 0x7F };
        Assert.Equal((ulong)expectedTime, new Z80Cpu(bus, r).Step());
        Assert.Equal((ushort)expectedPc, r.PC);
        Assert.Equal((byte)expectedB, r.B);
        Assert.Equal((byte)0xFF, r.F);
        Assert.Equal((byte)0, r.R);
        var expected = new List<CycleEvent>
        {
            new(Z80BusCycleKind.OpcodeFetch, (ushort)start, 0, 3, 4, (byte)opcode)
        };
        ulong readStart = 4;
        if (opcode == 0x10)
        {
            expected.Add(new(Z80BusCycleKind.Internal, (ushort)start, 4, 4, 5, 0));
            readStart = 5;
        }
        expected.Add(new(Z80BusCycleKind.MemoryRead, operandAddress, readStart, readStart + 3, readStart + 3, (byte)displacement));
        if (expectedTime > 8)
        {
            expected.Add(new(Z80BusCycleKind.Internal, operandAddress, readStart + 3, readStart + 3, readStart + 8, 0));
        }
        Assert.Equal(expected.ToArray(), bus.Events.ToArray());
    }

    [Theory]
    [InlineData(0xC5, 0xC1, "BC")]
    [InlineData(0xD5, 0xD1, "DE")]
    [InlineData(0xE5, 0xE1, "HL")]
    [InlineData(0xF5, 0xF1, "AF")]
    public void PushAndPopWrapStackWithHighFirstWriteAndLowFirstRead(int push, int pop, string pair)
    {
        var bus = new RecordingBus();
        bus.Memory[0x1000] = (byte)push;
        bus.Memory[0x1001] = (byte)pop;
        var r = new Z80Registers { PC = 0x1000, SP = 1, F = 0xA5 };
        var property = typeof(Z80Registers).GetProperty(pair)!;
        property.SetValue(r, (ushort)0xABCD);
        byte initialF = r.F;
        var cpu = new Z80Cpu(bus, r);
        Assert.Equal(11UL, cpu.Step());
        Assert.Equal((ushort)0xFFFF, r.SP);
        Assert.Equal((byte)initialF, r.F);
        property.SetValue(r, (ushort)0);
        Assert.Equal(10UL, cpu.Step());
        Assert.Equal((ushort)0xABCD, (ushort)property.GetValue(r)!);
        Assert.Equal((ushort)1, r.SP);
        Assert.Equal((ushort)0x1002, r.PC);
        Assert.Equal((byte)2, r.R);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0x1000, 0, 3, 4, (byte)push),
            new(Z80BusCycleKind.Internal, 0x1000, 4, 4, 5, 0),
            new(Z80BusCycleKind.MemoryWrite, 0, 5, 8, 8, 0xAB),
            new(Z80BusCycleKind.MemoryWrite, 0xFFFF, 8, 11, 11, 0xCD),
            new(Z80BusCycleKind.OpcodeFetch, 0x1001, 11, 14, 15, (byte)pop),
            new(Z80BusCycleKind.MemoryRead, 0xFFFF, 15, 18, 18, 0xCD),
            new(Z80BusCycleKind.MemoryRead, 0, 18, 21, 21, 0xAB)
        }, bus.Events.ToArray());
    }

    [Fact]
    public void AbsoluteCallWrapsOperandsAndReturnAddressBeforeReturning()
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFE] = 0xCD;
        bus.Memory[0xFFFF] = 0x34;
        bus.Memory[0] = 0x12;
        bus.Memory[0x1234] = 0xC9;
        var r = new Z80Registers { PC = 0xFFFE, SP = 0, F = 0xFF };
        var cpu = new Z80Cpu(bus, r);
        Assert.Equal(17UL, cpu.Step());
        Assert.Equal((ushort)0x1234, r.PC);
        Assert.Equal((ushort)0xFFFE, r.SP);
        Assert.Equal((byte)0, bus.Memory[0xFFFF]);
        Assert.Equal((byte)1, bus.Memory[0xFFFE]);
        Assert.Equal(10UL, cpu.Step());
        Assert.Equal((ushort)1, r.PC);
        Assert.Equal((ushort)0, r.SP);
        Assert.Equal((byte)0xFF, r.F);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0xFFFE, 0, 3, 4, 0xCD),
            new(Z80BusCycleKind.MemoryRead, 0xFFFF, 4, 7, 7, 0x34),
            new(Z80BusCycleKind.MemoryRead, 0, 7, 10, 10, 0x12),
            new(Z80BusCycleKind.Internal, 0, 10, 10, 11, 0),
            new(Z80BusCycleKind.MemoryWrite, 0xFFFF, 11, 14, 14, 0),
            new(Z80BusCycleKind.MemoryWrite, 0xFFFE, 14, 17, 17, 1),
            new(Z80BusCycleKind.OpcodeFetch, 0x1234, 17, 20, 21, 0xC9),
            new(Z80BusCycleKind.MemoryRead, 0xFFFE, 21, 24, 24, 1),
            new(Z80BusCycleKind.MemoryRead, 0xFFFF, 24, 27, 27, 0)
        }, bus.Events.ToArray());
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
    public void RestartPushesNextPcAndSelectsItsLiteralVector(int opcode, int vector)
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFF] = (byte)opcode;
        var r = new Z80Registers { PC = 0xFFFF, SP = 0x4000, F = 0xFF };
        Assert.Equal(11UL, new Z80Cpu(bus, r).Step());
        Assert.Equal((ushort)vector, r.PC);
        Assert.Equal((ushort)0x3FFE, r.SP);
        Assert.Equal((byte)0xFF, r.F);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0xFFFF, 0, 3, 4, (byte)opcode),
            new(Z80BusCycleKind.Internal, 0xFFFF, 4, 4, 5, 0),
            new(Z80BusCycleKind.MemoryWrite, 0x3FFF, 5, 8, 8, 0),
            new(Z80BusCycleKind.MemoryWrite, 0x3FFE, 8, 11, 11, 0)
        }, bus.Events.ToArray());
    }

    [Theory]
    [InlineData(0xC3, 10)]
    [InlineData(0xE9, 4)]
    public void AbsoluteAndHlJumpsPreserveFlagsAndDoNotReadTheTarget(int opcode, int cycles)
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFF] = (byte)opcode;
        bus.Memory[0] = 0x78;
        bus.Memory[1] = 0x56;
        var r = new Z80Registers { PC = 0xFFFF, HL = 0x5678, F = 0xFF };
        Assert.Equal((ulong)cycles, new Z80Cpu(bus, r).Step());
        Assert.Equal((ushort)0x5678, r.PC);
        Assert.Equal((byte)0xFF, r.F);
        Assert.Equal((byte)1, r.R);
        Assert.Equal(opcode == 0xC3 ? 3 : 1, bus.Events.Count);
        Assert.DoesNotContain(bus.Events, e => e.Address == 0x5678);
    }

    [Fact]
    public void StackWaitsShiftWritesAndFaultPreventsRetryingPartialPush()
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xC5;
        bus.WaitStates = cycle => cycle.Kind == Z80BusCycleKind.MemoryWrite ? 2 : 0;
        var r = new Z80Registers { BC = 0x1234, SP = 0x8000 };
        var cpu = new Z80Cpu(bus, r);
        Assert.Equal(15UL, cpu.Step());
        Assert.Equal(new CycleEvent(Z80BusCycleKind.MemoryWrite, 0x7FFF, 5, 10, 10, 0x12), bus.Events[2]);
        Assert.Equal(new CycleEvent(Z80BusCycleKind.MemoryWrite, 0x7FFE, 10, 15, 15, 0x34), bus.Events[3]);
        bus.Memory[1] = 0xC5;
        bus.WaitStates = cycle => cycle.Kind == Z80BusCycleKind.MemoryWrite && cycle.Address == 0x7FFC
            ? throw new InvalidOperationException("Synthetic bus failure") : 0;
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
        Assert.Equal((byte)0x12, bus.Memory[0x7FFD]);
        Assert.Equal((byte)0, bus.Memory[0x7FFC]);
        Assert.True(cpu.IsFaulted);
        ulong stopped = bus.TStates;
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
        Assert.Equal(stopped, bus.TStates);
    }
}
