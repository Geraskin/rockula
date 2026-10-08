# Formats instructions
Root AGENTS applies. Read docs/architecture/formats.md and contracts.md.
Formats receives host-supplied input and returns validated portable data; it does not perform
machine execution or commit a partial load. Core installs state atomically after validation.
Check lengths, arithmetic, output size, supported variants and decompression/control-flow budgets.
Do not serialize native structs or assume host endianness. Unsupported variants are clear errors.
No snapshot/tape parsers exist at bootstrap; introduce them only with the relevant milestone.
