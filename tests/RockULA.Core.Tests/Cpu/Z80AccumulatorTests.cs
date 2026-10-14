using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80AccumulatorTests
{
    // Zilog UM008011 pp.173–175,205–212; Young v0.90 §4.7,8.5,8.7.
    [Theory]
    [InlineData(0x07, true, false)]
    [InlineData(0x0F, false, false)]
    [InlineData(0x17, true, true)]
    [InlineData(0x1F, false, true)]
    public void AccumulatorRotationsExhaustEveryAAndFlagByte(int opcode, bool left, bool throughCarry)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)opcode;
        var r = new Z80Registers { BC = 0x1234, DE = 0x5678, HL = 0xABCD, SP = 0x8000 };
        var cpu = new Z80Cpu(bus, r);
        for (int a = 0; a < 256; a++)
        {
            for (int flags = 0; flags < 256; flags++)
            {
                r.PC = 0;
                r.R = 0x7F;
                r.A = (byte)a;
                r.F = (byte)flags;
                int outgoing = left ? a / 128 : a % 2;
                int incoming = throughCarry ? flags % 2 : outgoing;
                byte result = (byte)(left ? (a * 2) % 256 + incoming : a / 2 + incoming * 128);
                byte expectedFlags = (byte)((flags & 0xC4) | (result & 0x28) | outgoing);
                bus.Events.Clear();
                Assert.Equal(4UL, cpu.Step());
                Assert.Equal(result, r.A);
                Assert.Equal(expectedFlags, r.F);
                Assert.Equal((ushort)1, r.PC);
                Assert.Equal((byte)0, r.R);
                Assert.Single(bus.Events);
                Assert.Equal((ushort)0x1234, r.BC);
                Assert.Equal((ushort)0x5678, r.DE);
                Assert.Equal((ushort)0xABCD, r.HL);
                Assert.Equal((ushort)0x8000, r.SP);
            }
        }
    }

    [Fact]
    public void ComplementExhaustsAAndFlagsAndCopiesResultXy()
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0x2F;
        var r = new Z80Registers();
        var cpu = new Z80Cpu(bus, r);
        for (int a = 0; a < 256; a++)
        {
            for (int flags = 0; flags < 256; flags++)
            {
                r.PC = 0;
                r.A = (byte)a;
                r.F = (byte)flags;
                bus.Events.Clear();
                Assert.Equal(4UL, cpu.Step());
                Assert.Equal((byte)(255 - a), r.A);
                Assert.Equal((byte)((flags & 0xC5) | ((255 - a) & 0x28) | 0x12), r.F);
                Assert.Single(bus.Events);
            }
        }
    }

    [Fact]
    public void DecimalAdjustExhaustsEveryAAndFlagByteAgainstNibbleTables()
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0x27;
        var r = new Z80Registers { BC = 0x1234, DE = 0x5678, HL = 0xABCD, SP = 0x8000 };
        var cpu = new Z80Cpu(bus, r);
        for (int a = 0; a < 256; a++)
        {
            for (int flags = 0; flags < 256; flags++)
            {
                r.PC = 0;
                r.R = 0xFF;
                r.A = (byte)a;
                r.F = (byte)flags;
                bus.Events.Clear();
                var expected = DecimalTable(a, flags);
                Assert.Equal(4UL, cpu.Step());
                Assert.Equal(expected.A, r.A);
                Assert.Equal(expected.F, r.F);
                Assert.Equal((ushort)1, r.PC);
                Assert.Equal((byte)0x80, r.R);
                Assert.Single(bus.Events);
                Assert.Equal((ushort)0x1234, r.BC);
                Assert.Equal((ushort)0x5678, r.DE);
                Assert.Equal((ushort)0xABCD, r.HL);
                Assert.Equal((ushort)0x8000, r.SP);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BcdArithmeticMatchesDecimalIntegersForEveryTwoDigitPair(bool subtract)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = subtract ? (byte)0xDE : (byte)0xCE;
        bus.Memory[2] = 0x27;
        var r = new Z80Registers();
        var cpu = new Z80Cpu(bus, r);
        for (int a = 0; a < 100; a++)
        {
            for (int b = 0; b < 100; b++)
            {
                for (int carry = 0; carry < 2; carry++)
                {
                    r.PC = 0;
                    r.A = Bcd(a);
                    r.F = (byte)carry;
                    bus.Memory[1] = Bcd(b);
                    bus.Events.Clear();
                    Assert.Equal(7UL, cpu.Step());
                    Assert.Equal(4UL, cpu.Step());
                    int decimalResult = subtract ? a - b - carry : a + b + carry;
                    Assert.Equal(Bcd((decimalResult + 100) % 100), r.A);
                    Assert.Equal(decimalResult < 0 || decimalResult > 99, (r.F & 1) != 0);
                    Assert.Equal(subtract, (r.F & 2) != 0);
                    Assert.Equal((ushort)3, r.PC);
                }
            }
        }
    }

    private static byte Bcd(int value) => (byte)(value / 10 * 16 + value % 10);

    private static (byte A, byte F) DecimalTable(int a, int flags)
    {
        bool carry = (flags & 1) != 0;
        bool half = (flags & 16) != 0;
        bool subtract = (flags & 2) != 0;
        int high = a / 16;
        int low = a % 16;
        // Self-authored representation of the referenced nibble categories, not a
        // call to the production correction/flag logic. Rows C/H; columns high/low classes.
        int[][] corrections =
        [
            [0, 6, 0, 0x66, 0x60, 0x66],
            [6, 6, 6, 0x66, 0x66, 0x66],
            [0x60, 0x66, 0x60, 0x66, 0x60, 0x66],
            [0x66, 0x66, 0x66, 0x66, 0x66, 0x66]
        ];
        int highClass = high < 9 ? 0 : high == 9 ? 1 : 2;
        int lowClass = low < 10 ? 0 : 1;
        int correction = corrections[(carry ? 2 : 0) + (half ? 1 : 0)][highClass * 2 + lowClass];
        byte result = unchecked((byte)(a + (subtract ? -correction : correction)));
        bool newCarry = carry || highClass == 2 || (highClass == 1 && lowClass == 1);
        bool newHalf = subtract ? half && low < 6 : lowClass == 1;
        int output = (result & 0xA8) | (result == 0 ? 0x40 : 0) | (subtract ? 2 : 0)
            | (newCarry ? 1 : 0) | (newHalf ? 16 : 0);
        int ones = 0;
        for (int bit = 0; bit < 8; bit++) ones += (result >> bit) & 1;
        if (ones % 2 == 0) output |= 4;
        return (result, (byte)output);
    }
}
