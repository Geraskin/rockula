# Tests
`RockULA.Core.Tests` currently verifies 16 KiB ROM rejection and defensive input copying.
Add CPU/machine tests there during M1–M3. Add Formats and compatibility projects only when needed.
Run `dotnet test RockULA.slnx -c Release`.
External data belongs in ignored local-data with reviewed provenance, not in this directory.
See [verification strategy](../docs/testing.md).
