namespace RockULA.Core.Cpu;

public interface IZ80Bus
{
    ulong TStates { get; }

    byte Execute(Z80BusCycle cycle);

    void NotifyReti()
    {
    }
}
