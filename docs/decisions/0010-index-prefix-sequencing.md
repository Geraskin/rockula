# 0010: Index prefix sequencing and bounded instruction stepping
Status: accepted for work order 001i.

## Decision
Decode DD/FD iteratively within one Step. The last index prefix selects IX/IY;
each prefix and ordinary terminal opcode is a four-T M1, advancing PC/R.
ED removes substitution; DDCB/FDCB displacement and final payload are ordinary
three-T memory reads, so only the index and CB fetches increment R.
No temporary swapping of HL with IX/IY or heap-allocated instruction context.
Shared word/bit arithmetic stays testable through existing independent cases.
The instruction's terminal opcode determines EI-delay retirement. No prefix is
an interrupt boundary or an independent retired instruction. HALT retains the
logical next PC after the whole prefix sequence, then performs ordinary halted steps.

## Address and fault policy
Indexed addressing is signed displacement plus selected index modulo 65,536.
Five Internal T follow the displacement read for ordinary indexed memory. Immediate
memory loads instead read d and n then idle two T. Indexed CB reads d/payload then
idles two T; an extra internal T follows the operand read before optional write.
Internal labels are displacement address (five-T calculation), immediate/payload
address (two-T calculation), effective address (read/modify idle), or original
instruction address for inherited word/stack sequencing. They are logical conventions.
WZ commits the effective address after successful calculation idle, before operand
transfer. A failed read/write may therefore retain new WZ with old F/index/result
register. Indexed CB flags and destination register commit after successful memory
write; ignored ROM writes still copy the computed result, never a readback value.
Other indexed word WZ writers retain ADR 0009's boundary policy. Bus effects are not rolled back.
Unsupported terminals report original first-prefix address, last index prefix and
terminal opcode. Unsupported ED reports the ED prefix/payload with the same original
instruction address. Failed instructions preserve previous EI delay and require Reset.

## Bound and consequences
At most 65,536 consecutive index-prefix bytes are consumed in one Step, including
the first byte. Reaching that number without a terminal raises InvalidOperationException
and faults the CPU after the consumed fetches. This is an emulator execution bound,
not Z80 silicon behavior; it also bounds live-memory prefix streams beyond one
address-space traversal. A chain of 65,535 prefixes followed by a terminal is allowed.
Selection/count are local to Step; a fault cannot resume them. No new persistent
prefix latch or save-state schema is introduced. Q and complete save/restore remain future work.
Sources and verification are recorded in [001i](../plans/001i-index-prefixes.md).
