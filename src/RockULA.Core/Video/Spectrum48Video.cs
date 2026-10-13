using RockULA.Core.Machines;

namespace RockULA.Core.Video;

public static class Spectrum48Video
{
    public const int Width = 320;
    public const int Height = 240;
    public static void Render(Spectrum48Bus bus, Span<uint> pixels, ulong frame) => throw new NotImplementedException();
}
