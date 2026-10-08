# File format plan
Status: reserved Formats project; no tape or snapshot parser exists yet.

## Loading boundary
Hosts read user files with size limits and pass bytes to Formats. Parsers return validated
portable data without mutating a live machine. Core installs it atomically while paused.
Detect via extension plus header/content checks; do not assume a .rom is a game snapshot.

| Format | First supported scope | Later scope |
| --- | --- | --- |
| Raw ROM | Host input for exactly one 16 KiB 48K firmware image | Named multi-ROM profiles for 128K |
| SNA | 48K, fixed-size structure and stack-derived PC | Explicit 128K variant handling |
| Z80 | v1 48K including bounded RLE and register interpretation | v2/v3 48K, then 128K pages |
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
