using Avalonia;

namespace RockULA.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--smoke-devices")
        {
            Environment.ExitCode = Devices.DeviceSmoke.Run();
            return;
        }
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
