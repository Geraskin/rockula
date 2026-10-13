using RockULA.Core.Cpu;
using RockULA.Core.Input;

namespace RockULA.Core.Machines;

public sealed class Spectrum48Bus : Z80Bus
{
    public Spectrum48Bus(RomImage rom) => throw new NotImplementedException();
    public byte BorderColor => 0;
    public bool Beeper => false;
    public bool Mic => false;
    public bool EarHigh { get; set; } = true;
    public byte Joystick { get; set; }
    public byte Peek(ushort address) => throw new NotImplementedException();
    public void Poke(ushort address, byte value) => throw new NotImplementedException();
    public void SetKey(SpectrumKey key, bool down) => throw new NotImplementedException();
    public void ReleaseKeys() => throw new NotImplementedException();
    public void InstallRam(ReadOnlySpan<byte> ram, byte border) => throw new NotImplementedException();
    protected override byte ReadMemory(ushort address) => throw new NotImplementedException();
    protected override void WriteMemory(ushort address, byte value) => throw new NotImplementedException();
}
