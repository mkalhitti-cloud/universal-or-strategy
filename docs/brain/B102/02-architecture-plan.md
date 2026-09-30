# B102 Architecture Plan — XmlSerializer Private-Type Fix + Cancelled Eviction Gap

**Status**: REVIEW_PASS candidate
**Block**: B102
**Defects closed**: DW-B100 (SaveRules/LoadRules silent failure), DW-B101 (Cancelled order eviction gap)
**Files modified**: 1 (`src/PropTraderTools/CopyEngine.cs`)
**Files NOT modified**: `TradeCopierPanel.cs` (OnLoaded wiring correct as of e06bce7b)

---

## 1. DEFECT ANALYSIS — DW-B100

### Root Cause: `private sealed class` blocks XmlSerializer

`XmlSerializer` generates serialization code at runtime via reflection. The .NET runtime
enforces access modifier rules during this reflection pass: **private nested types are
invisible to reflection from outside the declaring type's own methods**. Because
`XmlSerializer` operates from its own generated assembly, it cannot see `private` nested
types and throws:

```
System.InvalidOperationException:
  "PropTraderTools.CopyEngine+CopyRuleDto is inaccessible due to its protection level.
   Only public types can be processed."
```

Changing the access modifier to `internal sealed class` makes both DTOs visible to any
code within the `PropTraderTools` assembly — including the runtime-generated serializer
code — without exposing them across assembly boundaries.

### Why the Bug Was Silent

`SaveRules` and `LoadRules` each wrap their entire body in a bare `catch (Exception) { }`
(L4066 and L4105 respectively). The `InvalidOperationException` thrown by `XmlSerializer`
on the first call to `Serialize()` / `Deserialize()` is swallowed without logging. The
caller sees no error. No file is ever written.

### Why LoadRules Always Returned Early

`LoadRules` sets `_persistenceLoaded = true` at L4066 on first entry and immediately
checks `if (!File.Exists(path)) return`. Because `SaveRules` never successfully wrote the
file (XmlSerializer threw, exception was swallowed, `File.WriteAllText` was never reached),
the file does not exist. `LoadRules` exits before deserialization. On subsequent calls,
`_persistenceLoaded` is already `true` so the method returns instantly without retrying.

### Full Save/Load Circuit Trace

```
[User action]
SetEnabled(true) / rule mutation
       |
       v
SaveRules(path)
  - BuildCopyRulesContainer() : CopyRulesContainer { Rules=[...], CopyEnabled=_isCopyEnabled }
  - new XmlSerializer(typeof(CopyRulesContainer))
      BEFORE FIX: throws InvalidOperationException (private type)
                  catch swallows it, File.WriteAllText never runs
      AFTER FIX:  serializes to XML string, File.WriteAllText(path, xml) succeeds
       |
       v  [NT8 restart or panel reload]
OnLoaded (UI thread)
  - _engine.LoadRules()
      BEFORE FIX: _persistenceLoaded=true, File.Exists=false -> early return
      AFTER FIX:  _persistenceLoaded=true (first call), File.Exists=true ->
                  XmlSerializer.Deserialize() -> _rules.Count restored
                  _isCopyEnabled restored -> CopyEnabledChanged?.Invoke(_isCopyEnabled)
       |
       v
CheckboxRestore (HOTFIX-B67)
  - _engine.GetSavedFollowerNames() -> returns saved list
  - foreach (_followerItems) -> item.IsSelected = true for matching names
  - SortFollowerRows()
  - TryAutoApply()
      BEFORE FIX: GetSavedFollowerNames() returns [] (no rules loaded) -> no checkboxes restored
      AFTER FIX:  checkboxes restored as designed
```

---

## 2. DEFECT ANALYSIS — DW-B101

### Root Cause: TryEvictFollowerBeSlot Early-Return Misses Cancelled

[`TryEvictFollowerBeSlot`](src/PropTraderTools/CopyEngine.cs:1385) guards at L1394:

```csharp
bool isFilled   = o.OrderState == OrderState.Filled;
bool isRejected = o.OrderState == OrderState.Rejected && o.Name == "PTT-BE-Stop";
if (!isFilled && !isRejected)
    return;   // <-- Cancelled: isFilled=false, isRejected=false -> EARLY RETURN
_entryDispatchedOrders.Clear();   // Never reached for Cancelled
```

