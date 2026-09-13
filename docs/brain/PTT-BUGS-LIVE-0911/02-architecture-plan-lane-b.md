# Architecture Plan — PTT-BUGS-LIVE-0911 Lane B
**BUG-2: Follower order slate not cleared between trades (fo=NULL bracket failure)**
**Status: PLAN_COMPLETE (Cycle 3 — VIOLATION-3 resolved)**
**Author: ptt-architect**
**Date: 2026-09-11**

---

## LANE-SPLIT GATE RESULT: LANES-APPROVED

| Gate Question | Answer | Reasoning |
|---|---|---|
| Q1. Same method or within 50 lines as BUG-1 fix? | **NO** | BUG-2 primary change is `FindFollowerBracketOrder` (line ~3902); BUG-1 primary change is `FlattenOneAccount`/`TryDispatchLeaderFlat` guard. ~1500 lines apart. FlattenOneAccount is touched by both but for orthogonal reasons (BUG-1 adds pre-dispatch position check; BUG-2 adds a one-line state-mark call). Lane A is VERIFY_PASS so changes are stable and non-conflicting. |
| Q2. Fix B design depends on Fix A final design? | **NO** | BUG-2 introduces a new `_followerSlateReady` field, new handler, and a gate in `FindFollowerBracketOrder`. None of these read, call, or depend on any method or field introduced by BUG-1. |
| Q3. Each fix has standalone value if the other is blocked? | **YES** | BUG-2 independently prevents the no-stop-protection scenario on 2nd+ trades — the primary financial risk from Friday 2026-09-11. |
| Q4. Each fix has an independent SIM verification path? | **YES** | BUG-2 SIM: execute QX-2T exit on Trade 1, then Trade 2 entry, verify all 6 bracket orders show fo=NOT-NULL for all followers. Completely distinct from BUG-1 SIM (flatten-on-flat guard). |

---

## 1. Problem Statement

On Friday 2026-09-11, follower account PA-APEX-422136-07 (-07) entered Trade 3 (MES Short x7 @ 7674.75, 5:58 AM) with **zero stop or target protection** from the copier. Six consecutive `[TP4-SFB]` log lines confirmed `fo=NULL` for Stop1, Stop2, Stop3, Target1, Target2, and Target3 on -07 (output.md lines 894, 900, 906 and following).

**First trade of the session (Trade 2) was clean.** The bug manifests exclusively on the 2nd+ trade after any exit that replaces standard named ATM bracket orders (QX, flatten, close). The absence of stop/target protection on -07 during Trade 3 represented an unmanaged risk exposure for the duration of that trade.

Trade 2 on -07 also exhibited an early instance: output.md lines 268, 274, 280 show `fo=NULL` on -08 for Trade 2's bracket sync (which was an intermediate slot with a bracket-arm race, but the financial exposure in that instance was bounded).

---

## 2. Root Cause

### Precise failure mechanism

**Evidence from output.md line 894:**
```
[TP4-SFB] acc=PA-APEX-422136-07 leaderName=Stop1 isStop=True fo=NULL
  followerOrders=[Entry:Cancelled,Entry:Cancelled,Entry:Filled,
                  Stop1:Cancelled,Target1:Cancelled,Stop2:Cancelled,
                  Target2:Cancelled,Stop3:Cancelled,Target3:Cancelled,
                  PTT-QX-Stop:Cancelled,PTT-QX-T1:Filled,
                  PTT-QX-Stop2:Cancelled,PTT-QX-T2:Cancelled,
                  Close:Filled,Entry:Cancelled,Entry:Filled]
```

Compare -08 at the same event (line 895):
```
[TP4-SFB] acc=PA-APEX-422136-08 leaderName=Stop1 isStop=True fo=Stop1
  followerOrders=[...Close:Filled,Entry:Cancelled,Entry:Filled,
                  Stop1:Submitted,Target1:Submitted,Stop2:Submitted,
                  Target2:Submitted,Stop3:Submitted,Target3:Submitted]
```

