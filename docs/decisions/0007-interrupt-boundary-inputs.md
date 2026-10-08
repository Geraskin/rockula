# ADR 0007: owner-supplied interrupt levels and NMI latch
Status: accepted. Date: 2026-10-08.
Supersedes ADR 0006's Reset-only HALT exit limit for the responses implemented here.

## Context
A CPU-only bus has no Spectrum interrupt device yet. Deterministic acceptance
needs explicit inputs and a bus acknowledgement distinct from ordinary memory.
Sources and independent tests are in [001e](../plans/001e-nmi-and-im1.md).

## Decision
The single owner supplies active-level booleans through SetInterruptLine and
SetNmiLine. INT is a level; NMI's inactive-to-active transition sets a pending latch.
Held NMI does not retrigger, deassertion retains a pending request, and multiple
edges before service coalesce. All levels/latch are readable and CPU Reset clears
them; whole-machine reset must reapply external levels. No thread-safe queue or
host-clock sampling is introduced.

Step samples the inputs at entry. Pending NMI wins, regardless of IFF1 or EI delay.
Otherwise asserted INT needs IFF1 and expired EI inhibition. Requests arriving
in bus callbacks wait until the next Step; this convention does not reproduce
physical sampling apertures. Accepted service is a complete Step, separate from
the handler instruction, and preserves the interrupted logical PC on the stack.
HALT is cleared on acceptance, so its next PC is the resume address.

NMI consumes its pending latch before bus callbacks and clears IFF1 only; a new
edge during response remains pending. IFF2 is preserved even for nested NMI.
Response is not an instruction and does not retire an existing EI delay; the next
successful instruction does. Detailed EI/NMI silicon quirks remain unverified.
IM 1 clears both IFFs. Eligible IM 0/2 or invalid modes fail explicitly before bus,
IFF/HALT/stack mutation; CPU latches its terminal fault. Disabled INT is ignored.

## Logical transactions
NMI performs an ignored M1 (4/3), Internal (1/0), then high/low stack writes (3/3)
and vectors to 0066 (11 T). IM 1 performs InterruptAcknowledge (6/5), Internal
(1/0), two stack writes, and vectors to 0038 (13 T). Both increment R once.
Internal/acknowledge Address is the interrupted PC as a logical label. Transfer
offsets retain the adapter convention of ADR 0004, not electrical pin evidence.
A protected AcknowledgeInterrupt(address) hook samples the device byte at the
logical transfer; default is FF. It never reads memory and IM 1 ignores the byte.
Wait insertion, checked time arithmetic and callbacks apply to acknowledgement.

## Limits and failures
Control changes occur before bus service, so failure leaves those changes plus
any completed writes/time intact; a fault requires Reset. No rollback is promised.
IM selection opcodes, IM 0/2 acceptance, RETN/RETI, daisy chain notification, full
state restoration, refresh pins and ULA interrupt timing remain future work.
Complete state capture must include input levels, pending NMI, HALT and EI delay.
