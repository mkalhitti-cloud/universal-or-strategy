# PTT-COPIER-B20-LANE-A -- Architecture Plan
# Phase 1 output (ptt-architect)
# Status: PLAN_COMPLETE
# Date: 2026-07-14

---

## §1 Block Summary

**Block**: PTT-COPIER-B20-LANE-A
**Lane purpose**: Close two P2 deferred items targeting `CopyEngine.cs` only.
- **T1** (DW-B19-02): Fix `PopulateOrderMap` dedup guard — replace object reference equality
  with name-string equality so the guard survives Rithmic account reconnect.
- **T2** (DW-B17-SYNC-01): Add `CopyEnabledChanged` event to `CopyEngine` so downstream
  subscribers can receive the boolean enabled state directly, eliminating the need to parse
  the `StatusUpdate` string for ON/OFF state.

Both tickets are surgical: T1 is a one-line predicate change; T2 adds one event field and
one invoke statement. No new methods, no new types, no scope expansion into Panel/Window.

---

## §2 Files In Scope

| File | Role | Changes |
|------|------|---------|
| `src/PropTraderTools/CopyEngine.cs` | Production — CopyEngine singleton | T1: 1-line predicate edit (line 659). T2: 1 event field added (after line 125) + 1 invoke line in SetEnabled (after line 234). |
| `src/PropTraderTools/CopyEngineTests.cs` | xUnit test suite | T1: 1 new [Fact] method. T2: 1 new [Fact] method. |

Wave workspace root: `c:\WSGTA\universal-or-strategy`

---

## §3 Files NOT In Scope

| File | Reason |
|------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` | Panel wiring for `CopyEnabledChanged` is Lane B work. Out of scope for Lane A. |
| `src/PropTraderTools/TradeCopierWindow.cs` | Window sync is Lane B work. Out of scope. |
| `src/PropTraderTools/TradeCopierAddOn.cs` | No changes required by either ticket. |
| All other `.cs` files | Zero modifications. No scope creep (V12.23). |

---

## §4 T1 Design -- PopulateOrderMap Dedup Guard (DW-B19-02)

### Root Cause

`PopulateOrderMap` (line 653, `CopyEngine.cs`) maintains a `ConcurrentBag<FollowerBinding>` per
entry signal name. The dedup guard at line 659 reads:

```csharp
if (!bag.Any(b => b.FollowerAccount == followerAccount))
```

This uses C# object reference equality on `Account`. After a Rithmic broker reconnect, NT8
internally recreates `Account` objects. The previously-stored `b.FollowerAccount` reference
points to the old (stale) object; `followerAccount` parameter points to the newly-created object.
Reference equality evaluates to `false` even when both represent the same trading account. The
guard misses, and a duplicate `FollowerBinding` is appended to the bag. Over time, multiple
reconnects accumulate ghost bindings, causing bracket lookups to see phantom entries.

### Fix

Change **line 659 only**:

```csharp
// BEFORE (reference equality -- fails after reconnect):
if (!bag.Any(b => b.FollowerAccount == followerAccount))

// AFTER (name equality -- survives reconnect):
if (!bag.Any(b => b.FollowerAccount?.Name == followerAccount?.Name))
```

### Rationale: `?.Name == ?.Name`

1. **Reconnect safety**: `Account.Name` is stable across reconnect (string property returning the
   broker account identifier string, e.g. `"Sim101"`). Name equality correctly identifies the
   same logical account regardless of which object reference NT8 created.

2. **Null-safety proof**:
   - If `b.FollowerAccount` is null: `b.FollowerAccount?.Name` evaluates to `null` (C# null-conditional).
   - If `followerAccount` is null: `followerAccount?.Name` evaluates to `null`.
   - `null == null` evaluates to `true` in C# string equality (reference equality of null, no
     NullReferenceException). Behavior for null-null pair is identical to old reference equality
     (both null = same slot = dedup fires). ✅ Safe.
   - For the non-null typical case: string `==` operator calls `string.Equals` (value equality).
     Same name → dedup fires correctly. ✅

3. **No field or method additions**: The fix is strictly contained within the existing lambda
   expression argument to `bag.Any()`. The method signature, the struct types, and all other
   fields remain unchanged.

4. **CYC unchanged**: The single `if` statement is preserved; only the boolean predicate
   expression changes. CYC = 2 (entry + 1 branch). ✅

---

## §5 T1 Test Design -- `PopulateOrderMap_DedupGuard_UsesNameEquality`

### Pattern: Reflection for Private Method + Shared Singleton Isolation

`PopulateOrderMap` is `private`. The test harness already provides `GetMethod()`:
```csharp
private static MethodInfo GetMethod(string name)
    => typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
