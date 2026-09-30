# Deferred Backlog -- B110

## Block: B110 (DW-B110: Remove CancelQxBracketsForFollowers from Leader Path)
## Date: 2026-08-26
## Status: PIPELINE_COMPLETE pending F5 gate

All 7 scans zero. ptt-sync-and-verify.ps1 confirmed 0 MISMATCH (16 files). VERIFY_PASS.
F5 gate and live Combo C re-test are Director-owned runtime actions.

---

## Items CLOSED This Block

| Item | Description | Closed By |
|------|-------------|-----------|
| DW-B110 | CancelQxBracketsForFollowers call-site removed from PttQuickExit.Execute leader path -- Combo C defect root cause eliminated | B110-T1 |
| B107-DEFER-01 (superseded) | B107 F5 gate -- superseded by DW-B110-POST-01 (same NT8 sync/compile batch) | B110 pipeline |
| B107-DEFER-02 (superseded) | B107 Combo C live re-test -- superseded by DW-B110-POST-02 (leader path changed; re-test must target B110 code) | B110 pipeline |

---

## Deferred Items -- Added This Block

### DW-B110-POST-01: Director F5 Gate

**Priority**: P0 -- prerequisite for live Combo C re-test
**Context**: `ptt-sync-and-verify.ps1` completed with 0 MISMATCH (16 files MD5-verified).
The NT8 F5 compilation step is the runtime compile gate. It must produce "Compilation succeeded"
(zero errors) after sync. File copy alone does not activate the new IL in the NT8 runtime.
**Action**: Director presses F5 (or Tools -> Edit NinjaScript -> Compile) in NinjaTrader 8
after confirming sync pass. Must produce zero compilation errors.
**Owner**: Director.
**Deferred to**: Director (immediate, prerequisite for DW-B110-POST-02).

---

### DW-B110-POST-02: Live Combo C Re-Test

**Priority**: P0 -- required before next live trading session involving BE-ALL then QX-ALL
**Context**: DW-B110 code change (removal of `CancelQxBracketsForFollowers` from leader path)
has been implemented and verified by independent code inspection (VERIFY_PASS). Full behavioral
validation of the Combo C scenario (BE-ALL followed by QX-ALL) requires a live NT8 session
with leader + follower accounts.

**Test sequence**:
1. Enter position on leader (Sim101) + followers (Sim102/103/104) via PTT Copier
2. Fire BE-ALL -- confirm BE brackets placed on all 4 accounts
3. Fire QX-ALL -- confirm:
   - Zero [BE-DIAG] lines emitted during QX sweep (guard 3b firing correctly for all followers)
   - All 4 accounts covered by PTT-QX-Stop/PTT-QX-T1/T2/T3 brackets
   - Zero [BE-RETRY] events (no spurious bracket re-placement)
   - No unprotected position on any account

**Pass criterion**: zero [BE-DIAG] lines during QX sweep; all 4 accounts covered; zero BE-RETRY;
no unprotected position.
**Fail criterion**: any unprotected position; any [BE-DIAG] line that was previously absent;
any BE-RETRY event on a follower account during the QX sweep.
**Owner**: Director.
**Deferred to**: Director SIM gate session (after DW-B110-POST-01 F5 green).

---

### DW-B110-POST-03: Spec Update

**Priority**: P1
**Context**: `specs/002-trade-copier-spec.html` must be updated to reflect B110 closure.
**Actions**:
1. Section `#section-dw-b110`: change badge to CLOSED B110-T1; add closure note
   "CancelQxBracketsForFollowers removed from PttQuickExit.Execute leader path. VERIFY_PASS 2026-08-26."
2. Section `#section-live-test-2026-08-25` (or equivalent Combo C live test section):
   add row "Combo C: AWAITING RE-TEST after B110-T1 -- pending Director F5 gate + SIM run."
**Owner**: Director (or next pipeline block after Combo C PASS).
**Deferred to**: After DW-B110-POST-02 PASS.

---

