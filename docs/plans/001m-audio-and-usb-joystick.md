# Work order 001m: Beeper, worker host and USB joystick
Status: implemented and CI verified; physical-device acceptance pending. Merged to
`main` in PR #6 (merge commit `0a8e744`).

## Goal
48K bit-4 beeper sound; volume/mute; one worker owns all machine mutation;
USB game HID selection with raw axes/hats/buttons (not Xbox-only), configurable
X/Y/fire/dead zone/inversion and optional direction buttons; keyboard and USB combine into Kempston or either
Sinclair half-row. Unplug, focus loss, pause, load and close release input/audio.
No installer/distribution changes requested.

## Sources reviewed 2026-10-13
World of Spectrum 48K original technical research, Port FE speaker bit 4:
https://worldofspectrum.net/faq/reference/48kreference.htm .
Sinclair Interface 2 original manual, explicit number-key mappings:
https://worldofspectrum.net/pub/sinclair/hardware-info/z/ZXInterface2_Manual.html .
SDL2 official APIs: SDL_OpenAudioDevice, SDL_QueueAudio, SDL_GetQueuedAudioSize,
SDL_ClearQueuedAudio, SDL_JoystickOpen/Update/GetAxis/GetHat/GetButton,
SDL_JoystickGetDeviceInstanceID/GetAttached, virtual joystick APIs.
https://wiki.libsdl.org/SDL2/CategoryAudio
https://wiki.libsdl.org/SDL2/CategoryJoystick
Native distribution Ultz.Native.SDL 2.32.10 (SDL zlib license), reviewed NuGet
metadata and upstream license; only Desktop depends on it. No emulator code imported.

## Design
Core integrates constant bit-4 intervals at actual logical bus transfer timestamps.
Integer rational sample windows preserve fraction across frames; area-average
48 kHz mono PCM, deterministic fixed-point DC blocker. Bound pending samples;
headless audio opt-in. This is a digital one-bit approximation, not the analog
EAR/MIC network, contention or an audiophile resampler.

A background thread owns machine/load/pause/restart, sampler and audio delivery.
Acknowledged bounded commands, coalesced immutable input and immutable latest
frame publication; the GUI never reads live CPU/RAM. Audio queue high-water
paces execution; fallback monotonic guest-time pacing when audio is unavailable.
Guest cycles are not removed. SDL audio uses queue mode without an application
callback; reset/close synchronizes with owner. SDL joystick polling lives on UI
thread, raw instance IDs survive device-index changes; explicit refresh/reselection
for new devices and automatic neutral state on unplug. Default axes 0/1, hat 0,
button 0; user can change these in Controls. No raw arbitrary HID report parser.

## Test-first acceptance
Independent PCM pulse/fraction/count/DC/reset/capacity and timed bus write tests;
Sinclair bit/row vectors and keyboard overlap; desktop fake-audio worker ownership,
pause/load/restart/fault/close tests and raw-axis/hat/button mapping boundaries.
CI Windows/Linux build/test/format; native dummy audio and virtual USB joystick
smoke on both; Linux native-window worker/frame/audio smoke. No game assets needed.
Real speakers/USB joystick and interactive controls remain user-hardware acceptance.

## Evidence so far
Test-first commit 389921b313a7018d2be12438eb7ffc5a5943e9e9e4, CI 38340942667: both platforms built
with zero warnings/errors; 15 expected failures, 1,549 passes, zero skips.
Implementation CI 38341772519 passed Core/desktop tests and native Windows device
smoke; Linux exposed the package's actual libSDL2-2.0.so filename. Resolver fix
CI 38341974935 passed native audio/virtual HID and worker-window smoke on Linux;
format verification identified three whitespace and two import-order corrections.
Physical speakers, the user's USB device, real games and sustained latency remain
pending. See docs/playing.md for exact configuration and limits.

## Verified acceptance
Code commit a188d587feb752d7b128a4a8658cb8cccba934c7:
[CI 38342608249](https://github.com/Geraskin/rockula/actions/runs/38342608249)
passed restore, Release builds with zero warnings/errors, 1,565 Core tests and
13 Desktop tests on both Windows and Linux (1,578 total, zero failures/skips).
All three headless demos and Linux format passed. Both native device smokes
queued/consumed/cleared PCM and verified virtual joystick axes, hat, buttons,
remapping and unplug. Linux Xvfb opened the worker-driven Avalonia window,
verified framebuffer upload and queued beeper PCM. The guest-tone/mute test
checks nonzero output, clearing old queued sound and silence without resetting
guest time. No real ROM/game, speakers or physical USB device was supplied;
interactive and sustained physical-device acceptance remains pending.
