# B194 Tickets

**Epic**: B194 -- Fix 4 confirmed NakedPositionDetector + DrainThenDispatch bugs
**Author**: ptt-architect (Phase 3)
**Date**: 2026-10-07
**Source plan**: docs/brain/B194/02-architecture-plan.md (REVIEW_PASS confirmed)
**Reviewer gate**: docs/brain/B194/02-plan-review.md (REVIEW_PASS -- zero violations)
**Test file**: src/PropTraderTools/Tests/B194Tests.cs (new, created by T1 engineer)
**Execute order**: T1 -> T2 -> T3 (sequential, single file)

---

## TICKET 1 -- F1 (TryNakedDetect PTT-Copy Guard) + F3 (DrainThenDispatch Submitted)

### 1. Ticket ID and Title

**T1**: Fix TryNakedDetect PTT-Copy fill false positive (F1) + Fix DrainThenDispatch Submitted exclusion (F3)

---

### 2. Spec Requirement IDs

| Bug ID | Priority | Root Cause | Fixed by |
|--------|----------|------------|----------|
| BUG-1 | P0 | PTT-Copy fill triggers NakedDetect before brackets arrive | F1 guard in TryNakedDetect |
| BUG-4 | P0 | Submitted orders excluded from entryCandidates drain filter | F3 LINQ predicate extension |

---

### 3. Scope Lock

**T1 SCOPE LOCK**: `TryNakedDetect` and `DrainThenDispatch` only. No other methods. No other files.

Any change outside these two methods in this ticket is a scope violation and must be reverted.

---

### 4. Method Signatures

Both signatures are **existing methods** -- signatures do not change. These are surgical inserts only.

```csharp
// Existing -- modified by F1 guard insertion
private void TryNakedDetect(OrderEventArgs e)

// Existing -- modified by F3 LINQ predicate extension
private void DrainThenDispatch(
    Account follower,
    Instrument instrument,
    int qty,
    double price,
    OrderAction action,
    OrderType orderType)
```

---

### 5. Exact Code Changes

#### F1 Change -- TryNakedDetect (src/PropTraderTools/CopyEngine.cs, ~line 6450)

**Current code at lines 6447-6461**:
```csharp
        // DW-NEW-08 Option E: thin dispatcher gate.
        // CYC=3: (1) terminal-state check, (2) follower-account check, (3) NakedPositionDetector call.
        // JS-021: no lock. JS-001: no throw. JS-033: synchronous void.
        private void TryNakedDetect(OrderEventArgs e)
        {
            if (
                e.Order.OrderState != OrderState.Filled
                && e.Order.OrderState != OrderState.Cancelled
                && e.Order.OrderState != OrderState.Rejected
            )
                return;
            if (!IsFollowerAccount(e.Order.Account))
                return;
            NakedPositionDetector(e.Order.Account);
        }
```

**After F1 -- update header comment and insert guard before existing terminal-state check**:
```csharp
        // DW-NEW-08 Option E: thin dispatcher gate.
        // CYC=4: (1) PTT-Copy fill guard (B194-F1), (2) terminal-state check,
        //        (3) follower-account check, (4) NakedPositionDetector call.
        // JS-021: no lock. JS-001: no throw. JS-033: synchronous void.
        private void TryNakedDetect(OrderEventArgs e)
        {
            // B194-F1: PTT-Copy fill guard -- follower fill has no brackets yet; brackets arrive
            // after leader Target1:Accepted. Never trigger NakedPositionDetector on own copy fill.
            // DW-B194-01: closed by this guard.
            if (e.Order.Name == "PTT-Copy" && e.Order.OrderState == OrderState.Filled)
                return;
            if (
                e.Order.OrderState != OrderState.Filled
                && e.Order.OrderState != OrderState.Cancelled
                && e.Order.OrderState != OrderState.Rejected
            )
                return;
            if (!IsFollowerAccount(e.Order.Account))
                return;
            NakedPositionDetector(e.Order.Account);
        }
```

**CYC post-F1**: 4 (pre: 3, +1 for PTT-Copy/Filled guard). JS-066 limit 8. PASS.

---

#### F3 Change -- DrainThenDispatch (src/PropTraderTools/CopyEngine.cs, ~line 6558)

**Current code at line 6558 (inside entryCandidates LINQ Where predicate)**:
```csharp
                    && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted)
```

**After F3 -- add Submitted to the OrderState filter**:
```csharp
                    // B194-F3: include Submitted -- PTT-Copy from completed drain-2 may be Submitted.
                    && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted
                        || o.OrderState == OrderState.Submitted)
```

Full entryCandidates predicate after change:
```csharp
            var entryCandidates = ActiveOrders(follower)
                .Where(o =>
                    o.Instrument == instrument
                    // B194-F3: include Submitted -- PTT-Copy from completed drain-2 may be Submitted.
                    && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted
                        || o.OrderState == OrderState.Submitted)
                    && (o.OrderType == OrderType.Limit || o.OrderType == OrderType.StopLimit)
                    && (
                        o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal) || o.Name == "Entry"
                    )
                )
                .ToList();
```

**CYC post-F3**: Unchanged. LINQ lambda predicates do not add McCabe branches to the enclosing method. JS-066 PASS.

---

### 6. JS Rule Constraints

