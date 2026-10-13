using System.Runtime.InteropServices;
using System.Text;
using RockULA.Core.Input;
using RockULA.Desktop.Playback;

namespace RockULA.Desktop.Devices;

/// <summary>UI-owned joystick handles; audio queue calls are serialized across owner/UI.</summary>
public sealed class SdlDevices : IAudioOutput, IDisposable
{
    private const uint AudioFlag = 0x10;
    private const uint JoystickFlag = 0x200;
    private readonly object _audioLock = new();
    private uint _audio;
    private long _queuedTotal;
    internal long TotalQueuedSamples => Interlocked.Read(ref _queuedTotal);
    private uint _initialized;
    private IntPtr _joystick;
    public string Status { get; private set; } = "SDL unavailable";
    internal uint AudioDeviceForSmoke => _audio;
    public bool JoysticksAvailable => (_initialized & JoystickFlag) != 0;
    public SdlDevices()
    {
        try
        {
            SdlNative.SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
            string joystickStatus = "USB joystick support ready";
            if (SdlNative.SDL_InitSubSystem(JoystickFlag) == 0)
            {
                _initialized |= JoystickFlag;
                SdlNative.SDL_JoystickEventState(0); // We poll, rather than accumulating an unconsumed SDL event queue.
            }
            else joystickStatus = "USB input unavailable: " + SdlNative.Error;
            if (SdlNative.SDL_InitSubSystem(AudioFlag) == 0)
            {
                _initialized |= AudioFlag;
                var desired = new SdlNative.AudioSpec { Frequency = 48000, Format = 0x8010, Channels = 1, Samples = 512 };
                _audio = SdlNative.SDL_OpenAudioDevice(IntPtr.Zero, 0, ref desired, out var obtained, 0);
                if (_audio != 0 && (obtained.Frequency != 48000 || obtained.Format != 0x8010 || obtained.Channels != 1))
                {
                    SdlNative.SDL_CloseAudioDevice(_audio);
                    _audio = 0;
                }
            }
            Status = (_audio == 0 ? "Silent playback: " + SdlNative.Error : "Beeper audio ready (48 kHz)") + " · " + joystickStatus;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Status = "Silent keyboard playback: " + ex.Message;
        }
    }
    public bool Available
    {
        get { lock (_audioLock) return _audio != 0 && SdlNative.SDL_GetAudioDeviceStatus(_audio) != 0; }
    }
    public int QueuedSamples
    {
        get { lock (_audioLock) return _audio == 0 ? 0 : (int)(SdlNative.SDL_GetQueuedAudioSize(_audio) / 2); }
    }
    public void Enqueue(short[] samples, int count)
    {
        if (count < 0 || count > samples.Length) throw new ArgumentOutOfRangeException(nameof(count));
        lock (_audioLock)
        {
            if (_audio == 0) return;
            if (SdlNative.SDL_QueueAudio(_audio, samples, checked((uint)count * 2)) != 0)
                throw new InvalidOperationException("Audio queue failed: " + SdlNative.Error);
            Interlocked.Add(ref _queuedTotal, count);
        }
    }
    public void Reset(bool playing)
    {
        lock (_audioLock)
        {
            if (_audio == 0) return;
            SdlNative.SDL_PauseAudioDevice(_audio, 1);
            SdlNative.SDL_ClearQueuedAudio(_audio);
            if (playing) SdlNative.SDL_PauseAudioDevice(_audio, 0);
        }
    }
    public IReadOnlyList<JoystickDevice> Enumerate()
    {
        var result = new List<JoystickDevice>();
        if (!JoysticksAvailable) return result;
        SdlNative.SDL_JoystickUpdate();
        for (int index = 0; index < SdlNative.SDL_NumJoysticks(); index++)
        {
            var guid = new StringBuilder(33);
            SdlNative.SDL_JoystickGetGUIDString(SdlNative.SDL_JoystickGetDeviceGUID(index), guid, 33);
            result.Add(new(SdlNative.SDL_JoystickGetDeviceInstanceID(index),
                Marshal.PtrToStringUTF8(SdlNative.SDL_JoystickNameForIndex(index)) ?? $"Joystick {index + 1}", guid.ToString()));
        }
        return result;
    }
    public void Select(int instanceId)
    {
        if (_joystick != IntPtr.Zero) { SdlNative.SDL_JoystickClose(_joystick); _joystick = IntPtr.Zero; }
        if (!JoysticksAvailable || instanceId < 0) return;
        SdlNative.SDL_JoystickUpdate();
        for (int index = 0; index < SdlNative.SDL_NumJoysticks(); index++)
            if (SdlNative.SDL_JoystickGetDeviceInstanceID(index) == instanceId)
            {
                _joystick = SdlNative.SDL_JoystickOpen(index);
                if (_joystick == IntPtr.Zero) throw new InvalidOperationException("Cannot open joystick: " + SdlNative.Error);
                return;
            }
        throw new InvalidOperationException("Joystick disconnected; refresh the device list.");
    }
    public JoystickReading Poll(JoystickMapping mapping)
    {
        if (_joystick == IntPtr.Zero) return new(JoystickButtons.None, "No USB joystick selected");
        SdlNative.SDL_JoystickUpdate();
        if (SdlNative.SDL_JoystickGetAttached(_joystick) == 0)
        {
            SdlNative.SDL_JoystickClose(_joystick);
            _joystick = IntPtr.Zero;
            return new(JoystickButtons.None, "USB joystick disconnected — refresh and select to reconnect");
        }
        int axes = SdlNative.SDL_JoystickNumAxes(_joystick);
        int buttons = SdlNative.SDL_JoystickNumButtons(_joystick);
        int hats = SdlNative.SDL_JoystickNumHats(_joystick);
        short x = mapping.XAxis >= 0 && mapping.XAxis < axes ? SdlNative.SDL_JoystickGetAxis(_joystick, mapping.XAxis) : (short)0;
        short y = mapping.YAxis >= 0 && mapping.YAxis < axes ? SdlNative.SDL_JoystickGetAxis(_joystick, mapping.YAxis) : (short)0;
        byte hat = mapping.Hat >= 0 && mapping.Hat < hats ? SdlNative.SDL_JoystickGetHat(_joystick, mapping.Hat) : (byte)0;
        ulong low = 0, high = 0;
        var pressed = new List<int>();
        for (int button = 0; button < Math.Min(buttons, 128); button++)
            if (SdlNative.SDL_JoystickGetButton(_joystick, button) != 0)
            {
                if (button < 64) low |= 1UL << button; else high |= 1UL << (button - 64);
                pressed.Add(button);
            }
        bool fire = JoystickMapping.Pressed(mapping.FireButton, low, high);
        var state = mapping.Map(x, y, hat, fire) | mapping.MapDirectionButtons(low, high);
        return new(state, $"USB: axes={axes}, buttons={buttons}, hats={hats} · X={x} Y={y} hat={hat} · pressed=[{string.Join(",", pressed)}] · {state}");
    }
    public void Dispose()
    {
        if (_joystick != IntPtr.Zero) { SdlNative.SDL_JoystickClose(_joystick); _joystick = IntPtr.Zero; }
        lock (_audioLock)
        {
            if (_audio != 0) { SdlNative.SDL_CloseAudioDevice(_audio); _audio = 0; }
        }
        if (_initialized != 0) { SdlNative.SDL_QuitSubSystem(_initialized); _initialized = 0; }
    }
}
