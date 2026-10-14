using RockULA.Core.Input;
using RockULA.Desktop.Devices;

namespace RockULA.Desktop.Preferences;

public sealed record PlayerPreferences
{
    public int Version { get; init; } = 1;
    public string? RomPath { get; init; }
    public string? SnapshotDirectory { get; init; }
    public int Volume { get; init; } = 50;
    public bool Muted { get; init; }
    public SpectrumJoystickMode Mode { get; init; } = SpectrumJoystickMode.Kempston;
    public JoystickMapping Mapping { get; init; } = new();
    public KeyboardJoystickKeys Keyboard { get; init; } = new();
    public string JoystickGuid { get; init; } = "";
    public string JoystickName { get; init; } = "";
    public JoystickDevice? FindJoystick(IEnumerable<JoystickDevice> devices)
    {
        if (JoystickGuid.Length == 0) return null;
        var matches = devices.Where(x => string.Equals(x.Guid, JoystickGuid, StringComparison.OrdinalIgnoreCase)
            && x.Name == JoystickName).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}
public sealed record PreferencesLoad(PlayerPreferences Value, string? Warning, bool CanSave = true);
