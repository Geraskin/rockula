namespace RockULA.Core.Audio;

/// <summary>Area-averaged one-bit PCM with rational phase and deterministic DC removal.</summary>
public sealed class BeeperSampler
{
    private readonly int _clockRate;
    private readonly short[] _buffer;
    private int _read;
    private int _count;
    private uint _phase;
    private long _area;
    private int _previousInput;
    private int _previousOutput;

    public BeeperSampler(int sampleRate = 48000, int clockRate = 3500000, int capacity = 8192)
    {
        if (sampleRate <= 0 || clockRate < sampleRate || capacity <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        SampleRate = sampleRate;
        _clockRate = clockRate;
        _buffer = new short[capacity];
    }
    public int SampleRate { get; }
    public int PendingSamples => _count;
    public ulong TotalSamples { get; private set; }
    public void Advance(ulong tStates, bool high)
    {
        UInt128 remaining = (UInt128)tStates * (uint)SampleRate;
        UInt128 required = (remaining + _phase) / (uint)_clockRate;
        if (required > (uint)(_buffer.Length - _count)) throw new InvalidOperationException("Beeper PCM buffer must be drained before advancing.");
        while (remaining > 0)
        {
            uint chunk = (uint)UInt128.Min(remaining, (uint)_clockRate - _phase);
            if (high) _area += (long)chunk * 16384;
            _phase += chunk;
            remaining -= chunk;
            if (_phase != _clockRate) continue;
            int input = (int)(_area / _clockRate);
            int output = input - _previousInput + _previousOutput * 995 / 1000;
            _previousInput = input;
            _previousOutput = output;
            _buffer[(_read + _count) % _buffer.Length] = (short)Math.Clamp(output, short.MinValue, short.MaxValue);
            _count++;
            TotalSamples++;
            _phase = 0;
            _area = 0;
        }
    }
    public int Drain(Span<short> destination)
    {
        int count = Math.Min(destination.Length, _count);
        for (int i = 0; i < count; i++) destination[i] = _buffer[(_read + i) % _buffer.Length];
        _read = (_read + count) % _buffer.Length;
        _count -= count;
        return count;
    }
    /// <summary>Delivery discontinuity: discard pending PCM/fraction and initialize DC at current level.</summary>
    public void Reset(bool high = false)
    {
        _read = _count = 0;
        _phase = 0;
        _area = 0;
        _previousInput = high ? 16384 : 0;
        _previousOutput = 0;
        TotalSamples = 0;
    }
}