| Rule | Requirement | T1 Compliance |
|------|-------------|---------------|
| JS-021 (P0) | No `lock()` anywhere | F1 guard reads `e.Order.Name` and `e.Order.OrderState` -- read-only property access, no lock. F3 is a LINQ predicate -- no state mutation, no lock. COMPLIANT. |
| JS-033 (P0) | No `async void` (non-event-handler) | Neither TryNakedDetect nor DrainThenDispatch are async. No new async methods. COMPLIANT. |
| JS-066 (P0) | CYC <= 8 per method | TryNakedDetect: CYC = 4 post-F1 (well below 8). DrainThenDispatch: CYC unchanged (3). COMPLIANT. |
| JS-001 (P0) | No `throw new XxxException` in hot paths | F1 guard uses `return`. F3 extends a LINQ predicate. Zero throw statements. COMPLIANT. |
| JS-081 | ASCII-only string literals | `"PTT-Copy"` is ASCII (0x50-0x54-0x54-0x2D-0x43-0x6F-0x70-0x79). No Unicode. COMPLIANT. |
| JS-082 | ASCII-only identifiers | No new identifiers introduced in T1. COMPLIANT. |
| DateTime.UtcNow only | No `DateTime.Now` | No DateTime references in T1 changes. COMPLIANT. |

**Grep verification commands** (engineer runs before VERIFY_PASS):
```powershell
# SCAN-01: zero lock() in modified methods
grep -n "lock(" src/PropTraderTools/CopyEngine.cs | grep -E "TryNakedDetect|DrainThenDispatch"

# SCAN-02: zero async void
grep -n "async void" src/PropTraderTools/CopyEngine.cs | grep -E "TryNakedDetect|DrainThenDispatch"

# SCAN-04: zero throw in modified methods
grep -n "throw " src/PropTraderTools/CopyEngine.cs | grep -E "TryNakedDetect|DrainThenDispatch"
```
All three must return zero matches.

---

### 7. xUnit [Fact] Test Names and Signatures

**File**: `src/PropTraderTools/Tests/B194Tests.cs` (create this file in T1 -- T2 and T3 append to it)

```csharp
// T1 Test 1 -- covers F1 (BUG-1)
[Fact]
public void TryNakedDetect_SkipsPTTCopyFill_NoNakedDetectorCalled()
```
**Asserts**: When an order has `Name == "PTT-Copy"` and `OrderState == Filled`, `TryNakedDetect`
returns immediately. `NakedPositionDetector` is NOT invoked (verified by confirming `StatusUpdate`
event is not fired with a "naked" or "flatten" message for the follower account).

**Mechanism**: Construct a stub `OrderEventArgs` with Name="PTT-Copy", State=Filled, on a
configured follower account. Subscribe to `StatusUpdate`. Call `TryNakedDetect` via reflection
or testable seam. Assert StatusUpdate count for flatten-related messages = 0.

```csharp
// T1 Test 2 -- covers F3 (BUG-4)
[Fact]
public void DrainThenDispatch_IncludesSubmittedInEntryCandidates()
```
**Asserts**: When a PTT-Copy Limit order is in `OrderState.Submitted` state on a follower account,
`DrainThenDispatch` includes it in `entryCandidates`, resulting in `follower.Cancel()` being called
for that order (not `SubmitEntryDirect`).

**Mechanism**: Supply a stub follower account with one PTT-Copy Limit order in Submitted state.
Call `DrainThenDispatch`. Assert `Cancel` was invoked (not `SubmitEntryDirect`). Verify
`_pendingDispatchDrains` contains the follower account key.

---

### 8. DW Card Assignments

| DW Card | Description | Status after T1 |
|---------|-------------|-----------------|
| DW-B194-01 | TryNakedDetect PTT-Copy fill false positive | CLOSED by F1 guard |
| DW-B194-03 | DrainThenDispatch Submitted order exclusion race | CLOSED by F3 LINQ extension |

---

### 9. 7-SCAN CHECKLIST (T1 Engineer Contract)

Every item must be verified before reporting COMPLETE on T1.

```
SCAN-01 [JS-021]: lock() grep
  Command: grep -n "lock(" src/PropTraderTools/CopyEngine.cs
  Requirement: zero matches in TryNakedDetect, zero matches in DrainThenDispatch
  Result: [ ] PASS  [ ] FAIL

SCAN-02 [JS-033]: async void grep
  Command: grep -n "async void" src/PropTraderTools/CopyEngine.cs
  Requirement: zero new async void methods (existing event-handlers exempt)
  Result: [ ] PASS  [ ] FAIL

SCAN-03 [JS-066]: CYC count
  TryNakedDetect post-F1: count if/foreach/while/case/&&/||/? -- must be <= 8
  Architect-verified: CYC = 4. Engineer must confirm branch count = 4.
  DrainThenDispatch: CYC unchanged at 3.
  Result: [ ] PASS  [ ] FAIL

SCAN-04 [JS-001]: throw new grep
  Command: grep -n "throw new" src/PropTraderTools/CopyEngine.cs
  Requirement: no new throw in TryNakedDetect or DrainThenDispatch
  Result: [ ] PASS  [ ] FAIL

SCAN-05 [JS-002]: return null grep
  Requirement: no new "return null" in TryNakedDetect or DrainThenDispatch
  ("PTT-Copy" string is ASCII-only -- no Unicode characters)
  Result: [ ] PASS  [ ] FAIL

SCAN-06 [ASCII-only / DateTime.UtcNow]:
  "PTT-Copy" string: verify all chars are 0x00-0x7F
  No DateTime.Now in new lines
  Result: [ ] PASS  [ ] FAIL

SCAN-07 [Test coverage]:
  TryNakedDetect_SkipsPTTCopyFill_NoNakedDetectorCalled -- covers F1 PTT-Copy guard branch
  DrainThenDispatch_IncludesSubmittedInEntryCandidates -- covers F3 Submitted branch
  Run: dotnet test --filter "TryNakedDetect_SkipsPTTCopyFill|DrainThenDispatch_IncludesSubmitted"
  Result: [ ] PASS  [ ] FAIL
```

