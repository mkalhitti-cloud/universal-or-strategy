# PTT-COPIER-B21-LANE-C — Ticket 1 Completion Report

**Epic**: PTT-COPIER-B21-LANE-C
**Ticket**: T1 — Remove ATM Template Dead Code from TradeCopierPanel.cs
**Spec**: DW-ATM-DROPDOWN-01
**Phase**: 4a (Engineer Implementation)
**Engineer**: ptt-engineer
**Date**: 2026-07-14
**Status**: BUILD_PASS

---

## Gates Passed

### STEP 0 — Rules Catalog Gate: PASS

Read `docs/standards/jane-street/RULES_CATALOG.md` (UTF-8 clean).
Removal-only task: zero P0 violations introduced.

| Rule ID | Check | Result |
|---------|-------|--------|
| JS-021 | `lock()` banned | PASS — no lock() calls added; SCAN-06 confirmed 0 matches post-edit |
| JS-033 | `async void` banned (non-event-handler) | PASS — all removed methods were synchronous void; no async void introduced |
| JS-001 | No throw in hot paths | PASS — no new code written; no throw introduced |
| JS-002 | No `return null` for missing values | PASS — no new code written; no null return introduced |

### STEP NT8-0 — NT8 Compiler Gate: PASS

Read `docs/standards/NT8_COMPILER_RULES.md`.
No NT8 rules triggered by removal-only work.
Relevant rule: NT8-003 (no `volatile double`) — confirmed: the ATM block contained no volatile fields.

---

## What Was Implemented

### File Modified

```
c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs
```

No other file was modified.

### Before / After Line Count

| Metric | Value |
|--------|-------|
| Line count before | ~1507 |
| Line count after | 1429 |
| Lines removed | ~78 |

### Items Removed (All 9 from T1 Checklist)

| Item | Description | Lines Removed |
|------|-------------|---------------|
| 9 | Header comment block `// PTT-COPIER-B11-T2 --` (lines 49–57) | Lines 49–57 (9 lines) |
| 1+2 | Field declarations comment + `_atmTemplateCombo` + `_activeAtmTemplateName` (lines 159–161) | Lines 159–161 (3 lines) |
| 8 | Call site `LoadAtmTemplates()` in `OnLoaded()` (line 459) | Line 459 (1 line) |
| 7 | Comment `// B11 T2: ATM template row` + call `BuildAtmTemplateRow(_contentPanel)` in `BuildUI()` (lines 565–566) | Lines 565–566 (2 lines) |
| 6 | Method `GetAtmTemplatesDirectory()` with comment block (lines 1378–1387) | Lines 1378–1387 (10 lines) |
| 3 | Method `BuildAtmTemplateRow(StackPanel root)` with comment block (lines 1388–1415) | Lines 1388–1415 (28 lines) |
| 4 | Method `LoadAtmTemplates()` with comment block (lines 1416–1435) | Lines 1416–1435 (20 lines) |
| 5 | Method `OnAtmTemplateSelectionChanged(object, SelectionChangedEventArgs)` with comment block (lines 1436–1445) | Lines 1436–1445 (10 lines) |
| — | Stale comment in `BuildRiskAtrRow` header: `"(after BuildAtmTemplateRow)"` fragment | 1 line updated |

**Note on Item — stale comment**: A comment in `BuildRiskAtrRow` at line 1424 read
`"// Called from BuildUI() at end of _contentPanel (after BuildAtmTemplateRow)."`.
The `"(after BuildAtmTemplateRow)"` fragment was a stale reference to the now-deleted method.
It was removed from that comment line to satisfy SCAN-03 (0 `BuildAtmTemplateRow` references).
The comment itself is retained with the fragment removed. This change traces directly to
DW-ATM-DROPDOWN-01 (removing all evidence of the dead code). No scope creep.

### `using System.IO` — Intentionally Retained

Per the ticket scope constraint: `using System.IO` (line 112) is NOT removed.
The NT8 compiler accepts unused using directives without error (confirmed in SCAN-07).
Removal is explicitly deferred to a future block.

---

## Layer 2 — 7-Scan Report

All scans run against:
```
c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs
```

