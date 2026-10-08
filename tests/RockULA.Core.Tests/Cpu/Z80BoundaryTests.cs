using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80BoundaryTests
{
    [Theory]
    [InlineData(0x00, 0x01)]
    [InlineData(0x7F, 0x00)]
    [InlineData(0x80, 0x81)]
    [InlineData(0xFF, 0x80)]
    public void NopIncrementsOnlyTheLowSevenRefreshBitsAndWrapsPc(int initialR, int expectedR)
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFF] = 0;
        var registers = new Z80Registers
        {
            PC = 0xFFFF,
            R = (byte)initialR,
            AF = 0xA5FF,
            BC = 0x1234,
            DE = 0x5678,
            HL = 0x9ABC,
            IX = 0x1357,
            IY = 0x2468,
            AlternateAF = 0x1111,
            AlternateBC = 0x2222,
            AlternateDE = 0x3333,
            AlternateHL = 0x4444,
            I = 0x80,
            Iff1 = true,
            Iff2 = true,
            InterruptMode = 2
        };
        var cpu = new Z80Cpu(bus, registers);

        Assert.Equal(4UL, cpu.Step());
        Assert.Equal((ushort)0, registers.PC);
        Assert.Equal((byte)expectedR, registers.R);
        Assert.Equal(new ushort[] { 0xA5FF, 0x1234, 0x5678, 0x9ABC, 0x1357, 0x2468,
                0x1111, 0x2222, 0x3333, 0x4444, 0xFFFF },
            new[] { registers.AF, registers.BC, registers.DE, registers.HL, registers.IX, registers.IY,
                registers.AlternateAF, registers.AlternateBC, registers.AlternateDE, registers.AlternateHL, registers.SP });
        Assert.Equal((byte)0x80, registers.I);
        Assert.True(registers.Iff1);
        Assert.True(registers.Iff2);
        Assert.Equal((byte)2, registers.InterruptMode);
        Assert.False(cpu.IsFaulted);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0xFFFF, 0, 3, 4, 0)
        }, bus.Events.ToArray());
    }

    [Theory]
    [InlineData(0x76)]
    [InlineData(0x27)]
    [InlineData(0xD3)]
    [InlineData(0xCB)]
    [InlineData(0xDD)]
    [InlineData(0xED)]
    [InlineData(0xFD)]
    [InlineData(0xF9)]
    public void UnsupportedInstructionsAndPrefixesFaultAtTheirOriginalAddress(int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0x1234] = (byte)opcode;
        bus.Memory[0x1235] = 0;
        var registers = new Z80Registers { PC = 0x1234, F = 0xFF };
        var cpu = new Z80Cpu(bus, registers);

        var error = Assert.Throws<UnsupportedOpcodeException>(() => cpu.Step());

        Assert.Equal((ushort)0x1234, error.Address);
        Assert.Equal((byte)opcode, error.Opcode);
        Assert.Contains("0x1234", error.Message);
        Assert.True(cpu.IsFaulted);
        Assert.Equal((ushort)0x1235, registers.PC);
        Assert.Equal((byte)1, registers.R);
        Assert.Equal((byte)0xFF, registers.F);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
        Assert.Equal(4UL, bus.TStates);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0x1234, 0, 3, 4, (byte)opcode)
        }, bus.Events.ToArray());
    }

    [Fact]
    public void EveryBaseEncodingOutsideTheDeclaredSliceFailsExplicitly()
    {
        // Independent, literal encoding coverage: do not query the production decoder.
        HashSet<byte> supported =
        [
            0x00, 0x01, 0x02, 0x06, 0x0A, 0x0E, 0x11, 0x12, 0x16, 0x1A, 0x1E, 0x21,
            0x22, 0x26, 0x2A, 0x2E, 0x31, 0x32, 0x36, 0x3A, 0x3E, 0x40, 0x41, 0x42,
            0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E,
            0x4F, 0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A,
            0x5B, 0x5C, 0x5D, 0x5E, 0x5F, 0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66,
            0x67, 0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F, 0x70, 0x71, 0x72,
            0x73, 0x74, 0x75, 0x77, 0x78, 0x79, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F,
            0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8A, 0x8B,
            0x8C, 0x8D, 0x8E, 0x8F, 0x90, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97,
            0x98, 0x99, 0x9A, 0x9B, 0x9C, 0x9D, 0x9E, 0x9F, 0xA0, 0xA1, 0xA2, 0xA3,
            0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xAB, 0xAC, 0xAD, 0xAE, 0xAF,
            0xB0, 0xB1, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xBB,
            0xBC, 0xBD, 0xBE, 0xBF, 0xC6, 0xCE, 0xD6, 0xDE, 0xE6, 0xEE, 0xF6, 0xFE,
            0x04, 0x0C, 0x14, 0x1C, 0x24, 0x2C, 0x34, 0x3C, 0x05, 0x0D, 0x15, 0x1D,
            0x25, 0x2D, 0x35, 0x3D, 0xC2, 0xCA, 0xD2, 0xDA, 0xE2, 0xEA, 0xF2, 0xFA,
            0xC3, 0xE9, 0x18, 0x20, 0x28, 0x30, 0x38, 0x10, 0xCD, 0xC4, 0xCC, 0xD4,
            0xDC, 0xE4, 0xEC, 0xF4, 0xFC, 0xC9, 0xC0, 0xC8, 0xD0, 0xD8, 0xE0, 0xE8,
            0xF0, 0xF8, 0xC7, 0xCF, 0xD7, 0xDF, 0xE7, 0xEF, 0xF7, 0xFF, 0xC5, 0xD5,
            0xE5, 0xF5, 0xC1, 0xD1, 0xE1, 0xF1
        ];

        for (int opcode = 0; opcode <= 0xFF; opcode++)
        {
            if (supported.Contains((byte)opcode))
            {
                continue;
            }

            var bus = new RecordingBus();
            bus.Memory[0] = (byte)opcode;
            var cpu = new Z80Cpu(bus);
            Assert.Throws<UnsupportedOpcodeException>(() => cpu.Step());
            Assert.True(cpu.IsFaulted);
            Assert.Equal(4UL, bus.TStates);
        }
    }

    [Fact]
    public void ExplicitResetClearsCpuFaultAndRegistersWithoutRewindingTheBus()
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0x76;
        bus.Memory[0x8000] = 0x42;
        var registers = new Z80Registers { AF = 0xFFFF, SP = 0x1234 };
        var cpu = new Z80Cpu(bus, registers);
        Assert.Throws<UnsupportedOpcodeException>(() => cpu.Step());
        bus.Memory[0] = 0;

        cpu.Reset();

        Assert.False(cpu.IsFaulted);
        Assert.Equal((ushort)0, registers.PC);
        Assert.Equal((ushort)0, registers.AF);
        Assert.Equal((byte)0, registers.R);
        Assert.Equal((ushort)0xFFFF, registers.SP);
        Assert.Equal(4UL, bus.TStates);
        Assert.Equal((byte)0x42, bus.Memory[0x8000]);
        Assert.Equal(4UL, cpu.Step());
        Assert.Equal(8UL, bus.TStates);
    }

    [Fact]
    public void MissingBusIsRejectedAtConstruction()
    {
        Assert.Throws<ArgumentNullException>(() => new Z80Cpu(null!));
    }
}