---

### 10. Expected BUILD_PASS Criteria

- `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj` exits 0, zero warnings on changed lines
- `dotnet test --filter "B194"` (or specific test names) exits 0 with 2 tests passing
- `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` returns zero results in modified methods
- `src/PropTraderTools/CopyEngine.cs` compiles with NT8 NinjaTrader 8 references intact (existing build system)
- `powershell -File .\scripts\ptt-sync.ps1` exits 0 (hard-link sync after .cs change)

---
---

## TICKET 2 -- F2 (HasNakedPosition TriggerPending Fix + IsBracketStopOrder Helper)

### 1. Ticket ID and Title

**T2**: Fix HasNakedPosition TriggerPending stop recognition (F2) + extract IsBracketStopOrder helper

---

### 2. Spec Requirement IDs

| Bug ID | Priority | Root Cause | Fixed by |
|--------|----------|------------|----------|
| BUG-2 | P1 | TriggerPending+Market stops not recognized as bracket stops in HasNakedPosition | F2: extract IsBracketStopOrder helper; refactor inner loop |

---

### 3. Scope Lock

**T2 SCOPE LOCK**: `HasNakedPosition` inner loop refactor and new `IsBracketStopOrder` helper only. No other methods. No other files.

Any change outside `HasNakedPosition` and the new `IsBracketStopOrder` in this ticket is a scope violation and must be reverted.

---

### 4. Method Signatures

**New method** (insert at ~line 6499, immediately before `HasNakedPosition`):
```csharp
internal static bool IsBracketStopOrder(Order o)
```
- Visibility: `internal static` (NOT `private static` -- must be `internal` for xUnit test project access without NT8 runtime)
- Parameters: `Order o` (NT8 `NinjaTrader.Cbi.Order`)
- Returns: `bool` -- `true` if order is any recognized bracket stop type
- CYC: 3

**Existing method** (modified inner loop only):
```csharp
internal static bool HasNakedPosition(Account acct)
```
- Visibility: `internal static` (promoted from `private static` -- required for xUnit test project access via `InternalsVisibleTo`; same promotion already applied to `IsBracketStopOrder`)
- Inner `foreach (Order o in acct.Orders)` body is replaced
- CYC: 8 (pre) -> 7 (post)

**InternalsVisibleTo note**: If `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` is not already present in `CopyEngine.cs` at namespace level, engineer MUST add it. Check existing file before adding to avoid duplicate.

---

### 5. Exact Code Changes

#### F2 Change 1 -- Insert IsBracketStopOrder (src/PropTraderTools/CopyEngine.cs, ~line 6499)

**Insert this new method immediately before the `HasNakedPosition` method** (currently at line 6501, preceded by the comment block at lines 6496-6500):

```csharp
        // B194-F2: recognize all bracket stop patterns.
        // (1) NT8 SIM ATM stops: TriggerPending + Market + StopPrice>0.
        //     StopPrice>0 guard required -- real market entries also have TriggerPending+Market
        //     but StopPrice=0. This discriminates bracket stops from market entries.
        // (2) Live stops: Working|Submitted + StopMarket|StopLimit.
        // CYC=3: base(1) + TriggerPending-branch(1) + WorkingStop-branch(1).
        // JS-021: no lock. JS-001: no throw. NT8: o.StopPrice confirmed AddOnBase available
        //         via B194-DIRECT commit a434679a.
        // 500ms wrap-period note: no TickCount usage here.
        // DW-B194-02: closed by this helper.
        internal static bool IsBracketStopOrder(Order o)
        {
            if (o.OrderState == OrderState.TriggerPending
                && o.OrderType == OrderType.Market
                && o.StopPrice > 0)
                return true;
            if ((o.OrderState == OrderState.Working || o.OrderState == OrderState.Submitted)
                && (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit))
                return true;
            return false;
        }
```

---

#### F2 Change 2 -- Replace HasNakedPosition inner loop (src/PropTraderTools/CopyEngine.cs, ~line 6517-6527)

**Update header comment and replace the `foreach (Order o in acct.Orders)` body**:

**Current header comment at lines 6496-6500**:
```csharp
        // DW-NEW-08 Option E: naked position check.
        // Returns true if acct has a non-flat position AND zero Working/Submitted Stop or Target.
        // CYC<=8 (see analysis: 2 foreach loops + 5 decision points).
        // JS-021: no lock. JS-002: returns bool. JS-001: no throw.
        // NT8: OrderState.Submitted = pre-Working (Accepted / partially acknowledged).
```

