# Ticket Review: DW-B159 — Ghost Re-entry Guard + BE-All Silent Rollback Repair
<!-- status: TICKET_REVIEW_PASS -->
<!-- wave: B159 -->
<!-- epic: DW-B159 -->
<!-- phase: 3.5 -->
<!-- pass: 2 (second pass — all 4 prior violations resolved) -->
<!-- reviewer: ptt-ticket-reviewer -->
<!-- input: docs/brain/B159/04-tickets.md, 02-architecture-plan.md, 02-plan-review.md, CopyEngine.cs -->

---

## Prior Violation Resolution Status

| ID | Ticket | Required test name | Status |
|----|--------|--------------------|--------|
| TC-T1-01 | T1 | `IsFollowerPositionBlocked_AccountWithNoPosition_ReturnsFalse` | ✅ PRESENT — ticket line 179 |
| TC-T1-01 | T1 | `IsFollowerPositionBlocked_AccountWithOpenPosition_ReturnsTrue` | ✅ PRESENT — ticket line 185 |
| TC-T2-01 | T2 | `TryArmBePriceRetry_ValidInputsFirstAttempt_AddsEntryToDict` | ✅ PRESENT — ticket line 584 |
| TC-T2-02 | T2 | `TryFireBePriceRetry_CancelledPttBeStop_KeyNotInDict_NoSubmitBeStop` | ✅ PRESENT — ticket line 591 |
| TC-T2-03 | T2 | `StillAtNonBePrice_WorkingStopAtOriginalPrice_ReturnsTrue` | ✅ PRESENT — ticket line 597 |

All 4 prior violations fully resolved.

---

## DW-B159-T1 — FM-A: Ghost Re-entry Guard

### Traceability
**PASS**

- Failure mode explicitly stated: FM-A (Ghost Re-entry). Ticket line 17.
- Spec requirement cited: "FM-A: ghost re-entry suppression" — Spec Requirements table, tickets line 702. ✓
- All new/modified symbols match the plan exactly:
  - `IsFollowerPositionBlocked(Account acc, Instrument instr)` — plan line 80, ticket line 38. Signature and CYC=3 match. ✓
  - `ShouldSkipFollower` hook as first statement before `ShouldSkipFollowerDispatch` — plan §"Hook in ShouldSkipFollower", ticket lines 55, 94–117. ✓
- No phantom work. All items trace to plan FM-A section.
- No missing plan items. Plan Component Summary FM-A rows (`IsFollowerPositionBlocked`, modified `ShouldSkipFollower`) fully covered.

### Insertion Point Accuracy
**PASS**

- Anchor lines 3051–3056 for `IsFollowerPositionBlocked` insertion: source-verified — lines 3051–3055 are the comment block ending at the `ShouldSkipFollower` declaration at line 3056. Ticket anchor matches source exactly. ✓
- Hook line 3064 (`ShouldSkipFollowerDispatch` as first statement): source-verified — `CopyEngine.cs:3064` = `if (ShouldSkipFollowerDispatch(acc))`. Ticket cites this correctly. ✓
- Post-insertion line shift (~8 lines) correctly described as approximate. ✓

### JS Pre-Check (Jane Street DNA + NT8 Constraints)
**PASS**

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | No `throw` in new methods | PASS — all branches use early `return` |
| JS-021 | No `lock()` | PASS — no lock() in `IsFollowerPositionBlocked` or hook |
| JS-021 | No `Dictionary<K,V>` for shared state | PASS — no shared-state dict in FM-A |
| JS-042 | ASCII-only log strings | PASS — `[PTT-COPY-GUARD] ghost-entry blocked: ` is ASCII-only |
| NT8 | No `async void` | PASS — no async in new code |
| NT8 | No `DateTime.Now` | PASS — no date usage |
| NT8 | No bare `acc.Orders` enumeration | PASS — FM-A uses `acc.Positions` (via `HasOpenPosition` line 5273); no `.Orders` call |
| NT8 | No `sealed` on window class | PASS — no window classes involved |