A `Cancelled` order makes both `isFilled` and `isRejected` false. The method returns
before calling `_entryDispatchedOrders.Clear()`. The stale entry-dispatch cache entry
for the cancelled orderId persists indefinitely.

### Why EvictDedup Is the Correct Fix Site

[`EvictDedup`](src/PropTraderTools/CopyEngine.cs:3102) already owns the `Cancelled`
state path for `_dedupCache`. Its guard already whitelists `Cancelled` alongside `Filled`
and `Rejected`. Adding the `_entryDispatchedOrders.Clear()` call here follows the existing
pattern: **EvictDedup is the consolidation point for all terminal-state eviction logic**.

This is a natural extension of the comment already present at L3112:
```
// DW-B91-A-v2: eviction moved to TryEvictFollowerBeSlot (position-flat).
// Prevents partial-fill re-dispatch: Filled fires before Submitted re-submit on Rithmic.
```
For `Cancelled`, the partial-fill concern does not apply (no re-submit on Cancelled).
Clearing in `EvictDedup` is safe and correct.

### Rithmic OrderId Recycle Risk Scenario

Rithmic reuses numeric order IDs after an order terminal event. The failure sequence:

```
1. DispatchCopy fires for master orderId="12345"
   -> _entryDispatchedOrders["12345"] = true   (Gate 5 cache populated)

2. Order "12345" gets Cancelled at broker

3. OrderStateChange -> EvictDedup("12345", Cancelled)
   BEFORE FIX: guard passes -> _dedupCache.TryRemove OK
               NO _entryDispatchedOrders.Clear() (TryEvictFollowerBeSlot skips Cancelled)
               _entryDispatchedOrders["12345"] STILL PRESENT

4. Rithmic reuses orderId "12345" for a new master entry order

5. DispatchCopy fires for new orderId="12345"
   -> Gate 5: IsEntryDispatched("12345") checks _entryDispatchedOrders -> FOUND -> returns false
   -> CopyEngine silently skips dispatch
   -> Follower accounts miss the entry fill: SILENT DISPATCH FAILURE
```

After fix, step 3 clears `_entryDispatchedOrders`, so step 5 finds no stale entry.

### Why ConcurrentDictionary.Clear() Is Lock-Free

`ConcurrentDictionary<TKey, TValue>.Clear()` is implemented using interlocked pointer
swaps on the internal segment array. It acquires no user-visible lock and satisfies
JS-021 (lock() ban). No `lock()` is introduced by this fix.

---

## 3. FIX SPECIFICATION

All changes are confined to `src/PropTraderTools/CopyEngine.cs`.

### Change 1 — CopyRuleDto access modifier (L3872)

```
BEFORE: private sealed class CopyRuleDto
AFTER:  internal sealed class CopyRuleDto
```

One word changed. Zero logic change.

### Change 2 — CopyRulesContainer access modifier (L3893)

```
BEFORE: private sealed class CopyRulesContainer
AFTER:  internal sealed class CopyRulesContainer
```

One word changed. Zero logic change.

### Change 3 — EvictDedup Cancelled branch

After the `_dedupCache.TryRemove(orderId, out _)` line, insert:

```csharp
if (state == OrderState.Cancelled)
    _entryDispatchedOrders.Clear(); // DW-B101: evict on Cancelled (Filled/Rejected handled by TryEvictFollowerBeSlot)
```

### Comment Update — EvictDedup L3112

Update the existing comment block to document the DW-B101 Cancelled path:

```csharp
// DW-B91-A-v2: eviction moved to TryEvictFollowerBeSlot (position-flat).
// Prevents partial-fill re-dispatch: Filled fires before Submitted re-submit on Rithmic.
// DW-B101: Cancelled eviction handled here (TryEvictFollowerBeSlot early-returns on Cancelled).
```

### Full Post-Fix EvictDedup Body

```csharp
internal void EvictDedup(string orderId, OrderState state)
{
    if (
        state != OrderState.Filled
        && state != OrderState.Cancelled
        && state != OrderState.Rejected
    )
        return;

    _dedupCache.TryRemove(orderId, out _);
    if (state == OrderState.Cancelled)
        _entryDispatchedOrders.Clear(); // DW-B101: evict on Cancelled (Filled/Rejected handled by TryEvictFollowerBeSlot)
    // DW-B91-A-v2: eviction moved to TryEvictFollowerBeSlot (position-flat).
    // Prevents partial-fill re-dispatch: Filled fires before Submitted re-submit on Rithmic.
    // DW-B101: Cancelled eviction handled here (TryEvictFollowerBeSlot early-returns on Cancelled).
}
```

