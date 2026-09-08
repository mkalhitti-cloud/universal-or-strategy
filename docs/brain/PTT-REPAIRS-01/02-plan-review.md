# PTT-REPAIRS-01 Plan Review
**Status**: REVIEW_PASS
**Phase**: 2 (Plan Review)
**Epic**: PTT-REPAIRS-01
**Reviewer**: ptt-plan-reviewer
**Review Date**: 2026-09-07 (Cycle 2 re-review)
**Review Cycle**: 2 of 2 (max)
**Plan Author**: ptt-architect
**Plan File**: `docs/brain/PTT-REPAIRS-01/02-architecture-plan.md` (REVIEW_FAIL_FIXED version)
**Prior Review**: Cycle 1 — REVIEW_FAIL (2 violations: V1 blocking test count, V2 advisory Execute() CYC)

---

## Final Verdict: REVIEW_PASS

**Violations from Cycle 1**: 2
**Violations resolved in Cycle 2**: 2
**New violations found in Cycle 2**: 0

All blocking and advisory violations from cycle 1 are resolved. No new violations found. Plan is cleared for Phase 3 (ticket generation).

---

## Cycle 1 Violation Resolution Status

### V1 FIX CONFIRMED — Test Baseline Corrected (was BLOCKING)

**Cycle 1 violation**: Plan stated baseline=129, target=135. Actual `CopyEngineTests.cs` had 300 `[Fact]` methods.

**Cycle 2 fix verification**:
- `Section 1` line 40: `"Test count baseline at B26 close: **300 tests** (grep-verified: 300 [Fact] methods in CopyEngineTests.cs)."` ✅
- `Section 1` line 41: `"After PTT-REPAIRS-01: **300 + 6 = 306 tests**."` ✅
- `Section 7` bottom (line 818): `"**Test count after PTT-REPAIRS-01**: 300 + 6 = **306**."` ✅

**Independent verification**: grep `^\s+\[Fact\]` against `src/PropTraderTools/CopyEngineTests.cs` returns exactly **300 matches** (confirmed this cycle). Target of 306 is arithmetically correct. **FIXED — V1 RESOLVED.**

---

### V2 FIX CONFIRMED — Execute() No-Arg CYC and Extraction Scope Corrected (was ADVISORY)

**Cycle 1 violation**: Plan conflated `Execute()` no-arg (CYC=7) with `Execute(forcedTargets)` (CYC=8). Extraction of `TryCancelBeOrders` was stated as mandatory for both overloads when it is only mandatory for `Execute(forcedTargets)`.

**Cycle 2 fix verification**:

Section 3 R6 (line 543):
> `Execute()` no-arg: CYC=7 (per current source — spec comment states "AT-LIMIT" but actual Lizard count is 7, confirmed by reviewer). Adding 1 guard branch → CYC=8. **AT LIMIT but PASSES** (<=8). `TryCancelBeOrders` extraction is **OPTIONAL** for the no-arg overload.
> — If the engineer prefers consistency, extraction is still acceptable — it reduces caller CYC.
> — Extraction is **NOT MANDATORY** for `Execute()` no-arg since CYC=7+1=8 passes.

Section 9 SCAN-01 table (lines 903, 904):
> `Execute()` no-arg: CYC=7+1=8 AT LIMIT but PASSES (extraction OPTIONAL)
> `Execute(forcedTargets)`: CYC=8+1=9 → **REQUIRES EXTRACTION (MANDATORY)**

Section 9 note (line 903):
> "`Execute()` **no-arg** is at CYC=7. Adding 1 guard branch → CYC=8. PASSES. `TryCancelBeOrders` extraction is **OPTIONAL**"

The plan now correctly distinguishes:
- `Execute()` no-arg: CYC=7 → +1 guard → CYC=8 → **OPTIONAL** extraction
- `Execute(forcedTargets)`: CYC=8 → +1 guard → CYC=9 → **MANDATORY** extraction

Call sites at lines 60 (no-arg), 149 (`Execute(forcedTargets)`), 214 (`ExecuteFollowers()`) confirmed present in source. **FIXED — V2 RESOLVED.**

---

## Confirmation of All Cycle 1 Passing Items

### LANE-SPLIT GATE COMPLIANCE

| Item | Result |
|------|--------|
| `LANE-SPLIT GATE RESULT: LANES-APPROVED` present | PASS — Section 2 line 87 |
| Q1 = NO (no same-method or ≤50-line overlaps) | PASS — Section 2 table confirms NO for all inter-group pairs |
| Q2 = NO (no cross-group design dependencies) | PASS — Section 2 confirms all groups independent |
| Q3 = YES (each group has standalone value) | PASS — Section 2 confirms Groups A, B, C independently valuable |
| Q4 = YES (each group has independent SIM verification path) | PASS — Section 2 confirms unit tests or gitleaks scan per group |
| Sequential execution rationale stated | PASS — "Sequential execution due to shared CopyEngineTests.cs write-set" at Section 2 line 89 |

