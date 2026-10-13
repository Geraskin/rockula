using System.Collections.Concurrent;
using RockULA.Core;
using RockULA.Core.Input;
using RockULA.Formats;
using RockULA.Desktop.Devices;
using RockULA.Desktop.Playback;

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
        public ConcurrentQueue<int> WriterThreads { get; } = new();
        public bool Available => true;
        public int QueuedSamples => Volatile.Read(ref _queued);
        public void Enqueue(short[] samples, int count)
        {
            WriterThreads.Enqueue(Environment.CurrentManagedThreadId);
            if (FailEnqueue) throw new IOException("Synthetic audio output failure");
            Interlocked.Add(ref _queued, count);
        }
        public void Reset(bool playing)
        {
            WriterThreads.Enqueue(Environment.CurrentManagedThreadId);
            Interlocked.Exchange(ref _queued, 0);
        }
    }
}
