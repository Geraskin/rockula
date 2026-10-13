# 0012: Port transactions and block iteration boundaries
Status: accepted for work order 001k.

Append PortRead/PortWrite to the bus kinds, leaving existing numeric values intact.
CPU I/O uses four T with transfer offset four, including the nominal I/O wait T.
The bus inserts additional waits and advances devices before sampling/committing
through separate protected ReadPort/WritePort hooks; defaults FF/ignored. No
memory or acknowledgement hook services ports. This is a logical transfer
convention, not electrical IORQ/RD/WR or live WAIT polling accuracy.

Immediate ports capture old A, then read n, transfer, and commit A/WZ afterward.
OUT (n),A sets WZ high=A and low=(n+1) mod256; IN uses full port+1. ED ports
capture original BC; WZ=BC+1 commits after successful I/O/result. IN (C) changes
flags but no register/memory; OUT (C),0 emits zero for the selected NMOS model.

One Step executes one block iteration. LDI/LDD read source, write destination,
idle two T labelled old DE, then commit HL/DE/BC/F. CPI/CPD read, idle five T
labelled old HL, then commit HL/BC/F and WZ=old WZ+direction. LDI/LDD preserve WZ.
Block input idles one T labelled first instruction address, reads old BC, writes
old HL, then commits B/HL/F/WZ=old BC+direction. Block output idles one T,
reads old HL, commits decremented B before writing port new BC, then commits
HL/F/WZ=new BC+direction after success. An output failure can thus retain new B.
These register commit points are logical policies, not netlist pipeline claims.

After a nonterminal repeat, idle five more T labelled old DE (transfer) or old
HL (search/I/O), then rewind PC by two. X/Y use the rewound ED address high byte.
Memory repeats set WZ=rewound PC+1; I/O retains its port WZ and applies Banks'
additional H/PV formulas using post-decrement B/data/carry. Base-iteration effects
are already committed before repeat idle; a repeat-idle fault retains them but
not PC rewind/repeat flag corrections. Completed memory/port effects never roll
back. Every failure faults the CPU until Reset and does not retire EI inhibition.

Interrupts are polled at next Step, including between repeats; first iteration
retires EI delay. R increments for the newly fetched ED/payload on each iteration.
PC rewind discards prior DD/FD prefixes. Next iteration observes changed bytes,
without a persistent repeat latch or heap allocation. Zero counts wrap normally,
so each Step is bounded even for 64K transfers. Host budgets bound whole runs.
No new save-state schema, Spectrum port decoder or pin accuracy is introduced.
Sources and evidence: [001k](../plans/001k-ports-and-blocks.md).
