# ADR 0015: Portable external snapshots and bounded desktop preferences
Status: accepted, 2026-10-13.

SNA and Z80 implement ISpectrum48Snapshot, a read-only architectural register/RAM/
border contract. Parsing owns RAM and is independent of machine execution. The
worker validates/installs while paused; restart retains the original imported
object. Z80 import never simulates SNA's stack pop. V1/v2/v3 ordinary 48K are
supported with exact expansion and required page coverage; incompatible machines,
paged external ROMs and AY/16K modifiers are explicit failures. The maximum
encoded input covers worst-case legal runs but does not permit unbounded allocation.
V3 phase and emulator configuration do not become full internal save state:
imports retain the documented phase-zero/missing-latch policy, and host controls
remain user preferences. ROM is still supplied by the user.

Host-only preferences use versioned size/depth-limited JSON and validated fields.
Missing/corrupt files fall back visibly; future schemas are read-only to prevent
unintended downgrade. Same-directory temporary replacement preserves prior data
on failed saves. No held input, machine state or runtime instance IDs are saved.
USB GUID plus name selects exactly one current device; identical-device ambiguity
is explicit. Restored ROM is validated and paused; no automatic game execution.
Startup/load/save stay outside machine ownership and do not mutate live hardware.
Native/UI smoke bypasses personal preferences.
