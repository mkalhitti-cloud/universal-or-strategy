# Architecture Plan — BWAVE-DW-REPAIR-LANEC

**Status**: REVIEW_PASS  
**Branch**: feature/bwave-dw-lane-c  
**Brain dir**: docs/brain/BWAVE-DW/Repair-LaneC/  
**Date**: 2026-08-20  
**Pipeline**: SINGLE-PIPELINE (same branch, trivial scope, no parallel execution benefit)

---

## 0. Deferred Backlog

`docs/brain/BWAVE-DW/LaneC/06-deferred-backlog.md` — file does not exist. No prior deferred items to close.

---

## 1. Scope Summary

Two targeted bug-fix tickets on `feature/bwave-dw-lane-c`:

| Ticket | DW ID | File(s) | Description |
|--------|-------|---------|-------------|
| R-LC-1 | DW-C39-05b | `TradeCopierWindow.cs` | ApplyFeatureFlags timing: move call inside Dispatcher.InvokeAsync lambda |
| R-LC-2 | DW-C39-20 | `CopyEngine.cs`, `TradeCopierPanel.cs`, `TradeCopierAddOn.cs` | Clear remaining pending BE slots when last panel closes |

---

## 2. Component Map

### R-LC-1 Components

| Component | Class | Method | Change |
|-----------|-------|--------|--------|
| Trade Copier window | `TradeCopierWindow` | `RefreshRuleRows()` | Add `ApplyFeatureFlags` call inside `Dispatcher.InvokeAsync` lambda |

No new methods. No new classes. One line added inside an existing lambda.

### R-LC-2 Components

| Component | Class | Method | Change |
|-----------|-------|--------|--------|
| Copy engine | `CopyEngine` | `ClearAllPendingBeSlots()` | **NEW** internal void — unsubscribes all pending BE slot handlers then clears dict |
| AddOn root | `TradeCopierAddOn` | `IsPanelsEmpty()` | **NEW** internal static bool — returns `_panels.IsEmpty` |
| Panel teardown | `TradeCopierPanel` | `Detach()` | Add 2-line guard after `DisarmPendingBe` call |

---

## 3. Method Signatures

### New methods for R-LC-2

```csharp
// CopyEngine.cs — place after IsPendingSlotsEmpty() (line 5764)
// DW-C39-20: Clear all pending BE slots when last panel closes.
// Called from TradeCopierPanel.Detach() when _panels is empty.
// JS-021: no lock -- ConcurrentDictionary foreach and Clear() are thread-safe.
// CYC=3 (base 1 + foreach 1 + null guard 1).
internal void ClearAllPendingBeSlots()
{
    foreach (var kvp in _pendingBeSlots)
    {
        if (kvp.Value.Account != null)
            kvp.Value.Account.AccountItemUpdate -= OnPendingBeAccountUpdate;
    }
    _pendingBeSlots.Clear();
}

// TradeCopierAddOn.cs — place as a static helper near _panels field (~line 41)
// DW-C39-20: Returns true when all panels have been detached.
// Called by TradeCopierPanel.Detach() for last-panel-close guard.
// JS-021: ConcurrentDictionary.IsEmpty is lock-free. CYC=1.
internal static bool IsPanelsEmpty() => _panels.IsEmpty;
```

---

## 4. Detailed Change Specifications

### R-LC-1 — RefreshRuleRows fix (TradeCopierWindow.cs)

**Problem**:
- `OnLoaded()` calls `RefreshRuleRows()` then immediately calls `ApplyFeatureFlags()` at line 153.
- `RefreshRuleRows()` queues its row-rebuild via `Dispatcher.InvokeAsync` (asynchronous).
- `ApplyFeatureFlags` runs synchronously at line 153 BEFORE the lambda fires.
- On startup with persisted rules, rows are rebuilt AFTER flags have been applied → Arm BE and Tighten buttons left enabled for Starter users.

