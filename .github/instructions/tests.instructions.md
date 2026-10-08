---
description: Independent emulator test evidence
applyTo: "tests/**/*.cs"
---
Read [test instructions](../../tests/AGENTS.md) and [testing strategy](../../docs/testing.md).
Use independently derived expectations and self-authored fixtures. Do not require firmware or
network downloads in ordinary CI. Separate semantic, bus timing and compatibility evidence.
Do not add skipped placeholder tests or infer compatibility from bootstrap tests.
