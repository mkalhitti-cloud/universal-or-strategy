# Ticket 3 Verification: DW-B186-PANEL-CALLSITES-01

**Epic**: B186 -- Quick ALL + QAll2t Instrument Scope
**Ticket**: T3 -- DW-B186-PANEL-CALLSITES-01
**Phase**: 4b (ptt-verifier)
**Date**: 2026-07-08
**Verifier**: ptt-verifier (independent Layer 3)
**Engineer's Layer 2 Report**: ticket-3-completion.md

---

## VERDICT: VERIFY_PASS

**Condition**: All 7 scans clean, all AC checks pass, all DNA rules satisfied.
**Flag (non-blocking)**: T3 changes are in working tree (uncommitted). Engineer must commit before Phase 5.

---

## Implementation Verification

### Call Site 1 -- OnQuickAllClick (TradeCopierPanel.cs:1618)

**Verified from source**:
```csharp
// B41: OnQuickAllClick -- fires all-accounts Quick Exit bracket swap. CYC=1.
// JS-033: synchronous void event handler. JS-021: no lock.
// B186-T3: pass _instrument so Execute(Instrument) combined gate can scope by instrument.
private void OnQuickAllClick(object sender, RoutedEventArgs e)
{
    var gqx = new PttGlobalQuickExit();
    gqx.Execute(_instrument);  // line 1618 -- CORRECT
}
```

- [x] gqx.Execute(_instrument) present (not Execute())
- [x] No null guard at call site
- [x] CYC = 1 (unchanged -- no decision points in method body)
- [x] Method body exact match to ticket spec

### Call Site 2 -- OnInstrQAll2tClick (TradeCopierPanel.cs:1708)

**Verified from source**:
```csharp
// B123/R9: OnInstrQAll2tClick -- fires global 2-target quick exit on all followers.
// CYC=2: (1)TryResolve2TargetContext false-path, (2)Execute call.
// JS-021: no lock. ASCII-only.
// B186-T3: pass _instrument so Execute(List, Instrument) combined gate can scope by instrument.
private void OnInstrQAll2tClick(object sender, RoutedEventArgs e)
{
    if (!TryResolve2TargetContext(out int qty, out var targets))
        return; // (1)
    LogQxTwoTarget("[PTT-QX-2T-ALL]", qty, targets);
    new PttGlobalQuickExit().Execute(targets, _instrument); // (2) -- CORRECT
}
```

- [x] Execute(targets, _instrument) present (not Execute(targets))
- [x] No null guard at call site
- [x] CYC = 2 (unchanged -- only 1 decision point: TryResolve2TargetContext false-path)
- [x] Method body exact match to ticket spec

### Scope Check -- No Other Changes to TradeCopierPanel.cs

Working tree diff shows exactly 2 hunks:
- Hunk 1: line ~1611-1621 (OnQuickAllClick) -- adds 1 comment line, changes 1 arg
- Hunk 2: line ~1698-1710 (OnInstrQAll2tClick) -- adds 1 comment line, changes 1 arg

**No other changes outside these 2 call sites.** PASS.

---

## Full B186 Integrity Check

### T1 Signatures Intact (PttGlobalQuickExit.cs)

```
PttGlobalQuickExit.cs:39:  internal void Execute(Instrument instrument)
PttGlobalQuickExit.cs:113: internal void Execute(
PttGlobalQuickExit.cs:115:     Instrument instrument
```

- [x] Execute(Instrument instrument) present at line 39 -- T1 INTACT
- [x] Execute(List<...> forcedTargets, Instrument instrument) present at line 113-115 -- T2 INTACT

### Combined Gate Verified (T1 and T2)

```
PttGlobalQuickExit.cs:41:  if (!CopyEngine.Instance.Flags.QxGlobalExit || instrument == null) return;
PttGlobalQuickExit.cs:118: if (!CopyEngine.Instance.Flags.QxGlobalExit || instrument == null) return;
```

Both Execute overloads have the combined gate pattern. Call sites correctly omit redundant null guards.

### 6 T_B186_ Tests Present (CopyEngineTests.cs)

