namespace RockULA.Core.Cpu;

public sealed partial class Z80Cpu
{
    private bool TryAcceptInterrupt()
    {
        bool nmi = IsNmiPending;
        if (!nmi)
        {
            if (!IsInterruptLineAsserted || !Registers.Iff1 || IsEiDelayActive)
            {
                return false;
            }

            if (Registers.InterruptMode != 1)
            {
                throw new NotSupportedException($"Interrupt response for IM {Registers.InterruptMode} is not implemented.");
            }
        }

        ushort interruptedPc = Registers.PC;
        if (nmi)
        {
            // Consume before callbacks: a new edge during this response remains pending.
            IsNmiPending = false;
        }
        else
        {
            Registers.Iff2 = false;
        }

        Registers.Iff1 = false;
        IsHalted = false;
        // Neither response decodes the sampled byte or increments PC.
        _bus.Execute(nmi
            ? new Z80BusCycle(Z80BusCycleKind.OpcodeFetch, interruptedPc, 4, 3)
            : new Z80BusCycle(Z80BusCycleKind.InterruptAcknowledge, interruptedPc, 6, 5));
        IncrementRefresh();
        Internal(interruptedPc, 1);
        Push(interruptedPc);
        Registers.PC = nmi ? (ushort)0x0066 : (ushort)0x0038;
        // A response is not the instruction that retires an existing EI delay.
        return true;
    }

    private void IncrementRefresh()
    {
        Registers.R = (byte)((Registers.R & 0x80) | ((Registers.R + 1) & 0x7F));
    }
}
