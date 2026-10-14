namespace RockULA.Core.Cpu;

[Flags]
public enum Z80Flags : byte
{
    None = 0,
    Carry = 0x01,
    Subtract = 0x02,
    ParityOverflow = 0x04,
    Undocumented3 = 0x08,
    HalfCarry = 0x10,
    Undocumented5 = 0x20,
    Zero = 0x40,
    Sign = 0x80
}
