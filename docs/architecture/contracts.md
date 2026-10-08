# State and host contracts
Whole-machine contracts remain proposals. Existing Core APIs include RomImage, Z80Registers,
Z80Cpu, IZ80Bus and Z80Bus for the declared CPU instruction slices; see [opcode coverage](z80-coverage.md).
Complete CPU/device state capture, remaining interrupt modes and machine restore are not yet implemented.

## Execution and ownership
A machine has reset, instruction stepping, bounded running, input submission, inspection and
state capture/restore operations. Choose final names in the implementing work order.
A run result reports stop reason (budget, breakpoint, requested pause, fault), cycles actually
executed and any instruction-boundary overshoot. A frame is an event, not a promise that the
last instruction ends exactly at the frame boundary.

Only the machine owner mutates live state. Desktop commands are acknowledged after application.
Core itself stays synchronous, with no worker/thread/Task requirement. The host decides when
to call it. Debugger register edits and snapshot installation happen while execution is paused.

## CPU state
Capture main/alternate register sets, IX/IY, SP/PC, I/R, IFF1/IFF2, interrupt mode, HALT and
pending EI semantics. Preserve any implemented internal state affecting subsequent behavior:
WZ/MEMPTR, flag-latch quirks and partial execution state if pausing below an instruction boundary
is supported. Instruction-boundary snapshots can avoid a micro-operation cursor, but that limit
must be explicit and tested.

Implemented boundary inspection includes Z80Cpu.IsHalted and IsEiDelayActive.
[ADR 0006](../decisions/0006-halt-and-ei-boundaries.md) specifies reset, PC and delay
retirement. [ADR 0007](../decisions/0007-interrupt-boundary-inputs.md) adds owner-supplied
INT/NMI levels, a pending NMI edge latch and NMI/IM 1 service.
[ADR 0008](../decisions/0008-ed-interrupt-control.md) adds IM2 and five ED commands. Full state capture/restore
remains future work; these input levels/latches must be included when it is implemented.
[ADR 0009](../decisions/0009-cb-and-wz-state.md) adds mutable Z80Registers.WZ and
ordinary CB execution. WZ has deterministic reset zero and must be included in future
internal snapshots; external formats missing WZ will need an explicit initialization policy.

## Machine state
Include model/profile/version, all RAM banks, paging latches when implemented, absolute/frame
time, interrupt/device latches, raster/fetch/FLASH phase, tape identity/cursor/pulse level,
input matrix, audio transition/resampling/filter phase and firmware identity.
Do not serialize host handles, paths, GUI widgets, clocks or audio-device queues.

Internal save states have a versioned schema, length limits and ROM-hash validation.
External SNA/Z80 snapshots cannot represent every internal latch: imports initialize missing
state by a documented policy; they are not lossless internal save states.
Restore atomically after full validation. Failure leaves the previous machine state intact.

## Input
Input events carry target T-state and logical Spectrum key/joystick operation. Hosts map their
keyboard layout and aliases into logical input; replay stores those events, not OS keycodes.
Support simultaneous keys and active-low matrix rows. Handle focus loss by releasing held
host keys; overlapping mappings need reference counts so releasing one alias does not release
a key still held by another. Replay must declare event ordering for identical timestamps.

## Video and audio
Frames publish dimensions, stride, pixel format, frame number and T-state timestamp.
Use nearest-neighbor scaling by default. Define buffer ownership: copying, immutable publication
or leases with explicit release; the UI cannot read a buffer being modified by the next frame.

Audio publishes sample rate, channel layout and timestamped bounded PCM blocks. Underrun is
a host condition: supply silence/report it without resetting or skipping guest time.
Queue reset on state load/pause/resume must avoid stale output and audible bursts.

## Errors and compatibility metadata
Malformed files, unsupported variants, missing firmware and execution faults are distinct errors.
Report format/version/offset where useful, without exposing arbitrary user paths in shared logs.
Never treat an unknown instruction as NOP or ignore unsupported snapshot fields silently.

Compatibility results identify build, model/profile, ROM and input hashes, execution budget,
input schedule, enabled fast-load policy, expected outputs and actual outputs.
Same initial complete state plus same events and budget must yield equal guest results across
headless and desktop-host execution.

Index prefix retirement, indexed memory sequencing and the explicit prefix-chain execution
bound follow [ADR 0010](../decisions/0010-index-prefix-sequencing.md).

ED word/I/R/nibble operation sequencing and the LD A,I/R interrupt limitation
follow [ADR 0011](../decisions/0011-ed-data-boundaries.md).
