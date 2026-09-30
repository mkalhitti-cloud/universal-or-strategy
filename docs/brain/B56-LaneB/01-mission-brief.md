# PTT-COPIER B56-LaneB — Mission Brief
# Written by: ptt-orchestrator
# Date: 2026-08-09
# Block: B56-LaneB
# Defects: DW-B56-02 (P1) + DW-B56-03 (P1)

---

## 1. Objective

Fix two P1 bugs discovered during Director live testing (2026-08-09):

- **DW-B56-02**: Rule rows not rebuilt from engine state after F5/LoadRules.
  The PTT Window always shows the hardcoded initial "MES" row, ignoring saved rules.
- **DW-B56-03**: Clone mode missing from PTT Window Copy Mode ComboBox AND
  `CopyMode.Clone = 2` is missing from the engine enum in `CopyEngine.cs`.

---

## 2. Spec Reference

- `specs/002-trade-copier-spec.html` id="section-b56"
- DW-B56-02 (lines 23311–23360): Rules not showing — Option B (post-load rebuild) selected.
- DW-B56-03 (lines 23362–23408): Clone missing — add enum value + ComboBox item + handler.

---

## 3. Prerequisite State

- B55-LaneA: FINAL_PASS confirmed.
- B55-LaneB: FINAL_PASS confirmed (06-deferred-backlog.md written 2026-08-09).
  Test baseline: **279 total (255 pass + 24 pre-existing fail)** per B55-LaneB backlog.
- B56-LaneA: Running in parallel — only touches `DispatchCopy`/`OnOrderUpdate` method
  bodies. No structural conflict with LaneB changes.

---

## 4. Files In Scope

| File | Changes |
|------|---------|
| `src/PropTraderTools/CopyEngine.cs` | CHANGE 1: `Clone = 2` in enum; CHANGE 2: `GetRuleInstruments()` method |
| `src/PropTraderTools/TradeCopierWindow.cs` | CHANGE 3: "Clone" ComboBox item + handler; CHANGE 4: `RefreshRuleRows()` + call in `OnLoaded` |
| `src/PropTraderTools/Tests/B56Tests.cs` | NEW FILE: `T_B56B_01`, `T_B56B_02` |
| `src/PropTraderTools/PropTraderTools.csproj` | Add `<Compile Include="Tests\B56Tests.cs" />` |

**DO NOT MODIFY**: `TradeCopierPanel.cs`, `CopyEngineTests.cs`

---

## 5. Four Exact Changes

### CHANGE 1 — CopyMode enum (CopyEngine.cs line ~77)
```
BEFORE: internal enum CopyMode { Signal = 0, Mirror = 1 }
AFTER:  internal enum CopyMode { Signal = 0, Mirror = 1, Clone = 2 }
```
Pre-condition: Architect must grep all `*.cs` for `CopyMode.Clone` to confirm Clone
is NOT already defined in a separate location.

### CHANGE 2 — GetRuleInstruments() method (CopyEngine.cs after GetCopyMode ~line 299)
```csharp
// CYC=2. Returns distinct instrument names from _rules for UI refresh.
// Called by TradeCopierWindow.RefreshRuleRows() after LoadRules().
// JS-021: no lock — ConcurrentBag foreach is lock-free.
// JS-002: returns empty IEnumerable (not null) when _rules is empty.
internal IEnumerable<string> GetRuleInstruments()
{
    var seen = new HashSet<string>();
    foreach (var r in _rules)
        if (seen.Add(r.Instrument))
            yield return r.Instrument;
}
```

### CHANGE 3 — Clone ComboBox item + handler (TradeCopierWindow.cs)
- Add `modeCb.Items.Add("Clone");` after existing `modeCb.Items.Add("Mirror");`
- In `OnCopyModeComboChanged`: add case/branch for index 2 → `SetCopyMode(CopyMode.Clone)`
- Architect must read actual method implementation to match switch vs. if-chain style.

### CHANGE 4 — RefreshRuleRows() method + OnLoaded call (TradeCopierWindow.cs)
```csharp
// CYC=3: null guard + foreach instruments + Dispatcher.InvokeAsync lambda.
private void RefreshRuleRows()
{
    var instruments = CopyEngine.Instance.GetRuleInstruments().ToList();
    if (instruments.Count == 0) return;    // no saved rules — keep default MES row
    Dispatcher.InvokeAsync(() =>
    {
        _rulesPanel.Children.Clear();
        foreach (var instr in instruments)
            _rulesPanel.Children.Add(BuildRuleRow(instr));
    });
}
```
- Add call `RefreshRuleRows();` in `OnLoaded` immediately after `CopyEngine.Instance.LoadRules()`.

---

