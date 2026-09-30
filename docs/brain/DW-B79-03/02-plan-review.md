# DW-B79-03 Plan Review

**Reviewer**: ptt-plan-reviewer
**Plan reviewed**: docs/brain/DW-B79-03/02-architecture-plan.md
**Review date**: 2026-08-10
**Phase**: 2 (Plan Review)

---

## 1. Summary of Plan

The plan addresses a race condition (DW-B79-03) where follower accounts receive conflicting QX
orders because their ATM brackets have not yet arrived in `acc.Orders` at the time
`PttGlobalQuickExit.ExecuteOne` runs its cancel step. The fix (Direction A) adds a two-line
pre-cancel guard in `ExecuteOne`: when `skipIfFollower=false` (follower path only),
call `CopyEngine.Instance?.CancelQxBrackets(acc, instr)` before constructing `PttQuickExit`.
This mirrors the leader's natural behavior (leader ATM brackets are always Working and
cancelled by `PttQuickExit.Execute`'s own `BuildQxSnapshot/CancelQxBrackets`).

The plan also documents Gap 2 (REPAIR-08, commit a3f68559) as already closed in
`PttBreakEven.SnapshotTargetsLocal` and requires no further code change.

Scope: 1 file modified (`PttGlobalQuickExit.cs`), 1 file created (`B79Tests.cs`),
1 file updated (`NO-PIPELINE-REPAIRS.md`).

---

## 2. Per-Section Checklist

### Section 1 — Problem Analysis

| Check | Result | Evidence |
|-------|--------|----------|
| Root cause matches actual source code | PASS | `PttGlobalQuickExit.Execute()` (source line 64) calls `ExecuteOne(follower, ..., skipIfFollower: false)` with no pre-cancel for follower ATM brackets. Race timeline is technically accurate. |
| Gap 2 status (plan says DONE) verified in source | PASS | `PttBreakEven.cs:321-325` confirmed. Lines 321-325 contain exactly `Working \| Accepted \| Submitted \| Initialized \| TriggerPending` with `// REPAIR-08` annotations. Gap 2 is closed. |
| NT8 async lag reference (line 1721) correct | PASS | NT8_FULL_REFERENCE.md OrderState section confirms async propagation. CancelSubmitted is a valid NT8 OrderState (line 971) and is correctly excluded from BuildQxSnapshot stateOk. |
| Per-chart QX button exclusion documented | PASS | Section 1.2 correctly identifies `PttQuickExit.Execute(skipIfFollower=true)` as the per-chart path. Source confirms: `TradeCopierPanel` calls `executor.Execute(acc, instr, t1, t2, skipIfFollower: true)` via compat overload. |

### Section 2 — Direction Decision

| Check | Result | Evidence |
|-------|--------|----------|
| Direction A technically prevents the conflict | PASS | `CancelQxBrackets(2-param)` at CopyEngine.cs:586 cancels orders in `Working\|Initialized\|Accepted\|Submitted\|TriggerPending`. After this call, follower brackets enter `CancelSubmitted`. `BuildQxSnapshot` uses same stateOk (CopyEngine.cs:625-629) — `CancelSubmitted` not in stateOk — so snapshot=0 and the internal cancel is a no-op. PTT-QX submits to a clean account. |
| Guard clearly described | PASS | Section 3.3 contains exact `if (!skipIfFollower) CopyEngine.Instance?.CancelQxBrackets(acc, instr);` with full XML doc. Section 3.4 shows call chain after fix. |
| Direction B rejection justified | PASS | Correctly identified as creating a protection gap (follower ends with no stop and no ATM brackets). |
| Direction B-revised rejection justified | PASS | Correctly identified as symptom treatment without fixing root cause. Correct note about price risk from `CancelSubmitted` orders. |
| Direction C live-trading safety assessment present | PASS | Section 2.3 documents bare PTT-BE-Stop as position-protecting (valid StopMarket GTC for full qty). Rejection justified: UX regression is fixable and fix is low-risk. |
| `PttQuickExit.Execute` unchanged (steering note) | PASS | Section 6 "Files Explicitly NOT Changed" confirms. |

### Section 3 — Implementation Specification

