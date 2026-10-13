using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80BlockTests
{
    // Original arithmetic oracle; 001k scopes Young/Jan, Banks repeat corrections and bus sources.
    public static IEnumerable<object[]> MemoryBlocks()
    {
        foreach (int opcode in new[] { 0xA0, 0xA8, 0xB0, 0xB8, 0xA1, 0xA9, 0xB1, 0xB9 }) yield return [opcode];
    }

    [Theory]
    [MemberData(nameof(MemoryBlocks))]
    public void MemoryBlocksAllAccumulatorOperandPairsCarriesAndCountPaths(int opcode)
    {
        var bus = Program(opcode);
        var cpu = new Z80Cpu(bus);
        for (int a = 0; a < 256; a++)
            for (int value = 0; value < 256; value++)
                foreach (int count in new[] { 0, 1, 2 })
                    for (int carry = 0; carry < 2; carry++) Check(a, value, count, 0xFE | carry);
        for (int flags = 0; flags < 256; flags++) Check(0xA5, 0x3C, 2, flags);

        void Check(int a, int value, int count, int flags)
        {
            cpu.Reset();
            var r = cpu.Registers;
            r.PC = 0x2800;
            r.A = (byte)a;
            r.F = (byte)flags;
            r.HL = 0xFFFF;
            r.DE = 0;
            r.BC = (ushort)count;
            r.WZ = 0xFFFE;
            r.IX = 0x1234;
            r.IY = 0x5678;
            bus.Memory[0xFFFF] = (byte)value;
            bus.Memory[0] = 0xA5;
            bus.Events.Clear();
            bool compare = opcode % 2 != 0;
            int direction = opcode is 0xA8 or 0xB8 or 0xA9 or 0xB9 ? -1 : 1;
            int afterCount = (count + 65535) % 65536;
            bool repeat = opcode >= 0xB0 && afterCount != 0 && (!compare || a != value);
            int expectedF = MemoryFlags(a, value, afterCount, flags, compare);
            if (repeat) expectedF = (expectedF & 0xD7) | 0x28;
            Assert.Equal(repeat ? 21UL : 16UL, cpu.Step());
            Assert.Equal((byte)expectedF, r.F);
            Assert.Equal((byte)a, r.A);
            Assert.Equal((ushort)afterCount, r.BC);
            Assert.Equal(unchecked((ushort)(0xFFFF + direction)), r.HL);
            Assert.Equal(compare ? (ushort)0 : unchecked((ushort)direction), r.DE);
            Assert.Equal(compare ? (byte)0xA5 : (byte)value, bus.Memory[0]);
            Assert.Equal(repeat ? (ushort)0x2800 : (ushort)0x2802, r.PC);
            Assert.Equal(repeat ? (ushort)0x2801 : compare ? unchecked((ushort)(0xFFFE + direction)) : (ushort)0xFFFE, r.WZ);
            Assert.Equal((byte)2, r.R);
            Assert.Equal((ushort)0x1234, r.IX);
            Assert.Equal((ushort)0x5678, r.IY);
            Assert.Equal(compare ? Z80BusCycleKind.Internal : Z80BusCycleKind.MemoryWrite, bus.Events[3].Kind);
        }
    }

    public static IEnumerable<object[]> IoBlocks()
    {
        foreach (int opcode in new[] { 0xA2, 0xAA, 0xB2, 0xBA, 0xA3, 0xAB, 0xB3, 0xBB }) yield return [opcode];
    }

    [Theory]
    [MemberData(nameof(IoBlocks))]
    public void IoBlocksAllBAndDataWithAddressBoundariesAndRepeatFlags(int opcode)
    {
        var bus = Program(opcode);
        var cpu = new Z80Cpu(bus);
        for (int b = 0; b < 256; b++)
            for (int value = 0; value < 256; value++)
                foreach (int boundary in new[] { 0, 1, 0x0F, 0x7F, 0x80, 0xFE, 0xFF }) Check(b, value, boundary, 0xFF);
        for (int flags = 0; flags < 256; flags++) Check(0x10, 0x80, 0xFF, flags);

        void Check(int b, int value, int boundary, int flags)
        {
            cpu.Reset();
            var r = cpu.Registers;
            bool input = opcode % 2 == 0;
            int direction = opcode is 0xAA or 0xBA or 0xAB or 0xBB ? -1 : 1;
            int c = input ? boundary : 0xFF;
            int hl = input ? 0xFFFF : 0x4000 + boundary;
            int afterB = (b + 255) % 256;
            int afterHl = (hl + direction + 65536) % 65536;
            ushort port = (ushort)((input ? b : afterB) * 256 + c);
            bool repeat = opcode >= 0xB0 && afterB != 0;
            r.PC = 0x2800;
            r.BC = (ushort)(b * 256 + c);
            r.HL = (ushort)hl;
            r.DE = 0x1234;
            r.A = 0xA5;
            r.F = (byte)flags;
            r.WZ = 0x9876;
            bus.Memory[hl] = input ? (byte)0xEE : (byte)value;
            bus.Ports[port] = (byte)value;
            bus.Events.Clear();
            bus.PortWrites.Clear();
            int sum = value + (input ? (c + direction + 256) % 256 : afterHl % 256);
            int expectedF = IoFlags(afterB, value, sum, repeat, 0x28);
            Assert.Equal(repeat ? 21UL : 16UL, cpu.Step());
            Assert.Equal((byte)expectedF, r.F);
            Assert.Equal((ushort)(afterB * 256 + c), r.BC);
            Assert.Equal((ushort)afterHl, r.HL);
            Assert.Equal((byte)0xA5, r.A);
            Assert.Equal((ushort)0x1234, r.DE);
            Assert.Equal((byte)value, bus.Memory[hl]);
            Assert.Equal(unchecked((ushort)(port + direction)), r.WZ);
            Assert.Equal(repeat ? (ushort)0x2800 : (ushort)0x2802, r.PC);
            Assert.Equal((byte)2, r.R);
            Assert.Equal(input ? Z80BusCycleKind.PortRead : Z80BusCycleKind.MemoryRead, bus.Events[3].Kind);
            Assert.Equal(input ? Z80BusCycleKind.MemoryWrite : Z80BusCycleKind.PortWrite, bus.Events[4].Kind);
            Assert.Equal(port, bus.Events[input ? 3 : 4].Address);
            if (!input) Assert.Equal(new[] { (port, (byte)value) }, bus.PortWrites.ToArray());
        }
    }

    [Theory]
    [InlineData(0xA0)]
    [InlineData(0xA1)]
    [InlineData(0xA2)]
    [InlineData(0xA3)]
    public void LiteralBusOrderAndInternalDurations(int opcode)
    {
        var bus = Program(opcode);
        var r = new Z80Registers { PC = 0x2800, HL = 0x4000, DE = 0x5000, BC = 0x02FF, A = 0x55 };
        bus.Memory[0x4000] = 0x80;
        bus.Ports[0x02FF] = 0x80;
        Assert.Equal(16UL, new Z80Cpu(bus, r).Step());
        var prefix = new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0x2800, 0, 3, 4, 0xED),
            new(Z80BusCycleKind.OpcodeFetch, 0x2801, 4, 7, 8, (byte)opcode)
        };
        CycleEvent[] tail = opcode switch
        {
            0xA0 => [new(Z80BusCycleKind.MemoryRead, 0x4000, 8, 11, 11, 0x80),
                new(Z80BusCycleKind.MemoryWrite, 0x5000, 11, 14, 14, 0x80), new(Z80BusCycleKind.Internal, 0x5000, 14, 14, 16, 0)],
            0xA1 => [new(Z80BusCycleKind.MemoryRead, 0x4000, 8, 11, 11, 0x80), new(Z80BusCycleKind.Internal, 0x4000, 11, 11, 16, 0)],
            0xA2 => [new(Z80BusCycleKind.Internal, 0x2800, 8, 8, 9, 0), new(Z80BusCycleKind.PortRead, 0x02FF, 9, 13, 13, 0x80),
                new(Z80BusCycleKind.MemoryWrite, 0x4000, 13, 16, 16, 0x80)],
            _ => [new(Z80BusCycleKind.Internal, 0x2800, 8, 8, 9, 0), new(Z80BusCycleKind.MemoryRead, 0x4000, 9, 12, 12, 0x80),
                new(Z80BusCycleKind.PortWrite, 0x01FF, 12, 16, 16, 0x80)]
        };
        Assert.Equal(prefix.Concat(tail), bus.Events);
    }

    [Theory]
    [InlineData(0xB0)]
    [InlineData(0xB8)]
    [InlineData(0xB1)]
    [InlineData(0xB9)]
    [InlineData(0xB2)]
    [InlineData(0xBA)]
    [InlineData(0xB3)]
    [InlineData(0xBB)]
    public void RepeatingIterationUsesAllPcHighBytesForXy(int opcode)
    {
        for (int high = 0; high < 256; high++)
        {
            var bus = new RecordingBus();
            ushort pc = (ushort)(high * 256 + 0x80);
            bus.Memory[pc] = 0xED;
            bus.Memory[pc + 1] = (byte)opcode;
            var r = new Z80Registers { PC = pc, HL = 0x4000, DE = 0x5000, BC = opcode % 4 < 2 ? (ushort)2 : (ushort)0x02FF, A = 1 };
            Assert.Equal(21UL, new Z80Cpu(bus, r).Step());
            Assert.Equal((byte)(high & 0x28), (byte)(r.F & 0x28));
            Assert.Equal(pc, r.PC);
        }
    }

    [Fact]
    public void PrefixedRepeatDiscardsIndexPrefixAndAcceptsNmiBetweenIterations()
    {
        var bus = Program(0xB0);
        bus.Memory[0x27FF] = 0xFD;
        bus.Memory[0x4000] = 0xA5;
        var r = new Z80Registers { PC = 0x27FF, BC = 2, HL = 0x4000, DE = 0x5000, IY = 0x1234, SP = 0x8000 };
        var cpu = new Z80Cpu(bus, r);
        bus.Advancing = time => { if (time == 18) cpu.SetNmiLine(true); };
        Assert.Equal(25UL, cpu.Step());
        Assert.Equal((ushort)0x2800, r.PC);
        Assert.Equal((ushort)0x2801, r.WZ);
        Assert.Equal((byte)0x2C, r.F);
        Assert.Equal((byte)3, r.R);
        Assert.Equal((ushort)1, r.BC);
        Assert.Equal((ushort)0x1234, r.IY);
        Assert.True(cpu.IsNmiPending);
        Assert.Equal(11UL, cpu.Step());
        Assert.Equal((byte)0, bus.Memory[0x7FFE]);
        Assert.Equal((byte)0x28, bus.Memory[0x7FFF]);
    }

    [Fact]
    public void EiInhibitsFirstBlockIterationThenAllowsInt()
    {
        var bus = new RecordingBus();
        byte[] code = [0xFB, 0xED, 0xB0];
        code.CopyTo(bus.Memory, 0);
        var r = new Z80Registers { HL = 0x4000, DE = 0x5000, BC = 2, InterruptMode = 1, SP = 0x8000 };
        var cpu = new Z80Cpu(bus, r);
        cpu.Step();
        cpu.SetInterruptLine(true);
        Assert.Equal(21UL, cpu.Step());
        Assert.False(cpu.IsEiDelayActive);
        Assert.Equal((ushort)1, r.PC);
        Assert.Equal((ushort)1, r.BC);
        Assert.Equal(13UL, cpu.Step());
        Assert.Equal((byte)1, bus.Memory[0x7FFE]);
        Assert.Equal((ushort)0x38, r.PC);
    }

    [Fact]
    public void SelfModifiedRepeatedPayloadIsFetchedAgain()
    {
        var bus = Program(0xB0);
        var r = new Z80Registers { PC = 0x2800, HL = 0x4000, DE = 0x2801, BC = 2 };
        var cpu = new Z80Cpu(bus, r);
        Assert.Equal(21UL, cpu.Step());
        Assert.Equal((byte)0, bus.Memory[0x2801]);
        var error = Assert.Throws<UnsupportedOpcodeException>(() => cpu.Step());
        Assert.Equal((byte)0, error.Opcode);
        Assert.Equal((ushort)1, r.BC);
        Assert.Equal((byte)4, r.R);
    }

    [Fact]
    public void RepeatedPcWrapAndFinalIterationPreserveTransferWz()
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFF] = 0xED;
        bus.Memory[0] = 0xB0;
        var r = new Z80Registers { PC = 0xFFFF, HL = 0x4000, DE = 0x5000, BC = 2 };
        var cpu = new Z80Cpu(bus, r);
        Assert.Equal(21UL, cpu.Step());
        Assert.Equal((ushort)0xFFFF, r.PC);
        Assert.Equal((ushort)0, r.WZ);
        Assert.Equal((byte)0x2C, r.F);
        Assert.Equal(16UL, cpu.Step());
        Assert.Equal((ushort)1, r.PC);
        Assert.Equal((ushort)0, r.WZ);
        Assert.Equal((ushort)0, r.BC);
        Assert.Equal((byte)0, r.F);
    }

    [Fact]
    public void BlockInputWaitsSampleLivePortBeforeMemoryWrite()
    {
        var bus = Program(0xA2);
        var r = new Z80Registers { PC = 0x2800, BC = 0x02FF, HL = 0x4000 };
        bus.WaitStates = c => c.Kind == Z80BusCycleKind.PortRead ? 3 : 0;
        bus.Advancing = t => { if (t == 16) bus.Ports[0x02FF] = 0xA5; };
        Assert.Equal(19UL, new Z80Cpu(bus, r).Step());
        Assert.Equal((byte)0xA5, bus.Memory[0x4000]);
        Assert.Equal(new CycleEvent(Z80BusCycleKind.PortRead, 0x02FF, 9, 16, 16, 0xA5), bus.Events[3]);
        Assert.Equal(new CycleEvent(Z80BusCycleKind.MemoryWrite, 0x4000, 16, 19, 19, 0xA5), bus.Events[4]);
    }

    [Theory]
    [InlineData(0xA0, 3)]
    [InlineData(0xA0, 4)]
    [InlineData(0xA1, 2)]
    [InlineData(0xA1, 3)]
    [InlineData(0xA2, 3)]
    [InlineData(0xA2, 4)]
    [InlineData(0xA3, 3)]
    [InlineData(0xA3, 4)]
    public void FailedBaseIterationRetainsOnlyCompletedTransfersAndOutputB(int opcode, int failure)
    {
        var bus = Program(opcode);
        bus.Memory[0x4000] = 0xA5;
        bus.Ports[0x02FF] = 0xA5;
        int cycle = -1;
        bus.WaitStates = _ => ++cycle == failure ? throw new IOException("block fault") : 0;
        var r = new Z80Registers { PC = 0x2800, HL = 0x4000, DE = 0x5000, BC = 0x02FF, A = 0x11, F = 0xFF, WZ = 0x9876 };
        var cpu = new Z80Cpu(bus, r);
        Assert.Throws<IOException>(() => cpu.Step());
        Assert.True(cpu.IsFaulted);
        Assert.Equal((ushort)0x4000, r.HL);
        Assert.Equal((ushort)0x5000, r.DE);
        Assert.Equal(opcode == 0xA3 && failure == 4 ? (ushort)0x01FF : (ushort)0x02FF, r.BC);
        Assert.Equal((byte)0xFF, r.F);
        Assert.Equal((ushort)0x9876, r.WZ);
        Assert.Equal(opcode == 0xA0 && failure == 4 ? (byte)0xA5 : (byte)0, bus.Memory[0x5000]);
        Assert.Empty(bus.PortWrites);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
    }

    [Fact]
    public void RepeatIdleFailureRetainsBaseIterationWithoutRewindingPc()
    {
        var bus = Program(0xB0);
        bus.Memory[0x4000] = 0xA5;
        int cycle = -1;
        bus.WaitStates = _ => ++cycle == 5 ? throw new IOException("repeat idle fault") : 0;
        var r = new Z80Registers { PC = 0x2800, HL = 0x4000, DE = 0x5000, BC = 2, WZ = 0x9876 };
        var cpu = new Z80Cpu(bus, r);
        Assert.Throws<IOException>(() => cpu.Step());
        Assert.Equal((ushort)0x2802, r.PC);
        Assert.Equal((ushort)1, r.BC);
        Assert.Equal((ushort)0x4001, r.HL);
        Assert.Equal((ushort)0x5001, r.DE);
        Assert.Equal((byte)0xA5, bus.Memory[0x5000]);
        Assert.Equal((byte)4, r.F);
        Assert.Equal((ushort)0x9876, r.WZ);
    }

    [Fact]
    public void SyntheticBlockProgramCopiesSearchesAndOutputsThreeBytes()
    {
        var bus = new RecordingBus();
        byte[] code = [0x21, 0, 0x40, 0x11, 0, 0x50, 0x01, 3, 0, 0xED, 0xB0,
            0x21, 0, 0x50, 0x01, 3, 0, 0x3E, 0x22, 0xED, 0xB1,
            0x21, 0, 0x50, 0x01, 0xFE, 3, 0xED, 0xB3, 0x76];
        code.CopyTo(bus.Memory, 0);
        bus.Memory[0x4000] = 0x11;
        bus.Memory[0x4001] = 0x22;
        bus.Memory[0x4002] = 0x33;
        var cpu = new Z80Cpu(bus);
        int steps = 0;
        while (!cpu.IsHalted && steps < 32) { cpu.Step(); steps++; }
        Assert.True(cpu.IsHalted);
        Assert.Equal(17, steps);
        Assert.Equal(230UL, bus.TStates);
        Assert.Equal((ushort)30, cpu.Registers.PC);
        Assert.Equal((ushort)0x5003, cpu.Registers.HL);
        Assert.Equal((ushort)0x00FE, cpu.Registers.BC);
        Assert.Equal((byte)0x22, cpu.Registers.A);
        Assert.Equal((byte)0x44, cpu.Registers.F);
        Assert.Equal((byte)25, cpu.Registers.R);
        Assert.Equal(new (ushort, byte)[] { (0x02FE, 0x11), (0x01FE, 0x22), (0x00FE, 0x33) }, bus.PortWrites.ToArray());
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33 }, bus.Memory[0x5000..0x5003]);
    }

    [Fact]
    public void ZeroTransferCountCompletesSixtyFourKThroughBoundedSteps()
    {
        var bus = Program(0xB0);
        var r = new Z80Registers { PC = 0x2800, HL = 0x4000, DE = 0x4000, BC = 0 };
        var cpu = new Z80Cpu(bus, r);
        for (int step = 0; step < 65536; step++)
        {
            bus.Events.Clear();
            Assert.Equal(step == 65535 ? 16UL : 21UL, cpu.Step());
        }
        Assert.Equal(1376251UL, bus.TStates);
        Assert.Equal((ushort)0, r.BC);
        Assert.Equal((ushort)0x4000, r.HL);
        Assert.Equal((ushort)0x4000, r.DE);
        Assert.Equal((ushort)0x2802, r.PC);
        Assert.Equal((ushort)0x2801, r.WZ);
        Assert.Equal((byte)0, r.R);
    }

    [Fact]
    public void ZeroIoCountCompletesTwoHundredFiftySixThroughBoundedSteps()
    {
        var bus = Program(0xB2);
        bus.PortReading = _ => 0xA5;
        var r = new Z80Registers { PC = 0x2800, HL = 0x4000, BC = 0x00FE };
        var cpu = new Z80Cpu(bus, r);
        for (int step = 0; step < 256; step++) Assert.Equal(step == 255 ? 16UL : 21UL, cpu.Step());
        Assert.Equal(5371UL, bus.TStates);
        Assert.Equal((ushort)0x00FE, r.BC);
        Assert.Equal((ushort)0x4100, r.HL);
        Assert.Equal((ushort)0x01FF, r.WZ);
        Assert.Equal((ushort)0x2802, r.PC);
        Assert.Equal((byte)0, r.R);
        Assert.Equal((byte)0x53, r.F);
        Assert.All(bus.Memory[0x4000..0x4100], value => Assert.Equal((byte)0xA5, value));
    }

    private static int MemoryFlags(int a, int value, int count, int incoming, bool compare)
    {
        if (!compare)
        {
            int sum = (a + value) % 256;
            return (incoming & 0xC1) | (count != 0 ? 4 : 0) | (sum & 8) | ((sum % 4) / 2 * 32);
        }
        int result = (a - value + 256) % 256;
        int half = a % 16 < value % 16 ? 1 : 0;
        int adjusted = (result - half + 256) % 256;
        return (incoming % 2) | (result & 0x80) | (result == 0 ? 64 : 0) | (half * 16)
            | (count != 0 ? 4 : 0) | 2 | (adjusted & 8) | ((adjusted % 4) / 2 * 32);
    }

    private static int IoFlags(int b, int data, int sum, bool repeat, int pcHigh)
    {
        bool carry = sum > 255;
        int parity = Z80PortTests.Parity((sum % 8) ^ b) / 4;
        bool half = carry;
        if (repeat)
        {
            // Banks' parity relation is expressed in 0/1 bits, not production flag masks.
            int adjust = carry ? (data >= 128 ? b - 1 : b + 1) : b;
            parity = (parity + Z80PortTests.Parity((adjust + 256) % 8) / 4 + 1) % 2;
            if (carry) half = b % 16 == (data >= 128 ? 0 : 15);
        }
        return (b & 0x80) | (b == 0 ? 64 : 0) | (repeat ? pcHigh & 0x28 : b & 0x28)
            | (half ? 16 : 0) | (parity * 4) | (data >= 128 ? 2 : 0) | (carry ? 1 : 0);
    }

    private static RecordingBus Program(int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0x2800] = 0xED;
        bus.Memory[0x2801] = (byte)opcode;
        return bus;
    }
}