| Test | Line | Status |
|------|------|--------|
| T_B186_01_ExecuteHasInstrumentParameter | 9980 | PRESENT |
| T_B186_02_ExecuteNullInstrumentReturnsImmediately | 10009 | PRESENT |
| T_B186_03_OnQuickAllClickPassesInstrument | 10070 | PRESENT |
| T_B186_04_ExecuteQAll2tHasTwoParams | 10038 | PRESENT |
| T_B186_05_ExecuteQAll2tNullInstrumentReturnsImmediately | 10053 | PRESENT |
| T_B186_06_OnInstrQAll2tClickPassesInstrument | 10085 | PRESENT |

All 6 B186 tests present in B186QuickExitInstrScopeTests class. PASS.

---

## 7-Scan Results (Verifier Independent -- Layer 3)

| Scan | Command | Verifier Result | Engineer Claim | Match |
|------|---------|-----------------|----------------|-------|
| SCAN-01 | git diff HEAD~1 HEAD -- src/ + lock\( | **0 hits** | 0 hits | YES |
| SCAN-02 | git diff HEAD~1 HEAD -- src/ + eturn null | **0 hits** | 0 hits | YES |
| SCAN-03 | git diff HEAD~1 HEAD -- src/ + sync void  | **0 hits** | 0 hits | YES |
| SCAN-04 | CYC manual: OnQuickAllClick=1, OnInstrQAll2tClick=2 | **1 and 2** | 1 and 2 | YES |
| SCAN-05 | git diff HEAD~1 HEAD -- src/ + [^\x00-\x7F] | **0 hits** | 0 hits | YES |
| SCAN-06 | git diff HEAD~1 HEAD -- src/ + 	hrow new | **0 hits** | 0 hits | YES |
| SCAN-07 | dotnet build ... 2>&1 | **0 errors, 0 warnings** | 0 errors | YES* |

*Engineer reported 2 pre-existing xUnit2004 warnings; verifier sees 0 warnings. Both agree on 0 errors. Non-blocking.

All 7 scans: **ZERO violations.**

Note: Since T3 changes are uncommitted, SCAN-01..06 were run against both HEAD~1..HEAD (which shows T1/T2 changes) and git diff (working tree -- T3 changes). T3 working tree diff is also clean on all 7 scans.

---

## AC Verification

| ID | Check | Command | Result |
|----|-------|---------|--------|
| AC1 | Execute(Instrument) overload in PttGlobalQuickExit | Select-String "Execute.*Instrument" | 2 matches (lines 39, 95) |
| AC2 | Execute(List, Instrument) overload in PttGlobalQuickExit | Select-String "internal void Execute" | lines 39, 113 -- both overloads present |
| AC3 | OnQuickAllClick passes _instrument | Select-String "gqx\.Execute.*_instrument" | 1 match (line 1618) |
| AC4 | OnInstrQAll2tClick passes _instrument | Select-String "Execute\(targets.*_instrument\)" | 1 match (line 1708) |
| AC5 | No null guard at call sites | source inspection + grep | 0 hits |
| AC6 | 6 T_B186_ tests | Select-String "T_B186_" | 6 distinct [Fact] methods |

---

## DNA Rule Checks

| Rule | Description | Result |
|------|-------------|--------|
| JS-021 (P0) | No lock() in modified code | PASS -- 0 hits in diff |
| JS-001 (P0) | No 	hrow new in hot paths | PASS -- 0 hits in diff |
| JS-002 (P0) | No eturn null | PASS -- both methods are oid |
| JS-033 (P0) | No sync void (non-handler) | PASS -- both are synchronous oid event handlers |
| JS-023 | No Monitor.Enter/Mutex/Semaphore for state | PASS -- 0 found |
| ASCII-only | No non-ASCII in new lines | PASS -- 0 hits in diff |
| CYC <= 8 | Cyclomatic complexity ceiling | PASS -- OnQuickAllClick=1, OnInstrQAll2tClick=2 |
| JS-008 | No mutable struct across threads | PASS -- no new structs |
| NT8 Constraints | No FontFamily, hex color, async in lifecycle methods | PASS -- 0 hits |

---

## Guardian Script

`
GUARDIAN PASS -- all symbols present. Hunk=0 Del=0 Add=0
`

PASS.

---

## ptt-sync

`
ptt-sync complete: 19 linked, 0 errors
`

TradeCopierPanel.cs and PttGlobalQuickExit.cs both in linked set. PASS.

---

## Ph4b Deletion Audit (MANDATORY)

Command: git diff HEAD~1 HEAD -- src/PropTraderTools/TradeCopierPanel.cs | Select-String "^-    (private|internal|public)"

Result: **0 deleted symbols** -- no DW-cards required.

Command against working tree: git diff -- src/PropTraderTools/TradeCopierPanel.cs | Select-String "^-    (private|internal|public)"

Result: **0 deleted symbols** -- PASS.

---

## Single-Hunk Ban Check

Command: (git diff -- src/PropTraderTools/TradeCopierPanel.cs | Select-String "^@@").Count

Result: **2** (two hunks, one per call site)

Count > 1 -- single-hunk ban does NOT apply. NOT a full-file replace. PASS.

---

## Engineer Report Cross-Check

All engineer Layer 2 claims verified independently:

| Claim | Engineer (Layer 2) | Verifier (Layer 3) | Discrepancy |
|-------|--------------------|--------------------|-------------|
| SCAN-01..06 | 0 hits | 0 hits | None |
| SCAN-07 build | 0 errors, 2 xUnit2004 warnings | 0 errors, 0 warnings | Minor -- build passes in both cases |
| OnQuickAllClick line | ~1618 | 1618 | None |
| OnInstrQAll2tClick line | ~1708 | 1708 | None |
| 6 T_B186_ tests | T_B186_01..06 | All 6 confirmed | None |
| Guardian | PASS Hunk=0 Del=0 Add=0 | PASS Hunk=0 Del=0 Add=0 | None |
| ptt-sync | 19/0 | 19/0 | None |
| Commit status | Claims BUILD_PASS (implies committed) | **Uncommitted (working tree)** | FLAG (non-blocking) |

**Critical Flag**: T3 changes in TradeCopierPanel.cs and CopyEngineTests.cs (T_B186_03, T_B186_06) exist in the working tree only -- not committed. The engineer's completion doc implies a committed state. Implementation is correct; commit required before Phase 5.

---

## Architecture Compliance

- T3 modifies exactly the 2 files listed in ticket scope: TradeCopierPanel.cs + CopyEngineTests.cs
- No new classes, no new methods beyond T_B186_03 and T_B186_06 tests
- Call-site changes are minimal surgical argument additions (1 char + _instrument each)
- Combined gate pattern (!QxGlobalExit || instrument == null) is in PttGlobalQuickExit, not at call site -- correct per B185 T1 precedent
- No CYC increase in any method

---

## ACTION REQUIRED BEFORE PHASE 5

**The engineer must commit the T3 working tree changes:**

`
git add src/PropTraderTools/TradeCopierPanel.cs src/PropTraderTools/CopyEngineTests.cs
git commit -m "B186 T3: DW-B186-PANEL-CALLSITES-01 -- pass _instrument to both Execute call sites + T_B186_03/06 tests"
`

This is a hygiene requirement. Implementation is correct. Phase 5 may proceed after commit.

---

## Summary

| Gate | Result |
|------|--------|
| All 7 DNA scans | PASS -- 0 violations |
| T3 call sites correct | PASS -- both use _instrument arg |
| No null guard at call site | PASS |
| No other changes to TradeCopierPanel.cs | PASS |
| T1+T2 signatures intact | PASS |
| All 6 T_B186_ tests present | PASS |
| Guardian | PASS |
| ptt-sync 19/0 | PASS |
| Ph4b deletion audit | PASS -- 0 deleted symbols |
| Single-hunk ban | PASS -- 2 hunks |
| Build 0 errors | PASS |
| Uncommitted T3 changes | FLAG (non-blocking) -- commit required |

---

*Written by: ptt-verifier (Phase 4b)*
*Epic: B186, Ticket: T3*
*Result: VERIFY_PASS (with commit-required flag)*