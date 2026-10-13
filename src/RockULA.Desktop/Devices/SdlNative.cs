using System.Reflection;
using System.Text;
using System.Runtime.InteropServices;

namespace RockULA.Desktop.Devices;

// SDL2 C ABI from the official headers. No SDL window or video subsystem is created.
internal static class SdlNative
{
    private const string Library = "SDL2";
    static SdlNative() => NativeLibrary.SetDllImportResolver(typeof(SdlNative).Assembly, Resolve);
    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? search)
    {
        if (name != Library) return IntPtr.Zero;
        string[] names = OperatingSystem.IsWindows() ? ["SDL2.dll"] : OperatingSystem.IsMacOS() ? ["libSDL2.dylib"] : ["libSDL2.so", "libSDL2-2.0.so", "libSDL2-2.0.so.0"];
        string rid = (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        foreach (string file in names)
        {
            if (NativeLibrary.TryLoad(Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", file), out var handle)) return handle;
            if (NativeLibrary.TryLoad(Path.Combine(AppContext.BaseDirectory, file), out handle)) return handle;
            if (NativeLibrary.TryLoad(file, assembly, search, out handle)) return handle;
        }
        return IntPtr.Zero;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioSpec
    {
        public int Frequency;
        public ushort Format;
        public byte Channels;
        public byte Silence;
        public ushort Samples;
        public ushort Padding;
        public uint Size;
        public IntPtr Callback;
        public IntPtr UserData;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct JoystickGuid
    {
        public uint A;
        public uint B;
        public uint C;
        public uint D;
    }
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern JoystickGuid SDL_JoystickGetDeviceGUID(int index);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_JoystickGetGUIDString(JoystickGuid guid, StringBuilder text, int length);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_InitSubSystem(uint flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_QuitSubSystem(uint flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr SDL_GetError();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_SetHint([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern uint SDL_OpenAudioDevice(IntPtr device, int capture, ref AudioSpec desired, out AudioSpec obtained, int changes);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_CloseAudioDevice(uint device);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_PauseAudioDevice(uint device, int pause);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_ClearQueuedAudio(uint device);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern uint SDL_GetQueuedAudioSize(uint device);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_GetAudioDeviceStatus(uint device);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_QueueAudio(uint device, short[] data, uint length);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_NumJoysticks();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr SDL_JoystickNameForIndex(int index);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_JoystickGetDeviceInstanceID(int index);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr SDL_JoystickOpen(int index);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_JoystickClose(IntPtr joystick);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_JoystickGetAttached(IntPtr joystick);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_JoystickNumAxes(IntPtr joystick);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_JoystickNumButtons(IntPtr joystick);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_JoystickNumHats(IntPtr joystick);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_JoystickUpdate();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_JoystickEventState(int state);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern short SDL_JoystickGetAxis(IntPtr joystick, int axis);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern byte SDL_JoystickGetButton(IntPtr joystick, int button);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern byte SDL_JoystickGetHat(IntPtr joystick, int hat);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_JoystickAttachVirtual(int type, int axes, int buttons, int hats);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_JoystickDetachVirtual(int index);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_JoystickSetVirtualAxis(IntPtr joystick, int axis, short value);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_JoystickSetVirtualButton(IntPtr joystick, int button, byte value);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_JoystickSetVirtualHat(IntPtr joystick, int hat, byte value);
    internal static string Error => Marshal.PtrToStringUTF8(SDL_GetError()) ?? "Unknown SDL error";
}
