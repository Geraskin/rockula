# Implemented Z80 coverage
Status: 248 standalone base encodings plus all 256 CB payloads, DD/FD and all 512 indexed CB payloads, 28 ED encodings and NMI/IM 1/2 responses; full M1/M2 are incomplete.
Tests use literal encoding/state/timing expectations and fail explicitly for other base bytes.

| Encoding | Meaning | Count | Nominal T-states |
| --- | --- | --- | --- |
| 00 | NOP | 1 | 4 |
| 40–7F except 76 | LD register/register, register/(HL), (HL)/register | 63 | 4 or 7 |
| 06,0E,16,1E,26,2E,36,3E | LD register/immediate and (HL)/immediate | 8 | 7 or 10 |
| 01,11,21,31 | LD BC/DE/HL/SP, immediate word | 4 | 10 |
| 02,12,0A,1A | Accumulator transfers through BC/DE | 4 | 7 |
| 32,3A | Accumulator transfers through absolute address | 2 | 13 |
| 22,2A | HL transfers through absolute address | 2 | 16 |
| 80–BF; C6,CE,D6,DE,E6,EE,F6,FE | ADD/ADC/SUB/SBC/AND/XOR/OR/CP | 72 | 4 or 7 |
| 04,0C,14,1C,24,2C,34,3C; 05,0D,15,1D,25,2D,35,3D | Byte INC/DEC | 16 | 4 or 11 |
| C3; C2,CA,D2,DA,E2,EA,F2,FA; E9 | JP / JP cc / JP (HL) | 10 | 10 or 4 |
| 18,20,28,30,38; 10 | JR / JR cc / DJNZ | 6 | 12; 7/12; 8/13 |
| CD; C4,CC,D4,DC,E4,EC,F4,FC | CALL / CALL cc | 9 | 17; 10/17 |
| C9; C0,C8,D0,D8,E0,E8,F0,F8 | RET / RET cc | 9 | 10; 5/11 |
| C7,CF,D7,DF,E7,EF,F7,FF | RST | 8 | 11 |
| C5,D5,E5,F5; C1,D1,E1,F1 | PUSH / POP BC,DE,HL,AF | 8 | 11 / 10 |
| 03,13,23,33; 0B,1B,2B,3B | INC/DEC BC,DE,HL,SP | 8 | 6 |
| 09,19,29,39 | ADD HL,BC/DE/HL/SP | 4 | 11 |
| F9 | LD SP,HL | 1 | 6 |
| 07,0F,17,1F | RLCA/RRCA/RLA/RRA | 4 | 4 |
| 27,2F | DAA/CPL | 2 | 4 |
| 08,EB,D9 | EX AF,AF'; EX DE,HL; EXX | 3 | 4 |
| E3 | EX (SP),HL | 1 | 19 |
| 76 | HALT | 1 | 4, then 4 per halted step |
| F3,FB | DI/EI | 2 | 4 |

Total: **248 base-byte encodings**.
ED is recognized as a prefix for 28 additional two-byte encodings:

| Encoding | Meaning | Nominal T-states |
| --- | --- | --- |
| ED44 | NEG | 8 |
| ED46, ED56, ED5E | IM 0, IM 1, IM 2 | 8 |
| ED45, ED4D | RETN, RETI | 14 |
| ED42,52,62,72; ED4A,5A,6A,7A | SBC/ADC HL,BC/DE/HL/SP | 15 |
| ED43,53,63,73; ED4B,5B,6B,7B | LD (nn),BC/DE/HL/SP and reverse | 20 |
| ED47,4F; ED57,5F | LD I/R,A; LD A,I/R | 9 |
| ED67,6F | RRD/RLD | 18 |