```

### Unique Signal Name (Cross-Test Contamination Prevention)

`CopyEngine.Instance` is a singleton shared across the entire test suite. The `_orderMap`
persists between tests. To prevent state bleed from other tests that may have called
`PopulateOrderMap` with generic signal names, the new test MUST use a unique signal name:

```csharp
string signalName = "B20-DEDUP-TEST-" + DateTime.UtcNow.Ticks;
```

This guarantees the bag retrieved from `_orderMap[signalName]` is empty before the test runs.

### Account Instantiation

`Account.Name` has a **public setter** (confirmed pre-condition). Object-initializer syntax is
valid:

```csharp
var a1 = new Account { Name = "Sim101" };
var a2 = new Account { Name = "Sim101" };
```

`a1` and `a2` are different object references (`object.ReferenceEquals(a1, a2) == false`) but
share the same `Name` value.

### Full Test Logic

```
1. Get MethodInfo for PopulateOrderMap via GetMethod("PopulateOrderMap")
2. Assert.NotNull(method)
3. Create unique signalName
4. Create a1 = new Account { Name = "Sim101" }
5. Create a2 = new Account { Name = "Sim101" }   // same Name, different ref
6. Invoke PopulateOrderMap(_engine, signalName, a1)
7. Invoke PopulateOrderMap(_engine, signalName, a2)   // same Name => dedup should fire
8. Read _orderMap via reflection GetField("_orderMap")
9. TryGetValue(signalName, out bag)
10. Assert.True(getResult)                           // bag must exist
11. Assert.Equal(1, bag.Count)                       // dedup guard prevented second Add
```

### `_orderMap` Field Reflection

```csharp
var fi = typeof(CopyEngine).GetField("_orderMap",
    BindingFlags.NonPublic | BindingFlags.Instance);
