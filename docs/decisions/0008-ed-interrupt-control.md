# ADR 0008: partial ED decoding and IM 2 vector response
Status: accepted. Date: 2026-10-08.
Extends [ADR 0007](0007-interrupt-boundary-inputs.md).

ED is recognized as a two-M1 instruction prefix for the five documented commands
in [001f](../plans/001f-ed-interrupt-control.md). It is not a standalone instruction
and interrupts are not sampled between its bytes. Each fetch updates PC/R with
normal wrap. Unsupported payloads report original prefix address, ED and payload,
consume eight nominal T-states and latch a terminal fault. Undocumented aliases
and ED NOP behavior remain unsupported deliberately.

RETN and RETI pop the return PC and restore IFF1 from IFF2 at the successful
instruction boundary. RETI also issues a zero-time NotifyReti callback after the
stack/CPU effects; RETN does not. IZ80Bus supplies a default no-op, Z80Bus exposes
protected OnReti for devices. This is a logical completion event, not the phase
at which real peripheral hardware decodes ED4D; daisy-chain pins are absent.

IM 2 uses the sampled acknowledgement byte without masking its low bit, forms
(I << 8) | byte, pushes PC high/low, then reads target low/high with 16-bit wrap.
Stack/table overlap therefore sees completed writes. Nominal response is 19 T
with the existing six-T acknowledge, one internal T, two writes and two reads.
PC target is committed only after both vector reads. Failure keeps prior control,
stack, memory and clock effects and requires Reset. IM 0 response is still rejected.

Source/model qualifications are in 001f: Zilog, Young/Jan's tested odd-vector and
IFF behavior, and the netlist timing study. No new electrical pin/sampling accuracy,
complete ED support, WZ/Q, state restoration or Spectrum device claim follows.
