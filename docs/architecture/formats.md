# File format plan
Status: bounded 48K SNA and ordinary 48K Z80 v1/v2/v3 implemented; tape and other models deferred.
SNA requires exactly 49,179 bytes, saved SP 4000–FFFE, IM 0–2 and border 0–7.
PC is popped from copied RAM without erasing bytes; restored SP wraps at FFFE.
Header IFF2 bit 2 initializes both IFFs. Import initializes clock/phase and missing
latches as described in ADR 0013; this is not a lossless internal save state.

## Loading boundary
Hosts read user files with size limits and pass bytes to Formats. Parsers return validated
portable data without mutating a live machine. Core installs it atomically while paused.
Detect via extension plus header/content checks; do not assume a .rom is a game snapshot.

| Format | First supported scope | Later scope |
| --- | --- | --- |
| Raw ROM | Host input for exactly one 16 KiB 48K firmware image | Named multi-ROM profiles for 128K |
| SNA | 48K, fixed-size structure and stack-derived PC | Explicit 128K variant handling |
| Z80 | v1 raw/RLE and v2/v3 ordinary 48K, exact pages 8/4/5 | Other hardware modes/128K deferred |
| TAP | Length-prefixed blocks converted to pulse sequences | Optional hash-guarded fast loading |
| TZX | Not in first playable release; reject clearly | Documented supported block matrix |
| Internal state | Versioned complete hardware state | Migration/rewind support after stable state contract |

## Snapshot requirements
Parse little-endian fields explicitly; avoid native-struct reinterpretation and host endianness.
For SNA account for the saved PC on the guest stack and SP adjustment. Validate reads at the top
of memory, wrap cases and malformed stack locations before installation.
For Z80 distinguish v1 from extended headers; validate lengths, compression markers, decompressed
sizes, hardware identifiers, duplicate/missing pages and unsupported model fields.

Restore interrupt and R register semantics according to the format specification. A snapshot
does not contain original firmware: match/select a user-supplied profile and report ambiguity.
Non-represented raster/tape/audio phases use a documented initialization policy.

## Tape requirements
Validate TAP block boundaries, checksum interpretation and total size. Convert standard blocks
into explicit pilot, sync, data and pause timing with bounded durations.
TZX adds control flow, loops, jumps, selections and signal blocks: unsupported block types
must yield a precise error, never be skipped as if playback were complete.
Cap expanded duration, nested control flow and iteration count to prevent unbounded work.

## Safety and tests
Bound file size, output allocation, arithmetic and decompression independently. Truncated input,
overflow, zero-length blocks and exact maximum limits need tests. Report offset and reason.
Parser unit tests use self-authored fixtures; larger external fixtures remain ignored and carry
a source/license/hash manifest. Round-trip tests need independent byte/field assertions too.

Future export operations declare what is lost. Do not claim an external snapshot is an exact
save of tape position, video phase or audio filters. Read/write the internal state only after
its versioned contract and atomic-restore regression are implemented.

## Implemented Z80 import policy (001n)
Input is bounded to 200000 bytes and decoded RAM to exactly 49152. Base AF fields
are A then F; other word pairs are little-endian. R bit7 comes from normalized
flag byte 12 (255 means 1); IFF1/IFF2 are independent; IM3 rejects. V1 nonzero PC
selects exact-size raw data or ED ED count/value runs with a required final
00 ED ED 00 marker. Zero/truncated/overflowing runs and suffixes reject.

A zero base PC selects an extended header of 23/54/55 bytes. Only hardware mode
0, no 16K/AY modifiers or paged Interface 1/MGT/Multiface ROM, is supported. Pages
8/4/5 map to 4000/8000/C000, in any order, each once; raw FFFF blocks contain
16384 bytes, compressed blocks expand to exactly that size. Missing/duplicate/
unknown pages reject. Z80 does not pop PC or adjust/erase the stack.

V3 timing counters and emulator configuration (issue/sync/joystick preferences,
unpaged device metadata, ROM classification fields) are not restored as full
hardware state. Imports use the existing clock/phase-zero and absent-latch policy;
host controls come from user preferences. No snapshot export or game compatibility
claim is implied. See ADR 0015 and the work order for explicit validation evidence.
