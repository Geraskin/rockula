using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80EdDataTests
{
    // Work order 001j lists hardware sources. Expectations use independent range/nibble math.
    [Theory]
    [InlineData(0x42, 0, true)]
    [InlineData(0x52, 1, true)]
    [InlineData(0x62, 2, true)]
    [InlineData(0x72, 3, true)]
    [InlineData(0x4A, 0, false)]
    [InlineData(0x5A, 1, false)]
    [InlineData(0x6A, 2, false)]
    [InlineData(0x7A, 3, false)]
    public void WordArithmeticAllHlValuesAndCarryBoundaries(int opcode, int pair, bool subtract)
    {
        var bus = Program(opcode);
        var cpu = new Z80Cpu(bus);
        int[] operands = [0, 1, 0x0FFF, 0x1000, 0x7FFF, 0x8000, 0xFFFF];
        for (int hl = 0; hl < 65536; hl++)
            foreach (int operand in pair == 2 ? new[] { hl } : operands)
                for (int carry = 0; carry < 2; carry++) Check(hl, operand, 0xFE | carry);
        for (int flags = 0; flags < 256; flags++) Check(0x8000, pair == 2 ? 0x8000 : 0xFFFF, flags);

        void Check(int hl, int operand, int incoming)
        {
            cpu.Reset();
            var r = cpu.Registers;
            r.HL = (ushort)hl;
            SetPair(r, pair, (ushort)operand);
            r.F = (byte)incoming;
            r.WZ = 0x1234;
            r.IX = 0xABCD;
            r.IY = 0x9876;
            bus.Events.Clear();
            int carry = incoming % 2;
            int raw = subtract ? hl - operand - carry : hl + operand + carry;
            int result = (raw + 131072) % 65536;
            int signedHl = hl < 32768 ? hl : hl - 65536;
            int signedOperand = operand < 32768 ? operand : operand - 65536;
            int signed = subtract ? signedHl - signedOperand - carry : signedHl + signedOperand + carry;
            int nibble = subtract ? hl % 4096 - operand % 4096 - carry : hl % 4096 + operand % 4096 + carry;
            int flags = ((result / 256) & 0xA8) | (subtract ? 2 : 0);
            if (result == 0) flags |= 64;
            if (nibble < 0 || nibble > 4095) flags |= 16;
            if (signed < -32768 || signed > 32767) flags |= 4;
            if (raw < 0 || raw > 65535) flags |= 1;
            Assert.Equal(15UL, cpu.Step());
            Assert.Equal((ushort)result, r.HL);
            Assert.Equal((byte)flags, r.F);
            Assert.Equal(unchecked((ushort)(hl + 1)), r.WZ);
            Assert.Equal((ushort)0xABCD, r.IX);
            Assert.Equal((ushort)0x9876, r.IY);
            Assert.Equal((byte)2, r.R);
            Assert.Equal(new[] { Z80BusCycleKind.OpcodeFetch, Z80BusCycleKind.OpcodeFetch, Z80BusCycleKind.Internal, Z80BusCycleKind.Internal }, bus.Events.Select(e => e.Kind));
        }
    }

    [Theory]
    [InlineData(0x43, 0, false)]
    [InlineData(0x53, 1, false)]
    [InlineData(0x63, 2, false)]
    [InlineData(0x73, 3, false)]
    [InlineData(0x4B, 0, true)]
    [InlineData(0x5B, 1, true)]
    [InlineData(0x6B, 2, true)]
    [InlineData(0x7B, 3, true)]
    public void AllWordTransfersWrapAndPreserveFlags(int opcode, int pair, bool load)
    {
        var bus = Program(opcode);
        bus.Memory[2] = 0xFF;
        bus.Memory[3] = 0xFF;
        var cpu = new Z80Cpu(bus);
        for (int flags = 0; flags < 256; flags++)
        {
            cpu.Reset();
            var r = cpu.Registers;
            // Program lives at 0100 so the wrapped operand cannot overwrite its fetches.
            r.PC = 0x100;
            bus.Memory[0x100] = 0xED;
            bus.Memory[0x101] = (byte)opcode;
            bus.Memory[0x102] = 0xFF;
            bus.Memory[0x103] = 0xFF;
            r.BC = 0x1122;
            r.DE = 0x3344;
            r.HL = 0x5566;
            r.SP = 0x7788;
            SetPair(r, pair, 0xABCD);
            r.F = (byte)flags;
            r.WZ = 0x9876;
            bus.Memory[0xFFFF] = 0x34;
            bus.Memory[0] = 0x12;
            bus.Events.Clear();
            Assert.Equal(20UL, cpu.Step());
            Assert.Equal(load ? (ushort)0x1234 : (ushort)0xABCD, Pair(r, pair));
            Assert.Equal(load ? (byte)0x34 : (byte)0xCD, bus.Memory[0xFFFF]);
            Assert.Equal(load ? (byte)0x12 : (byte)0xAB, bus.Memory[0]);
            Assert.Equal((byte)flags, r.F);
            Assert.Equal((ushort)0, r.WZ);
            Assert.Equal((ushort)0x104, r.PC);
            Assert.Equal(new ushort[] { 0x100, 0x101, 0x102, 0x103, 0xFFFF, 0 }, bus.Events.Select(e => e.Address));
            Assert.Equal(load ? Z80BusCycleKind.MemoryRead : Z80BusCycleKind.MemoryWrite, bus.Events[5].Kind);
        }
    }

    [Theory]
    [InlineData(0x47)]
    [InlineData(0x4F)]
    [InlineData(0x57)]
    [InlineData(0x5F)]
    public void IrTransfersAllValuesFlagsAndIff2(int opcode)
    {
        var bus = Program(opcode);
        var cpu = new Z80Cpu(bus);
        for (int value = 0; value < 256; value++)
            for (int flags = 0; flags < 256; flags++)
                for (int iff = 0; iff < 2; iff++)
                {
                    cpu.Reset();
                    var r = cpu.Registers;
                    r.A = (byte)value;
                    r.I = (byte)value;
                    r.R = (byte)value;
                    r.F = (byte)flags;
                    r.Iff1 = iff == 0;
                    r.Iff2 = iff != 0;
                    r.WZ = 0x9876;
                    bus.Events.Clear();
                    int refreshed = (value / 128) * 128 + (value + 2) % 128;
                    int expectedA = opcode == 0x5F ? refreshed : value;
                    int expectedF = opcode is 0x47 or 0x4F ? flags
                        : (expectedA & 0xA8) | (expectedA == 0 ? 64 : 0) | (iff * 4) | (flags % 2);
                    Assert.Equal(9UL, cpu.Step());
                    Assert.Equal((byte)expectedA, r.A);
                    Assert.Equal((byte)expectedF, r.F);
                    Assert.Equal((byte)(opcode == 0x4F ? value : refreshed), r.R);
                    Assert.Equal((byte)value, r.I);
                    Assert.Equal(iff != 0, r.Iff2);
                    Assert.Equal((ushort)0x9876, r.WZ);
                }
    }

    [Theory]
    [InlineData(0x67)]
    [InlineData(0x6F)]
    public void NibbleRotationsAllAccumulatorMemoryAndCarries(int opcode)
    {
        var bus = Program(opcode);
        var cpu = new Z80Cpu(bus);
        for (int a = 0; a < 256; a++)
            for (int memory = 0; memory < 256; memory++)
                for (int carry = 0; carry < 2; carry++) Check(a, memory, 0xFE | carry);
        for (int flags = 0; flags < 256; flags++) Check(0xA5, 0x3C, flags);

        void Check(int a, int memory, int flags)
        {
            cpu.Reset();
            var r = cpu.Registers;
            r.HL = 0xFFFF;
            r.A = (byte)a;
            r.F = (byte)flags;
            r.WZ = 0x9876;
            bus.Memory[0xFFFF] = (byte)memory;
            bus.Events.Clear();
            ulong start = bus.TStates;
            int resultA = (a / 16) * 16 + (opcode == 0x67 ? memory % 16 : memory / 16);
            int resultMemory = opcode == 0x67 ? (a % 16) * 16 + memory / 16 : (memory % 16) * 16 + a % 16;
            Assert.Equal(18UL, cpu.Step());
            Assert.Equal((byte)resultA, r.A);
            Assert.Equal((byte)resultMemory, bus.Memory[0xFFFF]);
            Assert.Equal((byte)((resultA & 0xA8) | (resultA == 0 ? 64 : 0) | EvenParity(resultA) | (flags % 2)), r.F);
            Assert.Equal((ushort)0, r.WZ);
            Assert.Equal(new CycleEvent[]
            {
                new(Z80BusCycleKind.OpcodeFetch, 0, start, start + 3, start + 4, 0xED),
                new(Z80BusCycleKind.OpcodeFetch, 1, start + 4, start + 7, start + 8, (byte)opcode),
                new(Z80BusCycleKind.MemoryRead, 0xFFFF, start + 8, start + 11, start + 11, (byte)memory),
                new(Z80BusCycleKind.Internal, 0xFFFF, start + 11, start + 11, start + 15, 0),
                new(Z80BusCycleKind.MemoryWrite, 0xFFFF, start + 15, start + 18, start + 18, (byte)resultMemory)
            }, bus.Events.ToArray());
        }
    }

    [Fact]
    public void NibbleWaitsSampleLiveMemoryAndKeepNmiPendingUntilRetirement()
    {
        var bus = Program(0x6F);
        var r = new Z80Registers { A = 0xA5, F = 1, HL = 0x4000, SP = 0x8000 };
        var cpu = new Z80Cpu(bus, r);
        bus.WaitStates = cycle => cycle.Kind == Z80BusCycleKind.MemoryRead ? 2 : 0;
        bus.Advancing = time =>
        {
            if (time == 13)
            {
                bus.Memory[0x4000] = 0x3C;
                cpu.SetNmiLine(true);
            }
        };
        Assert.Equal(20UL, cpu.Step());
        Assert.Equal((byte)0xA3, r.A);
        Assert.Equal((byte)0xC5, bus.Memory[0x4000]);
        Assert.True(cpu.IsNmiPending);
        Assert.Equal(11UL, cpu.Step());
        Assert.Equal((ushort)0x66, r.PC);
    }

    [Theory]
    [InlineData(0x57)]
    [InlineData(0x5F)]
    public void IrLoadsUseLogicalIff2DespiteIntArrivingDuringInstruction(int opcode)
    {
        var bus = Program(opcode);
        var r = new Z80Registers { I = 0, R = 0x7E, Iff1 = true, Iff2 = true, InterruptMode = 1, SP = 0x8000 };
        var cpu = new Z80Cpu(bus, r);
        bus.Advancing = time => { if (time == 9) cpu.SetInterruptLine(true); };
        Assert.Equal(9UL, cpu.Step());
        Assert.Equal((byte)0x44, r.F);
        Assert.Equal(13UL, cpu.Step());
        Assert.Equal((byte)0x44, r.F);
    }

    [Theory]
    [InlineData(0x4A, 2)]
    [InlineData(0x4A, 3)]
    [InlineData(0x4B, 4)]
    [InlineData(0x4B, 5)]
    [InlineData(0x43, 5)]
    [InlineData(0x57, 2)]
    [InlineData(0x67, 2)]
    [InlineData(0x67, 3)]
    [InlineData(0x67, 4)]
    public void FailedCycleDoesNotCommitResultFlagsOrWz(int opcode, int failure)
    {
        var bus = Program(opcode);
        bus.Memory[2] = 0;
        bus.Memory[3] = 0x40;
        bus.Memory[0x4000] = 0x34;
        bus.Memory[0x4001] = 0x12;
        var r = new Z80Registers { A = 0xA5, F = 0xFF, HL = 0x4000, BC = 0xABCD, WZ = 0x9876, I = 1 };
        int cycle = -1;
        bus.WaitStates = _ => ++cycle == failure ? throw new IOException("test bus fault") : 0;
        var cpu = new Z80Cpu(bus, r);
        Assert.Throws<IOException>(() => cpu.Step());
        Assert.Equal((byte)0xA5, r.A);
        Assert.Equal((byte)0xFF, r.F);
        Assert.Equal((ushort)0x4000, r.HL);
        Assert.Equal((ushort)0xABCD, r.BC);
        Assert.Equal((ushort)0x9876, r.WZ);
        Assert.Equal(opcode == 0x43 ? (byte)0xCD : (byte)0x34, bus.Memory[0x4000]);
        Assert.True(cpu.IsFaulted);
    }

    [Fact]
    public void NibbleIgnoredRomWriteStillCommitsAccumulator()
    {
        var bus = new ReadOnlyBus();
        var r = new Z80Registers { HL = 0x4000, A = 0xA5, F = 1 };
        Assert.Equal(18UL, new Z80Cpu(bus, r).Step());
        Assert.Equal((byte)0xA3, r.A);
        Assert.Equal((ushort)0x4001, r.WZ);
        Assert.Equal((byte)0xA5, r.F);
        Assert.Equal(1, bus.Writes);
    }

    [Fact]
    public void SyntheticEdDataProgramCombinesWordAndNibbleOperations()
    {
        var bus = new RecordingBus();
        byte[] code = [0x21, 0xFF, 0x7F, 0x01, 1, 0, 0xED, 0x4A, 0xED, 0x63, 0, 0x40,
            0xED, 0x5B, 0, 0x40, 0xED, 0x52, 0x21, 0, 0x40, 0x3E, 0xA5, 0xED, 0x6F, 0xED, 0x47, 0xED, 0x57, 0x76];
        code.CopyTo(bus.Memory, 0);
        var cpu = new Z80Cpu(bus);
        for (int step = 0; step < 12; step++) cpu.Step();
        Assert.True(cpu.IsHalted);
        Assert.Equal((byte)0xA0, cpu.Registers.A);
        Assert.Equal((byte)0xA0, cpu.Registers.I);
        Assert.Equal((ushort)0x8000, cpu.Registers.DE);
        Assert.Equal((byte)5, bus.Memory[0x4000]);
        Assert.Equal((byte)0xA0, cpu.Registers.F);
        Assert.Equal((ushort)30, cpu.Registers.PC);
        Assert.Equal((byte)19, cpu.Registers.R);
        Assert.Equal(147UL, bus.TStates);
    }

    private static RecordingBus Program(int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xED;
        bus.Memory[1] = (byte)opcode;
        return bus;
    }

    private static int EvenParity(int value)
    {
        int bits = 0;
        for (int bit = 0; bit < 8; bit++) bits += (value >> bit) & 1;
        return bits % 2 == 0 ? 4 : 0;
    }

    private static ushort Pair(Z80Registers r, int pair) => pair switch
    {
        0 => r.BC,
        1 => r.DE,
        2 => r.HL,
        3 => r.SP,
        _ => throw new ArgumentOutOfRangeException(nameof(pair))
    };

    private static void SetPair(Z80Registers r, int pair, ushort value)
    {
        switch (pair)
        {
            case 0: r.BC = value; break;
            case 1: r.DE = value; break;
            case 2: r.HL = value; break;
            case 3: r.SP = value; break;
        }
    }

    private sealed class ReadOnlyBus : Z80Bus
    {
        public int Writes { get; private set; }
        protected override byte ReadMemory(ushort address) => address switch { 0 => 0xED, 1 => 0x6F, 0x4000 => 0x3C, _ => 0 };
        protected override void WriteMemory(ushort address, byte value) => Writes++;
    }
}