### CYC Pre-Check
**PASS**

| Method | Ticket CYC | Plan CYC | Budget | Result |
|--------|-----------|---------|--------|--------|
| `IsFollowerPositionBlocked` (new) | 3 | 3 | ≤8 | PASS |
| `ShouldSkipFollower` (modified) | 4 | 4 | ≤8 | PASS |

### Test Coverage
**PASS**

T1 [Fact] tests enumerated (tickets lines 154–189):

| # | Method name | Method under test | Coverage |
|---|------------|-------------------|----------|
| 1 | `ShouldSkipFollower_FollowerHasOpenPosition_ReturnsTrue` | `ShouldSkipFollower` | FM-A-T3 ✓ |
| 2 | `ShouldSkipFollower_FollowerNoOpenPosition_ReturnsFalse` | `ShouldSkipFollower` | FM-A-T4 ✓ |
| 3 | `IsFollowerPositionBlocked_NullAccount_ReturnsFalse` | `IsFollowerPositionBlocked` | null-guard acc ✓ |
| 4 | `IsFollowerPositionBlocked_NullInstrument_ReturnsFalse` | `IsFollowerPositionBlocked` | null-guard instr ✓ |
| 5 | `IsFollowerPositionBlocked_AccountWithNoPosition_ReturnsFalse` | `IsFollowerPositionBlocked` | FM-A-T1 ✓ (was TC-T1-01) |
| 6 | `IsFollowerPositionBlocked_AccountWithOpenPosition_ReturnsTrue` | `IsFollowerPositionBlocked` | FM-A-T2 ✓ (was TC-T1-01) |

Count: **6** ≥ 6 minimum required. ✓

Plan FM-A test matrix fully covered:
- FM-A-T1 (Qty=0 → false): test #5 ✓
- FM-A-T2 (Qty>0 → true): test #6 ✓
- FM-A-T3 (open position → blocked): test #1 ✓
- FM-A-T4 (flat → pass-through): test #2 ✓

### Scan Checklist
**PASS**

All 7 scans present (ticket lines 203–212):

| Scan | Pattern | Expected | Present |
|------|---------|----------|---------|
| SCAN-01 | `grep "lock("` | Zero matches in new/modified code | ✅ |
| SCAN-02 | `grep "async void"` | Zero matches | ✅ |
| SCAN-03 | `grep "DateTime.Now"` | Zero matches | ✅ |
| SCAN-04 | `grep "Thread.Sleep"` | Zero matches | ✅ |
| SCAN-05 | `grep -P "[\x80-\xFF]"` | Zero matches — ASCII-only | ✅ |
| SCAN-06 | CYC audit: `IsFollowerPositionBlocked`=3, `ShouldSkipFollower`=4 | All ≤8 | ✅ |
| SCAN-07 | `grep "acc\.Orders"` (new code only) | Zero bare enumerations | ✅ |

### File Routing
**PASS**

