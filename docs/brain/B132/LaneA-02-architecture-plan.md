# B132 LaneA -- Architecture Plan

**Status**: REVIEW_PENDING
**Epic**: B132 LaneA
**Phase**: 1 -- Architecture Plan
**Architect**: ptt-architect
**Date**: 2026-08-31
**Defect**: DW-B132-LaneA (missing PTT-STP-Drag replacement stop after OCO group-cancel)

---

## Section A: Root Cause and NT8 API Facts

### Root Cause: OCO Group-Cancel Destroys Follower Stop on Every Target Drag

When `SyncAtmFollowerTarget` executes Block A for a follower account:

```csharp
acc.Cancel(new Order[] { fo }); // Block A -- cancel follower's ATM target bracket
```

`fo` is the follower's ATM bracket target order (e.g. `"Target3"`). NT8's ATM engine manages
stop and target brackets as an OCO (One-Cancels-Other) group. When `acc.Cancel()` is called
on ANY OCO group member, NT8 automatically cancels ALL orders in that group.

**Consequence**: Cancelling `"Target3"` also cancels `"Stop3"` (and potentially all OCO-linked
stops for that ATM strategy). After Block A fires:
- The follower's `"Target3"` is cancelled (intended).
- The follower's `"Stop3"` is cancelled (unintended side effect).
- Block B places a new `"PTT-TGT-Drag"` replacement target (correct).
- **[MISSING]** No replacement stop is placed.
- The follower account is left in an open position with NO stop protection.

### Confirmed NT8 API Facts (with source citations)

| Fact | Status | Source |
|------|--------|--------|
| `Account.Change()` is a silent no-op on ATM-owned brackets | CONFIRMED (B129 SIM, do not re-test) | `CopyEngine.cs` L2190-2192 comment; project rules (AGENTS.md) |
| `Account.Cancel()` on an OCO-linked order cancels the FULL OCO group | CONFIRMED (B131 SIM session 2) | Director SIM observation; DW-B134-OCO resolution |
| `Account.CreateOrder()` + `Submit()` is available in `AddOnBase` context | CONFIRMED | `NT8_FULL_REFERENCE.md` L2106-2121; `NT8_ADDON_KNOWLEDGE.md` L219-223; production usage in `SyncAtmFollowerBracket` L2222-2241 |
| ATM bracket order names follow `"Stop1"` / `"Stop2"` / `"Stop3"` pattern | CONFIRMED known fact | `IsAtmSTPOrder` predicate L2107-2113; B131 LaneA diagnosis |
| Leader bracket index derivable from order name suffix: `"Target3"` -> N=3, look up `"Stop3"` | CONFIRMED by design | `IsAtmSTPOrder` checks `StartsWith("Target")`; B131 LaneA test session confirmed `"Target3"` suffix = index 3 |
| OCO parameter for PTT-placed standalone orders must be `""` (empty) | CONFIRMED | `NT8_FULL_REFERENCE.md` L2118; B131 LaneA tickets L36 |

### Why `Account.Change()` Is Not the Answer

`Account.Change()` is a silent no-op on ATM-engine-owned brackets (confirmed B129 SIM gate).
The correct AddOn pattern for ATM bracket price changes is: `Cancel() + CreateOrder() + Submit()`.
This is already the established pattern for both `SyncAtmFollowerBracket` (stop drag) and
`SyncAtmFollowerTarget` (target drag). Phase C uses the same pattern for the replacement stop.

---

## Section B: Fix Strategy

### Phase C: Replacement Stop After Each Block B Target Placement

**Location**: In `SyncAtmFollowerTarget`, immediately after Block B's try/catch block.

**Flow** (complete method after B132 LaneA fix):

```
[Guard] acc == null                 -> return
[Guard] fo == null                  -> return
[Block A-Prime]  foreach sweep: cancel Working PTT-TGT-Drag orders (DW-B139, B131 LaneB -- UNCHANGED)
[Block A]        acc.Cancel(fo)     -- cancels follower's ATM target; OCO side-effect kills Stop{N} too
[Block B]        acc.CreateOrder + Submit  -- new PTT-TGT-Drag at newPrice
[Phase C -- NEW]:
    n = DeriveLeaderBracketIndex(leaderOrder)
    stopPrice = FindLeaderStopPrice(leaderOrder?.Account, n)
    CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stopPrice)
```

