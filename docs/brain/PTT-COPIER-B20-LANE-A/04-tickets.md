# PTT-COPIER-B20-LANE-A — Tickets
# Phase 3 output (ptt-architect)
# Status: TICKETS_COMPLETE
# Date: 2026-07-14
# Source plan: docs/brain/PTT-COPIER-B20-LANE-A/02-architecture-plan.md (REVIEW_PASS)

---

## Block Summary

**Block**: PTT-COPIER-B20-LANE-A
**Tickets**: T1 (DW-B19-02) + T2 (DW-B17-SYNC-01)
**Wave workspace root**: `c:\WSGTA\universal-or-strategy`
**Files modified**: `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/CopyEngineTests.cs`
**Files NOT modified**: `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`
**[Fact] baseline entering block**: 118
**[Fact] count after T1**: 119
**[Fact] count after T2**: 120

---

## Ticket T1 — PopulateOrderMap Dedup Guard (DW-B19-02)

### Spec Requirement
**ID**: DW-B19-02  
**Priority**: P2  
**Description**: `PopulateOrderMap` dedup guard uses C# object reference equality on `Account`.
After a Rithmic broker reconnect, NT8 recreates `Account` objects, causing the guard to miss
and duplicate `FollowerBinding` entries to accumulate in the `ConcurrentBag`. Fix the predicate
to use `Account.Name` string equality, which is stable across reconnects.

---

### Production Change

**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`

**Location**: Line 659 — inside `PopulateOrderMap`, the `bag.Any()` predicate.

**BEFORE** (current, line 659):
```csharp
if (!bag.Any(b => b.FollowerAccount == followerAccount))         // (1) branch
```

**AFTER** (replace the full line):
```csharp
if (!bag.Any(b => b.FollowerAccount?.Name == followerAccount?.Name))         // (1) branch
```

**Scope constraint**: This is the ONLY change to `CopyEngine.cs` for T1. No other lines are
touched. No method signatures change. No new fields or types are introduced.

---

### Method Context

```
Method: PopulateOrderMap(string signalName, Account followerAccount)
Access: private
CYC before fix: 2  (base=1, if=1)
CYC after  fix: 2  (unchanged — only the predicate expression changes)
Return type: void
```

The `?.` null-conditional operators in `b.FollowerAccount?.Name` and `followerAccount?.Name` are
**expression-level operators**, not control-flow branches. They do not increase CYC.

---

### Test — `PopulateOrderMap_DedupGuard_UsesNameEquality`

**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`

**Insertion point**: After the last `[Fact]` method in the class, before the closing `}` of
the test class (currently at line 2033).

**Test name (xUnit [Fact])**: `PopulateOrderMap_DedupGuard_UsesNameEquality`

**Verbatim test body**:
```csharp
// ===================================================================
// B20-LANE-A T1: PopulateOrderMap dedup guard uses Name equality
// ===================================================================

[Fact]
public void PopulateOrderMap_DedupGuard_UsesNameEquality()
{
    _engine.SetEnabled(false);
    // Use a unique signal name to avoid cross-test contamination
    string signalName = "B20-DEDUP-" + DateTime.UtcNow.Ticks;
    // a1 and a2 have the same Name but are different object references
    var a1 = new Account { Name = "Sim101-B20" };
    var a2 = new Account { Name = "Sim101-B20" };
    // PopulateOrderMap is private -- invoke via reflection
    var mi = typeof(CopyEngine).GetMethod(
        "PopulateOrderMap",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(mi);
    mi.Invoke(_engine, new object[] { signalName, a1 });
    mi.Invoke(_engine, new object[] { signalName, a2 });
    // Read _orderMap bag for signalName
    var mapField = typeof(CopyEngine).GetField(
        "_orderMap",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(mapField);
    var map = mapField.GetValue(_engine)
        as System.Collections.Concurrent.ConcurrentDictionary<
            string,
            System.Collections.Concurrent.ConcurrentBag<FollowerBinding>>;
    Assert.NotNull(map);
    System.Collections.Concurrent.ConcurrentBag<FollowerBinding> bag;
    Assert.True(map.TryGetValue(signalName, out bag), "Signal key not found in _orderMap");
    // With name equality, calling twice with same-name accounts -> exactly 1 entry
    Assert.Equal(1, bag.Count);
}
```

**What the test asserts**:
1. `PopulateOrderMap` exists and is accessible via reflection.
2. `_orderMap` exists and is accessible via reflection.
3. Invoking `PopulateOrderMap` twice with two different `Account` object references that share
   the same `Name` value results in exactly **1** entry in the bag (dedup guard fires on name
   equality, prevents duplicate).

