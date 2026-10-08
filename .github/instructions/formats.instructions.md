---
description: Snapshot and tape parser rules
applyTo: "src/RockULA.Formats/**/*.cs"
---
Read [Formats instructions](../../src/RockULA.Formats/AGENTS.md) and
[file contracts](../../docs/architecture/formats.md).
Validate bounded sizes and little-endian fields before producing portable state.
Never mutate a running machine, read arbitrary host paths or skip unsupported fields silently.
Negative tests cover truncation, malformed lengths, overflow and decompression limits.
