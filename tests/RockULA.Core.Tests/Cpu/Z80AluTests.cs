using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80AluTests
{
    // Zilog UM008011 pp.145–171; Young v0.90 §2.2/8.4 for parity and X/Y.
    [Theory]
    [InlineData(0xC6, 0)]
    [InlineData(0xCE, 1)]
    [InlineData(0xD6, 2)]
    [InlineData(0xDE, 3)]
    [InlineData(0xE6, 4)]
    [InlineData(0xEE, 5)]
    [InlineData(0xF6, 6)]
    [InlineData(0xFE, 7)]
    public void ImmediateAluExhaustsEveryPairAndInputCarry(int opcode, int operation)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)opcode;
        var cpu = new Z80Cpu(bus);
        for (int carry = 0; carry < 2; carry++)
        {
            for (int a = 0; a < 256; a++)
            {
                for (int operand = 0; operand < 256; operand++)
                {
                    bus.Events.Clear();
                    cpu.Registers.PC = 0;
                    cpu.Registers.A = (byte)a;
                    cpu.Registers.F = (byte)(0xFE | carry);
                    bus.Memory[1] = (byte)operand;
                    var expected = Expected(operation, a, operand, carry);
                    Assert.Equal(7UL, cpu.Step());
                    Assert.Equal(expected.A, cpu.Registers.A);
                    Assert.Equal(expected.F, cpu.Registers.F);
                    Assert.Equal((ushort)2, cpu.Registers.PC);
                }
            }
        }
    }

    public static IEnumerable<object[]> RegisterEncodings()
    {
        // Literal rows independent of the production decoder.
        byte[][] rows =
        [
            [0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87],
            [0x88, 0x89, 0x8A, 0x8B, 0x8C, 0x8D, 0x8E, 0x8F],
            [0x90, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97],
            [0x98, 0x99, 0x9A, 0x9B, 0x9C, 0x9D, 0x9E, 0x9F],
            [0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7],
            [0xA8, 0xA9, 0xAA, 0xAB, 0xAC, 0xAD, 0xAE, 0xAF],
            [0xB0, 0xB1, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7],
            [0xB8, 0xB9, 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF]
        ];
        string[] sources = ["B", "C", "D", "E", "H", "L", "Memory", "A"];
        for (int operation = 0; operation < rows.Length; operation++)
        {
            for (int source = 0; source < sources.Length; source++)
            {
                yield return [rows[operation][source], operation, sources[source]];
            }
        }
    }

    [Theory]
    [MemberData(nameof(RegisterEncodings))]
    public void EveryRegisterEncodingReadsItsSourceAndPreservesOtherRegisters(byte opcode, int operation, string source)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = opcode;
        var r = new Z80Registers
        {
            A = 0x7F,
            F = 0xFF,
            BC = 0x1280,
            DE = 0xFE01,
            HL = 0x4028,
            SP = 0xA000,
            IX = 0x1357,
            IY = 0x2468,
            AlternateAF = 0xABCD,
            AlternateBC = 0x9876,
            AlternateDE = 0x5432,
            AlternateHL = 0x1010
        };
        bus.Memory[0x4028] = 0xA5;
        byte operand = source == "Memory" ? bus.Memory[r.HL]
            : (byte)typeof(Z80Registers).GetProperty(source)!.GetValue(r)!;
        var expected = Expected(operation, r.A, operand, 1);
        var cpu = new Z80Cpu(bus, r);
        Assert.Equal(source == "Memory" ? 7UL : 4UL, cpu.Step());
        Assert.Equal(expected.A, r.A);
        Assert.Equal(expected.F, r.F);
        Assert.Equal(new ushort[] { 0x1280, 0xFE01, 0x4028, 0xA000, 0x1357, 0x2468, 0xABCD, 0x9876, 0x5432, 0x1010 },
            new[] { r.BC, r.DE, r.HL, r.SP, r.IX, r.IY, r.AlternateAF, r.AlternateBC, r.AlternateDE, r.AlternateHL });
        Assert.Equal((byte)0xA5, bus.Memory[0x4028]);
        Assert.Equal((ushort)1, r.PC);
        Assert.Equal((byte)1, r.R);
        Assert.Equal(new CycleEvent(Z80BusCycleKind.OpcodeFetch, 0, 0, 3, 4, opcode), bus.Events[0]);
        if (source == "Memory")
        {
            Assert.Equal(new CycleEvent(Z80BusCycleKind.MemoryRead, 0x4028, 4, 7, 7, 0xA5), bus.Events[1]);
        }
        Assert.Equal(source == "Memory" ? 2 : 1, bus.Events.Count);
    }

    [Theory]
    [InlineData(0x04, "B", false)]
    [InlineData(0x0C, "C", false)]
    [InlineData(0x14, "D", false)]
    [InlineData(0x1C, "E", false)]
    [InlineData(0x24, "H", false)]
    [InlineData(0x2C, "L", false)]
    [InlineData(0x34, "Memory", false)]
    [InlineData(0x3C, "A", false)]
    [InlineData(0x05, "B", true)]
    [InlineData(0x0D, "C", true)]
    [InlineData(0x15, "D", true)]
    [InlineData(0x1D, "E", true)]
    [InlineData(0x25, "H", true)]
    [InlineData(0x2D, "L", true)]
    [InlineData(0x35, "Memory", true)]
    [InlineData(0x3D, "A", true)]
    public void IncrementAndDecrementExhaustValuesAndPreserveCarry(int opcode, string destination, bool decrement)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)opcode;
        var cpu = new Z80Cpu(bus);
        for (int carry = 0; carry < 2; carry++)
        {
            for (int value = 0; value < 256; value++)
            {
                bus.Events.Clear();
                cpu.Registers.PC = 0;
                cpu.Registers.HL = 0x4000;
                cpu.Registers.F = (byte)(0xFE | carry);
                if (destination == "Memory")
                {
                    bus.Memory[0x4000] = (byte)value;
                }
                else
                {
                    typeof(Z80Registers).GetProperty(destination)!.SetValue(cpu.Registers, (byte)value);
                }
                var expected = Expected(decrement ? 2 : 0, value, 1, 0);
                ulong start = bus.TStates;
                Assert.Equal(destination == "Memory" ? 11UL : 4UL, cpu.Step());
                byte actual = destination == "Memory" ? bus.Memory[0x4000]
                    : (byte)typeof(Z80Registers).GetProperty(destination)!.GetValue(cpu.Registers)!;
                Assert.Equal(expected.A, actual);
                Assert.Equal((byte)((expected.F & 0xFE) | carry), cpu.Registers.F);
                Assert.Equal((ushort)1, cpu.Registers.PC);
                if (destination == "Memory")
                {
                    Assert.Equal(new CycleEvent[]
                    {
                        new(Z80BusCycleKind.OpcodeFetch, 0, start, start + 3, start + 4, (byte)opcode),
                        new(Z80BusCycleKind.MemoryRead, 0x4000, start + 4, start + 7, start + 7, (byte)value),
                        new(Z80BusCycleKind.Internal, 0x4000, start + 7, start + 7, start + 8, 0),
                        new(Z80BusCycleKind.MemoryWrite, 0x4000, start + 8, start + 11, start + 11, expected.A)
                    }, bus.Events.ToArray());
                }
            }
        }
    }

    [Fact]
    public void ImmediateOperandWrapsAndWaitsShiftSamplingWithoutExtraRefresh()
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFF] = 0xCE;
        bus.Memory[0] = 1;
        bus.WaitStates = cycle => cycle.Kind == Z80BusCycleKind.MemoryRead ? 2 : 0;
        bus.Advancing = time =>
        {
            if (time == 9)
            {
                bus.Memory[0] = 0x80;
            }
        };
        var r = new Z80Registers { PC = 0xFFFF, A = 0x7F, F = 1, R = 0xFF };
        Assert.Equal(9UL, new Z80Cpu(bus, r).Step());
        Assert.Equal((byte)0, r.A);
        Assert.Equal((byte)0x51, r.F);
        Assert.Equal((ushort)1, r.PC);
        Assert.Equal((byte)0x80, r.R);
        Assert.Equal(new CycleEvent(Z80BusCycleKind.MemoryRead, 0, 4, 9, 9, 0x80), bus.Events[1]);
    }

    // Independent mathematical oracle: range tests rather than production bit identities.
    private static (byte A, byte F) Expected(int operation, int a, int operand, int inputCarry)
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
}
