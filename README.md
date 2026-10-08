# RockULA!
**Rock Your Spectrum.**

A ZX Spectrum emulator written from scratch in C#, with a deterministic hardware core
and an Avalonia desktop interface. Original project code is licensed under MIT.

**Status: foundation only.** The solution contains a desktop welcome window, a headless
status command, ROM-input validation and its tests. CPU execution, video, sound, snapshots
and tape loading are planned; this version cannot run games.

## Direction
Start with a documented Spectrum 48K hardware profile and get snapshot-based games playable.
Then add faithful tape playback, raster timing, sound and debugger tools. Spectrum 128K/AY
and a browser host are later milestones, not prerequisites for the first playable release.
The CPU and ULA are our own implementations; UI and test infrastructure may use libraries.

## Quick start
Install a stable .NET 10 SDK. `global.json` accepts stable 10.0 SDK feature bands.
Windows is the first interactive target; Linux is also used for headless CI.

```sh
dotnet restore RockULA.slnx
dotnet build RockULA.slnx -c Release --no-restore
dotnet test RockULA.slnx -c Release --no-build
dotnet run --project src/RockULA.Headless -- --about
dotnet run --project src/RockULA.Desktop
```

The last command opens the welcome shell. Linux needs Avalonia's native display dependencies;
the dev container supports builds and headless tests, not a configured GUI session.
No ROM or game file is needed for the current shell or tests.

## Repository map
| Path | Responsibility |
| --- | --- |
| `src/RockULA.Core/` | Platform-independent hardware model; currently ROM validation only |
| `src/RockULA.Formats/` | Reserved project for bounded snapshot/tape parsers |
| `src/RockULA.Headless/` | CLI and, later, deterministic execution/trace tooling |
| `src/RockULA.Desktop/` | Avalonia desktop host; currently a welcome window |
| `tests/` | Unit, format and future compatibility verification |
| `docs/architecture/` | Hardware boundaries, timing, host and file contracts |
| `docs/plans/` | Roadmap and executable milestone work orders |
| `docs/decisions/` | Architecture decision records |
| `.github/instructions/` | Path-specific coding instructions |
| `.github/agents/` | Planning, implementation and review agent profiles |
| `.devcontainer/` | .NET development environment without host credentials or privileged mounts |

Start with [AGENTS.md](AGENTS.md), [architecture](docs/architecture/overview.md),
[roadmap](docs/plans/roadmap.md) and [the next work order](docs/plans/001-z80-foundation.md).
[Development](docs/development.md) explains commands and branching.
[Testing](docs/testing.md) describes what each check can prove.

## ROMs, games and references
Original ROMs, commercial game images and downloaded external test corpora are not bundled.
Users supply their own authorized files when loaders are implemented. Keep local data in
ignored `local-data/`. A raw ROM is firmware; TAP/TZX and SNA/Z80 are different formats.
Our MIT license covers our code, not third-party files; see
[asset and dependency policy](docs/licensing.md) and [references](docs/references.md).

No upstream emulator has been vendored. Reference implementations may be used as documented
test oracles after provenance review, without silently importing their code.
