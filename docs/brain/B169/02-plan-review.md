# B169 Plan Review
## Block: DW-B168-02 — _dragsInFlight HashSet<string> Race Fix
## Phase: 2 (Plan Review)
## Reviewer: ptt-plan-reviewer
## Verdict: REVIEW_PASS

---

## LANE-SPLIT GATE Compliance

| Item | Result |
|------|--------|
| LANE-SPLIT GATE RESULT stated in plan | PASS — "SINGLE-PIPELINE" declared at top of plan |
| Q1 answered (same method / within 50 lines?) | PASS — "NO" with span explanation |
| Q2 answered (Fix B depends on Fix A?) | PASS — "N/A" (single atomic fix) |
| Q3 answered (each fix has standalone value?) | PASS — "N/A" with correct reasoning |
| Q4 answered (independent SIM path?) | PASS — "N/A" with correct reasoning |
| Q1–Q4 consistent with SINGLE-PIPELINE conclusion | PASS |

---

## Correctness Checks

### Check 1 — Zero-regression: single-stop drag
**PASS**

Plan sections 2/5b/5c show:
- `TryTrackDragInFlight(order)` fires at ChangePending → adds suffix "2" to `_dragsInFlight`.
- `ResubmitCollateralLegs` checks legs "1" and "3": neither is in set → `FindLeaderCollateralOrder` called → `ResubmitOneCollateralLeg` proceeds.
- After `ResubmitCollateralLegs`, `_dragsInFlight.Remove("2")` executes (section 5c).
- Path is identical to original behaviour. Zero regression demonstrated.

### Check 2 — Zero-regression: two-stop simultaneous drag
**PASS**

Plan section 5g loop body + Section 9 constraint checklist confirm:
- Both suffixes added at ChangePending: set = {"1","2"}.
- Stop1 Accepted: loop checks "2" (in set → skip), "3" (not in set → resubmit leg 3). Clears "1".
- Stop2 Accepted: loop checks "1" (not in set → resubmit), "3" (not in set → resubmit). Clears "2".
- No CancelPending storm. Each leg handled exactly once per its own Accepted handler.

### Check 3 — Three-stop simultaneous drag correctness (no double-resubmit)
**PASS**

