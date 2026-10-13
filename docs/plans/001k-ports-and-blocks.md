# Work order 001k: Ports and block instructions
Status: independent expectations before implementation. Branch feat/z80-foundation, PR #6 to main.

## Scope and sources
Add immediate IN/OUT (base DB/D3), all 16 ED register-port encodings including
IN (C) and NMOS OUT (C),0, and all 16 ED transfer/search/input/output block
encodings. Total standalone base support becomes 250; ED support becomes 60.
Only SCF/CCF remain unsupported base operations; 196 ED payloads still fault.
Sources read 2026-10-13, hardware documentation/research only:
- Zilog UM008011-0816, printed pp.130–144 and 295–316:
  https://www.zilog.com/docs/z80/um0080.pdf .
- Young/Jan Undocumented Z80 v0.90 §§3.4,4.2–4.5:
  https://datasheets.chipdb.org/Zilog/Z80/z80-documented-0.90.pdf .
- David Banks, Undocumented Z80 Flags rev1.0 (2018-08-21), interrupted block
  instructions: https://zxe.io/depot/documents/technical/Banks,%20David/Undocumented%20Z80%20Flags%20rev1.0%20(2018-08-21)(Banks,%20David)(en)%5B!%5D.pdf .
- Weissflog netlist timing study (2021-12-06), LDI/LDD, CPI/CPD, INI/IND,
  OUTI/OUTD and repeating block sequencing:
  https://floooh.github.io/2021/12/06/z80-instruction-timing.html .
- Boo-boo/Kladov physical-chip MEMPTR research (2006), Zilog NMOS port/block rules:
  https://gist.github.com/drhelius/8497817 .
Important source correction: Young/Jan §4.4 reverses block input/output B timing.
Use Zilog's detailed descriptions and netlist traces: input uses old BC; output
uses decremented B. The older §4.5 LDIR=LDI+rewind flag approximation is refined
by Banks: each nonterminal iteration changes X/Y to ED-prefix PC bits 11/13;
repeated block I/O also modifies H/PV. These changes occur even without an
accepted interrupt and are observable at our Step boundary. B in the repeated
I/O formulas is the post-decrement B. No emulator implementation/corpus imported.

## Contract and implementation
ADR 0012 defines separate PortRead/PortWrite bus kinds, four-T logical transfers
at offset four, default unconnected reads FF/writes ignored and fault boundaries.
Existing enum numbers remain stable. The host owns all port decoding.
Immediate ports take 11 T and preserve F; old A supplies address high byte.
ED register ports take 12 T; IN preserves C and sets result SZXY/parity, H/N=0;
OUT preserves F. Capture original BC before writing a result into B/C.
Block Step executes one iteration (16 T terminal/nonrepeat, 21 T repeating),
rewinding PC by two to ED, including ignored index prefixes and wrapping. Fetch
again next Step so self-modification and interrupts are visible. Initial zero
counts wrap to FFFF/FF. EI inhibits the first iteration, not the whole block.
Flags use independently specified data/B formulas and Banks repeat correction;
WZ follows physical-chip rules, with logical commits specified by the ADR.
Files: Z80Bus/enum, new CPU port/block partials, decoder, test bus/new suites,
positive/negative boundary tables and current status/coverage/agent links.

## Acceptance and limits
Publish test-first expectations and inspect red logs. Verify all new encodings,
input/output address and flags sweeps, all transfer/compare byte pairs, zero/count
and early-match cases, wrapped PC/HL/DE, repeated PC-high X/Y, all B/value with
C/L boundary operands and repeated H/PV corrections. Literal bus traces, waits,
live port sampling, separate memory/ack accesses, ignored writes, partial
failures, EI/NMI between iterations, prefix discard, self-modification and
synthetic programs. Windows/Linux restore/build/tests/demos and Linux format.
Local SDK absent; record actual CI evidence. No Spectrum devices, contention,
physical WAIT/IORQ/refresh, external exercisers, Q, IM0 acceptance, full state
restore, ROM boot, UI or game compatibility claim.
