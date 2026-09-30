# B132 LaneA -- Tickets

**Status**: TICKET_REVIEW_PENDING
**Epic**: B132 LaneA
**Phase**: 3 -- Ticket Generation
**Architect**: ptt-architect
**Date**: 2026-08-31
**Source Plan**: `docs/brain/B132/LaneA-02-architecture-plan.md` (REVIEW_PASS, Cycle 2)
**Plan Review**: `docs/brain/B132/LaneA-02-plan-review.md` (REVIEW_PASS)

---

## Ticket B132-LaneA-T1

### TICKET HEADER

| Field | Value |
|-------|-------|
| **Ticket ID** | B132-LaneA-T1 |
| **Epic** | B132 LaneA |
| **Title** | SyncAtmFollowerTarget Phase C -- PTT-STP-Drag Replacement Stop After Target Drag |
| **Spec Req IDs** | DW-B141 (P0) |
| **Plan Sections** | A, B, C, D, E, G, H |
| **Status** | PENDING |

---

### CONTEXT

#### Defect

When `SyncAtmFollowerTarget` executes Block A for a follower account:

```csharp
acc.Cancel(new Order[] { fo }); // Block A -- cancel follower's ATM target bracket
```

`fo` is the follower's ATM bracket target order (e.g. `"Target3"`). NT8's ATM engine manages
stop and target brackets as an OCO (One-Cancels-Other) group. When `acc.Cancel()` is called
on ANY OCO group member, NT8 automatically cancels ALL orders in that group.

**Consequence**: Cancelling `"Target3"` also cancels `"Stop3"` (and all OCO-linked stops for
that ATM strategy). After Block A fires:
- The follower's `"Target3"` is cancelled (intended).
- The follower's `"Stop3"` is cancelled (unintended OCO side effect).
- Block B places a new `"PTT-TGT-Drag"` replacement target (correct).
- **[MISSING]** No replacement stop is placed.
- The follower account is left in an open position with NO stop protection.

#### Confirmed Root Cause and NT8 API Facts (Plan Section A)

| Fact | Status | Source |
|------|--------|--------|
| `Account.Change()` is a silent no-op on ATM-owned brackets | CONFIRMED (B129 SIM) | `CopyEngine.cs` L2190-2192 comment; AGENTS.md |
| `Account.Cancel()` on an OCO-linked order cancels the FULL OCO group | CONFIRMED (B131 SIM session 2) | Director SIM observation; DW-B134-OCO resolution |
| `Account.CreateOrder()` + `Submit()` available in AddOnBase context | CONFIRMED | `NT8_FULL_REFERENCE.md` L2106-2121; production usage in `SyncAtmFollowerBracket` L2222-2241 |
| ATM bracket order names follow `"Stop1"` / `"Stop2"` / `"Stop3"` pattern | CONFIRMED | `IsAtmSTPOrder` predicate L2107-2113; B131 LaneA diagnosis |
| Leader bracket index derivable from order name suffix (`"Target3"` -> N=3) | CONFIRMED by design | `IsAtmSTPOrder` checks `StartsWith("Target")`; B131 LaneA test session |
| OCO parameter for PTT-placed standalone orders must be `""` (empty) | CONFIRMED | `NT8_FULL_REFERENCE.md` L2118; B131 LaneA tickets L36 |

**Why `Account.Change()` is not the answer**: `Account.Change()` is a silent no-op on
ATM-engine-owned brackets (B129 SIM gate). Correct AddOn pattern = `Cancel() + CreateOrder() + Submit()`.
This is the established pattern for `SyncAtmFollowerBracket` and `SyncAtmFollowerTarget` Block B.
Phase C uses the same pattern for the replacement stop.

---

### SCOPE OF CHANGES

**Files to modify**: `src/PropTraderTools/CopyEngine.cs` ONLY.
**New test file**: `src/PropTraderTools/Tests/B132Tests.cs`
**No new namespaces. No new DLL imports.**

