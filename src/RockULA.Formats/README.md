# RockULA.Formats
Bounded 48K SNA and ordinary 48K Z80 v1/v2/v3 import. Both own RAM and implement
ISpectrum48Snapshot architectural CPU/RAM/border data. Z80 supports raw/compressed
v1 and exact three-page extended snapshots, rejects incompatible hardware and
malformed runs/blocks, and never changes SP to recover PC. Hosts select by extension
and bound reads; Core installs atomically. Tape/128K/full internal saves are deferred.
See [format contract](../../docs/architecture/formats.md).
