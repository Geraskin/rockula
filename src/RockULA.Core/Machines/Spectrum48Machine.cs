using RockULA.Core.Cpu;

namespace RockULA.Core.Machines;

public sealed class Spectrum48Machine
{
    public const ulong FrameTStates = 69888;
    public Spectrum48Machine(RomImage rom) => throw new NotImplementedException();
    public Spectrum48Bus Bus => throw new NotImplementedException();
    public Z80Cpu Cpu => throw new NotImplementedException();
    public ulong FrameNumber => Bus.TStates / FrameTStates;
    public void LoadSnapshot(Spectrum48CpuState state, ReadOnlySpan<byte> ram, byte border) => throw new NotImplementedException();
    public int RunFrame() => throw new NotImplementedException();
}