Build scan run against:
```
c:\WSGTA\universal-or-strategy\archive\v12-reference\Linting.csproj
```

### SCAN-01: `_atmTemplateCombo` absent

```
Command : Select-String -Path TradeCopierPanel.cs -Pattern "_atmTemplateCombo"
Result  : 0 matches
Status  : PASS
```

### SCAN-02: `_activeAtmTemplateName` absent

```
Command : Select-String -Path TradeCopierPanel.cs -Pattern "_activeAtmTemplateName"
Result  : 0 matches
Status  : PASS
```

### SCAN-03: `BuildAtmTemplateRow` absent

```
Command : Select-String -Path TradeCopierPanel.cs -Pattern "BuildAtmTemplateRow"
Result  : 0 matches
Status  : PASS
Note    : Required one additional edit — stale comment fragment in BuildRiskAtrRow header
          referenced BuildAtmTemplateRow. Fragment removed; SCAN-03 then returned 0.
```

### SCAN-04: `LoadAtmTemplates` absent

```
Command : Select-String -Path TradeCopierPanel.cs -Pattern "LoadAtmTemplates"
Result  : 0 matches
Status  : PASS
```

### SCAN-05: `OnAtmTemplateSelectionChanged` absent

```
Command : Select-String -Path TradeCopierPanel.cs -Pattern "OnAtmTemplateSelectionChanged"
Result  : 0 matches
Status  : PASS
```

### SCAN-06: No `lock(` in file (JS-021 gate)

```
Command : Select-String -Path TradeCopierPanel.cs -Pattern "lock\("
Result  : 0 matches
Status  : PASS
```

### SCAN-07: Build passes (NT8 gate)

```
Command : dotnet build archive\v12-reference\Linting.csproj
Result  : Build succeeded. 0 Warning(s). 0 Error(s).
Status  : PASS
```

**Note on PropTraderTools.csproj**: `dotnet build src\PropTraderTools\PropTraderTools.csproj`
produces 3 pre-existing errors unrelated to this ticket:
  - `AtrSizingEngine.cs`: NT8 NinjaTrader assembly not in .csproj reference set
    (NinjaTrader.NinjaScript.Indicators + Indicator type unavailable via standalone .csproj)
  - `CopyEngine.cs(634)`: nullable reference types require C# 8.0 (project targets 7.3)

These identical errors existed on `main` before T1 edits (confirmed via `git stash` + build).
Neither error appears in `TradeCopierPanel.cs` or any file modified by this ticket.
The authoritative build gate for PTT is `Linting.csproj` (confirmed green: 0 errors).

---

## Regression Guard

Existing `[Fact]` tests in `CopyEngineTests.cs` are not affected:
- No behavioral change to `CopyEngine` or any order path
- ATM template selection was unwired dead code — zero test coverage prior to this deletion
- `Linting.csproj` build covers test compilation: PASS (0 errors)

---

## Completion Checklist

- [x] Lines 49–57 (B11 T2 changelog header) deleted
- [x] Line 459 (`LoadAtmTemplates()` call in `OnLoaded`) deleted
- [x] Lines 565–566 (`// B11 T2: ATM template row` comment + `BuildAtmTemplateRow(_contentPanel)` call in `BuildUI`) deleted
- [x] Lines 159–161 (`// B11 T2` comment + 2 field declarations) deleted
- [x] Lines 1378–1387 (`GetAtmTemplatesDirectory` comment + method) deleted
- [x] Lines 1388–1415 (`BuildAtmTemplateRow` comment + method) deleted
- [x] Lines 1416–1435 (`LoadAtmTemplates` comment + method) deleted
- [x] Lines 1436–1445 (`OnAtmTemplateSelectionChanged` comment + method) deleted
- [x] SCAN-01 → 0 matches
- [x] SCAN-02 → 0 matches
- [x] SCAN-03 → 0 matches
- [x] SCAN-04 → 0 matches
- [x] SCAN-05 → 0 matches
- [x] SCAN-06 → 0 matches
- [x] SCAN-07 → 0 errors (Linting.csproj)
- [x] Existing `CopyEngineTests.cs` [Fact] tests unaffected (Linting.csproj: 0 errors)

---

## BUILD_PASS
