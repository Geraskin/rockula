using RockULA.Core.Cpu;

namespace RockULA.Core.Machines;

public readonly record struct Spectrum48CpuState(
    ushort AF, ushort BC, ushort DE, ushort HL, ushort AlternateAF, ushort AlternateBC,
    ushort AlternateDE, ushort AlternateHL, ushort IX, ushort IY, ushort PC, ushort SP,
    byte I, byte R, bool Iff1, bool Iff2, byte InterruptMode)
{
    public Z80Registers CreateRegisters() => new()
    {
        AF = AF, BC = BC, DE = DE, HL = HL, AlternateAF = AlternateAF,
        AlternateBC = AlternateBC, AlternateDE = AlternateDE, AlternateHL = AlternateHL,
        IX = IX, IY = IY, PC = PC, SP = SP, I = I, R = R,
        Iff1 = Iff1, Iff2 = Iff2, InterruptMode = InterruptMode
    };
}
