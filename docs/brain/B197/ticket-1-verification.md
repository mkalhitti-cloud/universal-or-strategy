# B197 Ticket 1 Verification

**Verifier**: ptt-verifier
**Epic**: B197 -- Gap Restore: TrySetSlateReadyOnDrainFill + IsLeaderFlatDrainAbort + TryCancelStaleGtcExitOnEntry + IsPttDragOrphanCancellable suffix fix
**Ticket**: T1 -- TrySetSlateReadyOnDrainFill (GAP-5, DW-B157-01)
**Phase**: 4b (Independent Verification)
**Date**: 2026-10-12
**Status**: VERIFY_PASS (with pre-existing test failure noted)

---

## Scope
TICKET 1 ONLY -- TrySetSlateReadyOnDrainFill

Files verified (READ ONLY):
- src/PropTraderTools/CopyEngine.cs (lines 1063-1080, 1554-1559)
- src/PropTraderTools/Tests/B197Tests.cs
- docs/brain/B197/04-tickets.md (T1 section only)
- docs/brain/B197/04-ticket-review.md (T1 section)
- docs/brain/B197/02-architecture-plan.md (T1 design section)
- docs/brain/B197/ticket-1-completion.md

---

## 7-Scan Results (ALL INDEPENDENT -- Layer 3)

### SCAN-01: CYC Check (Lizard)
Command: python -m lizard src/PropTraderTools/CopyEngine.cs --CCN 8
Pattern match: TrySetSlateReadyOnDrainFill
Result: CCN=6 (Lizard counts e?.Order as +1 extra branch vs McCabe spec estimate of 5)
- base(1) + Guard1_if(1) + Guard1_null_cond(?. operator)(1) + Guard2(1) + Guard3(1) + Guard4(1) = 6
- CCN=6 is <= 8 ceiling (JS-066 P0)
**PASS -- CYC=6, within <= 8 ceiling**

Note: Ticket spec estimates CYC=5 (McCabe), Lizard reports 6 (?. counted as extra branch).
Engineer completion report also noted CCN=6 (consistent). No discrepancy between engineer and verifier.

### SCAN-02: Lock Scan
Command: Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock("
Result: 9 hits, ALL in comments only (e.g., "// JS-021: no lock()" annotations). Zero executable lock() calls.
**PASS -- 0 executable lock() occurrences**

### SCAN-03: Async Void Scan
Command: Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "async void " | Where-Object { .Line -notmatch "//" }
Result: No output -- zero async void in non-comment code.
**PASS -- 0 async void occurrences**

### SCAN-04: Return Null Scan (new method scope)
TrySetSlateReadyOnDrainFill (lines 1070-1080) is declared private void.
Void methods cannot have eturn null;.
Nearest eturn null; in file is at line 1307, outside the method.
**PASS -- 0 return null in new method (void, N/A)**

### SCAN-05: ASCII Check
Command: Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]"
Result: No output -- 0 non-ASCII characters in entire file.
**PASS -- 0 non-ASCII characters**

### SCAN-06: Build Scan
Command: dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj
Result: 0 Error(s), 0 Warning(s) introduced by T1.
**PASS -- clean build**

### SCAN-07: Test Scan (B197Tests.cs)
Command: dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "FullyQualifiedName~B197"
Result: Passed! Failed: 0, Passed: 2, Skipped: 0, Total: 2
- TrySetSlateReadyOnDrainFill_SourceContainsFollowerSlateReady: PASS
- TrySetSlateReadyOnDrainFill_SourceContainsSlateReadyDrainMarker: PASS
**PASS -- 2/2 B197 tests pass**

### SCAN-07 EXTENDED: DwB15701SlateReadyTests (pre-existing test class in CopyEngineTests.cs)
Command: dotnet test --filter "FullyQualifiedName~DwB15701SlateReadyTests"
Result: Failed: 2, Passed: 3, Skipped: 3, Total: 8

