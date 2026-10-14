using RockULA.Core.Machines;

namespace RockULA.Formats;

public interface ISpectrum48Snapshot
{
    Spectrum48CpuState CpuState { get; }
    byte BorderColor { get; }
    ReadOnlySpan<byte> Ram { get; }
}
