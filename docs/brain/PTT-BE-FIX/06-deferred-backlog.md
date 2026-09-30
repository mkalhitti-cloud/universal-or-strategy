# PTT-BE-FIX -- Deferred Backlog
Block: PTT-BE-FIX (DW-B84/B85/B86 Productionisation)
Date: 2026-08-22
Status: PIPELINE_COMPLETE

---

## Carry-Forward Items (from B42-QX-BE-01 -- unchanged)

### DW-B42-01 -- T_BUG_QX_BE_01 does not assert PTT-QX-T3
**Priority**: Low
**Context**: T_BUG_QX_BE_01 asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate `IsPttQxTarget` also accepts T3 (name[8]<='3'). Standard MES/ES setups use 2 targets
(T1+T2). T3 is the second half of even-quantity splits on rare configs.
**Deferred to**: B43 or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.

### DW-B42-02 -- Live NT8 F5 verification required
**Priority**: High -- required before next live trading session
**Context**: The two bug directions can only be fully verified in a live NT8 session:
- Direction 1: Quick All -> BE All must place targets at BE price (not bare stop)
- Direction 2: BE All -> Quick All must start from clean slate
**Deferred to**: Next live F5 session (local compile + runtime confirm)
**Action**: Press sequence in SIM account before go-live.

### DW-B42-03 -- IsPttQxTarget range extension for future target slots
**Priority**: Conditional (low unless T4/T5 slots added)
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design
(PTT-QX-T1 in OCO-A, PTT-QX-T2 in OCO-B, T3 as potential 3rd slot). If a future block adds
PTT-QX-T4 or T5, `IsPttQxTarget` must be updated.
**Deferred to**: Block that adds 4th+ target slot.

---

## Current Block Deferred Items

### DW-PTT-BE-FIX-01 -- DW-B85 Option A: Lazy re-resolve for null followers
**Priority**: Medium
**Context**: When a follower account is not in Account.All at LoadRules() time, the Option B
warning is emitted. Option A would re-attempt resolution lazily in AllAccounts() when the
account later appears in Account.All. Per spec, Option A is deferred.
**Deferred to**: Next PTT productionisation block.
**Fix**: In AllAccounts(), replace null-skip with a lazy re-resolve: if followers[i] is null
but Account.All now contains the name, update followers[i] and yield return it.

### DW-PTT-BE-FIX-02 -- SIM gate: Path B 3-cycle runtime verification
**Priority**: High -- required before next live trading session with QX-ALL then BE-ALL sequence
**Context**: T1 (DW-B86) fixes the stop name guard but full SIM verification of Path B
(QX-ALL then BE-ALL, 3 cycles, checking stops=N > 0 on each follower) requires a live NT8
session with leader + follower accounts and open positions.
**Deferred to**: Next live F5 session.
**Action**: Run Path B test sequence (3 cycles) in SIM before go-live. Pass criteria:
"[BE] DW-B84-01 acc.Change() SimXXX stops=N newStop=X" with N > 0 for each follower account
across all 3 cycles. Fail criteria: stops=0 or [BE-DIAG-F] fires for any follower.

### DW-PTT-BE-FIX-03 -- Pre-existing 83 build errors in CopyEngineTests.cs
**Priority**: High -- blocks full test suite build
**Context**: There are 83 pre-existing errors in the test project baseline (CopyEngineTests.cs
stub infrastructure) plus 1 Globals ambiguity (CS0433 at CopyEngine.cs:L3350), totalling 84
pre-existing errors. These were confirmed pre-existing by engineer stash roundtrip (git stash ->
build -> git stash pop, identical error count). Unrelated to PTT-BE-FIX changes; all errors are
in CopyEngineTests.cs (test file untouched by this epic) and Globals at L3350 (52 lines before
the T2 edit range at L3402).
**Deferred to**: Dedicated test infrastructure remediation block.
**Action**: Separate remediation track. Investigate CopyEngineTests.cs stub failures + Globals
ambiguity at L3350. Fix must not touch PTT-BE-FIX delivered code.
