# B53-LaneC Architecture Plan — Cancel Propagation
# Status: REVIEW_PASS (pending review)
# Epic: DW-B53-03
# Wave workspace: C:\WSGTA\universal-or-strategy\src\PropTraderTools\

---

## 1. Summary of Changes

When a leader entry order reaches `OrderState.Cancelled`, all matching `"PTT-Copy"` working or
accepted follower orders for the same instrument are automatically cancelled via `acc.Cancel()`.

**Prerequisite state (LaneA final-pass confirmed):**
- `PttFollowerStrategy` removed; follower orders are placed directly by the AddOn (`CopyEngine`).
- Follower entry orders are named `"PTT-Copy"`, owned by the AddOn account. `acc.Cancel()` resolves cleanly.
- LaneB has NOT run — `FindFollowerWorkingEntry` does NOT yet exist in `CopyEngine.cs`.

**Scope: `CopyEngine.cs` only (1 file). `CopyEngineTests.cs` (2 new tests).**

---

## 2. Component List

| Component | Kind | File | New/Modified |
|---|---|---|---|
| `PttBuild.Tag` | const string | `CopyEngine.cs` | Modified |
| `IsLeaderEntryCancelled` | `internal static bool` | `CopyEngine.cs` | New |
| `FindFollowerWorkingEntry` | `internal static Order` (nullable) | `CopyEngine.cs` | New |
| `CancelFollowerEntryOrders` | `private void` | `CopyEngine.cs` | New |
| `DispatchAfterRuleMatch` | `private void` | `CopyEngine.cs` | New (extraction) |
| `OnOrderUpdate` | `private void` | `CopyEngine.cs` | Modified |
| `T_B53C_01` | `[Fact]` | `CopyEngineTests.cs` | New |
| `T_B53C_02` | `[Fact]` | `CopyEngineTests.cs` | New |

---

## 3. Method Signatures with CYC Annotations

### 3.1 `PttBuild.Tag` update

```csharp
internal const string Tag = "PTT-COPIER B53 | cancel-propagation | 2026-08-10";
```

**Change:** from `"PTT-COPIER B53 | remove-follower-strategy | 2026-08-09"`.

---

### 3.2 `IsLeaderEntryCancelled`

```csharp
// B53-LaneC DW-B53-03: predicate -- true when leader entry order is genuinely cancelled.
// CYC=3: (1) OrderState check, (2) IsBracketLegStatic check, (3) name+account guard.
// internal static for testability via [InternalsVisibleTo("CopyEngineTests")].
// JS-002: returns bool (no null). JS-021: no lock. JS-001: no throw.
// NT8: calls IsBracketLegStatic (not IsBracketLeg) -- static context requires static helper.
internal static bool IsLeaderEntryCancelled(Order order, CopyRule rule)
{
    if (order.OrderState != OrderState.Cancelled)          // (1)
        return false;
    if (IsBracketLegStatic(order))                         // (2)
        return false;
    return order.Name != "PTT-Copy"                        // (3) identity guard
        && order.Account.Name == rule.MasterAccount.Name;
}
```

**Placement:** After `IsBracketLegStatic` in `CopyEngine.cs` (approximately line 1532 area).

**CYC = 3.** Three decision points: Cancelled check, bracket check, name+account compound guard.

**NT8 note:** `IsBracketLegStatic` (not `IsBracketLeg`) is called because this is a `static` method.
`IsBracketLegStatic` already exists in `CopyEngine.cs` (line ~1532) and checks:
`order.FromEntrySignal != null || order.Name.StartsWith("Stop") || order.Name.StartsWith("Target")`.

---

### 3.3 `FindFollowerWorkingEntry`

```csharp
// B53-LaneC DW-B53-03: find a working/accepted PTT-Copy entry order on the given account/instrument.
// CYC=3: (1) foreach loop, (2) name+state filter, (3) instrument match.
// internal static for testability via [InternalsVisibleTo("CopyEngineTests")].
// JS-002: returns null when no match found -- null MUST be checked at call site (not propagated).
// JS-021: no lock. acc.Orders.ToList() snapshot prevents collection-modified exceptions (NT8 pattern).
// NT8-031: no OrderState.PendingSubmit -- uses Working and Accepted only.
internal static Order FindFollowerWorkingEntry(Account acc, Instrument instrument)
{
    foreach (var order in acc.Orders.ToList())                             // (1) foreach
    {
        if (order.Name != "PTT-Copy")                                      // (2) name filter
            continue;
        if (order.OrderState != OrderState.Working
            && order.OrderState != OrderState.Accepted)                    // (2) state filter
            continue;
        if (order.Instrument != instrument)                                // (3) instrument match
            continue;
        return order;
    }
    return null;
}
```