- Source: `src/PropTraderTools/CopyEngine.cs` — Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`). ✓
- Tests: `src/PropTraderTools/CopyEngineTests.cs` — Wave workspace. ✓
- No Director workspace paths. ✓

### SYNC + DIFF GUARD
**PASS**

- SYNC: `powershell -File .\scripts\ptt-sync.ps1` present (ticket lines 237–240). ✓
- DIFF GUARD: `powershell -File .\deploy-sync.ps1` with < 10,000 chars limit present (ticket lines 244–252). ✓

### Log Marker Consistency
**PASS**

- Plan marker: `[PTT-COPY-GUARD] ghost-entry blocked: {accName} {instrFullName}`
- Ticket implementation: `"[PTT-COPY-GUARD] ghost-entry blocked: " + acc.Name + " " + instr.FullName` (ticket line 112). ✓
- VERIFY criterion grep pattern matches exactly. ✓

### VERDICT: **TICKET_REVIEW_PASS**

---

## DW-B159-T2 — FM-B: BE-All Silent Rollback Repair

### Traceability
**PASS**

- Failure mode explicitly stated: FM-B (BE-All Silent Rollback). Ticket line 261.
- Spec requirements cited (Spec Requirements table, tickets lines 699–712): FM-B, FM-B V-06, attempt ceiling, safe `acc.Orders` enumeration — all present. ✓
- All new/modified symbols match the plan exactly:
  - `TryArmBePriceRetry(Account acc, Instrument instr, double bePrice)` — plan line 183, ticket line 327. ✓
  - `StillAtNonBePrice(Account acc, Instrument instr, double bePrice)` — plan line 216, ticket line 328. ✓
  - `TryFireBePriceRetry(Order o)` — plan line 254, ticket line 329. ✓
  - `_pendingBePriceRetry` — plan line 162, ticket line 309. ✓
  - `_bePriceRetryAttempts` — plan line 169, ticket line 316. ✓
- Hooks: `MoveStopToBreakEven` after line 6857 (ticket line 376); `OnOrderUpdate` between 1725–1726 (ticket line 377). Both match plan. ✓
- No phantom work. No missing plan FM-B items. Plan Component Summary FM-B rows fully covered.

### Insertion Point Accuracy
**PASS**

- Field anchor after line 322: `_beReplaceAttempts` ends at line 322; `_qxCancelInProgress` at line 327 — source-verified. Ticket anchor block (lines 281–288) matches source exactly. ✓
- `MoveStopToBreakEven` hook after line 6857: `StatusUpdate?.Invoke(...)` at 6857; `if (targets.Count == 0)` at 6860 — source-verified. Ticket anchor matches exactly. ✓
- `OnOrderUpdate` hook between lines 1725 and 1726: line 1725 = `TryRecordBeTargetFill(e.Order);`, line 1726 = `TryTriggerBeRecovery(e.Order);` — source-verified. Ticket anchor matches exactly. ✓
- Insertion order: `TryFireBePriceRetry` AFTER `TryRecordBeTargetFill` (1725), BEFORE `TryTriggerBeRecovery` (1726) — matches plan V-08 resolution. ✓

### JS Pre-Check (Jane Street DNA + NT8 Constraints)
**PASS**

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | No `throw` in new methods | PASS — all branches use early `return` |
| JS-021 | No `lock()` | PASS — all state via `ConcurrentDictionary` |
| JS-021 | `ConcurrentDictionary` not `Dictionary` | PASS — both fields typed `ConcurrentDictionary<string, T>` |
| JS-021 | `StringComparer.Ordinal` on both dicts | PASS — `_pendingBePriceRetry` (ticket line 310) and `_bePriceRetryAttempts` (ticket line 317) both declare `StringComparer.Ordinal` ✓ |
| JS-021 | `acc.Orders.ToList()` snapshot in `StillAtNonBePrice` | PASS — `foreach (var o in acc.Orders.ToList())` present (ticket line 478) ✓ |
| JS-042 | ASCII-only log strings | PASS — `[PTT-BE-RETRY]` and all variants are ASCII-only |
| NT8 | No `DateTime.Now` | PASS — no date usage in any new code |
| NT8 | No `async void` | PASS |
| NT8 | `"PTT-"` prefix on order name guard | PASS — `StartsWith("PTT-BE-Stop-", StringComparison.Ordinal)` used consistently |

### CYC Pre-Check
**PASS**

| Method | Ticket CYC | Plan CYC | Budget | Result |
|--------|-----------|---------|--------|--------|
| `TryArmBePriceRetry` (new) | 5 | 5 | ≤8 | PASS |
| `StillAtNonBePrice` (new) | 7 | 7 | ≤8 | PASS |
| `TryFireBePriceRetry` (new) | 7 | 7 | ≤8 | PASS |
| `MoveStopToBreakEven` (hook) | 4 (unchanged) | 4 | ≤8 | PASS |
| `OnOrderUpdate` (hook) | unchanged | unchanged | ≤8 | PASS |

### Test Coverage
**PASS**

T2 [Fact] tests enumerated (tickets lines 549–601):

| # | Method name | Method under test | Coverage |
|---|------------|-------------------|----------|
| 1 | `TryFireBePriceRetry_CancelledOrderWithRetryKey_HasOpenPosition_StillAtNonBePrice_CallsSubmitBeStop` | `TryFireBePriceRetry` | FM-B-T2 ✓ |
| 2 | `TryFireBePriceRetry_CancelledOrderWithRetryKey_AlreadyAtBePrice_NoSubmitBeStop` | `TryFireBePriceRetry` | false-path ✓ |
| 3 | `TryArmBePriceRetry_AttemptsAtCap_NoNewEntry` | `TryArmBePriceRetry` | ceiling guard ✓ |
| 4 | `TryFireBePriceRetry_WorkingOrder_RemovesFromDict` | `TryFireBePriceRetry` | FM-B-T4 / V-06 ✓ |
| 5 | `TryFireBePriceRetry_NonPttBeStopPrefix_ExitsEarly` | `TryFireBePriceRetry` | branch 3 ✓ |
| 6 | `TryArmBePriceRetry_ValidInputsFirstAttempt_AddsEntryToDict` | `TryArmBePriceRetry` | FM-B-T1 ✓ (was TC-T2-01) |
| 7 | `TryFireBePriceRetry_CancelledPttBeStop_KeyNotInDict_NoSubmitBeStop` | `TryFireBePriceRetry` | FM-B-T3 ✓ (was TC-T2-02) |
| 8 | `StillAtNonBePrice_WorkingStopAtOriginalPrice_ReturnsTrue` | `StillAtNonBePrice` | direct unit test ✓ (was TC-T2-03) |

Count: **8** ≥ 8 minimum required. ✓

Plan FM-B test matrix coverage:
- FM-B-T1 (`TryArmBePriceRetry` valid inputs → dict written): test #6 ✓
- FM-B-T2 (`TryFireBePriceRetry` Cancelled + slot + StillAtNonBe → SubmitBeStop): test #1 ✓
- FM-B-T3 (`TryFireBePriceRetry` Cancelled + no slot → no action): test #7 ✓
- FM-B-T4 (`TryFireBePriceRetry` Working + slot → TryRemove, no SubmitBeStop): test #4 ✓
- FM-B-T5 (`StillAtNonBePrice` direct unit test): test #8 ✓

### Scan Checklist
**PASS**

All 7 scans present (ticket lines 618–628):

| Scan | Pattern | Expected | Present |
|------|---------|----------|---------|
| SCAN-01 | `grep "lock("` | Zero matches — all state via ConcurrentDictionary | ✅ |
| SCAN-02 | `grep "async void"` | Zero matches | ✅ |
| SCAN-03 | `grep "DateTime.Now"` | Zero matches | ✅ |
| SCAN-04 | `grep "Thread.Sleep"` | Zero matches | ✅ |
| SCAN-05 | `grep -P "[\x80-\xFF]"` | Zero matches — ASCII-only | ✅ |
| SCAN-06 | CYC audit: `TryArmBePriceRetry`=5, `StillAtNonBePrice`=7, `TryFireBePriceRetry`=7 | All ≤8 | ✅ |
| SCAN-07 | `grep "acc\.Orders"` (new code only) | `StillAtNonBePrice` uses `.ToList()` snapshot — zero bare enumerations | ✅ |

### File Routing
**PASS**

- Source: `src/PropTraderTools/CopyEngine.cs` — Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`). ✓
- Tests: `src/PropTraderTools/CopyEngineTests.cs` — Wave workspace. ✓
- No Director workspace paths. ✓

