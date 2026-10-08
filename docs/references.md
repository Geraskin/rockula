# Reference register
Read relevant sections before implementing a feature. A URL here does not mean the whole
document was read, every fact is verified, or its contents may be redistributed.

## Read during bootstrap
| Source | Used for | Limit |
| --- | --- | --- |
| [Zilog Z80 UM0080](https://www.zilog.com/docs/z80/um0080.pdf) | Register/instruction/bus timing source and CPU work-order scope | Bootstrap inspected the manual structure; instruction-specific sections remain required in M1 |
| [48K technical reference](https://worldofspectrum.org/faq/reference/48kreference.htm) | Hardware map and 48K timing reference | Community hardware reference; select/verify issue-specific phase details in M3/M6 |
| [Avalonia getting started](https://docs.avaloniaui.net/docs/get-started) | Desktop host choice | Platform deployment still requires actual host validation |
| [Avalonia 12 breaking changes](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes) | Version-aware API assumptions | Not a substitute for compiling the selected packages |
| [Avalonia 12.1.3 release](https://github.com/AvaloniaUI/Avalonia/releases/tag/12.1.3) | Stable version selection | Pin is deliberate, not an automatic latest-version policy |
| [Avalonia 12.1.3 license](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/licence.md) | Direct dependency license | Native/transitive release notices need a separate audit |
| [.NET SDK selection](https://learn.microsoft.com/en-us/dotnet/core/versions/selection) | global.json and target-framework policy | Runtime/build validation remains distinct |
| [xUnit v2 getting started](https://xunit.net/docs/getting-started/v2/getting-started) | VSTest-based test project and pinned baseline | Tests here cover ROM input only |

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
