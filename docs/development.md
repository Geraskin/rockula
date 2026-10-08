# Development
## Environment
Install stable .NET 10 on Windows or Linux. SDK resolution starts at 10.0.100 and rolls forward
to a stable 10.0 feature band; previews are disabled. CI installs 10.0.x.
Use VS Code, Rider or Visual Studio with C# support.
The optional dev container uses a .NET 10 SDK image, a non-root user and C# extensions.
It has no Docker socket, GPU mount or host credentials. GUI/audio access is not configured.

```sh
dotnet --info
dotnet restore RockULA.slnx
dotnet build RockULA.slnx -c Release --no-restore
dotnet test RockULA.slnx -c Release --no-build
dotnet format RockULA.slnx --verify-no-changes --no-restore
dotnet run --project src/RockULA.Headless -- --about
dotnet run --project src/RockULA.Desktop
```

Run `dotnet format RockULA.slnx --no-restore` to apply formatting before checking.
Current package versions live in `Directory.Packages.props`; update deliberately and review
release notes. Package lockfiles can be added after a verified restore; do not fabricate them.

## Branches
The default/integration branch is `main`; the repository was created with main already.
Initial bootstrap is intentionally committed there. Subsequent substantial work uses a branch:
`feat/z80-foundation`, `feat/snapshot-sna`, `fix/alu-half-carry`, `docs/...` or `chore/...`.

Start from a fetched main and inspect local changes before switching. Use sibling worktrees for
concurrent work or risky migrations. Never reset/discard someone else's work or rewrite shared
commits. PRs target main unless a task explicitly builds on another branch; state that base.

## Style and design
Nullable and implicit usings are enabled; warnings are errors. Use file-scoped namespaces,
four spaces, clear names and explicit integer conversion/wrap points.
Avoid async inside the hardware loop, hidden globals and allocations per instruction.
Use spans for bounded binary decoding and byte buffers, while preserving ownership.
Prefer public APIs only where a host/parser/test boundary needs them; do not expose every CPU
helper merely to test it.

## Process
Use focused plans with observable exit gates. Hardware changes need source sections and independent
vectors. Format changes need malformed-input tests. Reversible documentation-only changes need
link/structure checks rather than artificial unit tests.
A review explains behavior, evidence and limits, not a chronology of edits.