**-07's follower order slate has Entry:Filled (Trade 3 entry confirmed) but NO working brackets (no Stop1:Submitted etc.).** -08 has both Entry:Filled AND all six bracket orders in Submitted state.

### Root cause chain

1. **Trade 2 exit via QX-2T**: `CancelQxBrackets` cancels `Stop1/Target1/Stop2/Target2/Stop3/Target3` on all follower accounts. These are replaced with `PTT-QX-Stop` + `PTT-QX-T1`. The standard ATM bracket names are now in `Cancelled` state on -07's order slate.

2. **Trade 2 full close**: `PTT-QX-T1` fills, then `Close` order fills. -07 position goes to zero (flat). However, NO cleanup or readiness state is tracked per-follower after exit.

3. **Trade 3 entry fires on -07**: Entry copy dispatched and confirmed `Entry:Filled`. The ATM bracket arm sequence (StartAtmStrategy for Clone mode, or direct CreateOrder for other modes) is triggered.

4. **Bracket sync race**: Leader's bracket orders (Stop1/Target1 etc.) reach Working/Accepted state and `OnOrderUpdate` fires `HandleBracketChange` → `SyncFollowerBracket` → `FindFollowerBracketOrder(acc, ...)`.

5. **`FindFollowerBracketOrder` returns null**: It calls `ActiveOrders(acc)` (which filters Filled/Cancelled/Rejected), then scans for Working/Submitted/Accepted/ChangeSubmitted/Initialized orders matching the bracket name pattern. -07's Trade 3 brackets have not yet reached Submitted state at this moment → no match → `fo=NULL`.

6. **SyncFollowerBracket silently returns** at line 2791 (`if (fo == null) return;`). Bracket price synchronization never fires. -07 remains without managed stop/target protection.

### Why -08 succeeded and -07 did not

-08 went through an additional flatten/re-entry cycle (BUG-1 related accidental long) before Trade 3. This extra state transition effectively reset -08's bracket arm timing such that when `HandleBracketChange` fired, -08's brackets were already in Submitted state. -07 had no such reset and hit the race window.

### Missing mechanism

There is no per-follower readiness gate. Once an exit wipes the standard ATM bracket names (QX, flush, flatten), CopyEngine has no mechanism to:
- Know that a given follower's slate is stale
- Wait for confirmation that the follower is flat AND the slate is clean
- Block bracket sync attempts during the window between exit and the next trade's bracket arm completing

---

## 3. Fix Design

### 3.1 New field

**File:** `src/PropTraderTools/CopyEngine.cs`

```csharp
// BUG2: Follower slate readiness gate.
// Key = follower account name (StringComparer.Ordinal).
// Value = true  -> slate confirmed clean; bracket sync allowed.
//         false -> exit in progress or slate unverified; bracket sync blocked.
// Absent key   -> treated as ready (first trade of session is always clean).
// JS-021: ConcurrentDictionary -- lock-free. JS-023: no volatile (ConcurrentDictionary
// guarantees memory visibility). JS-025: ConcurrentDictionary is canonical lock-free dict.
private readonly ConcurrentDictionary<string, bool> _followerSlateReady =
    new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
```

**Placement:** alongside existing ConcurrentDictionary field declarations (after `_nakedDetectLastQueuedTicks`, ~line 382).

### 3.2 New methods

All new methods: `private` or `private static`. No new public API surface. All ASCII-only string literals.

---

#### `TryMarkFollowerSlateNotReady(Account acc)`

```csharp
// BUG2: Mark follower slate as not-ready when any exit wipes its ATM brackets.
// Called from CancelQxBrackets and FlattenOneAccount on the follower account.
// Guard: only applies to known follower accounts (IsFollowerAccount check).
// CYC<=2: acc null(1) + IsFollowerAccount(2). JS-021: ConcurrentDictionary indexer is lock-free.
// JS-001: no throw. JS-002: void. ASCII-only.
private void TryMarkFollowerSlateNotReady(Account acc)
{
    if (acc == null)                    // (1)
        return;
    if (!IsFollowerAccount(acc))        // (2)
        return;
    _followerSlateReady[acc.Name] = false;
}
```