**Before** (`RefreshRuleRows()` lines 161–174):
```csharp
private void RefreshRuleRows()
{
    var instruments = new System.Collections.Generic.List<string>();
    foreach (var instr in CopyEngine.Instance.GetRuleInstruments())
        instruments.Add(instr);
    if (instruments.Count == 0)
        return;
    Dispatcher.InvokeAsync(() =>
    {
        _rulesPanel.Children.Clear();
        foreach (var instr in instruments)
            _rulesPanel.Children.Add(BuildRuleRow(instr));
    });
}
```

**After**:
```csharp
private void RefreshRuleRows()
{
    var instruments = new System.Collections.Generic.List<string>();
    foreach (var instr in CopyEngine.Instance.GetRuleInstruments())
        instruments.Add(instr);
    if (instruments.Count == 0)
        return;
    Dispatcher.InvokeAsync(() =>
    {
        _rulesPanel.Children.Clear();
        foreach (var instr in instruments)
            _rulesPanel.Children.Add(BuildRuleRow(instr));
        ApplyFeatureFlags(CopyEngine.Instance.Flags); // DW-C39-05b: apply flags after rows are built
    });
}
```

**Line 153 retention rationale**:
The call at line 153 (`ApplyFeatureFlags(CopyEngine.Instance.Flags)`) is **NOT removed**. It handles the path where `instruments.Count == 0` — `RefreshRuleRows` returns early, the Dispatcher.InvokeAsync lambda never fires, and the default MES row already in the DOM needs flags applied. The two-call pattern is intentional:

| Path | instruments.Count | Line 153 call | Lambda call |
|------|-------------------|---------------|-------------|
| No saved rules | 0 | Applies to default MES row (needed) | Never fires |
| Saved rules exist | > 0 | Applies before rows rebuilt (harmless, idempotent) | Applies after rebuild (correct) |

`ApplyFeatureFlags` is a pure setter — idempotent. Double-call on the saved-rules path is harmless.

**CYC analysis**: RefreshRuleRows after fix = 3 (unchanged).
- Branch 1: `instruments.Count == 0` guard
- Branch 2: `foreach (var instr in instruments)` in lambda
- No new branches from `ApplyFeatureFlags(...)` call

---

### R-LC-2 — Last-panel pending BE slot leak fix

**Problem**:
- `DW-C38-03` fixed `Detach()` to disarm only the OWN leader account's pending BE slot.
- When BE ALL arms multiple accounts and the LAST panel closes, the remaining slots for other accounts are never cleared.
- Those `AccountItemUpdate` watchers stay subscribed permanently → memory/handler leak.

**Root cause verified from code**:
- `TradeCopierAddOn.OnWindowDestroyed` (line 108): `_panels.TryRemove(chart, out panel)` → then `panel.Detach()`.
- `_panels` is already empty (for the last panel) BEFORE `Detach()` is called.
- `CopyEngine._pendingBeSlots` (line 270): `ConcurrentDictionary<string, PendingBeSlot>` — persists all armed slots.
- `IsPendingSlotsEmpty()` (line 5764): public-internal, returns `_pendingBeSlots.IsEmpty`.

#### Change 1 — CopyEngine.cs: Add ClearAllPendingBeSlots()

**Location**: After `IsPendingSlotsEmpty()` at line 5764.

```csharp
// DW-C39-20: Clear all pending BE slots when last panel closes.
// Called from TradeCopierPanel.Detach() when _panels is empty.
// JS-021: no lock -- ConcurrentDictionary.Clear() is thread-safe.
// Unsubscribe before Clear() to prevent orphan event handlers.
// CYC=3 (base 1 + foreach 1 + null guard 1).
internal void ClearAllPendingBeSlots()
{
    foreach (var kvp in _pendingBeSlots)
    {
        if (kvp.Value.Account != null)
            kvp.Value.Account.AccountItemUpdate -= OnPendingBeAccountUpdate;
    }
    _pendingBeSlots.Clear();
}
```

