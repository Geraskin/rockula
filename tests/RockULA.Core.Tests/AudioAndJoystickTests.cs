using RockULA.Core;
using RockULA.Core.Audio;
using RockULA.Core.Cpu;
using RockULA.Core.Input;
using RockULA.Core.Machines;

namespace RockULA.Core.Tests;

public sealed class AudioAndJoystickTests
{
    [Fact]
    public void SubsamplePulseIsIntegratedAndDcBlockerIsDeterministic()
    {
        var audio = new BeeperSampler(10, 100);
        audio.Advance(5, false);
        Assert.Equal(0, audio.PendingSamples);
        audio.Advance(5, true);
        audio.Advance(10, true);
        audio.Advance(10, false);
        short[] samples = new short[3];
        Assert.Equal(3, audio.Drain(samples));
        // Mean levels 8192, 16384, 0. y=x-oldX+oldY*995/1000, truncating toward zero.
        Assert.Equal(new short[] { 8192, 16343, -123 }, samples);
        Assert.Equal(3UL, audio.TotalSamples);
        Assert.Equal(0, audio.PendingSamples);
    }

    [Fact]
    public void FractionSurvivesChunkingAndDrainWrap()
    {
        var whole = new BeeperSampler(3, 10, 8);
        var split = new BeeperSampler(3, 10, 8);
        whole.Advance(20, true);
        for (int i = 0; i < 20; i++) split.Advance(1, true);
        short[] a = new short[6], b = new short[6];
        Assert.Equal(6, whole.Drain(a));
        Assert.Equal(2, split.Drain(b.AsSpan(0, 2)));
        split.Advance(10, false);
        Assert.Equal(4, split.Drain(b.AsSpan(2, 4)));
        Assert.Equal(a, b);
        Assert.Equal(9UL, split.TotalSamples);
        Assert.Equal(3, split.PendingSamples);
    }

    [Fact]
    public void CountTracksClockAcrossManyFramesWithoutRoundingEachFrame()
    {
        var audio = new BeeperSampler();
        short[] output = new short[1024];
        for (int frame = 0; frame < 100; frame++)
        {
            audio.Advance(69888, false);
            audio.Drain(output);
        }
        Assert.Equal(95846UL, audio.TotalSamples);
        Assert.Equal(0, audio.PendingSamples);
    }

    [Fact]
    public void CapacityFailureDoesNotMutatePhaseOrOutputAndResetClearsDc()
    {
        var audio = new BeeperSampler(10, 100, 2);
        audio.Advance(15, true);
        Assert.Throws<InvalidOperationException>(() => audio.Advance(20, false));
        Assert.Equal(1UL, audio.TotalSamples);
        audio.Advance(5, true);
        short[] output = new short[2];
        Assert.Equal(2, audio.Drain(output));
        Assert.Equal(new short[] { 16384, 16302 }, output);
        audio.Advance(5, false);
        audio.Reset(true);
        audio.Advance(10, true);
        Assert.Equal(1, audio.Drain(output));
        Assert.Equal((short)0, output[0]);
        Assert.Equal(1UL, audio.TotalSamples);
    }

    [Fact]
    public void BusIntegratesOldLevelBeforeEvenPortTransferAndImportDiscardsAudioState()
    {
        var machine = new Spectrum48Machine(RomImage.FromSpectrum48(new byte[16384]));
        machine.Bus.EnableAudio();
        machine.Bus.Execute(new(Z80BusCycleKind.PortWrite, 0x00FE, 4, 4, 0x10));
        machine.Bus.Execute(new(Z80BusCycleKind.Internal, 0, 69, 69));
        short[] output = new short[2];
        Assert.Equal(1, machine.Bus.Audio!.Drain(output));
        // First window has 192000 low units and 3308000 high units out of 3500000.
        Assert.Equal((short)15485, output[0]);
        machine.LoadSnapshot(default, new byte[49152], 0);
        Assert.Null(machine.Bus.Audio);
        Assert.False(machine.Bus.Beeper);
    }

    [Theory]
    [InlineData(0, 100, 2)]
    [InlineData(101, 100, 2)]
    [InlineData(10, 100, 0)]
    public void InvalidAudioConfigurationIsRejected(int rate, int clock, int capacity) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new BeeperSampler(rate, clock, capacity));

    [Theory]
    [InlineData(SpectrumJoystickMode.Sinclair67890, JoystickButtons.Left, 0xEFFE, 0xEF)]
    [InlineData(SpectrumJoystickMode.Sinclair67890, JoystickButtons.Right, 0xEFFE, 0xF7)]
    [InlineData(SpectrumJoystickMode.Sinclair67890, JoystickButtons.Down, 0xEFFE, 0xFB)]
    [InlineData(SpectrumJoystickMode.Sinclair67890, JoystickButtons.Up, 0xEFFE, 0xFD)]
    [InlineData(SpectrumJoystickMode.Sinclair67890, JoystickButtons.Fire, 0xEFFE, 0xFE)]
    [InlineData(SpectrumJoystickMode.Sinclair12345, JoystickButtons.Left, 0xF7FE, 0xFE)]
    [InlineData(SpectrumJoystickMode.Sinclair12345, JoystickButtons.Right, 0xF7FE, 0xFD)]
    [InlineData(SpectrumJoystickMode.Sinclair12345, JoystickButtons.Down, 0xF7FE, 0xFB)]
    [InlineData(SpectrumJoystickMode.Sinclair12345, JoystickButtons.Up, 0xF7FE, 0xF7)]
    [InlineData(SpectrumJoystickMode.Sinclair12345, JoystickButtons.Fire, 0xF7FE, 0xEF)]
    public void SinclairUsesLiteralActiveLowKeyboardRows(SpectrumJoystickMode mode, JoystickButtons button, ushort port, byte expected)
    {
        var bus = new Spectrum48Bus(RomImage.FromSpectrum48(new byte[16384]));
        SpectrumInput.Apply(bus, 0, button, mode);
        Assert.Equal(expected, bus.Execute(new(Z80BusCycleKind.PortRead, port, 4, 4)));
        Assert.Equal((byte)0, bus.Joystick);
    }

    [Fact]
    public void KeyboardAndJoystickOverlapIsUnionAndSwitchingReleasesPreviousMode()
    {
        var bus = new Spectrum48Bus(RomImage.FromSpectrum48(new byte[16384]));
        ulong held6 = 1UL << (int)SpectrumKey.D6;
        SpectrumInput.Apply(bus, held6, JoystickButtons.Left | JoystickButtons.Up, SpectrumJoystickMode.Sinclair67890);
        Assert.Equal((byte)0xED, bus.Execute(new(Z80BusCycleKind.PortRead, 0xEFFE, 4, 4)));
        SpectrumInput.Apply(bus, held6, JoystickButtons.None, SpectrumJoystickMode.Sinclair67890);
        Assert.Equal((byte)0xEF, bus.Execute(new(Z80BusCycleKind.PortRead, 0xEFFE, 4, 4)));
        SpectrumInput.Apply(bus, 0, JoystickButtons.Right | JoystickButtons.Fire, SpectrumJoystickMode.Kempston);
        Assert.Equal((byte)17, bus.Execute(new(Z80BusCycleKind.PortRead, 0x001F, 4, 4)));
        Assert.Equal((byte)0xFF, bus.Execute(new(Z80BusCycleKind.PortRead, 0xEFFE, 4, 4)));
        SpectrumInput.Apply(bus, 0, (JoystickButtons)31, SpectrumJoystickMode.KeyboardOnly);
        Assert.Equal((byte)0, bus.Joystick);
    }
}
