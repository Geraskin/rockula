using RockULA.Core.Machines;

namespace RockULA.Core.Video;

/// <summary>Final-RAM bitmap/attributes with cropped border; not a raster renderer.</summary>
public static class Spectrum48Video
{
    public const int Width = 320;
    public const int Height = 240;
    public static void Render(Spectrum48Bus bus, Span<uint> pixels, ulong frame)
    {
        ArgumentNullException.ThrowIfNull(bus);
        if (pixels.Length != Width * Height) throw new ArgumentException("Expected 320x240 pixels.", nameof(pixels));
        pixels.Fill(Color(bus.BorderColor, false));
        bool flash = (frame & 16) != 0;
        for (int y = 0; y < 192; y++)
            for (int column = 0; column < 32; column++)
            {
                ushort bitmap = (ushort)(0x4000 | ((y & 0xC0) << 5) | ((y & 7) << 8) | ((y & 0x38) << 2) | column);
                byte value = bus.Peek(bitmap);
                byte attribute = bus.Peek((ushort)(0x5800 + (y / 8) * 32 + column));
                int ink = attribute & 7;
                int paper = (attribute >> 3) & 7;
                if (flash && (attribute & 0x80) != 0) (ink, paper) = (paper, ink);
                bool bright = (attribute & 0x40) != 0;
                for (int bit = 0; bit < 8; bit++)
                    pixels[(y + 24) * Width + 32 + column * 8 + bit] = Color((value & (0x80 >> bit)) != 0 ? ink : paper, bright);
            }
    }
    private static uint Color(int index, bool bright)
    {
        uint intensity = bright ? 255U : 205U;
        return 0xFF000000U | ((index & 2) != 0 ? intensity << 16 : 0)
            | ((index & 4) != 0 ? intensity << 8 : 0) | ((index & 1) != 0 ? intensity : 0);
    }
}
