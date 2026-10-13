# Playing a 48K SNA

Install the stable .NET 10 SDK and use branch `feat/z80-foundation` until PR #6
is merged. From the repository root:

```sh
dotnet run --project src/RockULA.Desktop
```

1. Click **Load ROM** and select your authorized 16,384-byte 48K firmware image.
2. Click **Open 48K SNA** and select a 49,179-byte snapshot. It resumes automatically.
3. Use the game's menu to choose keyboard controls or Kempston (arrows and Space).
4. Use **Run / Pause** and **Restart**. Restart restores the loaded snapshot;
   loading a ROM clears that snapshot, so Run boots the ROM instead.

Shift maps to CAPS SHIFT, Ctrl to SYMBOL SHIFT. Focus loss and pausing release
held keys. Invalid files leave the previous machine paused and show an error.
ROM writes are protected. Files and assets are never downloaded automatically.

Only fixed-size 48K SNA is supported. SNA does not contain firmware. Z80, TAP/TZX,
128K and compressed archives are unsupported. Keep user assets in ignored `local-data/`.
There is no sound. Video redraws final RAM once per logical frame; contention,
raster effects and floating bus are absent. Host pacing may run slowly on busy machines.

For a reproducible bounded run:
```sh
dotnet run --project src/RockULA.Headless -- --sna local-data/48.rom local-data/game.sna --frames 100
```
The command reports cycles, PC and the final framebuffer SHA-256. Errors return a
nonzero exit code. `--demo-machine` checks a self-authored SNA without external assets.

Real ROM boot, game compatibility and interactive Windows file/input handling
are pending user-file acceptance; synthetic tests do not establish those results.
