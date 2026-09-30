# BWAVE-CYC Lane-B TB-T2 Engineer Completion Report

**Ticket**: TB-T2
**Target Method**: OnOrderUpdate
**File**: src/PropTraderTools/CopyEngine.cs
**Date**: 2025-01-09
**Build Tag**: BWAVE-CYC Lane-B TB-T2

---

## RULES CATALOG GATE

**GATE RESULT**: PASS
- NT8_COMPILER_RULES.md read (no `init`, no `record`, no `volatile double`, no `ImmutableDictionary`)
- JS-021 (no `lock()`): all new helpers use ConcurrentDictionary lock-free ops only
- JS-001 (no throw): all new helpers are void/bool with early returns
- JS-002 (no `return null` for missing values): helpers return void or bool
- JS-033 (no `async void`): all new helpers are synchronous

---

## DESIGN CORRECTION APPLIED

Per LaneB-02-architect-plan.md DESIGN CORRECTION section:
- `IsDispatchTriggerState` already exists at L1989 -- NOT created (no duplicate)
- `DispatchCopyToFollowers` loop is in `DispatchCopy` (TB-T4) -- NOT touched
- Actual source of CCN=23: two inline BE-recovery blocks at L1344-1374 (pre-extraction numbering)
- Correct extraction: `TryRecordBeTargetFill` + `TryTriggerBeRecovery`
- Additional helpers extracted to reach CCN<=8 on parent: `LogBeCancelDiag`, `TryReplaceOnAtmCancel`, `TryMirrorOrderUpdate`

---

## METHODS MODIFIED

### OnOrderUpdate (parent)
- **CCN before**: 23 (Lizard, from mission brief)
- **CCN after**: 8 (Lizard confirmed: `TrimSignal::OnOrderUpdate@1328-1413`)
- **Change**: Replaced two inline BE-recovery blocks (L1344-1376 pre-extraction) with calls to
  `TryRecordBeTargetFill(e.Order)` and `TryTriggerBeRecovery(e.Order)`.
  Also fused `if (IsPttEntryOrderCancelTrigger(...))` into `TryReplaceOnAtmCancel(...)` (no branch in parent),
  split `matchedRule == null || !matchedRule.Value.Enabled` into two separate early returns (eliminated `||`),
  and extracted `if ((CopyMode)_copyModeValue == CopyMode.Mirror)` into `TryMirrorOrderUpdate(...)` (no branch in parent).
- **4-gate sequence**: preserved in identical order (Gate1=enabled, Gate2=rule-match null, Gate2.5=rule-disabled, Gates-B+C=drag)

---

## HELPERS EXTRACTED (from Lizard output)

| Helper | CCN (Lizard) | Location | Notes |
|--------|-------------|----------|-------|
| `TryRecordBeTargetFill(Order o)` | 6 | L3635-3648 | DW-B92: records PTT-BE-Target-* fill before OCO cancel |
| `TryTriggerBeRecovery(Order o)` | 7 | L3658-3674 | DW-B79-08: re-places OCO pairs on PT-BE-Stop-* cancel |
| `LogBeCancelDiag(Order o)` | 3 | L3679-3692 | Diagnostic output -- extracted to eliminate `?.`/`??` from TryTriggerBeRecovery CCN |
| `TryReplaceOnAtmCancel(Order order)` | 2 | L851-856 | Fuses IsPttEntryOrderCancelTrigger + ReplaceFollowerCopyOnAtmCancel |
| `TryMirrorOrderUpdate(Order, CopyRule)` | 2 | L1905-1910 | Fuses CopyMode.Mirror guard with MirrorOrderUpdate |

---

## TEST SEAMS ADDED (CopyEngine.cs)

| Seam | Type | Purpose |
|------|------|---------|
| `TryRecordBeTargetFillNullTestable()` | internal void | Calls TryRecordBeTargetFill(null) |
| `WouldRecordBeTargetFill(OrderState, string, string)` | internal bool | Guard predicate + AddOrUpdate test seam |
| `GetFilledBeTargetCount(string)` | internal int | Read _filledBeTargetCount for assertions |
| `TryTriggerBeRecoveryNullTestable()` | internal void | Calls TryTriggerBeRecovery(null) |
| `WouldTriggerBeRecovery(OrderState, string)` | internal bool | Guard predicate test seam (no side effects) |

---

## [Fact] TESTS ADDED

File: src/PropTraderTools/Tests/BwaveCycLaneBTests.cs (class BwaveCycLaneBT2Tests)

1. `TryRecordBeTargetFill_DoesNothing_WhenOrderIsNull`
2. `TryRecordBeTargetFill_DoesNothing_WhenStateIsNotFilled`
3. `TryRecordBeTargetFill_DoesNothing_WhenNameDoesNotStartWithPttBeTarget`
4. `TryRecordBeTargetFill_IncrementsCount_WhenConditionMet`
5. `TryTriggerBeRecovery_DoesNothing_WhenOrderIsNull`
6. `TryTriggerBeRecovery_DoesNothing_WhenStateIsNotCancelled`
7. `TryTriggerBeRecovery_DoesNothing_WhenNameDoesNotStartWithPttBe`

**Filter run result**: `dotnet test --filter "BwaveCycLaneBT2"` -- **Passed: 7, Failed: 0, Total: 7**

---

## SCAN RESULTS

| Scan | Command | Result |
|------|---------|--------|
| SCAN-01 (lock) | grep -r "lock(" src/PropTraderTools/ | 0 hits in new code |
| SCAN-02 (non-ASCII) | Select-String non-ASCII | 0 (ASCII-only helpers) |
| SCAN-03 (FontFamily) | Select-String FontFamily | 0 (no UI in helpers) |
| SCAN-04 (#RRGGBB hex) | Select-String #[0-9A-Fa-f]{6} | 0 |
| SCAN-05 (PTT- prefix) | No new CreateOrder calls | 0 violations |
| SCAN-06 (DateTime.Now) | Select-String DateTime.Now[^U] | 0 (uses DateTime.UtcNow.Ticks in test only) |
| SCAN-07 (lock() pattern) | Select-String lock\s*\( | 0 |

---

## BUILD RESULTS

- **dotnet build**: `Build succeeded. 0 Warning(s). 0 Error(s).`
- **cs delta**: `No issues found!` (CodeScene delta analysis -- no new issues)
- **dotnet test (full run)**: Failed=119, Passed=421, Skipped=15, Total=555
  - Pre-existing baseline (TB-T1): Failed=122, Passed=403, Total=540
  - Delta: +15 total tests (7 TB-T2 + 8 others from LaneC work), -3 failures (pre-existing improved), +18 passing
  - **Zero new failures introduced**
- **Lizard CCN gate**: OnOrderUpdate=8 (was 23), all new helpers <=8, no warnings in modified methods

---

## BUILD_PASS