### Phase C Details

**C1 -- Derive bracket index N**:
`DeriveLeaderBracketIndex(leaderOrder)` parses the integer suffix from `leaderOrder.Name`.
- `"Target3"` -> N=3
- `"Target1"` -> N=1
- Unparseable / null -> N=0 (graceful skip)

**C2 -- Find leader's working stop price**:
`FindLeaderStopPrice(leaderOrder.Account, n)` scans the leader account's orders for a Working
order named `"Stop{N}"` and returns `order.StopPrice`. Returns `0.0` if not found.
- `leaderOrder.Account` is the leader's `Account` object (available via the 4th param added to `SyncAtmFollowerTarget`)
- Uses `ToList()` snapshot for safe iteration (same pattern as Block A-Prime)

**C3 -- Place PTT-STP-Drag replacement stop on follower account**:
`CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stopPrice)`:
- Guard: if `stopPrice <= 0.0`, log and return (graceful skip, no NT8 call).
- Calls `acc.CreateOrder(instr, stopAction, OrderType.StopMarket, OrderEntry.Automated, TimeInForce.Day, qty, 0, stopPrice, "", "PTT-STP-Drag", NinjaTrader.Core.Globals.MaxDate, (NinjaTrader.Cbi.CustomOrder)null)`
- Null-checks the returned order.
- Calls `acc.Submit(new[] { newStop })`.
- Logs: `acc.Name + ": PTT-STP-Drag placed bracket N=" + bracketIndex + " @ " + stopPrice`
- Wrapped in try/catch (no rethrow -- JS-001).

**Stop OrderAction**: `fo.OrderAction` is used directly. For a long position:
- `fo` (target) has `OrderAction.Sell` (close long at profit target)
- Stop must also be `OrderAction.Sell` (close long if price drops to stop level)
- This is identical to `SyncAtmFollowerBracket` L2224 which uses `fo.OrderAction` directly.

**No OCO linking**: The `oco` parameter is `""` (empty string). PTT-STP-Drag is a standalone order, not part of any NT8 ATM OCO group. This is intentional and consistent with the existing `SyncAtmFollowerBracket` convention (L2232: `"PTT-STP-Drag"`).

### Signature Change: SyncAtmFollowerTarget

**Before** (current):
```csharp
private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice)
```

**After** (B132 LaneA):
```csharp
private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice, Order? leaderOrder)
```

**Call site change** (SyncFollowerBracket L2158, one location only):
```csharp
// Before:
SyncAtmFollowerTarget(acc, fo, newPrice);

// After:
SyncAtmFollowerTarget(acc, fo, newPrice, leaderOrder);
```

`leaderOrder` is already in scope at the call site (it is a parameter of `SyncFollowerBracket`).
If `leaderOrder` is `null`, Phase C gracefully skips (DeriveLeaderBracketIndex returns 0,
FindLeaderStopPrice returns 0.0, CreateFollowerReplacementStop skips on `stopPrice <= 0.0`).

---

## Section C: Method Signatures and CYC Annotations

### Modified Methods

#### `SyncAtmFollowerTarget` (modified -- CopyEngine.cs)

```csharp
private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice, Order? leaderOrder)
```

| Item | Value |
|------|-------|
| CYC target | <= 8 |
| CYC result | 8 (UNCHANGED -- Phase C adds 3 unconditional method calls = 0 new branches) |
| Role | Cancel+resubmit for ATM-owned target bracket; Phase C adds PTT-STP-Drag replacement stop |
| Change | Add 4th param `Order? leaderOrder`; add 3-line Phase C section after Block B |

