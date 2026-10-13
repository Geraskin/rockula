using System.Collections.Concurrent;
using RockULA.Core;
using RockULA.Core.Input;
using RockULA.Desktop.Devices;
using RockULA.Desktop.Playback;
using RockULA.Formats;

namespace RockULA.Desktop.Tests;

public sealed class PlaybackTests
{
    [Theory]
    [InlineData(7999, -7999, 0, false, 0)]
    [InlineData(8000, 8000, 0, false, 5)]
    [InlineData(-8000, -8000, 0, true, 26)]
    [InlineData(0, 0, 3, true, 25)]
    [InlineData(0, 0, 12, false, 6)]
    public void AxisThresholdHatAndFireUseLiteralBits(short x, short y, byte hat, bool fire, byte expected) =>
        Assert.Equal((JoystickButtons)expected, new JoystickMapping().Map(x, y, hat, fire));

    [Fact]
    public void KeyboardRebindingPreservesMultipleActionsOnOneKeyAndRelease()
    {
        var mapping = new KeyboardJoystickKeys(Left: Avalonia.Input.Key.A, Fire: Avalonia.Input.Key.A);
        var held = new HashSet<Avalonia.Input.Key> { Avalonia.Input.Key.A };
        Assert.Equal((JoystickButtons)18, mapping.Read(held));
        held.Clear();
        Assert.Equal(JoystickButtons.None, mapping.Read(held));
    }

    [Fact]
    public void DigitalDirectionButtonsSupportBothMasksAndDisabledBindings()
    {
        var mapping = new JoystickMapping(LeftButton: 0, RightButton: 64, UpButton: 1, DownButton: 127);
        Assert.Equal((JoystickButtons)15, mapping.MapDirectionButtons(3, 1UL | (1UL << 63)));
        Assert.Equal(JoystickButtons.None, new JoystickMapping().MapDirectionButtons(ulong.MaxValue, ulong.MaxValue));
        Assert.False(JoystickMapping.Pressed(-1, ulong.MaxValue, ulong.MaxValue));
        Assert.False(JoystickMapping.Pressed(128, ulong.MaxValue, ulong.MaxValue));
    }

    [Fact]
    public void AxisInversionHandlesSignedMinimumWithoutOverflow()
    {
        var mapping = new JoystickMapping(InvertX: true, InvertY: true);
        Assert.Equal(JoystickButtons.Right | JoystickButtons.Down, mapping.Map(short.MinValue, short.MinValue, 0, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JoystickMapping(DeadZone: 0).Map(0, 0, 0, false));
    }

    [Fact]
    public async Task WorkerOwnsAudioAndPauseIsAcknowledgedBeforeTimeStops()
    {
        var audio = new FakeAudio();
        var session = new PlayerSession(audio);
        try
        {
            await session.LoadAsync(RomImage.FromSpectrum48(new byte[16384]));
            Assert.False(session.Latest.Running);
            Assert.Equal(0UL, session.Latest.TStates);
            await session.SetRunningAsync(true);
            await Until(() => session.Latest.Number >= 1);
            var first = session.Latest;
            uint[] firstCopy = first.Pixels.ToArray();
            await session.SetRunningAsync(false);
            ulong paused = session.Latest.TStates;
            await Task.Delay(30);
            Assert.Equal(paused, session.Latest.TStates);
            Assert.False(session.Latest.Running);
            Assert.Equal(0, audio.QueuedSamples);
            Assert.Equal(firstCopy, first.Pixels);
            Assert.Single(audio.WriterThreads.Distinct());
            await Assert.ThrowsAsync<ArgumentNullException>(() => session.LoadAsync(null!));
            Assert.Equal(paused, session.Latest.TStates);
            await session.RestartAsync();
            Assert.Equal(0UL, session.Latest.TStates);
            Assert.Equal(0UL, session.Latest.Number);
            Assert.False(session.Latest.Running);
        }
        finally { await session.CloseAsync(); }
        await session.CloseAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.SetRunningAsync(true));
        session.SetInput(new()); // Late UI polling during close is harmless.
    }

