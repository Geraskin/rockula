namespace RockULA.Formats;

public static class SpectrumSnapshotReader
{
    public const int MaximumFileSize = Z8048Snapshot.MaximumFileSize;
    public static ISpectrum48Snapshot Parse(string extension, ReadOnlySpan<byte> bytes) => extension.ToLowerInvariant() switch
    {
        ".sna" => Sna48Snapshot.Parse(bytes),
        ".z80" => Z8048Snapshot.Parse(bytes),
        _ => throw new NotSupportedException("Choose a 48K .sna or .z80 snapshot; archives and other formats are unsupported.")
    };
}
