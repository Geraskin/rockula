# Work order 001e: NMI and IM 1 acceptance
Status: implementation awaiting green CI.
Branch `feat/z80-foundation`, PR #6 to `main`.
Starting commit: `00c199da4c2ba6beca677912c0be6454baab09c9`.

## Scope and sources
Accept NMI and IM 1 INT at logical Step boundaries, including HALT exit. The
248 supported base encodings stay unchanged. IM is configured through the existing
register file; ED mode selection and RETN/RETI are future slices, as are IM 0/2.
Read [Zilog UM008011-0816](https://www.zilog.com/docs/z80/um0080.pdf), printed
pp.6, 12–14, 17–20: assertion sensitivity, priority, IFFs, stack/vector and timing.
NMI resets IFF1 and preserves IFF2 (Table 1), including nested requests; it does
not blindly copy IFF1 into IFF2. INT clears both IFFs.
Read [Weissflog netlist study, 2021-12-06](https://floooh.github.io/2021/12/06/z80-instruction-timing.html),
NMI Timing and Mode 0/1 Interrupt Timing: ignored M1 + one internal T + two writes
for NMI; acknowledge + two writes for IM 1. The netlist has model differences;
this supplies nominal sequencing, not measured original NMOS pin certification.

## Boundary and signal contract
Owner calls SetInterruptLine(bool asserted) and SetNmiLine(bool asserted), where
true represents active hardware level, independent of negative pin polarity.
INT is level-sensitive. NMI latches an inactive-to-active edge; deasserting does
not lose it, held assertion does not retrigger, multiple pending edges coalesce.
Read-only properties expose line levels and pending NMI. Reset clears all three;
the owner must reapply external device levels after CPU-only reset.
Step first handles pending NMI regardless of IFF/delay, otherwise asserted INT
requires IFF1 and no EI inhibition. One accepted response is one Step, not the
handler instruction. NMI consumes its latch before bus callbacks, so a new edge
during response remains pending. Response does not retire EI delay: the next
successfully completed instruction does. IFF1 is disabled during NMI handling.
Requests changed during a cycle apply at the next Step entry. This is a declared
boundary model, not hardware sampling aperture or pulse-width emulation.
An eligible INT in unsupported mode faults before acknowledgement/state/time
mutation; disabled INT does not reject a stored unsupported mode.

## Bus and state
NMI: ignored OpcodeFetch at unchanged PC (4/3), Internal at logical PC (1/0),
stack high then low writes (3/3 each), vector 0066; 11 nominal T, one R update.
IM 1: InterruptAcknowledge at logical PC (6/5), Internal at PC (1/0), then stack high/low (3/3), vector
0038; 13 nominal T, one R update. The acknowledge hook returns a byte separately
from memory; IM 1 ignores it. Its offset/address are logical conventions, not pins.
Acceptance clears HALT and the relevant IFFs before bus work; partial faults keep
committed memory/time/control effects and require Reset. Stack and R wrap.

## Files and tests before implementation
Z80Cpu.Interrupts.cs and Step/reset hooks; bus kind and protected acknowledge hook.
RecordingBus and Z80InterruptTests assert literal states/events independently.
Cover IFF combinations, flags, wrap, NMI edge/priority/nesting, requests in callbacks,
EI delay/repeated EI/DI, HALT resume PC, held INT, live acknowledgement with waits,
unsupported modes and failures before/during acknowledgement or stack writes.
Update contracts, ADR, status, references and PR with actual red/green evidence.

## Acceptance and limits
Windows/Linux restore, Release build/test, both demos; Linux formatting. Inspect
failing CI before implementation and green logs before recording a pass. No local
SDK is available. No full interrupt suite, daisy chain, RETI notification, state
restore, prefixes, Spectrum device/pin timing, external oracle or game claim.

## Red evidence
Test commit `0a9db2c62def1843be6bc2d9287808599e32ab5e`,
[CI 37726052839](https://github.com/Geraskin/rockula/actions/runs/37726052839):
Linux build passed with zero warnings/errors; 30 new cases failed, 311 passed
(all 310 previous cases plus the new DI-blocking case), zero skipped, 341 total.
Actual failing states/timings and unsupported acknowledgement were inspected before code.
