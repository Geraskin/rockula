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
using RockULA.Core.Machines;
using RockULA.Core.Video;
using RockULA.Formats;

namespace RockULA.Desktop;

public sealed class PlayerWindow : Window
{
    private readonly TextBlock _status = new() { Text = "Load your 16 KiB 48K ROM, then open a 48K .sna game.", TextWrapping = TextWrapping.Wrap };
    private readonly Image _screen = new() { Stretch = Stretch.Uniform, Focusable = true };
    private readonly WriteableBitmap _bitmap = new(new PixelSize(320, 240), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
    private readonly uint[] _pixels = new uint[320 * 240];
    private readonly byte[] _bytes = new byte[320 * 240 * 4];
    private readonly HashSet<Key> _down = [];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1000.0 * 69888 / 3500000) };
    private readonly bool _smoke;
    private RomImage? _rom;
    private Sna48Snapshot? _snapshot;
    private Spectrum48Machine? _machine;
    private bool _running;
    private bool _loading;
    private bool _closed;

    // Order follows the physical eight half-rows; aliases are evaluated from the held-key set.
    private static readonly Key[] Matrix = [Key.LeftShift, Key.Z, Key.X, Key.C, Key.V,
        Key.A, Key.S, Key.D, Key.F, Key.G, Key.Q, Key.W, Key.E, Key.R, Key.T,
        Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D0, Key.D9, Key.D8, Key.D7, Key.D6,
        Key.P, Key.O, Key.I, Key.U, Key.Y, Key.Enter, Key.L, Key.K, Key.J, Key.H,
        Key.Space, Key.LeftCtrl, Key.M, Key.N, Key.B];

    public PlayerWindow(bool smoke = false)
    {
        _smoke = smoke;
        Title = "RockULA! - Rock Your Spectrum.";
        Width = 980;
        Height = 800;
        MinWidth = 640;
        MinHeight = 560;
        _screen.Source = _bitmap;
        RenderOptions.SetBitmapInterpolationMode(_screen, BitmapInterpolationMode.None);
        var toolbar = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, Margin = new Thickness(12) };
        var rom = new Button { Content = "Load ROM" };
        var snapshot = new Button { Content = "Open 48K SNA" };
        var run = new Button { Content = "Run / Pause" };
        var restart = new Button { Content = "Restart" };
        toolbar.Children.Add(rom); toolbar.Children.Add(snapshot); toolbar.Children.Add(run); toolbar.Children.Add(restart);
        rom.Click += async (_, _) => await LoadAsync(true);
        snapshot.Click += async (_, _) => await LoadAsync(false);
        run.Click += (_, _) =>
        {
            if (_loading || _machine == null) return;
            if (_running) Pause(); else Start();
        };
        restart.Click += (_, _) =>
        {
            if (_loading || _rom == null) return;
            Pause();
            var candidate = new Spectrum48Machine(_rom);
            if (_snapshot != null) candidate.LoadSnapshot(_snapshot.CpuState, _snapshot.Ram, _snapshot.BorderColor);
            _machine = candidate;
            DrawFrame();
            Start();
        };
        var footer = new StackPanel { Margin = new Thickness(12), Spacing = 6 };
        footer.Children.Add(_status);
        footer.Children.Add(new TextBlock
        {
            Text = "Letters/numbers: Spectrum keys. Shift: CAPS SHIFT. Ctrl: SYMBOL SHIFT. Arrows + Space: Kempston. No sound yet.",
            TextWrapping = TextWrapping.Wrap
        });
        var panel = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(toolbar); panel.Children.Add(footer); panel.Children.Add(_screen);
        Content = panel;
        KeyDown += (_, e) => { if (_running) { _down.Add(e.Key); UpdateInput(); e.Handled = true; } };
        KeyUp += (_, e) => { _down.Remove(e.Key); UpdateInput(); };
        Deactivated += (_, _) => ReleaseInput();
        Closed += (_, _) => { _closed = true; _timer.Stop(); ReleaseInput(); _bitmap.Dispose(); };
        _timer.Tick += (_, _) => Tick();
        Opened += (_, _) =>
        {
            if (!_smoke) return;
            _rom = RomImage.FromSpectrum48(new byte[16384]); // Explicit test-only dummy, never offered as firmware.
            _machine = new Spectrum48Machine(_rom);
            _machine.Bus.Poke(0x4000, 0x80);
            _machine.Bus.Poke(0x5800, 7);
            _status.Text = "Synthetic GUI smoke test";
            Start();
        };
    }

    private void Start()
    {
        if (_machine == null || _machine.Cpu.IsFaulted) { _status.Text = "Restart after a CPU fault."; return; }
        _running = true;
        _timer.Start();
        _screen.Focus();
    }
    private void Pause()
    {
        _running = false;
        _timer.Stop();
        ReleaseInput();
        _status.Text = "Paused";
    }
    private void ReleaseInput()
    {
        _down.Clear();
        _machine?.Bus.ReleaseKeys();
    }
    private void UpdateInput()
    {
        if (_machine == null) return;
        for (int index = 0; index < Matrix.Length; index++)
        {
            bool pressed = _down.Contains(Matrix[index]);
            if (index == 0) pressed |= _down.Contains(Key.RightShift);
            if (index == 36) pressed |= _down.Contains(Key.RightCtrl);
            _machine.Bus.SetKey((SpectrumKey)index, pressed);
        }
        _machine.Bus.Joystick = (byte)((_down.Contains(Key.Right) ? 1 : 0) | (_down.Contains(Key.Left) ? 2 : 0)
            | (_down.Contains(Key.Down) ? 4 : 0) | (_down.Contains(Key.Up) ? 8 : 0) | (_down.Contains(Key.Space) ? 16 : 0));
    }
    private void Tick()
    {
        if (!_running || _machine == null) return;
        try
        {
            _machine.RunFrame();
            DrawFrame();
            _status.Text = $"Running · frame {_machine.FrameNumber} · PC {_machine.Cpu.Registers.PC:X4}";
            if (_smoke && _machine.FrameNumber >= 3)
            {
                if (_pixels[24 * 320 + 32] != 0xFFCDCDCD) throw new InvalidOperationException("GUI smoke pixel mismatch.");
                using (var frame = _bitmap.Lock())
                    if (unchecked((uint)Marshal.ReadInt32(frame.Address, 24 * frame.RowBytes + 32 * 4)) != 0xFFCDCDCD)
                        throw new InvalidOperationException("GUI smoke bitmap upload mismatch.");
                Console.WriteLine("GUI smoke passed: native window, 3 machine frames and bitmap upload.");
                Close();
            }
        }
        catch (Exception ex)
        {
            Pause();
            _status.Text = $"Stopped: {ex.Message}";
            if (_smoke) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; Close(); }
        }
    }
    private void DrawFrame()
    {
        if (_machine == null) return;
        Spectrum48Video.Render(_machine.Bus, _pixels, _machine.FrameNumber);
        Buffer.BlockCopy(_pixels, 0, _bytes, 0, _bytes.Length);
        using (var frame = _bitmap.Lock())
            for (int row = 0; row < 240; row++)
                Marshal.Copy(_bytes, row * 320 * 4, IntPtr.Add(frame.Address, row * frame.RowBytes), 320 * 4);
        _screen.InvalidateVisual();
    }
    private async Task LoadAsync(bool firmware)
    {
        if (_loading) return;
        if (!firmware && _rom == null) { _status.Text = "Load a 48K ROM first. SNA files do not contain firmware."; return; }
        _loading = true;
        Pause();
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = firmware ? "Select 48K firmware (16384 bytes)" : "Select a 48K SNA snapshot",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType(firmware ? "48K firmware" : "48K SNA") { Patterns = firmware ? ["*.rom", "*.bin"] : ["*.sna"] }]
            });
            if (_closed || files.Count == 0) return;
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
            if (_closed) return;
            if (count > maximum) throw new ArgumentException("File is too large for the selected 48K format.");
            if (firmware)
            {
                var selected = RomImage.FromSpectrum48(bytes.AsSpan(0, count));
                var candidate = new Spectrum48Machine(selected);
                _rom = selected;
                _snapshot = null;
                _machine = candidate;
                DrawFrame();
                _status.Text = "ROM loaded. Open a 48K SNA, or press Run to boot.";
            }
            else
            {
                var selected = Sna48Snapshot.Parse(bytes.AsSpan(0, count));
                var candidate = new Spectrum48Machine(_rom!);
                candidate.LoadSnapshot(selected.CpuState, selected.Ram, selected.BorderColor);
                _snapshot = selected;
                _machine = candidate;
                DrawFrame();
                Start();
            }
        }
        catch (Exception ex) { if (!_closed) _status.Text = $"Cannot load: {ex.Message}"; }
        finally { _loading = false; }
    }
}