**CYC branch accounting**:
| # | Branch | Code |
|---|--------|------|
| 1 | `if (acc == null)` | L2265 |
| 2 | `if (fo == null)` | L2267 |
| 3 | `foreach` A-Prime | L2273 |
| 4 | `OrderState == Working` | L2275 |
| 5 | `Name == "PTT-TGT-Drag"` | L2276 |
| 6 | `catch` A-Prime | L2283 |
| 7 | Block A `catch` | L2295 |
| 8 | `if (newTarget == null)` | L2317 |

Phase C adds: 0 branches (3 void helper calls with no `if` in main body). CYC = **8**. PASS.

#### `SyncFollowerBracket` (call site only -- CopyEngine.cs)

```csharp
private void SyncFollowerBracket(Account acc, Order leaderOrder, bool isStop, double newPrice, double tickSize)
```

| Item | Value |
|------|-------|
| CYC target | <= 8 |
| CYC result | 7 (UNCHANGED -- call site line only; no new branches) |
| Change | L2158: `SyncAtmFollowerTarget(acc, fo, newPrice)` -> `SyncAtmFollowerTarget(acc, fo, newPrice, leaderOrder)` |

### New Methods

#### `DeriveLeaderBracketIndex(Order? leaderOrder)` (new -- CopyEngine.cs)

```csharp
private static int DeriveLeaderBracketIndex(Order? leaderOrder)
```

| Item | Value |
|------|-------|
| CYC target | <= 3 |
| CYC count | 3 |
| Returns | `int N` (suffix of "Target3" -> 3; "Stop2" -> 2; unparseable/null -> 0) |
| Role | Isolates suffix-parse logic; zero-branch call site in SyncAtmFollowerTarget |

**CYC branches**:
| # | Branch |
|---|--------|
| 1 | `if (leaderOrder?.Name == null)` -> return 0 |
| 2 | `int.TryParse` success check |
| 3 | `if (n <= 0)` -> return 0 |

#### `FindLeaderStopPrice(Account? leaderAccount, int bracketIndex)` (new -- CopyEngine.cs)

```csharp
private static double FindLeaderStopPrice(Account? leaderAccount, int bracketIndex)
```

| Item | Value |
|------|-------|
| CYC target | <= 5 |
| CYC count | 5 |
| Returns | `double` stop price from leader's Working "Stop{N}" order; `0.0` if not found |
| Role | Encapsulates leader account order scan; null/zero guards; returns 0.0 (not null) |

**CYC branches**:
| # | Branch |
|---|--------|
| 1 | `if (leaderAccount == null)` -> return 0.0 |
| 2 | `if (bracketIndex <= 0)` -> return 0.0 |
| 3 | `foreach` over leaderAccount.Orders |
| 4 | `if (order.Name == "Stop{N}")` |
| 5 | `if (order.OrderState == OrderState.Working)` |

#### `CreateFollowerReplacementStop(Account followerAcc, Instrument instr, int qty, OrderAction stopAction, double stopPrice)` (new -- CopyEngine.cs)

```csharp
private void CreateFollowerReplacementStop(
    Account followerAcc,
    Instrument instr,
    int qty,
    OrderAction stopAction,
    double stopPrice)
```

| Item | Value |
|------|-------|
| CYC target | <= 4 |
| CYC count | 4 |
| Returns | `void` |
| Role | Calls `CreateOrder(StopMarket)` + `Submit()` for PTT-STP-Drag; all guards internal |

**CYC branches**:
| # | Branch |
|---|--------|
| 1 | `if (stopPrice <= 0.0)` -> return (graceful skip, log) |
| 2 | `try` block |
| 3 | `if (newStop == null)` -> return (log + return) |
| 4 | `catch` |

---

## Section D: Non-Regression Scope

### UNCHANGED Items

