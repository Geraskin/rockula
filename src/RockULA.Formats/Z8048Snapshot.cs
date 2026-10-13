using RockULA.Core.Machines;

namespace RockULA.Formats;

public sealed class Z8048Snapshot : ISpectrum48Snapshot
{
    public const int MaximumFileSize = 200000;
    public Spectrum48CpuState CpuState => default;
    public byte BorderColor => 0;
    public ReadOnlySpan<byte> Ram => [];
    public int Version => 0;
    public static Z8048Snapshot Parse(ReadOnlySpan<byte> bytes) => throw new NotImplementedException();
}
