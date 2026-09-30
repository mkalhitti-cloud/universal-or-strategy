# B75-LaneB Architecture Plan

**Status**: REVIEW_PENDING
**Phase**: 1 (Architecture)
**Lane**: B (Panel-side: TradeCopierPanel.cs)
**Date**: 2026-08-17
**Files in scope**:
- `src/PropTraderTools/TradeCopierPanel.cs`
- `src/PropTraderTools/CopyEngine.cs` (volatile field + two methods)

---

## 1. Hotfix Theme Summary

| Hotfix ID | Method Affected | File | Root Cause | Fix Approach |
|-----------|----------------|------|------------|-------------|
| HOTFIX-B66-ATM-TPL | `GetLeaderAtmTemplateName` | TradeCopierPanel.cs | `FindVisualChildByIndex<ComboBox>(ct,2)` returns wrong ComboBox: PTT-injected ComboBoxes shift the native ATM ComboBox to a higher index | Replace primary with `ct.AtmStrategy?.Name`; keep index-2 as fallback-2 |
| HOTFIX-B66-ATM-OBJ (panel-side) | `OnCloneModeClick` + `SetCloneAtmObjectCache` | TradeCopierPanel.cs + CopyEngine.cs | `ChartTrader.AtmStrategy.Name` is the runtime class name `"AtmStrategy"`, not the template file name; `StartAtmStrategy(string, order)` silently no-ops when name not found on disk | Capture the live `AtmStrategy` object at click time; store via `volatile _cloneAtmObject`; engine dispatch uses `StartAtmStrategy(obj, order)` object overload |
| HOTFIX-B67-CHECKBOX-RESTORE | `OnLoaded` restore block + new `GetSavedFollowerNames` | TradeCopierPanel.cs + CopyEngine.cs | After NT8 restart, `_followerItems` built with all `IsSelected=false`; first `TryAutoApply` wipes restored engine rule with empty follower list | After `LoadFollowers()`, query saved follower names from engine rules and set `IsSelected=true` on matches before any user interaction |

---

## 2. Two-Cache Design (HOTFIX-B66-ATM-OBJ)

### Fields (CopyEngine.cs, ~line 116-120)

```
_cloneAtmCache   : volatile string
                   Purpose: display/logging only ("MES $200 SL6" or the class name "AtmStrategy")
                   Written by: SetCloneAtmCache(string) on UI thread at Clone radio click
                   Read by: GetCloneAtmMode() on dispatch thread; logging

_cloneAtmObject  : volatile NinjaTrader.NinjaScript.AtmStrategy
                   Purpose: drives StartAtmStrategy dispatch — the live object
                   Written by: SetCloneAtmObjectCache(atmObj) on UI thread at Clone radio click
                   Read by: GetCloneAtmMode() on dispatch thread
```

### Why object wins over string

`ChartTrader.AtmStrategy.Name` (the string) returns the **runtime class name** `"AtmStrategy"`, not the user-selected template file name (e.g., `"MES $200 SL6"`). The string overload `StartAtmStrategy(string, Order)` silently no-ops when the string does not match a template file on disk. The result is a ghost yellow line on the chart and no brackets for the follower.

The object overload `StartAtmStrategy(NinjaTrader.NinjaScript.AtmStrategy, Order)` takes the **live strategy instance** captured directly from `ChartTrader.AtmStrategy` at click time. NT8 resolves the template internally from the live object. Brackets are correctly applied.

### volatile on reference type: JS-021 compliance

`volatile` on a managed reference type is valid C# — the CLR 4.0+ runtime guarantees that reference writes and reads are atomic on both 32-bit and 64-bit platforms. No lock is needed. This satisfies JS-021 (no `lock()` anywhere).

Writer thread: WPF UI thread (OnCloneModeClick).
Reader thread: NT8 order dispatch thread (GetCloneAtmMode in CopyEngine dispatch path).
Access pattern: single writer, single reader — no contention, no torn read possible.

### Fallback chain in GetCloneAtmMode (CopyEngine, downstream consumer)

```
_cloneAtmObject != null  =>  Named(_cloneAtmCache, atmObj)   -- object overload path (preferred)
_cloneAtmObject == null
  _cloneAtmCache non-empty  =>  Named(_cloneAtmCache)         -- string overload fallback
  _cloneAtmCache empty      =>  Inherit                       -- no ATM (user selected None)
```

The panel-side capture (OnCloneModeClick + SetCloneAtmObjectCache) feeds into this chain.
After an NT8 panel reload, `_cloneAtmObject` reverts to null; the string fallback preserves
display/logging continuity. The object path re-populates only when the user clicks Clone again.

