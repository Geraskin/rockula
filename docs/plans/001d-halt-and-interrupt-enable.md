# Work order 001d: HALT and interrupt enable state
Status: tests first; implementation pending.
Branch `feat/z80-foundation`, PR #6 to `main`.
Starting commit: `52d554f4a01d9c79560a29ee5210757615a9c49e`.

## Scope and sources
Add HALT (76), DI (F3), EI (FB): 248 base encodings after completion.
Read [Zilog UM008011-0816](https://www.zilog.com/docs/z80/um0080.pdf), printed
pp.14, 17–18 and 181–183. HALT uses repeated M1 reads with ignored data;
DI clears both IFFs; EI sets both and inhibits acceptance through the next instruction.
These are instruction-boundary semantics, not electrical IFF/pin timing.
SCF/CCF need a separately sourced Q/history contract and remain unsupported.

## State and bus policy
Expose read-only IsHalted and IsEiDelayActive on Z80Cpu. Both start false and Reset
clears them without rewinding bus time or memory. Logical PC points after HALT;
each halted Step performs one four-T-state OpcodeFetch at that fixed PC, updates R,
ignores fetched data and returns to its caller. The fetch address is a declared logical
convention, not a verified pin trace. Existing wait/sampling rules apply.
EI delay retires only after a successfully completed following instruction; another EI
renews it and DI clears it. Faults preserve the pending state and require Reset.
State inspection is not complete save/restore. No interrupt request/acknowledge API,
IM execution, NMI, RETN/RETI or interrupt exit from HALT in this slice; Reset is the
only implemented HALT exit. Hosts must bound calls rather than spin inside Step.

## Files and independent tests
Z80Cpu.cs adds control state and dispatch; Z80InterruptEnableTests.cs asserts literal
states, four-T timings and bus traces. Boundary tests update the literal supported set.
Cover every input F and IFF combination, repeated EI, EI followed by a multi-cycle
instruction, DI and HALT, PC/R wrapping, ignored unsupported/side-effect bytes during
HALT, waits and live memory sampling, fault behavior and reset of both latches.
Update coverage, README, CLI status, roadmap and current work-order links.

## Acceptance and limits
Required restore, Release build/test on Windows/Linux, both existing CLI demos,
Linux dotnet format --verify-no-changes. No local SDK is available; use CI and
inspect actual test logs before recording evidence. Publish and inspect failing
expectations before implementing. No silicon, refresh-pin, external exerciser,
Spectrum device, interactive UI or game compatibility claim.