---

## 4. CYC IMPACT

| Method | Before | After | Delta | Limit | Status |
|---|---|---|---|---|---|
| `CopyRuleDto` (class decl) | n/a | n/a | 0 | n/a | access-modifier only |
| `CopyRulesContainer` (class decl) | n/a | n/a | 0 | n/a | access-modifier only |
| `SaveRules` | unchanged | unchanged | 0 | 8 | PASS |
| `LoadRules` | unchanged | unchanged | 0 | 8 | PASS |
| `EvictDedup` | 2 | 3 | +1 | 8 | PASS |
| `TryEvictFollowerBeSlot` | unchanged | unchanged | 0 | 8 | PASS |

**DW-B100**: CYC delta = 0. Access modifier change introduces no new branches.
**DW-B101**: EvictDedup CYC 2 → 3. One new `if` branch. Well within the <= 8 ceiling.

---

## 5. JS-DNA COMPLIANCE

| Rule | Check | Result |
|---|---|---|
| JS-021 (no lock()) | `ConcurrentDictionary.Clear()` is lock-free; no `lock()` added | PASS |
| JS-001 (no throw in hot path) | No `throw new` added | PASS |
| JS-002 (no return null) | No `return null` added | PASS |
| JS-033 (no async void) | No async methods added or modified | PASS |
| JS-036 (no heap alloc in hot path) | No `new byte[]` or equivalent added | PASS |
| JS-037 (no new T[] in hot path) | No array allocation added | PASS |
| ASCII-only | No new string literals; comment text is ASCII-only | PASS |
| CYC <= 8 | Maximum post-fix CYC = 3 (EvictDedup) | PASS |
| NT8 API (CreateOrder prefix) | No new `CreateOrder` calls | PASS |
| DateTime.UtcNow | No date/time usage added | PASS |
| RULES_CATALOG gate | Confirmed PASS by orchestrator pre-flight | PASS |

---

## 6. FILES TOUCHED

| File | Change | Scope |
|---|---|---|
| `src/PropTraderTools/CopyEngine.cs` | 3 surgical changes (L3872, L3893, EvictDedup body) | DW-B100 + DW-B101 |
| `src/PropTraderTools/TradeCopierPanel.cs` | **NOT TOUCHED** — OnLoaded wiring correct as of e06bce7b | — |

**Maximum 1 file modified: CONFIRMED.**

The two DW-B100 changes (L3872, L3893) are non-overlapping with the DW-B101 change
(EvictDedup body). Zero cross-contamination risk. Changes can be applied in any order.

---

## 7. TEST PLAN (xUnit [Fact] ONLY — never NUnit/MSTest)

All tests are white-box unit tests using `internal` visibility (via
`[assembly: InternalsVisibleTo("...Tests")]`).

### T_B100_01 — SaveRules round-trip: file is written

```csharp
[Fact]
public void T_B100_01_SaveRules_WritesFile()
{
    // Arrange
    var engine = BuildTestEngine();
    engine.AddRule(BuildTestRule());
    var tmpPath = Path.GetTempFileName();

    // Act
    engine.SaveRules(tmpPath);

    // Assert
    Assert.True(File.Exists(tmpPath));  // file must be written (DW-B100: was never written before fix)
    File.Delete(tmpPath);
}
```

**What it asserts**: After the access-modifier fix, `XmlSerializer` no longer throws,
`File.WriteAllText` executes, and the file exists on disk.

### T_B100_02 — LoadRules round-trip: state restored

```csharp
[Fact]
public void T_B100_02_LoadRules_RestoresState()
{
    // Arrange
    var tmpPath = Path.GetTempFileName();
    var engine = BuildTestEngine();
    engine.AddRule(BuildTestRule());
    engine.SetCopyEnabled(true);
    engine.SaveRules(tmpPath);

    var engine2 = BuildTestEngine(); // fresh instance (no loaded state)

    // Act
    engine2.LoadRules(tmpPath);

    // Assert
    Assert.True(engine2.IsCopyEnabled);   // CopyEnabled restored
    Assert.Equal(1, engine2.RuleCount);   // rule count restored
    File.Delete(tmpPath);
}
```

