namespace RockULA.Core.Audio;

/// <summary>Bounded rational one-bit PCM sampler; owned by the machine caller.</summary>
public sealed class BeeperSampler
{
    public BeeperSampler(int sampleRate = 48000, int clockRate = 3500000, int capacity = 8192)
    {
        if (sampleRate <= 0 || clockRate < sampleRate || capacity <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        SampleRate = sampleRate;
    }
    public int SampleRate { get; }
    public int PendingSamples => 0;
    public ulong TotalSamples => 0;
    public void Advance(ulong tStates, bool high) => throw new NotImplementedException();
    public int Drain(Span<short> destination) => throw new NotImplementedException();
    public void Reset(bool high = false) => throw new NotImplementedException();
}
