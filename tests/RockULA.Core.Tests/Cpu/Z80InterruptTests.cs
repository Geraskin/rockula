using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

public sealed class Z80InterruptTests
{
    // Zilog UM008011-0816, pp.6, 12–14, 17–20; Weissflog NMI/IM1 timing trace.
    // Offsets and Internal addresses are logical conventions from ADR 0007.
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NmiPreservesIff2AndAllFlagsPushesUnchangedPcAndWrapsStack(bool iff1, bool iff2)
    {
        var bus = new RecordingBus();
        bus.Memory[0xABCD] = 0xD3; // Ignored fetch must not decode an unsupported byte.
        var registers = new Z80Registers();
        var cpu = new Z80Cpu(bus, registers);
        for (int flags = 0; flags < 256; flags++)
        {
            cpu.Reset();
            registers.PC = 0xABCD;
            registers.SP = 0;
            registers.R = 0xFF;
            registers.AF = (ushort)(0xA500 | flags);
            registers.BC = 0x1234;
            registers.Iff1 = iff1;
            registers.Iff2 = iff2;
            registers.InterruptMode = 2; // NMI is independent of maskable mode.
            cpu.SetNmiLine(true);
            ulong start = bus.TStates;
            bus.Events.Clear();

            Assert.Equal(11UL, cpu.Step());
            Assert.False(registers.Iff1);
            Assert.Equal(iff2, registers.Iff2);
            Assert.Equal((ushort)0x0066, registers.PC);
            Assert.Equal((ushort)0xFFFE, registers.SP);
            Assert.Equal((byte)0x80, registers.R);
            Assert.Equal((ushort)(0xA500 | flags), registers.AF);
            Assert.Equal((ushort)0x1234, registers.BC);
            Assert.Equal((byte)2, registers.InterruptMode);
            Assert.False(cpu.IsNmiPending);
            Assert.True(cpu.IsNmiLineAsserted);
            Assert.False(cpu.IsHalted);
            Assert.Equal(0, bus.InterruptAcknowledgements);
            Assert.Equal(new CycleEvent[]
            {
                new(Z80BusCycleKind.OpcodeFetch, 0xABCD, start, start + 3, start + 4, 0xD3),
                new(Z80BusCycleKind.Internal, 0xABCD, start + 4, start + 4, start + 5, 0),
                new(Z80BusCycleKind.MemoryWrite, 0xFFFF, start + 5, start + 8, start + 8, 0xAB),
                new(Z80BusCycleKind.MemoryWrite, 0xFFFE, start + 8, start + 11, start + 11, 0xCD)
            }, bus.Events.ToArray());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Im1ClearsBothIffsIgnoresAckDataAndPreservesAllFlags(bool iff2)
    {
        var bus = new RecordingBus { InterruptAcknowledging = _ => 0xD3 };
        bus.Memory[0xFFFF] = 0xED; // Must acknowledge the device, not read this memory.
        var registers = new Z80Registers();
        var cpu = new Z80Cpu(bus, registers);
        for (int flags = 0; flags < 256; flags++)
        {
            cpu.Reset();
            registers.PC = 0xFFFF;
            registers.SP = 1;
            registers.R = 0x7F;
            registers.AF = (ushort)(0x5A00 | flags);
            registers.Iff1 = true;
            registers.Iff2 = iff2;
            registers.InterruptMode = 1;
            cpu.SetInterruptLine(true);
            ulong start = bus.TStates;
            bus.Events.Clear();

            Assert.Equal(13UL, cpu.Step());
            Assert.False(registers.Iff1);
            Assert.False(registers.Iff2);
            Assert.True(cpu.IsInterruptLineAsserted);
            Assert.Equal((ushort)0x0038, registers.PC);
            Assert.Equal((ushort)0xFFFF, registers.SP);
            Assert.Equal((byte)0, registers.R);
            Assert.Equal((ushort)(0x5A00 | flags), registers.AF);
            Assert.Equal(flags + 1, bus.InterruptAcknowledgements);
            Assert.Equal(new CycleEvent[]
            {
                new(Z80BusCycleKind.InterruptAcknowledge, 0xFFFF, start, start + 5, start + 6, 0xD3),
                new(Z80BusCycleKind.Internal, 0xFFFF, start + 6, start + 6, start + 7, 0),
                new(Z80BusCycleKind.MemoryWrite, 0, start + 7, start + 10, start + 10, 0xFF),
                new(Z80BusCycleKind.MemoryWrite, 0xFFFF, start + 10, start + 13, start + 13, 0xFF)
            }, bus.Events.ToArray());
        }
    }

    [Fact]
    public void NmiEdgeSurvivesDeassertionCoalescesAndDoesNotRetriggerWhileHeld()
    {
        var bus = new RecordingBus();
        var cpu = new Z80Cpu(bus);
        cpu.SetNmiLine(true);
        cpu.SetNmiLine(false);
        cpu.SetNmiLine(true);
        Assert.True(cpu.IsNmiPending);
        Assert.Equal(11UL, cpu.Step());
        Assert.Equal((ushort)0xFFFD, cpu.Registers.SP);
        cpu.SetNmiLine(true);
        Assert.False(cpu.IsNmiPending);
        Assert.Equal(4UL, cpu.Step());
        Assert.Equal((ushort)0x0067, cpu.Registers.PC);
        cpu.SetNmiLine(false);
        cpu.SetNmiLine(true);
        cpu.SetNmiLine(false);
        Assert.True(cpu.IsNmiPending);
        Assert.False(cpu.IsNmiLineAsserted);
        Assert.Equal(11UL, cpu.Step());
        Assert.Equal((ushort)0xFFFB, cpu.Registers.SP);
        Assert.Equal((byte)0x67, bus.Memory[0xFFFB]);
        Assert.False(cpu.IsNmiPending);
    }

    [Fact]
    public void NmiHasPriorityOverIntAndNestedNmiPreservesSavedIff2()
    {
        var bus = new RecordingBus();
        var registers = new Z80Registers { Iff1 = true, Iff2 = true, InterruptMode = 0 };
        var cpu = new Z80Cpu(bus, registers);
        cpu.SetInterruptLine(true); // Unsupported mode cannot preempt pending NMI.
        cpu.SetNmiLine(true);
        Assert.Equal(11UL, cpu.Step());
        Assert.Equal(0, bus.InterruptAcknowledgements);
        Assert.True(registers.Iff2);
        Assert.False(registers.Iff1);
        cpu.SetNmiLine(false);
        cpu.SetNmiLine(true);
        Assert.Equal(11UL, cpu.Step());
        Assert.True(registers.Iff2);
        Assert.False(registers.Iff1);
        Assert.True(cpu.IsInterruptLineAsserted);
        Assert.Equal((ushort)0x0066, registers.PC);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptedInterruptLeavesHaltAndPushesTheInstructionAfterHalt(bool nmi)
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFF] = 0x76;
        var registers = new Z80Registers { PC = 0xFFFF, Iff1 = true, Iff2 = true, InterruptMode = 1 };
        var cpu = new Z80Cpu(bus, registers);
        Assert.Equal(4UL, cpu.Step());
        Assert.True(cpu.IsHalted);
        if (nmi) cpu.SetNmiLine(true);
        else cpu.SetInterruptLine(true);

        Assert.Equal(nmi ? 11UL : 13UL, cpu.Step());
        Assert.False(cpu.IsHalted);
        Assert.Equal(nmi ? (ushort)0x0066 : (ushort)0x0038, registers.PC);
        Assert.Equal((byte)0, bus.Memory[0xFFFD]);
        Assert.Equal((byte)0, bus.Memory[0xFFFE]);
        Assert.Equal((byte)2, registers.R);
        Assert.Equal(4UL, cpu.Step());
        Assert.Equal(nmi ? (ushort)0x0067 : (ushort)0x0039, registers.PC);
    }

    [Theory]
    [InlineData(0x00, 4, true)]
    [InlineData(0x01, 10, true)]
    [InlineData(0x76, 4, true)]
    [InlineData(0xF3, 4, false)]
    public void HeldIntWaitsForEiFollowingInstructionAndDiPreventsAcceptance(int opcode, int cycles, bool accepted)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xFB;
        bus.Memory[1] = (byte)opcode;
        var registers = new Z80Registers { InterruptMode = 1 };
        var cpu = new Z80Cpu(bus, registers);
        cpu.SetInterruptLine(true);
        Assert.Equal(4UL, cpu.Step());
        Assert.True(cpu.IsEiDelayActive);
        Assert.Equal((ulong)cycles, cpu.Step());
        Assert.Equal(0, bus.InterruptAcknowledgements);
        Assert.False(cpu.IsEiDelayActive);
        Assert.Equal(accepted ? 13UL : 4UL, cpu.Step());
        Assert.Equal(accepted ? 1 : 0, bus.InterruptAcknowledgements);
        if (accepted)
        {
            Assert.False(cpu.IsHalted);
            Assert.Equal((ushort)0x0038, registers.PC);
            Assert.Equal(opcode == 0x01 ? (byte)4 : (byte)2, bus.Memory[0xFFFD]);
        }
    }