**What it asserts**: Deserialization succeeds and both `_isCopyEnabled` and `_rules` are
restored correctly from the XML file written by the fixed `SaveRules`.

### T_B100_03 — LoadRules with missing file: no exception, empty state

```csharp
[Fact]
public void T_B100_03_LoadRules_MissingFile_IsNoop()
{
    // Arrange
    var engine = BuildTestEngine();

    // Act & Assert (must not throw)
    var ex = Record.Exception(() => engine.LoadRules("nonexistent_b100_03.xml"));
    Assert.Null(ex);
    Assert.Equal(0, engine.RuleCount);
}
```

**What it asserts**: `LoadRules` tolerates a missing file path gracefully, leaves
`_rules` empty, and does not throw.

### T_B101_01 — EvictDedup Cancelled clears _entryDispatchedOrders

```csharp
[Fact]
public void T_B101_01_EvictDedup_Cancelled_ClearsEntryDispatched()
{
    // Arrange
    var engine = BuildTestEngine();
    engine.SeedEntryDispatched("orderId-XYZ");  // simulate Gate 5 cache populated

    // Act
    engine.EvictDedup("orderId-XYZ", OrderState.Cancelled);

    // Assert
    Assert.False(engine.IsEntryDispatched("orderId-XYZ")); // DW-B101: stale entry evicted
}
```

**What it asserts**: After `EvictDedup` receives `Cancelled`, the stale
`_entryDispatchedOrders` entry is gone. Rithmic orderId recycle cannot cause Gate 5
dispatch failure.

### T_B101_02 — EvictDedup Filled does NOT clear _entryDispatchedOrders (TryEvictFollowerBeSlot owns Filled)

```csharp
[Fact]
public void T_B101_02_EvictDedup_Filled_DoesNotClearOtherEntries()
{
    // Arrange
    var engine = BuildTestEngine();
    engine.SeedEntryDispatched("orderId-OTHER"); // a different, unrelated entry

    // Act
    engine.EvictDedup("orderId-FILLED", OrderState.Filled);

    // Assert
    Assert.True(engine.IsEntryDispatched("orderId-OTHER")); // unrelated entry must survive
    // (Filled clearing of _entryDispatchedOrders is TryEvictFollowerBeSlot's responsibility,
    //  not EvictDedup's; EvictDedup must not over-clear on Filled)
}
```

**What it asserts**: `EvictDedup` with `Filled` does **not** clear `_entryDispatchedOrders`.
The `if (state == OrderState.Cancelled)` guard is correctly scoped to `Cancelled` only.
`TryEvictFollowerBeSlot` retains exclusive ownership of the `Filled` clearing path.

---

## 8. SCAN-01 THROUGH SCAN-07 (Engineer Contract)

| Scan | Check | Verdict |
|---|---|---|
| SCAN-01 | No `lock()` introduced | PASS — `ConcurrentDictionary.Clear()` is lock-free |
| SCAN-02 | No `throw new` in hot path | PASS — no throw added |
| SCAN-03 | No `return null` | PASS — no return null added |
| SCAN-04 | All CYC <= 8 after changes | PASS — EvictDedup CYC = 3 |
| SCAN-05 | ASCII-only identifiers and strings | PASS — comments and identifiers are ASCII |
| SCAN-06 | No `async void` | PASS — no async methods modified |
| SCAN-07 | NT8 CreateOrder calls use "PTT-" prefix | PASS — no CreateOrder calls added |

---

## Summary

B102 resolves two silent correctness defects with minimal surgery to one file:

- **DW-B100**: Two one-word access-modifier changes (`private` → `internal`) unblock
  `XmlSerializer`, enabling the save/load persistence circuit that has been silently
  failing since the XML path was introduced.

- **DW-B101**: One `if` branch added to `EvictDedup` ensures `Cancelled` orders evict
  their stale `_entryDispatchedOrders` entry, eliminating the Rithmic orderId-recycle
  silent dispatch failure scenario.

Total CYC impact: +1 (EvictDedup only). All JS-DNA rules: PASS.