---

### SPEC TRACEABILITY

| Spec Item | Plan Section | Addressed? |
|-----------|-------------|------------|
| G1: `.gitleaks.toml` creation with `KeyEventHandler` path-level allowlist | Section 3 G1 | PASS |
| R1: `IsNakedConditionMet` — FullName equality at `CopyEngine.cs:7414` | Section 3 R1 | PASS |
| R2: `TryDrainWatchdog` PendingCancelCount gate + `ReissueDrainCancels` helper | Section 3 R2, Section 4, Section 5 | PASS |
| R3: `LoadAndValidateLicense` — remove 3 lines dev_mode.txt bypass at `TradeCopierAddOn.cs:700-702` | Section 3 R3 | PASS |
| R4: Fix A (`ApplyFeatureFlags` remove `_modeCb.IsEnabled`) + Fix B (`OnCopyModeComboChanged` Elite gate) | Section 3 R4 | PASS |
| R5: `BuildRuleRow` immediate `Account.All` bind + `OnLoaded` foreach removal | Section 3 R5 | PASS |
| R6: `CancelPttBeOrders` returns -1 on exception + all 3 call sites updated | Section 3 R6, Section 6 | PASS |
| 6 `[Fact]` tests named (T_R1 through T_R6) | Section 7 | PASS |
| Correct baseline (300) and target (306) | Section 1, Section 7 | PASS (V1 fix confirmed) |

---

### ALL 6 [Fact] TEST NAMES PRESENT

| Test Name | Plan Section | Present? |
|-----------|-------------|---------|
| `T_R1_IsNakedConditionMet_FullNameEquality` | Section 7 | PASS |
| `T_R2_TryDrainWatchdog_DoesNotSubmitWhileCancelsInFlight` | Section 7 | PASS |
| `T_R3_LoadAndValidateLicense_DevModeFileHasNoEffect` | Section 7 | PASS |
| `T_R4_MirrorModeGate_RevertsToSignalOnNonElite` | Section 7 | PASS |
| `T_R5_BuildRuleRow_AccountAllBoundImmediately` | Section 7 | PASS |
| `T_R6_CancelPttBeOrders_ReturnsNegativeOneOnException` | Section 7 | PASS |

---

### CYC VALIDATION (SCAN-01)

| Method | File | CYC Before | CYC After | Status |
|--------|------|-----------|-----------|--------|
| `IsNakedConditionMet` | CopyEngine.cs | 4 | 4 | PASS |
| `TryDrainWatchdog` | CopyEngine.cs | 4 | 5 | PASS |
| `ReissueDrainCancels` (NEW) | CopyEngine.cs | — | 4 | PASS |
| `LoadAndValidateLicense` | TradeCopierAddOn.cs | 4 | 3 | PASS |
| `ApplyFeatureFlags` | TradeCopierWindow.cs | 5 | 5 | PASS |
| `OnCopyModeComboChanged` | TradeCopierWindow.cs | 4 | 5 | PASS |
| `BuildRuleRow` | TradeCopierWindow.cs | 1 | 2 | PASS |
| `OnLoaded` | TradeCopierWindow.cs | reduced | reduced | PASS |
| `CancelPttBeOrders` | PttGlobalQuickExit.cs | 7 | 8 | PASS (AT LIMIT) |
| `Execute()` no-arg | PttGlobalQuickExit.cs | 7 | 8 | PASS (AT LIMIT, extraction OPTIONAL) |
| `Execute(forcedTargets)` | PttGlobalQuickExit.cs | 8 | 9 w/o extraction | EXTRACTION MANDATORY — plan correctly instructs engineer to extract `TryCancelBeOrders` |
| `ExecuteFollowers()` | PttGlobalQuickExit.cs | 7 | 8 | PASS (AT LIMIT) |
| `TryCancelBeOrders` (MANDATORY new helper) | PttGlobalQuickExit.cs | — | 2 | PASS |

All methods at or below CYC=8 after planned changes (with mandatory extraction for `Execute(forcedTargets)`). PASS.

---

### RULES_CATALOG COMPLIANCE

