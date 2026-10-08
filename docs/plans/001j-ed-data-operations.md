# Work order 001j: ED data operations
Status: independent expectations before implementation. Branch feat/z80-foundation, PR #6 to main.

## Goal and sources
Add 22 ED encodings: ADC/SBC HL,BC/DE/HL/SP (8), LD (nn),BC/DE/HL/SP
and reverse (8), LD I/R,A and LD A,I/R (4), RRD/RLD (2). ED63/6B are
explicit word-transfer encodings; other aliases and undefined ED NOPs remain faults.
Total ED support becomes 28; 228 payloads remain unsupported. No ports or blocks yet.
Read on 2026-10-08: Zilog UM008011-0816 printed pp.94–97,103–104,108–109,
190–193,238–241 https://www.zilog.com/docs/z80/um0080.pdf ; Weissflog
2021-12-06 I/R and RRD/RLD timing sections
https://floooh.github.io/2021/12/06/z80-instruction-timing.html ;
Boo-boo/Kladov physical-chip MEMPTR research (2006), scoped Zilog NMOS rules
for ADC/SBC, word loads and RRD/RLD https://gist.github.com/drhelius/8497817 .
No upstream emulator code or external corpus is imported. X/Y follow established
result rules: word arithmetic high byte, I/R and nibble instructions result A.

## Implementation and failure policy
Use shared pair, memory and flag helpers; preserve index cancellation and atomic
prefix retirement. ADC/SBC take 15 T, full word S/Z/H/PV/N/C and high-byte X/Y,
WZ=old HL+1. Word loads take 20 T, little-endian with wrap, F unchanged,
WZ=nn+1. I/R take 9 T; LD R,A overwrites all R after both refresh increments;
LD A,R observes incremented R. LD A,I/R preserve C, clear H/N, PV=IFF2.
RRD/RLD take 18 T, memory read, four internal T, memory write; A high nibble
and C survive, remaining flags derive from resulting A, WZ=HL+1.
ADR 0011 defines logical commit points and LD A,I/R interrupt limitation.
Files: Z80Cpu.Extended.cs, new Z80Cpu.ExtendedData.cs and independent ED data tests,
old explicit unsupported tests, coverage/status/agent links.

## Acceptance
Publish test-first commit and inspect actual failing CI. Sweep all HL values
against carry/borrow boundaries for all eight arithmetic encodings; test signed
range overflow and full-word zero independently. Sweep all A/memory/carry nibble
combinations, I/R values/flags/IFF2, word transfers across F and address wrap,
ordered traces, waits/live sampling, prefix cancellation, EI/NMI and partial faults.
Run restore/Release build/all tests/both demos on Windows/Linux and Linux format.
Local SDK unavailable; inspect actual CI logs. Record counts and evidence below.
Ports, block instructions, aliases/NOPs, SCF/CCF/Q, IM0, physical interrupt sampling,
complete state restore, Spectrum devices/ROM boot/UI/games remain outside this slice.
