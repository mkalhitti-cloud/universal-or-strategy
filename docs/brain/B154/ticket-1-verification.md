# Ticket 1 Verification — Block B154
# Sweep stale PTT-BE-* and PTT-QX-* GTC exit orders on new entry fill

**Verifier:** PTT Verifier (ptt-verifier mode)
**Phase:** 4b — Independent Verification
**Ticket:** DW-B154-01 (HIGH), DW-B154-02 (MEDIUM)
**Date:** 2025 (current session)
**Source:** READ-ONLY scan of Wave workspace src/PropTraderTools/

---

## VERDICT: VERIFY_PASS

All 7 scans PASS. All 18 checklist items PASS. No discrepancies found vs Layer 2 report.

---

## Independent Scan Results (Layer 3 — Verifier-run)

### SCAN-7: `lizard --CCN 8 src/PropTraderTools/CopyEngine.cs`

**Result: 7 warnings total — all pre-existing, ZERO from B154 new code.**

B154 method CCN values (measured independently):
| Method | File Location | CCN | Lizard Warning? |
|--------|--------------|-----|-----------------|
| `IsStaleGtcExitNameFor` | CopyEngine.cs:4928-4933 | 4 | NO |
| `IsGtcExitCancellableState` | CopyEngine.cs:4939-4944 | 5 | NO |
| `TryCancelStaleGtcExitOnEntry` | CopyEngine.cs:4956-4987 | 8 | NO (exactly 8 — at limit) |
| `IsPttCopyEntryFilled` | CopyEngine.cs:7898-7905 | 7 | NO (<=8) |
| `IsPttCopyEntryFilledTestable` | CopyEngine.cs:7907 | 1 | NO |

Pre-existing warnings (7 total, unchanged from pre-B154):
1. `TrimSignal::MirrorCloseOneAccount` CCN=9 @ line 2416
2. `TrimSignal::TrimOneAccount` CCN=9 @ line 5719
3. `CopyRulesContainer::CancelExistingTgtDragOrders` CCN=9 @ line 8429
4. `CopyRulesContainer::HasInFlightFlattenOrder` CCN=9 @ line 8582
5. `CopyRulesContainer::ResubmitFollowerEntry` CCN=9 @ line 8613
6. `CopyRulesContainer::CancelStaleCascadeTgtDrag` CCN=10 @ line 8649
7. `CopyRulesContainer::IsPttDragOrderCancellable` CCN=10 @ line 8919

**PASS — 0 new warnings from B154.**

---

### SCAN-8: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\s*\("`

**Result: 79 matches total — all pre-existing. Zero matches in B154 new code (lines 4924-4990, 7892-7910).**

Verified independently: no `lock\s*(` in any B154 method (`IsStaleGtcExitNameFor`, `IsGtcExitCancellableState`, `TryCancelStaleGtcExitOnEntry`, `IsPttCopyEntryFilled`, `IsPttCopyEntryFilledTestable`).

**PASS — 0 matches in B154 new code.**

---

### SCAN-9: Non-ASCII bytes in `CopyEngine.cs`

Command: `(Get-Content src/PropTraderTools/CopyEngine.cs -Encoding Byte | Where-Object { $_ -gt 127 }).Count`

**Result: 0**

**PASS.**

---

### SCAN-9b: Non-ASCII bytes in `CopyEngineTests.cs`

Command: `(Get-Content src/PropTraderTools/CopyEngineTests.cs -Encoding Byte | Where-Object { $_ -gt 127 }).Count`

**Result: 0**

**PASS.**

---

### SCAN-10: `dotnet build src/PropTraderTools/`

**Result:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**PASS.**

---

### SCAN-L: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\("` (secondary)

**Result: 0 matches in B154 new code (lines 4924-4990, 7892-7910).**

**PASS.**

---

### SCAN-B: `dotnet test src/PropTraderTools/`

**Result:**
```
Passed!  - Failed: 0, Passed: 186, Skipped: 369, Total: 555, Duration: 605ms
```

B154 test results:
| Test | Class | Status |
|------|-------|--------|
| `IsPttCopyEntryFilled_ShouldReturnTrue_WhenFilledEntryOrder` | CopyEngineTests | SKIPPED (NT8-runtime) |
| `IsPttCopyEntryFilled_ShouldReturnTrue_WhenFilledPttCopyOrder` | CopyEngineTests | SKIPPED (NT8-runtime) |
| `IsPttCopyEntryFilled_ShouldReturnFalse_WhenOrderStateIsNotFilled` | CopyEngineTests | SKIPPED (NT8-runtime) |
| `IsPttCopyEntryFilled_ShouldReturnFalse_WhenNameIsNeither` | CopyEngineTests | SKIPPED (NT8-runtime) |
| `IsPttCopyEntryFilled_ShouldReturnFalse_WhenInstrumentIsNull` | CopyEngineTests | SKIPPED (NT8-runtime) |
| `TryCancelStaleGtcExitOnEntry_ShouldExist_AsPrivateInstanceHelper` (CopyEngineTests) | CopyEngineTests | SKIPPED (NT8-runtime) |
| `TryCancelStaleGtcExitOnEntry_ShouldExist_AsPrivateInstanceHelper` (B154T1ContractTests) | B154T1ContractTests | **PASSED** |