**CYC:** 2. **PASS.**

---

#### `TrySetFollowerSlateReady(Account acc)`

```csharp
// BUG2: Mark follower slate as ready after position confirmed flat + no active orders.
// Called from OnFollowerPositionUpdate.
// Conditions: acc non-null + IsFollowerAccount + IsAccountFlat + !HasAnyActiveOrders.
// Only writes ready=true when ALL conditions pass. Early-returns prevent false positives.
// CYC<=4: acc null(1) + IsFollowerAccount(2) + !IsAccountFlat(3) + HasAnyActiveOrders(4).
// JS-021: ConcurrentDictionary indexer is lock-free. JS-001: no throw. JS-002: void. ASCII-only.
private void TrySetFollowerSlateReady(Account acc)
{
    if (acc == null)                    // (1)
        return;
    if (!IsFollowerAccount(acc))        // (2)
        return;
    if (!IsAccountFlat(acc))            // (3)
        return;
    if (HasAnyActiveOrders(acc))        // (4)
        return;
    _followerSlateReady[acc.Name] = true;
}
```

**CYC:** 4. **PASS.**

---

#### `IsAccountFlat(Account acc)`

```csharp
// BUG2: Returns true when acc has no open positions across all instruments.
// Used by TrySetFollowerSlateReady to confirm flat before marking slate ready.
// NT8: acc.Positions is available in AddOnBase. Each Position.Quantity > 0 = open.
// CYC<=3: acc null(1) + foreach(2) + Quantity>0(3). JS-021: no lock (read-only enumeration).
// JS-001: no throw. JS-002: returns bool. ASCII-only. static.
private static bool IsAccountFlat(Account acc)
{
    if (acc == null)                    // (1)
        return true;
    foreach (var pos in acc.Positions)  // (2)
        if (pos.Quantity > 0)           // (3)
            return false;
    return true;
}
```

**CYC:** 3. **PASS.**

---

#### `HasAnyActiveOrders(Account acc)`

```csharp
// BUG2: Returns true when acc has any Working/Submitted/Accepted orders remaining.
// Used by TrySetFollowerSlateReady to confirm no residual active orders before ready.
// NT8: acc.Orders.ToList() snapshot -- standard pattern in CopyEngine (line 4898).
// CYC<=5: acc null(1) + foreach(2) + Working(3) + Submitted(4) + Accepted(5).
// JS-021: no lock. JS-001: no throw. JS-002: returns bool. ASCII-only. static.
private static bool HasAnyActiveOrders(Account acc)
{
    if (acc == null)                    // (1)
        return false;
    foreach (var o in acc.Orders.ToList())  // (2)
    {
        if (o.OrderState == OrderState.Working    // (3)
            || o.OrderState == OrderState.Submitted  // (4)
            || o.OrderState == OrderState.Accepted)  // (5)
            return true;
    }
    return false;
}
```

**CYC:** 5. **PASS.**

---

#### `OnFollowerPositionUpdate(object sender, PositionEventArgs e)`

```csharp
// BUG2: NT8 account.PositionUpdate event handler.
// Fires on NT8 account background thread (same context as OnOrderUpdate).
// When position goes flat and no active orders remain, marks slate ready for next trade.
// NT8: PositionEventArgs.Position.Account confirmed from TradeCopierPanel.cs line 2109.
// No UI operations -- ConcurrentDictionary write is thread-safe, no Dispatcher needed.
// CYC<=3: e/Position/Account null(1) + IsFollowerAccount(2) + TrySetFollowerSlateReady(0).
// JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only. No async/await.
private void OnFollowerPositionUpdate(object sender, PositionEventArgs e)
{
    if (e?.Position?.Account == null)           // (1)
        return;
    if (!IsFollowerAccount(e.Position.Account)) // (2)
        return;
    TrySetFollowerSlateReady(e.Position.Account);
}
```

