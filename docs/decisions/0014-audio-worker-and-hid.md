# ADR 0014: Worker-owned playback and SDL audio/joysticks
Status: accepted, 2026-10-13. Supersedes ADR 0013's interim UI-thread owner.

The first playable host needs beeper delivery and generic USB game HID rather than
only standardized Xbox controllers. Use SDL2 via a pinned native NuGet dependency
and a small ABI boundary confined to Desktop. Avalonia continues owning the window;
SDL owns only audio/joysticks, no secondary video loop.

Core's opt-in sampler integrates bit-4 intervals at bus transfer times, with rational
integer sample phase and bounded PCM. Area averaging and fixed-point DC removal
are explicit approximations; bit-3 analog coupling/tape/AY remain deferred.

One background owner processes bounded acknowledged commands and coalesced input
at frame boundaries. Published frames are immutable arrays. Audio queue depth
paces production; unavailable audio falls back to monotonic guest-time pacing.
Pause/load/restart/fault/close clear queued output and logical input. Volume/mute
changes delivery only. A UI thread polls SDL raw axes/hat/buttons into immutable
input; instance IDs identify devices and unplug returns neutral. Configurable
axes, inversion, dead zone and fire allow generic game joysticks without an SDL
GameController mapping database. Arbitrary non-game HID reports are out of scope.

Machine execution and audio I/O cannot touch Avalonia widgets. SDL output access
is serialized with disposal; native handles close after the worker acknowledges
shutdown. Native failures leave usable keyboard/silent playback and visible status.