**Singleton isolation**: Uses `"B20-DEDUP-" + DateTime.UtcNow.Ticks` as the signal name to
guarantee an empty bag entry before the test runs. `CopyEngine.Instance` persists across tests;
a unique signal key prevents state bleed from earlier tests.

---

### CYC Analysis (T1)

| Method | CYC Before | CYC After | Delta | Within Limit (<=8)? |
|--------|-----------|-----------|-------|---------------------|
| `PopulateOrderMap` | 2 | 2 | 0 | YES |

---

### JS Rule Constraints (T1)

| Rule | Description | Status |
|------|-------------|--------|
| JS-021 | No `lock()` anywhere in src/ | PASS — no lock added; predicate is a pure expression |
| JS-002 | No `return null` for missing values | PASS — method returns void |
| JS-001 | No `throw new XxxException` in hot paths | PASS — no throw added |
| JS-033 | No `async void` non-event-handlers | PASS — no async modifier |
| JS-015 | No unvalidated primitives crossing API boundary | PASS — no new parameters |
| JS-003 | Readonly struct prevents transposition | PASS — `FollowerBinding` struct unchanged |

---

### NT8 Compiler Constraints (T1)

| Rule | Check | Status |
|------|-------|--------|
| NT8-001 | No `{ get; init; }` | PASS — no new property with init accessor |
| NT8-002 | No `abstract record` / `sealed record` | PASS — no record type |
| NT8-003 | No `volatile double` or `volatile long` | PASS — no new volatile field |
| NT8-004 | No `ImmutableDictionary` | PASS — no immutable collection |
| NT8-007 | `CreateOrder` arg 12 as `string` | PASS — no `CreateOrder` call added |
| NT8-031 | `Math.Clamp` unavailable | PASS — not used |
| `Account.Name` setter | Object-initializer syntax requires public setter | PRE-CONFIRMED by B19 test `Gate2_UsesAccountName_SourceContractVerified` (line 1957). If compile error: use NT8 SDK factory + setter. |

---

### 7-Scan Checklist — T1 (Engineer Contract, Layer 1)

| # | Scan | Command | Expected Result |
|---|------|---------|-----------------|
| SCAN-01 | Old ref-equality pattern eliminated | `grep -n "b\.FollowerAccount == followerAccount" src/PropTraderTools/CopyEngine.cs` | **0 matches** |
| SCAN-02 | New name-equality pattern present | `grep -n "FollowerAccount?.Name == followerAccount?.Name" src/PropTraderTools/CopyEngine.cs` | **exactly 1 match** |
| SCAN-03 | New test method present | `grep -n "PopulateOrderMap_DedupGuard_UsesNameEquality" src/PropTraderTools/CopyEngineTests.cs` | **exactly 1 match** |
| SCAN-04 | [Fact] count is 119 | `grep -c "\[Fact\]" src/PropTraderTools/CopyEngineTests.cs` | **119** |
| SCAN-05 | No `lock(` in CopyEngine.cs | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | **0 matches** (comment-only lines excluded by inspection) |
| SCAN-06 | No `async void` in PropTraderTools | `grep -rn "async void " src/PropTraderTools/` | **0 matches** |
| SCAN-07 | Build passes | `dotnet build` or `dotnet test` in wave workspace | **0 errors** |

**All 7 scans must pass before T1 is marked complete.**

---

---

## Ticket T2 — Copy ON/OFF State Event (DW-B17-SYNC-01)

### Spec Requirement
**ID**: DW-B17-SYNC-01  
**Priority**: P2  
**Description**: `SetEnabled(bool enabled)` fires only a `StatusUpdate` string event ("Copy ON" /
"Copy OFF"). External callers — keyboard shortcuts, scripted tests, future Lane B subscribers —
cannot receive the boolean enabled state without parsing the string. Add a `public event
Action<bool> CopyEnabledChanged` field and fire it from `SetEnabled` after `StatusUpdate`.
Panel/Window subscriber wiring is deferred to Lane B/C; this ticket covers the event declaration
and fire site in `CopyEngine.cs` only.

---

### Production Changes

