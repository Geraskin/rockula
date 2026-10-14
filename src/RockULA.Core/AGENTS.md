# Core instructions
Root AGENTS applies. Read docs/architecture before introducing hardware APIs.
Core is synchronous, deterministic C#, with no GUI/host-device/file/network dependencies.
One machine owner, one integer T-state clock. CPU operations carry type/address/timing so future
contention can occur within instructions. Untimed inspection is a separate path.
Capture every internal state element that changes future execution, with explicit reset policy.
Use spans/arrays with documented ownership. Avoid per-instruction allocations and global state.
Our CPU/ULA are original work; independently test semantic and bus effects before claiming support.
Core implements the declared Z80 opcode pages, Q/WZ, functional 48K map/ports,
keyboard/Kempston/Sinclair input, frame execution and final-RAM video. Optional
BeeperSampler integrates bit-4 intervals into bounded rational PCM; callers drain it.
Read docs/architecture/z80-coverage.md and ADRs 0004–0014 for support/limits.
Contention, timed ULA, tape and complete internal state capture remain deferred.
Ports are separate timed bus transactions; repeating blocks execute one iteration per Step.
