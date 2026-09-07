# Ticket Review: WAVE1-LANE-B
# Phase 3.5 Output — ptt-ticket-reviewer (SECOND PASS)
# Input: docs/brain/WAVE1-LANE-B/04-tickets.md (REWORKED — all 5 VERIFICATION-ONLY)
#        docs/brain/WAVE1-LANE-B/02-architecture-plan.md
#        docs/brain/WAVE1-LANE-B/02-plan-review.md
# Date: 2026-08

---

## Rework Validation Note

The first-pass TICKET_REVIEW_FAIL cited T-1 and T-3 for describing engineering work on methods
already at CCN <= 8 (regression risk with zero CCN benefit). The rework converts ALL 5 tickets to
VERIFICATION-ONLY. The legitimacy of this conversion is established by:

1. **Live lizard baseline** (02-plan-review.md Section 1): Methods CCN > 8: NONE. All 5 files
   confirmed at CCN <= 8 by independent lizard run — this is the authoritative measurement.
2. **Plan reviewer informational note** (02-plan-review.md Section 4): The proposed engineering
   was classified as "additive improvements on already-compliant code" and "worth executing as a
   margin-of-safety improvement" — explicitly informational, not a mandate.
3. **CCN Decision Rule** (documented in 04-tickets.md header): "IF all methods in a ticket are
   CCN <= 8 (lizard-confirmed) → Engineering work is UNJUSTIFIED. Modifying live-trading code
   with zero CCN benefit = regression risk, BANNED." This rule is architecturally sound and
   consistent with JS-096 (make illegal states unrepresentable) and the no-scope-creep mandate.

Conversion is valid. Review proceeds on the reworked tickets.

---

## T-1 — PttBreakEven.cs (Verification Only)

### Traceability
- B-01 → SubmitBePair (CCN 5, COMPLIANT) → T-1: COVERED ✅
- B-06 → SubmitBeStopLocal (CCN 7, COMPLIANT) → T-1: COVERED ✅
- All other PttBreakEven.cs methods listed with CCN values, all <= 8 ✅
- No phantom work (ticket describes only reading and running scans — zero code changes) ✅
- Plan Section 4 (T-B1) lists PttBreakEven.cs in scope: TRACEABILITY CONFIRMED ✅

**Traceability: PASS**

### JS Pre-Check (JS-001/002/003/008/009/021/023/025/033)
- No `lock()` described or instructed in verification steps ✅
- No `async void` described ✅
- No `return null` pattern described ✅
- No `throw new Exception` described ✅
- No Dictionary<K,V> shared state described ✅
- No UI thread violation described ✅
- No mutable struct fields described ✅
- No SolidColorBrush without Freeze described ✅

**JS Pre-Check: PASS**

### CYC Pre-Check
No new methods are added. Verification-only. No CYC risk introduced.

**CYC Pre-Check: PASS**

### NT8 Check
- No async/await in lifecycle methods ✅
- No Account.All outside Loaded handler ✅
- No `sealed` on TradeCopierWindow ✅
- No FontFamily set on WPF element ✅
- No hardcoded hex color ✅
- No `CreateOrder` with non-"PTT-" prefix ✅
- No `DateTime.Now` ✅
- No code changes whatsoever (verification-only) — NT8 constraints cannot be violated ✅

**NT8 Check: PASS**

### Test Coverage
- `SubmitBePair_WhenOrderIsNull_DoesNotThrow` [Fact] ✅
- `SubmitBeStopLocal_WhenPositionIsFlat_SkipsSubmit` [Fact] ✅
- Both map to B-01 and B-06 respectively ✅
- Ticket specifies: if these tests do not exist, add them as the verification deliverable ✅

**Test Coverage: PASS**

### Scan Checklist
- SCAN-01: lock() grep with expected result documented ✅
- SCAN-02: async void grep with expected result documented ✅
- SCAN-03: return null grep with expected result documented ✅
- SCAN-04: lizard CCN <= 8 (per-method AT-LIMIT expectations documented) ✅
- SCAN-05: dotnet build 0 errors ✅
- SCAN-06: dotnet test >= 117 pass 0 fail ✅
- SCAN-07: ptt-sync-and-verify.ps1 0 MISMATCH ✅

All 7 scans present with explicit commands and expected results.

**Scan Checklist: PASS**

### File Routing
- `src/PropTraderTools/Features/PttBreakEven.cs` → Wave workspace ✅
- Completion artifact: `docs/brain/WAVE1-LANE-B/ticket-1-completion.md` ✅

**File Routing: PASS**

### Lane Isolation
"This ticket makes ZERO edits to CopyEngine.cs." — explicitly stated ✅

### Acceptance Criterion
Lizard CCN <= 8 confirmed AND PttBreakEven.cs NOT in `git diff --name-only` ✅

**VERDICT: TICKET_REVIEW_PASS**

---

## T-2 — PttBreakEvenSwap.cs (Verification Only)

