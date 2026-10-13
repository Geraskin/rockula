using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80PortTests
{
    // Independent port and flag expectations; hardware sources/corrections are in 001k.
    [Theory]
    [InlineData(0xDB)]
    [InlineData(0xD3)]
    public void ImmediatePortsUseAllSixteenAddressBitsAndPreserveFlags(int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = (byte)opcode;
        bus.PortReading = port => (byte)((port / 256) ^ (port % 256));
        var cpu = new Z80Cpu(bus);
        for (int a = 0; a < 256; a++)
            for (int n = 0; n < 256; n++) Check(a, n, 0xFF);
        for (int flags = 0; flags < 256; flags++) Check(0x28, 0xFF, flags);

        void Check(int a, int n, int flags)
        {
            cpu.Reset();
            var r = cpu.Registers;
            r.A = (byte)a;
            r.F = (byte)flags;
            bus.Memory[1] = (byte)n;
            bus.Events.Clear();
            bus.PortWrites.Clear();
            ulong start = bus.TStates;
            ushort port = (ushort)(a * 256 + n);
            Assert.Equal(11UL, cpu.Step());
            Assert.Equal((byte)(opcode == 0xDB ? a ^ n : a), r.A);
            Assert.Equal((byte)flags, r.F);
            Assert.Equal(opcode == 0xDB ? unchecked((ushort)(port + 1)) : (ushort)(a * 256 + (n + 1) % 256), r.WZ);
            Assert.Equal((ushort)2, r.PC);
            Assert.Equal((byte)1, r.R);
            Assert.Equal(new CycleEvent(opcode == 0xDB ? Z80BusCycleKind.PortRead : Z80BusCycleKind.PortWrite,
                port, start + 7, start + 11, start + 11, (byte)(opcode == 0xDB ? a ^ n : a)), bus.Events[2]);
            if (opcode == 0xD3) Assert.Equal(new[] { (port, (byte)a) }, bus.PortWrites.ToArray());
        }
    }

    public static IEnumerable<object[]> RegisterPorts()
    {
        foreach (int opcode in new[] { 0x40, 0x48, 0x50, 0x58, 0x60, 0x68, 0x70, 0x78,
            0x41, 0x49, 0x51, 0x59, 0x61, 0x69, 0x71, 0x79 }) yield return [opcode];
    }

    [Theory]
    [MemberData(nameof(RegisterPorts))]
    public void EveryRegisterPortChecksAllValuesFlagsAndUntargetedRegisters(int opcode)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xED;
        bus.Memory[1] = (byte)opcode;
        var cpu = new Z80Cpu(bus);
        bool input = opcode % 2 == 0;
        int target = (opcode / 8) % 8;
        for (int value = 0; value < 256; value++)
            for (int flags = 0; flags < 256; flags++)
            {
                cpu.Reset();
                var r = cpu.Registers;
                r.BC = 0x28FF;
                r.DE = 0x1234;
                r.HL = 0x5678;
                r.A = 0xA5;
                r.F = (byte)flags;
                if (!input && target != 6) Set(r, target, (byte)value);
                ushort port = r.BC;
                byte[] before = [r.B, r.C, r.D, r.E, r.H, r.L, 0, r.A];
                bus.Ports[port] = (byte)value;
                bus.Events.Clear();
                bus.PortWrites.Clear();
                Assert.Equal(12UL, cpu.Step());
                int f = input ? (value & 0xA8) | (value == 0 ? 64 : 0) | Parity(value) | (flags % 2) : flags;
                Assert.Equal((byte)f, r.F);
                Assert.Equal(unchecked((ushort)(port + 1)), r.WZ);
                byte[] after = [r.B, r.C, r.D, r.E, r.H, r.L, 0, r.A];
                for (int code = 0; code < 8; code++)
                    Assert.Equal(input && code == target && target != 6 ? (byte)value : before[code], after[code]);
                Assert.Equal(port, bus.Events[2].Address);
                Assert.Equal(input ? Z80BusCycleKind.PortRead : Z80BusCycleKind.PortWrite, bus.Events[2].Kind);
                if (!input) Assert.Equal(new[] { (port, target == 6 ? (byte)0 : (byte)value) }, bus.PortWrites.ToArray());
            }
    }

    [Fact]
    public void PortWaitsSampleLiveDataWithoutTouchingMemoryOrAcknowledgement()
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFF] = 0xDB;
        bus.Memory[0] = 0xFF;
        bus.Memory[0x28FF] = 0xEE;
        bus.WaitStates = c => c.Kind == Z80BusCycleKind.PortRead ? 2 : 0;
        bus.Advancing = time => { if (time == 13) bus.Ports[0x28FF] = 0x42; };
        var r = new Z80Registers { PC = 0xFFFF, A = 0x28, R = 0xFF, F = 0xA5 };
        Assert.Equal(13UL, new Z80Cpu(bus, r).Step());
        Assert.Equal((byte)0x42, r.A);
        Assert.Equal((ushort)1, r.PC);
        Assert.Equal((byte)0x80, r.R);
        Assert.Equal((ushort)0x2900, r.WZ);
        Assert.Equal((byte)0xEE, bus.Memory[0x28FF]);
        Assert.Equal(0, bus.InterruptAcknowledgements);
        Assert.Equal(new CycleEvent(Z80BusCycleKind.PortRead, 0x28FF, 7, 13, 13, 0x42), bus.Events[2]);
    }

    [Fact]
    public void UnconnectedPortHooksReturnFfAndIgnoreWrites()
    {
        var bus = new NoMemoryBus();
        Assert.Equal((byte)0xFF, bus.Execute(new Z80BusCycle(Z80BusCycleKind.PortRead, 0x1234, 4, 4)));
        Assert.Equal((byte)0xA5, bus.Execute(new Z80BusCycle(Z80BusCycleKind.PortWrite, 0x1234, 4, 4, 0xA5)));
        Assert.Equal(8UL, bus.TStates);
    }

    [Theory]
    [InlineData(0xDB)]
    [InlineData(0xD3)]
    [InlineData(0x40)]
    [InlineData(0x41)]
    public void FailedPortTransferDoesNotCommitFlagsDestinationOrWz(int opcode)
    {
        var bus = new RecordingBus();
        bool immediate = opcode is 0xDB or 0xD3;
        bus.Memory[0] = immediate ? (byte)opcode : (byte)0xED;
        bus.Memory[1] = immediate ? (byte)0xFF : (byte)opcode;
        bus.WaitStates = c => c.Kind is Z80BusCycleKind.PortRead or Z80BusCycleKind.PortWrite ? throw new IOException("port fault") : 0;
        var r = new Z80Registers { A = 0xA5, BC = 0x1234, F = 0xFF, WZ = 0x9876 };
        var cpu = new Z80Cpu(bus, r);
        Assert.Throws<IOException>(() => cpu.Step());
        Assert.True(cpu.IsFaulted);
        Assert.Equal((byte)0xA5, r.A);
        Assert.Equal((ushort)0x1234, r.BC);
        Assert.Equal((byte)0xFF, r.F);
        Assert.Equal((ushort)0x9876, r.WZ);
        Assert.Empty(bus.PortWrites);
    }

    [Fact]
    public void OutputHookFailureRetainsDeviceEffectAndAlreadyDecrementedB()
    {
        var bus = new FailingOutputBus();
        var r = new Z80Registers { BC = 0x02FF, HL = 0x4000, F = 0xFF, WZ = 0x9876 };
        var cpu = new Z80Cpu(bus, r);
        Assert.Throws<IOException>(() => cpu.Step());
        Assert.True(cpu.IsFaulted);
        Assert.Equal((ushort)0x01FF, r.BC);
        Assert.Equal((ushort)0x4000, r.HL);
        Assert.Equal((byte)0xFF, r.F);
        Assert.Equal((ushort)0x9876, r.WZ);
        Assert.Equal(((ushort)0x01FF, (byte)0xA5), bus.Written);
        Assert.Equal(16UL, bus.TStates);
    }

    private sealed class FailingOutputBus : Z80Bus
    {
        public (ushort Address, byte Value) Written { get; private set; }
        protected override byte ReadMemory(ushort address) => address switch { 0 => 0xED, 1 => 0xA3, 0x4000 => 0xA5, _ => 0 };
        protected override void WriteMemory(ushort address, byte value) => throw new InvalidOperationException("Unexpected memory write");
        protected override void WritePort(ushort address, byte value)
        {
            Written = (address, value);
            throw new IOException("Device fails after retaining the write");
        }
    }

    internal static int Parity(int value)
    {
        int ones = 0;
        for (int bit = 0; bit < 8; bit++) ones += (value >> bit) & 1;
        return ones % 2 == 0 ? 4 : 0;
    }

    private static void Set(Z80Registers r, int code, byte value)
    {
        switch (code)
        {
            case 0: r.B = value; break;
            case 1: r.C = value; break;
            case 2: r.D = value; break;
            case 3: r.E = value; break;
            case 4: r.H = value; break;
            case 5: r.L = value; break;
            case 7: r.A = value; break;
        }
    }

    private sealed class NoMemoryBus : Z80Bus
    {
        protected override byte ReadMemory(ushort address) => throw new InvalidOperationException("Memory read during port access");
        protected override void WriteMemory(ushort address, byte value) => throw new InvalidOperationException("Memory write during port access");
    }
}
