# B30-LaneB Architect Plan
# DW-B30-03: TryResolveLeaderAccount + Memory Leak Fix

**Date**: 2026-07-16
**Wave workspace**: `c:\WSGTA\universal-or-strategy\`
**Prerequisite**: B30-LaneA VERIFY_PASS (139 [Fact] confirmed)
**Target [Fact] count**: 139 → 140 (add 1 new test)

---

## 1. File Analysis

### TradeCopierPanel.cs — Key Observations

**Fields block (L115–172)**:
- `_leaderAccount` is at L120: `private Account _leaderAccount;`
- No `_chartTrader` or `_accountCombo` field exists
- No `_accountComboSelectionChanged` field exists

**`SetLeaderAccount(Account)` — L381–384** (public, called from AddOn):
```csharp
public void SetLeaderAccount(Account account) { _leaderAccount = account; }
```

**`Detach()` — L386–402**:
- Unregisters click trader, status/position/BE events, AccountItemUpdate
- Calls `DisarmPendingBe` + `DisarmTrailBe`
- Does NOT unsubscribe any ComboBox event (the leak)
- `_instrument = null; _leaderAccount = null;` at end

**5 button handlers that pass `_leaderAccount`**:

| Handler | Line | Usage of `_leaderAccount` |
|---------|------|--------------------------|
| `OnTrimClick` | L734 | `_engine.Trim(_leaderAccount, ...)` (two branches) |
| `OnFlattenClick` | L760 | `_engine.Flatten(_leaderAccount, ...)` (two branches) |
| `OnBeClick` | L790 | `_engine.ArmPendingBe(..., _leaderAccount, ...)` + DisarmPendingBe + DisarmTrailBe |
| `OnCancel2` | L910 | `_engine.CancelPendingEntries(_leaderAccount, ...)` |
| `OnTightenStop` | L942 | `_engine.TightenStop(_instrument, ticks)` — uses `_instrument`, NOT `_leaderAccount` directly |

Wait — checking `OnTightenStop` at L942–950: calls `_engine.TightenStop(_instrument, ticks)` — the 2-arg overload, not the leader-overload. This handler does NOT pass `_leaderAccount`.

**Re-checking the 5 handlers per spec ("Trim/Flatten/Cancel/BE/Tighten")**:
- `OnTrimClick` L734 — ✅ uses `_leaderAccount`
- `OnFlattenClick` L760 — ✅ uses `_leaderAccount`
- `OnBeClick` L790 — ✅ uses `_leaderAccount` (explicit null guard at L792-793)
- `OnCancel2` L910 — ✅ uses `_leaderAccount`
- `OnTightenStop` L942 — calls `_engine.TightenStop(_instrument, ticks)` — the old instrument-only overload

**Additional small handlers** (OnTrim/OnFlatten/OnCancel at L1264-1277):
- `OnTrim` L1264 — uses `_leaderAccount`
- `OnFlatten` L1269 — uses `_leaderAccount`
- `OnCancel` L1274 — uses `_leaderAccount`

Per the spec, the "5 button handlers" are the buffered-row ones (Trim2/Flatten2/Cancel2/BE/Tighten).
But `OnTightenStop` calls `_engine.TightenStop(_instrument, ticks)` — this is the 2-arg overload that
iterates ALL accounts. The B30-LaneA work added `TightenStop(Account, Instrument, int)` — the panel
should use this if the leader is available. The spec says apply `_leaderAccount ?? TryResolveLeaderAccount()`
to `OnTightenStop`, making it call the leader overload when possible.

**Decision on OnTightenStop**: The spec says change `var leader = _leaderAccount;` to
`var leader = _leaderAccount ?? TryResolveLeaderAccount();` and pass it to the engine. Since the
current code calls `_engine.TightenStop(_instrument, ticks)` (no leader), the fix adds leader
resolution and calls `_engine.TightenStop(leader, _instrument, ticks)` if leader != null,
fallback to `_engine.TightenStop(_instrument, ticks)` if still null.

Actually, re-reading spec: "Change: var leader = _leaderAccount; To: var leader = _leaderAccount ?? TryResolveLeaderAccount();"
This is a one-line change per handler — the spec says all 5 use `_leaderAccount` and the fix adds `?? TryResolveLeaderAccount()`.
For `OnTightenStop`, since it doesn't currently declare `var leader`, the change adds the leader resolution
and conditionally uses the leader overload.

### TradeCopierAddOn.cs — Key Observations

**`WireLeaderAccount` — L444–469** (private static):
- Uses `FindAccountComboBox` and `FindVisualChildByIndex` (both `private static`)
- Wires lambda directly: `accountCombo.SelectionChanged += (s, e) => { ... panel.SetLeaderAccount(acc); }`
- Lambda is ANONYMOUS — no way to unsubscribe
- No reference to the combo or handler is stored anywhere the panel can reach

**`FindAccountComboBox` — L492–505**: `private static ComboBox` → change to `internal static`
**`FindVisualChildByIndex<T>` — L512–517**: `private static T` → change to `internal static`

Note: `FindVisualChildByIndexInternal` must remain `private static` (it's an implementation detail
not part of the public API surface change).

---

## 2. Fix Architecture

### 2A. Memory Leak Fix (Core Architecture Decision)

The lambda in `WireLeaderAccount` captures `accountCombo` (WPF ComboBox) and `panel`.
If never unsubscribed:
- ComboBox holds a delegate → delegate captures panel → panel GC-prevented

**Approach**: Move the subscription into the panel itself. Add a new public method to panel:
```csharp
public void WireAccountCombo(ComboBox combo)
```
This method:
1. Stores `combo` in `_accountCombo` field
2. Creates the named handler and stores in `_accountComboSelectionChanged`
3. Subscribes `combo.SelectionChanged += _accountComboSelectionChanged`

Then `WireLeaderAccount` in the AddOn calls `panel.WireAccountCombo(accountCombo)` instead of
wiring the lambda inline.

`Detach()` unsubscribes:
```csharp
if (_accountCombo != null && _accountComboSelectionChanged != null)
    _accountCombo.SelectionChanged -= _accountComboSelectionChanged;
