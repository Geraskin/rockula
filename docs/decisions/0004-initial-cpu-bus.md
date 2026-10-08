# 0004: Transaction-level timing for the first CPU slice
Status: accepted for the first slice. Date: 2026-10-08.

## Context
Instructions must expose ordered memory effects before a Spectrum scheduler is attached.
The bootstrap has no CPU or bus. Zilog's waveforms specify electrical edges that a first
integer-T-state model will not fully reproduce.

## Decision
Use synchronous Z80Cpu.Step, a mutable owner-confined register file and IZ80Bus.Execute.
Bus transactions carry kind/address/base duration/logical transfer offset/data. Z80Bus owns
the clock, validates the operation and waits, computes timing with checked host arithmetic,
advances to transfer, performs it once, and advances to the transaction end.

For this first adapter convention, M1 has duration 4 and transfer offset 3; ordinary memory
transactions have duration/offset 3. These are declared logical transaction boundaries,
not a statement that every physical read/write pin edge occurs there.
Wait injection shifts transfer and completion before side effects occur.
Internal operations can be represented but no implemented instruction needs one.
Port/acknowledge and refresh-pin sequencing will be added with their instruction/device slices.

## Alternatives
Applying all effects before one instruction-total delay prevents meaningful device sampling.
A pin-level half-cycle engine is unnecessary for getting the first register/memory semantics
tested, but may become justified by measured ULA integration requirements.

## Consequences
Nominal cycles and operation order can be verified now. Exact NMOS internal/WZ, refresh
addresses, half-cycle signals, dynamic WAIT polling and Spectrum contention are deferred.
A future profile must validate/correct transfer phases before claiming raster accuracy.
Core reset does not rewind a bus that may own other devices; the machine reset will coordinate it.
An unsupported instruction consumes its initial fetch, marks the CPU faulted and requires reset.

## Verification
Independent per-encoding tests and explicit bus traces; a wait changes sampled data, not only
the final cycle total. Same synthetic initial state/program produces the same demo result.