| Method / Block | Reason |
|----------------|--------|
| Block A-Prime (L2270-2288) | DW-B139 / B131 LaneB fix -- zero modification; exactly as built |
| Block A (L2290-2298) | Cancel of leader's ATM target reference -- zero modification |
| Block B (L2300-2328) | PTT-TGT-Drag CreateOrder+Submit -- zero modification |
| `SyncAtmFollowerBracket` | Stop-drag path -- not in scope |
| `HandleBracketChange` | Upstream caller -- signature unchanged; call to `SyncFollowerBracket` unchanged |
| `FindFollowerBracketOrder` | Lookup method -- untouched |
| `SignalOrNameMatches` | B131 LaneA fix -- untouched |
| `IsAtmSTPOrder` | Predicate -- untouched |
| All B129, B130, B131 tests | Zero-impact: signature change adds nullable param with graceful null handling |

### Files Touched

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | 1 signature change; 1 call site line; 3 new method bodies (~50 lines total) |
| `src/PropTraderTools/Tests/B132Tests.cs` | New file -- 5 xUnit `[Fact]` tests |

### SIM Gate Carry-Forward (DW-B131-K3)

B131 LaneB deferred `DW-B131-K3` (SIM validation of DW-B139 Block A-Prime fix) is now co-scheduled
with the B132 LaneA SIM gate. The B132 SIM gate validates BOTH fixes together.

---

## Section E: Test Specifications

**File**: `src/PropTraderTools/Tests/B132Tests.cs`
**Class**: `B132LaneATests`
**Framework**: xUnit `[Fact]` ONLY (never NUnit or MSTest)
**Assembly access**: `InternalsVisibleTo("PropTraderTools.Tests")` confirmed at CopyEngine.cs L46.

---

### Test 1: `SyncAtmFollowerTarget_WhenTargetDragged_CreatesOnePTTTGTDragPerFollower`

**Setup**:
- Mock follower `Account` (`acc`) with empty `Orders` collection (no prior PTT-TGT-Drag).
- Mock leader `Order fo` with `Instrument.FullName = "ES 09-26 CME"`, `OrderAction = OrderAction.Sell`.
- Mock `leaderOrder` (not used in Block B path; Phase C graceful skip via N=0 if name unparseable).
- Track calls to `acc.CreateOrder(...)` with argument capture.

**Action**:
- Call `SyncAtmFollowerTarget(acc, fo, newPrice=4500.0, leaderOrder: null)`.

**Assert**:
- `acc.CreateOrder` was called exactly once.
- The call used `OrderType.Limit`, `OrderAction.Sell`, `fo.Instrument`, name `"PTT-TGT-Drag"`.
- `acc.Submit` was called exactly once with the returned order.

---

### Test 2: `SyncAtmFollowerTarget_WhenTargetDragged_CreatesOnePTTSTPDragPerFollower`

**Setup**:
- Mock follower `Account` (`acc`) with empty `Orders` collection.
- Mock leader `Order fo` with `Name = "Target3"`, `Instrument`, `Quantity = 1`, `OrderAction = OrderAction.Sell`.
- Mock `leaderOrder` with `Name = "Target3"`, `Account` having one Working order named `"Stop3"` with `StopPrice = 4480.0`.
- Track `acc.CreateOrder(...)` calls.

**Action**:
- Call `SyncAtmFollowerTarget(acc, fo, newPrice=4500.0, leaderOrder)`.

**Assert**:
- `acc.CreateOrder` was called exactly twice (once for PTT-TGT-Drag, once for PTT-STP-Drag).
- The second call used `OrderType.StopMarket`, `stopPrice = 4480.0`, name `"PTT-STP-Drag"`, `oco = ""`.
- `acc.Submit` was called exactly twice.

---

### Test 3: `SyncAtmFollowerTarget_WhenNoLeaderStopFound_SkipsSTPDragPlacement`

**Setup**:
- Mock follower `Account` (`acc`) with empty `Orders`.
- Mock leader `Order fo` with `Name = "Target3"`, `OrderAction = OrderAction.Sell`.
- Mock `leaderOrder` with `Name = "Target3"`, leader `Account` with NO Working `"Stop3"` order.

**Action**:
- Call `SyncAtmFollowerTarget(acc, fo, newPrice=4500.0, leaderOrder)`.

