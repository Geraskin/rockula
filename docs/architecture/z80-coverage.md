# Implemented Z80 coverage
Status: 248 base encodings including HALT/DI/EI and boundary NMI/IM 1 responses; full M1/M2 are incomplete.
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
The remaining 8 base bytes throw UnsupportedOpcodeException after the initial fetch.
They are SCF/CCF (37/3F), IN/OUT immediate (DB/D3),
and CB/DD/ED/FD prefix bytes. SCF/CCF await Q/history semantics; prefix and interrupt work is incomplete.
A rejected prefix reports that encountered byte and original PC; it does not decode its payload.

The register file includes main/alternate byte/pair views, IX/IY, PC/SP, I/R, IFFs and IM.
DI/EI update IFFs and track inhibition through the following instruction. Interrupt
request acceptance implements NMI and IM 1 only; ED mode-selection instructions
remain unsupported. Eligible IM 0/2/invalid modes fault explicitly before bus effects. R updates are verified for the unprefixed slice only.
Faulted CPUs cannot step again until Reset. Reset leaves bus time and memory untouched.
HALT and EI-delay are inspectable CPU control state, cleared by Reset. A halted Step
performs one logical M1 at the fixed next PC, ignores data and updates R. Reset or
an accepted NMI/IM 1 response exits HALT. See [ADR 0006](../decisions/0006-halt-and-ei-boundaries.md)
and [ADR 0007](../decisions/0007-interrupt-boundary-inputs.md).
SetInterruptLine supplies a level; SetNmiLine latches an assertion edge. NMI has
priority, ignores IFF1/EI inhibition and preserves IFF2. IM 1 checks IFF1/EI delay
and clears both IFFs. Each response pushes unchanged PC and increments R once:
NMI vectors to 0066 in 11 T-states, IM 1 to 0038 in 13. IM 1 acknowledges a device
byte separately from memory and ignores it. Signals arriving during a transaction
apply at next Step entry, an explicitly scoped boundary model.
RETN/RETI, IM 0/2, daisy chain and electrical interrupt sampling remain deferred.
This is not a complete save-state API: WZ, Q and future device state are absent.

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