### SYNC + DIFF GUARD
**PASS**

- SYNC: `powershell -File .\scripts\ptt-sync.ps1` present (ticket lines 675–678). ✓
- DIFF GUARD: `powershell -File .\deploy-sync.ps1` with < 10,000 chars limit present (ticket lines 680–688). ✓

### Log Marker Consistency
**PASS**

Plan markers (architecture-plan lines 425–428) vs ticket implementation:

| Plan marker | Ticket implementation | Match |
|-------------|----------------------|-------|
| `[PTT-COPY-GUARD] ghost-entry blocked: {accName} {instrFullName}` | T1 line 112: `"[PTT-COPY-GUARD] ghost-entry blocked: " + acc.Name + " " + instr.FullName` | ✓ |
| `[PTT-BE-RETRY] armed: {accName} bePrice={bePrice}` | T2 line 459: `"[PTT-BE-RETRY] armed: " + acc.Name + " bePrice=" + bePrice` | ✓ |
| `[PTT-BE-RETRY] fired: {accName} bePrice={bePrice}` | T2 line 534: `"[PTT-BE-RETRY] fired: " + o.Account.Name + " bePrice=" + bePrice` | ✓ |
| `[PTT-BE-RETRY] working-cleanup: {accName}` | T2 line 517: `"[PTT-BE-RETRY] working-cleanup: " + o.Account.Name` | ✓ |

