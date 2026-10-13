using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using RockULA.Core;
using RockULA.Core.Input;
using RockULA.Desktop.Devices;
using RockULA.Desktop.Playback;
using RockULA.Formats;

namespace RockULA.Desktop;

public sealed class PlayerWindow : Window
{
    private readonly TextBlock _status = new() { Text = "Load a 16 KiB 48K ROM, then open a 48K SNA.", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _deviceStatus = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Image _screen = new() { Stretch = Stretch.Uniform, Focusable = true };
    private readonly WriteableBitmap _bitmap = new(new PixelSize(320, 240), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
    private readonly byte[] _bytes = new byte[320 * 240 * 4];
    private readonly HashSet<Key> _down = [];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly SdlDevices _devices = new();
    private readonly PlayerSession _session;
    private readonly bool _smoke;
    private RomImage? _rom;
    private JoystickMapping _mapping = new();
    private KeyboardJoystickKeys _keyboard = new();
    private SpectrumJoystickMode _mode = SpectrumJoystickMode.Kempston;
    private int _selectedInstance = -1;
    private long _drawnVersion = -1;
    private bool _busy;
    private bool _closing;
    private bool _allowClose;
    private string? _notice;

    private static readonly Key[] Matrix = [Key.LeftShift, Key.Z, Key.X, Key.C, Key.V,
        Key.A, Key.S, Key.D, Key.F, Key.G, Key.Q, Key.W, Key.E, Key.R, Key.T,
        Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D0, Key.D9, Key.D8, Key.D7, Key.D6,
        Key.P, Key.O, Key.I, Key.U, Key.Y, Key.Enter, Key.L, Key.K, Key.J, Key.H,
        Key.Space, Key.LeftCtrl, Key.M, Key.N, Key.B];

    public PlayerWindow(bool smoke = false)
    {
        _smoke = smoke;
        _session = new PlayerSession(_devices);
        Title = "RockULA! — Rock Your Spectrum.";
        Width = 980; Height = 820; MinWidth = 640; MinHeight = 560;
        _screen.Source = _bitmap;
        RenderOptions.SetBitmapInterpolationMode(_screen, BitmapInterpolationMode.None);
        var toolbar = new WrapPanel { Margin = new Thickness(12) };
        Button Add(string name, Func<Task> action)
        {
            var button = new Button { Content = name, Margin = new Thickness(4) };
            button.Click += async (_, _) => await Perform(action);
            toolbar.Children.Add(button);
            return button;
        }
        Add("Load ROM", () => LoadAsync(true));
        Add("Open 48K SNA", () => LoadAsync(false));
        Add("Run / Pause", async () =>
        {
            ReleaseInput();
            await _session.SetRunningAsync(!_session.Latest.Running);
            _screen.Focus();
        });
        Add("Restart", async () => { ReleaseInput(); await _session.RestartAsync(); await _session.SetRunningAsync(true); _screen.Focus(); });
        Add("Controls / USB", ConfigureControls);
        var volume = new Slider { Minimum = 0, Maximum = 100, Value = 50, Width = 100, Margin = new Thickness(8) };
        var mute = new CheckBox { Content = "Mute", Margin = new Thickness(8), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        volume.ValueChanged += async (_, _) => { if (!_closing) await ChangeVolume((int)volume.Value, mute.IsChecked == true); };
        mute.IsCheckedChanged += async (_, _) => { if (!_closing) await ChangeVolume((int)volume.Value, mute.IsChecked == true); };
        toolbar.Children.Add(new TextBlock { Text = "Volume", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        toolbar.Children.Add(volume); toolbar.Children.Add(mute);
        var footer = new StackPanel { Margin = new Thickness(12), Spacing = 6 };
        footer.Children.Add(_status); footer.Children.Add(_deviceStatus);
        footer.Children.Add(new TextBlock { Text = "Spectrum: letters/numbers, Shift=CAPS, Ctrl=SYMBOL. Default joystick: arrows + Space. Configure USB or Sinclair in Controls.", TextWrapping = TextWrapping.Wrap });
        var panel = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top); DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(toolbar); panel.Children.Add(footer); panel.Children.Add(_screen);
        Content = panel;
        KeyDown += (_, e) => { if (_session.Latest.Running && !_busy) { _down.Add(e.Key); e.Handled = true; } };
        KeyUp += (_, e) => _down.Remove(e.Key);
        Deactivated += (_, _) => ReleaseInput();
        Closing += async (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            if (_closing) return;
            _closing = true;
            _timer.Stop();
            ReleaseInput();
            await _session.CloseAsync();
            _devices.Dispose();
            _allowClose = true;
            Close();
        };
        Closed += (_, _) => _bitmap.Dispose();
        _timer.Tick += (_, _) => Tick();
        Opened += async (_, _) =>
        {
            _timer.Start();
            if (!_smoke) return;
            await Perform(async () =>
            {
                _rom = RomImage.FromSpectrum48(new byte[16384]);
                await _session.LoadAsync(_rom, DeviceSmoke.Snapshot());
                await _session.SetRunningAsync(true);
            });
        };
    }
    private async Task ChangeVolume(int volume, bool muted)
    {
        try { await _session.SetVolumeAsync(volume, muted); }
        catch (Exception ex) { if (!_closing) _notice = ex.Message; }
    }
    private async Task Perform(Func<Task> action)
    {
        if (_busy || _closing) return;
        _busy = true;
        _notice = null;
        try { await action(); }
        catch (Exception ex)
        {
            if (!_closing) _notice = ex.Message;
            if (_smoke) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; Close(); }
        }
        finally { _busy = false; }
    }
    private void ReleaseInput()
    {
        _down.Clear();
        _session.SetInput(new(Mode: _mode));
    }
    private void Tick()
    {
        var reading = _devices.Poll(_mapping);
        _deviceStatus.Text = _devices.Status + " · " + reading.Status;
        var frame = _session.Latest;
        if (frame.Running && !_busy && IsActive)
        {
            ulong keys = 0;
            for (int index = 0; index < Matrix.Length; index++)
                if (_down.Contains(Matrix[index]) || (index == 0 && _down.Contains(Key.RightShift)) || (index == 36 && _down.Contains(Key.RightCtrl)))
                    keys |= 1UL << index;
            _session.SetInput(new(keys, _keyboard.Read(_down) | reading.Buttons, _mode));
        }
        else _session.SetInput(new(Mode: _mode));
        if (frame.Version != _drawnVersion)
        {
            Buffer.BlockCopy(frame.Pixels, 0, _bytes, 0, _bytes.Length);
            using (var bitmap = _bitmap.Lock())
                for (int row = 0; row < 240; row++)
                    Marshal.Copy(_bytes, row * 320 * 4, IntPtr.Add(bitmap.Address, row * bitmap.RowBytes), 320 * 4);
            _screen.InvalidateVisual();
            _drawnVersion = frame.Version;
        }
        _status.Text = _notice ?? $"{frame.Status} · frame {frame.Number} · PC {frame.PC:X4}";
        if (_smoke && frame.Number >= 3)
        {
            try
            {
                if (frame.Pixels[24 * 320 + 32] != 0xFFCDCDCD) throw new InvalidOperationException("Worker GUI pixel mismatch");
                using (var bitmap = _bitmap.Lock())
                    if (unchecked((uint)Marshal.ReadInt32(bitmap.Address, 24 * bitmap.RowBytes + 32 * 4)) != 0xFFCDCDCD)
                        throw new InvalidOperationException("Worker GUI bitmap upload mismatch");
                if (!_devices.Available) throw new InvalidOperationException("GUI smoke requires the dummy audio backend");
                Console.WriteLine("GUI smoke passed: worker frames, native window, bitmap upload and initialized audio.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            Close();
        }
        else if (_smoke && frame.Status.StartsWith("Stopped:")) { Console.Error.WriteLine(frame.Status); Environment.ExitCode = 1; Close(); }
    }
    private async Task ConfigureControls()
    {
        bool resume = _session.Latest.Running;
        ReleaseInput();
        await _session.SetRunningAsync(false);
        var controls = new ControlsWindow(_devices, _mapping, _keyboard, _mode, _selectedInstance);
        await controls.ShowDialog(this);
        if (_closing) return;
        _mapping = controls.Mapping;
        _keyboard = controls.Keyboard;
        _mode = controls.Mode;
        _selectedInstance = controls.SelectedInstance;
        ReleaseInput();
        if (resume) await _session.SetRunningAsync(true);
        _screen.Focus();
    }
    private async Task LoadAsync(bool firmware)
    {
        if (!firmware && _rom == null) throw new InvalidOperationException("Load a 48K ROM first; SNA has no firmware.");
        ReleaseInput();
        await _session.SetRunningAsync(false);
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = firmware ? "48K firmware (16384 bytes)" : "48K SNA snapshot (49179 bytes)", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(firmware ? "Firmware" : "48K SNA") { Patterns = firmware ? ["*.rom", "*.bin"] : ["*.sna"] }]
        });
        if (_closing || files.Count == 0) return;
        await using var input = await files[0].OpenReadAsync();
        int maximum = firmware ? 16384 : Sna48Snapshot.FileSize;
        byte[] bytes = new byte[maximum + 1];
        int count = 0;
        while (count <= maximum)
        {
            int read = await input.ReadAsync(bytes.AsMemory(count, bytes.Length - count));
            if (read == 0) break;
            count += read;
        }
        if (_closing) return;
        if (count > maximum) throw new ArgumentException("File too large for selected 48K format.");
        if (firmware)
        {
            var selected = RomImage.FromSpectrum48(bytes.AsSpan(0, count));
            await _session.LoadAsync(selected);
            _rom = selected;
            _notice = "ROM loaded. Open a 48K SNA, or press Run to boot.";
        }
        else
        {
            var snapshot = Sna48Snapshot.Parse(bytes.AsSpan(0, count));
            await _session.LoadAsync(_rom!, snapshot);
            await _session.SetRunningAsync(true);
            _screen.Focus();
        }
    }
}