TWO PRE-EXISTING FAILURES:

(A) TrySetSlateReadyOnDrainFill_CalledAfterHandleDrainTerminal -- FAIL
    Test searches for "TrySetSlateReadyOnDrainFill(e.Order)" in source.
    Actual call site: TrySetSlateReadyOnDrainFill(e) (OrderEventArgs, not Order).
    Test also searches for "HandleDrainTerminalState(e.Order)" -- this method does NOT exist.
    ROOT CAUSE: Pre-existing test written with wrong signature expectation.
      - Method takes OrderEventArgs e (per T1 spec), called as TrySetSlateReadyOnDrainFill(e).
      - Test expected Order argument and a non-existent HandleDrainTerminalState caller.
    STATUS: Pre-existing failure. This test was also failing BEFORE T1 (method did not exist).
    AT HEAD (before T1): TrySetSlateReadyOnDrainFill absent -> _MethodExists_StructuralCheck FAILED.
    After T1: method exists, _MethodExists_StructuralCheck now PASSES; but the 2 wrong-expectation
    tests remain failing because T1 did not introduce HandleDrainTerminalState or the e.Order call arg.

(B) TrySetSlateReadyOnDrainFill_NullOrder_NoSideEffect -- FAIL
    Test invokes CopyEngine.Instance which throws TypeInitializationException (requires NT8 runtime).
    ROOT CAUSE: Pre-existing NT8 runtime dependency in test infrastructure.
    STATUS: Pre-existing failure unrelated to T1 implementation.

CLASSIFICATION: Both failures are PRE-EXISTING and were failing BEFORE T1 was applied.
T1 did not introduce these failures. T1 also did not resolve them (outside T1 scope).
DW REQUIRED: A new DW item should be opened for the HandleDrainTerminalState expectation
mismatch in DwB15701SlateReadyTests (wrong arg type Order vs OrderEventArgs).

---

## Implementation Review

### Method: TrySetSlateReadyOnDrainFill

**Location**: src/PropTraderTools/CopyEngine.cs, lines 1070-1080
**Placement**: After closing brace of TrySetFollowerSlateReady (~line 1062), in follower-slate section. Correct per T1 spec §5 Step 1.

**Verified method body** (lines 1070-1080):
`csharp
private void TrySetSlateReadyOnDrainFill(OrderEventArgs e)
{
    if (e?.Order == null) return;                                          // (1)
    if (e.Order.OrderState != OrderState.Filled) return;                   // (2)
    if (!_drainOwnedOrderIds.ContainsKey(e.Order.OrderId)) return;         // (3)
    if (!IsFollowerAccount(e.Order.Account)) return;                       // (4)
    _followerSlateReady[e.Order.Account.Name] = true;
    NinjaTrader.Code.Output.Process(
        "[SLATE-READY-DRAIN] acct=" + e.Order.Account.Name,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
`

Guard sequence matches spec exactly:
- Guard (1) if (e?.Order == null) -- PRESENT ✓
- Guard (2) if (e.Order.OrderState != OrderState.Filled) -- PRESENT ✓
- Guard (3) if (!_drainOwnedOrderIds.ContainsKey(e.Order.OrderId)) -- PRESENT ✓
- Guard (4) if (!IsFollowerAccount(e.Order.Account)) -- PRESENT ✓
- Body: _followerSlateReady[e.Order.Account.Name] = true; -- PRESENT ✓
- Log: "[SLATE-READY-DRAIN] acct=" -- PRESENT ✓

### Call Site: OnOrderUpdate Filled branch

**Location**: src/PropTraderTools/CopyEngine.cs, lines 1554-1559
`csharp
else if (e.Order.OrderState == OrderState.Filled)
{
    TrySetSlateReadyOnDrainFill(e);                  // T1: set slate-ready FIRST
    AbortDrainOnFill(e.Order.Account.Name);          // R2-F1: clean _drainOwnedOrderIds
    // TODO B197-T3: TryCancelStaleGtcExitOnEntry(e);
}
`

