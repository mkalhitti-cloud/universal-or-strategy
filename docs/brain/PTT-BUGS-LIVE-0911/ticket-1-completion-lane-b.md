# Ticket 1 Completion Report -- PTT-BUGS-LIVE-0911 Lane B
**Engineer: ptt-engineer**
**Date: 2026-09-11**
**Ticket: T1 -- BUG-2: Post-Exit Clean Slate Gate**
**Scope: Ticket 1 ONLY (CopyEngine.cs + CopyEngineTests.cs)**

---

## 1. Ticket Scope

Implemented exactly Ticket 1 (BUG-2) as specified in `04-tickets-lane-b.md`.
No other tickets, bug fixes, or out-of-scope changes.
No changes to TradeCopierAddOn.cs, TradeCopierPanel.cs, TradeCopierWindow.cs, or PttQuickExit.cs.

---

## 2. Implementation Summary

### New field (CopyEngine.cs, after line 394)

- `private readonly ConcurrentDictionary<string, bool> _followerSlateReady`
  - StringComparer.Ordinal
  - No lock (JS-021, JS-025)
  - Absent key = gate open (BUG2-REQ-6)

### New methods added (5 total, inserted before CancelQxBrackets at original line 938)

| Method | CYC | Notes |
|---|---|---|
| `TryMarkFollowerSlateNotReady(Account)` | 2 | Sets key=false for follower accounts only |
| `TrySetFollowerSlateReady(Account)` | 4 | Sets key=true only when flat AND no active orders |
| `IsAccountFlat(Account)` | 3 | static, uses Account.Positions |
| `HasAnyActiveOrders(Account)` | 5 | static, uses Account.Orders.ToList() snapshot |
| `OnFollowerPositionUpdate(object, PositionEventArgs)` | 3 | synchronous void event handler, no async/await |

### Modified methods (6 total)

| Method | Change |
|---|---|
| `FindFollowerBracketOrder(Account, ...)` | Converted from expression-body to block-body. Added TryGetValue gate as FIRST check (BUG2-REQ-5, BUG2-REQ-6) |
| `Subscribe()` | Added `acc.PositionUpdate += OnFollowerPositionUpdate` in Account.All loop (BUG2-REQ-3) |
| `Unsubscribe()` | Added `acc.PositionUpdate -= OnFollowerPositionUpdate` in Account.All loop (BUG2-REQ-3) |
| `CancelQxBrackets(Account, Instrument)` 2-param | Added `TryMarkFollowerSlateNotReady(acc)` after null guard (BUG2-REQ-2) |
| `CancelQxBrackets(Account, Instrument, HashSet<Order>)` 3-param | Added `TryMarkFollowerSlateNotReady(acc)` after null guard (BUG2-REQ-2) |
| `FlattenOneAccount(Account, Instrument)` | Added `TryMarkFollowerSlateNotReady(acc)` after IsAccountFlattenable guard (BUG2-REQ-2) |

### Tests added (CopyEngineTests.cs)

Added `Bug2FollowerSlateReadyTests` class at namespace scope (not nested) with 8 [Fact] tests:
- 5 pass (pure BCL gate-semantics, no NT8 required)
- 3 skipped (NT8-runtime required for Account.Positions, Account.Orders, typeof(CopyEngine))

The 7 required test method names from the ticket are all present.

---

## 3. Scan Results

### SCAN-01: lock() check

**Command:** `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'lock\s*\(' | Measure-Object | Select-Object -ExpandProperty Count`

**Result:** 72 matches -- ALL in comments (e.g., `// JS-021: no lock (...)`)

**Actual lock() statements:** `Select-String -Pattern '^\s+lock\s*\(' | Measure-Object` = **0**

**Assessment:** PASS for new BUG2 code. The 72 comment matches are pre-existing JS-021 annotations (format: `no lock (...)`) not introduced by this ticket. No actual `lock()` statement exists anywhere in CopyEngine.cs. Document per ticket instructions: "report but do not fix in this PR."