---

## 3. GetLeaderAtmTemplateName Three-Tier Fallback

**Signature**: `internal static string GetLeaderAtmTemplateName(Chart currentChart)`
**File**: TradeCopierPanel.cs, line 2218
**Callers**: OnCloneModeClick (UI thread only)

### Fallback Table

| Tier | Trigger Condition | Method Used | Notes |
|------|------------------|-------------|-------|
| Guard-1 | `currentChart == null` | early `return string.Empty` | Chart not yet attached |
| Guard-2 | `FindVisualChild<ChartTrader>(currentChart) == null` | early `return string.Empty` | ChartTrader not in visual tree |
| Primary | `ct.AtmStrategy != null` | `ct.AtmStrategy.Name ?? string.Empty` | Direct property — immune to index shift |
| Fallback-1 | `ct.AtmStrategy == null`, `AtmStrategySelector` found | `sel.SelectedAtmStrategy.Name ?? string.Empty` | Type-based walk, covers non-standard CT builds |
| Fallback-2 | Both above null | `FindVisualChildByIndex<ComboBox>(ct, 2)?.SelectedItem as string ?? string.Empty` | Legacy pre-B66 path |
| Exception | Any NT8 API exception | `catch { return string.Empty; }` | Defensive — API can throw on unloaded chart |

### Why the primary path is immune to PTT ComboBox injection

`ct.AtmStrategy` is a **direct property** of the `ChartTrader` object, not a positional child lookup. PTT injects `_followersDropDown` and per-follower ATM ComboBoxes into the ChartTrader visual tree, which shifts `FindVisualChildByIndex<ComboBox>(ct, 2)` to return the wrong element. The direct property reference bypasses the visual tree entirely.

Confirmed from NT8 community forum topics 5133 and 6060 as the canonical ATM template access pattern for AddOn builds.

### Why fallback-2 is kept

Pre-B66 deployments with an unpatched NT8 build may not expose `ChartTrader.AtmStrategy` on all versions. Fallback-2 preserves backward compatibility for those environments. It is now the last resort rather than the sole path.

### Return contract

- **Never returns null** — every code path returns `string.Empty` or a non-null `.Name` value.
- **Never throws** — entire body wrapped in try/catch returning `string.Empty` on any exception.
- **Empty string semantics**: caller (`OnCloneModeClick`) passes empty string to `SetCloneAtmCache`, which stores it; `GetCloneAtmMode` interprets empty cache + null object as `FollowerAtmMode.Inherit` (no ATM, bare Limit order copy).

---

## 4. OnLoaded Restore Sequence

**Method**: `private void OnLoaded(object sender, RoutedEventArgs e)`
**File**: TradeCopierPanel.cs, line 616
**Thread**: WPF UI thread (Loaded event)

### Pre-conditions (must all be true before restore block fires)

1. `LoadFollowers()` has completed — `_followerItems` collection is populated with rows.
2. `_instrument != null` — chart has an active instrument attached.
3. `_leaderAccount != null` — leader account is resolved.
4. `saved.Count > 0` — at least one follower was previously saved for this instrument+leader pair.

### Restore Step Sequence

```
Step 1: LoadFollowers()
        --> _followerItems populated, all IsSelected=false (pre-fix state)

Step 2: Guard check
        if (_instrument != null && _leaderAccount != null)
        --> Only proceed if chart context is fully resolved

Step 3: GetSavedFollowerNames(_instrument.FullName, _leaderAccount.Name)
        --> Returns HashSet<string> of account names from persisted _rules
        --> Never null; empty set = no rule saved for this context

Step 4: Guard check: if (saved.Count > 0)
        --> Skip restore silently if no saved state (fresh session or different context)

Step 5: foreach (_followerItems)
            if (item.Account != null && saved.Contains(item.Account.Name))
                item.IsSelected = true
        --> Restores checkbox state from persisted rule

Step 6: SortFollowerRows()
        --> Re-sorts so checked (selected) rows float to top of list

Step 7: TryAutoApply()
        --> Re-registers the live copy rule with the now-correct follower selection
        --> Without this step, no active rule exists until user manually toggles a checkbox
```

### Guard Conditions Rationale

- `_instrument == null`: chart not yet bound (panel opened before instrument loaded). Skip silently — OnLoaded fires again when instrument attaches.
- `_leaderAccount == null`: leader not resolved. Skip silently — `GetSavedFollowerNames` would return empty set anyway.
- `saved.Count == 0`: clean session, no prior state to restore. Skip silently — no side effects.

### CYC Impact on OnLoaded

