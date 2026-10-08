using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80IndexTests
{
    // Independently authored expectations; hardware sources are scoped in work order 001i.
    public static IEnumerable<object[]> IndexedBits()
    {
        int[] rows = [0x00, 0x08, 0x10, 0x18, 0x20, 0x28, 0x30, 0x38,
            0x40, 0x48, 0x50, 0x58, 0x60, 0x68, 0x70, 0x78,
            0x80, 0x88, 0x90, 0x98, 0xA0, 0xA8, 0xB0, 0xB8,
            0xC0, 0xC8, 0xD0, 0xD8, 0xE0, 0xE8, 0xF0, 0xF8];
        foreach (int prefix in new[] { 0xDD, 0xFD })
            foreach (int row in rows)
                for (int target = 0; target < 8; target++)
                    yield return [prefix, row + target];
    }

    [Theory]
    [MemberData(nameof(IndexedBits))]
    public void AllIndexedCbPayloadsOperandsCarriesAndIncomingFlags(int prefix, int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)prefix;
        bus.Memory[1] = 0xCB;
        bus.Memory[2] = 0xFE;
        bus.Memory[3] = (byte)opcode;
        var cpu = new Z80Cpu(bus);
        for (int value = 0; value < 256; value++)
            for (int carry = 0; carry < 2; carry++) Check(value, 0xFE | carry);
        for (int flags = 0; flags < 256; flags++) Check(0xA5, flags);

        void Check(int value, int flags)
        {
            cpu.Reset();
            var r = cpu.Registers;
            r.IX = prefix == 0xDD ? (ushort)0x2802 : (ushort)0x5000;
            r.IY = prefix == 0xFD ? (ushort)0x2802 : (ushort)0x6000;
            r.BC = 0x1122;
            r.DE = 0x3344;
            r.HL = 0x5566;
            r.A = 0x77;
            r.F = (byte)flags;
            r.WZ = 0xFFFF;
            bus.Memory[0x2800] = (byte)value;
            byte[] before = [r.B, r.C, r.D, r.E, r.H, r.L, (byte)value, r.A];
            bus.Events.Clear();
            (int result, int f) = ExpectedBit(opcode, value, flags, 0x28);
            bool bit = opcode is >= 0x40 and < 0x80;
            Assert.Equal(bit ? 20UL : 23UL, cpu.Step());
            Assert.Equal((byte)f, r.F);
            byte[] after = [r.B, r.C, r.D, r.E, r.H, r.L, bus.Memory[0x2800], r.A];
            for (int target = 0; target < 8; target++)
                Assert.Equal(!bit && (target == 6 || target == opcode % 8) ? (byte)result : before[target], after[target]);
            Assert.Equal((ushort)0x2800, r.WZ);
            Assert.Equal((ushort)4, r.PC);
            Assert.Equal((byte)2, r.R);
            Assert.Equal(prefix == 0xDD ? (ushort)0x2802 : (ushort)0x5000, r.IX);
            Assert.Equal(prefix == 0xFD ? (ushort)0x2802 : (ushort)0x6000, r.IY);
            Assert.Equal(Z80BusCycleKind.MemoryRead, bus.Events[3].Kind);
        }
    }

    public static IEnumerable<object[]> AluSources()
    {
        foreach (int prefix in new[] { 0xDD, 0xFD })
            foreach (int row in new[] { 0x80, 0x88, 0x90, 0x98, 0xA0, 0xA8, 0xB0, 0xB8 })
                foreach (int source in new[] { 4, 5, 6 }) yield return [prefix, row + source];
    }

    [Theory]
    [MemberData(nameof(AluSources))]
    public void IndexedAluChecksAllPairsAndCarries(int prefix, int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)prefix;
        bus.Memory[1] = (byte)opcode;
        bus.Memory[2] = 0;
        var cpu = new Z80Cpu(bus);
        int source = opcode % 8;
        for (int a = 0; a < 256; a++)
            for (int value = 0; value < 256; value++)
                for (int carry = 0; carry < 2; carry++)
                {
                    cpu.Reset();
                    ushort index = source == 4 ? (ushort)(value * 256 + 0x55) : source == 5 ? (ushort)(0x5500 + value) : (ushort)0x4000;
                    if (prefix == 0xDD) cpu.Registers.IX = index;
                    else cpu.Registers.IY = index;
                    bus.Memory[0x4000] = (byte)value;
                    cpu.Registers.A = (byte)a;
                    cpu.Registers.F = (byte)(0xFE | carry);
                    cpu.Registers.HL = 0x1234;
                    cpu.Registers.WZ = 0x9876;
                    bus.Events.Clear();
                    (byte expectedA, byte expectedF) = ExpectedAlu((opcode - 0x80) / 8, a, value, carry);
                    Assert.Equal(source == 6 ? 19UL : 8UL, cpu.Step());
                    Assert.Equal(expectedA, cpu.Registers.A);
                    Assert.Equal(expectedF, cpu.Registers.F);
                    Assert.Equal((ushort)0x1234, cpu.Registers.HL);
                    Assert.Equal(source == 6 ? (ushort)0x4000 : (ushort)0x9876, cpu.Registers.WZ);
                }
    }

    public static IEnumerable<object[]> Loads()
    {
        // Literal affected LD encodings; HALT is deliberately absent.
        int[] opcodes = [0x44, 0x45, 0x46, 0x4C, 0x4D, 0x4E, 0x54, 0x55, 0x56,
            0x5C, 0x5D, 0x5E, 0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67,
            0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F,
            0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x77, 0x7C, 0x7D, 0x7E];
        foreach (int prefix in new[] { 0xDD, 0xFD })
            foreach (int opcode in opcodes) yield return [prefix, opcode];
    }

    [Theory]
    [MemberData(nameof(Loads))]
    public void HalfLoadsAndMemoryHlExceptions(int prefix, int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)prefix;
        bus.Memory[1] = (byte)opcode;
        bus.Memory[2] = 0x80;
        var r = new Z80Registers { BC = 0x1122, DE = 0x3344, HL = 0x5566, A = 0x77, F = 0xFF, IX = 0x2880, IY = 0x2880, WZ = 0x9999 };
        bus.Memory[0x2800] = 0x88;
        int source = opcode % 8;
        int target = (opcode / 8) % 8;
        bool memory = source == 6 || target == 6;
        byte[] before = [0x11, 0x22, 0x33, 0x44, memory ? (byte)0x55 : (byte)0x28, memory ? (byte)0x66 : (byte)0x80, 0x88, 0x77];
        Assert.Equal(memory ? 19UL : 8UL, new Z80Cpu(bus, r).Step());
        ushort selected = prefix == 0xDD ? r.IX : r.IY;
        byte[] after = [r.B, r.C, r.D, r.E, memory ? r.H : (byte)(selected / 256), memory ? r.L : (byte)(selected % 256), bus.Memory[0x2800], r.A];
        for (int code = 0; code < 8; code++) Assert.Equal(code == target ? before[source] : before[code], after[code]);
        Assert.Equal((byte)0xFF, r.F);
        Assert.Equal(memory ? (ushort)0x2800 : (ushort)0x9999, r.WZ);
        if (!memory) Assert.Equal((ushort)0x5566, r.HL);
    }

    [Theory]
    [InlineData(0xDD)]
    [InlineData(0xFD)]
    public void ImmediateMemoryAndSignedDisplacements(int prefix)
    {
        for (int d = 0; d < 256; d++)
        {
            var bus = new RecordingBus();
            bus.Memory[0] = (byte)prefix;
            bus.Memory[1] = 0x36;
            bus.Memory[2] = (byte)d;
            bus.Memory[3] = 0xA5;
            var r = new Z80Registers { IX = 0, IY = 0, F = 0xFF };
            ushort address = unchecked((ushort)(d < 128 ? d : d - 256));
            Assert.Equal(19UL, new Z80Cpu(bus, r).Step());
            Assert.Equal((byte)0xA5, bus.Memory[address]);
            Assert.Equal(address, r.WZ);
            Assert.Equal((byte)0xFF, r.F);
            Assert.Equal(new[] { Z80BusCycleKind.OpcodeFetch, Z80BusCycleKind.OpcodeFetch, Z80BusCycleKind.MemoryRead, Z80BusCycleKind.MemoryRead, Z80BusCycleKind.Internal, Z80BusCycleKind.MemoryWrite }, bus.Events.Select(e => e.Kind));
            Assert.Equal(new CycleEvent(Z80BusCycleKind.Internal, 3, 14, 14, 16, 0), bus.Events[4]);
        }
    }

    [Fact]
    public void LastPrefixWinsWithoutLeakingAndEdCancelsIndex()
    {
        var bus = new RecordingBus();
        byte[] program = [0xDD, 0xFD, 0xDD, 0x21, 0x34, 0x12, 0x21, 0x78, 0x56, 0xFD, 0xED, 0x44];
        program.CopyTo(bus.Memory, 0);
        var r = new Z80Registers { IY = 0xABCD, A = 1 };
        var cpu = new Z80Cpu(bus, r);
        Assert.Equal(22UL, cpu.Step());
        Assert.Equal((ushort)0x1234, r.IX);
        Assert.Equal((ushort)0xABCD, r.IY);
        Assert.Equal(10UL, cpu.Step());
        Assert.Equal((ushort)0x5678, r.HL);
        Assert.Equal(12UL, cpu.Step());
        Assert.Equal((byte)0xFF, r.A);
        Assert.Equal((byte)8, r.R);
    }

    [Fact]
    public void PrefixEiDelaysThroughWholeInstructionAndHaltIsBounded()
    {
        var bus = new RecordingBus();
        byte[] program = [0xDD, 0xFB, 0xFD, 0xDD, 0x76];
        program.CopyTo(bus.Memory, 0);
        var r = new Z80Registers { InterruptMode = 1, SP = 0x8000 };
        var cpu = new Z80Cpu(bus, r);
        Assert.Equal(8UL, cpu.Step());
        Assert.True(cpu.IsEiDelayActive);
        cpu.SetInterruptLine(true);
        Assert.Equal(12UL, cpu.Step());
        Assert.True(cpu.IsHalted);
        Assert.False(cpu.IsEiDelayActive);
        Assert.Equal(13UL, cpu.Step());
        Assert.False(cpu.IsHalted);
        Assert.Equal((ushort)0x38, r.PC);
        Assert.Equal((byte)5, bus.Memory[0x7FFE]);
    }

    [Theory]
    [InlineData(0xDD, 0x37)]
    [InlineData(0xFD, 0x3F)]
    [InlineData(0xDD, 0xD3)]
    [InlineData(0xFD, 0xDB)]
    public void UnsupportedTerminalReportsFirstAddressAndLastPrefix(int prefix, int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFF] = 0xFD;
        bus.Memory[0] = (byte)prefix;
        bus.Memory[1] = (byte)opcode;
        var r = new Z80Registers { PC = 0xFFFF, R = 0xFF };
        var cpu = new Z80Cpu(bus, r);
        var error = Assert.Throws<UnsupportedOpcodeException>(() => cpu.Step());
        Assert.Equal((ushort)0xFFFF, error.Address);
        Assert.Equal((byte?)prefix, error.Prefix);
        Assert.Equal((byte)opcode, error.Opcode);
        Assert.Equal((ushort)2, r.PC);
        Assert.Equal((byte)0x82, r.R);
        Assert.True(cpu.IsFaulted);
    }

    [Fact]
    public void PrefixOnlyMemoryFaultsAtExplicitBound()
    {
        var bus = new RecordingBus();
        Array.Fill(bus.Memory, (byte)0xDD);
        var cpu = new Z80Cpu(bus);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
        Assert.Equal(262144UL, bus.TStates);
        Assert.Equal((ushort)0, cpu.Registers.PC);
        Assert.Equal((byte)0, cpu.Registers.R);
        Assert.True(cpu.IsFaulted);
        cpu.Reset();
        bus.Memory[0] = 0;
        Assert.Equal(4UL, cpu.Step());
    }

    public static IEnumerable<object[]> Increments()
    {
        foreach (int prefix in new[] { 0xDD, 0xFD })
            foreach (int opcode in new[] { 0x24, 0x25, 0x2C, 0x2D, 0x34, 0x35 }) yield return [prefix, opcode];
    }

    [Theory]
    [MemberData(nameof(Increments))]
    public void ByteIncrementsAllValuesAndFlags(int prefix, int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)prefix;
        bus.Memory[1] = (byte)opcode;
        bus.Memory[2] = 0;
        var cpu = new Z80Cpu(bus);
        int target = (opcode / 8) % 8;
        bool decrement = opcode % 2 == 1;
        for (int value = 0; value < 256; value++)
            for (int flags = 0; flags < 256; flags++)
            {
                cpu.Reset();
                ushort index = target == 4 ? (ushort)(value * 256 + 0x55) : target == 5 ? (ushort)(0x5500 + value) : (ushort)0x4000;
                if (prefix == 0xDD) cpu.Registers.IX = index;
                else cpu.Registers.IY = index;
                bus.Memory[0x4000] = (byte)value;
                cpu.Registers.F = (byte)flags;
                bus.Events.Clear();
                int result = (value + (decrement ? 255 : 1)) % 256;
                int f = (result & 0xA8) | (flags % 2) | (decrement ? 2 : 0);
                if (result == 0) f |= 0x40;
                if (value == (decrement ? 128 : 127)) f |= 4;
                if (value % 16 == (decrement ? 0 : 15)) f |= 16;
                Assert.Equal(target == 6 ? 23UL : 8UL, cpu.Step());
                Assert.Equal((byte)f, cpu.Registers.F);
                ushort after = prefix == 0xDD ? cpu.Registers.IX : cpu.Registers.IY;
                Assert.Equal(result, target == 4 ? after / 256 : target == 5 ? after % 256 : bus.Memory[0x4000]);
            }
    }

    [Theory]
    [InlineData(0xDD)]
    [InlineData(0xFD)]
    public void WordOperationsAndStackWrap(int prefix)
    {
        var bus = new RecordingBus();
        byte[] program = [(byte)prefix, 0x21, 0xFF, 0xFF, (byte)prefix, 0x23,
            (byte)prefix, 0x2B, (byte)prefix, 0x09, (byte)prefix, 0x19,
            (byte)prefix, 0x29, (byte)prefix, 0x39, (byte)prefix, 0x22, 0x00, 0x40,
            (byte)prefix, 0x2A, 0x00, 0x40, (byte)prefix, 0xE5, (byte)prefix, 0xE1,
            (byte)prefix, 0xE3, (byte)prefix, 0xF9, (byte)prefix, 0xE9];
        program.CopyTo(bus.Memory, 0x100);
        var r = new Z80Registers { PC = 0x100, BC = 1, DE = 0x1000, HL = 0xABCD, SP = 0, F = 0xC4 };
        var cpu = new Z80Cpu(bus, r);
        Assert.Equal(14UL, cpu.Step());
        Assert.Equal((ushort)0xFFFF, Index());
        Assert.Equal(10UL, cpu.Step());
        Assert.Equal((ushort)0, Index());
        Assert.Equal(10UL, cpu.Step());
        Assert.Equal((ushort)0xFFFF, Index());
        foreach (int operand in new[] { 1, 0x1000, 0x1000, 0 })
        {
            int before = Index();
            int sum = before + operand;
            int result = sum % 65536;
            int f = (r.F & 0xC4) | ((result / 256) & 0x28);
            if (before % 4096 + operand % 4096 > 4095) f |= 16;
            if (sum > 65535) f |= 1;
            Assert.Equal(15UL, cpu.Step());
            Assert.Equal((ushort)result, Index());
            Assert.Equal((byte)f, r.F);
            Assert.Equal(unchecked((ushort)(before + 1)), r.WZ);
        }
        Assert.Equal((ushort)0x2000, Index());
        Assert.Equal(20UL, cpu.Step());
        Assert.Equal((byte)0x20, bus.Memory[0x4001]);
        Assert.Equal((ushort)0x4001, r.WZ);
        Assert.Equal(20UL, cpu.Step());
        Assert.Equal((ushort)0x2000, Index());
        Assert.Equal(15UL, cpu.Step());
        Assert.Equal((ushort)0xFFFE, r.SP);
        Assert.Equal((byte)0x20, bus.Memory[0xFFFF]);
        Assert.Equal(14UL, cpu.Step());
        Assert.Equal((ushort)0, r.SP);
        bus.Memory[0] = 0x34;
        bus.Memory[1] = 0x12;
        Assert.Equal(23UL, cpu.Step());
        Assert.Equal((ushort)0x1234, Index());
        Assert.Equal((ushort)0x1234, r.WZ);
        Assert.Equal((byte)0x20, bus.Memory[1]);
        Assert.Equal(10UL, cpu.Step());
        Assert.Equal((ushort)0x1234, r.SP);
        Assert.Equal(8UL, cpu.Step());
        Assert.Equal((ushort)0x1234, r.PC);
        Assert.Equal((ushort)0xABCD, r.HL);
        ushort Index() => prefix == 0xDD ? r.IX : r.IY;
    }

    [Theory]
    [InlineData(0xDD)]
    [InlineData(0xFD)]
    public void IgnoredPrefixPreservesOrdinaryRegisterSemantics(int prefix)
    {
        // Literal unprefixed table is independently maintained in boundary tests.
        var unsupported = new HashSet<int> { 0x37, 0x3F, 0xCB, 0xD3, 0xDB, 0xDD, 0xED, 0xFD };
        var affected = new HashSet<int> { 0x09, 0x19, 0x21, 0x22, 0x23, 0x29, 0x2A, 0x2B, 0x39, 0xE1, 0xE3, 0xE5, 0xE9, 0xF9,
            0x24, 0x25, 0x26, 0x2C, 0x2D, 0x2E, 0x34, 0x35, 0x36 };
        foreach (object[] row in Loads()) affected.Add((int)row[1]);
        foreach (object[] row in AluSources()) affected.Add((int)row[1]);
        Assert.Equal(85, affected.Count);
        int count = 0;
        for (int opcode = 0; opcode < 256; opcode++)
        {
            if (unsupported.Contains(opcode) || affected.Contains(opcode)) continue;
            count++;
            var plain = new RecordingBus();
            var indexed = new RecordingBus();
            plain.Memory[0x100] = (byte)opcode;
            indexed.Memory[0xFF] = (byte)prefix;
            indexed.Memory[0x100] = (byte)opcode;
            var a = Initial(0x100);
            var b = Initial(0xFF);
            ulong time = new Z80Cpu(plain, a).Step();
            Assert.Equal(time + 4, new Z80Cpu(indexed, b).Step());
            Assert.Equal(a.AF, b.AF);
            Assert.Equal(a.BC, b.BC);
            Assert.Equal(a.DE, b.DE);
            Assert.Equal(a.HL, b.HL);
            Assert.Equal(a.SP, b.SP);
            Assert.Equal(a.PC, b.PC);
            Assert.Equal(a.WZ, b.WZ);
            Assert.Equal((ushort)0x9876, b.IX);
            Assert.Equal((ushort)0xFEDC, b.IY);
            Assert.Equal(plain.Memory, indexed.Memory.Select((v, i) => i == 0xFF ? (byte)0 : v).ToArray());
        }
        Assert.Equal(163, count);
        static Z80Registers Initial(ushort pc) => new() { PC = pc, AF = 0x1234, BC = 0x2233, DE = 0x4455, HL = 0x6677, SP = 0x8000, IX = 0x9876, IY = 0xFEDC, WZ = 0xABCD };
    }

    [Fact]
    public void IndexedCbWaitsSampleLiveMemoryAndNmiWaitsForNextStep()
    {
        var bus = new RecordingBus();
        byte[] program = [0xDD, 0xCB, 1, 0x00];
        program.CopyTo(bus.Memory, 0);
        var r = new Z80Registers { IX = 0x3FFF, HL = 0x1234, SP = 0x8000, F = 0xFF };
        var cpu = new Z80Cpu(bus, r);
        bus.WaitStates = cycle => cycle.Kind == Z80BusCycleKind.MemoryRead && cycle.Address == 0x4000 ? 2 : 0;
        bus.Advancing = time =>
        {
            if (time == 4) cpu.SetNmiLine(true);
            if (time == 21) bus.Memory[0x4000] = 0x80;
        };
        Assert.Equal(25UL, cpu.Step());
        Assert.Equal((byte)1, r.B);
        Assert.Equal((byte)1, bus.Memory[0x4000]);
        Assert.Equal((byte)1, r.F);
        Assert.True(cpu.IsNmiPending);
        Assert.Equal(new CycleEvent(Z80BusCycleKind.MemoryRead, 0x4000, 16, 21, 21, 0x80), bus.Events[5]);
        Assert.Equal(11UL, cpu.Step());
        Assert.Equal((ushort)0x66, r.PC);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void IndexedCbFailuresRetainOnlyCompletedEffects(int failedCycle)
    {
        var bus = new RecordingBus();
        byte[] program = [0xFD, 0xCB, 0, 0];
        program.CopyTo(bus.Memory, 0);
        bus.Memory[0x4000] = 0x80;
        int cycleNumber = 0;
        bus.WaitStates = _ => ++cycleNumber == failedCycle + 1 ? throw new IOException("test bus fault") : 0;
        var r = new Z80Registers { IY = 0x4000, B = 0xA5, F = 0xFF, WZ = 0x9876 };
        var cpu = new Z80Cpu(bus, r);
        Assert.Throws<IOException>(() => cpu.Step());
        Assert.True(cpu.IsFaulted);
        Assert.Equal((byte)0xA5, r.B);
        Assert.Equal((byte)0xFF, r.F);
        Assert.Equal((byte)0x80, bus.Memory[0x4000]);
        Assert.Equal(failedCycle >= 5 ? (ushort)0x4000 : (ushort)0x9876, r.WZ);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
    }

    [Fact]
    public void IgnoredRomWriteStillCopiesComputedResultToOrdinaryH()
    {
        var bus = new ReadOnlyOperandBus();
        var r = new Z80Registers { IX = 0x4000, HL = 0x1234 };
        Assert.Equal(23UL, new Z80Cpu(bus, r).Step());
        Assert.Equal((byte)1, r.H);
        Assert.Equal((byte)0x34, r.L);
        Assert.Equal((ushort)0x4000, r.IX);
        Assert.Equal((byte)1, r.F);
        Assert.Equal(1, bus.Writes);
    }

    private sealed class ReadOnlyOperandBus : Z80Bus
    {
        public int Writes { get; private set; }
        protected override byte ReadMemory(ushort address) => address switch
        {
            0 => 0xDD,
            1 => 0xCB,
            2 => 0,
            3 => 4,
            0x4000 => 0x80,
            _ => 0
        };
        protected override void WriteMemory(ushort address, byte value) => Writes++;
    }

    [Theory]
    [InlineData(0xDD, 0x26)]
    [InlineData(0xDD, 0x2E)]
    [InlineData(0xFD, 0x26)]
    [InlineData(0xFD, 0x2E)]
    public void ImmediateHalvesPreserveOtherHalfAndFlags(int prefix, int opcode)
    {
        for (int value = 0; value < 256; value++)
        {
            var bus = new RecordingBus();
            bus.Memory[0] = (byte)prefix;
            bus.Memory[1] = (byte)opcode;
            bus.Memory[2] = (byte)value;
            var r = new Z80Registers { IX = 0x1234, IY = 0x1234, HL = 0xABCD, F = (byte)value };
            Assert.Equal(11UL, new Z80Cpu(bus, r).Step());
            ushort expected = opcode == 0x26 ? (ushort)(value * 256 + 0x34) : (ushort)(0x1200 + value);
            Assert.Equal(prefix == 0xDD ? expected : (ushort)0x1234, r.IX);
            Assert.Equal(prefix == 0xFD ? expected : (ushort)0x1234, r.IY);
            Assert.Equal((ushort)0xABCD, r.HL);
            Assert.Equal((byte)value, r.F);
        }
    }

    [Theory]
    [InlineData(0xDD)]
    [InlineData(0xFD)]
    public void WordIncrementsCheckWholeRangeAndAddChecksCarryBoundaries(int prefix)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)prefix;
        var cpu = new Z80Cpu(bus);
        for (int value = 0; value < 65536; value++)
        {
            foreach (int opcode in new[] { 0x23, 0x2B })
            {
                cpu.Reset();
                bus.Memory[1] = (byte)opcode;
                Set((ushort)value);
                cpu.Registers.F = (byte)(value % 256);
                bus.Events.Clear();
                Assert.Equal(10UL, cpu.Step());
                Assert.Equal(unchecked((ushort)(value + (opcode == 0x23 ? 1 : -1))), Get());
                Assert.Equal((byte)(value % 256), cpu.Registers.F);
            }
            foreach (int operand in new[] { 0, 1, 0x0FFF, 0x1000, 0x7FFF, 0x8000, 0xFFFF })
            {
                cpu.Reset();
                bus.Memory[1] = 0x09;
                Set((ushort)value);
                cpu.Registers.BC = (ushort)operand;
                cpu.Registers.F = (byte)(value % 256);
                bus.Events.Clear();
                int sum = value + operand;
                int result = sum % 65536;
                int f = (value & 0xC4) | ((result / 256) & 0x28);
                if (value % 4096 + operand % 4096 > 4095) f |= 16;
                if (sum > 65535) f |= 1;
                Assert.Equal(15UL, cpu.Step());
                Assert.Equal((ushort)result, Get());
                Assert.Equal((byte)f, cpu.Registers.F);
                Assert.Equal(unchecked((ushort)(value + 1)), cpu.Registers.WZ);
            }
        }
        void Set(ushort value)
        {
            if (prefix == 0xDD) cpu.Registers.IX = value;
            else cpu.Registers.IY = value;
        }
        ushort Get() => prefix == 0xDD ? cpu.Registers.IX : cpu.Registers.IY;
    }

    [Theory]
    [InlineData(0xDD)]
    [InlineData(0xFD)]
    public void IndexedCbAllDisplacementsAddressAndRefreshWrap(int prefix)
    {
        for (int d = 0; d < 256; d++)
        {
            var bus = new RecordingBus();
            ushort address = unchecked((ushort)(0xFF80 + (d < 128 ? d : d - 256)));
            bus.Memory[0xFFFF] = (byte)prefix;
            bus.Memory[0] = 0xCB;
            bus.Memory[1] = (byte)d;
            bus.Memory[2] = 0x40;
            // Do not overwrite the prefix stream when the effective address overlaps it.
            byte value = bus.Memory[address];
            var r = new Z80Registers { PC = 0xFFFF, IX = 0xFF80, IY = 0xFF80, R = 0xFF, F = 1 };
            Assert.Equal(20UL, new Z80Cpu(bus, r).Step());
            Assert.Equal(address, r.WZ);
            Assert.Equal((ushort)3, r.PC);
            Assert.Equal((byte)0x81, r.R);
            Assert.Equal((byte)(0x11 | ((address / 256) & 0x28) | (value % 2 == 0 ? 0x44 : 0)), r.F);
        }
    }

    [Theory]
    [InlineData(0xDD)]
    [InlineData(0xFD)]
    public void EdCancellationPreservesIndexAndRejectsEveryUnsupportedPayload(int prefix)
    {
        var supported = new HashSet<int> { 0x44, 0x45, 0x46, 0x4D, 0x56, 0x5E, 0x42, 0x4A, 0x52, 0x5A, 0x62, 0x6A, 0x72, 0x7A, 0x43, 0x4B, 0x53, 0x5B, 0x63, 0x6B, 0x73, 0x7B, 0x47, 0x4F, 0x57, 0x5F, 0x67, 0x6F };
        for (int payload = 0; payload < 256; payload++)
        {
            var bus = new RecordingBus();
            bus.Memory[0] = (byte)prefix;
            bus.Memory[1] = 0xED;
            bus.Memory[2] = (byte)payload;
            var r = new Z80Registers { IX = 0x1234, IY = 0x5678, A = 1, SP = 0x8000 };
            var cpu = new Z80Cpu(bus, r);
            if (supported.Contains(payload))
            {
                ulong expected = payload is 0x45 or 0x4D ? 18UL
                    : payload is 0x42 or 0x4A or 0x52 or 0x5A or 0x62 or 0x6A or 0x72 or 0x7A ? 19UL
                    : payload is 0x43 or 0x4B or 0x53 or 0x5B or 0x63 or 0x6B or 0x73 or 0x7B ? 24UL
                    : payload is 0x47 or 0x4F or 0x57 or 0x5F ? 13UL
                    : payload is 0x67 or 0x6F ? 22UL : 12UL;
                Assert.Equal(expected, cpu.Step());
            }
            else
            {
                var error = Assert.Throws<UnsupportedOpcodeException>(() => cpu.Step());
                Assert.Equal((byte?)0xED, error.Prefix);
                Assert.Equal((byte)payload, error.Opcode);
                Assert.Equal((ushort)0, error.Address);
                Assert.Equal(12UL, bus.TStates);
            }
            Assert.Equal((ushort)0x1234, r.IX);
            Assert.Equal((ushort)0x5678, r.IY);
            // LD R,A overwrites the three fetch increments with the original A=1.
            Assert.Equal(payload == 0x4F ? (byte)1 : (byte)3, r.R);
        }
    }

    [Fact]
    public void SyntheticIndexProgramUsesBothIndexesAndOrdinaryCopyDestination()
    {
        var bus = new RecordingBus();
        byte[] program = [0xDD, 0x21, 0x00, 0x40, 0xFD, 0x21, 0x01, 0x40,
            0xDD, 0x36, 0, 0x80, 0xDD, 0xCB, 0, 0x00, 0xFD, 0x70, 0,
            0xFD, 0x7E, 0, 0x76];
        program.CopyTo(bus.Memory, 0);
        var cpu = new Z80Cpu(bus);
        for (int step = 0; step < 7; step++) cpu.Step();
        Assert.True(cpu.IsHalted);
        Assert.Equal((byte)1, cpu.Registers.A);
        Assert.Equal((byte)1, cpu.Registers.B);
        Assert.Equal((byte)1, bus.Memory[0x4000]);
        Assert.Equal((byte)1, bus.Memory[0x4001]);
        Assert.Equal((byte)13, cpu.Registers.R);
        Assert.Equal((ushort)23, cpu.Registers.PC);
        Assert.Equal(112UL, bus.TStates);
    }

    private static (byte A, byte F) ExpectedAlu(int operation, int a, int operand, int inputCarry)
    {
        int carry = operation is 1 or 3 ? inputCarry : 0;
        bool subtract = operation is 2 or 3 or 7;
        int raw = operation switch
        {
            0 or 1 => a + operand + carry,
            2 or 3 or 7 => a - operand - carry,
            4 => a & operand,
            5 => a ^ operand,
            6 => a | operand,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        byte result = unchecked((byte)raw);
        int flags = result >= 128 ? 128 : 0;
        if (result == 0) flags |= 64;
        int xy = operation == 7 ? operand : result;
        if ((xy / 8) % 2 == 1) flags |= 8;
        if ((xy / 32) % 2 == 1) flags |= 32;
        if (operation is 4 or 5 or 6)
        {
            int ones = 0;
            for (int bit = 0; bit < 8; bit++) ones += (result >> bit) & 1;
            if (ones % 2 == 0) flags |= 4;
            if (operation == 4) flags |= 16;
        }
        else
        {
            int signedA = a < 128 ? a : a - 256;
            int signedOperand = operand < 128 ? operand : operand - 256;
            int signed = subtract ? signedA - signedOperand - carry : signedA + signedOperand + carry;
            if (signed < -128 || signed > 127) flags |= 4;
            int nibble = subtract ? a % 16 - operand % 16 - carry : a % 16 + operand % 16 + carry;
            if (nibble < 0 || nibble > 15) flags |= 16;
            if (raw < 0 || raw > 255) flags |= 1;
            if (subtract) flags |= 2;
        }
        return ((byte)(operation == 7 ? a : result), (byte)flags);
    }

    private static (int Result, int Flags) ExpectedBit(int opcode, int value, int flags, int wzHigh)
    {
        int row = opcode / 8;
        if (row >= 16)
        {
            int mask = 1 << (row % 8);
            return (row < 24 ? value & (255 - mask) : value | mask, flags);
        }
        if (row >= 8)
        {
            bool set = (value & (1 << (row - 8))) != 0;
            int f = (flags & 1) | 0x10 | ((wzHigh) & 0x28);
            if (!set) f |= 0x44;
            if (row == 15 && set) f |= 0x80;
            return (value, f);
        }
        int result = row switch
        {
            0 => (value * 2) % 256 + value / 128,
            1 => value / 2 + (value % 2) * 128,
            2 => (value * 2) % 256 + flags % 2,
            3 => value / 2 + (flags % 2) * 128,
            4 => (value * 2) % 256,
            5 => value / 2 + (value / 128) * 128,
            6 => (value * 2) % 256 + 1,
            7 => value / 2,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode))
        };
        int expected = result & 0xA8;
        if (result == 0) expected |= 0x40;
        int bits = 0;
        for (int bit = 0; bit < 8; bit++) bits += (result >> bit) & 1;
        if (bits % 2 == 0) expected |= 4;
        expected |= row is 1 or 3 or 5 or 7 ? value % 2 : value / 128;
        return (result, expected);
    }
}
