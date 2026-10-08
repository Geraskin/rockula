# 0011: ED data operation boundaries
Status: accepted for work order 001j.

Arithmetic captures HL/operand/carry after the payload fetch, runs four then three
Internal T labelled with the first instruction address, and commits HL/F/WZ
only after both succeed. Word loads/stores commit WZ=nn+1 after both operand
transfers; failed high-byte transfer can retain a low-byte write, but load pair
and WZ stay old. This extends ADR 0009, not a pin-accurate register pipeline.

I/R transfers use one Internal T labelled with the first instruction address and
commit afterward. LD R,A overwrites both bit 7 and low seven bits after fetches;
LD A,R samples post-fetch R. LD A,I/R copy IFF2 into PV and preserve C.
The manual's interrupt-during-LD A,I/R parity anomaly remains unsupported:
our inputs are serviced at Step entry, not an electrical sampling aperture.
An assertion during this instruction is pending until next Step and does not
retroactively clear PV. Do not claim full NMOS interrupt accuracy.

RRD/RLD capture A and memory after the read, idle four T labelled HL, then write.
After a successful write they commit A/F and WZ=HL+1. An ignored ROM write still
commits the computed A, without readback. A bus failure preserves prior A/F/WZ;
completed memory effects/time are retained. Commit policy is a logical boundary
convention; netlist traces show physical A/F updates in the next opcode fetch.
All sequences retain fault/reset behavior and whole-prefix interrupt retirement.
Sources, scope and verification: [001j](../plans/001j-ed-data-operations.md).
