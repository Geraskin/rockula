# Work order 001o: external oracle suite for the 48K machine
Status: implemented; Windows/real-asset acceptance pending. Authorized continuation,
`feat/oracle-diagnostics`; merged into `feat/z80-foundation` as PR #7, which PR #6 then
carried to `main` (merge commit `0a8e744`), because the CPU, machine and headless host
this slice extends existed only there.

## Goal
Let an independent, third-party program judge the machine, instead of only our own tests. Every
expected value in the existing suite is produced inside this repository, so a shared
misunderstanding between the implementation and its tests is invisible to it. M2 requires an
external oracle/exerciser report and 001 requires independent expected results.

## Scope and sources
Add one explicitly-invoked headless command that boots a user-supplied 16 KiB ROM into the
existing `Spectrum48Machine`, applies a bounded key script, runs a bounded frame budget, and
reports deterministic output. Reuse the existing machine, video and input paths; no new device
model, no contention, no timing change.

Oracles selected (details and hashes in [testing](../testing.md#external-oracle-suite) and
[references](../references.md)):

| Oracle | Source | License | Role |
| --- | --- | --- | --- |
| zx-diagnostics v0.37 (48K test ROM) | [brendanalford/zx-diagnostics](https://github.com/brendanalford/zx-diagnostics) release v0.37, `testrom.bin` | LGPL-3.0 | RAM, ULA, keyboard, sound, video and Z80 functional PASS/FAIL |
| OpenSE BASIC 3.1.2 | [knaxly-dev/zed-open-roms](https://github.com/knaxly-dev/zed-open-roms) mirror of Andrew S. Owen's GPL firmware | GPL-2.0 | redistributable drop-in 48K firmware for the ROM-boot path |

No oracle asset is committed. Assets live in the ignored `local-data/`; the repository records
the manifest and acquisition instructions only. Dowloading stays a manual user step; CI never
fetches them.

## Command behaviour
```
dotnet run --project src/RockULA.Headless -c Release -- \
  --oracle-rom ROM [--settle N] [--keys SEQ] [--frames 1..100000] [--png PATH]
```
- `ROM` is read with a 16384-byte bound and passed through `RomImage.FromSpectrum48`.
- `--settle N` runs N frames (default 100) before any key is applied, so a ROM can reach its
  prompt; it is bounded to 0..100000.
- `--keys SEQ` applies a bounded key script: digits `0`-`9`, `enter`, `space`, `h`, `p`, `r`,
  `i`, `n`, `t`. Each key is held for 8 frames and then released, so interrupt-driven key scan
  can observe it; an unknown token is a usage error (exit 2), never silently ignored.
- The run ends after `--frames` frames, renders the 320x240 framebuffer, and prints frames,
  T-states, PC, border colour and the framebuffer SHA-256 as `result` on stdout. `--png` writes
  the framebuffer for visual inspection.
- A missing, oversized or unreadable ROM is reported on stderr and returns exit 1; it is never
  reported as a pass.

The command is a **manual/explicitly-invoked suite**, not a CI job: the oracle ROM is not
redistributable, so a CI run could only pass by shipping an asset we may not ship. Per
[testing](../testing.md) an unavailable input is reported as unavailable, not as zero tests
passed. The framebuffer hash is a determinism/regression pin for a *given* ROM and key script;
it is not accuracy evidence, because it is produced by the implementation under test.

## Acceptance
- `dotnet restore/build/test/format` on the solution.
- The command with the zx-diagnostics ROM reaches `### Testing Completed ###` with every listed
  RAM and interrupt check at `PASS` after a scripted `--keys 1` press (`--settle 1500`), which
  the ROM reports as an explicit 48K selection rather than its 20-second fallback.
- The command with OpenSE BASIC boots to its banner and the ROM's own print/scroll path can be
  driven by a self-authored guest program (host code, not committed).
- `--help` lists the command; a missing ROM and an unknown key return non-zero with a clear
  message. Report exact runner output and note that ULA contention, floating bus and AY are
  outside both oracles and remain unverified.

## Not in this order
Committed ROM or corpus assets; automated parsing of the diagnostic ROM's screen text; ULA
contention, floating-bus or sound tests; a tape reader; a bundled reference emulator. The only
CI integration is the asset-free `--oracle-smoke` tool-path check.

## CPU-level oracle
**raxoft/z80test** (MIT, Patrik Rak, v1.2a) was rejected as a runner target: it ships only as
`.tap` plus assembly sources and its `print.asm` hard-codes stock 48K ROM entry points
(`ld a,2; jp 0x1601` CHAN-OPEN and `rst 0x10`), so it needs the copyrighted Sinclair ROM, which
this repository must not bundle or download, and Formats has no tape reader (M5).
**MrKWatkins.EmulatorTestSuites** packages the same families behind C# harnesses but is GPL-3.0
against this MIT repository, so it stays an owner licensing decision and is not referenced.

Instead, `--oracle-fuse` reads the **Fuse Z80 test corpus** with our own MIT reader and compares
registers, T-states and memory for one instruction per test against the corpus's independently
produced expectations. Results are reported as counts; nothing is silently masked.

## Open CPU divergences (never to be closed by loosening a test)
1. `76` HALT: the corpus keeps PC at the HALT address while we advance it past HALT. ADR 0006
   already calls our PC policy "an explicit logical abstraction, not independent electrical-pin
   evidence", and the interrupt return address needs the advanced PC. Resolve only with
   hardware-level evidence; until then the runner reports the divergence instead of masking it.
2. Eight `BIT n,(HL)` X/Y cases are compared with X/Y masked and counted separately because the
   reference C integration (`floooh/chips-test` `tests/z80-fuse.c`) blacklists exactly those
   names as Fuse defects.

Resolved by this work order: `37_1` SCF and `3f` CCF. X/Y were derived from the incoming F but
must come from the flags the instruction produces. Fixed in `Z80Cpu.Misc.cs`, with the two corpus
values added as an independent anchor test; the corpus moved from `failed=3` to `failed=1`.

## Intermediate evidence
- `--oracle-rom local-data/zx-diagnostics-v0.37-48k.rom --settle 1500 --keys 1 --frames 4000`
  passes every listed RAM and interrupt check. Exact output, hashes and the asset manifest are in
  [testing](../testing.md#external-oracle-suite).
- `--oracle-smoke` runs the report path on a self-authored dummy ROM and is wired into CI so the
  command path cannot rot; it is a tool-path check, not firmware and not accuracy evidence.
- The ROM-boot and print/scroll checks on OpenSE BASIC are recorded as manual observations in the
  same testing section; the ROM's own print entry (`CALL 0x0010` -> `JP 0x15F2`) printed and
  scrolled correctly with a ~2-frame cost per 6 KiB scroll.
- `--oracle-fuse local-data/tests.in local-data/tests.expected` reported `tests=1335 passed=1326
  failed=1 knownXyDivergence=8 skipped=0`; the single failure is the open HALT item above.
