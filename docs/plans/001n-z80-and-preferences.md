# Work order 001n: 48K Z80 snapshots and desktop preferences
Status: test-first. Authorized continuation feat/z80-foundation, PR #6 to main.

## Scope and sources
Support ordinary 48K Z80 v1 raw/compressed and v2/v3 page blocks, plus existing
SNA through one immutable portable snapshot interface. Desktop/headless select
format explicitly. No game assets or upstream emulator code.
Read World of Spectrum Z80 format rev1.12 (2004-07-05), 2026-10-13:
https://worldofspectrum.net/faq/reference/z80format.htm . Sections: 30-byte base
header, flag-255 convention, ED ED count/value compression, v1 EOF marker,
extended 23/54/55-byte headers and pages 8/4/5.
SDL2 official SDL_JoystickGetDeviceGUID/GetGUIDString APIs identify persisted USB
preferences; instance IDs remain runtime-only. .NET System.Text.Json and File
APIs are host-only. No additional dependencies.

## Parser policy
Bound input to 200000 bytes, output to exactly 49152 RAM. Explicit little-endian
pairs, AF byte order, independent IFF1/IFF2, split R bit7, IM0–2. V1 PC nonzero;
raw length exact; compressed requires terminal 00 ED ED 00, exact expansion and
no suffix. Zero-count/truncated/overflow runs reject. Extended header lengths
23/54/55 only; hardware mode 0, no modified 16K/AY/paged extension ROMs. Three
unique RAM pages 8->4000, 4->8000, 5->C000, arbitrary order, raw or compressed;
reject duplicate/missing/unknown pages, lengths and trailing blocks. PC may be
zero in extended snapshots. Do not pop/alter SP for Z80.
External imports continue clock/Q/WZ/device initialization policy. V3 raster
counter and emulator configuration (joystick/sync/issue preferences) are not
lossless machine state; declare metadata policy rather than claiming restoration.

## Preferences policy
Versioned bounded JSON in per-user application data/RockULA/settings.json.
Persist selected local ROM path and snapshot directory, volume/mute, interface,
keyboard aliases, raw joystick mapping and GUID/name. No RAM/game paths/assets,
held input or temporary device instance IDs. Restore ROM validated and paused;
missing/moved ROM is a visible recoverable error. Auto-select only one matching
GUID/name; ambiguous devices require explicit selection. Missing USB stays neutral.
Unknown newer schema does not get overwritten. Corrupt files use defaults and a
visible warning. Same-directory temporary write then replacement; failed writes
preserve old settings. Serialize saves, debounce volume changes, flush on close;
GUI/native smoke is isolated from personal settings.

## Acceptance
Publish independent register/RLE/page/negative and preference/schema/identity/file
failure tests before implementation, observe red CI. Windows/Linux restore/build/
tests/demos/format, native GUID virtual HID smoke and Linux worker Z80-window smoke.
Synthetic fixtures exercise desktop worker/headless import; real games, physical
USB identity, file dialogs and audible output remain user-hardware acceptance.