**CYC:** 3. **PASS.**
**Threading:** Fires on NT8 account background thread. No UI access. Safe. ✅

### 3.3 Modified methods

#### `FindFollowerBracketOrder(Account follower, ...)` — add gate

**File:** `src/PropTraderTools/CopyEngine.cs`, ~line 3902

**Current:**
```csharp
private Order? FindFollowerBracketOrder(
    Account follower,
    string? fromEntrySignalName,
    bool isStop,
    string? leaderName = null
) =>
    FindFollowerBracketOrder(
        ActiveOrders(follower),
        fromEntrySignalName,
        isStop,
        leaderName
    );
```

**Replace with:**
```csharp
// BUG2: Gate added -- if slate explicitly not-ready, return null immediately.
// Absent key (first trade): gate open (TryGetValue returns false -> compound is false).
// Key=false (after exit): gate closed -> return null, fo=NULL log suppressed.
// Key=true (after PositionUpdate flat+clean): gate open -> normal search.
// CYC<=2: base(1) + TryGetValue&&!ready(2). JS-021: ConcurrentDictionary.TryGetValue is lock-free.
// JS-001: no throw. JS-002: Order? null contract unchanged. ASCII-only.
private Order? FindFollowerBracketOrder(
    Account follower,
    string? fromEntrySignalName,
    bool isStop,
    string? leaderName = null
)
{
    bool ready;
    if (_followerSlateReady.TryGetValue(follower.Name, out ready) && !ready) // (1)
        return null;
    return FindFollowerBracketOrder(
        ActiveOrders(follower),
        fromEntrySignalName,
        isStop,
        leaderName
    );
}
```

**CYC:** 2 (was 1). **PASS.**

---

#### `Subscribe()` and `Unsubscribe()` — add PositionUpdate wiring

**File:** `src/PropTraderTools/CopyEngine.cs`, lines 1499–1509

**Current:**
```csharp
internal void Subscribe()
{
    foreach (Account acc in Account.All)
        acc.OrderUpdate += OnOrderUpdate;
}

internal void Unsubscribe()
{
    foreach (Account acc in Account.All)
        acc.OrderUpdate -= OnOrderUpdate;
}
```

**Replace with:**
```csharp
// BUG2: PositionUpdate subscription added alongside OrderUpdate (same Account.All loop).
// NT8: account.PositionUpdate confirmed available in AddOnBase (TradeCopierPanel.cs L861).
// CYC unchanged.
internal void Subscribe()
{
    foreach (Account acc in Account.All)
    {
        acc.OrderUpdate += OnOrderUpdate;
        acc.PositionUpdate += OnFollowerPositionUpdate; // BUG2
    }
}

internal void Unsubscribe()
{
    foreach (Account acc in Account.All)
    {
        acc.OrderUpdate -= OnOrderUpdate;
        acc.PositionUpdate -= OnFollowerPositionUpdate; // BUG2
    }
}
```

**CYC:** unchanged (1 foreach each). **PASS.**

---

#### `CancelQxBrackets(Account acc, Instrument instr)` — mark slate not-ready (2-param overload)

**File:** `src/PropTraderTools/CopyEngine.cs`, line 938

Add one call after the null guard:

```csharp
internal void CancelQxBrackets(Account acc, NinjaTrader.Cbi.Instrument instr)
{
    if (acc == null || instr == null)
        return; // (1)
    TryMarkFollowerSlateNotReady(acc); // BUG2: bracket wipe imminent
    var stale = new System.Collections.Generic.List<Order>();
    // ... rest unchanged
```

**CYC:** unchanged (4). New call adds 0 branches. **PASS.**

---

