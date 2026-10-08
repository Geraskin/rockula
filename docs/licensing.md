# Project, dependencies and asset policy
This is a repository policy, not a blanket rights statement about emulator-related files.

## Original work
The existing root LICENSE is MIT, copyright 2026 Alexey Geraskin. Preserve its notice.
Original code and documentation contributed here use that license unless a clearly marked
third-party notice says otherwise. Public source and binary releases are intended.

## Dependencies
Do not assume every dependency shares our license. Review the selected version's upstream
license, transitive/native dependencies and required notices before releasing binaries.
NuGet references are not vendored source. Maintain a dependency/notice inventory when creating
a release, including bundled render/audio components and fonts.

| Direct dependency | Bootstrap version | Role | Reference |
| --- | --- | --- | --- |
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent | 12.1.3 | Desktop shell | [Upstream license](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/licence.md) |
| xunit | 2.9.3 | Test framework | [Upstream project](https://github.com/xunit/xunit) |
| xunit.runner.visualstudio | 3.1.1 | Test runner | [Upstream project](https://github.com/xunit/visualstudio.xunit) |
| Microsoft.NET.Test.Sdk | 17.13.0 | Test discovery/execution | [Upstream project](https://github.com/microsoft/vstest) |

Core/Formats have no external package dependencies in the bootstrap.
A dependency table is not a complete release notice bundle; transitive auditing is still required
when publishing binaries. No firmware, game or reference implementation is vendored.

## Firmware and games
Our license does not grant rights to Sinclair firmware, game images, logos or external manuals.
Do not commit/download/bundle them merely because they are old or available on an archive site.
Users supply their own authorized assets in ignored local storage.
Any optional redistributable replacement firmware must be reviewed separately and documented
with its own source, version, license and compatibility limits before integration.

Synthetic tests can use self-authored short machine-code programs and dummy byte buffers.
A dummy buffer tests a contract and must never be offered as working firmware.

## Reference implementations and corpora
Reading a hardware specification is distinct from copying code. This project implements its own
CPU/ULA; if a contribution imports code, stop and document origin/license/notice requirements
before accepting it. Do not relicense incompatible upstream work under MIT.

External test-vector data, ZEX binaries and oracle executables also need provenance review.
Keep restricted or unreviewed assets outside Git. Commit manifests and reproducible acquisition
instructions only after their terms and source have been checked.
