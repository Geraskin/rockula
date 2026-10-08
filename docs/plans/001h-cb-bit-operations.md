# Work order 001h: complete CB page and scoped WZ
Status: independent expectations prepared before decoder changes.
Branch feat/z80-foundation, PR #6 to main.

## Scope and sources
Implement all 256 non-indexed CB payloads: RLC/RRC/RL/RR/SLA/SRA/SLL/SRL,
BIT/RES/SET for B/C/D/E/H/L/(HL)/A. Retain 248 standalone base encodings and
six ED commands. DD/FD and indexed CB are separate next work orders.
Read sources on 2026-10-08:
- Zilog UM008011-0816, printed pp.213–236, 243–260:
  https://www.zilog.com/docs/z80/um0080.pdf .
- Young/Jan Undocumented Z80 v0.90 §3.1 for SLL:
  https://datasheets.chipdb.org/Zilog/Z80/z80-documented-0.90.pdf .
- Martin Korth, no$ Sinclair ZX Specifications, Undocumented Flags and Internal
  MEMPTR Register; author's specification preserved at
  https://k1.spdns.de/Develop/Projects/zxsp/Info/nocash%20Sinclair%20ZX%20Specs.html .
  BIT register X/Y copy the unmasked operand. This differs from Young/Jan §4.1's
  older masked-result description; do not use that paragraph as the BIT oracle.
- Boo-boo/Kladov et al., MEMPTR research (2006), physical-chip measurements:
  https://gist.github.com/drhelius/8497817 . Zilog NMOS behavior only; no BM1 policy.
  Cross-check: MEMPTR wiki by Sainz de Baranda/Brewer/Helcmanovsky, edited 2026-01-07:
  https://github.com/redcode/Z80/wiki/MEMPTR . Only research documentation was read.
- Weissflog netlist timing study, CB Prefix, 2021-12-06:
  https://floooh.github.io/2021/12/06/z80-instruction-timing.html .
No emulator implementation, external expected corpus or guest ROM is imported.

## Contract and files
Two M1 fetches increment PC/R twice; no intermediate interrupt acceptance.
Register commands take 8 T. (HL) BIT reads then idles one T (12 total), never writes.
Other (HL) commands read, idle one T, write (15 total); idle address is a logical HL
label, not a pin claim. Existing wait/live-sampling and fault rules apply.
Rotates/shifts replace F with result S/Z/X/Y/parity and outgoing carry; H/N clear.
BIT preserves C, sets H, clears N, PV follows Z and S is set only for a set bit 7.
Register BIT X/Y use operand; memory BIT uses the prior WZ high byte. RES/SET preserve F.
Introduce owner-confined WZ inspection/editing in Z80Registers with deterministic
reset zero. ADR 0009 scopes boundary updates for existing loads, word ADD, control
flow, EX (SP),HL, ED returns and NMI/IM1/IM2. Other implemented commands preserve WZ.
No new bus kinds or complete save/restore API. Failed transactions retain prior effects;
WZ commit points are specified in the ADR, not claimed as microstate/pin accuracy.
Files: new Z80Cpu.Bit.cs, decoder, registers, relevant existing instruction files,
Z80BitTests/Z80WzTests and coverage/status documentation.

## Acceptance
Literal CB row/target coverage and independent arithmetic/bit-count expectations;
every operand/carry for every encoding, every incoming F for a representative operand,
all BIT (HL) operands/WZ high bytes/carries; untargeted state preservation, PC/R wrapping,
wait/live sampling, prefix atomicity/EI delay, read/internal/write faults and a synthetic
CB program. WZ updates/preservation/reset/failure are independently checked.
Publish tests and state-property scaffold first; inspect red CI before implementation.
Windows/Linux restore, Release build/test, both demos and Linux format; local SDK unavailable.
Update README, agent links, coverage, testing, roadmap, contracts and PR using actual logs.
No full CPU, Q, index page, IM0, contention, Spectrum boot or game compatibility claims.