**Replace header comment with**:
```csharp
        // DW-NEW-08 Option E: naked position check.
        // Returns true if acct has a non-flat position AND zero bracket stops or targets.
        // B194-F2: inner loop refactored to use IsBracketStopOrder(o) helper.
        //   Recognizes TriggerPending+Market+StopPrice>0 (NT8 SIM ATM stops) as bracket stops.
        //   CYC=7 post-F2 (was 8): base=1, foreach-pos=+1, qty=+1, no-pos=+1,
        //                           foreach-ord=+1, IsBracketStopOrder=+1, Limit-check=+1.
        // JS-021: no lock. JS-002: returns bool. JS-001: no throw.
        // NT8: OrderState.Submitted = pre-Working (Accepted / partially acknowledged).
```

**Current `foreach (Order o in acct.Orders)` body at lines 6519-6524**:
```csharp
                if (o.OrderState != OrderState.Working && o.OrderState != OrderState.Submitted)
                    continue;
                if (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit)
                    hasStop = true;
                else if (o.OrderType == OrderType.Limit)
                    hasTarget = true;
```

**Replace with** (inside the same `foreach (Order o in acct.Orders)` block):
```csharp
                // B194-F2: IsBracketStopOrder recognizes TriggerPending+Market+StopPrice>0
                //          (NT8 SIM ATM stops) in addition to Working|Submitted StopMarket|StopLimit.
                if (IsBracketStopOrder(o)) { hasStop = true; continue; }
                if ((o.OrderState == OrderState.Working || o.OrderState == OrderState.Submitted)
                    && o.OrderType == OrderType.Limit)
                    hasTarget = true;
```

**Full HasNakedPosition method after F2**:
```csharp
        // DW-NEW-08 Option E: naked position check.
        // Returns true if acct has a non-flat position AND zero bracket stops or targets.
        // B194-F2: inner loop refactored to use IsBracketStopOrder(o) helper.
        //   Recognizes TriggerPending+Market+StopPrice>0 (NT8 SIM ATM stops) as bracket stops.
        //   CYC=7 post-F2 (was 8): base=1, foreach-pos=+1, qty=+1, no-pos=+1,
        //                           foreach-ord=+1, IsBracketStopOrder=+1, Limit-check=+1.
        // JS-021: no lock. JS-002: returns bool. JS-001: no throw.
        // NT8: OrderState.Submitted = pre-Working (Accepted / partially acknowledged).
        internal static bool HasNakedPosition(Account acct)
        {
            bool hasPosition = false;
            foreach (Position p in acct.Positions)
            {
                if (p.Quantity > 0)
                {
                    hasPosition = true;
                    break;
                }
            }
            if (!hasPosition)
                return false;

            bool hasStop = false;
            bool hasTarget = false;
            foreach (Order o in acct.Orders)
            {
                // B194-F2: IsBracketStopOrder recognizes TriggerPending+Market+StopPrice>0
                //          (NT8 SIM ATM stops) in addition to Working|Submitted StopMarket|StopLimit.
                if (IsBracketStopOrder(o)) { hasStop = true; continue; }
                if ((o.OrderState == OrderState.Working || o.OrderState == OrderState.Submitted)
                    && o.OrderType == OrderType.Limit)
                    hasTarget = true;
            }
            return !hasStop && !hasTarget;
        }
```

**CYC post-F2**:
- `HasNakedPosition`: 7 (was 8). JS-066 limit 8. PASS.
- `IsBracketStopOrder`: 3. JS-066 limit 8. PASS.

---

### 6. JS Rule Constraints

| Rule | Requirement | T2 Compliance |
|------|-------------|---------------|
| JS-021 (P0) | No `lock()` anywhere | `IsBracketStopOrder` is a pure predicate; reads `o.OrderState`, `o.OrderType`, `o.StopPrice` -- all read-only, no lock. `HasNakedPosition` modified body: no lock. COMPLIANT. |
| JS-033 (P0) | No `async void` (non-event-handler) | `IsBracketStopOrder` is `internal static bool`. `HasNakedPosition` is `internal static bool`. No async in either. COMPLIANT. |
| JS-066 (P0) | CYC <= 8 per method | `HasNakedPosition` CYC = 7 post-F2. `IsBracketStopOrder` CYC = 3. Both well below 8. The helper extraction was architecturally REQUIRED because adding TriggerPending recognition directly would have pushed `HasNakedPosition` to CYC = 9 (violation). COMPLIANT. |
| JS-001 (P0) | No `throw new XxxException` in hot paths | `IsBracketStopOrder` uses only `return true` / `return false`. No throw. `HasNakedPosition` inner loop uses only assignment and `continue`. No throw. COMPLIANT. |
| JS-081 | ASCII-only string literals | No new string literals in either method. COMPLIANT. |
| JS-082 | ASCII-only identifiers | `IsBracketStopOrder` identifier: ASCII. `hasStop`, `hasTarget`: existing ASCII identifiers. COMPLIANT. |
| DateTime.UtcNow only | No `DateTime.Now` | No DateTime references in T2 changes. COMPLIANT. |

**Grep verification commands** (engineer runs before VERIFY_PASS):
```powershell
# SCAN-01: zero lock() in modified/new methods
grep -n "lock(" src/PropTraderTools/CopyEngine.cs | grep -E "IsBracketStopOrder|HasNakedPosition"

# SCAN-02: zero async void
grep -n "async void" src/PropTraderTools/CopyEngine.cs

# SCAN-04: zero throw in modified/new methods
grep -n "throw " src/PropTraderTools/CopyEngine.cs | grep -E "IsBracketStopOrder|HasNakedPosition"
```
All three must return zero matches.

---