## 6. New Test File: src/PropTraderTools/Tests/B56Tests.cs

**T_B56B_01**: `GetRuleInstruments_ReturnsEmpty_WhenNoRules`
- Engine with no rules → `GetRuleInstruments()` returns empty enumerable.
- Assert: `result.ToList().Count == 0`. CYC=1.

**T_B56B_02**: `CopyModeEnum_HasCloneValue2`
- Assert: `(int)CopyMode.Clone == 2`
- Assert: `Enum.IsDefined(typeof(CopyMode), 2)`
- CYC=1. Documents and locks the enum contract.

Both tests: pure C#, no NT8 API, no WPF, no reflection. Same header/namespace as `B55Tests.cs`.

---

## 7. Invariants (ptt-verifier confirms all 9)

| # | Invariant |
|---|-----------|
| INV-1 | `CopyMode` enum has exactly 3 values: Signal=0, Mirror=1, Clone=2 |
| INV-2 | `GetRuleInstruments()` exists in `CopyEngine`, returns `IEnumerable<string>` |
| INV-3 | `GetRuleInstruments()` returns empty (not null) when `_rules` is empty |
| INV-4 | `TradeCopierWindow` modeCb has exactly 3 items: "Signal (default)", "Mirror", "Clone" |
| INV-5 | `OnCopyModeComboChanged` handles index 2 → `SetCopyMode(CopyMode.Clone)` |
| INV-6 | `RefreshRuleRows()` exists in `TradeCopierWindow` |
| INV-7 | `RefreshRuleRows()` called after `LoadRules()` in `OnLoaded` |
| INV-8 | `T_B56B_01` PASS, `T_B56B_02` PASS |
| INV-9 | No new `lock()`, no new `async void`, no new `return null` in changed methods |

---

## 8. JS Rules Summary

| Rule | Constraint |
|------|-----------|
| JS-021 | No `lock()`. `GetRuleInstruments` uses `ConcurrentBag` foreach (lock-free). `RefreshRuleRows` uses `Dispatcher.InvokeAsync` (WPF marshal, not a lock). |
| JS-002 | `GetRuleInstruments` returns `IEnumerable<string>` — never null. `RefreshRuleRows` is void. |
| JS-033 | No `async void`. `RefreshRuleRows` is `private void` (sync caller; `Dispatcher.InvokeAsync` lambda is inside, method itself is not async void). |
| JS-001 | No `throw new` in hot path. |
| CYC | `GetRuleInstruments` CYC=2, `RefreshRuleRows` CYC=3, tests CYC=1. All ≤ 8. |

---

## 9. NT8 Notes

- Read `docs/standards/NT8_COMPILER_RULES.md` before any `.cs` edit.
- NT8-001: no `init` setters.
- NT8-003: no `volatile double`.
- NT8-006: `LINQ .ToList()` used in `RefreshRuleRows` only (UI thread, not hot path — acceptable).
- `Dispatcher.InvokeAsync` is valid in `TradeCopierWindow` (WPF Window class). NT8-042 does NOT apply.

---

## 10. 7-Scan Contract (ptt-verifier runs all independently)

| # | Scan | Pass Condition |
|---|------|---------------|
| SCAN-01 | `Select-String "lock("` in `src/` | 0 actual lock() calls |
| SCAN-02 | `Select-String "async void "` in `src/` | 0 async void declarations |
| SCAN-03 | `Select-String "return null"` in `src/` | 0 new instances |
| SCAN-04 | `Select-String "throw new "` in `src/` | 0 new instances |
| SCAN-05 | complexity_audit.py | `GetRuleInstruments` CYC≤8, `RefreshRuleRows` CYC≤8, B56Tests CYC≤8 |
| SCAN-06 | `dotnet build` | 0 errors |
| SCAN-07 | `dotnet test` | T_B56B_01 PASS, T_B56B_02 PASS; +2 delta vs baseline |

---

## 11. Hard-Link Sync

After SCAN-07 passes:
```
powershell -File scripts\verify_links.ps1 -Fix
```
Confirm: PASS (0 DESYNC).

---

## 12. Build Tag

`PTT-COPIER B56 | rules-refresh-clone-fix | {today-date}`

---

## 13. Pipeline Sequence

This is a 4-stage hard-stop pipeline:

| Stage | Role | Output |
|-------|------|--------|
| 1 | ptt-orchestrator | `01-mission-brief.md` (THIS FILE) |
| 2 | ptt-architect | `02-architecture-plan.md` |
| 3 | ptt-engineer | src edits + `ticket-1-completion.md` |
| 4 | ptt-verifier | `ticket-1-verification.md` + VERIFY_PASS |

---

*ptt-orchestrator | B56-LaneB | Stage 1 | 2026-08-09*
