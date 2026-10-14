# Work order 001b: byte ALU, branches and stack
Status: implemented and verified on Windows/Linux; awaiting PR review/merge. Continues PR #6 on `feat/z80-foundation`,
merge target `main`. Starting commit: `4c6a6d42a23bfbaf6c214a69bb22a7a2d71486c7`.

## Scope
Add 72 ADD/ADC/SUB/SBC/AND/XOR/OR/CP register, (HL) and immediate encodings;
16 byte INC/DEC encodings; JP/JR/DJNZ, CALL/RET/RST and PUSH/POP (50 encodings).
Combined coverage is 222 base encodings. Keep remaining bytes terminal errors.
No 16-bit arithmetic, DAA, rotates, exchanges, ports, HALT, interrupts or prefixes.

## Sources
- Zilog UM008011-0816, printed pp. 115–120, 145–171, 262–287, 292–293:
  [manual](https://www.zilog.com/docs/z80/um0080.pdf), read applicable unprefixed entries.
- Sean Young / Jan, The Undocumented Z80 Documented v0.90, sections 2.2 and 8.4,
  printed pp. 7 and 28–29: [research document](https://datasheets.chipdb.org/Zilog/Z80/z80-documented-0.90.pdf).
  Use result bits 3/5, CP operand bits 3/5, logical parity and arithmetic signed overflow.
  UM008011 contains obvious P/V wording errors in SBC/logic entries; the research table
  distinguishes VF from PF. No code, external vectors or complete manual is imported.
  This is reference-backed behavior for the intended NMOS profile, not newly measured silicon.

## Sequence and files
1. Independent arithmetic oracle uses integer/signed ranges, nibble arithmetic and bit counting.
   Exhaust every byte pair and carry input through immediate instructions; separately test
   every register/(HL) encoding and every INC/DEC value with both carries.
2. Literal condition tables cover both paths, signed relative wrap and DJNZ underflow.
   Check stack wrap, AF flags, high-first pushes, low-first pops, bus waits and faults.
3. Implement private ALU and control helpers in partial Z80Cpu files. No public helper API.
4. Add bounded --demo-loop: sum 3+2+1 through a subroutine, preserve BC on stack, write RAM.
   Keep the original --demo regression. Update status/coverage/CI and PR description.

## Timing decision
See [ADR 0005](../decisions/0005-internal-cycle-scope.md). Internal durations are sequenced
between real transfers, not appended to every instruction. Their addresses are logical labels,
not validated electrical bus addresses. Do not use them as a Spectrum contention oracle.

## Acceptance
Restore, Release build, tests on Windows/Linux, Linux format verification, both CLI demos.
Exhaustive input-pair vectors are looped within tests, not reported as individual xUnit cases.
Record red and green CI evidence. Full NMOS hidden state, WZ/Q, refresh, silicon bus pins,
external exercisers, GUI, Spectrum devices and games remain unverified.

## Red-phase evidence
Commit `5fc38cbface26a9b2008b8afc91e4d66a9b5ce62`,
[CI run 37722630715](https://github.com/Geraskin/rockula/actions/runs/37722630715):
builds passed on Windows/Linux; 139 new tests failed on unsupported execution, 127 existing
tests passed, zero skipped (266 total). The bounded guest-program regression is added with
implementation, bringing the intended suite to 267 cases.

## Green-phase evidence
Implementation commit `1cbfa8eacc2e6f20d72083ee041df2498bbf3b2a`,
[CI run 37722833543](https://github.com/Geraskin/rockula/actions/runs/37722833543):
- Windows/Linux restore and Release build passed, zero warnings/errors.
- All 267 tests passed on each platform, zero failed/skipped.
- 1,048,576 ALU operand/carry vectors and 8,192 INC/DEC value/carry vectors ran inside tests.
- Conditional JP/CALL/RET/JR checked all 256 flag bytes; nominal bus traces checked both paths.
- Both headless demos passed: original 51 T-states; loop PC=0012 A=06 F=00 BC=0000
  SP=8000 R=17 RAM[4000]=06, 23 instructions, 234 T-states.
- Linux formatting verification passed. Local literal encoding list contains 222 unique entries;
  relative documentation links and project XML/global JSON checks passed.

No local .NET SDK was available; execution evidence comes from GitHub Actions logs.
No interactive desktop test, silicon measurement, external exerciser or Spectrum/game check
was performed. Internal pin addresses and remaining instruction families are still deferred.
