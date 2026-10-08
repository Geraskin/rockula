# Work order 001f: ED interrupt control and IM 2
Status: tests first; implementation pending.
Branch feat/z80-foundation, PR #6 to main.

## Scope and sources
Keep 248 standalone base encodings; recognize ED as a prefix for five documented
instructions: ED46/56/5E select IM0/1/2; ED45 RETN and ED4D RETI. Other ED payloads
(including aliases) fault after two fetches with original address, prefix and payload.
IM 0 acceptance remains unsupported. Add IM 2 acceptance through the existing
acknowledgement hook: full device byte plus I forms a word pointer, stack high/low
writes precede low/high vector reads; address arithmetic wraps, nominal total 19 T.
Sources: Zilog UM008011-0816 printed pp.18–20, 184–186, 288–291;
https://www.zilog.com/docs/z80/um0080.pdf . Weissflog netlist timing study,
RETI/RETN and Mode 2 Interrupt Timing, 2021-12-06:
https://floooh.github.io/2021/12/06/z80-instruction-timing.html .
Young/Jan Undocumented Z80 v0.90 sections 5.2/5.3 document the full IM2 byte
(including odd values) and shared RETI/RETN IFF behavior, based on the author's tests:
https://datasheets.chipdb.org/Zilog/Z80/z80-documented-0.90.pdf .
RETI copies IFF2 to IFF1 just like RETN, as shown by the netlist trace; the manual's
RETI description does not fully state this. No emulator code is imported.

## Contract and files
Z80Cpu.Extended.cs fetches payload as M1 with PC/R update; no interrupt polling
between prefix and payload. Each mode instruction takes 8 T; returns take 14 T,
pop low/high and restore IFF1 only after successful stack reads. RETI additionally
calls NotifyReti at the completed instruction boundary with no extra time; this
is a logical peripheral notification, not daisy-chain pin decoding. Interface
provides a default no-op and Z80Bus a protected OnReti hook.
UnsupportedOpcodeException adds nullable Prefix and a prefixed constructor.
Z80Cpu.Interrupts.cs extends IM 2; retains IM 1/NMI behavior and failure policy.
No complete ED decoder, WZ/Q, IM 0, physical pin timing or complete state restore.

## Independent tests and acceptance
Literal five-encoding tables; all F/IFF states, prefix PC/R wrap and payload faults,
all remaining 251 ED bytes explicitly rejected; waits, stack/vector wrap/order,
live acknowledgement, vector read failure after committed stack, HALT resume,
EI+RETI held INT and NMI+RETN return. Existing unsupported IM2 expectation is
replaced by positive IM2 coverage; other unsupported modes still fail before effects.
Publish red tests before implementation. Windows/Linux restore, Release build/test,
both demos and Linux formatting; inspect actual logs. Local SDK unavailable.
Update coverage, contracts, README, agent current links and PR with actual evidence.
