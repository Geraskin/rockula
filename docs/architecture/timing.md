# Timing and bus contract
Accepted design. The CPU slices implement nominal memory-transaction ordering and sequenced internal durations.
ULA execution and Spectrum contention are not implemented. [ADR 0004](../decisions/0004-initial-cpu-bus.md)
defines logical transfers; [ADR 0005](../decisions/0005-internal-cycle-scope.md) defines
internal logical address labels. Neither establishes electrical address/refresh accuracy.

## One clock
Maintain an unsigned 64-bit absolute T-state count for a machine and a derived frame phase.
Hardware advances only through the scheduler. Use integer arithmetic for CPU, tape and device
events; rational sample accumulators handle conversion to host sample rates.

The initial 48K profile uses 224 T-states per line and 312 lines per frame (69,888 T-states).
At a nominal 3,500,000 Hz this is approximately 50.08 frames/second. Visible pixels are
256 by 192; border/cropping dimensions are a presentation decision distinct from the raster.
A profile document must specify the reference event defining phase zero before code relies on it.

## CPU bus sequencing
A CPU instruction cannot simply mutate all memory/ports and advance ULA once by its total time.
Device-visible effects need correct relative timing within the instruction.

A proposed bus distinguishes opcode/M1 fetch, ordinary memory read/write, port read/write,
interrupt acknowledge and internal cycles. Each operation identifies its address, base cycle
cost and the T-state at which the read or write becomes visible. A machine implementation adds
contention waits and advances ULA/tape/audio up to that sampling point before committing the effect.

A CPU-only test bus uses the same operation contract with no contention. Distinguish timed
execution from untimed debugger peek and snapshot installation. Reads must not be cached across
bus cycles when a device or floating bus can change.

Keep instruction stepping as the public debug convenience, but implement it in terms of
timed operations. Do not expose coroutine allocation on every T-state; a straightforward
synchronous sequencer is acceptable. Optimization must preserve the event trace.

## Contention and port rules
Document exact phase windows and delay patterns for the selected ULA. Contention depends on
access type, address/port and current phase; CPU totals must include inserted waits. An
eight-entry delay table alone is not a complete I/O contention model.
Even port aliases decode the ULA; do not implement keyboard/border only at the literal port 0xFE.

For every contention rule, test outside/inside display fetch windows, the first/last applicable
cycle, contended/uncontended addresses, and each relevant port class. Do not mix clone profiles.

## Raster ordering
ULA fetches bitmap and attribute data at documented times. Writes during a scanline affect only
data fetched afterward. Preserve border transitions at the relevant raster position, FLASH phase,
and the last fetched byte needed for floating-bus behavior.
An end-of-frame redraw from final RAM is an explicitly limited first renderer, not raster accuracy.

Frame output is published at a consistent boundary. An instruction may cross that boundary;
preserve remainder cycles rather than truncating/resetting instruction time.
Advance all crossed frame/device events, including when a run budget spans multiple boundaries.

## Interrupts and HALT
The interrupt line has a defined duration and phase, not a callback once per host frame.
Test IFF1/IFF2, EI's delayed acceptance, DI, HALT fetch behavior, R updates, IM 0/1/2, NMI and
RETN/RETI against a stated NMOS model. HALT continues hardware time until an interrupt or budget
boundary; it is not an infinite-loop detector.

Implemented HALT and DI/EI boundary behavior is defined in
[ADR 0006](../decisions/0006-halt-and-ei-boundaries.md). Each halted Step performs a
bounded logical M1 fetch; interrupt acceptance/exit and electrical refresh are deferred.

## Tape and audio
Tape advances its pulse cursor against absolute T-states; EAR reads observe the current signal.
The beeper records output transitions with timestamps. PCM generation uses a deterministic
resampler/filter and rational accumulator; preserve their phase in save states.
Turbo cannot shorten guest tape pulses. Fast-load traps are separate optional policies restricted
to a verified firmware hash, and must fall back to physical pulse playback.

## Accuracy gates
1. CPU semantics: full registers, memory, flags and nominal bus cycles.
2. Timed bus: event offsets, contention and interrupt tests.
3. Raster: byte/attribute fetches, border changes and floating-bus vectors.
4. Integration: replayable machine traces and compatibility cases.

Passing ZEX-style flag exercisers does not prove bus timing, and a screenshot alone does not
prove the machine model. Each gate needs independent expected results.
