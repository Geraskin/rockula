# 0003: Original implementation and user-supplied external assets
Status: accepted. Date: 2026-10-08.

## Context
The repository is public, already has MIT, and the creator wants to write the emulator from scratch.

## Decision
Implement CPU/ULA ourselves in C#. Reuse UI/test infrastructure after version/license review.
Do not vendor an emulator or automatically download firmware/games.
Keep user files and external corpora ignored, and document provenance for any test oracle/data.
Maintain root LICENSE unchanged.

## Alternatives
Wrapping a mature core would improve time-to-play but change the project's goal.
Bundling assets without a specific documented basis would blur our code license and asset rights.

## Consequences
Validation takes sustained work. Third-party binaries/data need separate manifests/notices.
The initial tests are self-contained and require no firmware. Optional replacement firmware
can be considered later with an explicit review of its terms and accuracy limits.

## Verification
Check tracked files/release contents and dependency provenance, and run normal CI with no ROM.
