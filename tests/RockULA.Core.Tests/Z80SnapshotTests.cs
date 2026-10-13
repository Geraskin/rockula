using RockULA.Formats;

namespace RockULA.Core.Tests;

public sealed class Z80SnapshotTests
{
    [Fact]
    public void BaseHeaderUsesLiteralPairOrderRFlagsIndependentIffsAndDefensiveRam()
    {
        byte[] data = Raw();
        for (int i = 0; i < 30; i++) data[i] = (byte)i;
        data[6] = 0x34; data[7] = 0x12;
        data[11] = 0xFF; data[12] = 0x0B;
        data[27] = 0; data[28] = 0xFF; data[29] = 0x42;
        data[30] = 0xAB;
        var snapshot = Z8048Snapshot.Parse(data);
        var s = snapshot.CpuState;
        Assert.Equal((ushort)0x0001, s.AF);
        Assert.Equal((ushort)0x0302, s.BC);
        Assert.Equal((ushort)0x0504, s.HL);
        Assert.Equal((ushort)0x0E0D, s.DE);
        Assert.Equal((ushort)0x1516, s.AlternateAF);
        Assert.Equal((ushort)0x100F, s.AlternateBC);
        Assert.Equal((ushort)0x1211, s.AlternateDE);
        Assert.Equal((ushort)0x1413, s.AlternateHL);
        Assert.Equal((ushort)0x1817, s.IY);
        Assert.Equal((ushort)0x1A19, s.IX);
        Assert.Equal((ushort)0x1234, s.PC);
        Assert.Equal((ushort)0x0908, s.SP);
        Assert.Equal((byte)10, s.I);
        Assert.Equal((byte)255, s.R);
        Assert.False(s.Iff1); Assert.True(s.Iff2);
        Assert.Equal((byte)2, s.InterruptMode);
        Assert.Equal((byte)5, snapshot.BorderColor);
        Assert.Equal(1, snapshot.Version);
        data[30] = 0;
        Assert.Equal((byte)0xAB, snapshot.Ram[0]);
    }

    [Fact]
    public void Flag255ConventionMeansUncompressedBorderZeroAndRHighSet()
    {
        byte[] data = Raw(); data[11] = 0x82; data[12] = 255;
        var snapshot = Z8048Snapshot.Parse(data);
        Assert.Equal((byte)0x82, snapshot.CpuState.R);
        Assert.Equal((byte)0, snapshot.BorderColor);
    }

    [Fact]
    public void V1RleHandlesLiteralEdRunEdAndTerminatorWithoutTouchingStack()
    {
        byte[] header = Raw()[..30]; header[12] = 0x20; header[8] = 0xFF; header[9] = 0xFF;
        var body = new List<byte> { 0xED, 0x00, 0xED, 0xED, 2, 0xED };
        body.AddRange(Runs(49148, 0x55)); body.AddRange(new byte[] { 0, 0xED, 0xED, 0 });
        var snapshot = Z8048Snapshot.Parse(header.Concat(body).ToArray());
        Assert.Equal(new byte[] { 0xED, 0, 0xED, 0xED }, snapshot.Ram[..4].ToArray());
        Assert.All(snapshot.Ram[4..].ToArray(), x => Assert.Equal((byte)0x55, x));
        Assert.Equal((ushort)0xFFFF, snapshot.CpuState.SP);
    }

