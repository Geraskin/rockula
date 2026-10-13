using System.Diagnostics;
using RockULA.Core.Input;
using RockULA.Formats;

namespace RockULA.Desktop.Devices;

internal static class DeviceSmoke
{
    public static int Run()
    {
        int index = -1;
        IntPtr virtualJoystick = IntPtr.Zero;
        using var devices = new SdlDevices();
        try
        {
            if (!devices.Available || !devices.JoysticksAvailable) throw new InvalidOperationException(devices.Status);
            devices.Reset(false);
            short[] pcm = [1000, -1000, 1000, -1000];
            devices.Enqueue(pcm, pcm.Length);
            if (devices.QueuedSamples != 4) throw new InvalidOperationException("Native audio queue length mismatch");
            devices.Reset(false);
            if (devices.QueuedSamples != 0) throw new InvalidOperationException("Native audio clear mismatch");
            devices.Enqueue(new short[4800], 4800);
            SdlNative.SDL_PauseAudioDevice(GetAudioForSmoke(devices), 0);
            var watch = Stopwatch.StartNew();
            while (devices.QueuedSamples >= 4800 && watch.ElapsedMilliseconds < 1500) Thread.Sleep(5);
            if (devices.QueuedSamples >= 4800) throw new InvalidOperationException("Dummy audio did not consume PCM");
            devices.Reset(false);
            index = SdlNative.SDL_JoystickAttachVirtual(1, 2, 2, 1);
            if (index < 0) throw new InvalidOperationException(SdlNative.Error);
            virtualJoystick = SdlNative.SDL_JoystickOpen(index);
            int instance = SdlNative.SDL_JoystickGetDeviceInstanceID(index);
            var descriptor = devices.Enumerate().Single(x => x.InstanceId == instance);
            if (descriptor.Guid.Length != 32 || !descriptor.Guid.All(Uri.IsHexDigit)) throw new InvalidOperationException("Native joystick GUID mismatch");
            devices.Select(instance);
            Set(SdlNative.SDL_JoystickSetVirtualAxis(virtualJoystick, 0, 20000));
            Set(SdlNative.SDL_JoystickSetVirtualAxis(virtualJoystick, 1, -20000));
            Set(SdlNative.SDL_JoystickSetVirtualButton(virtualJoystick, 0, 1));
            Set(SdlNative.SDL_JoystickSetVirtualHat(virtualJoystick, 0, 8));
            if ((byte)devices.Poll(new()).Buttons != 27) throw new InvalidOperationException("Native axis/hat/fire mapping mismatch");
            if ((byte)devices.Poll(new(XAxis: 1, YAxis: 0, FireButton: 1, Hat: -1)).Buttons != 6)
                throw new InvalidOperationException("Native remapping mismatch");
            Set(SdlNative.SDL_JoystickSetVirtualButton(virtualJoystick, 1, 1));
            if ((byte)devices.Poll(new(XAxis: -1, YAxis: -1, FireButton: -1, Hat: -1, LeftButton: 0, UpButton: 1)).Buttons != 10)
                throw new InvalidOperationException("Native digital direction-button mapping mismatch");
            Set(SdlNative.SDL_JoystickDetachVirtual(index));
            index = -1;
            if (devices.Poll(new()).Buttons != JoystickButtons.None) throw new InvalidOperationException("Unplug left stuck input");
            Console.WriteLine("Device smoke passed: native queued/consumed/cleared PCM, virtual game HID axes/hat/button/remapping/unplug.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            devices.Select(-1);
            if (virtualJoystick != IntPtr.Zero) SdlNative.SDL_JoystickClose(virtualJoystick);
            if (index >= 0) SdlNative.SDL_JoystickDetachVirtual(index);
        }
    }
    private static void Set(int result) { if (result != 0) throw new InvalidOperationException(SdlNative.Error); }
    private static uint GetAudioForSmoke(SdlDevices devices) => devices.AudioDeviceForSmoke;
    internal static ISpectrum48Snapshot Z80Snapshot()
    {
        var source = Snapshot();
        byte[] bytes = new byte[49182];
        bytes[7] = 0x80;
        bytes[9] = 0x90;
        bytes[29] = 1;
        source.Ram.CopyTo(bytes.AsSpan(30));
        return SpectrumSnapshotReader.Parse(".z80", bytes);
    }
    internal static Sna48Snapshot Snapshot()
    {
        byte[] sna = new byte[Sna48Snapshot.FileSize];
        sna[24] = 0x90; sna[25] = 1; sna[27 + 0x5001] = 0x80;
        byte[] code = [0x3E, 7, 0x32, 0, 0x58, 0x3E, 0x80, 0x32, 0, 0x40,
            0x3E, 0x10, 0xD3, 0xFE, 0xEE, 0x10, 0x18, 0xFA];
        code.CopyTo(sna, 27 + 0x4000);
        return Sna48Snapshot.Parse(sna);
    }
}
