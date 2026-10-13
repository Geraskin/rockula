# 0013: First snapshot player and carry history
Status: accepted for work order 001l.

Q is a mutable byte in the register file, initialized to zero. Explicit CPU flag
commits mark an ALU flag generator; value comparison is insufficient. Whole
instruction retirement copies F to Q for generators or clears Q for non-generators.
Prefix fetches never retire Q. POP AF/EX AF and bit RES/SET clear Q even though
AF can change. SCF/CCF consume prior Q; completed HALT fetch/interrupt clears it.
A fault preserves pre-instruction Q with any already committed F/bus effects.

Undefined ED opcodes become two-fetch NOPs, as hardware specifies; this is a
coverage expansion, not a fallback for arbitrary unknown errors. ED aliases
retain RETI notification only for physical ED4D. IM0 accepts RST device bytes;
other bytes fail after acknowledgement without inventing a NOP response.

The first machine is a 48K functional profile: integer frame clock and boundary
INT, final-RAM video and untimed owner input, no contention/raster/audio claim.
The desktop's UI thread is the single owner for a bounded first prototype,
an explicit initial exception to the planned worker/audio-driven host. Timer,
input and file completion handlers serialize mutation; file load creates a
validated candidate before replacing the old machine. Pausing/loading/focus loss
releases keys. Runtime faults pause and surface a message.

SNA imports replace CPU/bus atomically and initialize absent history/phase/device
state by documented policy. Keep on-stack PC bytes unchanged; recover PC/SP,
IFFs and R directly without executing a fake RETN or adding guest T-states.
Require a real user ROM; never present a synthetic test buffer as firmware.
The host reads files with an upper bound before the pure parser. A supplied ROM
can boot; a SNA can resume. Real firmware/game/UI acceptance remains a separate
local gate, not inferred from CI. Sources/tests: [001l](../plans/001l-first-snapshot-player.md).
