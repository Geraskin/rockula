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
        Assert.Equal((byte)7, r.R);
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
