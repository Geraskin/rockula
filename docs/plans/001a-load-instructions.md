# Work order 001a: first executable Z80 load slice
Status: in progress. Branch: `feat/z80-foundation`; base/merge target: main at
`9806310c19552c4e845d564ebf765a51e82f851f`.

## Outcome and scope
Implement the register file, synchronous CPU stepping, timed bus transactions, NOP and 83
unprefixed LD encodings: register/register or (HL), immediate 8/16-bit, BC/DE accumulator
indirection, absolute A/HL transfers. LD SP,HL is deferred with internal-cycle details.
This is 84 of 256 base-byte encodings, not a complete CPU or Spectrum machine.
No arithmetic, branches, stack, HALT, interrupts, prefixes, ULA or game loading in this slice.
The desktop welcome text is updated for truthful status only; no new GUI functionality.

## Source
Zilog UM008011-0816: register/refresh discussion (printed pp. 2–3), fetch/memory timing
(pp. 7–10), 8-bit loads (pp. 71–97) and the applicable unprefixed 16-bit loads
(pp. 99, 102, 107). [Manual](https://www.zilog.com/docs/z80/um0080.pdf).
Read the specified instruction entries and clock diagrams, not an upstream implementation.
Register/nominal-cycle expectations are manually specified independently in tests.
Exact electrical edge phase, M1 refresh address events and undocumented WZ are not claimed here.

## Contracts
See [ADR 0004](../decisions/0004-initial-cpu-bus.md). Bus owns its absolute clock. Each operation
has kind, address, nominal duration, logical transfer offset and optional write data.
A bus advances before transfer and after transfer; inserted waits shift both points.
CPU reset resets CPU state/fault latch, not RAM or the bus clock.

The deterministic initial register policy is all zero except SP=0xFFFF, IFFs=false, IM=0.
This is an emulator initialization choice, not a claim about unspecified silicon power-on values.
Untimed memory inspection belongs to a bus adapter and does not advance hardware time.
A failed instruction is terminal until explicit CPU reset; it is not skipped.

## Files and order
1. Add this plan and ADR.
2. Add independently derived tests plus API scaffolding whose execution/reset paths throw;
   publish a draft PR and verify semantic failures in CI.
3. Implement pair views/reset, bus transfer timing and instruction decoding.
4. Add an authorized, self-authored six-instruction CLI demo, a bounded smoke check and opcode
   coverage/status documentation. Keep ordinary --about truthful.
5. Run CI on Windows/Linux, inspect logs, correct any compile/format/test failures without
   weakening expectations, and report remaining scope.

## Regression evidence
Every supported encoding: destination/source state, preserved F, PC/R and nominal time.
Additionally: both main/alternate pair views, byte order, memory at 0xFFFF, PC wrap,
H/L source-address preservation, operand reads not incrementing R, wait-shifted data sampling,
write visibility, unsupported opcode/prefix failure with no subsequent execution, and reset.
Tests require no original ROM or network access.

## Acceptance
Release build, xUnit tests, formatting and headless --demo. Demo final state:
PC=000C, A=2A, B=03, HL=4000, R=06, RAM[4000]=RAM[4001]=2A, 51 T-states.
Only nominal transaction ordering and declared logical transfer offsets are verified; no
Spectrum contention, electrical pin-edge accuracy, complete NMOS quirks or game compatibility.

## Red-phase evidence
[CI run](https://github.com/Geraskin/rockula/actions/runs/37720384562), commit
`499ebcc4b9ece424049d668efd7e21e760188007`: build succeeded on Windows/Linux;
tests failed as intended. Linux recorded 119 failed, 8 passed, 0 skipped out of 127.
Failures include unsupported execution scaffolds and uncoupled pair/byte views.
No test expectations are weakened for the implementation.
