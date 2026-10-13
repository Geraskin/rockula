# Verification strategy
Tests cover ROM validation, registers/reset, all 252 standalone base encodings, exhaustive
byte ALU inputs, all flag values for conditional flow, stack/PC wrap, memory/idle traces,
waits, explicit unsupported bytes, partial-transfer faults and self-authored guest programs.
The ALU pair tests execute 1,048,576 vectors inside eight xUnit cases; INC/DEC executes
8,192 value/carry vectors inside sixteen cases. These are not separate discovered tests.
001c adds 524,288 word INC/DEC vectors and a 1,441,792-vector ADD HL sweep (all HL values
against seven carry-boundary operands, plus HL+HL). Rotations, CPL and DAA each cover all
65,536 A/F inputs. DAA additionally follows ADC/SBC for 40,000 decimal integer cases.
001d adds 2,048 DI/EI input-flag/IFF vectors, delay sequences, repeated HALT reads,
wait/live sampling and reset/fault control-state tests.
001e adds NMI/IM 1 IFF/flag sweeps, priority, edge/level handling, HALT exit, EI/DI
acceptance sequences, acknowledgement sampling/waits and partial-response faults.
001f adds 5,120 ED flag/IFF vectors, all 251 unsupported ED payloads, IM2 vector
order/wrap/overlap/waits/faults and synthetic interrupt-return programs.
CI builds on Windows/Linux, runs xUnit tests and both headless demos, and checks formatting
on Linux. It does not launch the desktop UI or prove Spectrum/game compatibility.

## Test layers
| Layer | Evidence | Required limits |
| --- | --- | --- |
| Domain/unit | Explicit expected states, flags, wrap and errors | No production helper used as oracle |
| CPU bus | Ordered M1/read/write/I/O/idle events, addresses, timestamps | Nominal cycles and wait insertion |
| Machine | Self-authored programs, keyboard/ports/interrupts, deterministic time | No original ROM requirement |
| Formats | Synthetic SNA/Z80/TAP fixtures and negative cases | Bounded decode; atomic failed-load behavior |
| Raster/audio | Exact pixel transitions and deterministic PCM hashes | Profile, phase and sample rate identified |
| External CPU | Pinned single-step corpus, reference traces, exercisers | Source/license/hash recorded; no silent omissions |
| External machine | Third-party diagnostic ROM PASS report through `--oracle-rom` | Asset source/license/SHA-256 recorded; explicit invocation, never a fabricated CI pass |
| Compatibility | User-supplied images with scripted input and expected behavior | Build/profile/ROM/asset hash and accuracy policy |
| Host | Actual input/video/audio, pause/load/reset/close | Real interactive check on claimed platform |

Create new test projects only when the feature lands; no permanently skipped placeholder suites.
Keep fast deterministic tests in normal CI. Slow/external suites require explicit invocation and
must report unavailable inputs as unavailable, not zero tests passed.
They are mandatory when the release claims the compatibility they verify.

## Independent CPU expectations
Cover every implemented opcode family, taken/untaken conditions, prefix combinations and edge
values. Test arithmetic exhaustively where useful. Check memory side effects, flags (including
model-specific undocumented ones), register/R state, nominal cycles and bus ordering.
A register-only oracle is insufficient for contention. ZEX-style success is useful but incomplete.

Compare traces at the first divergent event. When an oracle disagrees, verify its CPU model and
source; do not assume any existing emulator is exact in every detail.
Fix regressions with a minimal self-authored reproducer where licensing permits.

## External assets
Store downloaded corpora/user files under ignored `local-data/`. A committed manifest records
name, source URL/revision, license/redistribution basis, SHA-256, expected scope and acquisition
instructions, not embedded copyrighted content. No automatic game/firmware download in CI.
A new corpus runner must enforce size/budget bounds and summarize tested/skipped/failed counts.

## External oracle suite
Every expected value in the local suite is produced by the implementation under test, so it
cannot expose a shared misunderstanding between the code and its own tests. The headless host
therefore exposes an explicitly-invoked suite that boots one user-supplied ROM on the real 48K
machine:

