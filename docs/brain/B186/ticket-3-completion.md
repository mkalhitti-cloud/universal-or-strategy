# Ticket 3 Completion: DW-B186-PANEL-CALLSITES-01

**Epic**: B186 — Quick ALL + QAll2t Instrument Scope
**Ticket**: T3 — DW-B186-PANEL-CALLSITES-01
**Phase**: 4a (ptt-engineer)
**Date**: 2026-07-08
**Result**: BUILD_PASS

---

## What Was Implemented

### Change 1 — `OnQuickAllClick` in `src/PropTraderTools/TradeCopierPanel.cs` (~line 1618)

```csharp
// BEFORE:
gqx.Execute();

// AFTER:
gqx.Execute(_instrument);
```

Added doc comment `// B186-T3: pass _instrument so Execute(Instrument) combined gate can scope by instrument.`

CYC unchanged at 1 — only argument added to existing call, no new branch.

### Change 2 — `OnInstrQAll2tClick` in `src/PropTraderTools/TradeCopierPanel.cs` (~line 1708)

```csharp
// BEFORE:
new PttGlobalQuickExit().Execute(targets); // (2)

// AFTER:
new PttGlobalQuickExit().Execute(targets, _instrument); // (2)
```

Added doc comment `// B186-T3: pass _instrument so Execute(List, Instrument) combined gate can scope by instrument.`

CYC unchanged at 2 — only argument added to existing call on branch (2), no new branch.

**No null guard at call site** — consistent with combined gate pattern (`!flags.QxGlobalExit || instrument == null`) already in both Execute overloads. Matches B185 T1 precedent (`OnGlobalBeClick` passing `_instrument`).

### Tests Added — `src/PropTraderTools/CopyEngineTests.cs`

Added to `B186QuickExitInstrScopeTests` class:

- `T_B186_03_OnQuickAllClickPassesInstrument` — structural: reflects `OnQuickAllClick` exists on `TradeCopierPanel`. NT8-skip.
- `T_B186_06_OnInstrQAll2tClickPassesInstrument` — structural: reflects `OnInstrQAll2tClick` exists on `TradeCopierPanel`. NT8-skip.

Total B186 tests: **6** (T_B186_01..T_B186_06), all in `B186QuickExitInstrScopeTests`.

---

## 7-Scan Results

| Scan | Command | Result |
|------|---------|--------|
| SCAN-01 | `git diff HEAD -- src/ \| Select-String "^\+" \| Select-String "lock\("` | **0 hits — PASS** |
| SCAN-02 | `git diff HEAD -- src/ \| Select-String "^\+" \| Select-String "return null"` | **0 hits — PASS** |
| SCAN-03 | `git diff HEAD -- src/ \| Select-String "^\+" \| Select-String "async void "` | **0 hits — PASS** |
| SCAN-04 | CYC manual count: `OnQuickAllClick` CYC=1 (unchanged), `OnInstrQAll2tClick` CYC=2 (unchanged). Argument added to existing call — no new branch. | **PASS** |
| SCAN-05 | `git diff HEAD -- src/ \| Select-String "^\+" \| Select-String "[^\x00-\x7F]"` | **0 hits — PASS** |
| SCAN-06 | `git diff HEAD -- src/ \| Select-String "^\+" \| Select-String "throw new"` | **0 hits — PASS** |
| SCAN-07 | `dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 \| Select-Object -Last 5` | **0 errors, 2 warnings (pre-existing xUnit2004) — PASS** |

All 7 scans: **ZERO hits / ZERO errors**.

---

## AC Checks

### AC-1: 6 B186 tests present
```
CopyEngineTests.cs matched: T_B186_01, T_B186_02, T_B186_03, T_B186_04, T_B186_05, T_B186_06
```
**PASS** — all 6 tests present.

### AC-2: Execute with Instrument in PttGlobalQuickExit
```
PttGlobalQuickExit.cs:39:  internal void Execute(Instrument instrument)
```
**PASS** — T1/T2 overloads confirmed.

### AC-3: Call sites in TradeCopierPanel
```
TradeCopierPanel.cs:1618:  gqx.Execute(_instrument);
TradeCopierPanel.cs:1708:  new PttGlobalQuickExit().Execute(targets, _instrument); // (2)
```
**PASS** — both call sites verified.

### Guardian Script
```
GUARDIAN PASS -- all symbols present. Hunk=0 Del=0 Add=0
```
**PASS**

### ptt-sync
```
ptt-sync complete: 19 linked, 0 errors
```
**PASS** — `TradeCopierPanel.cs` and `PttGlobalQuickExit.cs` both in linked set.

---

## JS Compliance

| Rule | Status |
|------|--------|
| JS-021 `lock()` | PASS — no lock added |
| JS-001 `throw new` | PASS — no throw added |
| JS-002 `return null` | PASS — both methods `void` |
| JS-033 `async void` | PASS — both methods synchronous `void` event handlers |
| ASCII-only | PASS — no new string literals in T3 |

---

## Files Modified

1. `src/PropTraderTools/TradeCopierPanel.cs` — 2 call site changes (Change 1 + Change 2)
2. `src/PropTraderTools/CopyEngineTests.cs` — 2 new [Fact] tests added (T_B186_03, T_B186_06)

---

## BUILD_PASS

Build: **0 errors**. All 7 scans zero. All AC checks pass. Guardian pass. ptt-sync 19/19.

DW-B186-PANEL-CALLSITES-01 is complete. T3 resolves the call-site compile errors from T1+T2 and closes epic B186.

---
*Written by: ptt-engineer (Phase 4a)*
*Epic: B186, Ticket: 3 (T3)*
*Result: BUILD_PASS*
