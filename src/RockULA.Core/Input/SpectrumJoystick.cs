using RockULA.Core.Machines;

namespace RockULA.Core.Input;

[Flags]
public enum JoystickButtons : byte { None = 0, Right = 1, Left = 2, Down = 4, Up = 8, Fire = 16 }
public enum SpectrumJoystickMode { KeyboardOnly, Kempston, Sinclair67890, Sinclair12345 }

public static class SpectrumInput
{
    /// <summary>Replace input with the union of physical keyboard and selected joystick.</summary>
    public static void Apply(Spectrum48Bus bus, ulong keys, JoystickButtons joystick, SpectrumJoystickMode mode) =>
        throw new NotImplementedException();
}
