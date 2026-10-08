# Core instructions
Root AGENTS applies. Read docs/architecture before introducing hardware APIs.
Core is synchronous, deterministic C#, with no GUI/host-device/file/network dependencies.
One machine owner, one integer T-state clock. CPU operations carry type/address/timing so future
contention can occur within instructions. Untimed inspection is a separate path.
Capture every internal state element that changes future execution, with explicit reset policy.
Use spans/arrays with documented ownership. Avoid per-instruction allocations and global state.
Our CPU/ULA are original work; independently test semantic and bus effects before claiming support.
The bootstrap implements only RomImage validation, not hardware execution.