### 7. xUnit [Fact] Test Names and Signatures

**File**: `src/PropTraderTools/Tests/B194Tests.cs` (appended to -- T1 creates the file)

```csharp
// T2 Test 1 -- covers BUG-2 end-to-end (TriggerPending stop prevents flatten)
[Fact]
public void HasNakedPosition_ReturnsFalse_WhenOnlyTriggerPendingStopExists()
```
**Asserts**: An account with a non-zero position AND one `TriggerPending` + `Market` + `StopPrice > 0`
order causes `HasNakedPosition` to return `false` (bracket stop recognized; account is NOT naked).

**Mechanism**: Stub `Account` with `Position(qty=1)` + `Order(TriggerPending, Market, StopPrice=4100)`.
Call `CopyEngine.HasNakedPosition(stubAcct)` directly (internal visibility via
`InternalsVisibleTo`). Assert result == `false`.

```csharp
// T2 Test 2 -- covers IsBracketStopOrder TriggerPending+Market+StopPrice>0 path
[Fact]
public void IsBracketStopOrder_ReturnsTrue_ForTriggerPendingMarketWithStopPrice()
```
**Asserts**: `IsBracketStopOrder` returns `true` for an order with `State=TriggerPending`,
`Type=Market`, `StopPrice=4100.0` (StopPrice > 0).

**Mechanism**: Direct static call `CopyEngine.IsBracketStopOrder(order)` with stub order.
Assert result == `true`.

```csharp
// T2 Test 3 -- covers IsBracketStopOrder Working StopMarket path (existing behavior preserved)
[Fact]
public void IsBracketStopOrder_ReturnsTrue_ForWorkingStopMarket()
```
**Asserts**: `IsBracketStopOrder` returns `true` for an order with `State=Working`, `Type=StopMarket`.

**Mechanism**: Direct static call. Assert result == `true`.

```csharp
// T2 Test 4 -- confirms Limit orders are NOT misclassified as stops
[Fact]
public void IsBracketStopOrder_ReturnsFalse_ForWorkingLimitOrder()
```
**Asserts**: `IsBracketStopOrder` returns `false` for an order with `State=Working`, `Type=Limit`.
This guards against regression where target orders would be misclassified as bracket stops,
causing `hasStop = true` prematurely.

**Mechanism**: Direct static call. Assert result == `false`.

---

### 8. DW Card Assignments

| DW Card | Description | Status after T2 |
|---------|-------------|-----------------|
| DW-B194-02 | HasNakedPosition TriggerPending stop recognition blind spot | CLOSED by F2 helper extraction + inner loop refactor |

---

### 9. 7-SCAN CHECKLIST (T2 Engineer Contract)

Every item must be verified before reporting COMPLETE on T2.

```
SCAN-01 [JS-021]: lock() grep
  Command: grep -n "lock(" src/PropTraderTools/CopyEngine.cs
  Requirement: zero new lock() in IsBracketStopOrder or HasNakedPosition
  Result: [ ] PASS  [ ] FAIL

SCAN-02 [JS-033]: async void grep
  Command: grep -n "async void" src/PropTraderTools/CopyEngine.cs
  Requirement: zero async void in IsBracketStopOrder or HasNakedPosition
  Result: [ ] PASS  [ ] FAIL

SCAN-03 [JS-066]: CYC count
  IsBracketStopOrder: count if/return/&&/|| -- must be <= 8.
    Architect-verified: CYC = 3. Engineer must confirm branch count = 3.
    (base=1, TriggerPending-if=+1, WorkingStop-if=+1)
  HasNakedPosition: count all if/foreach/continue -- must be <= 8.
    Architect-verified: CYC = 7 post-F2. Engineer must confirm branch count = 7.
    (base=1, foreach-pos=+1, qty-check=+1, no-pos-return=+1, foreach-ord=+1,
     IsBracketStopOrder-if=+1, Limit-if=+1)
  Result: [ ] PASS  [ ] FAIL

SCAN-04 [JS-001]: throw new grep
  Command: grep -n "throw new" src/PropTraderTools/CopyEngine.cs
  Requirement: no new throw in IsBracketStopOrder or HasNakedPosition
  Result: [ ] PASS  [ ] FAIL

SCAN-05 [JS-002]: return null grep
  Requirement: no new "return null" in IsBracketStopOrder or HasNakedPosition
  (IsBracketStopOrder returns bool -- cannot return null; HasNakedPosition returns bool)
  Result: [ ] PASS  [ ] FAIL

SCAN-06 [ASCII-only / DateTime.UtcNow]:
  Identifier "IsBracketStopOrder": verify all chars 0x00-0x7F (all letters + digits)
  No DateTime.Now in new lines
  Result: [ ] PASS  [ ] FAIL

SCAN-07 [Test coverage]:
  HasNakedPosition_ReturnsFalse_WhenOnlyTriggerPendingStopExists -- covers BUG-2 end-to-end
  IsBracketStopOrder_ReturnsTrue_ForTriggerPendingMarketWithStopPrice -- covers branch 1 of helper
  IsBracketStopOrder_ReturnsTrue_ForWorkingStopMarket -- covers branch 2 of helper
  IsBracketStopOrder_ReturnsFalse_ForWorkingLimitOrder -- covers false path (anti-regression)
  Run: dotnet test --filter "HasNakedPosition_ReturnsFalse|IsBracketStopOrder"
  Result: [ ] PASS  [ ] FAIL
```