**Placement:** Near `FindFollowerBracketOrder` in `CopyEngine.cs`.

**CYC = 3.** Foreach loop body + state guard + instrument guard. Returns first match or null.

**JS-002 compliance:** null returned — caller (`CancelFollowerEntryOrders`) checks `if (found == null) continue`. Null is NOT propagated up the call chain.

**LaneB reuse:** If LaneB runs in a future block, it will reuse this method directly (no duplication).

---

### 3.4 `CancelFollowerEntryOrders`

```csharp
// B53-LaneC DW-B53-03: cancel matching PTT-Copy entry orders on all follower accounts.
// CYC=4: (1) foreach loop, (2) acc null guard, (3) found null check, (4) try/catch.
// JS-001: try/catch wraps acc.Cancel -- no throw in hot path.
// JS-002: null from FindFollowerWorkingEntry is checked here -- not propagated.
// JS-021: no lock. acc.Cancel is the NT8 broker API (thread-safe, order thread context).
// NT8: acc.Cancel takes Order[] array (not a single Order) -- per NT8-007 pattern.
private void CancelFollowerEntryOrders(Order order, CopyRule rule)
{
    foreach (var acc in rule.FollowerAccounts)                             // (1) foreach
    {
        if (acc == null)                                                   // (2) acc null guard
            continue;
        var found = FindFollowerWorkingEntry(acc, order.Instrument);
        if (found == null)                                                 // (3) JS-002 null check
            continue;
        try                                                                // (4) try/catch
        {
            acc.Cancel(new Order[] { found });
            StatusUpdate?.Invoke("Follower cancel: " + acc.Name + " " + order.Instrument.FullName);
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke("PTT-Cancel error: " + acc.Name + ": " + ex.Message);
        }
    }
}
```

**Placement:** Near `CancelOneAccount` in `CopyEngine.cs`.

**CYC = 4.** Foreach + acc null + found null + try/catch = 4 decision points.

**Does NOT touch ATM bracket orders.** `FindFollowerWorkingEntry` filters by `Name == "PTT-Copy"` — ATM-managed orders (Stop1, Stop2, Target1, etc.) are never returned.

---

### 3.5 `DispatchAfterRuleMatch` (extraction helper)

This method is extracted from the post-Gate-2.5 block of `OnOrderUpdate` to maintain `OnOrderUpdate` at CYC <= 8 after inserting the cancel propagation branch.

```csharp
// B53-LaneC: extracted from OnOrderUpdate post-Gate-2.5 to maintain CYC<=8.
// CYC=3: (1) mirror relay branch, (2) cancel propagation branch, (3) IsWorkingBracket branch.
// JS-021: no lock. JS-001: no throw (delegates to methods with own try/catch).
private void DispatchAfterRuleMatch(Order order, CopyRule rule)
{
    // B9 T3 -- Mirror mode relay (before cancel check -- per AD-4)
    if ((CopyMode)_copyModeValue == CopyMode.Mirror)                      // (1)
        MirrorOrderUpdate(order, rule);

    // B53-LaneC DW-B53-03: cancel propagation -- fires before Gate B.
    // Bypasses IsDedup entirely -- cancels are always dispatched.
    if (IsLeaderEntryCancelled(order, rule))                              // (2)
    {
        CancelFollowerEntryOrders(order, rule);
        return;
    }

    // Gate B: bracket drag detection -- divert to HandleBracketChange path
    if (IsWorkingBracket(order))                                          // (3)
    {
        if (order.FromEntrySignal != null)
            PopulateOrderMap(order.FromEntrySignal, order.Account);
        HandleBracketChange(order, rule);
        return;
    }

    // No bracket, not a cancel -- normal copy dispatch
    DispatchCopy(order, rule);
}
```

**CYC = 3.**

---

### 3.6 `OnOrderUpdate` modification