ORDERING INVARIANT SATISFIED:
- TrySetSlateReadyOnDrainFill(e) is FIRST (line 1556) ✓
- AbortDrainOnFill is SECOND (line 1557) ✓
- T3 placeholder comment on line 1558 (not yet implemented, per T1 scope) ✓

**CRITICAL ORDERING CHECK**: TrySetSlateReadyOnDrainFill PRECEDES AbortDrainOnFill.
AbortDrainOnFill removes order ID from _drainOwnedOrderIds. Guard (3) checks ContainsKey.
If reversed, Guard (3) would always fail silently. Ordering is CORRECT.

### Test Coverage

File: src/PropTraderTools/Tests/B197Tests.cs
Framework: xUnit (correct -- not NUnit/MSTest)
Class: B197Tests (correct)
Facts:
- TrySetSlateReadyOnDrainFill_SourceContainsFollowerSlateReady: 1 Assert.Contains ✓ (JS-056 compliant)
- TrySetSlateReadyOnDrainFill_SourceContainsSlateReadyDrainMarker: 1 Assert.Contains ✓ (JS-056 compliant)
Both tests PASS.

---

## DNA Rule Check Results

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (P0): No lock() | SCAN-02: 0 executable lock() in file. _drainOwnedOrderIds.ContainsKey and _followerSlateReady[] are ConcurrentDictionary ops. Lock-free. | PASS |
| JS-001 (P0): No throw | SCAN-04: No throw new in new method. Method uses early returns only. | PASS |
| JS-002: No null return | Method is void. No null return possible. | PASS |
| JS-066 (P0): CYC <= 8 | SCAN-01: CCN=6 (Lizard). <= 8 ceiling. | PASS |
| ASCII-only | SCAN-05: 0 non-ASCII characters in file. "[SLATE-READY-DRAIN] acct=" is ASCII. | PASS |
| No async void | SCAN-03: 0 async void in non-comment code. | PASS |
| DateTime.Now ban | No DateTime usage in new method. | PASS |
| FontFamily ban | Not applicable (no WPF element added). | N/A |
| Hex color ban | Not applicable (no color string added). | N/A |
| CreateOrder PTT- prefix | No CreateOrder call in new method. | N/A |
| sealed on TradeCopierWindow | Not modified by T1. | N/A |
| Magic string for state | No mode/state discrimination strings in new method. | PASS |
| return null where non-null expected | Void method. | PASS |
| Non-private constructor (CopyEngine singleton) | No new constructors added. | PASS |

---

## Cross-Check vs Ticket 1 Spec (Success Criteria)