**Design note on CYC**: CYC=3 (not 2 as stated in scope brief). The foreach adds 1 branch, the null guard adds 1 branch, base = 1. Total = 3. Well within ≤ 8.

**Unsubscription order**: Event unsubscription (`-= OnPendingBeAccountUpdate`) occurs INSIDE the foreach loop, BEFORE `_pendingBeSlots.Clear()`. This prevents orphan handlers that would fire after the slot is removed from the dictionary.

#### Change 2 — TradeCopierAddOn.cs: Add IsPanelsEmpty()

**Location**: After the `_panels` field declaration (~line 41), or grouped with other internal helpers.

```csharp
// DW-C39-20: Returns true when all panels have been detached.
// Called by TradeCopierPanel.Detach() for last-panel-close guard.
// JS-021: ConcurrentDictionary.IsEmpty is lock-free. CYC=1.
internal static bool IsPanelsEmpty() => _panels.IsEmpty;
```

**Pattern**: Mirrors the existing `IsPendingSlotsEmpty()` pattern in `CopyEngine` (line 5764).

#### Change 3 — TradeCopierPanel.cs: Guard in Detach()

**Location**: After `_engine.DisarmPendingBe(_leaderAccount)` at line 591.

**Before** (line 591):
```csharp
_engine.DisarmPendingBe(_leaderAccount);
// B32: DisarmTrailBe removed -- PTT no longer runs trail after BE (DW-B32-05).
```

**After**:
```csharp
_engine.DisarmPendingBe(_leaderAccount);
// DW-C39-20: if this was the last panel, clear remaining global pending BE slots.
// TradeCopierAddOn.TryRemove ran before Detach() -- _panels is already empty if last panel.
if (TradeCopierAddOn.IsPanelsEmpty())
    _engine.ClearAllPendingBeSlots();
// B32: DisarmTrailBe removed -- PTT no longer runs trail after BE (DW-B32-05).
```

---

## 5. JS Rule Constraints

### R-LC-1

| Rule | Constraint | Compliance |
|------|-----------|-----------|
| JS-021 | No `lock()` | PASS — Dispatcher.InvokeAsync is not a lock. Lambda is a synchronous Action on the UI thread. |
| JS-033 | No `async void` | PASS — `RefreshRuleRows` is `private void` (not async void). Lambda is a synchronous Action delegate, not an async lambda. |
| JS-001 | No throw in hot path | PASS — No exceptions thrown. |
| JS-002 | No return null | PASS — void method, no return value. |

### R-LC-2

| Rule | Constraint | Compliance |
|------|-----------|-----------|
| JS-021 | No `lock()` | PASS — `ConcurrentDictionary.Clear()`, `.IsEmpty`, and foreach enumeration are all lock-free. Event `-=` on NT8 Account is safe from any thread. |
| JS-033 | No `async void` | PASS — All new methods are non-async. `Detach()` is `public void` (non-async). |
| JS-001 | No throw in hot path | PASS — No exceptions thrown in any new code. |
| JS-002 | No return null | PASS — `ClearAllPendingBeSlots` is void. `IsPanelsEmpty` returns bool. Neither returns null. |
| ASCII-only | No Unicode identifiers or strings | PASS — All identifiers and comment strings are ASCII-only. |

---

## 6. Threading Model

### R-LC-1 Threading

| Call site | Thread | Safe? |
|-----------|--------|-------|
| `OnLoaded()` | UI thread (WPF Loaded event) | Yes |
| `RefreshRuleRows()` call | UI thread (called from OnLoaded) | Yes |
| `Dispatcher.InvokeAsync` lambda | UI thread (queued to UI dispatcher) | Yes |
| `ApplyFeatureFlags(...)` inside lambda | UI thread | Yes — same as existing call at line 153 |
| `_rulesPanel.Children` access | UI thread (inside lambda) | Yes — WPF UIElementCollection, UI-thread-only |

No threading changes. The fix moves a UI-thread call from synchronous dispatch to deferred dispatch on the same UI thread.

### R-LC-2 Threading