Word ADC/SBC replace all F, using full-word S/Z, arithmetic overflow, bit-11
carry/borrow and high-byte X/Y; WZ=old HL+1. ED word loads use wrapped
little-endian transfers, preserve F and set WZ=nn+1. ED63/6B are explicitly
supported word transfers. I/R writes preserve F; LD R,A replaces all R after
the fetch increments, while LD A,R observes incremented R. LD A,I/R preserve C,
clear H/N, copy result S/Z/X/Y and use IFF2 for PV (not parity). WZ is preserved.
The interrupt-during-LD A,I/R parity anomaly is outside our Step-entry interrupt
model; an assertion during the instruction does not retroactively change PV.
RRD/RLD read (HL), idle four T then write; A's high nibble and C survive and
other flags use resulting A and parity. WZ=HL+1. An ignored ROM write still
commits calculated A. [001j](../plans/001j-ed-data-operations.md) and
[ADR 0011](../decisions/0011-ed-data-boundaries.md) define sources and logical
commit/fault boundaries. Ports, blocks and remaining ED aliases/NOPs are deferred.

ED is not counted as a standalone base instruction. Other 228 ED payloads fault
with original address, prefix and payload after two fetches (8 T). Unsupported
aliases and ED NOPs are not silently accepted. Four other base bytes still fault
after one fetch: SCF/CCF (37/3F), IN/OUT (DB/D3).

CB is a prefix for **256 additional encodings**, all implemented:

| Payload | Meaning | Nominal T-states |
| --- | --- | --- |
| 00–3F | RLC/RRC/RL/RR/SLA/SRA/SLL/SRL | 8 register; 15 (HL) |
| 40–7F | BIT 0–7 | 8 register; 12 (HL) |
| 80–BF | RES 0–7 | 8 register; 15 (HL) |
| C0–FF | SET 0–7 | 8 register; 15 (HL) |

All eight targets are covered: B/C/D/E/H/L/(HL)/A. Ordinary CB fetches increment
R twice. Memory commands read then idle one T; modifying commands write afterward.
BIT never writes. Rotates/shifts use result S/Z/X/Y/parity and shifted-out C,
clearing H/N. RES/SET preserve all F. BIT sets H, clears N, preserves C,
sets PV with Z and sets S only for a set bit 7. Register BIT X/Y use the unmasked
operand; (HL) BIT X/Y use the previous WZ high byte. CB preserves WZ.
[001h](../plans/001h-cb-bit-operations.md) documents sources and the older BIT
reference discrepancy. [ADR 0009](../decisions/0009-cb-and-wz-state.md) defines
WZ reset, writers, preservation and fault boundaries for the current instruction set.

## Index prefixes
DD/FD are recognized prefixes, not standalone base instructions. Of the 248 base
encodings, 85 substitute IX/IY and 163 ignore the prefix (adding four T per prefix).
The last DD/FD selects the index. ED cancels substitution; EX DE,HL and EXX use
ordinary registers. H/L become index halves except when used with indexed memory.
No HL/index swapping is performed.

| Indexed operation | Nominal T-states, one index prefix |
| --- | --- |
| Register-half load, ALU, INC/DEC | 8 |
| Register-half immediate | 11 |
| Indexed memory LD/ALU; LD (index+d),n | 19 |
| Indexed memory INC/DEC | 23 |
| LD index,nn; POP index | 14 |
| LD index,(nn); LD (nn),index | 20 |
| INC/DEC index; LD SP,index | 10 |
| ADD index,BC/DE/index/SP; PUSH index | 15 |
| EX (SP),index | 23 |
| JP (index) | 8 |
| DDCB/FDCB BIT, all 128 aliases per page | 20 |
| DDCB/FDCB rotates/shifts/RES/SET | 23 |

All **256 DDCB and 256 FDCB payloads** are supported. Modifying operations copy
computed results to the encoded ordinary B/C/D/E/H/L/A register unless target 6;
ROM writes can be ignored without suppressing that copy. BIT aliases never write
memory/registers and use the effective address high byte for X/Y. Displacements
are signed and wrap at 16 bits; indexed effective addresses update WZ. The final
indexed-CB payload is a memory read, not M1, so it does not increment R.
Prefix chains retire atomically, including EI/HALT. A stream of 65,536 consecutive
DD/FD bytes faults explicitly to bound Step; this is an emulator policy, not
silicon behavior. Diagnostics identify the first instruction address and last
index prefix (ED diagnostics retain ED). See [001i](../plans/001i-index-prefixes.md)
and [ADR 0010](../decisions/0010-index-prefix-sequencing.md) for source, timing,
logical internal address labels and partial-effect boundaries. Q remains absent.

