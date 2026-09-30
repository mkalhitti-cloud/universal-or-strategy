# PTT-COPIER-B21-LANE-B Architecture Plan

**Status**: REVIEW_PENDING
**Block**: PTT-COPIER-B21, Lane B
**Defect**: DW-B19-02
**Author**: ptt-architect (Phase 1)
**Date**: 2026-07-07

---

## §1 Overview and Scope

B21-LANE-B closes defect DW-B19-02 from the B21 lane's perspective by adding a single,
independently authored xUnit [Fact] test that verifies the name-equality dedup guard in
`PopulateOrderMap`.

The production fix for DW-B19-02 was delivered by B20-LANE-A. B21-LANE-B makes no change
to any production source file. Its entire scope is one new test method appended to
[`CopyEngineTests.cs`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs).

**[Fact] baseline**: 120 confirmed by:

```
Select-String -Path CopyEngineTests.cs -Pattern "\[Fact\]" | Measure-Object = 120
```

**Expected count after B21-LANE-B**: 121

---

## §2 Current State — Fix Already Applied, Existing Test Already Present

### Production predicate (confirmed at CopyEngine.cs:665)

```csharp
// CYC=2. Records (signal, follower) association in _orderMap for future bracket lookups.
private void PopulateOrderMap(string fromEntrySignalName, Account followerAccount)
{
    var bag = _orderMap.GetOrAdd(
        fromEntrySignalName,
        _ => new ConcurrentBag<FollowerBinding>());
    // Dedup guard: prevent accumulating duplicate bindings on repeated Working state events
    if (!bag.Any(b => b.FollowerAccount?.Name == followerAccount?.Name))   // name equality
        bag.Add(new FollowerBinding(followerAccount, fromEntrySignalName));
}
```

Name-equality predicate `b.FollowerAccount?.Name == followerAccount?.Name` is in place.
Reference-equality bug (`b.FollowerAccount == followerAccount`) is **gone**.

### Existing B20-LANE-A test (CopyEngineTests.cs:2038)

```
PopulateOrderMap_DedupGuard_UsesNameEquality
Signal key: "B20-DEDUP-" + DateTime.UtcNow.Ticks
```

B21-LANE-B does **not** modify, rename, or remove this test.

---

## §3 B21-LANE-B Scope

| Item | Action |
|------|--------|
| `CopyEngine.cs` production code | NO CHANGE |
| `CopyEngineTests.cs` | Append ONE new `[Fact]` at end of file (before final `}`) |
| Any other `.cs` file | NO CHANGE |
| `AtrSizingEngine.cs` | NOT TOUCHED |
| `TradeCopierAddOn.cs` | NOT TOUCHED |
| `TradeCopierPanel.cs` | NOT TOUCHED |
| Any `.md` doc file | NOT TOUCHED |

---

## §4 Test Specification

### 4.1 Test name

```
PopulateOrderMap_DedupGuard_B21_NameEqualityContract
```

Different from the B20 test (`PopulateOrderMap_DedupGuard_UsesNameEquality`) per B21-lane
isolation contract.

### 4.2 Signal key

```csharp
string signalName = "B21-DEDUP-" + DateTime.UtcNow.Ticks;
```

`DateTime.UtcNow.Ticks` guarantees uniqueness across runs and avoids cross-test contamination
with the B20 test that uses `"B20-DEDUP-"`. `DateTime.UtcNow` is used — `DateTime.Now` is
**banned** per JS-006 / project mandate.

### 4.3 Setup

```csharp
_engine.SetEnabled(false);  // stop CopyEngine from reacting to injected state
```

The `CopyEngine.Instance` singleton must be quiesced before reflection-based invocation so
no live copy logic interferes with the test's bag inspection.

### 4.4 Reflection call sequence

```csharp
var a1 = new Account { Name = "Sim101-B21" };
var a2 = new Account { Name = "Sim101-B21" };
// a1 and a2: same Name, different object references -- re-creates post-reconnect scenario

var mi = typeof(CopyEngine).GetMethod(
    "PopulateOrderMap",
    BindingFlags.NonPublic | BindingFlags.Instance);
Assert.NotNull(mi);

mi.Invoke(_engine, new object[] { signalName, a1 });
mi.Invoke(_engine, new object[] { signalName, a2 });
```

`PopulateOrderMap` is `private` on `CopyEngine`. Reflection is the correct test-harness
access pattern (same as used in the B20 test). No `lock()` is introduced.

### 4.5 Bag-read via reflection

```csharp
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
```

### 4.6 Assertions

```csharp
// Dedup guard fired: two invocations with same-name accounts yield exactly 1 bag entry
Assert.Equal(1, bag.Count);
```

Single assertion proves: name-equality predicate treated the two distinct Account objects
as duplicates and suppressed the second `bag.Add`.

### 4.7 Complete method body (engineer reference)

```csharp
// =================================================================
// B21-LANE-B: PopulateOrderMap dedup guard -- B21 name-equality contract
// =================================================================

[Fact]
public void PopulateOrderMap_DedupGuard_B21_NameEqualityContract()
{
    _engine.SetEnabled(false);
    string signalName = "B21-DEDUP-" + DateTime.UtcNow.Ticks;
    var a1 = new Account { Name = "Sim101-B21" };
    var a2 = new Account { Name = "Sim101-B21" };
    var mi = typeof(CopyEngine).GetMethod(
        "PopulateOrderMap",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(mi);
    mi.Invoke(_engine, new object[] { signalName, a1 });
    mi.Invoke(_engine, new object[] { signalName, a2 });
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
    Assert.Equal(1, bag.Count);
}
```

