using System.Collections.Concurrent;
using System.Diagnostics;
using RockULA.Core;
using RockULA.Core.Input;
using RockULA.Core.Machines;
using RockULA.Core.Video;
using RockULA.Formats;

namespace RockULA.Desktop.Playback;

public sealed record PlayerInput(ulong Keys = 0, JoystickButtons Joystick = JoystickButtons.None,
    SpectrumJoystickMode Mode = SpectrumJoystickMode.Kempston);
/// <summary>Published pixels are immutable by contract; no live machine references escape.</summary>
public sealed record PlayerFrame(uint[] Pixels, ulong Number, ulong TStates, ushort PC, bool Running, string Status, long Version);

public sealed class PlayerSession
{
    private sealed record Command(Action Apply, TaskCompletionSource Completion);
    private readonly ConcurrentQueue<Command> _commands = new();
    private readonly SemaphoreSlim _slots = new(16, 16);
    private readonly AutoResetEvent _wake = new(false);
    private readonly object _commandLock = new();
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IAudioOutput _audio;
    private readonly short[] _pcm = new short[2048];
    private readonly Thread _thread;
    private PlayerInput _input = new();
    private PlayerFrame _latest = new(new uint[320 * 240], 0, 0, 0, false, "Load a ROM and 48K SNA", 0);
    private Spectrum48Machine? _machine;
    private RomImage? _rom;
    private Sna48Snapshot? _snapshot;
    private bool _running;
    private bool _stopping;
    private bool _audioFailed;
    private int _closeRequested;
    private int _volume = 50;
    private bool _muted;
    private long _version;
    private long _originTicks;
    private ulong _originTStates;
    private long _queueChanged;
    private int _lastQueued = -1;
    private string _audioWarning = "";