---

### METHODS TO ADD (new private helpers in CopyEngine.cs)

#### Method 1 -- `DeriveLeaderBracketIndex`

```csharp
private static int DeriveLeaderBracketIndex(Order? leaderOrder)
```

**Responsibility**: Parse the integer suffix from `leaderOrder.Name`.

**Logic**:
1. If `leaderOrder` is null or `leaderOrder.Name` is null or empty -> return 0.
2. Extract the trailing numeric substring from `leaderOrder.Name`
   (e.g. `"Target3"` -> `"3"`, `"Stop99"` -> `"99"`, `"TargetABC"` -> parse fails).
3. `int.TryParse` the extracted suffix -> if fails -> return 0.
4. If parsed value <= 0 -> return 0.
5. Return the parsed value.

**CYC target**: <= 3
**CYC branches**: (1) null/empty name guard; (2) `int.TryParse` success; (3) `if (n <= 0)` guard.

**Return contract**: Returns `0` (not null) on any failure path (JS-002 compliant).
**No lock()**. No async. No throw. ASCII-only string literals.

---

#### Method 2 -- `FindLeaderStopPrice`

```csharp
private static double FindLeaderStopPrice(Account? leaderAccount, int bracketIndex)
```

**Responsibility**: Scan `leaderAccount.Orders` for a Working order named `"Stop{bracketIndex}"`
and return its `StopPrice` (or `AuxPrice`). Returns `0.0` if not found.

**Logic**:
1. If `leaderAccount == null` -> return 0.0.
2. If `bracketIndex <= 0` -> return 0.0.
3. Build target name: `"Stop" + bracketIndex.ToString()`.
4. Snapshot: `var orders = leaderAccount.Orders.ToList()` (safe iteration pattern from Block A-Prime).
5. `foreach (var order in orders)`:
   a. If `order.Name == targetName` AND `order.OrderState == OrderState.Working` -> return `order.StopPrice`.
6. Return 0.0 (not found).

**CYC target**: <= 5
**CYC branches**: (1) null account guard; (2) zero-index guard; (3) `foreach`; (4) name match; (5) state == Working.

**Return contract**: Returns `double` (0.0 on all failure paths). No null return (JS-002 compliant).
**No lock()**. No async. No throw. ASCII-only string literals.

---

#### Method 3 -- `CreateFollowerReplacementStop`

```csharp
private void CreateFollowerReplacementStop(
    Account followerAcc,
    Instrument instr,
    int qty,
    OrderAction stopAction,
    double stopPrice)
```

**Responsibility**: Place a `StopMarket` order named `"PTT-STP-Drag"` on `followerAcc`
at `stopPrice`. Calls `CreateOrder()` then `Submit()`. Wraps NT8 API call in try/catch.

**Logic**:
1. Guard: if `stopPrice <= 0.0` -> log `"PTT-STP-Drag skipped: stopPrice <= 0"` and return.
2. `try`:
   a. `var newStop = followerAcc.CreateOrder(instr, stopAction, OrderType.StopMarket, OrderEntry.Automated, TimeInForce.Day, qty, 0, stopPrice, "", "PTT-STP-Drag", NinjaTrader.Core.Globals.MaxDate, (NinjaTrader.Cbi.CustomOrder)null);`
   b. If `newStop == null` -> log `"PTT-STP-Drag: CreateOrder returned null"` and return.
   c. `followerAcc.Submit(new[] { newStop });`
   d. Log: `followerAcc.Name + ": PTT-STP-Drag placed @ " + stopPrice.ToString()`
3. `catch (Exception ex)`:
   a. Log: `"PTT-STP-Drag error: " + ex.Message`
   b. Return (no rethrow -- JS-001 compliant).

**CYC target**: <= 4
**CYC branches**: (1) `if (stopPrice <= 0.0)`; (2) `try` block; (3) `if (newStop == null)`; (4) `catch`.