    [Fact]
    public void RepeatedEiInhibitsHeldIntAndHandlerReenableAllowsAnotherAcceptance()
    {
        var bus = new RecordingBus();
        byte[] program = [0xFB, 0xFB, 0x00];
        program.CopyTo(bus.Memory, 0);
        bus.Memory[0x38] = 0xFB;
        bus.Memory[0x39] = 0xC9; // EI; RET resumes the synthetic main program.
        var registers = new Z80Registers { InterruptMode = 1 };
        var cpu = new Z80Cpu(bus, registers);
        cpu.SetInterruptLine(true);
        Assert.Equal(4UL, cpu.Step());
        Assert.Equal(4UL, cpu.Step());
        Assert.True(cpu.IsEiDelayActive);
        Assert.Equal(4UL, cpu.Step());
        Assert.Equal(13UL, cpu.Step());
        Assert.Equal((byte)3, bus.Memory[0xFFFD]);
        Assert.Equal(4UL, cpu.Step());
        Assert.Equal(10UL, cpu.Step());
        Assert.Equal((ushort)3, registers.PC);
        Assert.Equal((ushort)0xFFFF, registers.SP);
        Assert.Equal(13UL, cpu.Step());
        Assert.Equal(2, bus.InterruptAcknowledgements);
    }

