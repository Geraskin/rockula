using RockULA.Core.Input;

namespace RockULA.Desktop.Devices;

public sealed record JoystickMapping(int XAxis = 0, int YAxis = 1, int FireButton = 0, int Hat = 0,
    int DeadZone = 8000, bool InvertX = false, bool InvertY = false)
{
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
public sealed record JoystickDevice(int InstanceId, string Name)
{
    public override string ToString() => Name;
}
public sealed record JoystickReading(JoystickButtons Buttons, string Status);