| Rule | Description | Status |
|------|-------------|--------|
| JS-021 | No `lock()` in changed files | PASS — CopyEngine.cs, TradeCopierAddOn.cs, TradeCopierWindow.cs, PttGlobalQuickExit.cs: zero `lock()` introduced. ConcurrentDictionary + Interlocked pattern used. |
| JS-002 | No `return null` in changed methods | PASS — `ReissueDrainCancels` is `void`; `IsNakedConditionMet` returns `bool`; `CancelPttBeOrders` returns `int`; `LoadAndValidateLicense` returns `FeatureFlags` (Starter() on catch, never null). |
| JS-001 | No `throw` in hot paths / on-order paths | PASS — R6 catch swallows and logs; R3 catch returns `Starter()`; no re-throws introduced. |
| JS-033 | No `async void` non-event-handler | PASS — No `async` methods introduced. `OnCopyModeComboChanged` is a WPF event handler (`void` is permitted). |
| JS-066 | CYC ≤ 8 | PASS — All changed methods verified at CYC ≤ 8. Mandatory extraction for `Execute(forcedTargets)` correctly identified and planned. |
| JS-080 | ASCII-only string literals | PASS — All log strings and comment strings in plan samples are ASCII-only. |
| xUnit `[Fact]` tests only (no NUnit/MSTest) | Section 7 uses `[Fact]` throughout | PASS |

---

### R6 CALL SITE VERIFICATION

| # | Method | Line | -1 Guard Present in Plan | Status |
|---|--------|------|--------------------------|--------|
| 1 | `Execute()` no-arg | ~60 | YES — `if (_beCancelCount < 0) { log; continue; }` | PASS |
| 2 | `Execute(forcedTargets)` | ~149 | YES — same pattern | PASS |
| 3 | `ExecuteFollowers()` | ~214 | YES — `if (_fBeCancelCount < 0) { log; continue; }` | PASS |

Source-confirmed call sites: lines 60, 149, 214 in [`Features/PttGlobalQuickExit.cs`](src/PropTraderTools/Features/PttGlobalQuickExit.cs). PASS.

---

### NT8 API SURFACE

| API | Availability | Plan Citation | Status |
|-----|-------------|---------------|--------|
| `Instrument.FullName` | String property on `Instrument` | NT8_FULL_REFERENCE.md:1926, PttGlobalQuickExit.cs:227 | PASS |
| `Account.All` ObservableCollection | Loaded-handler safe | NT8_ADDON_KNOWLEDGE.md:134,218; null guard added | PASS |
| `Account.Cancel(IEnumerable<Order>)` | AddOnBase available | CopyEngine.cs:7438 comment, PttGlobalQuickExit.cs:688 | PASS |
| `Order.OrderId` | `string` property | CopyEngine.cs:7694 comment | PASS |
| `FeatureFlags.MirrorMode` | `bool` property | TradeCopierWindow.cs:441 | PASS |
| `CopyEngine.Instance.Flags` | Singleton property | TradeCopierWindow.cs:153 | PASS |

No phantom APIs. All API claims grounded in actual source reads. PASS.

---

## Files Read in Cycle 2

| File | Verification Scope |
|------|--------------------|
| `docs/brain/PTT-REPAIRS-01/02-architecture-plan.md` | Full (V1 + V2 fix locations confirmed) |
| `docs/brain/PTT-REPAIRS-01/02-plan-review.md` | Full (cycle 1 violations confirmed) |
| `docs/standards/jane-street/RULES_CATALOG.md` | JS-001, JS-002, JS-021, JS-033, JS-066, JS-080 |
| `src/PropTraderTools/CopyEngineTests.cs` | grep `^\s+\[Fact\]` → 300 matches confirmed |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Lines 55-70 (call site 1, line 60), 140-165 (call site 2, line 149), 205-225 (call site 3, line 214) |

---

## Summary

Both violations from cycle 1 are resolved:

1. **V1 (BLOCKING) — RESOLVED**: Section 1 and Section 7 now correctly state baseline=**300**, target=**306**. Independently verified by grep against `CopyEngineTests.cs` (300 `[Fact]` matches confirmed this cycle).

2. **V2 (ADVISORY) — RESOLVED**: Plan now correctly distinguishes `Execute()` no-arg (CYC=7 → CYC=8 with guard → PASSES, extraction OPTIONAL) from `Execute(forcedTargets)` (CYC=8 → CYC=9 with guard → extraction MANDATORY). The no-arg overload is no longer misstated as requiring mandatory extraction.

No new violations found. All DNA rules (JS-021, JS-001, JS-002, JS-033, JS-066, JS-080) pass. All spec items G1, R1-R6 addressed. All 6 `[Fact]` test names present. CYC claims consistent and correct. Lane-split gate (Q1=NO, Q2=NO, Q3=YES, Q4=YES, LANES-APPROVED) intact. R6 call sites (lines 60, 149, 214) documented with -1 guards.

**Return**: REVIEW_PASS — Plan is cleared for Phase 3 (ticket generation).