---

### SCAN-02: CYC audit (manual branch count)

| Method | CYC | Limit | Result |
|---|---|---|---|
| `TryMarkFollowerSlateNotReady(Account)` | 2 | 8 | PASS |
| `TrySetFollowerSlateReady(Account)` | 4 | 8 | PASS |
| `IsAccountFlat(Account)` | 3 | 8 | PASS |
| `HasAnyActiveOrders(Account)` | 5 | 8 | PASS |
| `OnFollowerPositionUpdate(object, PositionEventArgs)` | 3 | 8 | PASS |
| `FindFollowerBracketOrder(Account, string?, bool, string?)` | 2 | 8 | PASS |

All 6 methods within stated CYC bounds. PASS.

---

### SCAN-03: async/await check

**Command:** `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '\basync\b|\bawait\b' | Measure-Object | Select-Object -ExpandProperty Count`

**Result:** 11 -- ALL in comments, ALL pre-existing (no async/await in any BUG2 method)

Lines: 1012, 2034, 3116, 3165, 3215, 3251, 3273, 3411, 6445, 7875, 7892 -- all pre-existing JS-033 annotation comments. `OnFollowerPositionUpdate` is synchronous void as required.

**Assessment:** PASS. Zero async/await in new BUG2 code.

---

### SCAN-04: Non-ASCII byte check

**Command:** `[System.IO.File]::ReadAllBytes('src\PropTraderTools\CopyEngine.cs') | Where-Object { $_ -gt 127 } | Measure-Object | Select-Object -ExpandProperty Count`

**Result:** **0**

**Assessment:** PASS.

---

### SCAN-05: AtmStrategy* compliance

**Command:** `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'AtmStrategyCreate|StartAtmStrategy|AtmStrategyChangeStopTarget' | Measure-Object | Select-Object -ExpandProperty Count`

**Result:** 14 -- ALL pre-existing (lines 98, 99, 1664, 2135, 4597, 5117, 5118, 5119, 5142, 5149, 5151, 7605, 7646, 7832)

None in any new BUG2 method. The existing StartAtmStrategy calls (lines 5149, 5151) are in `SendCopyWithAtm` which was not modified.

**Assessment:** PASS for new BUG2 code.

---

### SCAN-06: Build clean

**Command:** `dotnet build src\PropTraderTools\PropTraderTools.Tests.csproj 2>&1 | Select-String -Pattern "error|Build succeeded|Build FAILED"`

**Result:** `Build succeeded. 0 Error(s)`

Warnings present: pre-existing CS0436 (NT8 type shadowing) and CS8632 (nullable annotation context) -- all pre-existing, none introduced by BUG2.

**Full test suite:** `dotnet test` = **164 Passed, 359 Skipped, 0 Failed** (no regressions)

**Assessment:** PASS.

---

### SCAN-07: Functional verification (exit-path review)

**Command:** `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'TryMarkFollowerSlateNotReady'`

**Result:**

| Line | Context |
|---|---|
| 944 | Definition of `TryMarkFollowerSlateNotReady` |
| 1035 | `CancelQxBrackets(Account, Instrument)` 2-param -- after null guard |
| 1154 | `CancelQxBrackets(Account, Instrument, HashSet<Order>)` 3-param -- after null guard |
| 5536 | `FlattenOneAccount(Account, Instrument)` -- after IsAccountFlattenable guard |

**FindFollowerBracketOrder gate check:**

`Select-String -Pattern '_followerSlateReady.TryGetValue'` = line 4020 -- FIRST statement in method body (before ActiveOrders call)

**All 3 exit paths confirmed:**
- (a) `CancelQxBrackets` 2-param (line 1035): TryMarkFollowerSlateNotReady present PASS
- (b) `CancelQxBrackets` 3-param (line 1154): TryMarkFollowerSlateNotReady present PASS
- (c) `FlattenOneAccount` (line 5536): TryMarkFollowerSlateNotReady present PASS

