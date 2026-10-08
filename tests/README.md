# Tests
`RockULA.Core.Tests` verifies 16 KiB ROM input plus the first Z80 NOP/load slice.
`Cpu/` contains independently specified encoding/state/bus vectors and a synthetic program.
Add arithmetic, control flow and machine tests in later slices. Add Formats/compatibility
projects only when those implementations land.
Run `dotnet test RockULA.slnx -c Release`.
External data belongs in ignored local-data with reviewed provenance, not in this directory.
See [verification strategy](../docs/testing.md).
