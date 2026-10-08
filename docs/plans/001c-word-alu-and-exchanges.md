# Work order 001c: word arithmetic, decimal adjustment and exchanges
Status: tests first; implementation/CI pending. Branch `feat/z80-foundation`, PR #6 to `main`.
Starting commit: `2fb334fcbad9b2525e869b96c38ec22ba2bdcfd4`.

## Scope
Add 23 base encodings: ADD HL,BC/DE/HL/SP; INC/DEC BC/DE/HL/SP; LD SP,HL;
RLCA/RRCA/RLA/RRA; DAA/CPL; EX AF,AF', EX DE,HL, EXX and EX (SP),HL.
Combined coverage becomes 245. Remaining bytes still fault explicitly.
SCF/CCF are deferred until a reference-backed Q/history contract is implemented.
HALT, interrupts, ports, prefixes and Spectrum devices remain future slices.

## Sources
- [Zilog UM008011-0816](https://www.zilog.com/docs/z80/um0080.pdf), printed pp.112,
  124–127, 173–175, 188–189, 198, 201, 205–212: selected unprefixed instructions.
- [Young/Jan, Undocumented Z80 v0.90](https://datasheets.chipdb.org/Zilog/Z80/z80-documented-0.90.pdf),
  sections 2.2, 4.6, 4.7 and instruction tables 8.5/8.6/8.7: result X/Y, high-byte X/Y
  for ADD HL and the Ramsoft/Stefano Donati DAA tables (including unusual input states).
  These establish a scoped intended NMOS behavior, not new silicon measurements.
  Read as hardware references only; do not import implementation code or vector corpora.
- [Andre Weissflog's netlist timing study, 2021-12-06](https://floooh.github.io/2021/12/06/z80-instruction-timing.html),
  EX (SP),HL section: low/high reads, high/low writes, one internal T before writes and two
  afterward. Read the trace, not emulator source. The author notes netlist/model differences;
  this supplements nominal sequencing only and does not certify original NMOS pin behavior.

## Tests before implementation
- Every 16-bit INC/DEC value for each encoding, with unchanged F and unrelated registers.
- ADD HL source encodings, boundary/half-carry/carry values, alias HL+HL, preserved S/Z/PV,
  ignored input carry, high-byte X/Y and nominal bus durations.
- Four accumulator rotations over all A/F bytes: preserve S/Z/PV, clear H/N, outgoing carry,
  circular vs carry input and result X/Y. CPL over all A/F bytes.
- DAA over all A/F bytes; independent literal nibble table and half-carry table plus bit-count
  parity. Also all valid two-digit BCD add/subtract pairs with/without carry/borrow using
  decimal integer expectations, so a mirrored adjustment algorithm cannot be the only oracle.
- Exact exchange state, SP wrap, stack transfer order/waits/partial failure; LD SP,HL flags.

## Implementation and timing
Private partial CPU helper; no per-step allocation or host dependencies. Keep byte ALU tests.
Extend ADR 0005's logical Internal labels: opcode for extended fetch INC/DEC and LD SP,HL;
opcode for ADD HL's 4+3 internal durations; SP+1 then SP for exchange's 1+2 extensions.
No new claim of electrical internal address accuracy, refresh or Spectrum contention.

## Acceptance
Windows/Linux restore, Release build/test, both existing headless demos, Linux formatter.
Update coverage, current work-order links and PR. Record actual red/green CI counts and runs.
No full CPU, save-state, GUI, ROM boot or game compatibility claim.