### Traceability
- B-02 → SubmitSwapPair (CCN 4, COMPLIANT) → T-2: COVERED ✅
- B-08 → SubmitBareStopSwap (CCN 4, COMPLIANT) → T-2: COVERED ✅
- Execute (CCN 8, AT-LIMIT) listed and confirmed COMPLIANT ✅
- No phantom work ✅
- Plan Section 4 (T-B2) maps PttBreakEvenSwap.cs: TRACEABILITY CONFIRMED ✅

**Traceability: PASS**

### JS Pre-Check
No engineering work described. No JS violations possible from verification steps.

**JS Pre-Check: PASS**

### CYC Pre-Check
No new methods. Verification-only. No CYC risk.

**CYC Pre-Check: PASS**

### NT8 Check
No code changes. No NT8 violations possible.

**NT8 Check: PASS**

### Test Coverage
- `SubmitBareStopSwap_WhenPriceNotSubmittable_LogsAndSkips` [Fact] ✅
- `SubmitSwapPair_WhenPriceNotSubmittable_SkipsStop_SubmitsTarget` [Fact] ✅

**Test Coverage: PASS**

### Scan Checklist
- SCAN-01 through SCAN-07: all present with explicit commands and expected results ✅

**Scan Checklist: PASS**

### File Routing
- `src/PropTraderTools/Features/PttBreakEvenSwap.cs` → Wave workspace ✅
- Completion artifact: `docs/brain/WAVE1-LANE-B/ticket-2-completion.md` ✅

**File Routing: PASS**

### Lane Isolation
"This ticket makes ZERO edits to CopyEngine.cs." — explicitly stated ✅

**VERDICT: TICKET_REVIEW_PASS**

---

## T-3 — PttGlobalQuickExit.cs (Verification Only)

### Traceability
- B-03 → ExecuteFollowers (CCN 8, AT-LIMIT, COMPLIANT) → T-3: COVERED ✅
- B-04 → Execute no-arg (CCN 7) and Execute(List) (CCN 8, AT-LIMIT) → T-3: COVERED ✅
- B-07 → ExecuteOne (CCN 2, plan-review: CCN 6) → T-3: COVERED ✅
  Note: CCN discrepancy (ticket says 2, plan-review says 6) — both are <= 8 and COMPLIANT;
  this is an informational variance, not a violation. Lizard SCAN-04 will resolve at execution.
- 14 methods listed with CCN values, all <= 8 ✅
- No phantom work (verification-only, no helpers added) ✅
- Plan Section 4 (T-B3) maps PttGlobalQuickExit.cs: TRACEABILITY CONFIRMED ✅
- First-pass violation CORRECTED: engineering work for IsLeaderAccount, IsFlatPosition,
  FindFollowerPosQty, ClassifyTarget helpers is absent from T-3 as required ✅

**Traceability: PASS**

### JS Pre-Check
No engineering work described. No JS violations possible.

**JS Pre-Check: PASS**

### CYC Pre-Check
No new methods. Verification-only. No CYC risk.

**CYC Pre-Check: PASS**

### NT8 Check
No code changes. No NT8 violations possible.

**NT8 Check: PASS**

### Test Coverage
- `ExecuteOne_WhenSkipIfFollowerFalse_ArmsQxCancelGuard` [Fact] — covers B-07 ✅
- `ExecuteFollowers_WhenFollowerIsNull_SkipsFollower` [Fact] — covers B-03 ✅

**Test Coverage: PASS**

### Scan Checklist
- SCAN-01 through SCAN-07: all present with explicit commands, expected results, and
  AT-LIMIT method names documented for SCAN-04 ✅

**Scan Checklist: PASS**