The post-Gate-2.5 inline block is replaced with a single `DispatchAfterRuleMatch` call.

**Before (existing body, CYC=8):**
```csharp
// Gate 2.5: per-rule enable check
if (!matchedRule.Value.Enabled)
    return;

// B9 T3 -- Mirror mode relay (inserted after Gate 2.5, before Gate B)
if ((CopyMode)_copyModeValue == CopyMode.Mirror)
    MirrorOrderUpdate(e.Order, matchedRule.Value);

// Gate B: bracket drag detection -- divert to HandleBracketChange path
if (IsWorkingBracket(e.Order))
{
    if (e.Order.FromEntrySignal != null)
        PopulateOrderMap(e.Order.FromEntrySignal, e.Order.Account);
    HandleBracketChange(e.Order, matchedRule.Value);
    return;
}

// No bracket -- normal copy dispatch
DispatchCopy(e.Order, matchedRule.Value);
```

**After (LaneC, CYC maintained <= 8):**
```csharp
// Gate 2.5: per-rule enable check
if (!matchedRule.Value.Enabled)
    return;

// B53-LaneC: delegates to DispatchAfterRuleMatch (mirror relay + cancel check + Gate B + dispatch).
DispatchAfterRuleMatch(e.Order, matchedRule.Value);
```

**OnOrderUpdate CYC after LaneC:** Count of remaining decision points:
1. `TryFirePositionState` — no branch
2. Gate 1: `!_isCopyEnabled` — branch (1)
3. Follower-fill guard compound — branch (2)
4. Gate 2 foreach match — branch (3)
5. `matchedRule == null` — branch (4)
6. Gate 2.5 `!Enabled` — branch (5)
7. `DispatchAfterRuleMatch` call — no branch (straight call)

**CYC = 5. Compliant (was 8, now 5 via extraction).**

**Updated comment:** `// --- Hot path: CYC=5 (B53-LaneC extracted DispatchAfterRuleMatch; base was 7 B7-F0, +1 LaneA=8) ---`

---

## 4. Insertion Point in `OnOrderUpdate`

```
OnOrderUpdate body (sequential):
  pre-gate: TryFirePositionState(e)          [unchanged]
  Gate 1:   !_isCopyEnabled → return         [unchanged]
  Guard:    follower-fill ATM attach → return [unchanged, LaneA]
  Gate 2:   foreach rule match               [unchanged]
  Guard:    matchedRule == null → return      [unchanged]
  Gate 2.5: !rule.Enabled → return           [unchanged]
  *** REPLACED: DispatchAfterRuleMatch(e.Order, matchedRule.Value) ***
    (contains mirror relay → cancel check → Gate B → DispatchCopy)
```

The cancel propagation check is the 2nd branch inside `DispatchAfterRuleMatch`, after Mirror mode relay and before Gate B. This ensures:
- Cancel fires even in Mirror mode (mirror relay runs first, then cancel check fires).
- Cancel is completely transparent to `DispatchCopy` — the early `return` after `CancelFollowerEntryOrders` prevents `DispatchCopy` from seeing the cancel event.
- `IsDedup` is bypassed entirely for cancel events.

---

## 5. Data Flow

```
Leader entry order → OrderState.Cancelled
    → NT8 fires acc.OrderUpdate on order thread
    → OnOrderUpdate(sender, e)
        → TryFirePositionState (fires PositionStateChanged)
        → Gate 1 (_isCopyEnabled): TRUE
        → Follower-fill guard: FALSE (not Filled, not "PTT-Copy")
        → Gate 2: instrument + master account match → matchedRule found
        → Gate 2.5: rule.Enabled TRUE
        → DispatchAfterRuleMatch(order, rule):
            → Mirror relay: (if Mirror mode) MirrorOrderUpdate -- called first
            → IsLeaderEntryCancelled(order, rule): TRUE
                → CancelFollowerEntryOrders(order, rule):
                    → foreach follower acc:
                        → FindFollowerWorkingEntry(acc, order.Instrument)
                            → finds "PTT-Copy" Working/Accepted order for instrument
                            → returns Order (or null)
                        → null check: skip if no follower copy
                        → acc.Cancel(new Order[] { found })
                        → StatusUpdate?.Invoke("Follower cancel: ...")
                → return (early return, Gate B and DispatchCopy never reached)
```

