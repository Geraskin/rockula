namespace RockULA.Core.Cpu;

/// <summary>Owns the clock and orders one transfer inside each logical bus transaction.</summary>
public abstract class Z80Bus : IZ80Bus
{
    public ulong TStates { get; private set; }

    public byte Execute(Z80BusCycle cycle)
    {
        if (cycle.Kind != Z80BusCycleKind.OpcodeFetch
            && cycle.Kind != Z80BusCycleKind.MemoryRead
            && cycle.Kind != Z80BusCycleKind.MemoryWrite
            && cycle.Kind != Z80BusCycleKind.Internal
            && cycle.Kind != Z80BusCycleKind.InterruptAcknowledge)
        {
            throw new ArgumentOutOfRangeException(nameof(cycle), "Unknown bus transaction kind.");
        }

        if (cycle.BaseTStates <= 0 || cycle.TransferOffset < 0 || cycle.TransferOffset > cycle.BaseTStates)
        {
            throw new ArgumentOutOfRangeException(nameof(cycle), "Invalid transaction duration or transfer offset.");
        }

        int waits = GetWaitStates(cycle);
        if (waits < 0)
        {
            throw new InvalidOperationException("A bus cannot insert a negative number of wait states.");
        }

        ulong start = TStates;
        // Validate both timestamps before advancing or touching memory.
        ulong transfer = checked(start + (ulong)waits + (ulong)cycle.TransferOffset);
        ulong end = checked(start + (ulong)waits + (ulong)cycle.BaseTStates);
        AdvanceTo(transfer);

        byte value;
        switch (cycle.Kind)
        {
            case Z80BusCycleKind.OpcodeFetch:
            case Z80BusCycleKind.MemoryRead:
                value = ReadMemory(cycle.Address);
                break;
            case Z80BusCycleKind.InterruptAcknowledge:
                value = AcknowledgeInterrupt(cycle.Address);
                break;
            case Z80BusCycleKind.MemoryWrite:
                WriteMemory(cycle.Address, cycle.Data);
                value = cycle.Data;
                break;
            default:
                value = 0;
                break;
        }

        AdvanceTo(end);
        OnCycleCompleted(cycle, start, transfer, end, value);
        return value;
    }

    public void NotifyReti() => OnReti();

    protected virtual void OnReti()
    {
    }

    protected abstract byte ReadMemory(ushort address);

    protected abstract void WriteMemory(ushort address, byte value);

    /// <summary>Sample device data independently of memory; unwired data defaults to FF.</summary>
    protected virtual byte AcknowledgeInterrupt(ushort address) => 0xFF;

    protected virtual int GetWaitStates(Z80BusCycle cycle) => 0;

    /// <summary>Advance devices over the interval before the transaction is sampled.</summary>
    protected virtual void OnAdvance(ulong previous, ulong current)
    {
    }

    protected virtual void OnCycleCompleted(
        Z80BusCycle cycle, ulong start, ulong transfer, ulong end, byte value)
    {
    }

    private void AdvanceTo(ulong target)
    {
        if (target == TStates)
        {
            return;
        }

        ulong previous = TStates;
        TStates = target;
        OnAdvance(previous, target);
    }
}
