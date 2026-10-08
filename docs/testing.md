# Verification strategy
Tests cover ROM validation, registers/reset, all 248 supported base encodings, exhaustive
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
