using RockULA.Core.Machines;

namespace RockULA.Core.Input;

[Flags]
public enum JoystickButtons : byte { None = 0, Right = 1, Left = 2, Down = 4, Up = 8, Fire = 16 }
public enum SpectrumJoystickMode { KeyboardOnly, Kempston, Sinclair67890, Sinclair12345 }

public static class SpectrumInput
{
    /// <summary>Replace input with the union of physical keyboard and selected joystick.</summary>
    public static void Apply(Spectrum48Bus bus, ulong keys, JoystickButtons joystick, SpectrumJoystickMode mode)
    {
        ArgumentNullException.ThrowIfNull(bus);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        joystick &= (JoystickButtons)31;
        if (mode is SpectrumJoystickMode.Sinclair67890 or SpectrumJoystickMode.Sinclair12345)
        {
            SpectrumKey[] mapping = mode == SpectrumJoystickMode.Sinclair67890 ? SinclairRight : SinclairLeft;
            for (int bit = 0; bit < 5; bit++)
                if (((int)joystick & (1 << bit)) != 0) keys |= 1UL << (int)mapping[bit];
        }
        for (int key = 0; key < 40; key++) bus.SetKey((SpectrumKey)key, (keys & (1UL << key)) != 0);
        bus.Joystick = mode == SpectrumJoystickMode.Kempston ? (byte)joystick : (byte)0;
    }
    // Right, left, down, up, fire; explicitly labelled by keys to avoid inconsistent port numbering.
    private static readonly SpectrumKey[] SinclairRight = [SpectrumKey.D7, SpectrumKey.D6, SpectrumKey.D8, SpectrumKey.D9, SpectrumKey.D0];
    private static readonly SpectrumKey[] SinclairLeft = [SpectrumKey.D2, SpectrumKey.D1, SpectrumKey.D3, SpectrumKey.D4, SpectrumKey.D5];
}