    [Theory]
    [InlineData(23, 2)]
    [InlineData(54, 3)]
    [InlineData(55, 3)]
    public void ExtendedHeadersAcceptArbitraryPageOrderMixedEncodingAndPcZero(int extra, int version)
    {
        byte[] data = Extended(extra);
        var snapshot = Z8048Snapshot.Parse(data);
        Assert.Equal(version, snapshot.Version);
        Assert.Equal((ushort)0, snapshot.CpuState.PC);
        Assert.Equal(49152, snapshot.Ram.Length);
        Assert.All(snapshot.Ram[..16384].ToArray(), x => Assert.Equal((byte)8, x));
        Assert.All(snapshot.Ram.Slice(16384, 16384).ToArray(), x => Assert.Equal((byte)4, x));
        Assert.All(snapshot.Ram[32768..].ToArray(), x => Assert.Equal((byte)5, x));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(49181)]
    [InlineData(49183)]
    [InlineData(200001)]
    public void InvalidRawSizesReject(int length)
    {
        byte[] data = new byte[length]; if (length >= 8) data[6] = 1;
        Assert.Throws<ArgumentException>(() => Z8048Snapshot.Parse(data));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(22)]
    [InlineData(24)]
    [InlineData(53)]
    [InlineData(56)]
    public void UnsupportedExtendedLengthsReject(int length)
    {
        byte[] data = new byte[100]; data[30] = (byte)length;
        Assert.Throws<ArgumentException>(() => Z8048Snapshot.Parse(data));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void MalformedV1RleRejectsZeroCountOverflowShortRunMissingMarkerAndSuffix(int fault)
    {
        byte[] header = Raw()[..30]; header[12] = 0x20;
        var body = new List<byte>(Runs(49152, 7));
        if (fault == 0) body.InsertRange(0, new byte[] { 0xED, 0xED, 0, 7 });
        if (fault == 1) body.Add(1);
        if (fault == 2) body.RemoveRange(body.Count - 4, 4);
        if (fault == 3) body.AddRange(new byte[] { 0xED, 0xED, 1 });
        if (fault != 4) body.AddRange(new byte[] { 0, 0xED, 0xED, 0 });
        if (fault == 5) body.Add(99);
        Assert.Throws<ArgumentException>(() => Z8048Snapshot.Parse(header.Concat(body).ToArray()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ExtendedPageCoverageAndLengthsAreStrict(int fault)
    {
        byte[] data = Extended(23);
        if (fault == 0) data[57] = 4; // First page 5 becomes a duplicate of second page 4.
        if (fault == 1) data = data[..(data.Length - (Runs(16384, 8).Length + 3))];
        if (fault == 2) { data[55] = 0; data[56] = 0; } // Empty compressed block.
        if (fault == 3) data = data[..^1];
        if (fault == 4) data = data.Concat(new byte[] { 0xFF }).ToArray();
        Assert.Throws<ArgumentException>(() => Z8048Snapshot.Parse(data));
    }

    [Theory]
    [InlineData(34, 4)]
    [InlineData(34, 1)]
    [InlineData(37, 128)]
    [InlineData(37, 4)]
    [InlineData(36, 255)]
    [InlineData(59, 255)]
    [InlineData(60, 255)]
    public void UnsupportedHardwareOrPagedExtensionsReject(int offset, byte value)
    {
        byte[] data = Extended(54); data[offset] = value;
        Assert.Throws<NotSupportedException>(() => Z8048Snapshot.Parse(data));
    }

    [Fact]
    public void InvalidInterruptModeRejects()
    {
        byte[] data = Raw(); data[29] = 3;
        Assert.Throws<ArgumentException>(() => Z8048Snapshot.Parse(data));
    }
    internal static byte[] Raw()
    {
        byte[] data = new byte[49182]; data[6] = 1; return data;
    }
    internal static byte[] Extended(int extra)
    {
        var header = new byte[32 + extra]; header[30] = (byte)extra;
        var data = new List<byte>(header);
        foreach (byte page in new byte[] { 5, 4, 8 })
        {
            byte[] payload = page == 4 ? Enumerable.Repeat(page, 16384).ToArray() : Runs(16384, page);
            int length = page == 4 ? 65535 : payload.Length;
            data.Add((byte)length); data.Add((byte)(length >> 8)); data.Add(page); data.AddRange(payload);
        }
        return data.ToArray();
    }
    internal static byte[] Runs(int count, byte value)
    {
        var bytes = new List<byte>();
        while (count > 0) { int n = Math.Min(255, count); bytes.AddRange(new byte[] { 0xED, 0xED, (byte)n, value }); count -= n; }
        return bytes.ToArray();
    }
}
