# Work order 001l: First 48K snapshot player
Status: implemented and CI verified; real-file interactive acceptance pending. Merged
to `main` in PR #6 (merge commit `0a8e744`).

## Goal
Deliver an executable desktop path: choose user 16 KiB ROM, open a strict 48K
SNA, run frames, display bitmap/attributes/FLASH/border, keyboard and Kempston,
pause and restart. Booting a supplied ROM is also available. No firmware/games
are downloaded. This is a functional accuracy prototype, not a compatibility release.

## Sources read 2026-10-13
Zilog UM008011-0816 SCF/CCF and interrupt modes:
https://www.zilog.com/docs/z80/um0080.pdf ; Banks rev1.0 (2018-08-21), SCF/CCF;
redcode Z80_XCF_Flavor README hardware background (not its GPL code/binaries),
https://github.com/redcode/Z80_XCF_Flavor .
World of Spectrum FAQ SNA schema and 48K technical research:
https://worldofspectrum.net/faq/reference/formats.htm and
https://worldofspectrum.net/faq/reference/48kreference.htm ;
Claus Jahn SNA schema https://worldofspectrum.net/zx-modules/fileformats/snaformat.html .
World of Spectrum Z80 interrupt reference (32-T INT pulse):
https://worldofspectrum.net/faq/reference/z80reference.htm .
Avalonia official WriteableBitmap/StorageProvider API docs and selected package API.

## CPU prerequisite
SCF/CCF use Zilog Q/history ((Q xor F) or A) X/Y; Q=F after actual flag-generating
operations (including identical resulting F), else zero. POP AF/EX AF and RES/SET
are non-generators. Retire Q only after successful whole prefix/iteration; fault
retains previous Q. Reset/import Q=0; halted fetch and completed responses clear Q.
ED aliases decode their physical NEG/IM/return equivalents; undefined ED payloads
are documented two-fetch NOPs. All 252 standalone encodings and 256 ED payloads
then decode. IM0 supports acknowledged RST only, including Spectrum's FF; other
IM0 data still faults explicitly after acknowledgement. No pin/external oracle claim.

## Machine/import/host contract
48K ROM 0000–3FFF protected, RAM 4000–FFFF. Any even port decodes keyboard and
border/MIC/beeper latches; EAR defaults high. Conventional Kempston port low byte
1F. Logical frame=69,888 T, INT first 32 T; Step boundary sampling, no contention.
Final-RAM renderer crops a 320x240 frame around 256x192 active pixels, 16-frame
FLASH and normal/bright RGB palette. This does not reconstruct raster border/fetches.
One UI-thread owner for this first bounded prototype: no overlapping timer/file/input
mutation, one frame per tick, no guest cycle deletion. Production audio pacing/worker
and timed input replay are deferred. Focus/pause/load releases all held keys.

SNA is exactly 49,179 bytes: 27-byte header and 49,152 RAM. Decode little-endian,
IFF2 bit2 into both IFFs, IM0–2, border0–7. Saved SP must be 4000–FFFE; pop PC
from RAM and wrap restored SP if FF FE. Reject truncated/128K/ROM-stack wraps.
Never erase saved stack bytes. Parser copies RAM; machine validates all input then
replaces bus/CPU atomically. Import initializes clock/phase, Q/WZ, pending signals,
HALT/EI, held input and device latches; not a lossless internal save state.
Files: CPU Q/aliases, Core Machines/Input/Video, Formats Sna48Snapshot, desktop
player, bounded headless run/demo and synthetic CPU/machine/parser tests.

## Acceptance
Publish independent tests/scaffolds before implementation and inspect red CI.
Carry/history, alias/NOP and IM0 vectors; malformed SNA, register/stack/ownership
and atomic install; keyboard/multirow/ports/ROM protection, bitmap address/colour/
FLASH and clock/INT/overshoot; a synthetic SNA program through the complete path.
Windows/Linux restore/Release/test, demos, Linux format. Actual interactive UI,
real firmware boot and a real game require local user files/display; report pending.
No audio, TAP/TZX/Z80/128K, floating bus/contention, exact raster, full state restore
or tested game compatibility claim. Provide concrete launch instructions.

## Verification
Independent red tests: commit 7501d924284fd0d8c81f3e79379349665b8ae794, CI 38333703020:
69 expected failures, 1,477 passes, no skips. Core completion and host implementation
are on the same authorized feature branch; host commit bea022a2fdcd20216bf882593367a818fa2d5744.
[CI 38334641862](https://github.com/Geraskin/rockula/actions/runs/38334641862)
passed Windows/Linux restore, Release build (zero warnings/errors), all 1,546 tests,
all three headless demos, Linux format and Linux Xvfb native-window/bitmap-upload smoke.
No external ROM/game was supplied; real boot, game compatibility, file dialogs and
interactive Windows keyboard/pause responsiveness remain pending, not inferred passes.
Concrete launch and controls: [playing](../playing.md).
