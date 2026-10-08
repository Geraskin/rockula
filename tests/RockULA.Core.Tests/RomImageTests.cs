using RockULA.Core;

namespace RockULA.Core.Tests;

public sealed class RomImageTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(16383)]
    [InlineData(16385)]
    [InlineData(32768)]
    public void RejectsWrongFirmwareLength(int length)
    {
        var error = Assert.Throws<ArgumentException>(
            () => RomImage.FromSpectrum48(new byte[length]));

        Assert.Equal("bytes", error.ParamName);
    }

    [Fact]
    public void PreservesAllBytesOfAnExactSizeInput()
    {
        var input = new byte[16384];
        input[0] = 0xF3;
        input[8192] = 0x42;
        input[16383] = 0xC9;

        var image = RomImage.FromSpectrum48(input);

        Assert.Equal(input, image.Bytes.ToArray());
    }

    [Fact]
    public void CallerMutationCannotChangeFirmware()
    {
        var input = new byte[16384];
        input[0] = 0xF3;
        var image = RomImage.FromSpectrum48(input);

        input[0] = 0x00;

        Assert.Equal(0xF3, image.Bytes[0]);
    }
}
