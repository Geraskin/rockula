# Work order 001o: external oracle suite for the 48K machine
Status: implemented; Windows/real-asset acceptance pending. Authorized continuation,
`feat/oracle-diagnostics`. **Base and merge target: `feat/z80-foundation` (PR #6)**, because
the CPU, machine and headless host this slice extends exist only there; retarget to `main`
once PR #6 lands.

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
CI integration and any committed ROM; automated parsing of the diagnostic ROM's screen text;
ULAContention/floating-bus or sound tests; TAP/TZX; a bundled reference emulator.

## Intermediate evidence
Recorded during implementation (Linux, .NET SDK 10.0.401). Command and observed output are
reproduced in [testing](../testing.md#external-oracle-suite); the raw `--oracle-rom` runs and
their framebuffer hashes are listed there so a reviewer can repeat them.
