# Core instructions
Root AGENTS applies. Read docs/architecture before introducing hardware APIs.
Core is synchronous, deterministic C#, with no GUI/host-device/file/network dependencies.
One machine owner, one integer T-state clock. CPU operations carry type/address/timing so future
contention can occur within instructions. Untimed inspection is a separate path.
Capture every internal state element that changes future execution, with explicit reset policy.
Use spans/arrays with documented ownership. Avoid per-instruction allocations and global state.
Our CPU/ULA are original work; independently test semantic and bus effects before claiming support.
Core currently implements RomImage validation and 250 base Z80 encodings, all 256 CB payloads, DD/FD and all 512 indexed CB payloads, scoped WZ state, HALT/DI/EI state and 60 ED encodings and NMI/IM 1/2 responses.
Read docs/architecture/z80-coverage.md and ADRs 0004–0012 for actual support and timing limits.
No Spectrum memory map, ULA or complete CPU/state-restore contract is implemented.
Ports are separate timed bus transactions; repeating blocks execute one iteration per Step.