---

### 10. Expected BUILD_PASS Criteria

- `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj` exits 0, zero warnings on changed lines
- `dotnet test --filter "B194"` exits 0 with 6 tests passing (2 from T1 + 4 from T2)
- `IsBracketStopOrder` is visible from test project via `InternalsVisibleTo` (no runtime required)
- `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` returns zero results in modified methods
- `powershell -File .\scripts\ptt-sync.ps1` exits 0

---
---

## TICKET 3 -- F4 (FlattenOneAccount Belt-and-Suspenders Debounce)

### 1. Ticket ID and Title

**T3**: FlattenOneAccount belt-and-suspenders debounce + `_flattenFilledTicks` field (F4)

---

### 2. Spec Requirement IDs

| Bug ID | Priority | Root Cause | Fixed by |
|--------|----------|------------|----------|
| BUG-3 | P0 (residual path) | PTT-Flatten fill re-triggers TryNakedDetect; F1 guard (T1) covers PTT-Copy fills but PTT-Flatten fills pass the guard; can create a flatten loop at intervals > 500ms | F4: debounce at FlattenOneAccount submission layer (defense-in-depth) |

**Note on BUG-3 fix layering**: F1 (T1) eliminates the primary path (PTT-Copy fill triggering NakedDetect). F4 (T3) closes the residual path where a PTT-Flatten fill itself re-triggers NakedDetect. Both are required for complete BUG-3 closure.

---

### 3. Scope Lock

**T3 SCOPE LOCK**: `FlattenOneAccount` debounce insertions and `_flattenFilledTicks` field declaration only. No other methods. No other files.

Any change outside `FlattenOneAccount` and the `_flattenFilledTicks` field in this ticket is a scope violation and must be reverted.

---

### 4. Method Signatures

**New field** (insert after `_nakedDetectLastQueuedTicks` at ~line 374):
```csharp
private readonly ConcurrentDictionary<string, long> _flattenFilledTicks
    = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
```

**Existing method** (two insertions inside the body):
```csharp
private void FlattenOneAccount(Account acc, Instrument instrument)
```
- Signature unchanged
- CYC: 6 (pre) -> 7 (post, +1 debounce if-check). JS-066 limit 8. PASS.

---

### 5. Exact Code Changes

#### F4 Change 1 -- New field declaration (src/PropTraderTools/CopyEngine.cs, ~line 374)

**Current code at lines 373-380** (field insertion site):
```csharp
        private readonly ConcurrentDictionary<string, long> _nakedDetectLastQueuedTicks =
            new ConcurrentDictionary<string, long>(StringComparer.Ordinal);

        // DW-NEW-08 Option D: cancel-before-dispatch drain state.
        // Key = follower account name. ConcurrentDictionary: no lock (JS-021).
        // StringComparer.Ordinal: deterministic key comparison.
        private readonly ConcurrentDictionary<string, PendingDispatchDrain> _pendingDispatchDrains =
            new ConcurrentDictionary<string, PendingDispatchDrain>(StringComparer.Ordinal);
```

**Insert new field after `_nakedDetectLastQueuedTicks` line 374, before the `_pendingDispatchDrains` comment**:
```csharp
        // B194-F4: belt-and-suspenders flatten debounce.
        // Stores (long)Environment.TickCount at last acc.Submit(PTT-Flatten) per account name.
        // Prevents PTT-Flatten fill -> NakedDetect -> FlattenOneAccount re-entry loop.
        // 500ms grace window is far shorter than TickCount wrap period (~25d) -- safe.
        // ConcurrentDictionary: no lock (JS-021). StringComparer.Ordinal: deterministic key compare.
        // DW-B194-04: closed by this field + FlattenOneAccount debounce guard.
        private readonly ConcurrentDictionary<string, long> _flattenFilledTicks =
            new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
```

---

#### F4 Change 2 -- Debounce check in FlattenOneAccount (src/PropTraderTools/CopyEngine.cs, ~line 5029)

**Insertion point**: After the existing in-flight scan `foreach` loop (current lines 5014-5029, ending with the closing `}` of the foreach), BEFORE `var pos = FindPosition(acc, instrument)` (currently line 5030).

**Update method header comment** at lines 5000-5003:
```csharp
        // CYC=7: (1) active PTT-Flatten guard, (2) B194-F4 debounce guard,
        //        (3) pre-cancel pos null/qty guard, (4) CancelAllAccountOrders,
        //        (5) post-cancel re-read null/qty guard, (6) action ternary, (7) try/catch.
        // JS-021: no lock. JS-001: no throw in hot path. JS-002: void.
```

**Insert after line 5029** (the closing `}` of the in-flight scan foreach), before `var pos = ...`:
```csharp
            // B194-F4: belt-and-suspenders debounce. Skip if flatten was submitted within 500ms.
            // Prevents PTT-Flatten fill -> NakedDetect -> FlattenOneAccount re-entry loop.
            // Pattern matches _nakedDetectLastQueuedTicks at NakedPositionDetector line 6479.
            long flattenNow = (long)(int)Environment.TickCount;
            if (flattenNow - _flattenFilledTicks.GetOrAdd(acc.Name, 0L) < 500L)
            {
                StatusUpdate?.Invoke(acc.Name + ": flat-debounce skip");
                return;
            }
```

---

#### F4 Change 3 -- Timestamp recording in FlattenOneAccount (src/PropTraderTools/CopyEngine.cs, ~line 5066)