| Call site | Thread | Safe? |
|-----------|--------|-------|
| `OnWindowDestroyed` | NT8 event thread | Yes |
| `_panels.TryRemove` | NT8 event thread | Yes — ConcurrentDictionary |
| `panel.Detach()` | NT8 event thread | Yes — existing pattern |
| `IsPanelsEmpty()` | NT8 event thread | Yes — ConcurrentDictionary.IsEmpty is lock-free |
| `ClearAllPendingBeSlots()` | NT8 event thread | Yes — ConcurrentDictionary.Clear() is lock-free |
| `AccountItemUpdate -= ...` inside ClearAllPendingBeSlots | NT8 event thread | Yes — NT8 event subscribe/unsubscribe is thread-safe |

No Dispatcher.InvokeAsync needed — B5 contains no UI work.

---

## 7. Data Flow

### R-LC-1 Data Flow

```
NT8 startup
  → State.Configure → CopyEngine.Instance.SetFlags(flags)
  → OnLoaded()
      → CopyEngine.Instance.LoadRules()           (populates rule instruments)
      → RefreshRuleRows()
          → instruments.Count == 0?
              YES → return                          (early exit)
                  → line 153: ApplyFeatureFlags()  (correct: default MES row)
              NO → Dispatcher.InvokeAsync(lambda)  (queue to UI thread)
          → line 153: ApplyFeatureFlags()           (runs before lambda -- idempotent)
          [later]
          → lambda fires:
              → _rulesPanel.Children.Clear()
              → foreach: BuildRuleRow(instr)        (builds rows from saved rules)
              → ApplyFeatureFlags()                 (CORRECT: runs after rows exist)
```

### R-LC-2 Data Flow

```
Chart window closed
  → OnWindowDestroyed(window)
      → _panels.TryRemove(chart, out panel)        (atomic removal)
          → _panels.IsEmpty == true if last panel
      → panel.Detach()
          → UnsubscribeFollowerItems()
          → DisarmPendingBe(_leaderAccount)         (removes own slot)
          → IsPanelsEmpty()? YES                    (NEW guard)
              → ClearAllPendingBeSlots()
                  → foreach kvp in _pendingBeSlots
                      → if kvp.Value.Account != null
                          → kvp.Value.Account.AccountItemUpdate -= OnPendingBeAccountUpdate
                  → _pendingBeSlots.Clear()
                  → all orphan handlers removed
          → ...rest of Detach()
```

**Concurrent closure edge case**: If two panels close simultaneously, `TryRemove` is atomic. Only the panel that reduces `_panels` to zero will see `IsPanelsEmpty() == true`. The other panel's `DisarmPendingBe` will have already removed its own slot. `ClearAllPendingBeSlots()` clears whatever remains (potentially zero slots if all were disarmed). `ConcurrentDictionary.Clear()` is safe to call on an empty dict. CORRECT.

---

## 8. NinjaTrader 8 API Usage

All NT8 API patterns in both fixes replicate existing verified patterns from the same source file. No new NT8 API types are introduced.

| NT8 API | Pattern | Source reference in existing code |
|---------|---------|-----------------------------------|
| `Dispatcher.InvokeAsync(Action)` | WPF dispatcher queue | Line 168 — already in use |
| `Account.AccountItemUpdate -= handler` | Event unsubscription | Line 5758 — `DisarmPendingBe` |
| `ConcurrentDictionary.IsEmpty` | Lock-free atomic read | Line 5764 — `IsPendingSlotsEmpty()` |
| `ConcurrentDictionary.Clear()` | Lock-free bulk remove | New usage; .NET BCL, thread-safe |

**Key NT8 facts confirmed from NT8_FULL_REFERENCE.md / NT8_ADDON_KNOWLEDGE.md**:
- `AtmStrategyChangeStopTarget()` — StrategyBase-only, NOT used in either fix.
- `AtmStrategyCreate()` — StrategyBase-only, NOT used in either fix.
- Event subscribe/unsubscribe on `Account` objects is safe from non-UI threads.
- No `Account.CreateOrder()` or `Account.Cancel()` calls — not applicable to these fixes.

