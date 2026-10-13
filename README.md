# RockULA!
**Rock Your Spectrum.**

A ZX Spectrum emulator written from scratch in C#, with a deterministic hardware core
and an Avalonia desktop interface. Original project code is licensed under MIT.

**Status: first functional Spectrum 48K snapshot player in PR #6.**
The original Z80 implements 252 standalone base encodings, all CB/indexed-CB
payloads and all ED payloads (78 semantic encodings and 178 hardware NOPs),
including SCF/CCF flag history. Interrupts support NMI, IM 1/2 and IM 0 RST.
The machine provides protected ROM, RAM, keyboard/Kempston, frame interrupts and
final-RAM bitmap/attributes/FLASH/border. Desktop supports ROM/SNA selection,
run/pause and restart; headless supports bounded snapshot runs.
Desktop execution has a background machine owner, queued beeper sound with
volume/mute, and configurable USB game HID axes/hat/buttons. Controls selects
Kempston or Sinclair number-key rows and can rebind keyboard joystick aliases.

This is a prototype: no tape, Z80 snapshots, 128K, contention, raster border
or floating bus. Real firmware boot and game compatibility still require testing
with user-supplied files on the target desktop. External CPU exercisers remain pending.

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
dotnet run --project src/RockULA.Headless -- --demo
dotnet run --project src/RockULA.Headless -- --demo-loop
dotnet run --project src/RockULA.Desktop
```

The last command opens the snapshot player. Linux needs Avalonia's native display dependencies;
the dev container supports builds and headless tests, not a configured GUI session.
For games, click **Load ROM** (a 16,384-byte Spectrum 48K ROM), then
**Open 48K SNA** (a 49,179-byte `.sna`). Execution starts automatically.
Letters/numbers map to Spectrum keys; Shift is CAPS SHIFT, Ctrl is SYMBOL SHIFT.
Choose Kempston in the game's controls menu for arrows and Space (fire).
For USB, open **Controls / USB**, refresh/select your joystick and check the live
preview. Configure axes, hat, fire, direction buttons and dead zone as needed.
Use the same Kempston/Sinclair selection in the game and host. Volume/mute is in
the main toolbar. Settings are currently session-only.
**Run / Pause** pauses; **Restart** restores the originally opened snapshot.
See [playing instructions](docs/playing.md) for diagnostics and limits.

No external assets are required for tests or self-authored demos:
```sh
dotnet run --project src/RockULA.Headless -- --demo-machine
dotnet run --project src/RockULA.Headless -- --sna local-data/48.rom local-data/game.sna --frames 100
```

## Repository map
| Path | Responsibility |
| --- | --- |
| `src/RockULA.Core/` | Z80, functional 48K machine, video/input and ROM validation |
| `src/RockULA.Formats/` | Strict bounded 48K SNA parser; other formats deferred |
| `src/RockULA.Headless/` | CLI, bounded snapshot runner and synthetic demos |
| `src/RockULA.Desktop/` | Avalonia ROM/SNA player with keyboard and pause/restart |
| `tests/` | CPU, bus, ROM, SNA, machine, keyboard and video regressions |
| `docs/architecture/` | Hardware boundaries, timing, host and file contracts |
| `docs/plans/` | Roadmap and executable milestone work orders |
| `docs/decisions/` | Architecture decision records |
| `.github/instructions/` | Path-specific coding instructions |
| `.github/agents/` | Planning, implementation and review agent profiles |
| `.devcontainer/` | .NET development environment without host credentials or privileged mounts |

Start with [AGENTS.md](AGENTS.md), [architecture](docs/architecture/overview.md),
[roadmap](docs/plans/roadmap.md), [opcode coverage](docs/architecture/z80-coverage.md) and
[the current slice](docs/plans/001l-first-snapshot-player.md).
[Development](docs/development.md) explains commands and branching.
[Testing](docs/testing.md) describes what each check can prove.

## ROMs, games and references
Original ROMs, commercial game images and downloaded external test corpora are not bundled.
Users supply their own authorized files for the player. Keep local data in
ignored `local-data/`. A raw ROM is firmware; TAP/TZX and SNA/Z80 are different formats.
Our MIT license covers our code, not third-party files; see
[asset and dependency policy](docs/licensing.md) and [references](docs/references.md).

No upstream emulator has been vendored. Reference implementations may be used as documented
test oracles after provenance review, without silently importing their code.