| # | Criterion (from T1 §12) | Result |
|---|------------------------|--------|
| 1 | TrySetSlateReadyOnDrainFill method body exists in CopyEngine.cs | SATISFIED -- line 1070 |
| 2 | Method body contains _followerSlateReady[ write | SATISFIED -- line 1076 |
| 3 | Method body contains [SLATE-READY-DRAIN] log marker | SATISFIED -- line 1078 |
| 4 | TrySetSlateReadyOnDrainFill(e) appears FIRST in else if (OrderState.Filled) block, before AbortDrainOnFill | SATISFIED -- line 1556 is before 1557 |
| 5 | All 7 scans pass (0 lock, CYC<=8, ASCII-only, no new throw, guards present, build clean, T1 tests pass) | SATISFIED -- all 7 scans pass |
| 6 | dotnet test --filter B197 shows both Facts PASS | SATISFIED -- 2/2 pass |

---

## Architecture Compliance

| Requirement | Check | Result |
|-------------|-------|--------|
| Method placed after TrySetFollowerSlateReady in follower-slate section | Confirmed at line 1070 (~after line 1062 per spec §5 Step 1) | PASS |
| Method signature: private void TrySetSlateReadyOnDrainFill(OrderEventArgs e) | Confirmed at line 1070 | PASS |
| Call site: OnOrderUpdate Filled branch | Confirmed at line 1556 | PASS |
| SCOPE LOCK: only T1 in this session | Ticket T2/T3/T4 not modified (T3 is TODO placeholder only) | PASS |
| Files modified per spec: CopyEngine.cs + B197Tests.cs + .csproj | All 3 confirmed modified | PASS |
| Header comment block at method definition (B197 T1 DW-B157-01 attribution) | Confirmed at lines 1063-1069 | PASS |

---

## Pre-Existing Test Failure Assessment

Two tests in CopyEngineTests.cs:DwB15701SlateReadyTests are failing:

1. TrySetSlateReadyOnDrainFill_CalledAfterHandleDrainTerminal (CopyEngineTests.cs:9630)
   - Was failing BEFORE T1 (method was absent)
   - Still failing AFTER T1 due to wrong test expectations:
     (a) Test expects HandleDrainTerminalState(e.Order) call site -- this method does NOT exist in CopyEngine.cs
     (b) Test expects call arg e.Order (Order type), actual is e (OrderEventArgs type)
   - T1 spec/ticket correctly defines TrySetSlateReadyOnDrainFill(OrderEventArgs e) taking e
   - This pre-existing test has wrong API assumptions -- a new DW card should be created
   - NOT a T1 regression; NOT T1 scope

2. TrySetSlateReadyOnDrainFill_NullOrder_NoSideEffect (CopyEngineTests.cs:9562)
   - Was failing BEFORE T1 (CopyEngine.Instance requires NT8 runtime)
   - TypeInitializationException is pre-existing infrastructure limitation
   - NOT a T1 regression; NOT T1 scope

VERDICT on pre-existing failures: T1 did NOT introduce these failures and they were NOT
within T1 scope to fix. T1 successfully resolved DW-B157-01. A follow-up DW item is
recommended for the HandleDrainTerminalState expectation mismatch in CopyEngineTests.cs.

---

## Verdict

### 7-Scan Checklist Summary
| Scan | Result |
|------|--------|
| SCAN-01: CYC check (Lizard) -- TrySetSlateReadyOnDrainFill CCN=6 <= 8 | PASS |
| SCAN-02: lock() grep -- 0 executable locks | PASS |
| SCAN-03: async void grep -- 0 | PASS |
| SCAN-04: return null in new method (void, N/A) | PASS |
| SCAN-05: ASCII check -- 0 non-ASCII | PASS |
| SCAN-06: dotnet build -- 0 errors | PASS |
| SCAN-07: dotnet test --filter B197 -- 2/2 pass | PASS |

### DNA Rule Summary
All applicable DNA rules: PASS
JS-021 (no lock): PASS | JS-001 (no throw): PASS | JS-066 (CYC<=8): PASS | ASCII-only: PASS

### Success Criteria
All 6 T1 success criteria: SATISFIED

### Pre-existing Test Failures
2 pre-existing failures in DwB15701SlateReadyTests (CopyEngineTests.cs) noted.
Neither was introduced by T1. Neither is within T1 scope.
DW recommended for HandleDrainTerminalState mismatch.

---

## VERIFY_PASS

T1 implementation of TrySetSlateReadyOnDrainFill is correct, complete, and compliant.
All 7 independent scans pass. All DNA rules satisfied. Ordering invariant enforced.
DW-B157-01 closure confirmed. B197Tests.cs 2/2 pass.

---

*Written by: ptt-verifier*
*Epic: B197*
*Phase: 4b (Independent Verification)*
*Ticket: T1*
*Verdict: VERIFY_PASS*
*Violations: 0*
*Pre-existing failures noted: 2 (DwB15701SlateReadyTests, CopyEngineTests.cs -- not T1 scope)*
