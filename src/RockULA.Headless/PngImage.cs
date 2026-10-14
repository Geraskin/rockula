using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace RockULA.Headless;

/// <summary>Minimal 8-bit truecolour PNG writer used to inspect the headless oracle framebuffer.</summary>
internal static class PngImage
{
    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    /// <summary>Writes a <paramref name="width"/>x<paramref name="height"/> image from 0xAARRGGBB pixels.</summary>
    public static void Write(string path, ReadOnlySpan<uint> argb, int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Dimensions must be positive.");
        if (argb.Length != width * height) throw new ArgumentException("Pixel count must match the dimensions.", nameof(argb));

        using var file = File.Create(path);
        file.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8; // bit depth
        header[9] = 2; // colour type: truecolour
        WriteChunk(file, "IHDR", header);

        byte[] raw = new byte[checked(height * (1 + width * 3))];
        int offset = 0;
        for (int y = 0; y < height; y++)
        {
            raw[offset++] = 0; // per-scanline filter: none
            for (int x = 0; x < width; x++)
            {
                uint colour = argb[y * width + x];
                raw[offset++] = (byte)(colour >> 16);
                raw[offset++] = (byte)(colour >> 8);
                raw[offset++] = (byte)colour;
            }
        }

        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(raw);
        }

        WriteChunk(file, "IDAT", compressed.GetBuffer().AsSpan(0, checked((int)compressed.Length)));
        WriteChunk(file, "IEND", ReadOnlySpan<byte>.Empty);
    }

    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);

        Span<byte> tag = stackalloc byte[4];
        Encoding.ASCII.GetBytes(type, tag);
        stream.Write(tag);
        stream.Write(data);

        uint crc = Crc32(tag, data);
        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, crc);
        stream.Write(checksum);
    }

    private static uint Crc32(ReadOnlySpan<byte> tag, ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte value in tag) crc = Mix(crc, value);
        foreach (byte value in data) crc = Mix(crc, value);
        return crc ^ 0xFFFFFFFF;
    }

    private static uint Mix(uint crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return crc;
    }
}
