using RockULA.Core.Machines;

namespace RockULA.Formats;

public sealed class Sna48Snapshot
{
    public const int FileSize = 49179;
    public Spectrum48CpuState CpuState => throw new NotImplementedException();
    public byte BorderColor => throw new NotImplementedException();
    public ReadOnlySpan<byte> Ram => throw new NotImplementedException();
    public static Sna48Snapshot Parse(ReadOnlySpan<byte> bytes) => throw new NotImplementedException();
}
