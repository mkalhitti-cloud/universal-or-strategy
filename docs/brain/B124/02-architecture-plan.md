# B124 Architecture Plan — BE Button Active-State Brush + Arm Guard

**Status**: REVIEW_PASS candidate  
**Block**: B124  
**Phase**: 2 — Architecture  
**Files in scope**: `src/PropTraderTools/TradeCopierPanel.cs` (2 locations), `src/PropTraderTools/Tests/B124Tests.cs` (new)  
**Files explicitly excluded**: `CopyEngine.cs` — ZERO changes permitted

---

## 1. Summary

B124 delivers two surgical fixes to `TradeCopierPanel.cs`:

| Fix | Method | Change | Lines affected |
|-----|--------|--------|---------------|
| Fix 1 | `UpdateBeAllVisuals` | Replace `BrushCaution` with `BrushActive` in the armed else-branch | 1 line (~line 1061) |
| Fix 2 | `OnGlobalBeClick` | Replace disarm else-body with guard: log `[PTT-BE-ALL] already armed, ignoring double-press` + `return` | ~5 lines replaced by 2 (~lines 1388-1399) |

A new test file `B124Tests.cs` is created with 2 xUnit `[Fact]` tests covering both behaviors.

No `CopyEngine.cs` changes. No new fields. No new NT8 API surface. No new WPF resources.

---

## 2. Fix 1 — `UpdateBeAllVisuals` Brush Change

**Location**: `src/PropTraderTools/TradeCopierPanel.cs` line ~1061

**Current code (else-branch)**:
```csharp
else
{
    _globalBeBtn2.Background = BrushCaution;  // amber
}
```

**After B124**:
```csharp
else
{
    _globalBeBtn2.Background = BrushActive;   // green #22c55e (same as COPY active)
}
```

**BrushActive definition** (already exists at line 314, unchanged):
```csharp
private static readonly SolidColorBrush BrushActive = MakeBrush(34, 197, 94);
```

**Idle state**: Remains `Brushes.Transparent` — existing behavior preserved.  
The spec text "returns to BrushInactive" is superseded by the existing code observation: idle branch already uses `Transparent`. No change to the idle branch.

---

## 3. Fix 2 — `OnGlobalBeClick` Double-Press Guard

**Location**: `src/PropTraderTools/TradeCopierPanel.cs` line ~1388

**Current else-branch (armed state)**:
```csharp
else
{
    // Currently Armed -- disarm
    NinjaTrader.Code.Output.Process(
        "[BE-ALL] button: disarm all",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    if (Account.All != null)
        foreach (var acc in Account.All)
            CopyEngine.Instance.DisarmPendingBe(acc);
    UpdateBeAllVisuals(BeState.Idle);
}
```

**After B124 (else-branch replacement)**:
```csharp
else
{
    // Already armed -- guard: log and return (no disarm, no re-arm)
    NinjaTrader.Code.Output.Process(
        "[PTT-BE-ALL] already armed, ignoring double-press",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    return;
}
```

**What is removed**:
- `Account.All` iteration (NT8 API surface reduced)
- `CopyEngine.Instance.DisarmPendingBe(acc)` call
- `UpdateBeAllVisuals(BeState.Idle)` call from this path

**What is added**:
- Guard log message (ASCII-only)
- Early `return;`

---

## 4. Behavioral Note — BREAKING CHANGE

| Scenario | Before B124 | After B124 |
|----------|-------------|------------|
| _globalBeBtn2 clicked while idle | Arms (Execute called) | Arms (Execute called) — unchanged |
| _globalBeBtn2 clicked while already armed | **Disarms all** (DisarmPendingBe foreach) | **No-op** (log + return) |

**Rationale**: The guard prevents stacking multiple BE brackets that would result from a second Execute() call. The spec explicitly requires "log and return" with no disarm. This replaces the toggle-disarm behavior entirely.

**Impact**: Disarming pending BE via this button is no longer possible after arming. Disarm must occur via another mechanism (e.g., BE resolution, separate disarm control). This is an intentional product decision per the spec.

---

## 5. CYC Analysis

### `UpdateBeAllVisuals(BeState state)`

| Path | Count |
|------|-------|
| Base | 1 |
| `if (_globalBeBtn2 == null)` | +1 |
| `if (state == BeState.Idle)` | +1 |
| **Pre-B124 CYC** | **3** |
| **Post-B124 CYC** | **3** (no new branch; only constant reference swapped) |

