# B190 Ticket 2 -- Completion Report

**Epic**: PTT-COPIER B190
**Ticket**: T2 -- BuildInstrRow() spinner layout fix
**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-07-11
**Gate**: TICKET_REVIEW_PASS (04-ticket-review.md)

---

## What Was Implemented

### 1. `BuildInstrRow()` -- body replaced in [`src/PropTraderTools/TradeCopierPanel.cs`](../../../src/PropTraderTools/TradeCopierPanel.cs)

Old body (B189-T4): used plain `Button` for `-`/`+` (no hold-to-scroll, wrong dock direction).

New body (B190-T2): delegates entirely to `BuildSpinnerCluster()`. CYC = 1.

```csharp
private void BuildInstrRow()
{
    _instrRowPanel = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };
    _instrRowPanel.Children.Add(
        BuildSpinnerCluster("Quick2t", _quickT1, OnQuickUp, OnQuickDown,
            OnInstr2tClick, ref _instr2tBtn));
    _instrRowPanel.Children.Add(
        BuildSpinnerCluster("QAll2t", CopyEngine.Instance.GlobalQuickAllT1,
            OnQuickAllUp, OnQuickAllDown,
            OnInstrQAll2tClick, ref _instrQAll2tBtn));
}
```

### 2. `BuildSpinnerCluster()` -- new private method added immediately after `BuildInstrRow()`

- Returns a `DockPanel` (never null -- JS-002 PASS).
- `RepeatButton` Delay=400, Interval=100: hold-to-scroll auto-fire restored (AC7).
- `DockPanel.SetDock(arrows, Dock.Right)` then `cluster.Children.Add(arrows)` before btn: arrows grid docked Right, btn fills remaining space via `LastChildFill=true` (AC6).
- `ref Button btnField`: assigns `_instr2tBtn` / `_instrQAll2tBtn` from inside the helper.
- CYC = 1 (sequential construction, no branches).
- ASCII-only: `"^"` = 0x5E, `"v"` = 0x76, `"Quick2t"`, `"QAll2t"`, `"NTButtonStyle"`. No FontFamily. No hex color literals.

### 3. Tests appended to [`src/PropTraderTools/Tests/B190Tests.cs`](../../../src/PropTraderTools/Tests/B190Tests.cs)

Two `[Fact]` tests added (T_B190_T2_01, T_B190_T2_02):

- **T_B190_T2_01_BuildSpinnerCluster_ReturnsDockPanel**: Uses reflection to confirm `BuildSpinnerCluster` exists as a private instance method on `TradeCopierPanel` and that its return type is `DockPanel`. No NT8 runtime required.
- **T_B190_T2_02_BuildSpinnerCluster_GridDockedRight**: Uses reflection to confirm 6 parameters with correct types (string label, int ticks, ..., ref Button btnField). Confirms `parameters[5].ParameterType.IsByRef == true` (the `ref Button` AC6 layout contract). No NT8 runtime required.

---

## 7-Scan Results

| Scan | Command | Result |
|------|---------|--------|
| SCAN-01 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | **0 errors, 0 warnings** -- PASS |
| SCAN-02 | `Select-String -Pattern "lock\("` scoped to new/modified lines (1180-1250) | **0 matches** -- PASS |
| SCAN-03 | `Select-String -Pattern "async void"` scoped to new/modified lines | **0 matches** -- PASS |
| SCAN-04 | `Select-String -Pattern "return null"` scoped to BuildInstrRow + BuildSpinnerCluster | **0 matches** -- PASS. `BuildSpinnerCluster` always returns `cluster` (DockPanel). |
| SCAN-05 | CYC manual check | `BuildInstrRow` CYC=1 (2 sequential calls, no branches). `BuildSpinnerCluster` CYC=1 (sequential construction, no branches). Both <= 8. -- PASS |
| SCAN-06 | Non-ASCII check on new lines | **0 non-ASCII characters**. `"^"`=0x5E, `"v"`=0x76, all string literals ASCII. -- PASS |
| SCAN-07 | `powershell -File .\scripts\ptt-sync.ps1` | **19 linked, 0 errors** -- PASS |

All 7 scans: **ZERO violations**.

---

## DNA Constraints Verified

| Rule | Status |
|------|--------|
| JS-021: 0 new `lock()` | PASS |
| JS-002: `BuildSpinnerCluster` never null (returns `cluster`) | PASS |
| JS-066: `BuildInstrRow` CYC=1, `BuildSpinnerCluster` CYC=1. Both <= 8. | PASS |
| JS-081: All string literals ASCII-only. No FontFamily. No hex color. | PASS |
| JS-023: Both methods UI-thread-only (called from BuildUI() from ctor) | PASS |
| JS-033: 0 `async void` | PASS |

---

## Spec AC Satisfaction

| AC | Description | Status |
|----|-------------|--------|
| AC6 | Arrows Grid docked Right in the spinner cluster | PASS -- `DockPanel.SetDock(arrows, Dock.Right)` before btn |
| AC7 | RepeatButton hold auto-fire (Delay=400, Interval=100) | PASS -- both `up` and `dn` RepeatButtons have Delay=400, Interval=100 |
| AC8 | No `+`/`-` plain Button code remaining in BuildInstrRow | PASS -- entire old body replaced |

---

**Return: BUILD_PASS**

*Written by: ptt-engineer*
*Epic: B190*
*Ticket: T2*
*Phase: 4a (Implementation)*
