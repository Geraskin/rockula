namespace RockULA.Core;

/// <summary>A defensively copied 48K firmware input; this type does not execute it.</summary>
public sealed class RomImage
{
    public const int Spectrum48Size = 16 * 1024;

    private readonly byte[] _bytes;

    private RomImage(byte[] bytes)
    {
        _bytes = bytes;
    }

    public ReadOnlySpan<byte> Bytes => _bytes;

    public static RomImage FromSpectrum48(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Spectrum48Size)
        {
            throw new ArgumentException(
                $"A Spectrum 48K firmware image must contain exactly {Spectrum48Size} bytes.",
                nameof(bytes));
        }

        return new RomImage(bytes.ToArray());
    }
}