`CaptureOtherLegTargetPrices(legSuffix, acc)` (source line 3731) uses `ParseTargetLegIndex` with `excludeSuffix` — the current leg's suffix is excluded from `otherLegPrices`. Therefore when Stop3's handler runs:
- `otherLegPrices` contains legs "1" and "2" only (Stop3's own suffix "3" is excluded).
- At Stop3 Accepted: suffixes "1" and "2" have already been removed by Stop1 and Stop2 handlers.
- `_dragsInFlight.Contains("1")` → false → resubmit. `_dragsInFlight.Contains("2")` → false → resubmit.
- Result: exactly ONE `ResubmitOneCollateralLeg` call per collateral leg. No double-resubmit.

### Check 4 — ChangeRejected/Cancelled clear (no permanent skip)
**PASS**

- ChangeRejected: handled inside new `TryTrackDragInFlight` method (plan section 5e). When `order.OrderState == OrderState.ChangeRejected` → `_dragsInFlight.Remove(suffix)`.
- Cancelled: handled inside `TryCancelFollowerEntries` (plan section 5d) via new `TryRemoveDragSuffix(order)` call inside the `IsAtmBracketName` block, before `return true`.
- Both paths remove the suffix before the event is consumed. No permanent orphan.

### Check 5 — Suffix extraction expression confirmed from source
**PASS**

Plan section 3 confirms `ParseLegSuffix(order.Name)` at lines 3477-3481, verified against source (confirmed lines 3477-3481 exist and match the quoted body). At site B (HandleAtmStopSync Accepted), the existing `legSuffix` parameter is reused directly — no re-extraction. Expression is source-confirmed, not assumed.

---

## Safety Checks

### Check 6 — No lock() introduced
**PASS**

- Plan section 9 explicitly asserts no new `lock()`.
- All new methods (`TryTrackDragInFlight`, `TryRemoveDragSuffix`) carry `// JS-021: no lock` comments in plan code samples.
- `_dragsInFlight` field comment states: "JS-021: no lock -- all NT8 order callbacks for the leader account execute on the same NT8 background thread; concurrent access is impossible."
- Section 11 (NT8 API/Threading Notes) provides the threading rationale.
- No JS-021 violation.

### Check 7 — ASCII-only (no Unicode in string literals)
**PASS**

All string literals in plan code samples are ASCII. Field comment includes "ASCII-only. No Unicode." New method comments repeat "ASCII-only." No Unicode characters or curly quotes detected.

### Check 8 — FindLeaderCollateralOrder AFTER Contains check
**PASS**

Plan section 5g shows the modified loop body in explicit order:
1. `if (_dragsInFlight.Contains(s)) continue;`  ← guard first
2. `Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);`  ← lookup second

Contains guard (O(1), alloc-free) precedes the O(n) ToList scan. Constraint satisfied.

---

## Removal Verification

### Check 9 — IsLeaderLegBeingChanged REMOVED (exact line range stated)
**PASS**

Plan sections 2 (table, label F) and 6 state: lines 3815-3818 REMOVED completely, not commented out. Source read confirms these exact lines contain the method. Plan states "All callers: only `ResubmitCollateralLegs` called `IsLeaderLegBeingChanged` (line 3983)." Caller also replaced in section 5g.

### Check 10 — IsLeaderLegBeingChangedTestable REMOVED (exact line range stated)
**PASS**

Plan sections 2 (table, label G) and 6 state: lines 3820-3824 (including comment block) REMOVED completely, not commented out. Source read confirms these exact lines contain the seam. Plan states only `DwB168Tests` called the seam — that class is also removed.

---

## Quality Checks

### Check 11 — CYC numbers stated for all touched methods, all < 8
**PASS (with documentation note)**

Plan section 8 provides a complete CYC table. All post-change values are ≤ 8:

| Method | CYC Before (plan) | CYC After (plan) | ≤ 8? |
|--------|-------------------|------------------|------|
| HandleAtmStopSync | 2 | 3 | YES |
| TryHandleBracketDrag | 3 | 3 | YES |
| TryCancelFollowerEntries | 4 | 4 | YES |
| ResubmitCollateralLegs | 3 | 3 | YES |
| TryTrackDragInFlight (new) | — | 3 | YES |
| TryRemoveDragSuffix (new) | — | 2 | YES |

**Documentation note (non-blocking):** The plan's "CYC Before = 2" for `HandleAtmStopSync` is inconsistent with the method body, which has two existing branches (`fo.StopPrice < tickSize` and `capturedTargetPrice.HasValue`), giving CYC = 3 before the change. The existing source comment at line 3485 says "CYC<=6". The CYC detail at the bottom of section 8 correctly lists two existing branches plus the one new branch (= CYC 3 after), which is self-consistent. The discrepancy in the "Before" column is a documentation error only. The post-change CYC of 3 is well within budget and the error does not affect compliance.

No method exceeds CYC 8. No JS-CYC violation.

### Check 12 — ptt-sync.ps1 listed as mandatory post-change step
**PASS**

Plan section 12, step 3: `powershell -File .\scripts\ptt-sync.ps1` listed explicitly as the third post-change step. Also referenced in section 9 constraint checklist.

---

## Plan Completeness Checks

### Check 13 — Full C# code samples for all 8 edit sites
**PASS**

| Site | Description | Present? |
|------|-------------|----------|
| 5a | `_dragsInFlight` field declaration | YES |
| 5b | `TryHandleBracketDrag` call to `TryTrackDragInFlight` | YES |
| 5c | `HandleAtmStopSync` Accepted: `_dragsInFlight.Remove` | YES |
| 5d | `TryCancelFollowerEntries` Cancelled: `TryRemoveDragSuffix` | YES |
| 5e | New method `TryTrackDragInFlight` (full body) | YES |
| 5f | New method `TryRemoveDragSuffix` (full body) | YES |
| 5g | Modified `ResubmitCollateralLegs` loop body | YES |
| 5h | `DragsInFlightForTest()` internal seam | YES |

All 8 edit sites have complete C# code samples.

### Check 14 — 4 [Fact] test signatures with correct names
**PASS**

Plan section 7 provides the `DwB168_02Tests` class with all 4 `[Fact]` signatures:

| # | Test Name | [Fact]? |
|---|-----------|---------|
| 1 | `DragsInFlight_SingleStop_CollateralResubmitted` | YES |
| 2 | `DragsInFlight_ThreeStop_CollateralSuppressedForInFlightLeg` | YES |
| 3 | `DragsInFlight_ThreeStop_LastHandlerResubmitsAllLegs` | YES |
| 4 | `DragsInFlight_ClearOnCancelled_PreventsPermamentSkip` | YES |

Names match the spec test contract (section 6 of DW-B168-02-design.md).

### Check 15 — Files touched and line-count delta stated
**PASS**

Plan section 4 provides a complete table covering both files:
- `src/PropTraderTools/CopyEngine.cs`: +32 net lines (itemised per change)
- `src/PropTraderTools/CopyEngineTests.cs`: -28 net lines (DwB168Tests removed, DwB168_02Tests added)
- Total estimated diff: ~60-80 net lines. Within DIFF GUARD.

---

## Spec Coverage Matrix

| DW-B168-02 Design Requirement | Addressed? | Plan Section |
|-------------------------------|-----------|--------------|
| Root cause: IsLeaderLegBeingChanged race | YES | Section 1 |
| New `_dragsInFlight HashSet<string>` field | YES | Section 2H, 5a |
| Add at ChangePending (before broker round-trip) | YES | Section 2A, 5b, 5e |
| Clear at Accepted (after ResubmitCollateralLegs) | YES | Section 2B, 5c |
| Clear at ChangeRejected (prevent permanent orphan) | YES | Section 2C, 5e |
| Clear at Cancelled (prevent permanent orphan) | YES | Section 2D, 5d, 5f |
| Replace `IsLeaderLegBeingChanged` guard with `Contains` | YES | Section 2E, 5g |
| `FindLeaderCollateralOrder` moved after Contains guard | YES | Section 5g |
| `IsLeaderLegBeingChanged` REMOVED | YES | Section 6, 10 |
| `IsLeaderLegBeingChangedTestable` REMOVED | YES | Section 6, 10 |
| `DwB168Tests` REMOVED | YES | Section 7, 10 |
| `DwB168_02Tests` added (4 tests) | YES | Section 7 |
| Internal seam `DragsInFlightForTest()` | YES | Section 5h |
| ptt-sync.ps1 post-change step | YES | Section 12 |
| Zero regression: single-stop drag | YES | Section 9 |
| Zero regression: two-stop drag | YES | Section 9 |
| Three-stop correctness (no storm) | YES | Section 9 |
| CYC ≤ 8 for all touched methods | YES | Section 8 |
| No lock() (JS-021) | YES | Section 9, 11 |
| ASCII-only | YES | Section 9, all code samples |

All spec requirements addressed.

---

## DNA Rule Violations

**None found.**

No JS-001, JS-002, JS-003, JS-008, JS-009, JS-010, JS-021, JS-023, NT8 async/await, DateTime.Now, lock(), sealed, FontFamily, hardcoded hex, CreateOrder prefix, Account.All-in-ctor, or CYC>8 violations found in the plan.

---

## Summary

| Category | Result |
|----------|--------|
| LANE-SPLIT GATE compliance | PASS |
| Correctness (all 5 checks) | PASS |
| Safety (all 3 checks) | PASS |
| Removal verification (2 checks) | PASS |
| Quality (CYC + ptt-sync) | PASS |
| Plan completeness (3 checks) | PASS |
| Spec coverage | PASS (all requirements addressed) |
| DNA rule violations | NONE |

---

## VERDICT: REVIEW_PASS
