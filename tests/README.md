# Tests
`RockULA.Core.Tests` verifies 16 KiB ROM input and declared Z80 CPU slices.
Independent expectations cover byte ALU flags, conditions, stack, memory/idle traces and waits.
See [testing](../docs/testing.md) and [opcode coverage](../docs/architecture/z80-coverage.md).
Add Formats/compatibility projects only when their implementation and reviewed fixtures arrive.
No firmware, game or external corpus is required by ordinary tests.
