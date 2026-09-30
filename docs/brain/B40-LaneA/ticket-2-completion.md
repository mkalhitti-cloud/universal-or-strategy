# B40-LaneA Ticket A2 — Completion Report

**Date**: 2026-07-28
**Engineer**: ptt-engineer (PTT Engineer mode)
**Status**: BUILD_PASS

---

## Summary

Ticket A2 wires the BE ALL button armed/wait FSM to the UI in both `TradeCopierPanel.cs`
and `TradeCopierWindow.cs`. When the user clicks BE ALL while in drawdown, the button
turns amber ("BE ALL Armed") and shows the armed state. When all pending slots fire or
the user cancels, it resets to purple ("BE ALL").

---

## Changes Made

### TradeCopierPanel.cs

**1. Promoted `BeState` enum to `internal`** (line 327)
- Changed from `private enum BeState` to `internal enum BeState`
- Required so `TradeCopierWindow` (separate class, same namespace) can reference it
- Values: `Idle`, `Armed` (unchanged)

**2. Added `_globalBeState` field** (line 218, after `BrushPurple`)
```csharp
private BeState _globalBeState = BeState.Idle;
```

**3. Replaced `OnGlobalBeClick` body** (line 942–961)
- Old: called `GlobalBe.Execute(...)` + green flash timer
- New: switch FSM — Idle→Execute+Armed (if pending slots exist); Armed→DisarmAll+Idle
- CYC=4

**4. Updated `OnPendingBeFiredDispatch`** (lines 769–778)
- Inside `Dispatcher.InvokeAsync` lambda, after `OnBeConnected(...)`, added auto-reset:
  checks `_globalBeState == Armed && IsPendingSlotsEmpty()`, then resets to Idle + UpdateBeAllVisuals

**5. Added `UpdateBeAllVisuals(BeState state)` method** (lines 784–788)
- Purple (`BrushPurple`) for Idle, Amber (`BrushCaution`) for Armed
- Null-guards `_globalBeBtn2` before setting Background
- CYC=2

**6. Updated `Detach()`** (lines 504–508)
- Added `Account.All` loop calling `DisarmPendingBe(acc)` for global cleanup
- Added `_globalBeState = BeState.Idle` reset

**Exact field names found in TradeCopierPanel.cs:**
- BE ALL button field: `_globalBeBtn2` (line 210)
- Purple brush: `BrushPurple` = `MakeBrush(168, 85, 247)` (line 214)
- Caution/amber brush: `BrushCaution` — pre-existing from B38/B39 (line 246)
- Per-account BE state: `_beState` (line 200) — unchanged
- Global BE state: `_globalBeState` (line 218) — NEW

---

### TradeCopierWindow.cs

**1. Added `_windowGlobalBeState` field** (after `_windowGlobalBeBtn`, line 75+)
```csharp
private TradeCopierPanel.BeState _windowGlobalBeState = TradeCopierPanel.BeState.Idle;
```
References `TradeCopierPanel.BeState` directly (now `internal`)

**2. Subscribed `PendingBeFired` in `OnLoaded`** (line 124+)
```csharp
_engine.PendingBeFired += OnWindowPendingBeFiredDispatch;
```

**3. Updated `OnWindowClosed`** (lines 132–143)
- Unsubscribed `_engine.PendingBeFired -= OnWindowPendingBeFiredDispatch`
- Added `Account.All` loop calling `CopyEngine.Instance.DisarmPendingBe(acc)`
- Added `_windowGlobalBeState = TradeCopierPanel.BeState.Idle` reset

**4. Replaced `OnWindowGlobalBeClick`** (lines 870+)
- Old: green flash timer (B39 behaviour)
- New: switch FSM mirroring Panel — Idle→Execute+Armed; Armed→DisarmAll+Idle
- CYC=4

**5. Added `OnWindowPendingBeFiredDispatch` method**
- Marshals `PendingBeFired` event to UI thread via `Dispatcher.InvokeAsync`
- Checks `_windowGlobalBeState == Armed && IsPendingSlotsEmpty()` then resets
- CYC=2

**6. Added `UpdateWindowBeAllVisuals(TradeCopierPanel.BeState state)` method**
- `WBrushPurple` for Idle, `WBrushCaution` for Armed
- Null-guards `_windowGlobalBeBtn` before setting Background
- CYC=2

**Exact field names found in TradeCopierWindow.cs:**
- BE ALL button field: `_windowGlobalBeBtn` (line 73)
- Purple brush: `WBrushPurple` = `MakeWinBrush(168, 85, 247)` (line 69)
- Caution/amber brush: `WBrushCaution` = `MakeWinBrush(245, 158, 11)` (line 65) — pre-existing
- Teardown handlers: `OnWindowClosed` (line 132) and `OnClosed` (line 138) — PendingBeFired unsubscribed in `OnWindowClosed`

---

## BeState Enum Location

- **File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`
- **Line**: 327
- **Access modifier**: `internal` (promoted from `private` in this ticket)
- **Values**: `Idle`, `Armed`
- **Namespace**: `NinjaTrader.NinjaScript.AddOns.PropTraderTools`

---

## DisarmPendingBe Signature Used

```csharp
internal void DisarmPendingBe(Account leader)
```
Single `Account` parameter (confirmed from CopyEngine.cs line 1975).

---

## 7-Scan Results

| Scan | Command | Result |
|------|---------|--------|
| SCAN-01 | `Select-String ... -Pattern "lock\("` | ✅ 0 new — all hits are JS-021 compliance comments |
| SCAN-02 | `Select-String ... -Pattern "async void"` | ✅ 0 new — all hits are JS-033 compliance comments |
| SCAN-03 | `Select-String ... -Pattern "return null;"` | ✅ 0 new — pre-existing only (CopyEngine, TradeCopierAddOn, Panel guard methods) |
| SCAN-04 | `Select-String ... -Pattern "throw new"` | ✅ 0 new — 1 pre-existing (AccountDisplayConverter.ConvertBack one-way marker) |
| SCAN-05 | `complexity_audit.py` | ✅ 0 violations — new methods: OnGlobalBeClick CYC=4, UpdateBeAllVisuals CYC=2, OnPendingBeFiredDispatch CYC=2, Window mirrors all ≤4 |
| SCAN-06 | `[Fact]` count in CopyEngineTests.cs | ✅ 202 [Fact] tests (T2 is UI-only wiring; A3 adds new tests) |
| SCAN-07 | `verify_links.ps1` | ✅ OK=12, DESYNC=0, MISSING=0 |

---

## Hard-Link Sync

```
OK       : CopyEngine.cs          (hard-linked)
OK       : TradeCopierAddOn.cs    (hard-linked)
OK       : TradeCopierPanel.cs    (hard-linked)
OK       : TradeCopierWindow.cs   (hard-linked)
OK       : Core\PttContracts.cs   (hard-linked)
OK       : Features\*.cs          (hard-linked × 6)
DESYNC   : 0
MISSING  : 0
```

---

## Jane Street DNA Compliance

- JS-021: No `lock()` in any new code ✅
- JS-033: No `async void` in any new code ✅
- JS-001: No `throw` in new code ✅
- JS-008: No new brushes needed — reused pre-existing `BrushCaution`/`WBrushCaution` ✅
- JS-023: All UI updates via `Dispatcher.InvokeAsync(...)` ✅
- NT8: `TradeCopierWindow` class has no `sealed` keyword ✅
- NT8: `Account.All` access only inside `OnLoaded` (already subscribed), `OnWindowClosed`, and `Detach()` — not in constructors or `OnInitialize` ✅
