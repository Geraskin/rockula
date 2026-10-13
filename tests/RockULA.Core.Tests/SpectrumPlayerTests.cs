using RockULA.Core;
using RockULA.Core.Cpu;
using RockULA.Core.Input;
using RockULA.Core.Machines;
using RockULA.Core.Video;
using RockULA.Formats;

namespace RockULA.Core.Tests;

public sealed class SpectrumPlayerTests
{
    [Fact]
    public void SnaRestoresExplicitHeaderAndStackWithoutErasingRam()
    {
        byte[] data = Fixture();
        for (int offset = 1; offset < 19; offset += 2) { data[offset] = (byte)offset; data[offset + 1] = 0xA5; }
        data[0] = 0x12;
        data[19] = 4;
        data[20] = 0xFE;
        data[21] = 0x33;
        data[22] = 0x44;
        data[26] = 6;
        var snapshot = Sna48Snapshot.Parse(data);
        var state = snapshot.CpuState;
        Assert.Equal((ushort)0xA501, state.AlternateHL);
        Assert.Equal((ushort)0xA503, state.AlternateDE);
        Assert.Equal((ushort)0xA505, state.AlternateBC);
        Assert.Equal((ushort)0xA507, state.AlternateAF);
        Assert.Equal((ushort)0xA509, state.HL);
        Assert.Equal((ushort)0xA50B, state.DE);
        Assert.Equal((ushort)0xA50D, state.BC);
        Assert.Equal((ushort)0xA50F, state.IY);
        Assert.Equal((ushort)0xA511, state.IX);
        Assert.Equal((ushort)0x4433, state.AF);
        Assert.Equal((ushort)0x8000, state.PC);
        Assert.Equal((ushort)0x9002, state.SP);
        Assert.True(state.Iff1);
        Assert.True(state.Iff2);
        Assert.Equal((byte)0xFE, state.R);
        Assert.Equal((byte)0x12, state.I);
        Assert.Equal((byte)6, snapshot.BorderColor);
        data[27 + 0x5001] = 0;
        Assert.Equal((byte)0x80, snapshot.Ram[0x5001]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(26)]
    [InlineData(49178)]
    [InlineData(49180)]
    [InlineData(131103)]
    public void SnaRejectsWrongLengths(int length) => Assert.Throws<ArgumentException>(() => Sna48Snapshot.Parse(new byte[length]));

    [Theory]
    [InlineData(0x3FFF, 1, 0)]
    [InlineData(0xFFFF, 1, 0)]
    [InlineData(0x9000, 3, 0)]
    [InlineData(0x9000, 1, 8)]
    public void SnaRejectsInvalidStackModeAndBorder(int sp, int mode, int border)
    {
        byte[] data = Fixture();
        data[23] = (byte)sp;
        data[24] = (byte)(sp >> 8);
        data[25] = (byte)mode;
        data[26] = (byte)border;
        Assert.Throws<ArgumentException>(() => Sna48Snapshot.Parse(data));
    }

    [Fact]
    public void StackAtFffeRestoresWrappedSp()
    {
        byte[] data = Fixture();
        data[23] = 0xFE;
        data[24] = 0xFF;
        data[^2] = 0x34;
        data[^1] = 0x12;
        var state = Sna48Snapshot.Parse(data).CpuState;
        Assert.Equal((ushort)0, state.SP);
        Assert.Equal((ushort)0x1234, state.PC);
    }

    [Fact]
    public void CpuCannotWriteIntoRom()
    {
        byte[] rom = new byte[16384];
        byte[] code = [0x3E, 0x42, 0x32, 0, 0, 0x3A, 0, 0];
        code.CopyTo(rom, 0);
        var machine = new Spectrum48Machine(RomImage.FromSpectrum48(rom));
        machine.Cpu.Step(); machine.Cpu.Step(); machine.Cpu.Step();
        Assert.Equal((byte)0x3E, machine.Cpu.Registers.A);
        Assert.Equal((byte)0x3E, machine.Bus.Peek(0));
    }

    [Fact]
    public void AllFortyKeysAndCombinedRowsUseActiveLowSelection()
    {
        var bus = Bus();
        ushort[] ports = [0xFEFE, 0xFDFE, 0xFBFE, 0xF7FE, 0xEFFE, 0xDFFE, 0xBFFE, 0x7FFE];
        for (int key = 0; key < 40; key++)
        {
            bus.ReleaseKeys();
            bus.SetKey((SpectrumKey)key, true);
            for (int row = 0; row < 8; row++)
                Assert.Equal(row == key / 5 ? (byte)(255 - (1 << (key % 5))) : (byte)255, Read(bus, ports[row]));
        }
        bus.ReleaseKeys();
        bus.SetKey(SpectrumKey.CapsShift, true);
        bus.SetKey(SpectrumKey.S, true);
        Assert.Equal((byte)0xFC, Read(bus, 0xFCFE));
        bus.EarHigh = false;
        Assert.Equal((byte)0xBC, Read(bus, 0xFCFE));
        bus.ReleaseKeys();
        Assert.Equal((byte)0xBF, Read(bus, 0xFEFE));
    }

    [Fact]
    public void EvenPortAliasesLatchBorderAndBeeperAndJoystickIsSeparate()
    {
        var bus = Bus();
        bus.Execute(new Z80BusCycle(Z80BusCycleKind.PortWrite, 0x1234, 4, 4, 0x1D));
        Assert.Equal((byte)5, bus.BorderColor);
        Assert.True(bus.Beeper);
        Assert.True(bus.Mic);
        bus.Joystick = 0x15;
        Assert.Equal((byte)0x15, Read(bus, 0xAB1F));
        Assert.Equal((byte)0xFF, Read(bus, 0xABFF));
    }

    [Theory]
    [InlineData(0, 0x4000, 0x5800)]
    [InlineData(1, 0x4100, 0x5800)]
    [InlineData(7, 0x4700, 0x5800)]
    [InlineData(8, 0x4020, 0x5820)]
    [InlineData(63, 0x47E0, 0x58E0)]
    [InlineData(64, 0x4800, 0x5900)]
    [InlineData(128, 0x5000, 0x5A00)]
    [InlineData(191, 0x57E0, 0x5AE0)]
    public void DisplayUsesLiteralBitmapAddressesAndBitOrder(int y, int address, int attr)
    {
        var bus = Bus();
        bus.Poke((ushort)address, 0x81);
        bus.Poke((ushort)attr, 7);
        uint[] pixels = new uint[320 * 240];
        Spectrum48Video.Render(bus, pixels, 0);
        Assert.Equal(0xFFCDCDCDU, pixels[(y + 24) * 320 + 32]);
        Assert.Equal(0xFF000000U, pixels[(y + 24) * 320 + 33]);
        Assert.Equal(0xFFCDCDCDU, pixels[(y + 24) * 320 + 39]);
    }

    [Fact]
    public void BrightFlashAndBorderAreRenderedIndependently()
    {
        var bus = Bus();
        bus.Poke(0x4000, 0x80);
        bus.Poke(0x5800, 0xCA); // FLASH, bright, blue paper, red ink.
        bus.Execute(new Z80BusCycle(Z80BusCycleKind.PortWrite, 0xFE, 4, 4, 4));
        uint[] pixels = new uint[320 * 240];
        Spectrum48Video.Render(bus, pixels, 0);
        Assert.Equal(0xFF00CD00U, pixels[0]);
        Assert.Equal(0xFFFF0000U, pixels[24 * 320 + 32]);
        Assert.Equal(0xFF0000FFU, pixels[24 * 320 + 33]);
        Spectrum48Video.Render(bus, pixels, 16);
        Assert.Equal(0xFF0000FFU, pixels[24 * 320 + 32]);
    }

    [Fact]
    public void FailedImportIsAtomicAndSuccessfulImportInitializesMissingState()
    {
        var machine = new Spectrum48Machine(RomImage.FromSpectrum48(new byte[16384]));
        machine.Bus.Poke(0x4000, 0xA5);
        var original = machine.Cpu;
        var state = Sna48Snapshot.Parse(Fixture()).CpuState;
        Assert.Throws<ArgumentException>(() => machine.LoadSnapshot(state, new byte[1], 0));
        Assert.Same(original, machine.Cpu);
        Assert.Equal((byte)0xA5, machine.Bus.Peek(0x4000));
        var snapshot = Sna48Snapshot.Parse(Fixture());
        machine.LoadSnapshot(snapshot.CpuState, snapshot.Ram, 3);
        Assert.NotSame(original, machine.Cpu);
        Assert.Equal((ushort)0x8000, machine.Cpu.Registers.PC);
        Assert.Equal((byte)0, machine.Cpu.Registers.Q);
        Assert.Equal((ushort)0, machine.Cpu.Registers.WZ);
        Assert.Equal(0UL, machine.Bus.TStates);
        Assert.False(machine.Cpu.IsHalted);
    }

    [Fact]
    public void SyntheticSnaRunsFramesWithInputScreenAndPreservedOvershoot()
    {
        byte[] bytes = Fixture();
        byte[] code = [0x3E, 7, 0x32, 0, 0x58, 0x3E, 0x80, 0x32, 0, 0x40,
            0x3E, 2, 0xD3, 0xFE, 0xDB, 0xFE, 0x32, 0, 0x60, 0x18, 0xF5];
        code.CopyTo(bytes, 27 + 0x4000);
        var snapshot = Sna48Snapshot.Parse(bytes);
        var machine = new Spectrum48Machine(RomImage.FromSpectrum48(new byte[16384]));
        machine.LoadSnapshot(snapshot.CpuState, snapshot.Ram, snapshot.BorderColor);
        machine.Bus.SetKey(SpectrumKey.Space, true);
        Assert.True(machine.RunFrame() > 0);
        Assert.InRange(machine.Bus.TStates, 69888UL, 69910UL);
        Assert.Equal((byte)0xFE, machine.Bus.Peek(0x6000));
        Assert.Equal((byte)2, machine.Bus.BorderColor);
        uint[] pixels = new uint[320 * 240];
        Spectrum48Video.Render(machine.Bus, pixels, machine.FrameNumber);
        Assert.Equal(0xFFCDCDCDU, pixels[24 * 320 + 32]);
        machine.RunFrame();
        Assert.InRange(machine.Bus.TStates, 139776UL, 139798UL);
        Assert.Equal(2UL, machine.FrameNumber);
    }

    [Fact]
    public void FrameInterruptPulseTracksHardwareTimeIncludingCrossedBoundary()
    {
        var machine = new Spectrum48Machine(RomImage.FromSpectrum48(new byte[16384]));
        Assert.True(machine.Cpu.IsInterruptLineAsserted);
        machine.Bus.Execute(new Z80BusCycle(Z80BusCycleKind.Internal, 0, 32, 0));
        Assert.False(machine.Cpu.IsInterruptLineAsserted);
        machine.Bus.Execute(new Z80BusCycle(Z80BusCycleKind.Internal, 0, 69856, 0));
        Assert.True(machine.Cpu.IsInterruptLineAsserted);
    }

    private static Spectrum48Bus Bus() => new(RomImage.FromSpectrum48(new byte[16384]));
    private static byte Read(Spectrum48Bus bus, ushort port) => bus.Execute(new Z80BusCycle(Z80BusCycleKind.PortRead, port, 4, 4));
    internal static byte[] Fixture()
    {
        byte[] bytes = new byte[49179];
        bytes[23] = 0;
        bytes[24] = 0x90;
        bytes[25] = 1;
        bytes[27 + 0x5000] = 0;
        bytes[27 + 0x5001] = 0x80;
        return bytes;
    }
}
