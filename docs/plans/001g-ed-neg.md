# Work order 001g: ED NEG
Status: implemented and verified on Windows/Linux; awaiting PR review/merge.
Branch feat/z80-foundation, PR #6 to main.

Implement documented ED44 only: A becomes 0−A in 8 T through two M1 fetches.
Sources: Zilog UM008011-0816 printed pp.176–177,
https://www.zilog.com/docs/z80/um0080.pdf ; Young/Jan Undocumented Z80 v0.90
§2.2, https://datasheets.chipdb.org/Zilog/Z80/z80-documented-0.90.pdf .
S/Z/X/Y follow result, H follows low-nibble borrow, PV means input 80h,
N is set and C means nonzero input. No imported emulator implementation.

Files: Z80Cpu.Extended.cs, Z80NegTests.cs, all-ED rejection table and current
coverage/status documentation. No new state or architecture contract.
Test all 65,536 AF inputs with independent arithmetic expectations, two fetches,
PC/R wrapping, waits and failed payload fetch retaining AF. Aliases stay unsupported.
Acceptance: Windows/Linux restore, Release build/test, both demos and Linux format.
Local SDK unavailable; inspect CI red and green logs. No IM0, Q/WZ, complete ED,
physical pin timing, Spectrum compatibility or external corpus claims.

## Red evidence
[CI 37747977012](https://github.com/Geraskin/rockula/actions/runs/37747977012):
Windows/Linux Release builds passed with zero warnings/errors; two new semantic/fetch tests fail on unsupported ED44;
358 tests pass, including payload-failure handling; no skips, 360 total.

## Green evidence
[CI 37748207703](https://github.com/Geraskin/rockula/actions/runs/37748207703):
Windows/Linux restore and Release build passed with zero warnings/errors;
all 360 tests passed on each platform, zero skips/failures. The 65,536 AF
vectors, wrapped/waited two-fetch trace and payload failure passed. Both demos
passed unchanged at 51/234 T-states; Linux formatting passed.
No local .NET SDK, external corpus or interactive UI validation was available.