**FindFollowerBracketOrder gate:**
- `TryGetValue` gate is FIRST check (line 4020), before `ActiveOrders(follower)` call (line 4023) PASS
- Gate semantics: absent=OPEN, false=CLOSED, true=OPEN PASS

**fo=NULL log entries during gate-closed window: EXPECTED and NOT a failure** (per ticket SCAN-07 pass criteria)

**Assessment:** PASS.

---

## 4. Deviations from Ticket

None. All 6 BUG2-REQ-N requirements implemented exactly as specified.

**One addition (non-scope violation):** An extra `[Fact]` test `FollowerSlateReady_AbsentKey_GateOpen_TryGetValueReturnsFalse` was added as a pure-BCL runnable variant of Test 1. This supplements (does not replace) the required 7 tests. It runs without NT8 and verifies the TryGetValue absent-key gate-open semantic directly. This is in the spirit of the ticket's test coverage requirements.

The 7 required test method names from the ticket specification are all present exactly as specified in Section 8.

---

## 5. Spec Requirement Coverage

| ID | Status |
|---|---|
| BUG2-REQ-1 | PASS -- `_followerSlateReady: ConcurrentDictionary<string,bool>(StringComparer.Ordinal)` added |
| BUG2-REQ-2 | PASS -- `TryMarkFollowerSlateNotReady` in all 3 exit paths |
| BUG2-REQ-3 | PASS -- `PositionUpdate += / -=` in Subscribe/Unsubscribe |
| BUG2-REQ-4 | PASS -- `OnFollowerPositionUpdate` + `TrySetFollowerSlateReady` + guards |
| BUG2-REQ-5 | PASS -- Gate in `FindFollowerBracketOrder(Account,...)` as first check |
| BUG2-REQ-6 | PASS -- Absent key = gate open (TryGetValue returns false for absent key) |

---

## 6. Self-Sign-Off Checklist

- [x] `_followerSlateReady` field added after `_drainOwnedOrderIds` with StringComparer.Ordinal
- [x] `TryMarkFollowerSlateNotReady` implemented -- CYC=2, no lock, no throw
- [x] `TrySetFollowerSlateReady` implemented -- CYC=4, no lock, no throw
- [x] `IsAccountFlat` implemented -- CYC=3, static, no lock, no throw
- [x] `HasAnyActiveOrders` implemented -- CYC=5, static, .ToList() snapshot, no lock
- [x] `OnFollowerPositionUpdate` implemented -- CYC=3, synchronous void, no Dispatcher
- [x] `FindFollowerBracketOrder(Account,...)` converted to block-body with gate -- CYC=2
- [x] `Subscribe()` updated -- PositionUpdate += OnFollowerPositionUpdate added
- [x] `Unsubscribe()` updated -- PositionUpdate -= OnFollowerPositionUpdate added (paired)
- [x] `CancelQxBrackets` 2-param: TryMarkFollowerSlateNotReady inserted after null guard
- [x] `CancelQxBrackets` 3-param: TryMarkFollowerSlateNotReady inserted after null guard
- [x] `FlattenOneAccount`: TryMarkFollowerSlateNotReady inserted after IsAccountFlattenable guard
- [x] All 7 xUnit [Fact] test names written in CopyEngineTests.cs
- [x] SCAN-01 PASS (zero actual lock() statements in new code)
- [x] SCAN-02 PASS (all 6 methods at or below stated CYC)
- [x] SCAN-03 PASS (zero async/await in new methods)
- [x] SCAN-04 PASS (zero non-ASCII bytes)
- [x] SCAN-05 PASS (no StrategyBase-only calls in new code)
- [x] SCAN-06 PASS (build succeeded, 0 errors, 164 tests pass, 0 regressions)
- [x] SCAN-07 PASS (all 3 exit paths confirmed, gate is first check in FindFollowerBracketOrder)

---

## BUILD_PASS