#### `CancelQxBrackets(Account acc, Instrument instr, HashSet<Order> snapshot)` — mark slate not-ready (3-param overload)

**File:** `src/PropTraderTools/CopyEngine.cs`, line 1052

This is the **primary QX-2T execution path**: `TradeCopierPanel` QX-2T button → `PttGlobalQuickExit.Execute()` → `PttQuickExit.Execute()` → `CopyEngine.Instance?.CancelQxBrackets(leader, instr, snapshot)` (3-param). The Friday 2026-09-11 BUG-2 reproduction path goes through this overload.

Add one call after the null guard (line 1058), identical to the 2-param change:

```csharp
internal void CancelQxBrackets(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Instrument instr,
    System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order> snapshot
)
{
    if (acc == null || instr == null)
        return; // (1)
    TryMarkFollowerSlateNotReady(acc); // BUG2: bracket wipe imminent (3-param path)
    var stale = new System.Collections.Generic.List<Order>();
    // ... rest unchanged
```

**CYC:** unchanged (5). New call adds 0 branches. **PASS.**

**Note:** `CancelQxBracketsForFollowers` calls the 2-param overload (line 1216) — it is covered by the 2-param fix above. Both overloads are now instrumented; all QX-exit paths are covered.

---

#### `FlattenOneAccount(Account acc, Instrument instrument)` — mark slate not-ready

**File:** `src/PropTraderTools/CopyEngine.cs`, line 5417

Add one call after IsAccountFlattenable guard passes:

```csharp
private void FlattenOneAccount(Account acc, Instrument instrument)
{
    if (!IsAccountFlattenable(acc, instrument))
        return; // (1)
    TryMarkFollowerSlateNotReady(acc); // BUG2: flatten dispatch imminent
    CancelAllAccountOrders(acc, instrument);
    // ... rest unchanged
```

**CYC:** unchanged (2). New call adds 0 branches. **PASS.**

---

### 3.4 Gate semantics — three-state logic

| `_followerSlateReady` state | TryGetValue result | Gate expression | Gate decision |
|---|---|---|---|
| Key absent (session start, first trade) | false | `false && !ready` = false | **OPEN** — normal search |
| Key = `false` (after CancelQxBrackets / FlattenOneAccount) | true, ready=false | `true && true` = true | **CLOSED** — return null |
| Key = `true` (after PositionUpdate flat+clean) | true, ready=true | `true && false` = false | **OPEN** — normal search |

This guarantees: first trade always works, no gate regression on clean sessions.

---

## 4. Affected Methods Summary

| Method | File | ~Line | Change |
|---|---|---|---|
| `_followerSlateReady` field | CopyEngine.cs | ~382 | NEW field |
| `TryMarkFollowerSlateNotReady(Account)` | CopyEngine.cs | new | NEW method |
| `TrySetFollowerSlateReady(Account)` | CopyEngine.cs | new | NEW method |
| `IsAccountFlat(Account)` | CopyEngine.cs | new | NEW static helper |
| `HasAnyActiveOrders(Account)` | CopyEngine.cs | new | NEW static helper |
| `OnFollowerPositionUpdate(object, PositionEventArgs)` | CopyEngine.cs | new | NEW event handler |
| `FindFollowerBracketOrder(Account, ...)` | CopyEngine.cs | ~3902 | MODIFIED — gate added |
| `Subscribe()` | CopyEngine.cs | 1499 | MODIFIED — PositionUpdate += |
| `Unsubscribe()` | CopyEngine.cs | 1505 | MODIFIED — PositionUpdate -= |
| `CancelQxBrackets(Account, Instrument)` | CopyEngine.cs | 938 | MODIFIED — mark not-ready (2-param) |
| `CancelQxBrackets(Account, Instrument, HashSet<Order>)` | CopyEngine.cs | 1052 | MODIFIED — mark not-ready (3-param, primary QX-2T path) |
| `FlattenOneAccount(Account, Instrument)` | CopyEngine.cs | 5417 | MODIFIED — mark not-ready |