| Check | Result | Evidence |
|-------|--------|----------|
| Change location correct | PASS | `PttGlobalQuickExit.cs` `ExecuteOne` (source lines 92-101 confirmed in source). |
| Current `ExecuteOne` CYC=1 accurate | PASS | Source lines 92-101: single delegation statement, no conditionals. CYC=1. |
| New `ExecuteOne` CYC=2 accurate | PASS | `if (!skipIfFollower)` adds 1 branch. McCabe CYC = 1 (base) + 1 (conditional) = 2. Plan's branch-count reasoning at Section 4 is correct. |
| Null-conditional `?.` on `CopyEngine.Instance` | PASS | Matches existing pattern throughout the file (e.g. source line 40: `engine?.IsFollowerAccount`, line 54: `engine?.FindRule`). Silent skip when engine is null is intentional and correct. |
| Call chain diagram accurate | PASS | Section 3.4 correctly shows leader path (skipIfFollower=true) bypasses new guard; follower path (skipIfFollower=false) fires pre-cancel before `PttQuickExit.Execute`. |
| No helper extraction needed | PASS | Single `if` guard calling existing tested method. Correct per "minimal change" principle. |

### Section 4 — CYC Analysis

| Method | Plan Before | Plan After | Source Verified | Result |
|--------|------------|------------|-----------------|--------|
| `PttGlobalQuickExit.Execute` | 8 | 8 | CYC=8 confirmed: 7 branch points (foreach×2, if-continue×2, rule null-check, follower foreach, follower null continue) + base = 8. | PASS |
| `PttGlobalQuickExit.ExecuteOne` | 1 | 2 | CYC=1 in source (zero conditionals). Fix adds 1 branch → CYC=2. | PASS |
| `PttGlobalQuickExit.ResolveQuickTicks` | 2 | 2 | Source: 2 conditionals (engine null, tick lookup). CYC=2. Unchanged. | PASS |
| `PttGlobalQuickExit.SnapshotTargetOrders` | 4 | 4 | Source: null guard(1), foreach(2), stateOk compound(3), isTarget compound(4). CYC=4. Unchanged. | PASS |
| `CopyEngine.CancelQxBrackets(acc,instr)` | 6 | 6 | CopyEngine.cs:584 comment confirms CYC=6. Unchanged (called, not modified). | PASS |

**All methods <= 8. No CYC violation.** PASS.

### Section 5 — Test Plan

| Check | Result | Evidence |
|-------|--------|----------|
| Minimum 2 new [Fact] tests | PASS | T_DW_B79_03_01 and T_DW_B79_03_02 are mandatory; T_DW_B79_03_03 is optional. |
| T_DW_B79_03_01 has concrete assert conditions | PASS | `cancelInvocationCount >= 1` (pre-cancel fired for follower). Call order verified via spy. |
| T_DW_B79_03_02 has concrete assert conditions | PASS | `executeOneCancelCount == 0` (leader path does not fire the new guard). |
| T_DW_B79_03_03 (optional) has concrete assert conditions | PASS | `result.Count == 0` given CancelSubmitted order. Tests the invariant that makes Direction A work. |
| Acceptance criterion correct | PASS | `>= 539` [Fact] count. Both targets (541 min, 542 recommended) exceed this. |
| Tests exercise the chosen direction | PASS | T1 directly tests `skipIfFollower=false` path. T2 directly tests `skipIfFollower=true` path (guard does NOT fire). T3 verifies `BuildQxSnapshot` excludes `CancelSubmitted`. All three cover Direction A's correctness. |

### Section 6 — File Change Summary

| Check | Result | Evidence |
|-------|--------|----------|
| Change files accurate | PASS | `PttGlobalQuickExit.cs` (modify), `B79Tests.cs` (create), `NO-PIPELINE-REPAIRS.md` (modify). |
| Not-changed files accurate | PASS | `PttQuickExit.cs`, `CopyEngine.cs`, `PttBreakEven.cs`, `TradeCopierPanel.cs` all confirmed as not requiring change. |
| `NO-PIPELINE-REPAIRS.md` update documented | PASS | Appendix C provides exact carry-forward table row template. |

### Section 7 — 7-Scan Checklist

