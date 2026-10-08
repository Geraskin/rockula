using RockULA.Core.Cpu;

namespace RockULA.Core.Tests.Cpu;

internal readonly record struct CycleEvent(
    Z80BusCycleKind Kind, ushort Address, ulong Start, ulong Transfer, ulong End, byte Value);

internal sealed class RecordingBus : Z80Bus
{
    public byte[] Memory { get; } = new byte[65536];

    public List<CycleEvent> Events { get; } = [];

    public Func<Z80BusCycle, int>? WaitStates { get; set; }

    public Func<ushort, byte>? InterruptAcknowledging { get; set; }

    public int InterruptAcknowledgements { get; private set; }

    public Action<ulong>? Advancing { get; set; }

    protected override byte ReadMemory(ushort address) => Memory[address];

    protected override void WriteMemory(ushort address, byte value)
    {
        Memory[address] = value;
    }

    protected override byte AcknowledgeInterrupt(ushort address)
    {
        InterruptAcknowledgements++;
        return InterruptAcknowledging?.Invoke(address) ?? 0xFF;
    }

    protected override int GetWaitStates(Z80BusCycle cycle) => WaitStates?.Invoke(cycle) ?? 0;

    protected override void OnAdvance(ulong previous, ulong current)
    {
        Advancing?.Invoke(current);
    }

    protected override void OnCycleCompleted(
        Z80BusCycle cycle, ulong start, ulong transfer, ulong end, byte value)
    {
        Events.Add(new CycleEvent(cycle.Kind, cycle.Address, start, transfer, end, value));
    }
}