---

## 6. JS Rule Compliance Table

| Rule | Description | Status in LaneC |
|---|---|---|
| JS-001 | No throw in hot paths | `CancelFollowerEntryOrders` wraps `acc.Cancel` in `try/catch`. `StatusUpdate` logs error. PASS |
| JS-002 | No return null for missing values | `FindFollowerWorkingEntry` returns nullable `Order`. Null checked at call site in `CancelFollowerEntryOrders` via `if (found == null) continue`. NOT propagated. PASS |
| JS-010 | Smart constructor / no public default constructor for types | No new types created. PASS |
| JS-021 | No `lock()` | No `lock` anywhere. `acc.Cancel` is NT8 broker API (thread-safe). PASS |
| JS-023 | Cross-thread fields must be `volatile` | No new instance fields added. PASS |
| JS-025 | No lock, use concurrent collections | No new state storage. PASS |
| JS-033 | No `async void` | All new methods are synchronous. PASS |
| JS-003 | `readonly struct` prevents field transposition | No new structs. Existing `CopyRule` is readonly struct. PASS |

---

## 7. NT8 Compiler Rule Checklist

| Rule | Check | Result |
|---|---|---|
| NT8-001 | `{ get; init; }` banned | No new properties with `init`. PASS |
| NT8-002 | `abstract record` / `sealed record` banned | No records. PASS |
| NT8-003 | `volatile double` banned | No new fields. PASS |
| NT8-004 | `ImmutableDictionary` banned | Not used. PASS |
| NT8-005 | `readonly struct` with `private set` banned | No new structs. PASS |
| NT8-007 | `acc.Cancel` takes `Order[]` not `string` | `acc.Cancel(new Order[] { found })` — correct. PASS |
| NT8-013 | `DateTime.Now` banned for order expiry | No `DateTime.Now` in new code. PASS |
| NT8-014 | Signal name must start with `"PTT-"` | No new `CreateOrder` calls in LaneC. PASS |
| NT8-018 | `lock()` banned | No `lock`. PASS |
| NT8-019 | `async void` banned | No async methods. PASS |
| NT8-031 | `OrderState.PendingSubmit` does not exist | Using `Working` and `Accepted` only. PASS |
| NT8-042 | `Dispatcher.InvokeAsync` unavailable | Not used. `StatusUpdate` delegate used instead. PASS |
| NT8-043 | Null-conditional compound assignment banned (`?.` with `-=`) | Not used. PASS |
| NT8-044 | `StringComparison` requires `using System;` | Not using `StringComparison`. String `==` used. PASS |

---

## 8. Test Contract Definitions

### T_B53C_01 — `IsLeaderEntryCancelled_LeaderCancelledEntry_ReturnsTrue`

**File:** `CopyEngineTests.cs`
**Class:** `CopyEngineTests`

**Setup:**
- A stub/mock `Order` with:
  - `OrderState = OrderState.Cancelled`
  - `Name = "LeaderOrder"` (not "PTT-Copy", not starting with "Stop" or "Target")
  - `Account.Name = "Sim101"`
  - `FromEntrySignal = null` (makes `IsBracketLegStatic` return false)
- A `CopyRule` created via `CopyRule.Create(...)` with `MasterAccount.Name = "Sim101"`.

**Assert:**
```csharp
Assert.True(CopyEngine.IsLeaderEntryCancelled(order, rule));
```

**What it verifies:** When a genuine leader entry order (not bracket, not follower) reaches Cancelled, `IsLeaderEntryCancelled` returns true.

---

### T_B53C_02 — `IsLeaderEntryCancelled_BracketLeg_ReturnsFalse`

**File:** `CopyEngineTests.cs`
**Class:** `CopyEngineTests`

**Setup:**
- Same as T_B53C_01 but with `FromEntrySignal = "some-signal"` (makes `IsBracketLegStatic` return true — bracket leg detected).

**Assert:**
```csharp
Assert.False(CopyEngine.IsLeaderEntryCancelled(order, rule));
```

**What it verifies:** Bracket legs (stop/target legs) are not treated as leader entries — cancel propagation is suppressed for them.

---

## 9. Architecture Decisions