    [Fact]
    public void IntPulseBeforeBoundaryIsNotLatchedAndDisabledIntDoesNotWakeHalt()
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0x76;
        var registers = new Z80Registers { InterruptMode = 2 };
        var cpu = new Z80Cpu(bus, registers);
        cpu.SetInterruptLine(true);
        Assert.Equal(4UL, cpu.Step());
        Assert.Equal(4UL, cpu.Step());
        Assert.True(cpu.IsHalted);
        cpu.SetInterruptLine(false);
        registers.Iff1 = true;
        Assert.Equal(4UL, cpu.Step());
        Assert.True(cpu.IsHalted);
        Assert.Equal(0, bus.InterruptAcknowledgements);
        cpu.SetNmiLine(true);
        Assert.Equal(11UL, cpu.Step());
        Assert.False(cpu.IsHalted);
    }

    [Fact]
    public void NmiBypassesEiDelayWithoutRetiringItAsAnInstruction()
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0xFB;
        var cpu = new Z80Cpu(bus);
        cpu.Step();
        cpu.SetNmiLine(true);
        Assert.Equal(11UL, cpu.Step());
        Assert.True(cpu.IsEiDelayActive);
        Assert.True(cpu.Registers.Iff2);
        Assert.False(cpu.Registers.Iff1);
        Assert.Equal((byte)1, bus.Memory[0xFFFD]);
        Assert.Equal(4UL, cpu.Step());
        Assert.False(cpu.IsEiDelayActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequestsRaisedDuringInstructionAreHandledAtTheNextStep(bool nmi)
    {
        var bus = new RecordingBus();
        bus.Memory[0] = 0x01; // LD BC,1234 must finish before service.
        bus.Memory[1] = 0x34;
        bus.Memory[2] = 0x12;
        var registers = new Z80Registers { Iff1 = true, Iff2 = true, InterruptMode = 1 };
        var cpu = new Z80Cpu(bus, registers);
        bus.Advancing = time =>
        {
            if (time != 3) return;
            if (nmi)
            {
                cpu.SetNmiLine(true);
                cpu.SetNmiLine(false); // Short pulse is retained.
            }
            else cpu.SetInterruptLine(true);
        };
        Assert.Equal(10UL, cpu.Step());
        Assert.Equal((ushort)0x1234, registers.BC);
        Assert.Equal((ushort)3, registers.PC);
        Assert.Equal(nmi ? 11UL : 13UL, cpu.Step());
        Assert.Equal((byte)3, bus.Memory[0xFFFD]);
    }

    [Fact]
    public void NewNmiEdgeDuringResponseRemainsPendingForNestedService()
    {
        var bus = new RecordingBus();
        var cpu = new Z80Cpu(bus);
        cpu.SetNmiLine(true);
        bus.Advancing = time =>
        {
            if (time != 3) return;
            cpu.SetNmiLine(false);
            cpu.SetNmiLine(true);
        };
        Assert.Equal(11UL, cpu.Step());
        Assert.True(cpu.IsNmiPending);
        Assert.Equal(11UL, cpu.Step());
        Assert.False(cpu.IsNmiPending);
        Assert.Equal((byte)0x66, bus.Memory[0xFFFB]);
    }

    [Fact]
    public void AcknowledgeAndStackWaitsShiftSamplingAndPreserveTransactionOrder()
    {
        var bus = new RecordingBus
        {
            WaitStates = cycle => cycle.Kind == Z80BusCycleKind.InterruptAcknowledge ? 2
                : cycle.Kind == Z80BusCycleKind.MemoryWrite ? 1 : 0
        };
        byte vector = 0;
        bus.Advancing = time =>
        {
            if (time == 7) vector = 0xFD;
        };
        bus.InterruptAcknowledging = address =>
        {
            Assert.Equal((ushort)0x1234, address);
            Assert.Equal(7UL, bus.TStates);
            return vector;
        };
        var registers = new Z80Registers { PC = 0x1234, SP = 0x8000, Iff1 = true, InterruptMode = 1 };
        var cpu = new Z80Cpu(bus, registers);
        cpu.SetInterruptLine(true);
        Assert.Equal(17UL, cpu.Step());
        Assert.Equal(1, bus.InterruptAcknowledgements);
        Assert.Equal(new CycleEvent[]
        {
            new(Z80BusCycleKind.InterruptAcknowledge, 0x1234, 0, 7, 8, 0xFD),
            new(Z80BusCycleKind.Internal, 0x1234, 8, 8, 9, 0),
            new(Z80BusCycleKind.MemoryWrite, 0x7FFF, 9, 13, 13, 0x12),
            new(Z80BusCycleKind.MemoryWrite, 0x7FFE, 13, 17, 17, 0x34)
        }, bus.Events.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void EligibleUnsupportedModeFaultsBeforeBusOrAcceptanceEffects(int mode)
    {
        var bus = new RecordingBus();
        var registers = new Z80Registers { PC = 0x1234, SP = 0x8000, Iff1 = true, Iff2 = true, InterruptMode = (byte)mode };
        var cpu = new Z80Cpu(bus, registers);
        cpu.SetInterruptLine(true);
        var error = Assert.Throws<NotSupportedException>(() => cpu.Step());
        Assert.Contains($"IM {mode}", error.Message);
        Assert.True(cpu.IsFaulted);
        Assert.True(registers.Iff1);
        Assert.True(registers.Iff2);
        Assert.Equal((ushort)0x1234, registers.PC);
        Assert.Equal((ushort)0x8000, registers.SP);
        Assert.Equal((byte)0, registers.R);
        Assert.Equal(0UL, bus.TStates);
        Assert.Empty(bus.Events);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BusFailureAfterAcceptanceKeepsControlEffectsAndRequiresReset(bool nmi)
    {
        var bus = new RecordingBus { WaitStates = _ => throw new InvalidOperationException("test failure") };
        var registers = new Z80Registers { PC = 0x1234, Iff1 = true, Iff2 = true, InterruptMode = 1 };
        var cpu = new Z80Cpu(bus, registers);
        if (nmi) cpu.SetNmiLine(true);
        else cpu.SetInterruptLine(true);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
        Assert.True(cpu.IsFaulted);
        Assert.False(registers.Iff1);
        Assert.Equal(nmi, registers.Iff2);
        Assert.False(cpu.IsNmiPending);
        Assert.Equal((ushort)0x1234, registers.PC);
        Assert.Equal((byte)0, registers.R);
        Assert.Equal(0UL, bus.TStates);
        Assert.Equal(0, bus.InterruptAcknowledgements);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LowStackWriteFailureKeepsHighWriteAndAdvancedStack(bool nmi)
    {
        var bus = new RecordingBus
        {
            WaitStates = cycle => cycle.Kind == Z80BusCycleKind.MemoryWrite && cycle.Address == 0x7FFE
                ? throw new InvalidOperationException("low write failure") : 0
        };
        var registers = new Z80Registers { PC = 0x1234, SP = 0x8000, Iff1 = true, Iff2 = true, InterruptMode = 1 };
        var cpu = new Z80Cpu(bus, registers);
        if (nmi) cpu.SetNmiLine(true);
        else cpu.SetInterruptLine(true);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
        Assert.True(cpu.IsFaulted);
        Assert.Equal((ushort)0x1234, registers.PC);
        Assert.Equal((ushort)0x7FFE, registers.SP);
        Assert.Equal((byte)0x12, bus.Memory[0x7FFF]);
        Assert.Equal((byte)0, bus.Memory[0x7FFE]);
        Assert.Equal((byte)1, registers.R);
        Assert.Equal(nmi ? 8UL : 10UL, bus.TStates);
        Assert.Equal(3, bus.Events.Count);
    }

    [Fact]
    public void AcknowledgeHookFailureKeepsSampleTimeAndDisablesIffs()
    {
        var bus = new RecordingBus { InterruptAcknowledging = _ => throw new InvalidOperationException("ack failure") };
        var registers = new Z80Registers { Iff1 = true, Iff2 = true, InterruptMode = 1 };
        var cpu = new Z80Cpu(bus, registers);
        cpu.SetInterruptLine(true);
        Assert.Throws<InvalidOperationException>(() => cpu.Step());
        Assert.Equal(5UL, bus.TStates);
        Assert.Equal(1, bus.InterruptAcknowledgements);
        Assert.Equal((byte)0, registers.R);
        Assert.False(registers.Iff1);
        Assert.False(registers.Iff2);
        Assert.True(cpu.IsFaulted);
        Assert.Empty(bus.Events);
    }

    [Fact]
    public void ResetClearsSignalsAndPendingEdgeWithoutRewindingBus()
    {
        var bus = new RecordingBus();
        var cpu = new Z80Cpu(bus);
        Assert.False(cpu.IsInterruptLineAsserted);
        Assert.False(cpu.IsNmiLineAsserted);
        Assert.False(cpu.IsNmiPending);
        cpu.Step();
        cpu.SetInterruptLine(true);
        cpu.SetNmiLine(true);
        Assert.True(cpu.IsNmiPending);
        cpu.Reset();
        Assert.False(cpu.IsInterruptLineAsserted);
        Assert.False(cpu.IsNmiLineAsserted);
        Assert.False(cpu.IsNmiPending);
        Assert.Equal(4UL, bus.TStates);
        Assert.Equal(4UL, cpu.Step());
    }
}
