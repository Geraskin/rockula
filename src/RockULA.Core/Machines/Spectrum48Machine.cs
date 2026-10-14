using RockULA.Core.Cpu;

namespace RockULA.Core.Machines;

/// <summary>Single-owner functional 48K machine; one bounded run to the next frame.</summary>
public sealed class Spectrum48Machine
{
    public const ulong FrameTStates = 69888;
    private readonly RomImage _rom;
    public Spectrum48Machine(RomImage rom)
    {
        ArgumentNullException.ThrowIfNull(rom);
        _rom = rom;
        Bus = new Spectrum48Bus(rom);
        Cpu = new Z80Cpu(Bus);
        Wire(Bus, Cpu);
    }
    public Spectrum48Bus Bus { get; private set; }
    public Z80Cpu Cpu { get; private set; }
    public ulong FrameNumber => Bus.TStates / FrameTStates;
    public void LoadSnapshot(Spectrum48CpuState state, ReadOnlySpan<byte> ram, byte border)
    {
        if (ram.Length != 49152 || border > 7 || state.InterruptMode > 2)
            throw new ArgumentException("Invalid 48K snapshot state.");
        var candidateBus = new Spectrum48Bus(_rom);
        candidateBus.InstallRam(ram, border);
        var candidateCpu = new Z80Cpu(candidateBus, state.CreateRegisters());
        Wire(candidateBus, candidateCpu);
        Bus = candidateBus;
        Cpu = candidateCpu;
    }
    private static void Wire(Spectrum48Bus bus, Z80Cpu cpu)
    {
        bus.InterruptLevel = cpu.SetInterruptLine;
        cpu.SetInterruptLine(bus.TStates % FrameTStates < 32);
    }
    public int RunFrame()
    {
        ulong target = checked((FrameNumber + 1) * FrameTStates);
        int steps = 0;
        while (Bus.TStates < target)
        {
            if (++steps > 100000) throw new InvalidOperationException("Frame instruction budget exceeded.");
            Cpu.Step();
        }
        return steps;
    }
}