---

## 9. CYC Analysis Summary

| Method | File | CYC Before | CYC After | Within ≤8? |
|--------|------|-----------|----------|-----------|
| `RefreshRuleRows()` | TradeCopierWindow.cs | 3 | 3 | YES |
| `ClearAllPendingBeSlots()` | CopyEngine.cs | N/A (new) | 3 | YES |
| `IsPanelsEmpty()` | TradeCopierAddOn.cs | N/A (new) | 1 | YES |
| `Detach()` | TradeCopierPanel.cs | ~6 | ~7 | YES |

`Detach()` CYC branch count (conservative estimate):
1. `if (_currentChart != null)` → +1
2. `if (_leaderAccount != null)` → +1
3. `if (_accountCombo != null && _accountComboSelectionChanged != null)` → +1
4. `foreach (IPttModule m in _modules)` → +1
5. NEW: `if (TradeCopierAddOn.IsPanelsEmpty())` → +1
Base = 1 + 5 branches = 6 total (not accounting for UnsubscribeFollowerItems which is extracted). CYC = 6. Well within ≤8.

---

## 10. NT8 Sync Requirements

Both tickets touch `.cs` files in `src/PropTraderTools/`. **NT8 sync is required** after all changes.

**Command**:
```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```

Expected result: `18/18 OK` (or current file count — all files verified, 0 MISMATCH).

After sync: **Press F5 in NinjaTrader 8** to recompile. Build must be green.

**Files to sync** (4 .cs files total across both tickets):
- R-LC-1: `src/PropTraderTools/TradeCopierWindow.cs`
- R-LC-2: `src/PropTraderTools/CopyEngine.cs`
- R-LC-2: `src/PropTraderTools/TradeCopierPanel.cs`
- R-LC-2: `src/PropTraderTools/TradeCopierAddOn.cs`

---

## 11. xUnit Test Requirements

### R-LC-1

No new public API surface. The fix is a UI timing behavior that requires WPF dispatcher infrastructure to test properly. xUnit unit test is not practical for this lambda timing scenario.

**Acceptance path**: SIM verification — startup with Starter license + persisted rules. Verify Arm BE and Tighten buttons are disabled on startup.

### R-LC-2

`ClearAllPendingBeSlots()` is `internal void` — testable via `[assembly: InternalsVisibleTo("...Tests")]`.

**xUnit test** (if test project has InternalsVisibleTo access):
```csharp
[Fact]
public void ClearAllPendingBeSlots_WhenSlotsArmed_SlotsAreEmpty()
{
    // Arrange: arm slots via CopyEngine (test-only overload or reflection)
    // Act: engine.ClearAllPendingBeSlots()
    // Assert: engine.IsPendingSlotsEmpty() == true
}
```

**Test name**: `ClearAllPendingBeSlots_WhenSlotsArmed_SlotsAreEmpty`

`IsPanelsEmpty()` is a static read of a static ConcurrentDictionary — requires a running AddOn to populate. Not unit-testable in isolation.

**Acceptance path for IsPanelsEmpty/Detach guard**: SIM verification — arm BE ALL on two accounts (two panels), close last chart, verify no orphan AccountItemUpdate handlers (check via logging or SIM debug).

---

## 12. SCAN Checklists (SCAN-01 through SCAN-07)

### R-LC-1 SCAN

