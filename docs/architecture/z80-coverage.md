# Implemented Z80 coverage
Status: initial documented load slice; full M1/M2 are incomplete.
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

Total: **84 base-byte encodings** (NOP plus 83 LD encodings).
The remaining 172 base bytes throw UnsupportedOpcodeException after the initial fetch.
This includes HALT (76), LD SP,HL (F9), arithmetic, branches, stack and all four prefix bytes.
A rejected prefix reports that encountered byte and original PC; it does not decode its payload.

The register file includes main/alternate byte/pair views, IX/IY, PC/SP, I/R, IFFs and IM.
IFF/IM storage is not interrupt emulation. R updates are verified for the unprefixed slice only.
Faulted CPUs cannot step again until Reset. Reset leaves bus time and memory untouched.
This is not a complete save-state API: WZ, HALT, EI-delay and future device state are absent.

## Timing evidence and limits
Ordered memory operations, nominal instruction totals, little-endian/wrapped transfers,
PC/R behavior, preserved F and wait-shifted sampling are tested.
Current logical offsets are specified in [ADR 0004](../decisions/0004-initial-cpu-bus.md).
Electrical pin edges, refresh-address events, dynamic WAIT polling, NMOS undocumented quirks,
ULA/contention/floating bus and real ROM boot are not verified or implemented.

## Demo
`dotnet run --project src/RockULA.Headless -- --demo` executes this self-authored program:
`LD HL,4000; LD (HL),2A; LD A,(HL); LD (4001),A; LD B,03; NOP`.
It is bounded to six instructions. Its expected result is independently asserted in CPU tests.
The demo bus is flat RAM, not a Spectrum machine, and requires no firmware.