    public PlayerSession(IAudioOutput audio)
    {
        _audio = audio;
        _thread = new Thread(Run) { IsBackground = true, Name = "RockULA machine owner" };
        _thread.Start();
    }
    public PlayerFrame Latest => Volatile.Read(ref _latest);
    public void SetInput(PlayerInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        lock (_commandLock)
        {
            if (_closeRequested != 0) return;
            Volatile.Write(ref _input, input);
            _wake.Set();
        }
    }
    public Task LoadAsync(RomImage rom, Sna48Snapshot? snapshot = null) => Submit(() =>
    {
        var candidate = CreateMachine(rom, snapshot); // Validate completely before replacing the old machine.
        ResetDelivery(false);
        _machine?.Bus.ReleaseKeys();
        _rom = rom;
        _snapshot = snapshot;
        _machine = candidate;
        _running = false;
        Volatile.Write(ref _input, new());
        Publish("Loaded; paused");
    });
    public Task SetRunningAsync(bool running) => Submit(() =>
    {
        if (running && (_machine == null || _machine.Cpu.IsFaulted)) throw new InvalidOperationException("Load or restart the machine first.");
        _running = running;
        _machine?.Bus.ReleaseKeys();
        Volatile.Write(ref _input, new());
        ResetDelivery(running);
        Anchor();
        Publish(running ? "Running" : "Paused");
    });
    public Task RestartAsync() => Submit(() =>
    {
        if (_rom == null) throw new InvalidOperationException("Load a ROM first.");
        var candidate = CreateMachine(_rom, _snapshot);
        ResetDelivery(false);
        _machine = candidate;
        _running = false;
        Volatile.Write(ref _input, new());
        Publish("Restarted; paused");
    });
    public Task SetVolumeAsync(int volume, bool muted) => Submit(() =>
    {
        if (volume is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(volume));
        _volume = volume;
        _muted = muted;
        // Clear previously queued samples so mute/volume applies immediately.
        ResetDelivery(_running, false);
        Anchor();
    });
    public async Task CloseAsync()
    {
        if (Interlocked.Exchange(ref _closeRequested, 1) == 0)
        {
            await Submit(() =>
            {
                _running = false;
                _stopping = true;
                _machine?.Bus.ReleaseKeys();
                ResetDelivery(false);
                Publish("Closed");
            }, true);
        }
        await _closed.Task;
    }
    private async Task Submit(Action action, bool closing = false)
    {
        await _slots.WaitAsync();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_commandLock)
        {
            if ((_closeRequested != 0 && !closing) || _closed.Task.IsCompleted)
            {
                _slots.Release();
                throw new ObjectDisposedException(nameof(PlayerSession));
            }
            _commands.Enqueue(new(action, completion));
            _wake.Set();
        }
        await completion.Task;
    }
    private static Spectrum48Machine CreateMachine(RomImage rom, Sna48Snapshot? snapshot)
    {
        var machine = new Spectrum48Machine(rom);
        if (snapshot != null) machine.LoadSnapshot(snapshot.CpuState, snapshot.Ram, snapshot.BorderColor);
        machine.Bus.EnableAudio();
        return machine;
    }
    private void Run()
    {
        try
        {
            while (!_stopping)
            {
                while (_commands.TryDequeue(out var command))
                {
                    try { command.Apply(); command.Completion.TrySetResult(); }
                    catch (Exception ex) { command.Completion.TrySetException(ex); }
                    finally { _slots.Release(); }
                }
                if (_stopping) break;
                if (!_running || _machine == null) { _wake.WaitOne(20); continue; }
                bool audio = !_audioFailed && _audio.Available;
                if (audio)
                {
                    int queued = _audio.QueuedSamples;
                    if (queued != _lastQueued) { _lastQueued = queued; _queueChanged = Stopwatch.GetTimestamp(); }
                    if (queued >= 1920)
                    {
                        if (Stopwatch.GetElapsedTime(_queueChanged).TotalSeconds < 2) { _wake.WaitOne(2); continue; }
                        _audioFailed = true;
                        _audioWarning = "Audio device stopped consuming; silent playback";
                        ResetDelivery(false);
                        Anchor();
                    }
                }
                else
                {
                    long target = _originTicks + (long)((_machine.Bus.TStates - _originTStates) * (double)Stopwatch.Frequency / 3500000);
                    double remaining = (target - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
                    if (remaining > 0) { _wake.WaitOne(Math.Clamp((int)Math.Ceiling(remaining), 1, 10)); continue; }
                }
                try
                {
                    var input = Volatile.Read(ref _input);
                    SpectrumInput.Apply(_machine.Bus, input.Keys, input.Joystick, input.Mode);
                    _machine.RunFrame();
                    int count = _machine.Bus.Audio!.Drain(_pcm);
                    if (!_audioFailed && _audio.Available)
                    {
                        for (int i = 0; i < count; i++) _pcm[i] = (short)(_muted ? 0 : _pcm[i] * _volume / 100);
                        try { _audio.Enqueue(_pcm, count); }
                        catch (Exception ex)
                        {
                            _audioFailed = true;
                            _audioWarning = "Audio unavailable: " + ex.Message;
                            ResetDelivery(false);
                            Anchor();
                        }
                    }
                    Publish("Running");
                }
                catch (Exception ex)
                {
                    _running = false;
                    _machine.Bus.ReleaseKeys();
                    Volatile.Write(ref _input, new());
                    ResetDelivery(false);
                    Publish("Stopped: " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _running = false;
            _stopping = true;
            try { ResetDelivery(false); } catch (Exception) { }
            Volatile.Write(ref _latest, new(new uint[320 * 240], 0, 0, 0, false, "Owner stopped: " + ex.Message, ++_version));
            lock (_commandLock)
            {
                _closeRequested = 1;
                while (_commands.TryDequeue(out var command))
                {
                    command.Completion.TrySetException(ex);
                    _slots.Release();
                }
            }
        }
        finally
        {
            lock (_commandLock)
            {
                _closeRequested = 1;
                _wake.Dispose();
                _closed.TrySetResult();
            }
        }
    }
    private void Anchor()
    {
        _originTicks = Stopwatch.GetTimestamp();
        _originTStates = _machine?.Bus.TStates ?? 0;
        _queueChanged = _originTicks;
        _lastQueued = -1;
    }
    private void ResetDelivery(bool playing, bool resetSampler = true)
    {
        if (resetSampler) _machine?.Bus.Audio?.Reset(_machine.Bus.Beeper);
        try { _audio.Reset(playing); }
        catch (Exception ex) { _audioFailed = true; _audioWarning = "Audio unavailable: " + ex.Message; }
    }
    private void Publish(string status)
    {
        uint[] pixels = new uint[320 * 240];
        if (_machine != null) Spectrum48Video.Render(_machine.Bus, pixels, _machine.FrameNumber);
        string audio = _audioWarning.Length > 0 ? _audioWarning : (!_audio.Available ? "No audio device; silent playback" : "");
        Volatile.Write(ref _latest, new(pixels, _machine?.FrameNumber ?? 0, _machine?.Bus.TStates ?? 0,
            _machine?.Cpu.Registers.PC ?? 0, _running, audio.Length == 0 ? status : status + " · " + audio, ++_version));
    }
}