**Insertion point**: Inside the `try` block, after the `if (order != null)` null check, BEFORE `acc.Submit(new[] { order })` (currently line 5067). `flattenNow` is in scope (declared in F4 Change 2 above in the same method body).

**Current lines 5066-5068**:
```csharp
                if (order != null)
                    acc.Submit(new[] { order });
                StatusUpdate?.Invoke(acc.Name + ": flatten " + posAfterCancel.Quantity);
```

**After F4 Change 3**:
```csharp
                if (order != null)
                {
                    // B194-F4: record timestamp at submission (not at fill) so the 500ms lockout
                    // begins from the moment flatten is dispatched to NT8.
                    _flattenFilledTicks.AddOrUpdate(acc.Name, flattenNow, (_, __) => flattenNow);
                    acc.Submit(new[] { order });
                }
                StatusUpdate?.Invoke(acc.Name + ": flatten " + posAfterCancel.Quantity);
```

**CYC post-F4**: 7 (was 6; +1 for debounce if-check in F4 Change 2). JS-066 limit 8. PASS.

**Note on TickCount cast**: `(long)(int)Environment.TickCount` matches the existing pattern at line 6479 exactly. The cast to `int` first (before widening to `long`) preserves the ~25-day wrap behavior. This is intentional and documented. The 500ms window is far shorter than the wrap period, making the wrap-around safe for this use case.

---

### 6. JS Rule Constraints

| Rule | Requirement | T3 Compliance |
|------|-------------|---------------|
| JS-021 (P0) | No `lock()` anywhere | `_flattenFilledTicks` uses `ConcurrentDictionary.GetOrAdd` (value literal `0L`, no delegate, no closure allocation) and `AddOrUpdate`. Both are lock-free atomic operations. Zero `lock()` statements. COMPLIANT. |
| JS-033 (P0) | No `async void` (non-event-handler) | `FlattenOneAccount` is a synchronous `void` method (not async). No change to signature. COMPLIANT. |
| JS-066 (P0) | CYC <= 8 per method | `FlattenOneAccount` CYC = 7 post-F4. Well below 8. COMPLIANT. |
| JS-001 (P0) | No `throw new XxxException` in hot paths | F4 Change 2 uses `return`. F4 Change 3 uses `AddOrUpdate`. No throw. COMPLIANT. |
| JS-081 | ASCII-only string literals | `"flat-debounce skip"` is ASCII (0x66-0x6C-0x61-0x74-0x2D-0x64-0x65-0x62-0x6F-0x75-0x6E-0x63-0x65-0x20-0x73-0x6B-0x69-0x70). No Unicode. COMPLIANT. |
| JS-082 | ASCII-only identifiers | `_flattenFilledTicks`, `flattenNow` are ASCII. COMPLIANT. |
| DateTime.UtcNow only | No `DateTime.Now` | Debounce uses `Environment.TickCount` (not `DateTime`). No `DateTime.Now` references. COMPLIANT. |

**Grep verification commands** (engineer runs before VERIFY_PASS):
```powershell
# SCAN-01: zero lock() in modified method
grep -n "lock(" src/PropTraderTools/CopyEngine.cs | grep "FlattenOneAccount"

# SCAN-02: zero async void
grep -n "async void" src/PropTraderTools/CopyEngine.cs

# SCAN-04: zero throw in modified method
grep -n "throw new" src/PropTraderTools/CopyEngine.cs | grep "FlattenOneAccount"

# SCAN-06: no DateTime.Now in new lines
grep -n "DateTime\.Now[^U]" src/PropTraderTools/CopyEngine.cs
```
All must return zero matches.

---

### 7. xUnit [Fact] Test Names and Signatures

**File**: `src/PropTraderTools/Tests/B194Tests.cs` (appended to -- T1 created file, T2 appended)

```csharp
// T3 Test 1 -- covers F4 debounce logic (BUG-3 residual path)
[Fact]
public void FlattenOneAccount_SkipsWhenFlattenWithin500ms()
```
**Asserts**:
1. First call to `FlattenOneAccount` for an account with an open position submits a flatten order (`acc.Submit` call count = 1).
2. Second call to `FlattenOneAccount` for the same account within 500ms emits a `StatusUpdate` event containing `"flat-debounce skip"` and does NOT call `acc.Submit` a second time (total call count remains 1).

**Mechanism**:
- Construct a stub `Account` with `Name="TestAcct"` and `Position(qty=1, Long)`.
- Subscribe to `StatusUpdate` event; capture messages in a list.
- Call `FlattenOneAccount` once -- assert `acc.Submit` called once.
- Call `FlattenOneAccount` a second time immediately (< 500ms elapsed).
- Assert `StatusUpdate` list contains a message with `"flat-debounce skip"`.
- Assert `acc.Submit` call count is still 1 (second call was suppressed).

**Supplementary test** (optional but recommended -- engineer discretion):
```csharp
[Fact]
public void FlattenOneAccount_SubmitsAfterDebounceExpired()
```
**Asserts**: After 500ms has elapsed since the first flatten, a second call to `FlattenOneAccount`
submits again (call count = 2). Tests the expiry path of the debounce.

**Mechanism**: Manually inject a past timestamp into `_flattenFilledTicks` (e.g., set
`_flattenFilledTicks[acc.Name] = (long)(int)Environment.TickCount - 600L`) before the second call.
Assert `acc.Submit` call count = 2.

