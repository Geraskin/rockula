using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

// Independent opcode table and nominal timing expectations: Zilog UM008011-0816,
// printed pp. 71-97, 99, 102 and 107. No production decoder/helper is used as an oracle.
public sealed class Z80LoadTests
{
    public static IEnumerable<object[]> RegisterLoads =>
    [
        [0x40, "B", "B", 4],
        [0x41, "B", "C", 4],
        [0x42, "B", "D", 4],
        [0x43, "B", "E", 4],
        [0x44, "B", "H", 4],
        [0x45, "B", "L", 4],
        [0x46, "B", "(HL)", 7],
        [0x47, "B", "A", 4],
        [0x48, "C", "B", 4],
        [0x49, "C", "C", 4],
        [0x4A, "C", "D", 4],
        [0x4B, "C", "E", 4],
        [0x4C, "C", "H", 4],
        [0x4D, "C", "L", 4],
        [0x4E, "C", "(HL)", 7],
        [0x4F, "C", "A", 4],
        [0x50, "D", "B", 4],
        [0x51, "D", "C", 4],
        [0x52, "D", "D", 4],
        [0x53, "D", "E", 4],
        [0x54, "D", "H", 4],
        [0x55, "D", "L", 4],
        [0x56, "D", "(HL)", 7],
        [0x57, "D", "A", 4],
        [0x58, "E", "B", 4],
        [0x59, "E", "C", 4],
        [0x5A, "E", "D", 4],
        [0x5B, "E", "E", 4],
        [0x5C, "E", "H", 4],
        [0x5D, "E", "L", 4],
        [0x5E, "E", "(HL)", 7],
        [0x5F, "E", "A", 4],
        [0x60, "H", "B", 4],
        [0x61, "H", "C", 4],
        [0x62, "H", "D", 4],
        [0x63, "H", "E", 4],
        [0x64, "H", "H", 4],
        [0x65, "H", "L", 4],
        [0x66, "H", "(HL)", 7],
        [0x67, "H", "A", 4],
        [0x68, "L", "B", 4],
        [0x69, "L", "C", 4],
        [0x6A, "L", "D", 4],
        [0x6B, "L", "E", 4],
        [0x6C, "L", "H", 4],
        [0x6D, "L", "L", 4],
        [0x6E, "L", "(HL)", 7],
        [0x6F, "L", "A", 4],
        [0x70, "(HL)", "B", 7],
        [0x71, "(HL)", "C", 7],
        [0x72, "(HL)", "D", 7],
        [0x73, "(HL)", "E", 7],
        [0x74, "(HL)", "H", 7],
        [0x75, "(HL)", "L", 7],
        [0x77, "(HL)", "A", 7],
        [0x78, "A", "B", 4],
        [0x79, "A", "C", 4],
        [0x7A, "A", "D", 4],
        [0x7B, "A", "E", 4],
        [0x7C, "A", "H", 4],
        [0x7D, "A", "L", 4],
        [0x7E, "A", "(HL)", 7],
        [0x7F, "A", "A", 4]
    ];

    [Theory]
    [MemberData(nameof(RegisterLoads))]
    public void RegisterAndHlLoadsPreserveStateAndBusOrder(int opcode, string destination, string source, int cycles)
    {
        var bus = new RecordingBus();
        var registers = new Z80Registers
        {
            B = 0x11, C = 0x22, D = 0x33, E = 0x44, H = 0x80, L = 0x20, A = 0x77, F = 0xD7
        };
        bus.Memory[0] = (byte)opcode;
        bus.Memory[0x8020] = 0x66;
        var initial = new Dictionary<string, byte>
        {
            ["B"] = 0x11, ["C"] = 0x22, ["D"] = 0x33, ["E"] = 0x44,
            ["H"] = 0x80, ["L"] = 0x20, ["(HL)"] = 0x66, ["A"] = 0x77
        };
        var expected = new Dictionary<string, byte>(initial);
        expected[destination] = initial[source];

        var cpu = new Z80Cpu(bus, registers);

        Assert.Equal((ulong)cycles, cpu.Step());
        Assert.Equal(new[] { expected["B"], expected["C"], expected["D"], expected["E"],
                expected["H"], expected["L"], expected["A"] },
            new[] { registers.B, registers.C, registers.D, registers.E, registers.H, registers.L, registers.A });
        Assert.Equal(expected["(HL)"], bus.Memory[0x8020]);
        Assert.Equal((byte)0xD7, registers.F);
        Assert.Equal((ushort)1, registers.PC);
        Assert.Equal((byte)1, registers.R);
        var events = new List<CycleEvent>
        {
            new(Z80BusCycleKind.OpcodeFetch, 0, 0, 3, 4, (byte)opcode)
        };
        if (source == "(HL)")
        {
            events.Add(new CycleEvent(Z80BusCycleKind.MemoryRead, 0x8020, 4, 7, 7, 0x66));
        }
        else if (destination == "(HL)")
        {
            events.Add(new CycleEvent(Z80BusCycleKind.MemoryWrite, 0x8020, 4, 7, 7, initial[source]));
        }

        Assert.Equal(events.ToArray(), bus.Events.ToArray());
    }