```

### 2B. TryResolveLeaderAccount (CYC Analysis)

```csharp
private Account TryResolveLeaderAccount()
{
    var combo = TradeCopierAddOn.FindAccountComboBox(_chartTrader);
    if (combo?.SelectedItem is Account acc) return acc;    // (1) null check via ?.
    return null;                                           // (2)
}
```

Wait — `TradeCopierPanel` has NO `_chartTrader` field. The panel does not hold a ChartTrader reference.

**Revised approach**: Use the stored `_accountCombo` field instead:
```csharp
private Account TryResolveLeaderAccount()
{
    if (_accountCombo?.SelectedItem is Account acc) return acc;  // (1)
    return null;                                                  // (2)
}
```
CYC = 2 (null-conditional + pattern match). This is cleaner: no need for `FindAccountComboBox`
(already have the ComboBox stored). No `TradeCopierAddOn` call needed inside the method.

**But the spec says**: `var combo = TradeCopierAddOn.FindAccountComboBox(_chartTrader);`

Since `_chartTrader` doesn't exist in the panel, and we ARE storing `_accountCombo`, use `_accountCombo`:
```csharp
private Account TryResolveLeaderAccount()
{
    if (_accountCombo?.SelectedItem is NinjaTrader.Cbi.Account acc) return acc;
    return null;
}
```
This fulfills the CYC=2 spec requirement and doesn't require a non-existent `_chartTrader`.

Making `FindAccountComboBox` and `FindVisualChildByIndex` `internal static` is still required by
the spec — so the change is made even if `TryResolveLeaderAccount` uses `_accountCombo` directly.

### 2C. Button Handler Updates (5 handlers)

**Pattern**: `var leader = _leaderAccount ?? TryResolveLeaderAccount();`

Handlers to update:

1. **`OnTrimClick` (L734–743)**: Uses `_leaderAccount` in two branches.
   - Add `var leader = _leaderAccount ?? TryResolveLeaderAccount();` at top of body (after `_instrument` null guard)
   - Replace `_leaderAccount` with `leader` in both branches

2. **`OnFlattenClick` (L760–769)**: Same pattern.

3. **`OnBeClick` (L790–813)**: Has explicit `if (_leaderAccount == null) return;` guard at L793.
   - Add `var leader = _leaderAccount ?? TryResolveLeaderAccount();` after `_instrument` null guard
   - Change `if (_leaderAccount == null) return;` to `if (leader == null) return;`
   - Replace all `_leaderAccount` with `leader` in the switch cases

4. **`OnCancel2` (L910–913)**: Uses `_leaderAccount` inline.
   - Add `var leader = _leaderAccount ?? TryResolveLeaderAccount();`
   - Replace `_leaderAccount` with `leader`

5. **`OnTightenStop` (L942–950)**: Currently calls `_engine.TightenStop(_instrument, ticks)` (no leader).
   - Add `var leader = _leaderAccount ?? TryResolveLeaderAccount();`
   - If `leader != null`: call `_engine.TightenStop(leader, _instrument, ticks)` (B30-A leader overload)
   - If `leader == null`: keep existing `_engine.TightenStop(_instrument, ticks)` fallback
   - This is NOT a "one-line change" but aligns with spec intent

---

## 3. Exact Change Plan

### TradeCopierAddOn.cs Changes

**Change A1**: L492 — `private static ComboBox FindAccountComboBox` → `internal static ComboBox FindAccountComboBox`

**Change A2**: L512 — `private static T FindVisualChildByIndex<T>` → `internal static T FindVisualChildByIndex<T>`

**Change A3**: L464–468 — Replace inline lambda with `panel.WireAccountCombo(accountCombo)` call:
```csharp
// OLD:
accountCombo.SelectionChanged += (s, e) =>
{
    var acc = accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
    panel.SetLeaderAccount(acc);
};