✓ CYC 3 ≤ 8

### `OnGlobalBeClick(object sender, RoutedEventArgs e)`

| Path | Pre-B124 | Post-B124 |
|------|----------|-----------|
| Base | 1 | 1 |
| `if (IsPendingSlotsEmpty())` | +1 | +1 |
| `if (Account.All != null)` in else | +1 | — (removed) |
| `foreach` in else | +1 | — (removed) |
| **CYC** | **4** | **2** |

✓ CYC pre=4, post=2. Both ≤ 8. Complexity decreases with this fix.

---

## 6. Test Plan — `B124Tests.cs`

**File**: `src/PropTraderTools/Tests/B124Tests.cs`  
**Framework**: xUnit (NUnit and MSTest BANNED per project mandate)  
**Pattern**: Delegate injection via `PttGlobalBreakEven` test-injection constructor — counts invocations without touching NT8 APIs.

### Test 1 — `GuardReturnsWithoutRearmingWhenAlreadyArmed`

```csharp
[Fact]
public void GuardReturnsWithoutRearmingWhenAlreadyArmed()
```

**Arrange**:
- Seed `IsPendingSlotsEmpty()` stub → returns `false` (slots NOT empty = already armed)
- Capture `_executeCallCount` at its current value (represents prior arm state)

**Act**:
- Invoke click handler (second press simulation)

**Assert**:
- `_executeCallCount` has NOT been incremented (Execute was not called again)
- Guard path taken: no re-arm, no disarm

### Test 2 — `FirstPressArmsWhenNotYetArmed`

```csharp
[Fact]
public void FirstPressArmsWhenNotYetArmed()
```

**Arrange**:
- Seed `IsPendingSlotsEmpty()` stub → returns `true` (slots empty = idle, not armed)
- `_executeCallCount` starts at 0

**Act**:
- Invoke click handler (first press simulation)

**Assert**:
- `_executeCallCount == 1` (Execute called exactly once)

---

## 7. Seven-Scan Checklist (Pre-populated for Ticket Phase)

| Scan | Check | Command | Expected |
|------|-------|---------|----------|
| SCAN-01 | JS-021: `lock()` ban | `grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs` | 0 results in modified lines |
| SCAN-02 | JS-033: `async void` ban | `grep -n "async void" src/PropTraderTools/TradeCopierPanel.cs` | 0 results |
| SCAN-03 | CYC all modified methods ≤ 8 | `python scripts/complexity_audit.py` on `UpdateBeAllVisuals`, `OnGlobalBeClick` | `UpdateBeAllVisuals`=3, `OnGlobalBeClick`=2 |
| SCAN-04 | ASCII-only in modified string literals | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/TradeCopierPanel.cs` | 0 results in new/modified lines |
| SCAN-05 | `return null` in modified methods | `grep -n "return null" src/PropTraderTools/TradeCopierPanel.cs` in scope of `UpdateBeAllVisuals` and `OnGlobalBeClick` | 0 results |
| SCAN-06 | Build | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 0 warnings (new) |
| SCAN-07 | xUnit tests pass | `dotnet test` targeting `B124Tests.cs` | Test 1 PASS, Test 2 PASS |

---

## 8. Files Changed

| File | Change Type | Description |
|------|-------------|-------------|
| `src/PropTraderTools/TradeCopierPanel.cs` | Modified (2 locations) | Fix 1: `UpdateBeAllVisuals` line ~1061 — `BrushCaution` → `BrushActive`; Fix 2: `OnGlobalBeClick` else-branch ~lines 1388-1399 — replace disarm body with guard log + return |
| `src/PropTraderTools/Tests/B124Tests.cs` | New file | xUnit `[Fact]` tests: `GuardReturnsWithoutRearmingWhenAlreadyArmed`, `FirstPressArmsWhenNotYetArmed` |

**Files confirmed NOT changed**: `CopyEngine.cs`, `TradeCopierAddOn.cs`, `TradeCopierWindow.cs`, all other files.

---

## 9. Deferred Items

None. B124 is self-contained. No items deferred from B107 block this work.  
B124 is parallel-safe with B121 and B122 (no shared method mutations).
