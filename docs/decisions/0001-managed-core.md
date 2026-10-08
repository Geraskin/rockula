# 0001: C# and deterministic host-independent core
Status: accepted. Date: 2026-10-08.

## Context
The creator chose C# and wants original hardware emulation without authoring C/C++.
Desktop usability and inspectability matter; browser execution is a later possibility.

## Decision
Use .NET 10, Avalonia 12.1.3 for the first desktop host, and xUnit v2 for initial tests.
Keep Core platform-neutral, synchronous and independent of wall clocks/host devices.
Keep parsing in Formats, host I/O in Desktop/Headless, and one T-state scheduler per machine.
Use a small code-built welcome shell initially; add view-model structure with actual emulator UX.

## Alternatives
WPF would favor Windows; an existing emulator core would change the from-scratch goal.
A browser-first UI would introduce deployment/audio constraints before CPU correctness.
A shared UI project is deferred until there is a second host.

## Consequences
One core can be tested without a GUI. Hosts need explicit ownership/pacing adapters.
Avalonia can include native rendering dependencies; the project does not promise a fully
managed transitive stack. Browser portability requires its own future validation.

## Verification
Build Core/Formats without GUI packages, run headless tests, and check cross-host deterministic
results when execution exists. Interactive desktop validation is separate from compilation.
