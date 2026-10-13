# Formats instructions
Root AGENTS applies. Read docs/architecture/formats.md and contracts.md.
Formats receives host-supplied input and returns validated portable data; it does not perform
machine execution or commit a partial load. Core installs state atomically after validation.
Check lengths, arithmetic, output size, supported variants and decompression/control-flow budgets.
Do not serialize native structs or assume host endianness. Unsupported variants are clear errors.
Sna48Snapshot implements strict fixed-size 48K SNA. Preserve stack bytes and copy input RAM.
Z8048Snapshot supports v1 and ordinary 48K v2/v3 with bounded RLE and exact pages
8/4/5. ISpectrum48Snapshot is a read-only external import contract, not an internal
save state. Other models/tapes remain deferred and require independent tests.
