using RockULA.Core.Input;

namespace RockULA.Desktop.Devices;

public sealed record JoystickMapping(int XAxis = 0, int YAxis = 1, int FireButton = 0, int Hat = 0,
    int DeadZone = 8000, bool InvertX = false, bool InvertY = false,
    int LeftButton = -1, int RightButton = -1, int UpButton = -1, int DownButton = -1)
{
    public JoystickButtons MapDirectionButtons(ulong low, ulong high)
    {
        JoystickButtons state = JoystickButtons.None;
        if (Pressed(LeftButton, low, high)) state |= JoystickButtons.Left;
        if (Pressed(RightButton, low, high)) state |= JoystickButtons.Right;
        if (Pressed(UpButton, low, high)) state |= JoystickButtons.Up;
        if (Pressed(DownButton, low, high)) state |= JoystickButtons.Down;
        return state;
    }
    public static bool Pressed(int button, ulong low, ulong high) => button is >= 0 and < 128
        && ((button < 64 ? low : high) & (1UL << (button % 64))) != 0;
    public JoystickButtons Map(short x, short y, byte hat, bool fire)
    {
        if (DeadZone is < 1 or > 32767) throw new ArgumentOutOfRangeException(nameof(DeadZone));
        int horizontal = InvertX ? -(int)x : x;
        int vertical = InvertY ? -(int)y : y;
        JoystickButtons state = JoystickButtons.None;
        if (horizontal >= DeadZone || (hat & 2) != 0) state |= JoystickButtons.Right;
        if (horizontal <= -DeadZone || (hat & 8) != 0) state |= JoystickButtons.Left;
        if (vertical >= DeadZone || (hat & 4) != 0) state |= JoystickButtons.Down;
        if (vertical <= -DeadZone || (hat & 1) != 0) state |= JoystickButtons.Up;
        if (fire) state |= JoystickButtons.Fire;
        return state;
    }
}
public sealed record JoystickDevice(int InstanceId, string Name, string Guid = "")
{
    public override string ToString() => Name;
}
public sealed record JoystickReading(JoystickButtons Buttons, string Status);