The register file includes main/alternate byte/pair views, IX/IY, PC/SP, I/R, IFFs and IM.
DI/EI update IFFs and track inhibition through the following instruction. Interrupt
request acceptance implements NMI and IM 1/2. Eligible IM 0/invalid modes fault
explicitly before bus effects. ED mode commands preserve all F and both IFFs.
RETN/RETI pop PC, restore IFF1 from IFF2 and preserve F; RETI also emits a logical
completion notification. ED and CB prefix/payload sequences are atomic with respect to interrupt polling. R increments once per opcode fetch, including both ED bytes.
Faulted CPUs cannot step again until Reset. Reset leaves bus time and memory untouched.
HALT and EI-delay are inspectable CPU control state, cleared by Reset. A halted Step
performs one logical M1 at the fixed next PC, ignores data and updates R. Reset or
an accepted NMI/IM 1/2 response exits HALT. See [ADR 0006](../decisions/0006-halt-and-ei-boundaries.md)
and [ADR 0007](../decisions/0007-interrupt-boundary-inputs.md).
SetInterruptLine supplies a level; SetNmiLine latches an assertion edge. NMI has
priority, ignores IFF1/EI inhibition and preserves IFF2. IM 1 checks IFF1/EI delay
and clears both IFFs. Each response pushes unchanged PC and increments R once:
NMI vectors to 0066 in 11 T-states, IM 1 to 0038 in 13. IM 1 acknowledges a device
byte separately from memory and ignores it. Signals arriving during a transaction
apply at next Step entry, an explicitly scoped boundary model.
IM2 uses the full acknowledged byte with I, pushes PC then reads the target low/high
with wrap (19 T). See [ADR 0008](../decisions/0008-ed-interrupt-control.md).
IM0, daisy-chain pin decoding and electrical interrupt sampling remain deferred.
This is not a complete save-state API: WZ is implemented at logical boundaries; Q and future device state are absent.

## Timing evidence and limits
Ordered memory operations, nominal instruction totals, little-endian/wrapped transfers,
PC/R behavior, arithmetic/logic flags, condition paths, stack order and wait-shifted sampling are tested.
Byte ALU flags follow Zilog and the scoped Young/Jan v0.90 reference in [001b](../plans/001b-alu-control-flow.md).
CP copies X/Y from its operand; arithmetic uses overflow, logic uses even parity; byte INC/DEC preserve C.
Word ADD HL preserves S/Z/PV, ignores input C, sets H/C and high-byte X/Y; word INC/DEC preserves all F.
Accumulator rotations preserve S/Z/PV and copy result X/Y. DAA is checked for all A/F inputs
against the scoped nibble tables and valid decimal arithmetic in [001c](../plans/001c-word-alu-and-exchanges.md).
EX (SP),HL reads low/high then writes high/low; SP is unchanged at instruction boundaries.
The netlist reference establishes nominal order only, not full NMOS internal state or pins.
Current logical offsets are specified in [ADR 0004](../decisions/0004-initial-cpu-bus.md).
Internal durations use logical address labels as documented in [ADR 0005](../decisions/0005-internal-cycle-scope.md).
Electrical pin edges, internal address-pin accuracy, refresh, dynamic WAIT polling, wider NMOS quirks,
ULA/contention/floating bus and real ROM boot are not verified or implemented.

## Demo
`dotnet run --project src/RockULA.Headless -- --demo` executes this self-authored program:
`LD HL,4000; LD (HL),2A; LD A,(HL); LD (4001),A; LD B,03; NOP`.
It is bounded to six instructions. Its expected result is independently asserted in CPU tests.
The demo bus is flat RAM, not a Spectrum machine, and requires no firmware.

`--demo-loop` sums 3+2+1 through a subroutine preserving BC on stack, then stores the result.
It is bounded to 64 steps; expected completion takes 23 instructions and 234 T-states:
PC=0012 A=06 F=00 BC=0000 SP=8000 R=17 RAM[4000]=06. Both demos run in CI.
