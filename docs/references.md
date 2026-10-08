# Reference register
Read relevant sections before implementing a feature. A URL here does not mean the whole
document was read, every fact is verified, or its contents may be redistributed.

## Read during bootstrap
| Source | Used for | Limit |
| --- | --- | --- |
| [Zilog Z80 UM0080](https://www.zilog.com/docs/z80/um0080.pdf) | Register/instruction/bus timing source and CPU work-order scope | 001a reads register/refresh and loads; 001b reads byte ALU/control/stack; 001c reads word ALU, DAA, rotations and exchanges; 001d reads HALT and DI/EI boundary semantics; 001e reads NMI/IM 1 priority, IFFs and responses; 001f reads mode selection and interrupt returns; 001g reads NEG; 001h reads rotate/shift/bit commands; pin-level timing remains unverified |
| [Young / Jan, Undocumented Z80 v0.90](https://datasheets.chipdb.org/Zilog/Z80/z80-documented-0.90.pdf) | Scoped NMOS byte ALU, word X/Y, DAA, SLL and IM2/RETI behavior (§2.2, §4.6–4.7, §8.4–8.7; §5.2–5.3) | 001h uses §3.1 for SLL, but supersedes §4.1 masked-result BIT X/Y with the Korth source; no source/vector import or local silicon measurement |
| [Weissflog netlist timing study](https://floooh.github.io/2021/12/06/z80-instruction-timing.html) | EX (SP),HL, ordinary CB read/idle/write order and NMI/IM 1/2 and interrupt-return nominal sequencing | 2021-12-06 trace; netlist differs from original NMOS in some details; no code imported or pin-level claim |
| [48K technical reference](https://worldofspectrum.org/faq/reference/48kreference.htm) | Hardware map and 48K timing reference | Community hardware reference; select/verify issue-specific phase details in M3/M6 |
| [Avalonia getting started](https://docs.avaloniaui.net/docs/get-started) | Desktop host choice | Platform deployment still requires actual host validation |
| [Avalonia 12 breaking changes](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes) | Version-aware API assumptions | Not a substitute for compiling the selected packages |
| [Avalonia 12.1.3 release](https://github.com/AvaloniaUI/Avalonia/releases/tag/12.1.3) | Stable version selection | Pin is deliberate, not an automatic latest-version policy |
| [Avalonia 12.1.3 license](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/licence.md) | Direct dependency license | Native/transitive release notices need a separate audit |
| [.NET SDK selection](https://learn.microsoft.com/en-us/dotnet/core/versions/selection) | global.json and target-framework policy | Runtime/build validation remains distinct |
| [xUnit v2 getting started](https://xunit.net/docs/getting-started/v2/getting-started) | VSTest-based test project and pinned baseline | Selected tests now cover ROM input and declared CPU slices |

The timing reference prevents rounding guest frames to exactly 50 Hz; the Avalonia version guide
prevents assuming v11 APIs/binding behavior apply unchanged to v12. External process skills from
other repositories are not copied as mandatory dependencies.

## To read at implementation time
- Snapshot and tape specifications: record exact SNA/Z80/TAP/TZX reference sections in each plan.
- Undocumented NMOS Z80 behavior and measured silicon vectors: identify the processor/profile
  and distinguish measured facts from undocumented emulation conventions.
- CPU single-step corpora, ZEXDOC/ZEXALL and chosen reference emulator: verify source, license,
  runner assumptions and tested state before adding runners or claiming coverage.
- Host audio library API/license: select in M5; no provider is selected in the foundation.
- Avalonia browser host/interop/audio limitations: verify when M8 starts.

Hardware manuals, archived references and external test datasets are not bundled. Record source
revision/date and small permitted citations in tests/plans, rather than copying complete manuals.

## Read for CB/WZ (001h)
- [Martin Korth no$ Sinclair ZX specification, preserved mirror](https://k1.spdns.de/Develop/Projects/zxsp/Info/nocash%20Sinclair%20ZX%20Specs.html): Undocumented Flags, register BIT operand X/Y and memory BIT WZ dependence. Only these sections are used; unrelated older SCF/CCF simplifications are not adopted.
- [Boo-boo/Kladov MEMPTR research, 2006](https://gist.github.com/drhelius/8497817): preserved report of physical Zilog Z80 chip measurements; used for current WZ writers and preservation, no BM1 clone policy or test code import.
- [MEMPTR research wiki](https://github.com/redcode/Z80/wiki/MEMPTR): Sainz de Baranda/Brewer/Helcmanovsky, revision dated 2026-01-07, cross-check of current writer table. Documentation only; emulator implementation was not read or copied. Retrieved 2026-10-08.