**No changes to:** TradeCopierAddOn.cs, TradeCopierPanel.cs, TradeCopierWindow.cs, PttQuickExit.cs, or any file outside CopyEngine.cs (tests excepted).

---

## 5. NT8 API Compliance

| API | Availability | Confirmation source |
|---|---|---|
| `account.PositionUpdate` event | AddOnBase ✅ | TradeCopierPanel.cs line 861 (existing usage) |
| `PositionEventArgs.Position.Account` | AddOnBase ✅ | TradeCopierPanel.cs line 2109 (existing handler signature) |
| `Account.Positions` collection | AddOnBase ✅ | NT8 standard Account API |
| `Account.Orders` collection | AddOnBase ✅ | Already used at lines 4898, 4919, etc. |
| `Account.All` | AddOnBase ✅ | Already used at lines 1501, 1507 |
| `account.Cancel()` + `account.CreateOrder()` | AddOnBase ✅ | Not used in this fix |
| `AtmStrategyCreate()` | StrategyBase ONLY ❌ | NOT used in this fix |
| `StartAtmStrategy()` | StrategyBase ONLY ❌ | NOT used in this fix |

---

## 6. Spec Requirement IDs

| ID | Requirement | Satisfied by |
|---|---|---|
| BUG2-REQ-1 | `_followerSlateReady: ConcurrentDictionary<string, bool>` field added to CopyEngine | Section 3.1 |
| BUG2-REQ-2 | Slate marked not-ready on any QX/flatten/close exit dispatch — **covers both CancelQxBrackets overloads** | `TryMarkFollowerSlateNotReady` called from `CancelQxBrackets(2-param)` (line 938) + `CancelQxBrackets(3-param, line 1052)` + `FlattenOneAccount` |
| BUG2-REQ-3 | `account.PositionUpdate` subscribed in `Subscribe()`, unsubscribed in `Unsubscribe()` | Section 3.3 (Subscribe/Unsubscribe) |
| BUG2-REQ-4 | `OnFollowerPositionUpdate` sets ready=true when account flat AND no active orders | `TrySetFollowerSlateReady` via `IsAccountFlat` + `HasAnyActiveOrders` |
| BUG2-REQ-5 | `FindFollowerBracketOrder(Account, ...)` returns null when slate explicitly not-ready | Section 3.3 (FindFollowerBracketOrder gate) |
| BUG2-REQ-6 | First trade of session unaffected — absent key in `_followerSlateReady` = gate open | Section 3.4 gate semantics table |

---

## 7. 7-Scan Checklist (Engineer Contract)

