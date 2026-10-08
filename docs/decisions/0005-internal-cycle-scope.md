# ADR 0005: nominal internal cycle scope
Status: accepted for work order 001b; electrical address validation is deferred.

## Decision
Retain ADR 0004's timed logical transfers. Represent extended M cycles as an ordinary fetch/read
and a following Internal operation: one T for INC/DEC (HL), PUSH/RST/conditional RET/CALL,
one before DJNZ's displacement read, five after a taken relative displacement read.
This preserves nominal totals and memory transfer order without claiming pin-level accuracy.

Internal Address labels identify the associated logical instruction or operand address:
opcode for PUSH/RST/RET/DJNZ; last operand for CALL and taken JR/DJNZ; HL for INC/DEC (HL).
They are not verified Z80 address-pin values (refresh/IR and WZ are not modeled).
Internal operations have no memory transfer and TransferOffset=0. Bus adapters may insert
explicit synthetic waits for testing, but cannot derive accurate Spectrum contention from
these labels. Verify and replace them against measured per-T-state traces before M6.

## Consequences
Memory reads/writes still advance devices before visibility. No per-step allocation is added.
Instruction totals and wait-shifted effects can be tested now. Full save state and electrical
accuracy remain future contracts; this slice must not advertise cycle-accurate hardware.

Source: Zilog UM008011-0816 instruction M-cycle/T-state tables, selected entries listed in
[001b](../plans/001b-alu-control-flow.md). Tables establish durations, not internal pin addresses.
