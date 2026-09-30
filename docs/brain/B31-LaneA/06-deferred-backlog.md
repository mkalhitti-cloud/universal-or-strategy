# B31-LaneA Deferred Backlog

**Block**: B31-LaneA
**Date**: 2026-07-17
**Pipeline**: PIPELINE_COMPLETE (VERIFY_PASS confirmed, commit c49d25a3)
**[Fact] count**: 146

---

## Block B31-LaneA Closed Items

| Defect | Severity | Status |
|--------|----------|--------|
| DW-B31-01 | P0 | **CLOSED** — MoveStopToBreakEven + TightenOneStop now use in-place `order.StopPrice + acc.Change(new Order[]{order})`. TryCreateStopWithRetry deleted. ATM OCO link preserved. |
| DW-B31-02 | P2 | **CLOSED** — NT8-046 appended to NT8_COMPILER_RULES.md documenting multi-param silent no-op vs. single-array safe path distinction. |

---

## Deferred Items (carry to B32)

### DW-B31-DEFER-01 | P2 | Raw `.Orders` without `.ToList()` in HasWorkingEntries
**Source**: Noted in B30-LaneC verification report (CopyEngine.cs:L733).
**Status**: Pre-existing before B31. Not in scope for B31-LaneA.
**Carry-forward**: Candidate for a future lane targeting `HasWorkingEntries` snapshot safety.

### DW-B31-DEFER-02 | P3 | Live test required before Director closes DW-B31-01
**Action**: Director runs B31 LIVE TEST PROCEDURE (see specs/002-trade-copier-spec.html#block-b31).
**Criteria**:
  - Status bar shows "Sim101: BE moving stop -> {price}" then "BE stop moved @ {price}"
  - Orders tab: Stop1 still Working (NOT Cancelled)
  - No PTT-BE-Stop orphan order in Orders tab
  - OCO column on Stop1 unchanged
  - Button goes blue "BE Live"
  - Trail ratchets Stop1 price up as PnL improves
**Status**: Pending Director action post-F5 green.

### DW-B31-DEFER-03 | P3 | MoveStopToBreakEven_DoesNotCallCancel may behave differently under Mono/NT8 JIT
**Context**: T_B31_02 uses `GetMethodBody().LocalVariables` to detect `OrderAction` typed locals.
JIT optimization level under NT8's embedded Mono runtime may produce different LocalVariables results
than standard .NET desktop. If T_B31_02 fails in NT8 embedded test runner, replace with a source-scan
approach (read CopyEngine.cs text, assert "acc.Cancel" not in method body).
**Status**: Speculative. Monitor on first NT8 F5 test run.

---

## Notes for B32

- `TryCreateStopWithRetry` is gone. Do NOT re-introduce it.
- The SCAN pattern for NT8-046 (`TryCreateStopWithRetry|acc\.Cancel\(new Order\[\]`) should be
  included in any future scan checklists that touch CopyEngine.cs stop-management methods.
- B31 commit is `c49d25a3` on branch `001-agent-arena-platform` (Wave workspace main).
- Prior block anchor: `a5186877` (hotfix-F2-F5, pre-B31 baseline).