The restore block adds four conditional branches to OnLoaded:
- Branch A: `if (_instrument != null && _leaderAccount != null)` (+1)
- Branch B: `if (saved.Count > 0)` (+1)
- Branch C: `foreach (_followerItems)` (+1 loop branch)
- Branch D: `if (item.Account != null && saved.Contains(...))` (+1)

Net additive: +4 branches to OnLoaded total CYC.
OnLoaded is not a hot path — it fires once per panel load. CYC impact is acceptable.
The restore block is straight-line at each step; no nested complexity.

---

## 5. P0 Gate (Rules Catalog Check)

Rules checked: JS-001 (no throw in hot paths), JS-002 (no return null), JS-021 (no lock), JS-033 (no async void).

### Scan Results: Three Hotfix Methods

| Check | Pattern | GetLeaderAtmTemplateName | OnCloneModeClick | OnLoaded restore block | GetSavedFollowerNames |
|-------|---------|--------------------------|-----------------|----------------------|----------------------|
| JS-021 `lock(` | `lock\s*\(` | 0 new | 0 new | 0 new | 0 new |
| JS-001 `throw new` | `throw\s+new\s+\w+Exception` | 0 new | 0 new | 0 new | 0 new |
| JS-002 `return null` | `return\s+null\s*;` | 0 (all paths return `string.Empty`) | n/a (void) | n/a (void) | 0 (returns empty HashSet) |
| JS-033 `async void` | `async\s+void` | 0 (static method) | 0 (sync void event handler) | 0 (sync void event handler) | 0 (non-async) |

### Pre-existing ASCII Arrows (Lines 1044-1107) — NOT B75-LaneB

The Unicode arrow characters at TradeCopierPanel.cs lines 1044-1107 are **pre-existing code** introduced by **B73-LaneB** (confirmed: B73-LaneB pipeline FINAL_PASS 2026-08-17, 15 hotfixes in TradeCopierPanel.cs). These arrows are **not introduced by any B75-LaneB hotfix**. B75-LaneB does not touch lines 1044-1107. The P0 ASCII scan for B75-LaneB is not affected by these pre-existing characters.

### P0 Gate Result

```
PASS — zero P0 violations in the three B75-LaneB hotfix methods.
```

---

## 6. Test Scope

Ten required tests, organized by hotfix. All are xUnit `[Fact]` tests per the V12 test framework mandate (JS-051..065, xUnit only — no NUnit, no MSTest).

### T_B66TPL — GetLeaderAtmTemplateName (5 tests)

| Test ID | Method Under Test | Assertion |
|---------|------------------|-----------|
| T_B66TPL_01 | `GetLeaderAtmTemplateName(null)` | Returns `string.Empty`; does not throw |
| T_B66TPL_02 | Chart has no ChartTrader child | Returns `string.Empty`; does not throw |
| T_B66TPL_03 | `ct.AtmStrategy` is non-null mock with `.Name = "MES $200 SL6"` | Returns `"MES $200 SL6"` (primary path) |
| T_B66TPL_04 | `ct.AtmStrategy` is null; `AtmStrategySelector` found with `SelectedAtmStrategy.Name = "ATM1"` | Returns `"ATM1"` (fallback-1 path) |
| T_B66TPL_05 | All paths null (no AtmStrategy, no selector, no index-2 ComboBox) | Returns `string.Empty` (not throw, not null) |

### T_B66OBJ_P — OnCloneModeClick object cache (2 tests, panel-side only)

| Test ID | Method Under Test | Assertion |
|---------|------------------|-----------|
| T_B66OBJ_P01 | `SetCloneAtmObjectCache(nonNullMock)` | `GetCloneAtmMode()` returns `Named` instance with `AtmObject != null` |
| T_B66OBJ_P02 | `SetCloneAtmObjectCache(null)` | Call completes without throw; `_cloneAtmObject` remains null; `GetCloneAtmMode()` returns `Inherit` when string cache also empty |

### T_B67 — GetSavedFollowerNames / OnLoaded restore (3 tests)

| Test ID | Method Under Test | Assertion |
|---------|------------------|-----------|
| T_B67_01 | `GetSavedFollowerNames(instrument, master)` with one matching rule having two followers | Returns `HashSet<string>` with both follower names |
| T_B67_02 | `GetSavedFollowerNames(instrument, master)` with no matching rule in `_rules` | Returns empty `HashSet<string>` (not null, not throw) |
| T_B67_03 | `OnLoaded` restore block: after `LoadFollowers()` with two followers in saved rule | Both matching `_followerItems` have `IsSelected = true`; non-matching items remain `IsSelected = false` |

---

## 7. CYC Pre-Check

### GetLeaderAtmTemplateName