All 4 markers match plan exactly. VERIFY grep patterns in T2 use correct escaped brackets. No marker drift. ✓

### VERDICT: **TICKET_REVIEW_PASS**

---

## Violations Summary

| ID | Ticket | Rule | Status |
|----|--------|------|--------|
| TC-T1-01 | T1 | TEST-COVERAGE | **RESOLVED** — `IsFollowerPositionBlocked_AccountWithNoPosition_ReturnsFalse` and `_AccountWithOpenPosition_ReturnsTrue` both present |
| TC-T2-01 | T2 | TEST-COVERAGE | **RESOLVED** — `TryArmBePriceRetry_ValidInputsFirstAttempt_AddsEntryToDict` present |
| TC-T2-02 | T2 | TEST-COVERAGE | **RESOLVED** — `TryFireBePriceRetry_CancelledPttBeStop_KeyNotInDict_NoSubmitBeStop` present |
| TC-T2-03 | T2 | TEST-COVERAGE | **RESOLVED** — `StillAtNonBePrice_WorkingStopAtOriginalPrice_ReturnsTrue` present |

No new violations found. Zero open violations.

---

## Overall: **TICKET_REVIEW_PASS**

All checks pass on both tickets. All 4 prior violations confirmed resolved.

**Summary of checks run:**

| Check | T1 | T2 |
|-------|----|----|
| 1. Traceability (FM-A/FM-B, spec citations, symbol names match plan) | PASS | PASS |
| 2. Insertion point accuracy (hook line numbers source-verified) | PASS | PASS |
| 3. 7-scan checklist presence (all 7 scans, SCAN-01 through SCAN-07) | PASS | PASS |
| 4. JS/CYC pre-checks (StringComparer.Ordinal, .ToList(), no lock, CYC values) | PASS | PASS |
| 5. Test coverage (T1≥6 with 6, T2≥8 with 8; plan test matrix fully covered) | PASS | PASS |
| 6. Log marker consistency ([PTT-COPY-GUARD], [PTT-BE-RETRY] variants match plan) | PASS | PASS |
| 7. Single file scope (CopyEngine.cs only) | PASS | PASS |
| 8. SYNC + DIFF GUARD present | PASS | PASS |

**Safe to spawn engineer (Phase 4a).** Engineer reads `04-ticket-review.md` first, then `04-tickets.md`.

---

*Review produced by ptt-ticket-reviewer. Status: TICKET_REVIEW_PASS. Phase 3.5 pass 2. Wave B159.*
