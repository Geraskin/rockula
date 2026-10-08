# Development roadmap
Updated: 2026-10-08. Status is about implemented evidence, not intended scope.

| Milestone | Status | Deliverable | Exit gate |
| --- | --- | --- | --- |
| M0 Foundation | Build/test verified; interactive UI check pending | Solution, shell, CLI, ROM validation, docs, agent workflow, CI | Build/tests plus honest validation record |
| M1 Z80 foundation | First NOP/load slice in validation; M1 incomplete | State, bus, ALU, unprefixed instruction slices | Independent semantic and nominal bus tests |
| M2 Complete CPU | Not started | CB/ED/DD/FD/indexed-CB, interrupts, undocumented NMOS behavior | Coverage matrix and external oracle/exerciser reports |
| M3 48K machine | Not started | Memory, keyboard/ports, basic ULA, interrupts, run loop | Deterministic synthetic machine programs |
| M4 First playable | Not started | SNA, Z80 v1, firmware selection, desktop input/video | Curated user-supplied snapshot cases and responsive host |
| M5 Sound and tape | Not started | Beeper/audio pacing, TAP pulse playback | Signal tests, sustained audio and real tape loads |
| M6 Timing and tooling | Not started | Contention, raster/floating bus, debugger, complete save states | Event traces, raster regressions, state/replay equality |
| M7 128K | Deferred | Explicit model, paging, AY, applicable formats | 128K-specific tests without 48K regressions |
| M8 Browser and extras | Deferred | WASM host, supported TZX blocks, optional rewind | Browser performance/audio tests; per-feature gates |

Instruction bus sequencing is designed in M1; contention is not postponed by making it impossible
to insert within an instruction. M3's basic final-RAM renderer is deliberately limited; M6 replaces
it with timed fetches. M4 is a useful playable checkpoint, not an accuracy claim.

## M0 foundation
Provide a reproducible development setup, honest status, and no mandatory ROM download.
Desktop displays a welcome shell; headless exposes status/help; Core validates immutable ROM
input; Formats is a reserved buildable library. Initial tests concern input validation only.
No instruction execution or compatibility is implemented.

## M1/M2 CPU
Implement a small test bus with independent operation logs before attaching Spectrum devices.
Maintain an explicit opcode coverage matrix including ignored/repeated prefixes and illegal/
undocumented behavior. Cover flags, wraparound, branches, alternate registers, stack order,
R and interrupt behavior. Enable tracing without per-instruction allocations in normal execution.

Run curated single-step vectors and a separately licensed reference oracle; pin revisions and
record corpus hashes. Integrate ZEXDOC/ZEXALL only after their source/license and runner assumptions
are understood. Never use their final success message as the only accuracy gate.

## M3 machine
Wire firmware/RAM, active-low keyboard, ULA port aliases, border/beeper latches and interrupt line.
Use tiny self-authored guest programs so CI needs no original ROM. Establish frame/time identity
and complete-machine replay before connecting a wall-clock host.

## M4 first playable checkpoint
Implement 48K SNA and Z80 v1 parsing with malformed-input tests and paused atomic installation.
Add firmware file selection and clear errors, pixel viewport, key mapping, pause/reset and integer
scaling. Track game compatibility by format/hash/profile; no game assets in source control.
Require a real interactive session on Windows, and deterministic bounded headless runs.
First release supports only formats and profiles backed by these results.

## M5 sound/tape
Choose a maintained host audio library in an ADR after a license/API review; the bootstrap does
not preselect one. Test deterministic beeper-to-PCM conversion before real audio output.
Implement TAP standard pulse loading, loader controls, bounded turbo and optional explicit
fast-load policy. Sound/pacing/keyboard responsiveness need sustained checks on the target host.

## M6 accuracy/debuggability
Implement per-profile contention, timed ULA fetches, raster border and floating bus.
Add register/memory inspection, instruction step, breakpoints and bounded trace capture.
Versioned internal state must replay video/audio/tape exactly after restore. Measure throughput
in headless mode separately from GUI/audio overhead, with build/hardware/workload recorded.

## Release rules
A milestone is complete only when its plan links to actual commits and current verification.
Every release lists supported machine models/formats, known accuracy limits, included assets and
third-party notices. Require build/test pass, no accidental firmware/game binaries, and an
interactive acceptance check for claimed playable UI. Never publish guessed compatibility.
