namespace RockULA.Core.Cpu;

public enum Z80BusCycleKind
{
    OpcodeFetch,
    MemoryRead,
    MemoryWrite,
    Internal
}
