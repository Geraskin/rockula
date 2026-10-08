namespace RockULA.Core.Cpu;

/// <summary>Mutable register file confined to the owner of a CPU.</summary>
public sealed class Z80Registers
{
    public byte A { get; set; }
    public byte F { get; set; }
    public byte B { get; set; }
    public byte C { get; set; }
    public byte D { get; set; }
    public byte E { get; set; }
    public byte H { get; set; }
    public byte L { get; set; }
    public byte AlternateA { get; set; }
    public byte AlternateF { get; set; }
    public byte AlternateB { get; set; }
    public byte AlternateC { get; set; }
    public byte AlternateD { get; set; }
    public byte AlternateE { get; set; }
    public byte AlternateH { get; set; }
    public byte AlternateL { get; set; }
    public byte I { get; set; }
    public byte R { get; set; }

    public ushort AF { get; set; }
    public ushort BC { get; set; }
    public ushort DE { get; set; }
    public ushort HL { get; set; }
    public ushort AlternateAF { get; set; }
    public ushort AlternateBC { get; set; }
    public ushort AlternateDE { get; set; }
    public ushort AlternateHL { get; set; }
    public ushort IX { get; set; }
    public ushort IY { get; set; }
    public ushort PC { get; set; }
    public ushort SP { get; set; } = 0xFFFF;
    public bool Iff1 { get; set; }
    public bool Iff2 { get; set; }
    public byte InterruptMode { get; set; }

    public void Reset()
    {
        throw new NotImplementedException("Red-phase register reset scaffold.");
    }
}
