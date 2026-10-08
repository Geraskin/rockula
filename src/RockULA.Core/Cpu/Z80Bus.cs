namespace RockULA.Core.Cpu;

/// <summary>Owns the clock and orders one transfer inside each logical bus transaction.</summary>
public abstract class Z80Bus : IZ80Bus
{
    public ulong TStates => 0;

    public byte Execute(Z80BusCycle cycle)
    {
        throw new NotImplementedException("Red-phase bus scaffold.");
    }

    protected abstract byte ReadMemory(ushort address);

    protected abstract void WriteMemory(ushort address, byte value);

    protected virtual int GetWaitStates(Z80BusCycle cycle) => 0;

    protected virtual void OnAdvance(ulong previous, ulong current)
    {
    }

    protected virtual void OnCycleCompleted(
        Z80BusCycle cycle, ulong start, ulong transfer, ulong end, byte value)
    {
    }
}
