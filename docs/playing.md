# Playing a 48K snapshot

Install the stable .NET 10 SDK and check out `main`; the player landed in PR #6
(merge commit `0a8e744`). From the repository root:

```sh
dotnet run --project src/RockULA.Desktop
```

1. Click **Load ROM** and select your authorized 16,384-byte 48K firmware image.
2. Click **Open snapshot** and select a 48K `.sna` or `.z80`. It resumes automatically.
3. Use the game's menu to choose keyboard controls or Kempston (arrows and Space).
4. Use **Run / Pause** and **Restart**. Restart restores the loaded snapshot;
   loading a ROM clears that snapshot, so Run boots the ROM instead.

Shift maps to CAPS SHIFT, Ctrl to SYMBOL SHIFT. Focus loss and pausing release
held keys. Invalid files leave the previous machine paused and show an error.
ROM writes are protected. Files and assets are never downloaded automatically.

48K SNA is exactly 49,179 bytes. Ordinary 48K Z80 v1/v2/v3 is supported, including
compressed RAM. These snapshots contain no firmware. 128K/16K/AY/extension-ROM
models, TAP/TZX and archives are unsupported. Keep user assets in ignored `local-data/`.
Desktop plays the 48K bit-4 beeper. Use toolbar volume/mute. No AY/128K or tape
audio is implemented. Video redraws final RAM once per logical frame; contention,
raster effects and floating bus are absent. Host pacing may run slowly on busy machines.

For a reproducible bounded run:
```sh
dotnet run --project src/RockULA.Headless -- --snapshot local-data/48.rom local-data/game.sna --frames 100
```
The command reports cycles, PC and the final framebuffer SHA-256. Errors return a
nonzero exit code. `--demo-machine` checks a self-authored SNA without external assets.

Real ROM boot, game compatibility and interactive Windows file/input handling
are pending user-file acceptance; synthetic tests do not establish those results.

## USB joystick and controls
Open **Controls / USB**. The machine pauses while you configure it, then resumes
if it was running. Refresh, select the USB game joystick/gamepad, and move it while
watching the live preview. This uses raw SDL joystick input, not Xbox-only mappings.

Indices start at zero. Defaults: horizontal axis 0, vertical axis 1, hat 0,
fire button 0, approximately 24% dead zone. Change/invert axes if needed. Disable
unused sources with -1. The preview lists pressed button numbers; choose the fire
button you prefer. For digital pads exposing directions as buttons, expand the
optional direction-button settings. Axes, hat and direction buttons are combined.

Choose **Kempston**, **Sinclair: keys 6–0**, or **Sinclair: keys 1–5**, matching
the game's menu. Keyboard-only disables joystick aliases. Keyboard joystick
aliases default to arrows/Space and can be rebound by clicking each action then
pressing a key (Escape cancels). Spectrum letter/number keys remain available.

Unplug releases USB directions/fire. Reconnect, refresh and select again.
Focus loss, pause and loading release input. Configuration persists between runs.
SDL supports devices exposed as game joysticks by the OS; arbitrary non-game HID
report protocols are not parsed. Your physical USB joystick still needs acceptance
on your machine; virtual HID smoke proves the host path, not every device.

## Audio and pacing
One worker owns execution, PCM generation and delivery. Audio queue depth paces
production; the GUI displays the latest immutable frame and submits input. If the
audio backend is unavailable or stops consuming PCM, playback falls back to silent
monotonic pacing and reports that state. Pause/restart/load clears pending audio;
mute/volume changes clear previously queued output without changing guest cycles.
The first sampler averages digital bit-4 intervals at 48 kHz and removes DC; analog
MIC/EAR mixing, contention and band-limited reconstruction remain deferred.
Real speakers, sustained playback quality and interactive USB/file controls need
local user-hardware validation.

## Remembered preferences
The desktop stores bounded version-1 JSON in the user's application-data
`RockULA/settings.json` (normally `%APPDATA%` on Windows, XDG config on Linux).
It remembers the validated local ROM path, last snapshot directory, volume/mute,
interface, keyboard aliases and USB mappings. Only one matching GUID/name is
selected automatically; identical-device ambiguity requires explicit selection.
Runtime instance IDs and held input are never saved.

The saved ROM is validated and restored **paused**. A missing/moved/invalid ROM
shows an error; choose Load ROM again. No game or snapshot starts automatically.
Settings changes are debounced and flushed when closing. Invalid JSON uses defaults
with a warning; future-version settings are kept read-only. Save failures are visible
and leave the old file intact. This is preferences, not a game save/rewind feature.

Headless accepts both snapshot types and preserves `--sna` as an alias.
`--demo-z80` executes a self-authored v3 fixture without external assets. SNA/Z80
loading through real dialogs, automatic physical-USB restoration and real-game
compatibility remain local acceptance checks.
