# B42-QX-BE-01 — Deferred Backlog
Block: B42-QX-BE-01 (Quick All / BE All any-order interaction repair)
Date: 2026-08-05
Status: PIPELINE_COMPLETE

## Current Block Deferred Items

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3
**Priority**: Low
**Context**: T_BUG_QX_BE_01 asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate `IsPttQxTarget` also accepts T3 (name[8]<='3'). Standard MES/ES setups use 2 targets
(T1+T2). T3 is the second half of even-quantity splits on rare configs.
**Deferred to**: B43 or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.

### DW-B42-02 — Live NT8 F5 verification required
**Priority**: High — required before next live trading session
**Context**: The two bug directions can only be fully verified in a live NT8 session:
- Direction 1: Quick All → BE All must place targets at BE price (not bare stop)
- Direction 2: BE All → Quick All must start from clean slate
**Deferred to**: Next live F5 session (local compile + runtime confirm)
**Action**: Press sequence in SIM account before go-live.

### DW-B42-03 — IsPttQxTarget range extension for future target slots
**Priority**: Conditional (low unless T4/T5 slots added)
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design
(PTT-QX-T1 in OCO-A, PTT-QX-T2 in OCO-B, T3 as potential 3rd slot). If a future block adds
PTT-QX-T4 or T5, `IsPttQxTarget` must be updated.
**Deferred to**: Block that adds 4th+ target slot.
