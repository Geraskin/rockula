# Work order 001i: DD/FD and indexed CB
Status: implemented; Windows/Linux CI verified, with limits below.
Branch feat/z80-foundation, PR #6 to main.

## Scope and sources
Extend the existing 248 base encodings and six ED commands through DD/FD decoding;
implement all 256 DDCB and all 256 FDCB payloads. Index substitution affects 85 base
encodings per prefix, including undocumented halves; 163 base encodings ignore it.
The four remaining base operations and 250 remaining ED payloads still fault.
Read on 2026-10-08:
- Zilog UM008011-0816 indexed loads (printed pp.79–87, 99–109), stack/exchanges,
  indexed byte ALU, word arithmetic (pp.194–202) and indexed bit operations:
  https://www.zilog.com/docs/z80/um0080.pdf .
- Young/Jan Undocumented Z80 v0.90 §§3.2–3.3, 3.5–3.7:
  https://datasheets.chipdb.org/Zilog/Z80/z80-documented-0.90.pdf .
- Weissflog netlist study 2021-12-06, DD and FD Prefixes, LD (IX+d),n and
  DD CB and FD CB Prefix:
  https://floooh.github.io/2021/12/06/z80-instruction-timing.html .
- Prior BIT/WZ research remains scoped by 001h and ADR 0009.
Only hardware/research documentation is used; no emulator implementation/corpus is imported.

## Contract and files
ADR 0010 specifies iterative last-prefix selection and a 65,536-prefix Step bound.
ED cancels index substitution. Terminal EI, including DD/FD EI, starts the normal
delay; interrupts are accepted only at next Step entry. HALT retires the complete
prefix instruction, then continues ordinary halted M1 steps. IX/IY never alias HL.
Register-half substitution excludes H/L used with indexed memory. EX DE,HL and EXX
ignore the index. Indexed CB copies modifying results to the encoded ordinary
register (unless target 6); BIT aliases never write memory or registers.
Signed displacement wraps at 16 bits and commits effective address to WZ.
Indexed CB has two M1s, displacement/payload memory reads, two internal T, operand
read and one internal T; optional write gives 20/23 T. General indexed memory
uses displacement read plus five internal T; LD (index+d),n instead reads d,n,
idles two T and writes (19 T). INC/DEC memory takes 23 T. Register halves take
8 T or 11 T with immediate. Word/stack totals retain base sequencing plus 4 T.
Logical internal addresses and fault commit points are explicit in the ADR.
Files: new Z80Cpu.Index.cs, Step/decoder, shared word/bit helpers; new indexed
register/memory/CB/prefix tests; coverage/status and agent links.

## Acceptance and limits
Publish independent expectations before implementation and inspect actual red logs.
Literal affected-opcode tables, register/half state and memory H/L exceptions,
all ALU operand pairs/carries by index source, INC/DEC flags, word arithmetic,
all indexed CB payloads/values/carries plus incoming flags; signed d/wrap,
stack/absolute/prefix wrap, WZ, memory/idle traces and waits/live sampling.
Ignored/mixed/repeated prefixes, ED cancellation, EI/HALT/NMI boundaries,
unsupported terminal diagnostics, cycle failures, read-only memory copies,
prefix-only bound and a synthetic IX/IY program. Existing suites must remain green.
Windows/Linux restore/Release build/test, both demos and Linux format; local SDK absent.
No Q, remaining ED/I/O/SCF/CCF, IM0 acceptance, complete save states, electrical
sampling/contention, Spectrum boot, UI or game compatibility claims.

## Verification evidence
Independent test-first commit 471e9ab85389b2ba885d579342a40cd7c315f0667:
[CI 37754120098](https://github.com/Geraskin/rockula/actions/runs/37754120098)
built successfully on Windows/Linux; each reported 645 expected failures and
701 passes, zero skips, 1,346 total. Actual logs show DD/FD rejection, not a
compile failure. Implementation CI then exposed a test-count mistake: the mixed
index/ED sequence contains eight M1 fetches, not seven. The expectation was
corrected by counting its bytes; production refresh logic was unchanged.

Verified head 069d4e9c2c040b520693ba178f73c16cbe6a1fab:
[CI 37754791932](https://github.com/Geraskin/rockula/actions/runs/37754791932).
Both Windows and Linux restored, built Release with zero warnings/errors and
passed all 1,382 tests with zero failures/skips. Both demos completed with the
independent 51/234-T expected states; Linux format passed. Actual job logs were
inspected. Markdown links and project XML/SDK JSON passed local checks.
No local .NET SDK was available. External exercisers, full-chip pin traces,
interactive UI, ROM boot and games were not run and are not claimed.
