# Architecture decisions
Use sequential filenames and the [template](template.md). Records distinguish accepted,
proposed and superseded choices; new evidence may revise them without hiding the earlier decision.

- [0001: C# and deterministic host-independent core](0001-managed-core.md)
- [0002: First model and incremental accuracy](0002-48k-first.md)
- [0003: Original implementation and user-supplied assets](0003-original-core-and-assets.md)
- [0004: Initial CPU transaction timing and fault/reset behavior](0004-initial-cpu-bus.md)
- [0005: Nominal internal cycles and logical address limits](0005-internal-cycle-scope.md)
- [0006: HALT and EI instruction boundaries](0006-halt-and-ei-boundaries.md)
- [0007: Interrupt boundary inputs](0007-interrupt-boundary-inputs.md)
- [0008: ED interrupt control and IM2](0008-ed-interrupt-control.md)
- [0009: CB operations and instruction-boundary WZ](0009-cb-and-wz-state.md)
