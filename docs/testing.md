# Verification strategy
Tests cover ROM validation, registers/reset, all 250 supported base encodings, exhaustive
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
Explicit unsupported checks now reject 196 ED payloads and the two SCF/CCF base
bytes. Ignored index-prefix coverage grows to 165 encodings per prefix.
[001k](plans/001k-ports-and-blocks.md) records sources, corrections and CI evidence.