| Scan | Scope | Result |
|------|-------|--------|
| SCAN-01 lock() ban (JS-021) | PttGlobalQuickExit.cs + B79Tests.cs | PASS — grep command present, expected 0 matches. Confirmed: no `lock(` in current source. |
| SCAN-02 throw new (JS-001) | PttGlobalQuickExit.cs + B79Tests.cs | PASS — grep command present. Note on test arrange-only is acceptable. |
| SCAN-03 return null (JS-002) | PttGlobalQuickExit.cs | PASS — grep command present. `SnapshotTargetOrders` confirmed returns empty list never null (source line 112). |
| SCAN-04 async void (JS-033) | PttGlobalQuickExit.cs | PASS — grep command present. All methods are synchronous void. |
| SCAN-05 non-ASCII (JS-066 equivalent) | Both files | PASS — Select-String command present with correct `[^\x00-\x7F]` pattern. |
| SCAN-06 CYC audit | PttGlobalQuickExit.cs | PASS — complexity_audit.py command present with correct expected values. |
| SCAN-07 [Fact] count | All test files | PASS — Select-String command present with `>= 539` expected minimum. |

All 7 scans present with exact commands, expected zero results, and explicit PASS assertions. ✅

---

## 3. Rule Compliance Check (DNA Block)

### JS-021 — No lock() (P0)

**Result**: PASS  
No `lock(` in `PttGlobalQuickExit.cs` (confirmed in source). No lock in proposed new code in Section 3.3. `CopyEngine.CancelQxBrackets` itself uses no lock (CopyEngine.cs:578 header confirms: "JS-021: no lock").

### JS-001 — No throw in hot paths (P0)

**Result**: PASS  
New code in `ExecuteOne` (Section 3.3): `if (!skipIfFollower) CopyEngine.Instance?.CancelQxBrackets(...)` — no `throw`. `CopyEngine.CancelQxBrackets(2-param)` (CopyEngine.cs:603): `try { acc.Cancel(...); } catch { }` — swallows exceptions, no re-throw. No new `throw` introduced. Note: existing `try/catch` blocks in `PttQuickExit.Execute` are not being modified and are pre-existing.

### JS-002 — No return null (P0)

**Result**: PASS  
`ExecuteOne` returns `void`. `SnapshotTargetOrders` returns empty list (never null), confirmed source line 112. No new `return null` introduced.

### JS-033 — No async void (P0)

**Result**: PASS  
`ExecuteOne` is `private void` (synchronous). No `async` keyword in proposed change. All methods in `PttGlobalQuickExit.cs` are synchronous.

### JS-066 — ASCII-only (from DNA block; no separate catalog entry in current catalog version)

**Result**: PASS  
XML doc comment in Section 3.3 uses only ASCII characters. No Unicode, emoji, or curly quotes.

### CYC <= 8 mandate (P1)

**Result**: PASS  
See Section 4 table. All touched methods remain <= 8.

### NT8 violations

**Result**: PASS  
- No `async/await` in NT8 lifecycle methods.
- No `Account.All` in constructor (constructor not modified; `Account.All` is used in `Execute()`, called from UI thread post-Loaded, which is correct per NT8-021).
- `CreateOrder` uses `PTT-QX-` prefix (existing, unchanged code in `PttQuickExit.cs`).
- No `DateTime.Now` (uses `DateTime.MaxValue` for GTC).
- No hardcoded `#RRGGBB` hex colors.
- No sealed `TradeCopierWindow`.
- No `FontFamily` override.

---

## 4. Critical Check Results

### Check A — Gap 2 Truly Already Fixed

**PASS**  
`PttBreakEven.cs:321-325` confirmed in source. Exact code present:

```csharp
bool stateOk = o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Accepted
            || o.OrderState == OrderState.Submitted      // REPAIR-08
            || o.OrderState == OrderState.Initialized    // REPAIR-08
            || o.OrderState == OrderState.TriggerPending; // REPAIR-08
```

This matches the plan's Appendix B verbatim. Gap 2 is CLOSED in the actual source file.

### Check B — Direction A Technically Correct

**PASS**  
Pre-cancel mechanism confirmed correct via source inspection:

1. `CancelQxBrackets(acc, instr)` at CopyEngine.cs:586 cancels all orders in
   `Working|Initialized|Accepted|Submitted|TriggerPending` — covers all ATM bracket states
   that could exist at QX-ALL fire time.