```sh
dotnet run --project src/RockULA.Headless -c Release -- \
  --oracle-rom local-data/zx-diagnostics-v0.37-48k.rom --settle 1500 --keys 1 --frames 4000 \
  --png local-data/oracle-zxdiag.png
```

It settles for `--settle` frames (default 100), applies an optional comma-separated `--keys`
script (digits `0`-`9`, `enter`, `space`, `h`/`p`/`r`/`i`/`n`/`t`) holding each key for 8
frames and then releasing it, runs `--frames` further frames, renders the 320x240 framebuffer
and prints `frames`, `T`, `PC`, `border` and `FramebufferSHA256`. A missing or oversized ROM
returns exit 1 and an unknown key token returns exit 2, both with a message on stderr; an
unavailable input is never reported as a pass. The hash is deterministic for identical inputs
but depends on the total frame count, because the renderer flashes attributes; it is produced
by the implementation under test, so it pins a regression and is not accuracy evidence.

Assets are not committed; keep them in ignored `local-data/`.

| Asset | Source (retrieved) | License | SHA-256 | Scope |
| --- | --- | --- | --- | --- |
| `zx-diagnostics-v0.37-48k.rom` | [brendanalford/zx-diagnostics](https://github.com/brendanalford/zx-diagnostics) release v0.37, `testrom.bin` (2026-10-13) | LGPL-3.0 | `e38354b6b8f393a65a2512cb622c6f255a4d015e9022244a873f0c6c2b0cf464` | 48K RAM, ULA, keyboard, sound, video and Z80 functional PASS/FAIL |
| `opense-3.1.2.rom` | [knaxly-dev/zed-open-roms](https://github.com/knaxly-dev/zed-open-roms) `opense-3.1.2.rom`; upstream OpenSE BASIC, Andrew S. Owen, 2012 (2026-10-13) | GPL-2.0 | `45790ed8d7baac284b41fe1e35bcdd0c24e754c191e6fdcbaff4df2d1e44270d` | redistributable 48K firmware for the ROM-boot path |
| `tests.in` + `tests.expected` | [Fuse Z80 test corpus](https://github.com/tom-seddon/fuse-emulator-code/tree/master/fuse/z80/tests), mirrored in [floooh/chips-test](https://github.com/floooh/chips-test) under `tests/fuse/` (2026-10-13) | GPL-2.0 (Fuse) | `fec4e44dfa4fa889f976aa546a774d48c2a7366ab12312d449f79e85e68d77d7` / `207dc604db5d2a0a49ebd40e1bcb985a919e7a6e3dea1a52884ee05f9a1bf005` | single-instruction CPU corpus: registers, T-states and memory |

Observed on Linux, .NET SDK 10.0.401, 2026-10-13 (`feat/oracle-diagnostics`):

- `--oracle-rom local-data/zx-diagnostics-v0.37-48k.rom --settle 1500 --keys 1 --frames 4000`
  printed `frames=5510 T=385082880 PC=0943 border=7 FramebufferSHA256=AD984EDADD28FD09C4E351DC72D78A48278FDE50E3201D1C9CE92FA143863723`. The
  scripted key selected 48K and the screen reported `Lower 16K RAM tests`, `Upper RAM
  Walk/Inversion/March/Random` and `Testing interrupts` all `PASS`, then
  `### Testing Completed ###`. Without a key the ROM completes after its own 20-second fallback
  (`No selection made, assuming 48K mode.`) with the same PASS results.
- `--oracle-rom local-data/opense-3.1.2.rom --frames 2000` printed `frames=2100 T=146764801
  PC=10A6 border=0 FramebufferSHA256=69DCE375951CBF8D529B96451429AE62CB40F8D46E963F7A86EDA78B6C347D09`
  with the ROM's own banner `© 1981 Nine Tiles Networks Ltd`; two runs produced the same hash.
- A missing ROM path returned exit 1 and `--keys zzz` returned exit 2.
- `--oracle-fuse local-data/tests.in local-data/tests.expected` runs the Fuse single-instruction
  corpus on a flat 64 KiB bus and reported `tests=1335 passed=1324 failed=3 knownXyDivergence=8
  skipped=0`. The three failures are the undocumented X/Y bits after SCF (`37_1`) and CCF (`3f`)
  and the HALT PC convention (`76`); the eight `knownXyDivergence` entries are the `BIT n,(HL)`
  cases the reference integration blacklists as "FUSE handles those wrong". Contention events
  (MC/PC) and bus read/write events are not compared: this machine has no contention model.
  The runner exits non-zero while those three remain open.
- `--oracle-smoke` runs the same report path on a self-authored dummy ROM (no external asset) and
  prints `frames=60 T=4193280 PC=FF00 border=0 FramebufferSHA256=4D856B7A9008759AFF68A0D1CDE95B895FB952761719549F339D00D6EDA9F543`;
  CI runs it so the command cannot rot. It is a tool-path check, not firmware and not accuracy
  evidence.
- Manual check on OpenSE BASIC: a self-authored guest program calling the ROM's print entry
  (`CALL 0x0010` -> `JP 0x15F2`) printed a character and scrolled the screen, and one 6 KiB scroll
  cost about two frames, matching real hardware. The ROM also reports `ERR_NR` OK after an ENTER.
  These are session observations with a host-side guest program, not a committed suite.

ULA contention, floating bus, exact raster timing and AY are outside both oracles and remain
unverified. The diagnostic ROM's `/M1 bad?` line reports its absent diagnostic board, not this
machine.

## Determinism and state
Same firmware/profile/state, events and T-state budget must produce the same register/RAM/frame/
audio results. Test save/restore followed by equal replay, including tape pulse cursor and audio
phase. Snapshot import/export tests also assert known bytes/fields; two reciprocal bugs can pass
a round trip.

## Performance
Benchmark in Release with trace disabled and fixed input/budget; report hardware/runtime/commit.
Measure CPU-only and complete-machine throughput separately. Do not remove accuracy or sampling
events to improve a benchmark. Benchmarks are planned, not present in this foundation.

001g adds all 65,536 NEG AF inputs, fetch/refresh wrap, waits and payload failure.
At 001g, unsupported ED coverage rejected 250 remaining payloads, including aliases.

001h checks 196,608 ordinary CB encoding/operand/flag vectors across 256 cases,
plus 1,048,576 BIT (HL) operand/WZ-high/carry vectors across eight cases.
All 256 payloads, memory read/idle/write order, prefix/refresh wrap, waits/live
sampling, EI/NMI atomicity, faults and a synthetic CB program are covered.
WZ checks cover existing load/arithmetic/control/return/interrupt writers,
non-writer preservation and reset, history observed by BIT and partial failures.
The old first-byte CB rejection was replaced by positive page coverage; the four other unsupported base-byte cases remain explicit negative tests.

001i adds 393,216 indexed CB value/carry/flag vectors across 512 cases,
6,291,456 indexed ALU operand-pair/carry vectors across 48 cases and 786,432
indexed byte INC/DEC value/flag vectors across 12 cases. Two word sweeps add
262,144 INC/DEC and 917,504 ADD carry-boundary vectors. Literal tables cover all
85 affected and all 163 ignored base encodings for each prefix. Additional cases
cover all displacements, index halves, stack/address/refresh wrap, WZ, ED
cancellation, mixed/repeated prefixes, EI/HALT/NMI boundaries, waits/live reads,
partial-cycle failures, ignored ROM writes, the prefix-only execution bound and
a self-authored index program. The ignored-prefix comparison supplements literal
expectations and existing base suites; it is not an external independent oracle.

Indexed-slice evidence: [CI 37754791932](https://github.com/Geraskin/rockula/actions/runs/37754791932),
1,382 tests passed on each of Windows/Linux, zero failures/skips/warnings/errors,
both demos and Linux format passed. [001i](plans/001i-index-prefixes.md) records
red/green commits and remaining verification limits.

001j covers eight ED ADC/SBC encodings with 5,769,216 independently calculated
word operand/carry/flag vectors (all HL values, seven boundary operands, HL
self-operand and incoming flag sweeps). I/R checks execute 524,288 value/flag/IFF2
vectors; nibble rotations execute 262,656 A/memory/carry/flag vectors with literal
bus traces. Eight word-transfer cases check all 256 F values and wrapped accesses.
Wait/live reads, ignored ROM writes, prefix cancellation, boundary interrupts,
partial-cycle failures and a 147-T synthetic data program supplement existing
suites. At 001j, explicit unsupported tests rejected 228 remaining ED payloads.
The LD A,I/R interrupt anomaly, electrical sequencing and external exercisers
remain unverified; [001j](plans/001j-ed-data-operations.md) records the limits.

ED data-slice evidence: [CI 37765521189](https://github.com/Geraskin/rockula/actions/runs/37765521189),
1,418 tests passed on each Windows/Linux platform, zero failures/skips/warnings/errors,
both demos and Linux format passed. [001j](plans/001j-ed-data-operations.md)
records test-first and verified commits.

001k adds 1,180,160 immediate/register port address/value/flag vectors,
3,147,776 memory transfer/search A/data/count/carry/flag vectors and 3,672,064
block I/O B/data/C-or-L-boundary/flag vectors. All 32 new ED and both new base
encodings are checked. Repeated PC-high X/Y sweeps and Banks H/PV relations are
independent expectations, not values generated by CPU helpers. Literal event
traces verify input/output B address ordering and 16/21-T sequencing. Wait/live
reads, ignored ROM/port writes, device failure after a retained write, staged
faults, EI/NMI between iterations, self-modified code and PC/HL/DE wrap are covered.
Zero-count programs execute all 65,536 transfer iterations and all 256 input
iterations through bounded Steps; a synthetic copy/search/output program takes
17 steps and 234 T, with each step checked against a literal duration list.
At 001k, explicit unsupported checks rejected 196 ED payloads and the two SCF/CCF
base bytes. Ignored index-prefix coverage then covered 165 encodings per prefix.
[001k](plans/001k-ports-and-blocks.md) records sources, corrections and CI evidence.

Port/block-slice evidence: [CI 38331001314](https://github.com/Geraskin/rockula/actions/runs/38331001314),
all 1,486 tests passed on both Windows/Linux, zero failures/skips/warnings/errors,
both demos and Linux format passed. [001k](plans/001k-ports-and-blocks.md) records
test-first, port-only and verified commits and source discrepancies.

## First snapshot player evidence
001l replaces obsolete unsupported-byte assertions with independent SCF/CCF Q,
ED alias/NOP and IM0 RST checks. It adds strict SNA length/stack/register/ownership
and atomic-import cases, ROM protection, all keyboard half-rows and aliases,
Kempston/latches, bitmap layout, bright/FLASH, frame overshoot and interrupt phase.
A self-authored SNA executes actual input/output/video paths without external assets.

Test-first CI 38333703020 produced 69 expected failures and 1,477 passes. Host/core
[CI 38334641862](https://github.com/Geraskin/rockula/actions/runs/38334641862)
passed all 1,546 tests on Windows and Linux with zero warnings/errors/skips;
three headless demos and Linux format passed. Linux Xvfb smoke opened the native
Avalonia window, executed three frames and verified the uploaded bitmap pixel.
This smoke does not prove file-picker behavior, interactive keys or a real game.
Real firmware boot, interactive Windows acceptance and game compatibility remain pending.

## Audio and USB player evidence
[CI 38342608249](https://github.com/Geraskin/rockula/actions/runs/38342608249),
code commit a188d587feb752d7b128a4a8658cb8cccba934c7: 1,565 Core plus 13 Desktop
tests passed on Windows/Linux (1,578 total, no failures/skips, zero build warnings/errors).
Independent tests cover rational PCM pulse area, fractional counts, DC/reset/ring
bounds and bus-transfer timing; Sinclair rows/keyboard overlap; raw HID mapping;
worker command acknowledgement, pause/restart/fault/close, output fallback and
real synthetic guest-tone/mute delivery. Headless demos and Linux format passed.
Native dummy-audio/virtual-joystick smoke passed on both platforms; Linux Xvfb
verified worker-frame presentation and actual queued PCM. Virtual input and dummy
audio do not establish real game/device compatibility or audible quality. See
[001m](plans/001m-audio-and-usb-joystick.md) and [playing](playing.md) for limits.
