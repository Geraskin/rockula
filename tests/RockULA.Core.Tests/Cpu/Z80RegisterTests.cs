using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80RegisterTests
{
    [Theory]
    [InlineData("AF", "A", "F")]
    [InlineData("BC", "B", "C")]
    [InlineData("DE", "D", "E")]
    [InlineData("HL", "H", "L")]
    [InlineData("AlternateAF", "AlternateA", "AlternateF")]
    [InlineData("AlternateBC", "AlternateB", "AlternateC")]
    [InlineData("AlternateDE", "AlternateD", "AlternateE")]
    [InlineData("AlternateHL", "AlternateH", "AlternateL")]
    public void PairAndByteViewsShareStorage(string pair, string high, string low)
    {
        var registers = new Z80Registers();
        var type = typeof(Z80Registers);
        type.GetProperty(pair)!.SetValue(registers, (ushort)0x1234);
        Assert.Equal((byte)0x12, (byte)type.GetProperty(high)!.GetValue(registers)!);
        Assert.Equal((byte)0x34, (byte)type.GetProperty(low)!.GetValue(registers)!);

        type.GetProperty(high)!.SetValue(registers, (byte)0xAB);
        type.GetProperty(low)!.SetValue(registers, (byte)0xCD);
        Assert.Equal((ushort)0xABCD, (ushort)type.GetProperty(pair)!.GetValue(registers)!);
    }

    [Fact]
    public void AlternateRegistersDoNotAliasTheMainSet()
    {
        var registers = new Z80Registers
        {
            AF = 0x1234,
            BC = 0x5678,
            DE = 0x9ABC,
            HL = 0xDEF0,
            AlternateAF = 0x0102,
            AlternateBC = 0x0304,
            AlternateDE = 0x0506,
            AlternateHL = 0x0708
        };

        Assert.Equal(new ushort[] { 0x1234, 0x5678, 0x9ABC, 0xDEF0 },
            new[] { registers.AF, registers.BC, registers.DE, registers.HL });
        Assert.Equal(new ushort[] { 0x0102, 0x0304, 0x0506, 0x0708 },
            new[] { registers.AlternateAF, registers.AlternateBC, registers.AlternateDE, registers.AlternateHL });
    }

    [Fact]
    public void ResetUsesTheDeclaredDeterministicInitializationPolicy()
    {
        var registers = new Z80Registers
        {
            AF = 0xFFFF, BC = 0xFFFF, DE = 0xFFFF, HL = 0xFFFF,
            AlternateAF = 0xFFFF, AlternateBC = 0xFFFF, AlternateDE = 0xFFFF, AlternateHL = 0xFFFF,
            IX = 0xFFFF, IY = 0xFFFF, PC = 0xFFFF, SP = 0x1234,
            I = 0xFF, R = 0xFF, Iff1 = true, Iff2 = true, InterruptMode = 2
        };

        registers.Reset();

        Assert.Equal(new ushort[11], new[]
        {
            registers.AF, registers.BC, registers.DE, registers.HL,
            registers.AlternateAF, registers.AlternateBC, registers.AlternateDE, registers.AlternateHL,
            registers.IX, registers.IY, registers.PC
        });
        Assert.Equal((ushort)0xFFFF, registers.SP);
        Assert.Equal((byte)0, registers.I);
        Assert.Equal((byte)0, registers.R);
        Assert.False(registers.Iff1);
        Assert.False(registers.Iff2);
        Assert.Equal((byte)0, registers.InterruptMode);
    }
}
