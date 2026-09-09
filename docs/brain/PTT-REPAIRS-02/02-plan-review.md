# PTT-REPAIRS-02 Plan Review
**Status**: REVIEW_PASS
**Phase**: 2 (Plan Review)
**Epic**: PTT-REPAIRS-02
**Reviewer**: ptt-plan-reviewer
**Review Date**: 2026-09-07
**Review Cycle**: 2 of 2 (Correction Cycle 1 verified)
**Plan Author**: ptt-architect
**Plan File**: `docs/brain/PTT-REPAIRS-02/02-architecture-plan.md`

---

## Final Verdict: REVIEW_PASS

**Violations found**: 0 blocking
**Violations resolved**: 1 (V1 JS-023 mis-citation — fixed in Correction Cycle 1)

All 9 checklist items PASS. Plan is approved for Phase 3 ticket generation.

---

## Correction Cycle 1 Fix Verification

**V1 (JS-023 mis-citation) — RESOLVED**

Previous violation: Section 11 row for JS-023 contained the description "No mutable struct"
(which is JS-008's description), and the JS-008 row was absent.

Corrected plan (Section 11) now reads:

```
| JS-008 | No mutable struct fields / unfrozen brushes | PASS -- no struct usage introduced |
| JS-023 | No UI update from off-thread without Dispatcher.InvokeAsync | N/A -- no UI changes in this fix |
```

- JS-023 is now correctly described as the off-thread UI update rule. N/A status is correct
  because the fix is confined to `CopyEngine.cs` backend with no UI thread involvement. VERIFIED. [checkmark]
- JS-008 is now correctly described as "No mutable struct fields / unfrozen brushes." VERIFIED. [checkmark]
- No new errors introduced by the correction. [checkmark]

**V1: CLOSED.**

---

## Per-Item Findings

---

### 1. LANE-SPLIT GATE

**Result**: PASS

- Gate result explicitly stated: `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` (Section 2). [checkmark]
- Q1 = YES (single change in `EvictDedup`, single method, well under 50 lines). [checkmark]
- SINGLE-PIPELINE path correctly terminates gate at Q1=YES with STOP directive. [checkmark]
- No Q3/Q4 required for single-pipeline path. [checkmark]
- Justification grounded: Option B premise is moot because DW-LB-FL-01 V7 Fix A already
  hoists `TryClearLeaderDirectionOnFlat` above the dedup gate (line 4191 cited). [checkmark]

**Finding**: PASS

---

### 2. OPTION SELECTION

**Result**: PASS

**Option A justification** (Section 3, points 1-4):

- Point 1: `_entryDispatchedOrders` (DW-B91-A) is the primary MGC guard via
  `IsEntryDispatched(orderId)`. Confirmed in source at line 5716 in `IsLiveEntryBlocked`. [checkmark]
- Point 2: MGC cancel+resubmit uses a NEW orderId. Original instrKey cleared on Cancelled
  (lines 5758-5759, confirmed). New orderId's dispatch gated by TryAdd on cleared instrKey.
  Path unaffected by Option A. [checkmark]
- Point 3: Followers dispatch on Submitted/Accepted (`IsDispatchTriggerState`), before the
  Filled event arrives. instrKey guard has no protective function post-fill. [checkmark]
- Point 4: `ClearLiveEntryForInstrument` correctly demoted to secondary guard. [checkmark]

**Option B rejection** (Section 3, lines 75-78):
- DW-LB-FL-01 V7 Fix A already implements the Option B intent at line 4191. [checkmark]
- Rejection reasoning grounded in specific source line. [checkmark]

**Finding**: PASS

---

### 3. MGC GUARD INTEGRITY

**Result**: PASS

Section 5 provides 6-row scenario matrix covering all MGC paths:

| Scenario | After Fix | Verdict |
|----------|-----------|---------|
| First dispatch: instrKey set | YES (unchanged) | OK |
| MGC cancel: Cancelled branch clears instrKey | YES (unchanged) | OK |
| MGC resubmit NEW orderId: instrKey clear -- TryAdd succeeds | YES (unchanged) | OK |
| MGC resubmit BEFORE cancel: instrKey still set -- blocked | YES (Cancelled not Filled path) | OK |
| Leader fill: instrKey cleared by new fix | YES (released) | FIXED |
| Position flat: ClearLiveEntryForInstrument | YES (now secondary guard) | OK |

Cancelled branch (lines 5751-5760) is explicitly noted as unchanged. Fix operates only inside
the `if (state == OrderState.Filled)` block. Two guards are independent.

`_entryDispatchedOrders` eviction in Filled branch is documented as deferred (DW-REPAIRS-02-01,
P2, OPEN) -- pre-existing behavior, out of scope. Correctly documented.

**Finding**: PASS

---

### 4. CYC BUDGET

**Result**: PASS

**EvictDedup CYC after fix (independent verification)**:

Branch contributions:
1. `if (state != Filled && state != Cancelled && state != Rejected)` = 1
2. `if (state == OrderState.Cancelled)` = 1
3. `if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))` = 1
4. `if (state == OrderState.Filled)` = 1
5. `if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))` = 1 (NEW)
Total: base(1) + 5 branches = CYC=6. Budget <=7. PASS.

**IsLiveEntryBlocked CYC** (Section 4): CYC=4 unchanged. Budget <=5. PASS.

Section 4 CYC table entries are accurate and consistent with Section 8 (SCAN-01).

Cosmetic note (non-blocking): Section 3 and Section 9 use slightly different wording for
the header comment (`filledInstrKey-TryRemove(5)` vs `filledInstrKey-remove(5)`). Both
represent the same branch; wording difference is immaterial.

**Finding**: PASS

---

### 5. TEST DESIGN

**Result**: PASS

Test `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry` (Section 7):

- **Exact defect scenario exercised**: 5-step sequence -- dispatch (gate pass) -- instrKey
  confirmed set -- EvictDedup(Filled) -- instrKey confirmed cleared -- re-dispatch (gate pass).
  This is the exact over-blocking defect. [checkmark]
- **All four seam methods confirmed** at `CopyEngine.cs` lines 4264-4282 (InternalsVisibleTo
  at line 46):
  - `IsLiveEntryBlocked_ForTest` (line 4264) [checkmark]
  - `EvictDedup_ForTest` (line 4270) [checkmark]
  - `ClearLiveEntryForInstrument_ForTest` (line 4273) [checkmark]
  - `LiveEntryInstrumentsContains_ForTest` (line 4276) [checkmark]
- **True behavioral assertions**: Steps 2 and 4 assert on actual `_liveEntryInstruments`
  state via `LiveEntryInstrumentsContains_ForTest`. Step 5 exercises the full gate path
  via `IsLiveEntryBlocked_ForTest`. No reflection or compile-only checks. [checkmark]
- **Pre-condition guard**: `ClearLiveEntryForInstrument_ForTest("MGC DEC26")` isolates test
  from residual state. [checkmark]
- **[Fact] only** (no [Theory]). [checkmark]
- **CYC=1** (no branches in test body). [checkmark]
- **Test count**: 300 (verified baseline) + 1 = 301. [checkmark]

**Finding**: PASS

---

### 6. JANE STREET RULES

**Result**: PASS

Section 11 compliance table (corrected plan):

| Rule | Plan Description | Status | Finding |
|------|-----------------|--------|---------|
| JS-001 | No throw in hot paths | PASS -- TryRemove does not throw | PASS [checkmark] |
| JS-002 | No return null | PASS -- EvictDedup is void | PASS [checkmark] |
| JS-021 | No lock() | PASS -- zero lock() introduced; ConcurrentDictionary.TryRemove is lock-free | PASS [checkmark] |
| JS-008 | No mutable struct fields / unfrozen brushes | PASS -- no struct usage introduced | CORRECTLY ATTRIBUTED [checkmark] |
| JS-023 | No UI update from off-thread without Dispatcher.InvokeAsync | N/A -- no UI changes in this fix | CORRECTLY ATTRIBUTED, N/A CORRECT [checkmark] |
| JS-025 | ConcurrentDictionary is lock-free canonical | PASS -- TryRemove mirrors Cancelled branch | PASS [checkmark] |
| JS-066 | CYC <= 8 | PASS -- EvictDedup 5->6 (<=7); IsLiveEntryBlocked 4 (<=5) | PASS [checkmark] |
| JS-080 (implied) | ASCII-only string literals | PASS -- all ASCII | PASS [checkmark] |

- V1 fix verified: JS-023 now correctly states "No UI update from off-thread without
  Dispatcher.InvokeAsync" and is correctly marked N/A. [checkmark]
- JS-008 now correctly states "No mutable struct fields / unfrozen brushes." [checkmark]
- JS-021 citation correct: "No lock()" with ConcurrentDictionary.TryRemove as lock-free. [checkmark]
- JS-025 citation correct: "ConcurrentDictionary.TryRemove is lock-free." [checkmark]
- No rule mis-citations remain. [checkmark]

**Finding**: PASS

---

### 7. SCAN-01 THROUGH SCAN-07

**Result**: PASS

All 7 scans addressed in Section 8:

| Scan | Description | Plan Result | Finding |
|------|-------------|-------------|---------|
| SCAN-01 | CYC <= 8 all changed methods | EvictDedup 5->6 (<=7); IsLiveEntryBlocked 4 (<=5) | PASS |
| SCAN-02 | No lock() in changed files | None introduced; ConcurrentDictionary.TryRemove lock-free | PASS |
| SCAN-03 | No return null in changed methods | EvictDedup is void | PASS |
| SCAN-04 | No DateTime.Now | No DateTime usage introduced | PASS |
| SCAN-05 | No async void non-event-handler | No async methods introduced | PASS |
| SCAN-06 | ASCII-only string literals | All comment and log text ASCII | PASS |
| SCAN-07 | No ?.Event -= null-conditional unsubscription | No event subscription changes | PASS |

All 7 present. All return PASS for the planned change.

**Finding**: PASS

---

### 8. ASCII-ONLY COMPLIANCE

**Result**: PASS

All code snippets in Sections 3, 6, 7, 9 reviewed character by character:

- Fixed code block (Section 3): `_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey)`,
  `_liveEntryInstruments.TryRemove(filledInstrKey, out _)` -- ASCII only. [checkmark]
- Comment text `// PTT-REPAIRS-02: clear instrKey on fill -- entry lifecycle is complete, followers dispatched.`
  Dashes are `--` (ASCII 0x2D repeated), not em-dash. [checkmark]
- Before/after code blocks (Section 9): ASCII only. [checkmark]
- Test string literals: `"MGC DEC26|Sell"`, `"PTTR02-orderId-1"`, `"PTTR02-orderId-2"`,
  `"MGC DEC26"` -- ASCII only. [checkmark]
- Header comment: `terminal-guard(1) + Cancelled(2) + instrKey-lookup(3) + Filled(4) + filledInstrKey-remove(5)` -- ASCII only. [checkmark]
- No Unicode characters, emoji, or curly/smart quotes found anywhere in the plan's code snippets. [checkmark]

**Finding**: PASS

---

### 9. NO LOCK()

**Result**: PASS

No `lock(` keyword appears in any proposed code in the plan.

Fix uses `ConcurrentDictionary.TryRemove` exclusively -- consistent with JS-021 (lock-free
requirement) and JS-025 (ConcurrentDictionary is lock-free canonical). Pattern mirrors the
existing Cancelled branch at lines 5758-5759.

**Finding**: PASS

---

## Spec Coverage Matrix

| Requirement | Plan Section | Addressed? |
|-------------|-------------|------------|
| PTT-REPAIRS-02-T1: Fix `_liveEntryInstruments` over-blocking on fill in `EvictDedup` | Sections 3, 5, 9 | YES |
| Option A selected: clear instrKey in `EvictDedup` on Filled | Section 3 | YES |
| MGC cancel+resubmit guard (DW-B142-MGC-02) must not be broken | Sections 3 (points 1-2), 5 | YES |
| `_entryDispatchedOrders` remains primary MGC guard | Sections 3 (point 1), 6 | YES |
| Test: `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry` | Section 7 | YES |
| Test count baseline: 300 (verified), target: 301 | Section 1 | YES |
| All 7 scan results provided | Section 8 | YES |
| CYC projections provided for changed and unchanged methods | Section 4 | YES |
| NT8 API surface documented (no phantom APIs) | Section 10 | YES |
| RULES_CATALOG compliance table complete and correctly attributed | Section 11 | YES |

All spec requirements addressed.

---

## Violation Summary

| ID | Severity | Rule | Location | Status |
|----|----------|------|----------|--------|
| V1 | BLOCKING | JS-023 | Section 11, row 3 | CLOSED (Correction Cycle 1) |

**Active blocking violations: 0**

---

## Return

**REVIEW_PASS**

Correction Cycle 1 verified: V1 (JS-023 mis-citation) is resolved.
- JS-023 now correctly described as "No UI update from off-thread without Dispatcher.InvokeAsync" and correctly marked N/A.
- JS-008 now correctly described as "No mutable struct fields / unfrozen brushes."
- No new violations introduced by the correction.

All 9 checklist items PASS:
1. LANE-SPLIT GATE: PASS -- SINGLE-PIPELINE result present and valid.
2. OPTION SELECTION: PASS -- Option A justified (4-point proof); Option B rejection grounded.
3. MGC GUARD INTEGRITY: PASS -- DW-B142-MGC-02 provably intact (6-row scenario matrix).
4. CYC BUDGET: PASS -- EvictDedup 5->6 (<=7 budget); IsLiveEntryBlocked 4 (<=5 budget).
5. TEST DESIGN: PASS -- Behavioral 5-step test, correct seams, CYC=1, [Fact] only.
6. JANE STREET RULES: PASS -- JS-008, JS-021, JS-023, JS-025 all correctly attributed.
7. SCAN-01 through SCAN-07: PASS -- All 7 present and all return PASS.
8. ASCII-ONLY: PASS -- No Unicode, emoji, or curly quotes in any code snippet.
9. NO LOCK(): PASS -- Zero lock() in proposed code; ConcurrentDictionary.TryRemove only.

Phase 3 (ticket generation) is UNLOCKED.

---

*ptt-plan-reviewer · PTT-REPAIRS-02 · 2026-09-07 · Cycle 2 (Re-Review)*
