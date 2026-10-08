# Test instructions
Root AGENTS applies. Read docs/testing.md.
Expectations must be independent from production helpers. Use explicit state/bus vectors,
self-authored machine-code programs and synthetic byte fixtures.
Ordinary tests must run offline after package restore and require no original ROM/game.
External corpora/oracles need reviewed source/license/hash and an explicit runner/report.
Do not skip failing tests or add empty pass/skip placeholders. Report tested scope and unavailable
suites separately. Tests of ROM input in the bootstrap are not CPU or compatibility evidence.