| # | Decision | Rationale |
|---|---|---|
| AD-1 | `IsLeaderEntryCancelled` calls `IsBracketLegStatic` (not `IsBracketLeg`) | It is a `static` method; `IsBracketLeg` is an instance method. `IsBracketLegStatic` already exists for this exact purpose (line ~1532, used by `IsWorkingBracket`). |
| AD-2 | `FindFollowerWorkingEntry` is `internal static` | Shared helper for reuse by future LaneB without duplication. Testable via `[InternalsVisibleTo]`. |
| AD-3 | `DispatchAfterRuleMatch` extraction is required | `OnOrderUpdate` was at CYC=8. Adding the cancel check would reach CYC=9, violating the Jane Street CYC<=8 mandate. Extraction reduces `OnOrderUpdate` to CYC=5 and keeps `DispatchAfterRuleMatch` at CYC=3. |
| AD-4 | Cancel check is placed AFTER Mirror mode relay | Per spec AD-4: cancel fires even during Mirror mode. Mirror relay runs first so that mirror-close on a cancelled entry (if any) can still fire. Cancel propagation then fires and returns early. |
| AD-5 | `CancelFollowerEntryOrders` iterates `rule.FollowerAccounts` | No new fields, no lock, no ConcurrentDictionary needed. Rule is a readonly struct passed by value — thread safe. |
| AD-6 | `acc.Orders.ToList()` snapshot in `FindFollowerWorkingEntry` | Matches existing pattern in `FindFollowerBracketOrder` — prevents collection-modified exceptions from concurrent NT8 order updates during iteration. |
| AD-7 | Early `return` after `CancelFollowerEntryOrders` | Prevents `IsDedup`, Gate B, and `DispatchCopy` from processing a cancel event. Cancel propagation is always dispatched — no dedup suppression. |

---

## 10. 7-Scan Checklist (SCAN-01 through SCAN-07) — Engineer Contract

| Scan | Check | Expected Result |
|---|---|---|
| SCAN-01 | `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` | Zero matches |
| SCAN-02 | `grep -rn "async void" src/PropTraderTools/CopyEngine.cs` | Zero matches (existing patterns only, no new) |
| SCAN-03 | `grep -rn "return null" src/PropTraderTools/CopyEngine.cs` | `FindFollowerWorkingEntry` has exactly 1 `return null` — verify it is the only new one added, and its call site has `if (found == null) continue` |
| SCAN-04 | `grep -rn "DateTime.Now" src/PropTraderTools/CopyEngine.cs` | Zero matches |
| SCAN-05 | `grep -n "PTT-" src/PropTraderTools/CopyEngine.cs` | `PttBuild.Tag` updated to `"PTT-COPIER B53 | cancel-propagation | 2026-08-10"`. `StatusUpdate` strings use `"PTT-Cancel error:"` prefix (ASCII only). |
| SCAN-06 | `grep -rn "OrderState.PendingSubmit" src/PropTraderTools/CopyEngine.cs` | Zero matches |
| SCAN-07 | `grep -rn "IsBracketLeg(" src/PropTraderTools/CopyEngine.cs` | `IsLeaderEntryCancelled` calls `IsBracketLegStatic` (not `IsBracketLeg`). Verify no `IsBracketLeg` call inside a static method. |

---

## 11. Spec Requirement IDs Satisfied

| Requirement | Source | Satisfied By |
|---|---|---|
| DW-B53-03 | B53-LaneC change spec | `IsLeaderEntryCancelled` + `FindFollowerWorkingEntry` + `CancelFollowerEntryOrders` + `DispatchAfterRuleMatch` + `OnOrderUpdate` modification |
| Cancel fires before Gate B | Spec insertion point | `DispatchAfterRuleMatch`: cancel check is branch (2), Gate B is branch (3) |
| Cancel bypasses IsDedup | Spec constraint | Early `return` after `CancelFollowerEntryOrders` — never reaches `DispatchCopy` which contains `IsDedup` |
| PttBuild.Tag updated | Spec change | Tag → `"PTT-COPIER B53 | cancel-propagation | 2026-08-10"` |
| 2 new [Fact] tests | Test contract | `T_B53C_01`, `T_B53C_02` in `CopyEngineTests.cs` |

---

*Plan written by ptt-architect. Status: awaiting ptt-plan-reviewer.*
