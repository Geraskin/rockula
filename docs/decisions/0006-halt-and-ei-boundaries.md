# ADR 0006: HALT and EI at instruction boundaries
Status: accepted. Date: 2026-10-08.

## Context
The synchronous CPU needs bounded HALT execution and EI inhibition state before
interrupt acceptance can be added. Architectural registers alone do not capture these.
Source: Zilog UM008011-0816, printed pp.14, 17–18, 181–183; see [001d](../plans/001d-halt-and-interrupt-enable.md).

## Decision
Z80Cpu owns read-only IsHalted and IsEiDelayActive, initialized and reset false.
After HALT, PC is the next logical instruction address. A halted Step reads one M1
at that PC, ignores data, increments the low seven R bits, and advances four T-states
plus waits. It returns without incrementing PC or decoding the data. This address
policy is an explicit logical abstraction, not independent electrical-pin evidence.

At completed EI boundaries both IFFs are true and IsEiDelayActive is true. The
following successfully completed instruction clears the delay; EI renews it and
DI clears both IFFs and the delay. The latch stays true throughout that following
instruction's bus callbacks. Intra-fetch physical IFF transitions are not represented.
A failed instruction does not retire the delay; the terminal fault requires Reset.

Reset clears all CPU control latches and architectural registers while leaving bus
memory/time to the owner. Future complete state capture must include these latches.

## Consequences
Hosts can enforce a finite step budget even in HALT. This slice implements no
interrupt lines, acknowledge, NMI, IM handling, RETN/RETI or interrupt HALT exit;
only Reset exits HALT currently. Inspection is not a state-restore API. Future
interrupt acceptance must check IFF1 and this inhibition latch at its boundary,
and must preserve the documented resumed PC convention.
