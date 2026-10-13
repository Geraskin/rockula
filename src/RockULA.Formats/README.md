# RockULA.Formats
Strict 48K SNA parsing into immutable portable CPU/RAM data. Exactly 49,179 bytes;
little-endian fields, validated stack-derived PC, IM/border and defensive RAM ownership.
Core installs the result atomically. Formats never opens files or runs a machine.
Z80, 128K SNA and tape formats remain deferred.
See [format contract](../../docs/architecture/formats.md).