**Assert**:
- `acc.CreateOrder` was called exactly once (Block B only -- PTT-TGT-Drag).
- NO second `acc.CreateOrder` call for PTT-STP-Drag.
- No exception thrown. Method completes normally.

---

### Test 4: `SyncAtmFollowerTarget_DeriveLeaderBracketIndex_ParsesNameSuffix`

**Setup**:
- Directly test the static helper `DeriveLeaderBracketIndex`.

**Action + Assert**:
- `DeriveLeaderBracketIndex(order with Name="Target3")` == 3
- `DeriveLeaderBracketIndex(order with Name="Target1")` == 1
- `DeriveLeaderBracketIndex(order with Name="Stop2")` == 2
- `DeriveLeaderBracketIndex(order with Name="Stop99")` == 99
- `DeriveLeaderBracketIndex(null)` == 0
- `DeriveLeaderBracketIndex(order with Name="")` == 0
- `DeriveLeaderBracketIndex(order with Name="TargetABC")` == 0 (non-numeric suffix)

---

### Test 5: `SyncAtmFollowerTarget_FindLeaderStopPrice_ReturnsCorrectPrice`

**Setup**:
- Directly test the static helper `FindLeaderStopPrice`.
- Leader account with two orders:
  - Working `"Stop3"` at `StopPrice = 4480.0`
  - Working `"Stop1"` at `StopPrice = 4470.0`

**Action + Assert**:
- `FindLeaderStopPrice(leaderAccount, bracketIndex=3)` == 4480.0
- `FindLeaderStopPrice(leaderAccount, bracketIndex=1)` == 4470.0
- `FindLeaderStopPrice(leaderAccount, bracketIndex=2)` == 0.0 (not found)
- `FindLeaderStopPrice(null, bracketIndex=3)` == 0.0 (null account)
- `FindLeaderStopPrice(leaderAccount, bracketIndex=0)` == 0.0 (zero index)

---

## Section F: DW Items (Deferred Work)

**DW-F: None. All API facts confirmed via NT8_FULL_REFERENCE.md and NT8_ADDON_KNOWLEDGE.md.**

Rationale:
- `Account.CreateOrder()` + `Submit()` in AddOnBase: confirmed from NT8_FULL_REFERENCE.md L2106-2121 and production usage in `SyncAtmFollowerBracket`.
- `Order.Name` for scanning leader account orders: confirmed from NT8_ADDON_KNOWLEDGE.md L229.
- `Order.StopPrice` for reading stop price: confirmed from NT8_ADDON_KNOWLEDGE.md L225.
- OCO group-cancel behavior: confirmed (B131 SIM session 2 -- Director observation, DW-B134-OCO resolved).
- `oco = ""` for standalone (non-OCO) PTT-STP-Drag: confirmed from B131 LaneA tickets and NT8_FULL_REFERENCE.md L2118.

No genuinely unknown API facts remain.

---

## Section G: Lamport / Scan Checklist (SCAN-01 through SCAN-07)

| Scan ID | Description | Command | Required Result |
|---------|-------------|---------|-----------------|
| SCAN-01 | No `lock()` usage in modified or new code | `grep -r "lock(" src/ --include="*.cs"` | 0 matches in new/modified code |
| SCAN-02 | No `async void` (non-event-handler) | `grep -rn "async void " src/ --include="*.cs"` | 0 matches in new/modified code |
| SCAN-03 | No `return null` in new code (value types used) | `grep -rn "return null;" src/ --include="*.cs"` | 0 matches in `DeriveLeaderBracketIndex` / `FindLeaderStopPrice` / `CreateFollowerReplacementStop` |
| SCAN-04 | No `throw new XxxException(...)` in hot paths | `grep -rn "throw new " src/ --include="*.cs"` | 0 matches in new/modified methods |
| SCAN-05 | CYC <= 8 on all modified/new methods | `python scripts/complexity_audit.py` | 0 violations: `SyncAtmFollowerTarget`=8, `DeriveLeaderBracketIndex`=3, `FindLeaderStopPrice`=5, `CreateFollowerReplacementStop`=4 |
| SCAN-06 | ASCII-only identifiers and string literals | `grep -Prn "[^\x00-\x7F]" src/ --include="*.cs"` | 0 matches in new/modified code |
| SCAN-07 | Build clean | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 0 warnings |