---

## §5 Files Touched

| File (Wave workspace) | Change |
|-----------------------|--------|
| `src/PropTraderTools/CopyEngineTests.cs` | Append new `[Fact]` before closing `}` of test class |
| `src/PropTraderTools/CopyEngine.cs` | **NO CHANGE** |

No other file in `src/PropTraderTools/` is touched.

---

## §6 7-Scan Pre-Flight Checklist

The engineer MUST run all 7 scans after appending the new test. Expected results:

| Scan | Pattern | Expected Result |
|------|---------|-----------------|
| SCAN-01 | `lock\s*\(` in new test code | ZERO matches — test uses no lock() |
| SCAN-02 | Non-ASCII characters in new test code | ZERO matches — all identifiers and strings are ASCII-only |
| SCAN-03 | `FontFamily` in new test code | ZERO matches — not applicable to test |
| SCAN-04 | Hex color string literals (`#[0-9A-Fa-f]{3,6}`) in new test code | ZERO matches — no UI code |
| SCAN-05 | `CreateOrder` without `"PTT-"` prefix | NOT APPLICABLE — no `CreateOrder` call in test |
| SCAN-06 | `DateTime\.Now[^.]` in new test code | ZERO matches — only `DateTime.UtcNow.Ticks` used |
| SCAN-07 | `async\s+void\s+\w+\(` in new test code | ZERO matches — test method is synchronous `void` |

All 7 scans must return zero matches on the new code block before the ticket is closed.

---

## §7 CYC Verification

`PopulateOrderMap` is **not modified** by B21-LANE-B.

Current CYC=2 (one conditional branch at line 665, confirmed from source read).
CYC is unchanged after this work. No CYC audit action required.

The new test method is a linear sequence with no branches. Its CYC = 1, which is
below the Jane Street CYC <= 8 threshold.

---

## §8 Jane Street Rules Applicable to Test Code

| Rule | Constraint | Compliance in new test |
|------|-----------|------------------------|
| JS-021 | No `lock()` anywhere | Compliant — no lock used |
| JS-002 | No `return null` | Compliant — void method, no return |
| JS-033 | No `async void` | Compliant — synchronous void |
| JS-006 (project) | `DateTime.UtcNow` only, never `DateTime.Now` | Compliant — uses `.UtcNow.Ticks` |
| CYC <= 8 | All modified methods | Compliant — new test CYC = 1 |
| ASCII-only | No Unicode in identifiers or string literals | Compliant — all ASCII |

---

## §9 Parallel Safety — Lane B Isolation

Lane B touches exclusively `CopyEngineTests.cs` (append only).

Files confirmed as **NOT TOUCHED** by Lane B:

- `AtrSizingEngine.cs`
- `TradeCopierAddOn.cs`
- `TradeCopierPanel.cs`
- `TradeCopierWindow.cs`
- `CopyEngine.cs`
- Any `.md` documentation files
- `manifest.json` (updated only by orchestrator, not lane workers)

Any parallel lane operating on the above files during B21 will encounter zero merge conflicts
from Lane B's changes.

---

## §10 NT8 Compiler Rules Applicable

| Rule | Constraint | Applicability to test |
|------|-----------|----------------------|
| NT8-003 | No `volatile double` | Not applicable — test uses no fields |
| NT8-004 | No `ImmutableDictionary` | Not applicable — test uses `ConcurrentDictionary` (safe) |
| NT8-006 | `ConcurrentBag.Any()` requires `using System.Linq` | **CHECK**: confirm `using System.Linq` is already at top of `CopyEngineTests.cs`. If not present, engineer must add it. The test method itself does not call `.Any()` directly, but `PopulateOrderMap` does internally — no new `using` needed in the test file beyond what B20 already required. |

---

## §11 Shelved / Carry-Forward Items (Unchanged)

Items deferred from prior blocks that are not in scope for B21-LANE-B:

- All items in `docs/brain/PTT-COPIER-B20-LANE-A/06-deferred-backlog.md` (if present)
  remain deferred and are not reopened or closed by this lane.
- DW-B19-02 production fix: **closed by B20-LANE-A**, carried forward as verified.

---

## §12 Ticket Summary (for Phase 3)

**One ticket only (T1)**:

| Field | Value |
|-------|-------|
| Spec req | DW-B19-02 (complementary lane coverage) |
| File | `src/PropTraderTools/CopyEngineTests.cs` |
| Method to add | `PopulateOrderMap_DedupGuard_B21_NameEqualityContract` |
| Method signature | `public void PopulateOrderMap_DedupGuard_B21_NameEqualityContract()` |
| JS rules | JS-021, JS-002, JS-033, JS-006, CYC<=8 |
| xUnit [Fact] | 1 new (total goes from 120 to 121) |
| Scans | SCAN-01 through SCAN-07 (see §6) |
| Production code change | None |

---

**PLAN STATUS**: REVIEW_PENDING