**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`

#### CHANGE A — New event field

**Insertion point**: After line 125 — the line containing `internal event Action<string> PendingBeFired;`

**Insert these 4 lines** (immediately after `PendingBeFired` declaration):
```csharp
// B20-LANE-A T2: Copy enabled state change notification (DW-B17-SYNC-01)
// Fired from SetEnabled after StatusUpdate. Carries the new bool state directly.
// Lane C wires TradeCopierPanel and TradeCopierWindow subscribers.
public event Action<bool> CopyEnabledChanged;
```

#### CHANGE B — Invoke site in `SetEnabled`

**Location**: `SetEnabled` method body, after the `StatusUpdate?.Invoke(...)` line (currently line 234).

**CURRENT** `SetEnabled` body (lines 231–235):
```csharp
internal void SetEnabled(bool enabled)
{
    _isCopyEnabled = enabled;
    StatusUpdate?.Invoke("Copy " + (enabled ? "ON" : "OFF"));
}
```

**AFTER** `SetEnabled` body:
```csharp
internal void SetEnabled(bool enabled)
{
    _isCopyEnabled = enabled;
    StatusUpdate?.Invoke("Copy " + (enabled ? "ON" : "OFF"));
    CopyEnabledChanged?.Invoke(enabled);
}
```

**Scope constraint**: CHANGE A and CHANGE B are the ONLY changes to `CopyEngine.cs` for T2.
No Panel or Window files are touched. Panel/Window wiring is explicitly Lane B/C scope.

---

### Method Context

```
Method: SetEnabled(bool enabled)
Access: internal
CYC before T2: 1  (base=1, no branches)
CYC after  T2: 1  (unchanged — ?.Invoke is a null-conditional expression, not an if-branch)
Return type: void
```

`CopyEnabledChanged?.Invoke(enabled)` uses the C# null-conditional member invocation. The compiler
atomically snapshots the delegate before the null check — this is the standard thread-safe delegate
invocation pattern. No lock() required. JS-021 compliant.

---

### Test — `SetEnabled_FiresCopyEnabledChanged`

**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`

**Insertion point**: After the T1 test (`PopulateOrderMap_DedupGuard_UsesNameEquality`), before
the closing `}` of the test class.

**Test name (xUnit [Fact])**: `SetEnabled_FiresCopyEnabledChanged`

**Verbatim test body**:
```csharp
// ===================================================================
// B20-LANE-A T2: SetEnabled fires CopyEnabledChanged event
// ===================================================================

[Fact]
public void SetEnabled_FiresCopyEnabledChanged()
{
    _engine.SetEnabled(false);
    bool? received = null;
    Action<bool> handler = v => received = v;
    _engine.CopyEnabledChanged += handler;
    try
    {
        _engine.SetEnabled(true);
        Assert.Equal(true, received);
        _engine.SetEnabled(false);
        Assert.Equal(false, received);
    }
    finally
    {
        _engine.CopyEnabledChanged -= handler;
    }
}
```

**What the test asserts**:
1. After `SetEnabled(true)`, `received` is `true` — event fired with correct value.
2. After `SetEnabled(false)`, `received` is `false` — event fired with correct value.

**Singleton teardown**: The handler is always unsubscribed in the `finally` block. Because
`CopyEngine.Instance` is a singleton shared across the test suite, failing to unsubscribe would
leak the lambda and accumulate event subscribers across test runs, potentially causing false
positives in future tests. The `try/finally` pattern guarantees unsubscription even on assertion
failure.

---

### CYC Analysis (T2)

| Method | CYC Before | CYC After | Delta | Within Limit (<=8)? |
|--------|-----------|-----------|-------|---------------------|
| `SetEnabled` | 1 | 1 | 0 | YES |

---

### JS Rule Constraints (T2)

| Rule | Description | Status |
|------|-------------|--------|
| JS-021 | No `lock()` anywhere in src/ | PASS — `CopyEnabledChanged?.Invoke` uses null-conditional, no lock needed; atomically snapshots delegate reference |
| JS-002 | No `return null` for missing values | PASS — method returns void |
| JS-001 | No `throw new XxxException` in hot paths | PASS — no throw added |
| JS-033 | No `async void` non-event-handlers | PASS — no async modifier |
| JS-015 | No unvalidated primitives crossing API boundary | PASS — `bool enabled` is validated by caller context; already existing parameter |
| JS-023 | No misuse of `volatile` fields | PASS — `_isCopyEnabled` is an existing field (no change); no new volatile field added |