2. After cancel call, brackets enter `CancelSubmitted` (NT8_FULL_REFERENCE.md line 971).
3. `BuildQxSnapshot` at CopyEngine.cs:616 uses stateOk that does NOT include `CancelSubmitted`
   (CopyEngine.cs:625-629). Therefore snapshot=0 for a follower that was pre-cancelled.
4. `CancelQxBrackets(leader, instr, snapshot)` (3-param overload) with empty snapshot is a
   no-op (stale.Count stays 0, returns early at line 674).
5. PTT-QX orders submit to a follower account that has no conflicting Working orders.
   NT8 sim sees no conflict. The race is eliminated.

### Check C — CYC Analysis Accurate

**PASS**  
- `PttGlobalQuickExit.Execute`: CYC=8 independently verified by branch-count on source.
  The plan's annotation (8 named branches) is accurate.
- `PttGlobalQuickExit.ExecuteOne`: Current CYC=1 confirmed (0 conditionals, 1 base).
  Post-fix CYC=2 confirmed (1 conditional added by `if (!skipIfFollower)`).
- `ResolveQuickTicks`: CYC=2 confirmed (2 guards in source).
- `SnapshotTargetOrders`: CYC=4 confirmed (4 decision points in source).
- `CancelQxBrackets(2-param)`: CYC=6 confirmed (per CopyEngine.cs:584 comment, unchanged).

### Check D — Test Names and Assert Conditions

**PASS**  
Two mandatory [Fact] tests with concrete assert conditions provided:

| Test name | Assert | Covers |
|-----------|--------|--------|
| `ExecuteOne_Follower_PreCancelsBeforeQxSubmit` | `cancelInvocationCount >= 1` | Guard fires on follower path |
| `ExecuteOne_Leader_DoesNotPreCancelFollowerBrackets` | `executeOneCancelCount == 0` | Guard does NOT fire on leader path |
| `BuildQxSnapshot_ExcludesCancelSubmitted_Orders` (optional) | `result.Count == 0` | Underlying invariant of Direction A |

Both mandatory tests directly test the `if (!skipIfFollower)` guard by covering the true and false branches. Assert conditions are specific and verifiable.

---

## 5. Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|------------|--------------|
| Fix QX conflict on follower accounts | YES | Section 2-3 |
| Do not modify `PttQuickExit.Execute` | YES | Section 6 (Not-Changed table) |
| Do not modify `CopyEngine.CancelQxBrackets` | YES | Section 6 |
| Gap 2 already closed — document only | YES | Sections 1.1, Appendix B |
| Direction C live-trading safety assessment | YES | Section 2.3 |
| CYC <= 8 for all touched methods | YES | Section 4 |
| 7-scan checklist present | YES | Section 7 |
| Minimum 2 [Fact] tests with assert conditions | YES | Section 5.2-5.3 |
| NO-PIPELINE-REPAIRS.md carry-forward update | YES | Section 6 + Appendix C |
| NT8 API usage correct (OrderState, CancelSubmitted) | YES | Sections 1.2, 3.3, Appendix A |

---

## 6. Violations Found

**None.**

No P0 violations. No P1 violations. No NT8 violations. No CYC violations.
The plan is technically sound, minimal, and complete.

---

## 7. Final Verdict

**REVIEW_PASS**

The DW-B79-03 architecture plan is approved for Phase 3 (ticket generation).

All checks pass:
- Gap 2 confirmed closed in source (REPAIR-08, commit a3f68559) ✅
- Direction A technically correct — pre-cancel eliminates the follower ATM conflict race ✅
- CYC unchanged for `Execute()` (stays at 8), `ExecuteOne` rises from 1 to 2 (well within budget) ✅
- All DNA rules (JS-001/002/021/033/066) compliant — no violations in proposed code ✅
- NT8 OrderState usage correct — `CancelSubmitted` exclusion from `BuildQxSnapshot` is the key invariant and is verified against NT8_FULL_REFERENCE.md ✅
- Test plan covers both branches of the new guard with concrete assert conditions ✅
- 7-scan checklist complete with exact commands and expected zero results ✅
- Minimal footprint: 2 lines changed in 1 source file ✅
