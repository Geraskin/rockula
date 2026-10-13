# 0016: External oracle suite for the 48K machine
Status: accepted. Date: 2026-10-13.

## Context
The 48K machine, snapshot player and CPU slices are verified by self-authored programs and
exhaustive local vectors. Every expected value in that suite is produced inside this repository,
so it confirms our reading of a contract but cannot expose a shared misunderstanding between the
implementation and its own tests. [Work order 001](../plans/001-z80-foundation.md) asks for
independent expected results and roadmap M2 gates on an "external oracle/exerciser" report.

Real firmware boot was recorded as unverified. Booting a user-supplied replacement firmware
(OpenSE BASIC 3.1.2, GPL-2.0) reached the ROM's print and keyboard loop with a correct
framebuffer, attributes, `FRAMES` counter and interrupt count, but the remaining editor
behaviour could not be classified as correct or defective: every available expectation still
came from the implementation under test. A separate check showed the ROM's own print entry
(`CALL 0x0010` -> `JP 0x15F2`) prints and scrolls correctly, and each 6 KiB scroll costs about
two frames, as on real hardware. The uncertainty was in the *reference*, not in the code.

## Decision
Add an explicitly-invoked external oracle suite to the headless host:

```
dotnet run --project src/RockULA.Headless -c Release -- \
  --oracle-rom ROM [--settle N] [--keys SEQ] [--frames N] [--png PATH]
```

It boots one user-supplied 16 KiB ROM into the existing `Spectrum48Machine`, applies a bounded
key script, runs a bounded frame budget, renders the final 320x240 framebuffer and reports
frames, T-states, PC, border colour and the framebuffer SHA-256, optionally writing a PNG.

Two third-party oracles are adopted; neither is vendored:

| Oracle | License | Role |
| --- | --- | --- |
| zx-diagnostics v0.37 48K test ROM (Dylan Smith / Brendan Alford) | LGPL-3.0 | functional RAM/ULA/keyboard/sound/video/Z80 PASS/FAIL |
| OpenSE BASIC 3.1.2 (Andrew S. Owen) | GPL-2.0 | redistributable drop-in firmware so a ROM boot can be exercised without the Sinclair ROM |

Assets stay in the ignored `local-data/`; the repository commits only the manifest (name,
source, revision, license, SHA-256, scope) and reproducible acquisition instructions. The suite
is not a CI job and never fabricates a pass: a missing ROM is an unavailable input reported on
stderr with a non-zero exit code, consistent with the external-asset policy in
[testing](../testing.md) and [licensing](../licensing.md).

## Alternatives
- **Commit the oracle ROM and run it in CI.** Rejected: the diagnostic image is LGPL-3.0 and the
  firmware is GPL-2.0; shipping them inside an MIT repository needs its own review, and CI must
  not download firmware.
- **Freeze golden framebuffer hashes as the accuracy gate.** Rejected as a primary gate: those
  values are produced by the implementation under test, so they pin regressions only.
- **Write an in-repository reference Z80 or ULA.** Rejected: a second implementation by the same
  authors shares the same blind spots and buys little for the cost.
- **Depend on a reference emulator executable.** Rejected for this milestone: provenance and
  runner assumptions are unresolved, and the machines/languages differ.

## Consequences
Benefits: the machine can now be judged by an unrelated program; the ROM-boot path has a
reproducible, redistributable fixture; M2 gains an external report instead of an internal one.
Costs and limits: the run is manual, so it is not a regression gate; the framebuffer hash is
deterministic per ROM/keys but is not accuracy evidence; zx-diagnostics exercises the 48K
functional profile and does not cover ULA contention, floating bus, exact raster timing or AY,
which remain M6/M7 scope. Unresolved: whether to add raxoft/z80test (`z80ccf`, `z80memptr`) as a
CPU-level oracle for undocumented flags and MEMPTR, and whether any redistributable firmware
could be pinned for CI without weakening the no-download rule.

## Verification
- `--oracle-rom local-data/zx-diagnostics-v0.37-48k.rom --settle 1500 --keys 1 --frames 4000`
  reaches `### Testing Completed ###` with the 16K/upper RAM tests and the interrupt test at
  `PASS`, and the ROM records the key as an explicit 48K selection.
- `--oracle-rom local-data/opense-3.1.2.rom --frames 2000` prints the OpenSE banner and reports
  the same framebuffer hash across repeated runs with the same inputs.
- A missing ROM path and an unknown `--keys` token both return non-zero with a clear message.
- Results and the exact commands are recorded in [testing](../testing.md#external-oracle-suite).