Branches:
1. `if (currentChart == null) return` — null guard
2. `if (ct == null) return` — null guard
3. `if (ct.AtmStrategy != null)` — primary path condition
4. `if (sel?.SelectedAtmStrategy != null)` — fallback-1 condition
5. `catch {}` — exception handler (conventions vary: some tools count catch as +1, some do not)

CYC = 5 (or 4 not counting catch). Either: well below 8. **PASS**.

### OnCloneModeClick

Branches:
1. `if (_currentChart != null)` — sole branch

CYC = 2. **PASS**.

### OnLoaded restore block (additive contribution)

The restore block adds to OnLoaded's existing CYC:
- +1: `if (_instrument != null && _leaderAccount != null)`
- +1: `if (saved.Count > 0)`
- +1: `foreach (_followerItems)` loop
- +1: `if (item.Account != null && saved.Contains(...))` filter

Additive contribution: +4 branches. The restore block in isolation is CYC=4.
OnLoaded is a lifecycle method called once per panel creation — CYC is not a hot-path concern here.

### GetSavedFollowerNames (CopyEngine)

Branches:
1. `foreach (_rules)` — outer loop
2. `if (rule.Instrument != instrument || ...)` continue — filter
3. `foreach (f in rule.FollowerAccounts)` — inner loop
4. `if (f?.Name != null)` — null guard before Add

CYC = 4 + 1 base = 5. Comment in source says CYC=2 (counting only two foreach loops). Either counting: well below 8. **PASS**.

---

## 8. Data Flow Summary

### Clone Mode Dispatch (Post-B66 Fix)

```
UI: Clone radio click
  -> OnCloneModeClick
     -> FindVisualChild<ChartTrader>(_currentChart) -- UI thread required
     -> ct?.AtmStrategy                             -- live object captured
     -> SetCloneAtmObjectCache(atmObj)              -- volatile write
     -> GetLeaderAtmTemplateName(_currentChart)     -- string for display/logging only
     -> SetCloneAtmCache(tpl)                       -- volatile string write
  
NT8 order event thread:
  -> DispatchCopy
     -> GetCloneAtmMode()
        -> _cloneAtmObject != null ? Named(string, obj) : check string fallback
     -> SendCopyWithAtm(Named with AtmObject)
        -> StartAtmStrategy(namedMode.AtmObject, order)  -- object overload, NT8 manages submit
     -> Follower: "Entry" order + ATM brackets
```

### Checkbox Restore (Post-B67 Fix)

```
NT8 restart
  -> LoadRules() (CopyEngine)  -- _rules populated from disk
  -> OnLoaded (TradeCopierPanel, UI thread)
     -> _followerItems.Clear() + re-add with IsSelected=false
     -> LoadFollowers()         -- rows populated in ScrollViewer
     -> Guard: _instrument != null && _leaderAccount != null
     -> GetSavedFollowerNames(instrument, leader)  -- reads _rules (ConcurrentBag, safe)
     -> Guard: saved.Count > 0
     -> foreach _followerItems: IsSelected = true on name match
     -> SortFollowerRows()      -- checked rows float to top
     -> TryAutoApply()          -- live rule re-registered
  
First user checkbox toggle:
  -> TryAutoApply sees correctly populated followers
  -> Does NOT wipe the restored rule
```

---

## 9. Component List

| Component | Class | File | Role in B75-LaneB |
|-----------|-------|------|------------------|
| Panel | `TradeCopierPanel` | TradeCopierPanel.cs | Hosts OnLoaded, OnCloneModeClick, GetLeaderAtmTemplateName |
| Engine | `CopyEngine` | CopyEngine.cs | Hosts _cloneAtmObject field, SetCloneAtmObjectCache, GetSavedFollowerNames |
| Rule model | `CopyRule` | CopyEngine.cs | Read-only in restore path (FollowerAccounts list) |
| Follower row | `FollowerItem` | TradeCopierPanel.cs | IsSelected toggled in restore path |
| NT8 ChartTrader | `ChartTrader` | NT8 API | .AtmStrategy property used in GetLeaderAtmTemplateName + OnCloneModeClick |
| NT8 AtmStrategy | `NinjaTrader.NinjaScript.AtmStrategy` | NT8 API | Live object stored in _cloneAtmObject |
| NT8 AtmStrategySelector | `NinjaTrader.Gui.NinjaScript.AtmStrategy.AtmStrategySelector` | NT8 API | Fallback-1 in GetLeaderAtmTemplateName |

---

*Plan written by ptt-architect for B75-LaneB Phase 1.*
*Review with: ptt-plan-reviewer. On REVIEW_PASS, proceed to Phase 3 (tickets).*
