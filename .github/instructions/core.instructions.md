---
description: Hardware-core C# and timing rules
applyTo: "src/RockULA.Core/**/*.cs"
---
Read [Core instructions](../../src/RockULA.Core/AGENTS.md) and
[timing](../../docs/architecture/timing.md).
Guest time uses integer T-states. Sequence device-visible bus effects within instructions.
Use intentional byte/ushort wrapping; no platform I/O, GUI, Task scheduling or wall-clock reads.
Hardware expectations come from independent sources, not implementation helpers.