var orderMap = (ConcurrentDictionary<string, ConcurrentBag<FollowerBinding>>)fi.GetValue(_engine);
```

Because `FollowerBinding` is `internal` to `PropTraderTools`, the cast is valid inside the same
namespace.

---

## §6 T2 Design -- `CopyEnabledChanged` Event (DW-B17-SYNC-01)

### Problem

`SetEnabled(bool enabled)` (line 231) writes `_isCopyEnabled` and fires `StatusUpdate` with a
human-readable string ("Copy ON" / "Copy OFF"). Subscribers that need the boolean state must
parse the string. When external callers (keyboard shortcuts, scripted tests) call `SetEnabled`
independently of the Panel toggle button, the Panel's toggle state and the engine's
`_isCopyEnabled` diverge with no boolean signal available to resync.

### Fix — Two Atomic Additions

**Addition 1**: New event field in the event block, AFTER the `PendingBeFired` declaration
(currently line 125):

```csharp
// B20 T2: Copy enabled state change notification (DW-B17-SYNC-01)
// Fired from SetEnabled after StatusUpdate -- carries the new bool state directly.
// Subscribers: Panel/Window can bind toggle state without parsing StatusUpdate string.
public event Action<bool> CopyEnabledChanged;
```

Placement precision: insert immediately after line 125 (`internal event Action<string> PendingBeFired;`).
The field is `public` to allow Panel and Window to subscribe without `internal` visibility casting.

**Addition 2**: One invoke line in `SetEnabled`, AFTER `StatusUpdate?.Invoke(...)` (line 234):

```csharp
internal void SetEnabled(bool enabled)
{
    _isCopyEnabled = enabled;
    StatusUpdate?.Invoke("Copy " + (enabled ? "ON" : "OFF"));
    CopyEnabledChanged?.Invoke(enabled);   // NEW: DW-B17-SYNC-01
}
```

### Design Decisions

1. **Event vs property**: `event Action<bool>` is the correct NT8 add-on pattern for reactive
   state. No polling required. Matches the existing `StatusUpdate` / `PositionStateChanged` /
   `PendingBeFired` pattern in the same event block.

2. **`public` visibility**: `StatusUpdate` is `internal` (Panel/Window are in same assembly, so
   `internal` is sufficient). However, `public` is chosen for `CopyEnabledChanged` to match
   `PositionStateChanged` visibility and to leave the door open for future cross-assembly
   subscribers. No security concern — it is an event field, not a mutable state field.

3. **Null-conditional invocation**: `CopyEnabledChanged?.Invoke(enabled)` is the standard C#
   null-conditional delegate invocation pattern. It atomically snapshots the delegate reference
   before the null check, preventing a TOCTOU race on subscribe/unsubscribe. No lock() needed.
   JS-021 compliant.

4. **Thread context**: `SetEnabled` is called from the UI thread. The `CopyEnabledChanged` event
   fires on the same UI thread. Panel/Window subscribers receive the callback on the UI thread —
   no `Dispatcher.InvokeAsync` required inside `CopyEngine` for this event. Lane B
   (Panel/Window wiring) may add `Dispatcher.InvokeAsync` guards in subscriber callbacks if they
   perform UI mutations — that is outside Lane A scope.

5. **CYC unchanged**: No new branch. `CopyEnabledChanged?.Invoke(enabled)` is a null-conditional
   expression statement, not an `if` branch. CYC of `SetEnabled` stays at 1.

---

## §7 T2 Test Design -- `SetEnabled_FiresCopyEnabledChanged`

### Pattern: Direct Event Subscription (No Reflection Needed)

`CopyEnabledChanged` is `public`, so the test subscribes directly without reflection:

```csharp
bool? received = null;
Action<bool> handler = v => received = v;
_engine.CopyEnabledChanged += handler;
```

### Assertions

```
SetEnabled(true)  → Assert.Equal(true,  received)
SetEnabled(false) → Assert.Equal(false, received)
```

### Teardown Requirement (Singleton Safety)

Because `_engine` is `CopyEngine.Instance` (shared singleton), the event subscription **MUST**
be torn down after the test to prevent the lambda from accumulating across other tests. The
engineer must unsubscribe in the test body's finally or in `Dispose`:

```csharp
try
{
    // ... SetEnabled calls and assertions ...
}
finally
{
    _engine.CopyEnabledChanged -= handler;
}
```

The existing `IDisposable` pattern in `CopyEngineTests` handles `StatusUpdate` unsubscription.
The same discipline applies here. The engineer may store the handler in a local variable and
unsubscribe in Dispose, or use the try/finally pattern within the test method.

### Full Test Logic

```
1. bool? received = null;
2. Action<bool> handler = v => received = v;
3. _engine.CopyEnabledChanged += handler;
4. try {
5.     _engine.SetEnabled(true);
6.     Assert.Equal(true, received);
7.     _engine.SetEnabled(false);
8.     Assert.Equal(false, received);
9. }
10. finally { _engine.CopyEnabledChanged -= handler; }
```

---

## §8 Deferred Items from B19-L2 Addressed by This Lane

| Spec ID | Description | Ticket | Status After Lane A |
|---------|-------------|--------|---------------------|
| DW-B19-02 | `PopulateOrderMap` dedup guard uses reference equality; fails after Rithmic reconnect. | T1 | **CLOSED** |
| DW-B17-SYNC-01 | Copy ON/OFF state desync: no boolean event from `SetEnabled`; Panel/Window cannot reliably sync toggle state. | T2 | **CLOSED** |

---

## §9 Items Remaining Deferred (Carry-Forward from B19-L2 Open Items)

The following 10 items from `docs/brain/PTT-COPIER-B19-L2/06-deferred-backlog.md` are NOT
addressed by B20-LANE-A and carry forward unchanged:

| ID | Description | Priority | Status |
|----|-------------|----------|--------|
| DW-B9-01 | ATR box visualization on chart canvas. Depends on chart canvas drawing API investigation. | P2 | OPEN |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset for limit price entry. | P3 | OPEN |
| DW-B12-DEFER-01 | Full-panel mode expansion: Buy Ask / Sell Bid quick-entry buttons. | P2 | OPEN |
| DW-B12-DEFER-02 | Auto-trail stop from BE CONNECTED level. | P3 | OPEN |
| DW-B12-DEFER-03 | Correct Math.Clamp ban comment attribution (NT8-003 -> .NET 4.8 constraint); add NT8-031 rule. | P3 | OPEN |
| DW-B12-DEFER-04 | Align CopyEngineTests.cs test names with 04-tickets.md contract names for audit trail. | P3 | OPEN |
| DW-B19L2-DEFER-01 | `ExitBufferTicks` value-object (JS-015): prevent raw `int` crossing Trim/Flatten API boundary. | P2 | OPEN |
| DW-B19L2-DEFER-02 | Spread validation guard in GetAsk/GetBid: reject stale/crossed quotes before placing limit order. | P2 | OPEN |
| DW-B19L2-DEFER-03 | `OnMarketData` event hook in TradeCopierPanel to cache latest ask/bid tick. Eliminates stale-quote risk. | P2 | OPEN |
| DW-B19L2-DEFER-04 | Telemetry: log anchor price (ask/bid value, buffer ticks, limitPx) at each Trim/Flatten CreateOrder call. | P3 | OPEN |

---

## §10 CYC Analysis

### T1 -- `PopulateOrderMap`

```
Method entry:          +1 (base)
if (!bag.Any(...)):    +1 (1 branch)
---
CYC = 2
```

The lambda `_ => new ConcurrentBag<FollowerBinding>()` is a factory delegate, not a control-flow
branch. The predicate `b => b.FollowerAccount?.Name == followerAccount?.Name` is an expression,
not a branch. The null-conditional `?.` operators are expression-level operators, not if-branches.
CYC remains **2** after fix. ✅ Within Jane Street limit of 8.

### T2 -- `SetEnabled`

```
Method entry:          +1 (base)
No if/else/while/for
Ternary in string:     (tool-dependent; director confirms CYC=1)
---
CYC = 1
```

`CopyEnabledChanged?.Invoke(enabled)` is a null-conditional expression statement. No new control-
flow branch. CYC remains **1** after fix. ✅ Within Jane Street limit of 8.

### Unchanged Methods

All other methods in `CopyEngine.cs` are unmodified. No CYC regressions.

---

## §11 JS Rule Compliance

| Rule | Description | T1 Status | T2 Status |
|------|-------------|-----------|-----------|
| JS-021 | No `lock()` in src/ | ✅ PASS — no lock added | ✅ PASS — event invocation uses `?.Invoke` (lock-free) |
| JS-002 | No `return null` in hot paths | ✅ PASS — `PopulateOrderMap` returns `void` | ✅ PASS — `SetEnabled` returns `void` |
| JS-001 | No `throw new XxxException` in hot paths | ✅ PASS — no throw added | ✅ PASS — no throw added |
| JS-023 | `volatile` fields — no misuse | ✅ PASS — `FollowerBinding.FollowerAccount` is not volatile; unchanged | ✅ PASS — no new volatile field; `_isCopyEnabled` volatile write unchanged |
| JS-033 | No `async void` non-event-handlers | ✅ PASS — no async method added | ✅ PASS — no async method added |
| JS-010 | Smart constructor for CopyEngine | ✅ PASS — private constructor at line 227 unchanged | ✅ PASS — no constructor change |
| JS-015 | No unvalidated primitives crossing API | ✅ PASS — no new parameter added | ✅ PASS — `bool enabled` already validated by caller context |
| JS-003 | Readonly struct prevents transposition | ✅ PASS — `FollowerBinding` struct unchanged | ✅ PASS — no struct change |

### Event Invocation Thread Safety Note (JS-021 Extension)

`CopyEnabledChanged?.Invoke(enabled)` uses the null-conditional operator (`?.`). In C#, this
pattern captures a snapshot of the delegate reference before the null check, which is the
compiler-recommended safe event-invocation idiom. It does not require a lock() and is immune
to the subscribe/unsubscribe race on the null check. ✅ JS-021 compliant.

---

## §12 NT8 Constraints

| Rule | Check | Result |
|------|-------|--------|
| NT8-001 | No `{ get; init; }` accessor | ✅ PASS — no new property with init accessor |
| NT8-002 | No `abstract record` / `sealed record` | ✅ PASS — no record types added |
| NT8-003 | No `volatile` on `double` / `long` fields | ✅ PASS — no new volatile field added |
| NT8-004 | No `ImmutableDictionary` | ✅ PASS — no immutable collections added |
| NT8-007 | `CreateOrder` arg 12 as `string` | ✅ PASS — no `CreateOrder` call added |
| NT8-031 | `Math.Clamp` unavailable in .NET 4.8 | ✅ PASS — not used by either ticket |

### Account.Name Public Setter -- Confirmed Pre-Condition

The T1 test uses object-initializer syntax `new Account { Name = "Sim101" }`. This requires
`Account.Name` to have a public setter. This pre-condition is **confirmed** by the director
brief (PTT-COPIER-B20-LANE-A task specification). The existing B19 test suite confirms
`Account.Name` is a public instance property of type `string` (see
`Gate2_UsesAccountName_SourceContractVerified` at line 1957). The public setter availability
is accepted as a confirmed architectural fact for this block.

If the engineer encounters a compile error on `new Account { Name = "..." }`, they must fall
back to constructing the Account via whichever NT8-sanctioned factory the SDK provides and
setting the Name via its setter. This fallback must not require reflection for the production
code path.

---

## §13 Component Summary

```
PTT-COPIER-B20-LANE-A
  T1 -- DW-B19-02
    File: src/PropTraderTools/CopyEngine.cs
      PopulateOrderMap() line 659: 1-line predicate change
        BEFORE: b.FollowerAccount == followerAccount
        AFTER:  b.FollowerAccount?.Name == followerAccount?.Name
    File: src/PropTraderTools/CopyEngineTests.cs
      New [Fact]: PopulateOrderMap_DedupGuard_UsesNameEquality
        Pattern: reflection GetMethod, unique signal name, two same-name different-ref Accounts
        Assert: bag.Count == 1

  T2 -- DW-B17-SYNC-01
    File: src/PropTraderTools/CopyEngine.cs
      After line 125: new event field
        public event Action<bool> CopyEnabledChanged;
      In SetEnabled after line 234: new invoke line
        CopyEnabledChanged?.Invoke(enabled);
    File: src/PropTraderTools/CopyEngineTests.cs
      New [Fact]: SetEnabled_FiresCopyEnabledChanged
        Pattern: direct subscription, try/finally unsubscribe
        Assert: Equal(true, received) and Equal(false, received)

  NOT IN SCOPE (Lane B): TradeCopierPanel.cs, TradeCopierWindow.cs, TradeCopierAddOn.cs
```

---

## §14 Pre-Flight Summary

| Check | Result |
|-------|--------|
| Spec requirements covered | DW-B19-02 (T1), DW-B17-SYNC-01 (T2) |
| Files modified | 2 (CopyEngine.cs, CopyEngineTests.cs) |
| Files NOT modified | 3 (Panel, Window, AddOn) |
| New methods/types introduced | 0 |
| New event fields introduced | 1 (`CopyEnabledChanged`) |
| CYC violations | 0 (PopulateOrderMap=2, SetEnabled=1, both within limit=8) |
| JS P0 violations | 0 |
| NT8-P0 violations | 0 |
| lock() usages added | 0 |
| return null usages added | 0 |
| volatile fields added | 0 |
| DateTime.Now usages | 0 (test uses DateTime.UtcNow.Ticks) |
| Non-ASCII characters | 0 |
| Sequential thinking thoughts | 9 (required: 8+) ✅ |

---

**Return: PLAN_COMPLETE**