| # | Scan | Check | Result |
|---|------|-------|--------|
| SCAN-01 | `lock()` grep | `grep -r "lock(" src/PropTraderTools/TradeCopierWindow.cs` | No new lock() introduced |
| SCAN-02 | `async void` grep | `grep -rn "async void " src/PropTraderTools/TradeCopierWindow.cs` | No async void introduced |
| SCAN-03 | `return null` grep | `grep -rn "return null" src/PropTraderTools/TradeCopierWindow.cs` | No return null introduced |
| SCAN-04 | ASCII-only | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/TradeCopierWindow.cs` | No non-ASCII characters |
| SCAN-05 | CYC ≤ 8 | `python scripts/complexity_audit.py` for TradeCopierWindow.cs | RefreshRuleRows CYC=3 |
| SCAN-06 | NT8 sync | `powershell -File scripts\ptt-sync-and-verify.ps1` | 0 MISMATCH |
| SCAN-07 | Build | F5 in NinjaTrader 8 | Green compile |

### R-LC-2 SCAN

| # | Scan | Check | Result |
|---|------|-------|--------|
| SCAN-01 | `lock()` grep | `grep -r "lock(" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/TradeCopierPanel.cs src/PropTraderTools/TradeCopierAddOn.cs` | No new lock() introduced |
| SCAN-02 | `async void` grep | Same files | No async void introduced |
| SCAN-03 | `return null` grep | Same files | No return null introduced |
| SCAN-04 | ASCII-only | `grep -Pn "[^\x00-\x7F]"` on all 3 files | No non-ASCII characters |
| SCAN-05 | CYC ≤ 8 | `python scripts/complexity_audit.py` for all 3 files | ClearAllPendingBeSlots CYC=3, IsPanelsEmpty CYC=1, Detach CYC≤8 |
| SCAN-06 | NT8 sync | `powershell -File scripts\ptt-sync-and-verify.ps1` | 0 MISMATCH |
| SCAN-07 | Build | F5 in NinjaTrader 8 | Green compile |

---

## 13. Acceptance Criteria

### R-LC-1 Acceptance Criteria

1. **Code change**: `ApplyFeatureFlags(CopyEngine.Instance.Flags);` added as final line inside the `Dispatcher.InvokeAsync` lambda in `RefreshRuleRows()`.
2. **Line 153 unchanged**: `ApplyFeatureFlags(CopyEngine.Instance.Flags)` at line 153 in `OnLoaded` is NOT removed.
3. **CYC unchanged**: `RefreshRuleRows()` CYC remains 3.
4. **SIM gate**: Starter license + persisted rules — Arm BE and Tighten buttons are DISABLED after startup. Previously they were enabled.
5. **All 7 scans pass**.

### R-LC-2 Acceptance Criteria

1. **New method `ClearAllPendingBeSlots()`**: Present in `CopyEngine.cs` with correct body (unsubscribe before Clear, no lock).
2. **New method `IsPanelsEmpty()`**: Present in `TradeCopierAddOn.cs`, `internal static bool`, returns `_panels.IsEmpty`.
3. **Detach() guard**: Two lines added after `DisarmPendingBe` call — `if (TradeCopierAddOn.IsPanelsEmpty()) _engine.ClearAllPendingBeSlots();`
4. **CYC**: `ClearAllPendingBeSlots()` CYC=3, `IsPanelsEmpty()` CYC=1, `Detach()` CYC≤8.
5. **SIM gate**: Close last chart after BE ALL armed on two accounts — no orphan AccountItemUpdate handlers remain (verified via debug log or `IsPendingSlotsEmpty() == true`).
6. **All 7 scans pass**.

---

## 14. File Change Summary

| File | Ticket | Change type | Lines changed |
|------|--------|-------------|---------------|
| `src/PropTraderTools/TradeCopierWindow.cs` | R-LC-1 | Modify: add 1 line inside lambda | +1 |
| `src/PropTraderTools/CopyEngine.cs` | R-LC-2 | Add: new method after line 5764 | +10 |
| `src/PropTraderTools/TradeCopierAddOn.cs` | R-LC-2 | Add: new method after line 41 | +4 |
| `src/PropTraderTools/TradeCopierPanel.cs` | R-LC-2 | Modify: add 3 lines after line 591 | +3 |

**Total diff**: +18 lines across 4 files. Well within 10k character diff limit.

---

*Plan authored by ptt-architect. Requires REVIEW_PASS from ptt-plan-reviewer before ptt-engineer execution.*