    [Theory]
    [InlineData(0x06, "B", 7)]
    [InlineData(0x0E, "C", 7)]
    [InlineData(0x16, "D", 7)]
    [InlineData(0x1E, "E", 7)]
    [InlineData(0x26, "H", 7)]
    [InlineData(0x2E, "L", 7)]
    [InlineData(0x36, "(HL)", 10)]
    [InlineData(0x3E, "A", 7)]
    public void ImmediateByteLoadsUseAnOperandReadAndPreserveFlags(int opcode, string destination, int cycles)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)opcode;
        bus.Memory[1] = 0xA5;
        var registers = new Z80Registers { HL = 0x8000, F = 0xFF, R = 0xFF };
        var cpu = new Z80Cpu(bus, registers);

        Assert.Equal((ulong)cycles, cpu.Step());
        byte actual = destination == "(HL)"
            ? bus.Memory[0x8000]
            : (byte)typeof(Z80Registers).GetProperty(destination)!.GetValue(registers)!;
        Assert.Equal((byte)0xA5, actual);
        Assert.Equal((byte)0xFF, registers.F);
        Assert.Equal((byte)0x80, registers.R);
        Assert.Equal((ushort)2, registers.PC);
        var events = new List<CycleEvent>
        {
            new(Z80BusCycleKind.OpcodeFetch, 0, 0, 3, 4, (byte)opcode),
            new(Z80BusCycleKind.MemoryRead, 1, 4, 7, 7, 0xA5)
        };
        if (destination == "(HL)")
        {
            events.Add(new CycleEvent(Z80BusCycleKind.MemoryWrite, 0x8000, 7, 10, 10, 0xA5));
        }

        Assert.Equal(events.ToArray(), bus.Events.ToArray());
    }

    [Theory]
    [InlineData(0x01, "BC")]
    [InlineData(0x11, "DE")]
    [InlineData(0x21, "HL")]
    [InlineData(0x31, "SP")]
    public void ImmediateWordLoadsReadLowByteThenHighByte(int opcode, string destination)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)opcode;
        bus.Memory[1] = 0x34;
        bus.Memory[2] = 0x12;
        var registers = new Z80Registers { F = 0xFF };
        var cpu = new Z80Cpu(bus, registers);

        Assert.Equal(10UL, cpu.Step());
        Assert.Equal((ushort)0x1234,
            (ushort)typeof(Z80Registers).GetProperty(destination)!.GetValue(registers)!);
        Assert.Equal((byte)0xFF, registers.F);
        Assert.Equal((byte)1, registers.R);
        Assert.Equal((ushort)3, registers.PC);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0, 0, 3, 4, (byte)opcode),
            new(Z80BusCycleKind.MemoryRead, 1, 4, 7, 7, 0x34),
            new(Z80BusCycleKind.MemoryRead, 2, 7, 10, 10, 0x12)
        }, bus.Events.ToArray());
    }

    [Theory]
    [InlineData(0x02, false, false)]
    [InlineData(0x12, true, false)]
    [InlineData(0x0A, false, true)]
    [InlineData(0x1A, true, true)]
    public void AccumulatorIndirectLoadsUseTheSelectedPair(int opcode, bool useDe, bool read)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)opcode;
        bus.Memory[0x8000] = 0x42;
        bus.Memory[0x9000] = 0x37;
        var registers = new Z80Registers { BC = 0x8000, DE = 0x9000, A = 0xA5, F = 0xFF };
        var cpu = new Z80Cpu(bus, registers);
        ushort address = useDe ? (ushort)0x9000 : (ushort)0x8000;
        byte source = useDe ? (byte)0x37 : (byte)0x42;

        Assert.Equal(7UL, cpu.Step());
        Assert.Equal(read ? source : (byte)0xA5, registers.A);
        Assert.Equal(read ? source : (byte)0xA5, bus.Memory[address]);
        Assert.Equal((byte)0xFF, registers.F);
        Assert.Equal((ushort)1, registers.PC);
        Assert.Equal((byte)1, registers.R);
        Assert.Equal((ushort)0x8000, registers.BC);
        Assert.Equal((ushort)0x9000, registers.DE);
        Assert.Equal(useDe ? (byte)0x42 : (byte)0x37, bus.Memory[useDe ? 0x8000 : 0x9000]);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0, 0, 3, 4, (byte)opcode),
            new(read ? Z80BusCycleKind.MemoryRead : Z80BusCycleKind.MemoryWrite,
                address, 4, 7, 7, read ? source : (byte)0xA5)
        }, bus.Events.ToArray());
    }

    [Theory]
    [InlineData(0x32, false)]
    [InlineData(0x3A, true)]
    public void AbsoluteAccumulatorLoadsReadTheirAddressBeforeTheTransfer(int opcode, bool read)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)opcode;
        bus.Memory[1] = 0x00;
        bus.Memory[2] = 0x80;
        bus.Memory[0x8000] = 0x42;
        var registers = new Z80Registers { A = 0xA5, F = 0xFF };
        var cpu = new Z80Cpu(bus, registers);

        Assert.Equal(13UL, cpu.Step());
        Assert.Equal(read ? (byte)0x42 : (byte)0xA5, registers.A);
        Assert.Equal(read ? (byte)0x42 : (byte)0xA5, bus.Memory[0x8000]);
        Assert.Equal((byte)0xFF, registers.F);
        Assert.Equal((ushort)3, registers.PC);
        Assert.Equal((byte)1, registers.R);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0, 0, 3, 4, (byte)opcode),
            new(Z80BusCycleKind.MemoryRead, 1, 4, 7, 7, 0x00),
            new(Z80BusCycleKind.MemoryRead, 2, 7, 10, 10, 0x80),
            new(read ? Z80BusCycleKind.MemoryRead : Z80BusCycleKind.MemoryWrite,
                0x8000, 10, 13, 13, read ? (byte)0x42 : (byte)0xA5)
        }, bus.Events.ToArray());
    }

    [Theory]
    [InlineData(0x22, false)]
    [InlineData(0x2A, true)]
    public void AbsoluteHlLoadsWrapTheSecondAddressAndPreserveByteOrder(int opcode, bool read)
    {
        var bus = new RecordingBus();
        bus.Memory[0x0100] = (byte)opcode;
        bus.Memory[0x0101] = 0xFF;
        bus.Memory[0x0102] = 0xFF;
        bus.Memory[0xFFFF] = 0x78;
        bus.Memory[0] = 0x56;
        var registers = new Z80Registers { PC = 0x0100, HL = 0x1234, F = 0xFF };
        var cpu = new Z80Cpu(bus, registers);

        Assert.Equal(16UL, cpu.Step());
        Assert.Equal(read ? (ushort)0x5678 : (ushort)0x1234, registers.HL);
        Assert.Equal(read ? (byte)0x78 : (byte)0x34, bus.Memory[0xFFFF]);
        Assert.Equal(read ? (byte)0x56 : (byte)0x12, bus.Memory[0]);
        Assert.Equal((byte)0xFF, registers.F);
        Assert.Equal((ushort)0x0103, registers.PC);
        Assert.Equal((byte)1, registers.R);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.OpcodeFetch, 0x0100, 0, 3, 4, (byte)opcode),
            new(Z80BusCycleKind.MemoryRead, 0x0101, 4, 7, 7, 0xFF),
            new(Z80BusCycleKind.MemoryRead, 0x0102, 7, 10, 10, 0xFF),
            new(read ? Z80BusCycleKind.MemoryRead : Z80BusCycleKind.MemoryWrite,
                0xFFFF, 10, 13, 13, read ? (byte)0x78 : (byte)0x34),
            new(read ? Z80BusCycleKind.MemoryRead : Z80BusCycleKind.MemoryWrite,
                0, 13, 16, 16, read ? (byte)0x56 : (byte)0x12)
        }, bus.Events.ToArray());
    }

    [Fact]
    public void OperandAndProgramCounterWrapWithoutExtraRefreshIncrement()
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFE] = 0x01;
        bus.Memory[0xFFFF] = 0x34;
        bus.Memory[0] = 0x12;
        var registers = new Z80Registers { PC = 0xFFFE, R = 0x7F };
        var cpu = new Z80Cpu(bus, registers);

        Assert.Equal(10UL, cpu.Step());
        Assert.Equal((ushort)0x1234, registers.BC);
        Assert.Equal((ushort)1, registers.PC);
        Assert.Equal((byte)0, registers.R);
        Assert.Equal(new ushort[] { 0xFFFE, 0xFFFF, 0 },
            bus.Events.Select(e => e.Address).ToArray());
    }

    [Fact]
    public void SixInstructionSyntheticProgramProducesTheDocumentedResult()
    {
        var bus = new RecordingBus();
        byte[] program = [0x21, 0x00, 0x40, 0x36, 0x2A, 0x7E, 0x32, 0x01, 0x40, 0x06, 0x03, 0x00];
        program.CopyTo(bus.Memory, 0);
        var cpu = new Z80Cpu(bus);
        ulong[] costs = new ulong[6];

        for (int i = 0; i < costs.Length; i++)
        {
            costs[i] = cpu.Step();
        }

        Assert.Equal(new ulong[] { 10, 10, 7, 13, 7, 4 }, costs);
        Assert.Equal(51UL, bus.TStates);
        Assert.Equal((ushort)12, cpu.Registers.PC);
        Assert.Equal((byte)0x2A, cpu.Registers.A);
        Assert.Equal((byte)3, cpu.Registers.B);
        Assert.Equal((ushort)0x4000, cpu.Registers.HL);
        Assert.Equal((byte)6, cpu.Registers.R);
        Assert.Equal((byte)0x2A, bus.Memory[0x4000]);
        Assert.Equal((byte)0x2A, bus.Memory[0x4001]);
    }
}