### OBS-B110-01: CopyEngine.cs:923 Comment Drift (non-blocking)

**Priority**: P2
**Context**: `CopyEngine.cs` L923 comment reads:
"Called by PttGlobalQuickExit.Execute before placing new PTT-QX-* orders on the leader."
Post-B110, `CancelQxBracketsForFollowers` is no longer called by any production code path.
The comment is inaccurate but CopyEngine.cs was deliberately kept NO CHANGE per plan Section 3.
**Action**: Update comment to reflect current state (e.g., "Available for future use; no
current production caller after B110.").
**Owner**: Next pipeline block that touches CopyEngine.cs.
**Deferred to**: Future (dedicated comment-hygiene pass or next CopyEngine block).

---

## Carried Forward from B107 (status unchanged -- unaffected by B110)

### DW-B107 -- MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers

**Priority**: P2 (correctness violation, functionally benign in observed test)
**Context**: `MoveStopToBreakEven` Step A (`CopyEngine.cs` ~L3380) collects target orders
with no native-vs-PTT discrimination and no count cap. Stale PTT-BE-Target-4 included in
snapshot. Same class as DW-B106 (fixed QX path in B107-T1; BE path not in scope).
**Deferred to**: B108 (next pipeline block after current testing batch).
**Full brief**: `docs/brain/DW-B107/00-defect-brief.md`

---

### DW-PTT-BE-FIX-03 -- Pre-existing test build errors (83 errors in CopyEngineTests.cs)

**Priority**: High -- blocks full test suite build
**Context**: Pre-existing errors in CopyEngineTests.cs stub infrastructure (83 errors) plus
CS0433 Globals ambiguity at CopyEngine.cs:L3350. Confirmed pre-existing, unrelated to B110.
**Deferred to**: Dedicated test infrastructure remediation block.

---

### DW-B89-DEFERRED-01..06 (carry-forward, unaffected by B110)

| Item | Description |
|------|-------------|
| DW-B89-DEFERRED-01 | Ctrl+F5 NT8 compilation gate (DW-B89 changes) |
| DW-B89-DEFERRED-02 | SIM gate PATH A nominal (3 cycles) |
| DW-B89-DEFERRED-03 | SIM gate PATH A buf=0 edge case (short position) |
| DW-B89-DEFERRED-04 | SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles) |
| DW-B89-DEFERRED-05 | SIM gate DW-B87 timing race cycle |
| DW-B89-DEFERRED-06 | Spec update: close DW-B89/B88/B87 in spec HTML |

---

### DW-B42-01/02/03 (carry-forward, unaffected by B110)

| Item | Description |
|------|-------------|
| DW-B42-01 | T_BUG_QX_BE_01 does not assert PTT-QX-T3 |
| DW-B42-02 | Live NT8 F5 verification required (Direction 1 + 2) |
| DW-B42-03 | IsPttQxTarget range extension for future T4/T5 slots |

---

### DW-PTT-BE-FIX-01/02 (carry-forward, unaffected by B110)

| Item | Description |
|------|-------------|
| DW-PTT-BE-FIX-01 | DW-B85 Option A: Lazy re-resolve for null followers |
| DW-PTT-BE-FIX-02 | SIM gate: Path B 3-cycle runtime verification |

---

## Summary

| Category | Count | Items |
|----------|-------|-------|
| Closed this block | 3 | DW-B110, B107-DEFER-01 (superseded), B107-DEFER-02 (superseded) |
| New deferred -- B110 pipeline | 3 | DW-B110-POST-01 (F5), DW-B110-POST-02 (Combo C), DW-B110-POST-03 (spec) |
| New deferred -- observation | 1 | OBS-B110-01 (comment drift, P2) |
| Carry-forward from B107 (unchanged) | 14 | DW-B107, DW-PTT-BE-FIX-03, DW-B89-DEFERRED-01..06, DW-B42-01/02/03, DW-PTT-BE-FIX-01/02 |

**Total open items**: 18 (4 new + 14 carry-forward)