### File Routing
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` → Wave workspace ✅
- Completion artifact: `docs/brain/WAVE1-LANE-B/ticket-3-completion.md` ✅

**File Routing: PASS**

### Lane Isolation
"This ticket makes ZERO edits to CopyEngine.cs." — explicitly stated ✅

**VERDICT: TICKET_REVIEW_PASS**

---

## T-4 — PttQuickExit.cs (Verification Only)

### Traceability
- B-05 → Execute main (CCN 5, COMPLIANT) → T-4: COVERED ✅
- B-09 → SubmitStopOrder (CCN 5, COMPLIANT) → T-4: COVERED ✅
- B-10 → SubmitTargetOrder (CCN 4, COMPLIANT) → T-4: COVERED ✅
- InstrumentDefaults.GetQuickTicks (CCN 4) included in scope ✅
- SnapshotStopPrice (CCN 8, AT-LIMIT) documented ✅
- No phantom work ✅
- Plan Section 4 (T-B4) maps PttQuickExit.cs: TRACEABILITY CONFIRMED ✅

**Traceability: PASS**

### JS Pre-Check
No engineering work described. No JS violations possible.

**JS Pre-Check: PASS**

### CYC Pre-Check
No new methods. Verification-only. No CYC risk.

**CYC Pre-Check: PASS**

### NT8 Check
No code changes. No NT8 violations possible.

**NT8 Check: PASS**

### Test Coverage
Six [Fact] methods specified:
- `IsFlatOrMissing_NullLeader_ReturnsTrue` ✅
- `IsFlatOrMissing_ZeroQty_ReturnsTrue` ✅
- `GetQuickTicks_MesInstrument_Returns4And8` ✅
- `GetQuickTicks_MgcInstrument_Returns2And4` ✅
- `GetQuickTicks_NullOrEmpty_Returns4And8` ✅
- `GetQuickTicks_UnknownInstrument_Returns4And8` ✅

**Test Coverage: PASS**

### Scan Checklist
- SCAN-01 through SCAN-07: all present with explicit commands and expected results ✅
- SCAN-04 documents SnapshotStopPrice expected at exactly CCN=8 (AT-LIMIT) ✅

**Scan Checklist: PASS**

### File Routing
- `src/PropTraderTools/Features/PttQuickExit.cs` → Wave workspace ✅
- Completion artifact: `docs/brain/WAVE1-LANE-B/ticket-4-completion.md` ✅

**File Routing: PASS**

### Lane Isolation
"This ticket makes ZERO edits to CopyEngine.cs." — explicitly stated ✅

**VERDICT: TICKET_REVIEW_PASS**

---

## T-5 — PttGlobalBreakEven.cs (Verification Only)

### Traceability
- No B-XX methods directly target this file — documented explicitly ✅
- Scope is "confirm clean CCN state for the complete WAVE1-LANE-B surface" — valid
  architectural rationale; the aggregate coverage table shows all B-01..B-10 are
  accounted for in T-1 through T-4 ✅
- Plan Section 4 (T-B5) maps PttGlobalBreakEven.cs as verification-only: CONFIRMED ✅
- No phantom work ✅

**Traceability: PASS**

### JS Pre-Check
No engineering work described. The ticket explicitly pre-empts the SCAN-01 false-positive
for the `// JS-021: no lock().` comment on line 4: documented as comment-only, not a violation ✅

**JS Pre-Check: PASS**

### CYC Pre-Check
No new methods. Maximum CCN in file is 5. No CYC risk.

**CYC Pre-Check: PASS**

### NT8 Check
No code changes. No NT8 violations possible.

**NT8 Check: PASS**

### Test Coverage
Three [Fact] methods specified:
- `Execute_IntOverload_DelegatesToIEnumerableOverload` ✅
- `IncrementBuffer_IncreasesBufferByOneTick` ✅
- `DecrementBuffer_DecreasesBufferByOneTick` ✅

**Test Coverage: PASS**

### Scan Checklist
- SCAN-01: lock() grep with explicit comment-hit pre-documentation ✅
- SCAN-02 through SCAN-07: all present with explicit commands and expected results ✅
- SCAN-04 documents expected upper bound as CCN <= 5 (tighter than CCN <= 8 standard) ✅

**Scan Checklist: PASS**

### File Routing
- `src/PropTraderTools/Features/PttGlobalBreakEven.cs` → Wave workspace ✅
- Completion artifact: `docs/brain/WAVE1-LANE-B/ticket-5-completion.md` ✅

**File Routing: PASS**

### Lane Isolation
"This ticket makes ZERO edits to CopyEngine.cs." — explicitly stated ✅

**VERDICT: TICKET_REVIEW_PASS**

---

## Aggregate Spec Coverage

| Spec Req | Method | Ticket | Status |
|----------|--------|--------|--------|
| B-01 | PttBreakEven.SubmitBePair | T-1 | COVERED |
| B-02 | PttBreakEvenSwap.SubmitSwapPair | T-2 | COVERED |
| B-03 | PttGlobalQuickExit.ExecuteFollowers | T-3 | COVERED |
| B-04 | PttGlobalQuickExit.Execute() and Execute(List) | T-3 | COVERED |
| B-05 | PttQuickExit.Execute (main) | T-4 | COVERED |
| B-06 | PttBreakEven.SubmitBeStopLocal | T-1 | COVERED |
| B-07 | PttGlobalQuickExit.ExecuteOne | T-3 | COVERED |
| B-08 | PttBreakEvenSwap.SubmitBareStopSwap | T-2 | COVERED |
| B-09 | PttQuickExit.SubmitStopOrder | T-4 | COVERED |
| B-10 | PttQuickExit.SubmitTargetOrder | T-4 | COVERED |

**All 10 B-XX requirements covered. No gaps. No duplicates.**

---

## Lane Isolation Summary

WAVE1-LANE-B ISOLATION CONFIRMED across all 5 tickets:
- Zero edits to CopyEngine.cs ✅
- Zero edits to ANY .cs file (all tickets are verification-only) ✅
- `git diff --name-only` must show NO files after all 5 tickets are executed ✅

---

## Second-Pass Rework Confirmation

| First-Pass Violation | Rework Applied | Resolved |
|----------------------|----------------|----------|
| T-1: Engineering work (GetBeOcoSeq, IsFollowerAcct, IsFinalOrder helpers) on CCN-compliant code | All helpers removed; T-1 is now verification-only | ✅ |
| T-3: Engineering work (IsLeaderAccount, IsFlatPosition, FindFollowerPosQty, ClassifyTarget helpers) on CCN-compliant code | All helpers removed; T-3 is now verification-only | ✅ |

Both first-pass violations are fully resolved. No new violations introduced.

---

## Overall: TICKET_REVIEW_PASS