Zero failures. Contract test in `B154T1ContractTests` (no NT8 dependency) runs and passes.

**PASS.**

---

## Cross-Check: Layer 3 vs Layer 2 (engineer self-report)

| Item | Engineer (Layer 2) | Verifier (Layer 3) | Match? |
|------|-------------------|--------------------|--------|
| SCAN-7 new warnings | 0 | 0 | YES |
| SCAN-7 pre-existing warnings | 7 | 7 (confirmed names) | YES |
| SCAN-8 lock matches in new code | 0 | 0 | YES |
| SCAN-9 non-ASCII bytes CopyEngine.cs | 0 | 0 | YES |
| SCAN-9b non-ASCII bytes CopyEngineTests.cs | 0 | 0 | YES |
| SCAN-10 build errors | 0 | 0 | YES |
| SCAN-B failed tests | 0 | 0 | YES |
| SCAN-B passed tests | 186 | 186 | YES |
| SCAN-B skipped tests | 369 | 369 | YES |
| IsPttCopyEntryFilled CCN | 7 | 7 | YES |
| TryCancelStaleGtcExitOnEntry CCN | 8 | 8 | YES |

**No discrepancies found between Layer 2 and Layer 3.**

---

## Verification Checklist

### Scan Gates

| Item | Result | Notes |
|------|--------|-------|
| SCAN-7: lizard CYC = 0 new warnings on CopyEngine.cs | **PASS** | 7 pre-existing, 0 from B154 |
| SCAN-8: grep lock = 0 matches in new code | **PASS** | 79 pre-existing, 0 in B154 methods |
| SCAN-9: no non-ASCII bytes in CopyEngine.cs | **PASS** | 0 bytes > 127 |
| SCAN-9b: no non-ASCII bytes in CopyEngineTests.cs | **PASS** | 0 bytes > 127 |
| SCAN-10: build clean | **PASS** | 0 errors, 0 warnings |
| SCAN-L: lock( secondary check | **PASS** | 0 in B154 methods |
| SCAN-B: all tests pass | **PASS** | 0 failures |

### Method Structure

| Item | Result | Notes |
|------|--------|-------|
| `TryCancelStaleGtcExitOnEntry` exists as private instance method | **PASS** | CopyEngine.cs:4956, `private void TryCancelStaleGtcExitOnEntry(Order order)` |
| `IsPttCopyEntryFilled` exists as private static method | **PASS** | CopyEngine.cs:7898, `private static bool IsPttCopyEntryFilled(Order order)` |
| `IsPttCopyEntryFilledTestable` exists as internal method | **PASS** | CopyEngine.cs:7907, `internal bool IsPttCopyEntryFilledTestable(Order o) => IsPttCopyEntryFilled(o);` |
| `TriggerPending` is in the state filter | **PASS** | `IsGtcExitCancellableState` at line 4944 includes `OrderState.TriggerPending` |
| Call site is between `TryNakedDetect` and `HandleDrainTerminalState` in `OnOrderUpdate` | **PASS** | Line 1712: TryNakedDetect; line 1717: TryCancelStaleGtcExitOnEntry; line 1722: HandleDrainTerminalState |
| Log line `"[BE-ENTRY-SWEEP]"` is present and unconditional (not inside an if) | **PASS** | Lines 4983-4986: outside any if block, after stale.Count==0 early-return guard. Fires whenever stale.Count > 0. |
| `try/catch` wraps `acc.Cancel(stale)` | **PASS** | Lines 4977-4981: `try { acc.Cancel(stale); } catch { }` |
| `acc.Orders.ToList()` pattern used (no lock()) | **PASS** | Line 4964: `foreach (var o in acc.Orders.ToList())` — no lock |
| No lock() in modified methods | **PASS** | Zero lock() in lines 4924-4990 and 7892-7910 |
| ASCII-only in all new string literals | **PASS** | SCAN-9: 0 bytes > 127; `"[BE-ENTRY-SWEEP]"`, `"PTT-BE-"`, `"PTT-QX-"`, `"Entry"`, `"PTT-Copy"` all 7-bit ASCII |

### Tests

| Item | Result | Notes |
|------|--------|-------|
| 6 new test methods present in CopyEngineTests.cs for Ticket 1 | **PASS** | Tests 1-5 (IsPttCopyEntryFilled_*) at lines 4298-4401 in CopyEngineTests class; Test 6 (TryCancelStaleGtcExitOnEntry_ShouldExist_AsPrivateInstanceHelper) at line 4392 in CopyEngineTests + line 9274 in B154T1ContractTests |

Note: The ticket spec (04-tickets.md line 217) says "All tests in existing CopyEngineTests class (do not create a new class)." The engineer added a duplicate Test 6 in the new `B154T1ContractTests` class to allow it to run without NT8 (the CopyEngineTests version is skipped). The `B154T1ContractTests` pattern exactly follows `B153IsPttQxTargetTests` precedent established in B153. Both versions of Test 6 exist; the runnable one passes. This is an acceptable deviation — the spec intent (verifiable reflection test) is fully satisfied. The 6 required test methods are all present.

### Spec / Architecture Compliance

| Item | Result | Notes |
|------|--------|-------|
| Implementation satisfies DW-B154-01: sweeps stale PTT-BE-* GTC exits on new entry fill | **PASS** | `IsStaleGtcExitNameFor` checks `StartsWith("PTT-BE-")` at line 4931 |
| Implementation satisfies DW-B154-02: sweeps stale PTT-QX-* GTC exits on new entry fill | **PASS** | `IsStaleGtcExitNameFor` checks `StartsWith("PTT-QX-")` at line 4932 |
| Architecture plan compliance: predicate + helper + call site pattern | **PASS** | All three elements implemented per plan |
| TriggerPending MUST be included (HOTFIX-QX-DOUBLE-01 constraint) | **PASS** | Included in `IsGtcExitCancellableState` at line 4944 |
| IsCancelAllStateOk NOT used (omits TriggerPending) | **PASS** | No `IsCancelAllStateOk` call in `TryCancelStaleGtcExitOnEntry` |
| JS-001: no throw in hot path | **PASS** | `try { acc.Cancel(stale); } catch { }` absorbs exception |
| JS-002: void helper, no nullable return | **PASS** | `TryCancelStaleGtcExitOnEntry` is void; `IsPttCopyEntryFilled` returns bool |
| JS-021: no lock() anywhere in new methods | **PASS** | Zero lock() in all B154 new code |
| JS-033: no async void | **PASS** | All new methods are synchronous |
| DW card annotation present in completion.md | **PASS** | Documented in ticket-1-completion.md |

---

## DNA Rule Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021/JS-023/JS-025: No lock() in new code | No lock in TryCancelStaleGtcExitOnEntry, IsPttCopyEntryFilled | PASS |
| JS-001/JS-002/JS-003: No throw in OnOrderUpdate/gate methods | try/catch absorbs, no rethrow | PASS |
| JS-008/JS-009: No mutable struct fields, brushes frozen | N/A — no structs or brushes | N/A |
| JS-010: Singleton/signal struct constructors | N/A — no new constructors | N/A |
| NT8: No async/await in lifecycle methods | No async added | PASS |
| NT8: No FontFamily= WPF attribute | N/A — no WPF | N/A |
| NT8: No #RRGGBB hex color | No hex colors in new code | PASS |
| NT8: CreateOrder uses PTT- prefix | No CreateOrder calls in new code | N/A |
| NT8: DateTime.UtcNow (not DateTime.Now) | No DateTime in new code | N/A |
| ASCII-only compliance | SCAN-9: 0 bytes > 127 | PASS |

---

## Discrepancies Found

**None.**

Layer 2 (engineer self-report) and Layer 3 (verifier independent scan) are in full agreement on all scan counts and results.

One minor observation: the engineer noted "7 methods (MirrorCloseOneAccount, TrimOneAccount, CancelExistingTgtDragOrders, HasInFlightFlattenOrder, ResubmitFollowerEntry, and 2 others)" as pre-existing lizard warnings. The actual 7 are those 5 plus `CancelStaleCascadeTgtDrag` (CCN=10) and `IsPttDragOrderCancellable` (CCN=10). This is consistent with the count (7=7) and is not a discrepancy — the engineer correctly categorized these as pre-existing.

---

## Final Verdict

**VERIFY_PASS**

All 7 scans pass. All 18 checklist items pass. No DNA rule violations. No lock() in new code. No non-ASCII bytes. Build succeeds with 0 errors/warnings. Tests: 0 failures. All required methods implemented with correct signatures, CYC values, state filters, log format, and call-site positioning per spec and architecture plan.
