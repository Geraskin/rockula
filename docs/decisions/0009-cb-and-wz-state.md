# 0009: CB operations and instruction-boundary WZ state
Status: accepted for work order 001h.

## Context and decision
BIT (HL) exposes previous WZ bits 11/13 through F bits 3/5. HL or the memory byte
cannot substitute for this history. Add mutable ushort WZ to the owner-confined
Z80Registers. Construction/reset use zero as emulator policy, not silicon power-on evidence.
Future internal snapshots must retain WZ. This is still not a complete state restore API.
Source/provenance and the conflicting older BIT description are scoped in
[001h](../plans/001h-cb-bit-operations.md).

At successful instruction boundaries, implemented WZ writers are:

| Instruction | WZ |
| --- | --- |
| LD A,(BC/DE/nn) | source address + 1, wrapped |
| LD (BC/DE/nn),A | A high byte; wrapped address + 1 low byte, no carry into A |
| LD HL,(nn); LD (nn),HL | nn + 1, wrapped |
| ADD HL,rr | original HL + 1, wrapped |
| EX (SP),HL | popped word |
| JP nn / JP cc,nn; CALL nn / CALL cc,nn | immediate target, even when condition fails |
| Taken JR/DJNZ/RET/RET cc; RST; RETN/RETI | resulting PC |
| NMI/IM1/IM2 response | handler PC |

Other implemented instructions, including all ordinary CB commands, preserve WZ.
JP (HL), PUSH/POP, untaken JR/DJNZ/RET cc, mode commands and HALT preserve it.
CB has two M1 fetches. Register operations take 8 T; BIT (HL) takes 12 T and
read-modify-write takes 15 T. One Internal cycle labelled with HL follows the
memory read. BIT (HL) copies previous WZ high X/Y and does not modify WZ.

## Failure and limits
WZ changes after the associated successful data transfer/arithmetic/return response.
Immediate JP/CALL targets commit after successful operand fetch, before any conditional
call stack writes. Thus a failed CALL can retain its new WZ and partial stack effects.
Word transfers/exchange commit WZ after their complete current sequence; failed memory
CB writes retain the old F because flags commit after the write succeeds. No rollback
of bus or memory effects is promised. Reset is required after a fault.
These are logical instruction/fault boundaries. Mid-cycle WZ values, address pins,
refresh edges and electrical interrupt sampling are not modeled or asserted.
Q/history, indexed prefixes, remaining ED commands and complete save/restore remain deferred.