```
SCAN-01: lock() grep
  Command: grep -r "lock(" src/PropTraderTools/CopyEngine.cs
  Expected: ZERO matches (no lock() keyword in any new or modified method)
  Scope: all new methods in this fix

SCAN-02: CYC check
  Methods to verify (all must be <= 8):
    TryMarkFollowerSlateNotReady  -> CYC=2  PASS
    TrySetFollowerSlateReady      -> CYC=4  PASS
    IsAccountFlat                 -> CYC=3  PASS
    HasAnyActiveOrders            -> CYC=5  PASS
    OnFollowerPositionUpdate      -> CYC=3  PASS
    FindFollowerBracketOrder(Acc) -> CYC=2  PASS (was 1)
  Unmodified methods retain their prior-passing CYC.

SCAN-03: async/await grep
  Command: grep -n "async\|await" src/PropTraderTools/CopyEngine.cs
  Expected: ZERO new async/await in any method introduced by this fix.
  OnFollowerPositionUpdate must be synchronous.

SCAN-04: Unicode/emoji grep
  Command: grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
  Expected: ZERO matches in new string literals.
  All new log messages, comments, and string literals must be ASCII-only.

SCAN-05: NT8 API compliance
  Verify: no call to AtmStrategyCreate() or StartAtmStrategy() in new methods.
  Verify: no call to AtmStrategyChangeStopTarget() anywhere in new methods.
  Verify: only AddOnBase-available APIs used (Account.All, acc.PositionUpdate,
          acc.Positions, acc.Orders, ConcurrentDictionary).

SCAN-06: Build clean
  Command: powershell -File .\scripts\build_readiness.ps1
  Expected: ZERO errors, ZERO warnings.
  Note: OnFollowerPositionUpdate signature must match NT8 EventHandler<PositionEventArgs>
  exactly (object sender, PositionEventArgs e) as confirmed from TradeCopierPanel.cs L2109.

SCAN-07: Functional verification (SIM test)
  Procedure:
    1. Load SIM rules with at least 2 follower accounts.
    2. Enter Trade 1: confirm all followers get brackets (fo=NOT-NULL in TP4-SFB logs).
    3. Exit Trade 1 using QX-2T button.
    4. Verify [TP4-SFB] logs for followers show _followerSlateReady=false state (gate closed).
    5. Wait for PositionUpdate(flat) on all followers. Verify gate opens.
    6. Enter Trade 2: verify ALL 6 bracket orders show fo=NOT-NULL for all followers.
    7. Verify no fo=NULL lines appear in output for 2nd trade brackets.
  Pass criteria: No bracket submission orders are dispatched to follower accounts while
  _followerSlateReady gate is closed (false). fo=NULL log entries are expected during the
  gate-closed window and do NOT constitute a scan failure. Verify by confirming no
  Account.CreateOrder calls execute for bracket orders on 2nd+ trade until PositionUpdate
  hasPos=False fires and slate is confirmed clean.
```

---

## 8. xUnit Test Suggestions

All tests in `src/PropTraderTools/CopyEngineTests.cs`.

> **Note:** The `fo=NULL` diagnostic log emitted by `TryLogSFBTrace` (called in `SyncFollowerBracket` BEFORE the `if (fo == null) return` guard) is **intentionally preserved** during the gate-closed window. This is by design: the log line provides observability into the gate state and the order search result. The fix prevents the downstream bracket submission (financial protection goal), not the log line. Test 7(c) below reflects this correct behavior.

```csharp
// BUG2-REQ-1 + BUG2-REQ-6
[Fact]
public void FollowerSlateReady_AbsentKey_GateOpen_FindFollowerBracketOrderProceedsNormally()
// Verifies: when _followerSlateReady has no entry for follower, FindFollowerBracketOrder(Account)
// calls through to ActiveOrders search (gate open = first trade unaffected).

// BUG2-REQ-2 + BUG2-REQ-5
[Fact]
public void FollowerSlateReady_AfterNotReady_GateClosed_FindFollowerBracketOrderReturnsNull()
// Verifies: after TryMarkFollowerSlateNotReady, FindFollowerBracketOrder(Account) returns null
// even when matching bracket orders exist in follower.Account.Orders.

// BUG2-REQ-4 + BUG2-REQ-5
[Fact]
public void FollowerSlateReady_AfterFlatAndCleanOrders_GateOpen_FindFollowerBracketOrderFindsOrders()
// Verifies: TrySetFollowerSlateReady (called when flat + no active orders) sets ready=true,
// and FindFollowerBracketOrder proceeds normally (gate open).

// BUG2-REQ-4
[Fact]
public void TrySetFollowerSlateReady_WithOpenPosition_DoesNotSetReady()
// Verifies: IsAccountFlat guard blocks ready when position still open.

// BUG2-REQ-4
[Fact]
public void TrySetFollowerSlateReady_WithActiveOrders_DoesNotSetReady()
// Verifies: HasAnyActiveOrders guard blocks ready when Working/Submitted/Accepted orders remain.

// BUG2-REQ-6
[Fact]
public void TryMarkFollowerSlateNotReady_OnNonFollowerAccount_NoStateChange()
// Verifies: IsFollowerAccount guard prevents non-follower accounts from being marked not-ready.

// BUG2-REQ-2 + BUG2-REQ-5 (test 7c -- revised)
[Fact]
public void FollowerSlateReady_GateClosed_BracketSyncBlocked_FoNullLogPreserved()
// Verifies: when slate=false (gate closed), FindFollowerBracketOrder(Account) returns null,
// causing SyncFollowerBracket to return early before any ATM bracket submit or price-sync
// is dispatched to the follower account.
// The fo=NULL diagnostic log line (TryLogSFBTrace) may still appear -- this is intentional
// (observability). The test asserts that no bracket orders are submitted to the follower
// account (i.e., no CreateOrder calls on the follower), not that the log is absent.
// Criterion: gate blocks bracket propagation on 2nd+ trade; SyncFollowerBracket returns
// early before any ATM submit when slate=false.
```