**OrderName**: `"PTT-STP-Drag"` -- ASCII-only, PTT- prefix compliant.
**oco parameter**: `""` (empty string) -- PTT-STP-Drag is NOT part of any NT8 ATM OCO group.
**stopAction**: Passed from caller (`fo.OrderAction`) -- same action as the cancelled stop.
**No lock()**. No async. No throw (catch + return only). No return null (void method).

---

### METHODS TO MODIFY (in CopyEngine.cs)

#### Method 4 -- `SyncAtmFollowerTarget` (signature change + Phase C addition)

**Signature change**:
```csharp
// BEFORE:
private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice)

// AFTER:
private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice, Order? leaderOrder)
```

**Phase C addition** (3 lines, after Block B's try/catch -- zero new branches in main body):
```csharp
// [Phase C -- B132 LaneA] Replace follower's OCO-cancelled stop after target drag
int bracketIdx = DeriveLeaderBracketIndex(leaderOrder);
double stp = FindLeaderStopPrice(leaderOrder?.Account, bracketIdx);
CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp);
```

**CYC impact**: Phase C adds 3 unconditional method calls -- ZERO new branches.
CYC of `SyncAtmFollowerTarget` stays at **8** (PASS).

**CYC branch accounting** (all 8 branches verified):

| # | Branch | Location |
|---|--------|----------|
| 1 | `if (acc == null)` | L2265 |
| 2 | `if (fo == null)` | L2267 |
| 3 | `foreach` A-Prime sweep | L2273 |
| 4 | `OrderState == Working` check | L2275 |
| 5 | `Name == "PTT-TGT-Drag"` check | L2276 |
| 6 | `catch` A-Prime | L2283 |
| 7 | Block A `catch` | L2295 |
| 8 | `if (newTarget == null)` | L2317 |

Phase C = 3 void helper calls with no `if` in main body. Delta CYC = 0.

**Complete updated method flow** (for engineer reference):
```
[Guard] acc == null                -> return
[Guard] fo == null                 -> return
[Block A-Prime] foreach sweep: cancel Working PTT-TGT-Drag orders (DW-B139 -- UNCHANGED)
[Block A]       acc.Cancel(fo)     -- cancels ATM target; OCO side-effect kills Stop{N} (UNCHANGED)
[Block B]       acc.CreateOrder(Limit) + Submit  -- PTT-TGT-Drag at newPrice (UNCHANGED)
[Phase C -- NEW]:
    bracketIdx = DeriveLeaderBracketIndex(leaderOrder)
    stp = FindLeaderStopPrice(leaderOrder?.Account, bracketIdx)
    CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp)
```

---

#### Method 5 -- `SyncFollowerBracket` (call site update only)

**One line change at ~L2158**:
```csharp
// BEFORE:
SyncAtmFollowerTarget(acc, fo, newPrice);

// AFTER:
SyncAtmFollowerTarget(acc, fo, newPrice, leaderOrder);
```

`leaderOrder` is already in scope at the call site (it is a parameter of `SyncFollowerBracket`).

**CYC impact**: 0. `SyncFollowerBracket` CYC remains 7 (UNCHANGED). No new branches.

---

### REQUIRED IMPLEMENTATION RULES

| Rule | Constraint | Basis |
|------|-----------|-------|
| No `lock()` | `acc.Orders.ToList()` and `leaderAccount.Orders.ToList()` are NT8 thread-safe collections; no lock needed | JS-021 (P0) |
| No `async void` | All void methods are synchronous; no async anywhere | JS-033 (P0) |
| No `return null` | All return types are value types (`int`, `double`) or `void`; returns 0 / 0.0 on failure | JS-002 (P0) |
| No `throw new XxxException` | Defensive guard + log + return pattern in all new methods; catch blocks do not rethrow | JS-001 (P0) |
| CYC <= 8 | SyncAtmFollowerTarget=8, DeriveLeaderBracketIndex=3, FindLeaderStopPrice=5, CreateFollowerReplacementStop=4 | Jane Street strict |
| `"PTT-STP-Drag"` | ASCII-only order name; PTT- prefix required on all `CreateOrder` names | NT8 convention |
| xUnit [Fact] only | Never NUnit, never MSTest | JS testing standard |
| `oco = ""` | PTT-STP-Drag must NOT be part of any OCO group | NT8_FULL_REFERENCE.md L2118 |
| Null-safe dereference | `leaderOrder?.Account` (not `leaderOrder.Account`) in Phase C call | V-01 resolution |
| `DateTime.UtcNow` | No `DateTime.Now` anywhere in new code | NT8 constraint |

---

### XUNIT TESTS (file: src/PropTraderTools/Tests/B132Tests.cs, class: B132LaneATests)

All tests use `[Fact]` attribute (xUnit). Never NUnit. Never MSTest.
`InternalsVisibleTo("PropTraderTools.Tests")` is confirmed at `CopyEngine.cs` L46.

---

#### [Fact] SyncAtmFollowerTarget_WhenTargetDragged_CreatesOnePTTTGTDragPerFollower

**What it asserts**: When `SyncAtmFollowerTarget` is called with `leaderOrder = null` (Phase C
graceful skip via N=0), Block B still fires exactly once and creates exactly one `"PTT-TGT-Drag"`
order via `acc.CreateOrder(... OrderType.Limit ...)` + one `Submit`.

**Setup**:
- Mock follower `Account` (`acc`) with empty `Orders` collection (no prior PTT-TGT-Drag).
- Mock leader `Order fo` with `Instrument.FullName = "ES 09-26 CME"`, `OrderAction = OrderAction.Sell`, `Quantity = 1`.
- Track all calls to `acc.CreateOrder(...)` with argument capture.

**Action**: `SyncAtmFollowerTarget(acc, fo, newPrice: 4500.0, leaderOrder: null)`

**Asserts**:
- `acc.CreateOrder` called exactly once.
- That call used `OrderType.Limit`, `OrderAction.Sell`, instrument `fo.Instrument`, name `"PTT-TGT-Drag"`.
- `acc.Submit` called exactly once with the returned order.

---

#### [Fact] SyncAtmFollowerTarget_WhenTargetDragged_CreatesOnePTTSTPDragPerFollower

**What it asserts**: When `leaderOrder.Name = "Target3"` and the leader account has a Working
`"Stop3"` at price 4480.0, Phase C fires and creates a second `"PTT-STP-Drag"` StopMarket order
on the follower account at exactly that price.

**Setup**:
- Mock follower `Account` (`acc`) with empty `Orders` collection.
- Mock follower `Order fo` with `Name = "Target3"`, `Instrument`, `Quantity = 1`, `OrderAction = OrderAction.Sell`.
- Mock `leaderOrder` with `Name = "Target3"`, `Account` containing one Working order named `"Stop3"` with `StopPrice = 4480.0`.
- Track all `acc.CreateOrder(...)` calls (argument capture).

**Action**: `SyncAtmFollowerTarget(acc, fo, newPrice: 4500.0, leaderOrder)`

**Asserts**:
- `acc.CreateOrder` called exactly twice (Block B = PTT-TGT-Drag, Phase C = PTT-STP-Drag).
- First call: `OrderType.Limit`, name `"PTT-TGT-Drag"`.
- Second call: `OrderType.StopMarket`, `stopPrice = 4480.0`, name `"PTT-STP-Drag"`, `oco = ""`.
- `acc.Submit` called exactly twice.

---

#### [Fact] SyncAtmFollowerTarget_WhenNoLeaderStopFound_SkipsSTPDragPlacement

**What it asserts**: When `leaderOrder.Name = "Target3"` but the leader account has NO Working
`"Stop3"` order, `FindLeaderStopPrice` returns `0.0`, `CreateFollowerReplacementStop` guards and
returns without placing any order. Only PTT-TGT-Drag (Block B) is created.

**Setup**:
- Mock follower `Account` (`acc`) with empty `Orders`.
- Mock follower `Order fo` with `Name = "Target3"`, `OrderAction = OrderAction.Sell`.
- Mock `leaderOrder` with `Name = "Target3"`, leader `Account` with NO Working `"Stop3"` order
  (account has other orders, but none named `"Stop3"` in Working state).
- Track `acc.CreateOrder(...)` calls.

**Action**: `SyncAtmFollowerTarget(acc, fo, newPrice: 4500.0, leaderOrder)`

**Asserts**:
- `acc.CreateOrder` called exactly once (Block B only -- PTT-TGT-Drag).
- No second `acc.CreateOrder` call for PTT-STP-Drag.
- No exception thrown. Method completes normally.

---

#### [Fact] SyncAtmFollowerTarget_DeriveLeaderBracketIndex_ParsesNameSuffix

**What it asserts**: `DeriveLeaderBracketIndex` correctly parses the integer suffix from
order names and returns 0 for null, empty, or non-numeric-suffix names.

**Setup**: Directly call the static helper `DeriveLeaderBracketIndex`.

**Action + Assert** (all in one [Fact]):
```
DeriveLeaderBracketIndex(order { Name = "Target3" })  == 3
DeriveLeaderBracketIndex(order { Name = "Target1" })  == 1
DeriveLeaderBracketIndex(order { Name = "Stop2"   })  == 2
DeriveLeaderBracketIndex(order { Name = "Stop99"  })  == 99
DeriveLeaderBracketIndex(null)                        == 0
DeriveLeaderBracketIndex(order { Name = ""        })  == 0
DeriveLeaderBracketIndex(order { Name = "TargetABC"}) == 0  (non-numeric suffix)
```

---

#### [Fact] SyncAtmFollowerTarget_FindLeaderStopPrice_ReturnsCorrectPrice

**What it asserts**: `FindLeaderStopPrice` returns the `StopPrice` of the correct Working
`"Stop{N}"` order, and returns `0.0` for null account, zero index, or missing order.

**Setup**: Mock leader `Account` with two Working orders:
- `"Stop3"` with `StopPrice = 4480.0`
- `"Stop1"` with `StopPrice = 4470.0`

**Action + Assert** (all in one [Fact]):
```
FindLeaderStopPrice(leaderAccount, bracketIndex: 3) == 4480.0
FindLeaderStopPrice(leaderAccount, bracketIndex: 1) == 4470.0
FindLeaderStopPrice(leaderAccount, bracketIndex: 2) == 0.0   (not found)
FindLeaderStopPrice(null,          bracketIndex: 3) == 0.0   (null account)
FindLeaderStopPrice(leaderAccount, bracketIndex: 0) == 0.0   (zero index)
```

---

### 7-SCAN CHECKLIST (VERBATIM -- ENGINEER CONTRACT)

The engineer MUST run all 7 scans and confirm 0 violations before submitting the completion artifact.

| Scan ID | Command | Required Result |
|---------|---------|-----------------|
| **SCAN-01** | `grep -r "lock(" src/ --include="*.cs"` | 0 results in new or modified code |
| **SCAN-02** | `grep -rn "async void " src/ --include="*.cs"` | 0 results in new or modified code |
| **SCAN-03** | `grep -rn "return null;" src/ --include="*.cs"` | 0 results in `DeriveLeaderBracketIndex`, `FindLeaderStopPrice`, `CreateFollowerReplacementStop` |
| **SCAN-04** | `grep -rn "throw new " src/ --include="*.cs"` | 0 results in new or modified methods |
| **SCAN-05** | `python scripts/complexity_audit.py` | 0 violations: `SyncAtmFollowerTarget`=8, `DeriveLeaderBracketIndex`=3, `FindLeaderStopPrice`=5, `CreateFollowerReplacementStop`=4 |
| **SCAN-06** | `grep -Prn "[^\x00-\x7F]" src/ --include="*.cs"` | 0 results in new or modified code |
| **SCAN-07** | `dotnet build` | 0 errors, 0 warnings |

**SCAN notes for engineer**:
- SCAN-01: `acc.Orders.ToList()` and `leaderAccount.Orders.ToList()` are NT8 thread-safe; no lock needed.
- SCAN-02: All 3 new methods are `private void` or `private static int/double`. No async.
- SCAN-03: All return types are value types (`int`, `double`) or `void`. Zero null returns.
- SCAN-04: `CreateFollowerReplacementStop` uses try/catch with no rethrow; other helpers are pure computation.
- SCAN-05: Phase C adds 0 branches to `SyncAtmFollowerTarget` (3 unconditional method calls).
- SCAN-06: New string literals `"PTT-STP-Drag"`, `"Stop"`, `"PTT-STP-Drag placed @ "` are all ASCII-only.
- SCAN-07: `SyncAtmFollowerTarget` 4th param is nullable; one call site updated at `SyncFollowerBracket` L2158; no other callers.

---

### NON-REGRESSION SCOPE

The following items MUST remain UNCHANGED (zero lines modified):

| Item | Location | Reason |
|------|----------|--------|
| Block A-Prime (pre-sweep) | `SyncAtmFollowerTarget` L2270-2288 | DW-B139 / B131 LaneB fix -- do not touch |
| Block A (`acc.Cancel(fo)`) | `SyncAtmFollowerTarget` L2290-2298 | Unchanged by design |
| Block B (`acc.CreateOrder` Limit + Submit) | `SyncAtmFollowerTarget` L2300-2328 | Unchanged by design |
| `SyncAtmFollowerBracket` | CopyEngine.cs | Stop-drag path -- not in scope |
| `HandleBracketChange` | CopyEngine.cs | Upstream caller -- signature unchanged |
| `FindFollowerBracketOrder` | CopyEngine.cs | Untouched |
| `SignalOrNameMatches` | CopyEngine.cs | B131 LaneA fix -- untouched |
| `IsAtmSTPOrder` | CopyEngine.cs | Predicate -- untouched |
| All B129, B130, B131 tests | Tests/ | Zero-impact: new nullable param has graceful null handling |

---

### ACCEPTANCE CRITERIA

| ID | Criterion |
|----|-----------|
| AC-01 | Follower account receives one PTT-TGT-Drag AND one PTT-STP-Drag per target drag |
| AC-02 | PTT-STP-Drag stop price equals leader's `Stop{N}` price at time of drag |
| AC-03 | Block A-Prime (DW-B139) is UNCHANGED -- zero lines modified |
| AC-04 | All B129 / B130 / B131 existing tests still green |
| AC-05 | All 5 new xUnit [Fact] tests green |
| AC-06 | All 7 scans (SCAN-01 through SCAN-07) return 0 violations |

---

### COMPLETION ARTIFACT

**Engineer writes**: `docs/brain/B132/LaneA-ticket-1-completion.md`

The completion artifact MUST contain:
1. Confirmation that all 5 [Fact] tests pass.
2. Output of all 7 scans (copy-paste the zero-result output for each).
3. Confirmation that Block A-Prime, Block A, Block B are unchanged (diff evidence).
4. Confirmation that `dotnet build` is clean (0 errors, 0 warnings).
5. git diff summary showing only `CopyEngine.cs` and `B132Tests.cs` modified.

**Verifier reads** `docs/brain/B132/LaneA-ticket-1-completion.md` in Phase 4b.

---

## Footer

**Status**: TICKET_REVIEW_PENDING
**Epic**: B132 LaneA
**Phase**: 3 -- Ticket Generation
**Ticket count**: 1 (B132-LaneA-T1)
**Spec Req IDs covered**: DW-B141 (P0)
**Plan sections covered**: A, B, C, D, E, G, H
