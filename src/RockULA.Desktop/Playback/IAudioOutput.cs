namespace RockULA.Desktop.Playback;

public interface IAudioOutput
{
    bool Available { get; }
    int QueuedSamples { get; }
    void Enqueue(short[] samples, int count);
    void Reset(bool playing);
}