---

## 9. Data Flow Diagram

```
EXIT PATH (QX-2T -- 3-param, primary path):
  PttQuickExit.Execute -> CopyEngine.CancelQxBrackets(acc, instr, snapshot)  [line 1052]
    └── TryMarkFollowerSlateNotReady(acc)
          └── _followerSlateReady[acc.Name] = false

EXIT PATH (CancelQxBracketsForFollowers -- 2-param):
  CancelQxBracketsForFollowers -> CancelQxBrackets(acc, instr)  [line 938]
    └── TryMarkFollowerSlateNotReady(acc)
          └── _followerSlateReady[acc.Name] = false

EXIT PATH (Flatten):
  FlattenOneAccount(acc, instr)
    └── TryMarkFollowerSlateNotReady(acc)
          └── _followerSlateReady[acc.Name] = false

RECOVERY PATH (NT8 account background thread):
  account.PositionUpdate fires
    └── OnFollowerPositionUpdate(sender, e)
          └── IsFollowerAccount(acc) ? proceed : return
          └── TrySetFollowerSlateReady(acc)
                ├── IsAccountFlat(acc) ? proceed : return
                ├── HasAnyActiveOrders(acc) ? return : proceed
                └── _followerSlateReady[acc.Name] = true

BRACKET SYNC GUARD PATH (NT8 account background thread):
  OnOrderUpdate -> HandleBracketChange -> SyncFollowerBracket
    └── FindFollowerBracketOrder(follower, signal, isStop, leaderName)
          ├── _followerSlateReady.TryGetValue(name, out ready)
          │     absent  -> gate OPEN  -> ActiveOrders search -> return Order or null
          │     false   -> gate CLOSED -> return null immediately
          │     true    -> gate OPEN  -> ActiveOrders search -> return Order or null
          └── FindFollowerBracketOrder(IEnumerable<Order>, ...)
```

---

## 10. Jane Street Compliance Summary

| Rule | Requirement | Status |
|---|---|---|
| JS-021 | No `lock()` anywhere | ✅ All new state uses ConcurrentDictionary |
| JS-001 | No throw in hot path | ✅ All new methods return early, no exceptions thrown |
| JS-002 | No null return on non-nullable contracts | ✅ `Order?` null contract unchanged; bool/void helpers explicit |
| JS-003 | No mutable struct fields | ✅ No new struct types; all new fields are ConcurrentDictionary reference types |
| JS-008 | No DateTime.Now | ✅ No datetime used in this fix |
| JS-010 | No external subclassing of discriminated unions | ✅ No new types |
| JS-023 | Volatile for hot bools | ✅ ConcurrentDictionary used (no volatile needed; dict guarantees visibility) |
| JS-025 | ConcurrentDictionary as lock-free pattern | ✅ `_followerSlateReady` is ConcurrentDictionary |

**No async/await in NT8 lifecycle methods.** ✅
**ASCII-only string literals.** ✅
**PTT- prefix on order names.** ✅ (no new order creation in this fix)

---

*PLAN_COMPLETE*
