using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80CompletionTests
{
    [Theory]
    [InlineData(0x37, false)]
    [InlineData(0x37, true)]
    [InlineData(0x3F, false)]
    [InlineData(0x3F, true)]
    public void CarryInstructionsAllAfAndPriorFlagHistories(int opcode, bool priorGenerator)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)opcode;
        var cpu = new Z80Cpu(bus);
        for (int a = 0; a < 256; a++)
            for (int f = 0; f < 256; f++)
            {
                cpu.Reset();
                var r = cpu.Registers;
                r.A = (byte)a;
                r.F = (byte)f;
                r.Q = priorGenerator ? (byte)f : (byte)0;
                r.WZ = 0x1234;
                bus.Events.Clear();
                int q = priorGenerator ? f : 0;
                int result = (f & 0xC4) | (opcode == 0x37 ? 1 : f % 2 == 0 ? 1 : 16);
                int xy = ((q ^ result) | a) & 0x28;
                int flags = result | xy;
                Assert.Equal(4UL, cpu.Step());
                Assert.Equal((byte)flags, r.F);
                Assert.Equal((byte)flags, r.Q);
                Assert.Equal((byte)a, r.A);
                Assert.Equal((ushort)0x1234, r.WZ);
            }
    }

    [Theory]
    [InlineData(0x37, 0x00, 0xFF, 0xC5)] // Fuse Z80 corpus test 37_1 (SCF)
    [InlineData(0x3F, 0x00, 0x5B, 0x50)] // Fuse Z80 corpus test 3f (CCF)
    public void CarryInstructionsMatchIndependentCorpusValues(int opcode, byte a, byte f, byte expected)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)opcode;
        var cpu = new Z80Cpu(bus);
        var r = cpu.Registers;
        r.A = a;
        r.F = f;
        r.Q = 0;
        cpu.Step();
        Assert.Equal(expected, r.F);
    }

    [Theory]
    [InlineData(0xB7, true)]
    [InlineData(0x00, false)]
    [InlineData(0x08, false)]
    [InlineData(0xF1, false)]
    public void IdenticalFlagValuesStillDistinguishGenerators(int opcode, bool generator)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)opcode;
        bus.Memory[0x8000] = 0x44;
        var r = new Z80Registers { AF = 0x0044, AlternateAF = 0x0044, Q = 0x44, SP = 0x8000 };
        new Z80Cpu(bus, r).Step();
        Assert.Equal((byte)0x44, r.F);
        Assert.Equal(generator ? (byte)0x44 : (byte)0, r.Q);
    }

    [Theory]
    [InlineData(0xCB, 0x80, false)]
    [InlineData(0xCB, 0x40, true)]
    [InlineData(0xDD, 0x80, false)]
    [InlineData(0xFD, 0x40, true)]
    public void BitGroupsRetireHistoryWithWholePrefix(int prefix, int payload, bool generator)
    {
        var bus = new RecordingBus();
        byte[] code = prefix == 0xCB ? [(byte)prefix, (byte)payload] : [(byte)prefix, 0xCB, 0, (byte)payload];
        code.CopyTo(bus.Memory, 0);
        var r = new Z80Registers { F = 0xA5, Q = 0xA5, IX = 0x4000, IY = 0x4000 };
        var cpu = new Z80Cpu(bus, r);
        cpu.Step();
        Assert.Equal(generator ? r.F : (byte)0, r.Q);
        cpu.Reset();
        Assert.Equal((byte)0, r.Q);
    }

    [Theory]
    [InlineData(0x4C)]
    [InlineData(0x54)]
    [InlineData(0x5C)]
    [InlineData(0x64)]
    [InlineData(0x6C)]
    [InlineData(0x74)]
    [InlineData(0x7C)]
    public void NegAliasesHaveIndependentByteExpectations(int payload)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xED;
        bus.Memory[1] = (byte)payload;
        var cpu = new Z80Cpu(bus);
        for (int a = 0; a < 256; a++)
        {
            cpu.Reset();
            cpu.Registers.A = (byte)a;
            int result = (256 - a) % 256;
            int flags = (result & 0xA8) | (a == 0 ? 64 : 1) | (a % 16 != 0 ? 16 : 0) | (a == 128 ? 4 : 0) | 2;
            Assert.Equal(8UL, cpu.Step());
            Assert.Equal((byte)result, cpu.Registers.A);
            Assert.Equal((byte)flags, cpu.Registers.F);
        }
    }

    [Theory]
    [InlineData(0x4E, 0)]
    [InlineData(0x66, 0)]
    [InlineData(0x6E, 0)]
    [InlineData(0x76, 1)]
    [InlineData(0x7E, 2)]
    public void ModeAliasesAndReturnAliases(int payload, int mode)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xED;
        bus.Memory[1] = (byte)payload;
        var r = new Z80Registers { F = 0xA5, Q = 0xA5 };
        Assert.Equal(8UL, new Z80Cpu(bus, r).Step());
        Assert.Equal((byte)mode, r.InterruptMode);
        Assert.Equal((byte)0xA5, r.F);
        Assert.Equal((byte)0, r.Q);
    }

    [Theory]
    [InlineData(0x55)]
    [InlineData(0x5D)]
    [InlineData(0x65)]
    [InlineData(0x6D)]
    [InlineData(0x75)]
    [InlineData(0x7D)]
    public void ReturnAliasesDoNotEmitRetiNotification(int payload)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xED;
        bus.Memory[1] = (byte)payload;
        bus.Memory[0x8000] = 0x34;
        bus.Memory[0x8001] = 0x12;
        var r = new Z80Registers { SP = 0x8000, Iff2 = true, F = 0xA5 };
        Assert.Equal(14UL, new Z80Cpu(bus, r).Step());
        Assert.Equal((ushort)0x1234, r.PC);
        Assert.True(r.Iff1);
        Assert.Empty(bus.RetiNotifications);
    }

    [Fact]
    public void UndefinedEdBytesAreTwoFetchNopsWithWrap()
    {
        var actual = new HashSet<int>();
        for (int row = 0x40; row < 0x80; row += 8)
            for (int low = 0; low < 7; low++) actual.Add(row + low);
        foreach (int value in new[] { 0x47, 0x4F, 0x57, 0x5F, 0x67, 0x6F }) actual.Add(value);
        foreach (int value in new[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA8, 0xA9, 0xAA, 0xAB, 0xB0, 0xB1, 0xB2, 0xB3, 0xB8, 0xB9, 0xBA, 0xBB }) actual.Add(value);
        Assert.Equal(78, actual.Count);
        for (int payload = 0; payload < 256; payload++)
        {
            if (actual.Contains(payload)) continue;
            var bus = new RecordingBus();
            bus.Memory[0xFFFF] = 0xED;
            bus.Memory[0] = (byte)payload;
            var r = new Z80Registers { PC = 0xFFFF, AF = 0x1234, WZ = 0x5678, Q = 0x34, R = 0xFF };
            Assert.Equal(8UL, new Z80Cpu(bus, r).Step());
            Assert.Equal((ushort)1, r.PC);
            Assert.Equal((ushort)0x1234, r.AF);
            Assert.Equal((ushort)0x5678, r.WZ);
            Assert.Equal((byte)0x81, r.R);
            Assert.Equal((byte)0, r.Q);
        }
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
    public void Im0AcknowledgedRstPushesUnchangedPc(int data, int target)
    {
        var bus = new RecordingBus { InterruptAcknowledging = _ => (byte)data };
        var r = new Z80Registers { PC = 0x1234, SP = 0x8000, Iff1 = true, Iff2 = true, Q = 0x44 };
        var cpu = new Z80Cpu(bus, r);
        cpu.SetInterruptLine(true);
        Assert.Equal(13UL, cpu.Step());
        Assert.Equal((ushort)target, r.PC);
        Assert.Equal((byte)0x34, bus.Memory[0x7FFE]);
        Assert.Equal((byte)0x12, bus.Memory[0x7FFF]);
        Assert.Equal((byte)0, r.Q);
    }
}