### SCAN Notes

- SCAN-01: No `lock()` anywhere. `acc.Orders.ToList()` and `leaderOrder.Account.Orders.ToList()` are NT8 thread-safe collections; no lock required.
- SCAN-02: All new methods are `private void`, `private static int`, `private static double`. No async.
- SCAN-03: All value-type returns (`int`, `double`). `CreateFollowerReplacementStop` is `void`. No null return paths.
- SCAN-04: All new methods use try/catch with no rethrow; catch blocks log via `StatusUpdate?.Invoke(...)` and return.
- SCAN-05: See Section C for full CYC accounting per method.
- SCAN-06: New string literals: `"PTT-STP-Drag"`, `"Stop"`, `": PTT-STP-Drag placed bracket N="` -- all ASCII-only.
- SCAN-07: Signature change to `SyncAtmFollowerTarget` is backward-compatible (nullable 4th param); one call site updated; no other callers.

---

## Section H: Rules Catalog Gate Result

### JS Rules Checked

| Rule ID | Description | Applies to this change | Result |
|---------|-------------|------------------------|--------|
| **JS-021** (P0) | No `lock()` anywhere | `acc.Orders` and `leaderOrder.Account.Orders` iteration; no lock needed (NT8 thread-safe collections) | **PASS** |
| **JS-001** (P0) | No `throw` in hot paths | `CreateFollowerReplacementStop` wraps `CreateOrder`+`Submit` in try/catch; no rethrow; `FindLeaderStopPrice` / `DeriveLeaderBracketIndex` are pure computation, no throw | **PASS** |
| **JS-002** (P0) | No `return null` for missing values | `DeriveLeaderBracketIndex` returns `int` (0 on failure); `FindLeaderStopPrice` returns `double` (0.0 on failure); `CreateFollowerReplacementStop` returns `void`; no null returned | **PASS** |
| **JS-033** (P0) | No `async void` (non-event-handler) | All new methods are `private void` or `private static int/double`; no async | **PASS** |
| **CYC <= 8** | Jane Street strict standard | `SyncAtmFollowerTarget`=8, helpers=3/5/4 -- all within limit | **PASS** |
| **ASCII-only** | No Unicode in C# identifiers or literals | All new string literals ASCII-only | **PASS** |
| **Minimal change** | Only touch what is required | 1 signature change + 1 call site line + 3 new helper methods; Block A-Prime / Block A / Block B UNCHANGED | **PASS** |
| **No cross-contamination** | File split validation | 2 files (CopyEngine.cs + B132Tests.cs); zero sibling method modifications | **PASS** |
| **DateTime.UtcNow** | No `DateTime.Now` | No date/time used in any new code | **PASS** |
| **PTT- prefix** | All `CreateOrder` names start with `"PTT-"` | New stop order name: `"PTT-STP-Drag"` | **PASS** |

### Gate Result: PASS

All P0 rules confirmed compliant. No violations in the proposed design. No existing code paths
modified except the one call site in `SyncFollowerBracket`. All new methods satisfy CYC <= 8.
Plan is safe to proceed to Phase 2 (review) and Phase 3 (ticket generation).

---

## Completion Gate

- [x] STEP 0: Rules Catalog Gate confirmed PASS (Section H)
- [x] STEP 1: All mandatory reads complete (B131 LaneB plan, 3 CopyEngine.cs methods, 2 NT8 docs)
- [x] STEP 2: All 5 NT8 API facts confirmed with source citations (Section A table)
- [x] STEP 3: Phase C design complete (3 helpers designed, CYC verified)
- [x] STEP 4: All 8 plan sections written (A through H)
- [x] STEP 5: `docs/brain/B132/` directory created; plan file written

---

*Status: REVIEW_PENDING (corrected, Cycle 1)*
*Epic: B132 LaneA*
*Phase: 1 -- Architecture Plan*