**Thread-safety note**: `CopyEnabledChanged?.Invoke(enabled)` captures the delegate snapshot
before the null check (C# compiler guarantees this for `?.`). This prevents a TOCTOU race
on concurrent subscribe/unsubscribe. No lock() is required and none may be added (JS-021).

---

### NT8 Compiler Constraints (T2)

| Rule | Check | Status |
|------|-------|--------|
| NT8-001 | No `{ get; init; }` | PASS — `event Action<bool>` field, not a property |
| NT8-002 | No `abstract record` / `sealed record` | PASS — no record type |
| NT8-003 | No `volatile double` or `volatile long` | PASS — no new volatile field |
| NT8-004 | No `ImmutableDictionary` | PASS — no immutable collection |
| NT8-007 | `CreateOrder` arg 12 as `string` | PASS — no `CreateOrder` call added |
| NT8-031 | `Math.Clamp` unavailable | PASS — not used |
| `event Action<bool>` syntax | Standard C# delegate event field | PASS — supported in .NET 4.8 / C# 7.x |

---

### 7-Scan Checklist — T2 (Engineer Contract, Layer 1)

| # | Scan | Command | Expected Result |
|---|------|---------|-----------------|
| SCAN-01 | `CopyEnabledChanged` declared and invoked in CopyEngine.cs | `grep -n "CopyEnabledChanged" src/PropTraderTools/CopyEngine.cs` | **>= 2 matches** (declaration line + invoke line) |
| SCAN-02 | Invoke site exactly as specified | `grep -n "CopyEnabledChanged?.Invoke(enabled)" src/PropTraderTools/CopyEngine.cs` | **exactly 1 match** |
| SCAN-03 | New test method present | `grep -n "SetEnabled_FiresCopyEnabledChanged" src/PropTraderTools/CopyEngineTests.cs` | **exactly 1 match** |
| SCAN-04 | [Fact] count is 120 | `grep -c "\[Fact\]" src/PropTraderTools/CopyEngineTests.cs` | **120** |
| SCAN-05 | No `lock(` in CopyEngine.cs | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | **0 matches** (comment-only lines excluded by inspection) |
| SCAN-06 | No `async void` in PropTraderTools | `grep -rn "async void " src/PropTraderTools/` | **0 matches** |
| SCAN-07 | Build passes | `dotnet build` or `dotnet test` in wave workspace | **0 errors** |

**All 7 scans must pass before T2 is marked complete.**

---

## Execution Order

T1 MUST be completed (all SCAN-01..07 pass) before T2 begins. This ordering is required because:
1. SCAN-04 for T2 asserts a count of 120, which presupposes T1 added the count-119 test.
2. T2 builds on the T1-modified `CopyEngine.cs` state.

```
T1 start → T1 SCAN-01..07 PASS → T2 start → T2 SCAN-01..07 PASS → Lane A COMPLETE
```

---

## Block-Level JS/NT8 Summary

| Category | Check | Result |
|----------|-------|--------|
| JS-021 lock() | No lock added by T1 or T2 | PASS |
| JS-002 return null | No null return added | PASS |
| JS-001 throw in hot path | No throw added | PASS |
| JS-033 async void | No async void added | PASS |
| NT8-001 init accessor | Not used | PASS |
| NT8-002 record types | Not used | PASS |
| NT8-003 volatile double | Not used | PASS |
| NT8-004 ImmutableDictionary | Not used | PASS |
| DateTime.Now | Not used — test uses `DateTime.UtcNow.Ticks` | PASS |
| Non-ASCII characters | None in any added code | PASS |
| lock() forensic scan | `grep -r "lock(" src/PropTraderTools/CopyEngine.cs` → 0 | PASS |

---

## Deferred Items Closed by This Block

| Spec ID | Description | Ticket | Status After Lane A |
|---------|-------------|--------|---------------------|
| DW-B19-02 | `PopulateOrderMap` dedup guard fails after Rithmic reconnect | T1 | CLOSED |
| DW-B17-SYNC-01 | No boolean event from `SetEnabled`; Panel/Window cannot reliably sync toggle | T2 | CLOSED |

---

## Carry-Forward Open Items (Unchanged from B19-L2)

The following 10 items carry forward to B20-LANE-B and beyond unchanged:

| ID | Description | Priority |
|----|-------------|----------|
| DW-B9-01 | ATR box visualization on chart canvas | P2 |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset for limit price entry | P3 |
| DW-B12-DEFER-01 | Full-panel mode expansion: Buy Ask / Sell Bid quick-entry buttons | P2 |
| DW-B12-DEFER-02 | Auto-trail stop from BE CONNECTED level | P3 |
| DW-B12-DEFER-03 | Correct Math.Clamp ban comment attribution; add NT8-031 rule | P3 |
| DW-B12-DEFER-04 | Align CopyEngineTests.cs test names with 04-tickets.md contract names | P3 |
| DW-B19L2-DEFER-01 | `ExitBufferTicks` value-object (JS-015) | P2 |
| DW-B19L2-DEFER-02 | Spread validation guard in GetAsk/GetBid | P2 |
| DW-B19L2-DEFER-03 | `OnMarketData` event hook in TradeCopierPanel | P2 |
| DW-B19L2-DEFER-04 | Telemetry: log anchor price at each Trim/Flatten CreateOrder call | P3 |

---

**Return: TICKETS_COMPLETE**
