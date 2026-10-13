using RockULA.Core.Cpu;
using RockULA.Core.Input;

namespace RockULA.Core.Machines;

/// <summary>Functional 48K map and ports, without contention/floating bus.</summary>
public sealed class Spectrum48Bus : Z80Bus
{
    private readonly byte[] _memory = new byte[65536];
    private readonly byte[] _pressed = new byte[8];
    internal Action<bool>? InterruptLevel { get; set; }

    public Spectrum48Bus(RomImage rom)
    {
        ArgumentNullException.ThrowIfNull(rom);
        rom.Bytes.CopyTo(_memory);
    }
    public byte BorderColor { get; private set; }
    public bool Beeper { get; private set; }
    /// <summary>Raw bit-3 MIC output latch; no analog/audio signal model.</summary>
    public bool Mic { get; private set; }
    public bool EarHigh { get; set; } = true;
    public byte Joystick { get; set; }
    public byte Peek(ushort address) => _memory[address];
    public void Poke(ushort address, byte value)
    {
        if (address < 0x4000) throw new ArgumentOutOfRangeException(nameof(address), "ROM is immutable.");
        _memory[address] = value;
    }
    public void SetKey(SpectrumKey key, bool down)
    {
        int code = (int)key;
        if (code is < 0 or >= 40) throw new ArgumentOutOfRangeException(nameof(key));
        int mask = 1 << (code % 5);
        _pressed[code / 5] = (byte)(down ? _pressed[code / 5] | mask : _pressed[code / 5] & ~mask);
    }
    public void ReleaseKeys()
    {
        Array.Clear(_pressed);
        Joystick = 0;
    }
    public void InstallRam(ReadOnlySpan<byte> ram, byte border)
    {
        if (ram.Length != 49152 || border > 7) throw new ArgumentException("Invalid 48K RAM or border.");
        ram.CopyTo(_memory.AsSpan(0x4000));
        BorderColor = border;
    }
    protected override byte ReadMemory(ushort address) => _memory[address];
    protected override void WriteMemory(ushort address, byte value)
    {
        if (address >= 0x4000) _memory[address] = value;
    }
    protected override byte ReadPort(ushort address)
    {
        if ((address & 1) == 0)
        {
            int keys = 0x1F;
            for (int row = 0; row < 8; row++)
                if ((address & (1 << (row + 8))) == 0) keys &= ~_pressed[row];
            return (byte)(0xA0 | (EarHigh ? 0x40 : 0) | keys);
        }
        return (address & 0xFF) == 0x1F ? (byte)(Joystick & 0x1F) : (byte)0xFF;
    }
    protected override void WritePort(ushort address, byte value)
    {
        if ((address & 1) != 0) return;
        BorderColor = (byte)(value & 7);
        Mic = (value & 8) != 0;
        Beeper = (value & 0x10) != 0;
    }
    protected override void OnAdvance(ulong previous, ulong current) =>
        InterruptLevel?.Invoke(current % Spectrum48Machine.FrameTStates < 32);
}
