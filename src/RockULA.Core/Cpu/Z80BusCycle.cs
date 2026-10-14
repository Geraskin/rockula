namespace RockULA.Core.Cpu;

/// <summary>A logical bus transaction; transfer offsets are not pin-level timing.</summary>
public readonly record struct Z80BusCycle(
    Z80BusCycleKind Kind,
    ushort Address,
    int BaseTStates,
    int TransferOffset,
    byte Data = 0);
