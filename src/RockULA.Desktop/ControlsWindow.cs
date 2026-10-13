using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using RockULA.Core.Input;
using RockULA.Desktop.Devices;

namespace RockULA.Desktop;

public sealed record KeyboardJoystickKeys(Key Up = Key.Up, Key Down = Key.Down,
    Key Left = Key.Left, Key Right = Key.Right, Key Fire = Key.Space)
{
    public JoystickButtons Read(HashSet<Key> held) =>
        (held.Contains(Right) ? JoystickButtons.Right : 0) | (held.Contains(Left) ? JoystickButtons.Left : 0)
        | (held.Contains(Down) ? JoystickButtons.Down : 0) | (held.Contains(Up) ? JoystickButtons.Up : 0)
        | (held.Contains(Fire) ? JoystickButtons.Fire : 0);
}

public sealed class ControlsWindow : Window
{
    private readonly SdlDevices _devices;
    private readonly ComboBox _device = new() { MinWidth = 420 };
    private readonly TextBlock _live = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private readonly Button[] _keyButtons = new Button[5];
    private int _capture = -1;
    public JoystickMapping Mapping { get; private set; }
    public KeyboardJoystickKeys Keyboard { get; private set; }
    public SpectrumJoystickMode Mode { get; private set; }
    public int SelectedInstance { get; private set; }

    public ControlsWindow(SdlDevices devices, JoystickMapping mapping, KeyboardJoystickKeys keyboard,
        SpectrumJoystickMode mode, int selectedInstance)
    {
        _devices = devices;
        Mapping = mapping;
        Keyboard = keyboard;
        Mode = mode;
        SelectedInstance = selectedInstance;
        Title = "RockULA! — Controls";
        Width = 620;
        Height = 760;
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = "USB game joystick / gamepad", FontSize = 20 });
        panel.Children.Add(_device);
        var refresh = new Button { Content = "Refresh connected devices" };
        refresh.Click += (_, _) => Refresh();
        panel.Children.Add(refresh);
        _device.SelectionChanged += (_, _) =>
        {
            try
            {
                SelectedInstance = (_device.SelectedItem as JoystickDevice)?.InstanceId ?? -1;
                _devices.Select(SelectedInstance);
            }
            catch (Exception ex) { _live.Text = ex.Message; SelectedInstance = -1; }
        };
        Refresh();
        var modes = new ComboBox { ItemsSource = new[] { "Keyboard only", "Kempston", "Sinclair: keys 6–0", "Sinclair: keys 1–5" }, SelectedIndex = (int)mode };
        modes.SelectionChanged += (_, _) => { if (modes.SelectedIndex >= 0) Mode = (SpectrumJoystickMode)modes.SelectedIndex; };
        panel.Children.Add(new TextBlock { Text = "Emulated interface (choose the same in your game)" });
        panel.Children.Add(modes);
        panel.Children.Add(new TextBlock { Text = "Indices start at 0. Use -1 to disable an axis/hat/button." });
        AddNumber(panel, "Horizontal axis", mapping.XAxis, -1, 63, value => Mapping = Mapping with { XAxis = value });
        AddNumber(panel, "Vertical axis", mapping.YAxis, -1, 63, value => Mapping = Mapping with { YAxis = value });
        AddNumber(panel, "D-pad / hat", mapping.Hat, -1, 63, value => Mapping = Mapping with { Hat = value });
        AddNumber(panel, "Fire button", mapping.FireButton, -1, 127, value => Mapping = Mapping with { FireButton = value });
        AddNumber(panel, "Dead zone (%)", (int)Math.Round(mapping.DeadZone * 100.0 / 32767), 1, 80,
            value => Mapping = Mapping with { DeadZone = (int)Math.Round(value * 32767.0 / 100) });
        var invertX = new CheckBox { Content = "Invert horizontal axis", IsChecked = mapping.InvertX };
        var invertY = new CheckBox { Content = "Invert vertical axis", IsChecked = mapping.InvertY };
        invertX.IsCheckedChanged += (_, _) => Mapping = Mapping with { InvertX = invertX.IsChecked == true };
        invertY.IsCheckedChanged += (_, _) => Mapping = Mapping with { InvertY = invertY.IsChecked == true };
        panel.Children.Add(invertX); panel.Children.Add(invertY);
        panel.Children.Add(new TextBlock { Text = "Keyboard joystick aliases — click, then press a key" });
        string[] names = ["Up", "Down", "Left", "Right", "Fire"];
        Key[] keys = [keyboard.Up, keyboard.Down, keyboard.Left, keyboard.Right, keyboard.Fire];
        var row = new WrapPanel();
        for (int index = 0; index < 5; index++)
        {
            int slot = index;
            var button = new Button { Content = $"{names[index]}: {keys[index]}", Margin = new Thickness(3) };
            _keyButtons[index] = button;
            button.Click += (_, _) => { _capture = slot; button.Content = "Press a key (Esc cancels)"; Focus(); };
            row.Children.Add(button);
        }
        panel.Children.Add(row);
        KeyDown += (_, e) =>
        {
            if (_capture < 0) return;
            int slot = _capture;
            if (e.Key != Key.Escape)
                Keyboard = slot switch
                {
                    0 => Keyboard with { Up = e.Key }, 1 => Keyboard with { Down = e.Key },
                    2 => Keyboard with { Left = e.Key }, 3 => Keyboard with { Right = e.Key },
                    _ => Keyboard with { Fire = e.Key }
                };
            Key[] current = [Keyboard.Up, Keyboard.Down, Keyboard.Left, Keyboard.Right, Keyboard.Fire];
            _keyButtons[slot].Content = $"{names[slot]}: {current[slot]}";
            _capture = -1;
            e.Handled = true;
        };
        panel.Children.Add(_live);
        panel.Children.Add(new TextBlock { Text = "Axes and hat are combined. Unplugging releases all USB input. Refresh and select after reconnecting. Settings last for this session.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var done = new Button { Content = "Done", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        done.Click += (_, _) => Close();
        panel.Children.Add(done);
        Content = new ScrollViewer { Content = panel };
        _timer.Tick += (_, _) => _live.Text = _devices.Poll(Mapping).Status;
        Opened += (_, _) => _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }
    private void Refresh()
    {
        int selected = SelectedInstance;
        var list = new List<JoystickDevice> { new(-1, "None — keyboard only") };
        list.AddRange(_devices.Enumerate());
        _device.ItemsSource = list;
        _device.SelectedItem = list.FirstOrDefault(x => x.InstanceId == selected) ?? list[0];
    }
    private static void AddNumber(StackPanel panel, string label, int initial, int minimum, int maximum, Action<int> changed)
    {
        var row = new DockPanel();
        var number = new NumericUpDown { Minimum = minimum, Maximum = maximum, Value = initial, Increment = 1, Width = 120 };
        number.ValueChanged += (_, _) => { if (number.Value.HasValue) changed((int)number.Value.Value); };
        DockPanel.SetDock(number, Dock.Right);
        row.Children.Add(number);
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        panel.Children.Add(row);
    }
}