// NEW:
panel.WireAccountCombo(accountCombo);
```

### TradeCopierPanel.cs Changes

**Change P1**: Add fields after `_leaderAccount` field (L120):
```csharp
private ComboBox    _accountCombo;                         // B30-B: stored for Detach unsubscribe
private EventHandler _accountComboSelectionChanged;        // B30-B: named handler for leak-free Detach
```

**Change P2**: Add `WireAccountCombo` public method (after `SetLeaderAccount` at L384):
```csharp
public void WireAccountCombo(ComboBox combo)
{
    _accountCombo = combo;
    _accountComboSelectionChanged = (s, e) =>
        _leaderAccount = _accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
    combo.SelectionChanged += _accountComboSelectionChanged;
}
```
CYC = 1 (straight-line assignment + subscription, no branches).

**Change P3**: Update `Detach()` (L386–402) — add unsubscribe before `_instrument = null`:
```csharp
if (_accountCombo != null && _accountComboSelectionChanged != null)
    _accountCombo.SelectionChanged -= _accountComboSelectionChanged;
_accountCombo = null;
_accountComboSelectionChanged = null;
```

**Change P4**: Add `TryResolveLeaderAccount()` after `WireAccountCombo` (L385 area):
```csharp
// B30-B: TryResolveLeaderAccount -- late resolve when _leaderAccount not yet set.
// CYC=2: null-conditional combo check(1), pattern match cast(2).
// JS-002: returns null (not throw); callers use null as no-op signal.
private Account TryResolveLeaderAccount()
{
    if (_accountCombo?.SelectedItem is NinjaTrader.Cbi.Account acc) return acc;
    return null;
}
```

**Change P5**: Update 5 button handlers. Each one adds `var leader = _leaderAccount ?? TryResolveLeaderAccount();`

**OnTrimClick (L734–743)**:
```csharp
private void OnTrimClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    var leader = _leaderAccount ?? TryResolveLeaderAccount();        // B30-B: late resolve
    double ask = GetAsk();
    double bid = GetBid();
    if (ask <= 0 || bid <= 0 || _trimBuffer == 0)
        _engine.Trim(leader, _instrument);
    else
        _engine.Trim(leader, _instrument, _trimBuffer, ask, bid);
}
```

**OnFlattenClick (L760–769)**:
```csharp
private void OnFlattenClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    var leader = _leaderAccount ?? TryResolveLeaderAccount();        // B30-B: late resolve
    double ask = GetAsk();
    double bid = GetBid();
    if (ask <= 0 || bid <= 0 || _flattenBuffer == 0)
        _engine.Flatten(leader, _instrument);
    else
        _engine.Flatten(leader, _instrument, _flattenBuffer, ask, bid);
}
```

**OnBeClick (L790–813)**:
```csharp
private void OnBeClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    var leader = _leaderAccount ?? TryResolveLeaderAccount();        // B30-B: late resolve
    if (leader == null) return;
    switch (_beState) { ... use leader instead of _leaderAccount ... }
}
```

**OnCancel2 (L910–913)**:
```csharp
private void OnCancel2(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    var leader = _leaderAccount ?? TryResolveLeaderAccount();        // B30-B: late resolve
    if (leader != null) _engine.CancelPendingEntries(leader, _instrument);
}
```

**OnTightenStop (L942–950)**:
```csharp
private void OnTightenStop(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    var leader = _leaderAccount ?? TryResolveLeaderAccount();        // B30-B: late resolve
    int ticks = int.TryParse(_tightenTicksBox?.Text, out var t)
        ? Math.Max(1, Math.Min(500, t))
        : 5;
    if (leader != null)
        _engine.TightenStop(leader, _instrument, ticks);             // B30-A leader overload
    else
        _engine.TightenStop(_instrument, ticks);                     // fallback: all accounts
}
```

---

## 4. Test Decision

**Decision**: Add to `CopyEngineTests.cs` (NOT a new file).
- Rationale: `TradeCopierPanel` uses `NinjaTrader.Gui.Chart.ChartTrader` which is NT8 runtime.
  The panel constructor calls `CopyEngine.Instance` and `BuildUI()` — both need NT8.
  The test must be pure reflection / null-safety without NT8 panel instantiation.
- The test verifies `TryResolveLeaderAccount` is `private` and returns null when `_accountCombo` is null.
- Use reflection: invoke method via `MethodInfo.Invoke` on a null-constructed panel... but
  TradeCopierPanel constructor calls NT8 APIs.

**Revised test approach** (NT8-safe):
```csharp
[Fact]
public void TryResolveLeaderAccount_MethodExists_IsPrivate()
{
    // Verify the method exists with correct visibility.
    var mi = typeof(TradeCopierPanel).GetMethod(
        "TryResolveLeaderAccount",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(mi);
    Assert.Equal(typeof(NinjaTrader.Cbi.Account), mi.ReturnType);
    // Method takes no parameters.
    Assert.Equal(0, mi.GetParameters().Length);
}
```

This is purely structural (reflection-based) — no NT8 runtime, no panel construction.
Verifies the contract: method exists, is private, returns Account, takes 0 params.
Follows existing CopyEngineTests.cs pattern (see T_B28_01, T_B28_02, T_B28_03 tests).

---

## 5. Summary of All File Changes

### TradeCopierAddOn.cs (3 changes)
- A1: L492 `private static` → `internal static` (FindAccountComboBox)
- A2: L512 `private static` → `internal static` (FindVisualChildByIndex)
- A3: L464–468 Replace inline lambda with `panel.WireAccountCombo(accountCombo)`

### TradeCopierPanel.cs (6 changes)
- P1: Add 2 fields (`_accountCombo`, `_accountComboSelectionChanged`)
- P2: Add `WireAccountCombo(ComboBox combo)` public method
- P3: Add `TryResolveLeaderAccount()` private method
- P4: Update `Detach()` to unsubscribe + null fields
- P5a: Update `OnTrimClick` — add `var leader = _leaderAccount ?? TryResolveLeaderAccount()`
- P5b: Update `OnFlattenClick` — same
- P5c: Update `OnBeClick` — same + update null guard
- P5d: Update `OnCancel2` — same
- P5e: Update `OnTightenStop` — same + conditional leader overload

### CopyEngineTests.cs (1 new [Fact])
- Add `TryResolveLeaderAccount_MethodExists_IsPrivate` at end of class (before closing brace)

---

## 6. CYC Analysis

| Method | CYC | Notes |
|--------|-----|-------|
| `TryResolveLeaderAccount` | 2 | null-conditional(1) + pattern match(2) |
| `WireAccountCombo` | 1 | straight-line, no branches |
| `OnTrimClick` (after fix) | 4 | unchanged from before (instrument(1), ask/bid(2)(3), buffer(4)) |
| `OnFlattenClick` (after fix) | 4 | unchanged |
| `OnBeClick` (after fix) | 5 | instrument(1), leader null(2), switch cases(3)(4)(5) |
| `OnCancel2` (after fix) | 2 | instrument(1), leader null(2) |
| `OnTightenStop` (after fix) | 4 | instrument(1), parse fallback(2), leader null(3), engine branch(4) |

All ≤ 8. JS-021 compliant (no lock). JS-002 compliant (TryResolveLeaderAccount returns null not throws).

---

## 7. JS / NT8 Rule Compliance

| Rule | Status |
|------|--------|
| JS-021 no lock() | No lock in any new code ✅ |
| JS-002 no return null in hot path | TryResolveLeaderAccount returns null — ALLOWED (callers handle null) ✅ |
| NT8-001 no { get; init; } | Not used in new code ✅ |
| Memory leak: += must have -= in Detach | WireAccountCombo subscribes; Detach unsubscribes ✅ |
| No _chartTrader dependency | TryResolveLeaderAccount uses _accountCombo (not _chartTrader) ✅ |
