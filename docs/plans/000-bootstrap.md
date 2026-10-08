# Work order 000: repository foundation
Date: 2026-10-08. Target: `main`. Initial bootstrap is authorized directly on main.
Initial repository: one MIT LICENSE commit; main is already the sole default branch.

## Goal
Prepare a public C# project for incremental AI-assisted implementation, without pretending
an emulator already exists. Preserve the existing LICENSE unchanged.

## Deliverables
- Root README, AGENTS, contributor guidance and scoped Copilot instructions/agent profiles.
- Architecture, timing, state/host/file contracts, roadmap, references and ADRs.
- .NET 10 solution: Core, Formats, Headless, Desktop and Core.Tests.
- Immutable 16 KiB ROM-input contract with meaningful rejection/copy-ownership tests.
- Avalonia 12.1.3 welcome shell and headless `--about`/`--help`.
- Nullable/warnings defaults, formatting, CI, dev container and PR/issue templates.
- No downloaded firmware, games, emulator code or external process-skill dependency.

## Verification
1. Parse XML/JSON/YAML and confirm project references/solution paths exist.
2. Check relative Markdown links and accidental binary/user-data additions.
3. `dotnet restore/build/test/format` using commands in AGENTS.
4. Inspect the desktop welcome window when an interactive display is available.
5. Publish one bootstrap commit and verify default branch/ref/files and CI result.

The execution environment used to prepare this bootstrap has no .NET SDK or GUI display.
Static validation is available; runtime/build/UI checks must be reported separately and may run
in GitHub Actions or a .NET-equipped workstation. Do not infer a pass from valid XML.

## Scope limits
ROM validation is not memory mapping or execution. The welcome window and CLI report foundation
status. Formats contains no parser. No Z80/ULA/tape/audio implementation is part of this order.

## Recorded evidence
Bootstrap code commit: `101e810098cbad62159048fe833d4fee67224659`.
[CI run 1](https://github.com/Geraskin/rockula/actions/runs/37719280891) completed successfully
on both ubuntu-latest and windows-latest:
- Restore and Release solution build succeeded.
- Linux build reported 0 warnings and 0 errors.
- Six ROM-input tests passed on both runners; no skipped tests.
- Headless status command succeeded on both runners.
- Linux formatting verification succeeded.
- Static XML/JSON/YAML, relative Markdown links, project/solution references and newline checks passed.
- Remote tree contains all 56 new files plus the original LICENSE with its unchanged blob hash.

Interactive GUI launch, dev-container construction and emulator compatibility are not verified.
This evidence covers the foundation only; the CPU and machine are still unimplemented.
