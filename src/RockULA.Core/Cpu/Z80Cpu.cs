namespace RockULA.Core.Cpu;

/// <summary>Instruction-boundary stepping for the explicitly supported base-opcode slice.</summary>
public sealed class Z80Cpu
{
    public Z80Cpu(IZ80Bus bus, Z80Registers? registers = null)
    {
        ArgumentNullException.ThrowIfNull(bus);
        Registers = registers ?? new Z80Registers();
    }

    public Z80Registers Registers { get; }

    public bool IsFaulted => false;

    public ulong Step()
    {
        throw new NotImplementedException("Red-phase CPU scaffold.");
    }

    public void Reset()
    {
        throw new NotImplementedException("Red-phase CPU reset scaffold.");
    }
}
