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

    public ushort AF
    {
        get => (ushort)((A << 8) | F);
        set
        {
            A = (byte)(value >> 8);
            F = unchecked((byte)value);
        }
    }

    public ushort BC
    {
        get => (ushort)((B << 8) | C);
        set
        {
            B = (byte)(value >> 8);
            C = unchecked((byte)value);
        }
    }

    public ushort DE
    {
        get => (ushort)((D << 8) | E);
        set
        {
            D = (byte)(value >> 8);
            E = unchecked((byte)value);
        }
    }

    public ushort HL
    {
        get => (ushort)((H << 8) | L);
        set
        {
            H = (byte)(value >> 8);
            L = unchecked((byte)value);
        }
    }

    public ushort AlternateAF
    {
        get => (ushort)((AlternateA << 8) | AlternateF);
        set
        {
            AlternateA = (byte)(value >> 8);
            AlternateF = unchecked((byte)value);
        }
    }

    public ushort AlternateBC
    {
        get => (ushort)((AlternateB << 8) | AlternateC);
        set
        {
            AlternateB = (byte)(value >> 8);
            AlternateC = unchecked((byte)value);
        }
    }

    public ushort AlternateDE
    {
        get => (ushort)((AlternateD << 8) | AlternateE);
        set
        {
            AlternateD = (byte)(value >> 8);
            AlternateE = unchecked((byte)value);
        }
    }

    public ushort AlternateHL
    {
        get => (ushort)((AlternateH << 8) | AlternateL);
        set
        {
            AlternateH = (byte)(value >> 8);
            AlternateL = unchecked((byte)value);
        }
    }

    public ushort IX { get; set; }
    public ushort IY { get; set; }
    public ushort PC { get; set; }
    public ushort SP { get; set; } = 0xFFFF;
    public bool Iff1 { get; set; }
    public bool Iff2 { get; set; }
    public byte InterruptMode { get; set; }

    /// <summary>Applies our deterministic initialization policy, not a silicon power-on claim.</summary>
    public void Reset()
    {
        AF = BC = DE = HL = 0;
        AlternateAF = AlternateBC = AlternateDE = AlternateHL = 0;
        IX = IY = PC = 0;
        SP = 0xFFFF;
        I = R = InterruptMode = 0;
        Iff1 = Iff2 = false;
    }
}
