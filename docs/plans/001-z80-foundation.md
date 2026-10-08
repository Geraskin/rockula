# Work order 001: Z80 state, timed bus and first instruction slices
Status: first slice implemented and verified in [work order 001a](001a-load-instructions.md); broader M1 is incomplete. Suggested branch: `feat/z80-foundation`.
Base/merge target: current `main`. Read AGENTS and architecture/timing before coding.

## Outcome
A CPU can execute independently tested initial instruction slices against a synthetic test bus.
No Spectrum devices or firmware boot are required. Produce a coverage matrix and an explicit
unsupported-opcode error for anything not implemented yet.

## Sources and decisions
Read Zilog UM0080's register, instruction-fetch/memory/I/O timing, flags and instruction sections.
Choose NMOS behavior explicitly. The manual is insufficient for undocumented flags, WZ and
prefix quirks; keep those deferred with references until M2.
Decide phase convention and visibility sampling points in a short ADR before designing the bus.
Use independent expected results, not another port of our own ALU.

## Sequence and files
1. Add `src/RockULA.Core/Cpu/` state and flag definitions; expose byte/pair views consistently.
   Test pair round-trips, alternate sets, reset contract and 16-bit address wrapping.
2. Add minimal synchronous timed bus operations under `Cpu/` or `Machines/`.
   Implement a test-only flat 64 KiB bus and operation recorder in `tests/RockULA.Core.Tests/`.
   Separate opcode/M1, memory, port, interrupt acknowledge and internal idle operations.
   Demonstrate that wait insertion changes total time and sampling order inside an instruction.
3. Add fetch/decode for NOP, immediate/register LD and 16-bit immediate LD first.
   Check PC, byte ordering, R updates and event traces. Unsupported opcode reports original PC,
   prefix bytes and context; never quietly executes NOP.
4. Add ALU helpers and register/immediate arithmetic in focused slices. Exhaustively test 8-bit
   input pairs where practical; independently derive S/Z/H/PV/N/C and applicable X/Y flags.
   Keep arithmetic overflow intentional; include INC/DEC carry preservation and boundary cases.
5. Add relative/absolute branches and stack/call/return slices. Cover both branch paths, signed
   displacement, PC/SP wrap, push/pop byte order and conditional timing.
6. Add remaining unprefixed families incrementally, including exchanges, rotates, DAA, ports,
   DI/EI/HALT. Do not label M1 complete until implemented slices and deferred families are explicit.

No single PR has to finish the whole unprefixed table. Land coherent slices with coverage/status
updates; M2 handles all prefixes and deeper interrupt/undocumented cases.

## Important edge cases
- Prefix fetches update R differently from ordinary operand reads; never increment on every byte.
- R's bit 7, 16-bit little-endian accesses at 0xFFFF, and signed index/branch displacement.
- Conditional instructions have different timings and may still read operands when untaken.
- Internal cycles have addresses/phases relevant to future contention; do not model all as
  addressless trailing delay without checking the source.
- EI delay/HALT are state transitions, not just flags in a register DTO.
- DAA and flags need independent tables; parity and signed overflow are different operations.

## Acceptance
Run solution restore/build/test/format and focused CPU tests. Every implemented opcode has at
least semantic evidence and nominal timing/bus evidence; conditional variants exercise both paths.
Record test/vector counts, exceptions, actual runner output and source sections in the PR.
Show a short deterministic trace of a synthetic load/arithmetic/branch/stack program.
Do not claim CPU completeness, game compatibility or cycle-accurate ULA behavior.

## Not in this order
Avalonia changes, real ROM boot, snapshot/tape loaders, ULA rendering, host audio, contention
tables, 128K, browser packaging, generic dependency injection/device plugins.