---

### 8. DW Card Assignments

| DW Card | Description | Status after T3 |
|---------|-------------|-----------------|
| DW-B194-04 | FlattenOneAccount loop belt-and-suspenders debounce | CLOSED by F4 field + debounce guard + timestamp recording |

**BUG-3 complete closure status after T1 + T3**:
- Primary path (PTT-Copy fill -> NakedDetect): CLOSED by T1/F1
- Residual path (PTT-Flatten fill -> NakedDetect re-entry): CLOSED by T3/F4

---

### 9. 7-SCAN CHECKLIST (T3 Engineer Contract)

Every item must be verified before reporting COMPLETE on T3.

```
SCAN-01 [JS-021]: lock() grep
  Command: grep -n "lock(" src/PropTraderTools/CopyEngine.cs
  Requirement: zero new lock() in FlattenOneAccount or near _flattenFilledTicks field
  _flattenFilledTicks must use ONLY GetOrAdd/AddOrUpdate -- no lock() wrapper
  Result: [ ] PASS  [ ] FAIL

SCAN-02 [JS-033]: async void grep
  Command: grep -n "async void" src/PropTraderTools/CopyEngine.cs
  Requirement: FlattenOneAccount remains synchronous void; no new async void anywhere
  Result: [ ] PASS  [ ] FAIL

SCAN-03 [JS-066]: CYC count
  FlattenOneAccount post-F4: count all if/foreach/while/try/ternary -- must be <= 8.
    Architect-verified: CYC = 7 post-F4. Engineer must confirm branch count = 7.
    (1) active PTT-Flatten guard foreach, (2) debounce if (NEW), (3) pre-cancel pos guard,
    (4) post-cancel pos guard, (5) action ternary, (6) if (order != null), (7) try/catch
  Result: [ ] PASS  [ ] FAIL

SCAN-04 [JS-001]: throw new grep
  Command: grep -n "throw new" src/PropTraderTools/CopyEngine.cs
  Requirement: no new throw in F4 changes (debounce check and timestamp record)
  Result: [ ] PASS  [ ] FAIL

SCAN-05 [JS-002]: return null grep
  Requirement: no new "return null" in FlattenOneAccount
  (all new returns are bare "return;" -- void method)
  Result: [ ] PASS  [ ] FAIL

SCAN-06 [ASCII-only / DateTime.UtcNow]:
  "flat-debounce skip" string: verify all chars 0x00-0x7F
  "_flattenFilledTicks" identifier: verify all chars 0x00-0x7F
  "flattenNow" identifier: verify all chars 0x00-0x7F
  No DateTime.Now in new lines: grep -n "DateTime\.Now[^U]" src/PropTraderTools/CopyEngine.cs
  Result: [ ] PASS  [ ] FAIL

SCAN-07 [Test coverage]:
  FlattenOneAccount_SkipsWhenFlattenWithin500ms -- covers debounce guard (new branch added by F4)
  Run: dotnet test --filter "FlattenOneAccount_SkipsWhenFlattenWithin500ms"
  Result: [ ] PASS  [ ] FAIL
```

---

### 10. Expected BUILD_PASS Criteria

- `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj` exits 0, zero warnings on changed lines
- `dotnet test --filter "B194"` exits 0 with 7 tests passing (2 from T1 + 4 from T2 + 1 from T3)
- `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` returns zero results in `FlattenOneAccount` or near `_flattenFilledTicks`
- `grep -n "DateTime\.Now[^U]" src/PropTraderTools/CopyEngine.cs` returns zero results
- `powershell -File .\scripts\ptt-sync.ps1` exits 0 (hard-link sync after .cs change)

---

## Summary Table

| Ticket | Bug(s) Fixed | Methods Changed | New Symbols | DW Cards | Tests |
|--------|-------------|-----------------|-------------|----------|-------|
| T1 | BUG-1 (P0), BUG-4 (P0) | TryNakedDetect, DrainThenDispatch | none | DW-B194-01, DW-B194-03 | 2 [Fact] |
| T2 | BUG-2 (P1) | HasNakedPosition (refactor) | IsBracketStopOrder (internal static) | DW-B194-02 | 4 [Fact] |
| T3 | BUG-3 (P0 residual) | FlattenOneAccount | _flattenFilledTicks (field) | DW-B194-04 | 1 [Fact] |
| **Total** | **4 bugs** | **4 methods** | **1 method + 1 field** | **4 DW cards** | **7 [Fact]** |

## Carry-Forward DW Cards (NOT closed by B194)

These open cards are inherited from prior epics and are NOT addressed in B194:

| DW Card | Description | Blocks |
|---------|-------------|--------|
| DW-B183-03 | IsLaneEEligible CYC=8 frozen -- any branch addition requires helper extraction first | Future Lane E modification |
| DW-B192-SINST-01 | SetInstrument() CYC=8 ceiling in TradeCopierPanel.cs | Future SetInstrument() modification |
| DW-B193-01 | OnQuickAllBufferChanged old label strings "Quick ALL"/"QAll2t" | B195+ when method is next touched |

---

*Written by: ptt-architect*
*Epic: B194*
*Phase: 3 (Ticket Generation)*
*Source plan: docs/brain/B194/02-architecture-plan.md (REVIEW_PASS)*
*Reviewer confirmation: docs/brain/B194/02-plan-review.md (REVIEW_PASS -- zero violations)*