    [Fact]
    public async Task BoundedCommandQueueDoesNotDeadlockAndVolumeIsValidated()
    {
        var session = new PlayerSession(new FakeAudio());
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 40).Select(i => session.SetVolumeAsync(i, false)));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.SetVolumeAsync(101, false));
            await session.LoadAsync(RomImage.FromSpectrum48(new byte[16384]));
        }
        finally { await session.CloseAsync(); }
    }

    [Fact]
    public async Task CpuFaultPublishesStoppedFrameAndRestartRemainsAvailable()
    {
        byte[] rom = Enumerable.Repeat((byte)0xDD, 16384).ToArray();
        var session = new PlayerSession(new FakeAudio());
        try
        {
            await session.LoadAsync(RomImage.FromSpectrum48(rom));
            byte[] sna = new byte[Sna48Snapshot.FileSize];
            Array.Fill(sna, (byte)0xDD, 27, 49152);
            sna[24] = 0x40; // SP=4000, stacked PC=DDDD: entire address space is DD.
            await session.LoadAsync(RomImage.FromSpectrum48(rom), Sna48Snapshot.Parse(sna));
            await session.SetRunningAsync(true);
            await Until(() => session.Latest.Status.StartsWith("Stopped:"));
            Assert.False(session.Latest.Running);
            await session.RestartAsync();
            Assert.Equal(0UL, session.Latest.TStates);
        }
        finally { await session.CloseAsync(); }
    }

    [Fact]
    public async Task RealGuestBeeperProducesPcmAndMuteClearsOldOutputWithoutResettingGuestTime()
    {
        byte[] rom = new byte[16384];
        new byte[] { 0x3E, 0x10, 0xD3, 0xFE, 0xEE, 0x10, 0x18, 0xFA }.CopyTo(rom, 0);
        var audio = new FakeAudio();
        var session = new PlayerSession(audio);
        try
        {
            await session.LoadAsync(RomImage.FromSpectrum48(rom));
            await session.SetRunningAsync(true);
            await Until(() => audio.Pcm.Any(x => x.Any(sample => sample != 0)));
            ulong beforeMute = session.Latest.TStates;
            await session.SetVolumeAsync(50, true);
            await Until(() => !audio.Pcm.IsEmpty);
            Assert.All(audio.Pcm, block => Assert.All(block, sample => Assert.Equal((short)0, sample)));
            Assert.True(session.Latest.TStates >= beforeMute);
            await session.SetVolumeAsync(100, false);
            await Until(() => audio.Pcm.Any(x => x.Any(sample => sample != 0)));
        }
        finally { await session.CloseAsync(); }
    }

    [Fact]
    public async Task OutputFailureFallsBackToSilentPacingWithoutStoppingCpu()
    {
        var audio = new FakeAudio { FailEnqueue = true };
        var session = new PlayerSession(audio);
        try
        {
            await session.LoadAsync(RomImage.FromSpectrum48(new byte[16384]));
            await session.SetRunningAsync(true);
            await Until(() => session.Latest.Number >= 2);
            Assert.True(session.Latest.Running);
            Assert.Contains("Audio unavailable", session.Latest.Status);
        }
        finally { await session.CloseAsync(); }
    }

    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(5, timeout.Token);
    }
    private sealed class FakeAudio : IAudioOutput
    {
        private int _queued;
        public bool FailEnqueue { get; init; }
        public ConcurrentQueue<short[]> Pcm { get; } = new();
        public ConcurrentQueue<int> WriterThreads { get; } = new();
        public bool Available => true;
        public int QueuedSamples => Volatile.Read(ref _queued);
        public void Enqueue(short[] samples, int count)
        {
            WriterThreads.Enqueue(Environment.CurrentManagedThreadId);
            if (FailEnqueue) throw new IOException("Synthetic audio output failure");
            Pcm.Enqueue(samples.AsSpan(0, count).ToArray());
            Interlocked.Add(ref _queued, count);
        }
        public void Reset(bool playing)
        {
            WriterThreads.Enqueue(Environment.CurrentManagedThreadId);
            Interlocked.Exchange(ref _queued, 0);
            Pcm.Clear();
        }
    }
}
