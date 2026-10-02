# B184 Architecture Plan
## ptt-architect Phase 1

**Date**: 2026-07-07
**Author**: ptt-architect
**Epic**: B184 — Restore CopyEngine.cs BE Event Infrastructure + Full ATR Removal
**Status**: PLAN_COMPLETE (cycle 2 — V-01 resolved)

---

## LANE-SPLIT GATE RESULT

| Question | Answer | Reasoning |
|----------|--------|-----------|
| Q1. Same method or within 50 lines? | **NO** | T1 adds symbols in the BE/pending-slots region of CopyEngine.cs. T2 removes symbols in ATR field/method regions (~lines 136, 640, 1324, 2746). The two regions are hundreds of lines apart and share no method boundary. |
| Q2. T2 design depends on T1's final design? | **NO** | T1 restores BE event infrastructure (BeState enum, 2 events, 3 methods). T2 removes ATR subsystem (fields, 5 methods, file deletions). Zero design dependency between the two symbol sets. |
| Q3. Each fix has standalone value if the other is blocked? | **YES** | T1 alone: resolves 4 P0 compile-blocking deferred items (DW-B183-06/-07/-08/-09). The build cannot reach zero errors without T1. T2 alone: Director-approved ATR removal with 6-file scope — independent Director value even if T1 were delayed. |
| Q4. Each fix has independent SIM/build verification path? | **YES** | T1: `dotnet build` exits 0 + `verify_critical_symbols.ps1` exits 0. T2: `dotnet build` exits 0 + grep confirms no ATR references + `verify_critical_symbols.ps1` exits 0. Fully orthogonal verification chains. |

**LANE-SPLIT GATE RESULT: LANES-APPROVED**

> **Execution override**: Despite LANES-APPROVED, T1 is executed FIRST. T1 resolves compile-blocking P0 defects; T2 cannot use a broken build as its baseline. Sequential execution enforced: T1 commit → T1 VERIFY_PASS → T2 commit → T2 VERIFY_PASS → Ph5.

---

## Deferred Backlog Items Closed by B184

| DW ID | Description | Priority | Status After B184 |
|-------|-------------|----------|-------------------|
| DW-B183-06 | PendingBeArmed event absent from CopyEngine.cs | P0 | CLOSED by T1 |
| DW-B183-07 | GlobalBeAllDisarmed event absent from CopyEngine.cs | P0 | CLOSED by T1 |
| DW-B183-08 | DisarmPendingBe method absent from CopyEngine.cs | P0 | CLOSED by T1 |
| DW-B183-09 | IsPendingSlotsEmpty + RaiseBeAllDisarmed absent from CopyEngine.cs | P0 | CLOSED by T1 |
| DW-B183-10 | DispatchCopy CYC=8 frozen | P1 | PARTIALLY LIFTED by T2 (ATR branch removed from ResolveBaseQty/DispatchCopy path) |

Items remaining open after B184: DW-B183-01, -02, -03, -04, -05 (all P1/P2 scope).

---

## Component List

### T1 Components (CopyEngine.cs additions)

| # | Symbol | Kind | Insertion region |
|---|--------|------|-----------------|
| 1 | `BeState` | `enum` | Near other inner enums in CopyEngine class |
| 2 | `PendingBeArmed` | `event` | Near other event declarations |
| 3 | `GlobalBeAllDisarmed` | `event` | Near other event declarations |
| 4 | `DisarmPendingBe(Account leader)` | `internal void` method | BE methods region |
| 5 | `IsPendingSlotsEmpty()` | `internal bool` method | BE methods region |
| 6 | `RaiseBeAllDisarmed()` | `internal void` method | BE methods region |

### T2 Components (multi-file removals)

**CopyEngine.cs** — 7 items removed:
- Fields: `_atrEnabled` (volatile bool), `_atrEngine` (volatile AtrSizingEngine)
- Methods: `SetAtrEngine(AtrSizingEngine, bool)`, `UpdateMaxRisk(double)`, `UpdateAtrFraction(double)`, `GetSuggestedQty(Instrument)`, `ResolveBaseQty(Instrument, int)`
- Call-site patch: Replace `ResolveBaseQty(instrument, signalQty)` in `DispatchCopy` with `signalQty`

**TradeCopierPanel.cs** — 6 fields + 10 methods + 4 call sites removed

**TradeCopierAddOn.cs** — 2 fields + 4 methods + 2 call sites removed

**FeatureFlags.cs** — 1 record parameter + 4 factory site args removed (arity 7 → 6)

**AtrSizingEngine.cs** — FILE DELETED

**AtrCalc.cs** — FILE DELETED

---

## T1 Fix Strategy — CopyEngine.cs BE Event Infrastructure

### Fix Source
All 6 symbols are sourced verbatim from:
```powershell
git show f01b5155:src/PropTraderTools/CopyEngine.cs
```

The engineer must NOT cherry-pick the full file. Extract each symbol individually.

### Step-by-Step Engineer Instructions

**Step 1 — Confirm symbol locations in f01b5155:**
```powershell
git show f01b5155:src/PropTraderTools/CopyEngine.cs | Select-String -Pattern "enum BeState|PendingBeArmed|GlobalBeAllDisarmed|DisarmPendingBe|IsPendingSlotsEmpty|RaiseBeAllDisarmed" -Context 2
```

**Step 2 — Verify BeState enum canonical home:**
```powershell
# Check if BeState is defined in TradeCopierPanel.cs (would conflict if so)
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "enum BeState"
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "enum BeState"
# If Panel already declares BeState → DO NOT add to CopyEngine. Escalate to Director.
# If neither declares it → restore from f01b5155 into CopyEngine.cs.
```

**Step 3 — Verify excluded symbols are absent from f01b5155 context:**
```powershell
# The 5 B180-2 approved removals must NOT be re-introduced:
git show f01b5155:src/PropTraderTools/CopyEngine.cs | Select-String "ResetBeStateOnFlat|ExecuteBeIdle|ExecuteBeArmed|UpdateBeVisuals|OnBeConnected"
# If any of these appear ADJACENT to the 6 symbols being restored, be surgical.
```

**Step 4 — Insert symbols at structurally correct locations:**

The 6 symbols must be inserted at their original structural positions within CopyEngine.cs, NOT all at one location. This is mandatory to produce multiple diff hunks (Single-Hunk Ban compliance).

Expected structural placement in CopyEngine.cs:
- `BeState` enum → insert near other inner class/enum definitions
- `PendingBeArmed` event + `GlobalBeAllDisarmed` event → insert adjacent to other event declarations in the class
- `DisarmPendingBe`, `IsPendingSlotsEmpty`, `RaiseBeAllDisarmed` methods → insert in the BE-related method cluster

**Step 5 — Hunk count verification (Single-Hunk Ban):**
```powershell
(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count
# Must NOT equal 1. If = 1, the insertion was squashed into one block.
# Resolution: verify symbols are inserted at their original structural locations, not all at one spot.
```

**Step 6 — Commit Size Gate (pre-commit, MANDATORY):**
```powershell
(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^[+-]").Count
# If result > 300: STOP. Do NOT commit.
# Escalate to Director for explicit approval before proceeding.
# T1 scope estimate is ~30-80 lines added — far below 300. If this check returns > 300,
# the edit was broader than intended and must be reviewed before commit.
```

### Method Signature Specifications (from f01b5155)

The engineer reads these from the commit. Expected signatures based on Panel usage:

```csharp
// BeState enum — referenced as BeState.Idle and BeState.Armed in Panel
internal enum BeState { Idle, Armed, Connected }

// PendingBeArmed event — subscribed as: _engine.PendingBeArmed += OnPendingBeArmedDispatch
// OnPendingBeArmedDispatch signature: private void OnPendingBeArmedDispatch(string instr, string accountName)
internal event Action<string, string> PendingBeArmed;

// GlobalBeAllDisarmed event — subscribed as: _engine.GlobalBeAllDisarmed += OnGlobalBeAllDisarmed
// OnGlobalBeAllDisarmed signature: private void OnGlobalBeAllDisarmed()
internal event Action GlobalBeAllDisarmed;

// DisarmPendingBe — called as: CopyEngine.Instance.DisarmPendingBe(_leaderAccount)
// _leaderAccount is NinjaTrader.Cbi.Account
internal void DisarmPendingBe(NinjaTrader.Cbi.Account leader);

// IsPendingSlotsEmpty — called as: CopyEngine.Instance.IsPendingSlotsEmpty()
internal bool IsPendingSlotsEmpty();

// RaiseBeAllDisarmed — called as: CopyEngine.Instance.RaiseBeAllDisarmed()
internal void RaiseBeAllDisarmed();
```

> NOTE: These are inferred from Panel call sites. The AUTHORITATIVE signatures come from `git show f01b5155:src/PropTraderTools/CopyEngine.cs`. If any signature differs, the f01b5155 version is the source of truth.

### T1 JS Rule Constraints

| Rule | Constraint | Check |
|------|-----------|-------|
| JS-021 | No `lock()` in restored methods | Grep restored diff for `lock(` |
| JS-001 | No `throw` in restored methods | DisarmPendingBe must null-guard, not throw |
| JS-002 | No `return null` | Methods return void or bool — N/A |
| JS-033 | No `async void` except event handlers | Methods are not async |
| JS-066 | CYC <= 8 per method | DisarmPendingBe likely CYC=2-4; IsPendingSlotsEmpty CYC=1; RaiseBeAllDisarmed CYC=1 |
| ASCII | ASCII-only identifiers | All identifiers are ASCII ✓ |

### T1 Acceptance Criteria
1. `dotnet build` exits 0 errors (172 CS0436 NT8 artifact warnings are pre-existing — OK).
2. `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "enum BeState"` returns a match.
3. `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "PendingBeArmed"` returns a match.
4. `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "GlobalBeAllDisarmed"` returns a match.
5. `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "DisarmPendingBe"` returns a match.
6. `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "IsPendingSlotsEmpty"` returns a match.
7. `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "RaiseBeAllDisarmed"` returns a match.
8. None of the 5 B180-2 excluded symbols introduced: `Select-String "ResetBeStateOnFlat|ExecuteBeIdle|ExecuteBeArmed|UpdateBeVisuals|OnBeConnected"` returns 0 matches on the diff.
9. `powershell -File .\scripts\verify_critical_symbols.ps1` exits 0 (16 B183 symbols still present).
10. `powershell -File .\scripts\ptt-sync.ps1` runs clean.
11. Hunk count: `(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count -ne 1`.
12. **Commit Size Gate**: `(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^[+-]").Count` — if result > 300, **STOP. Do NOT commit. Obtain Director explicit approval first.**

---

## T2 Removal Strategy — Full ATR Subsystem Deletion

### Execution Order Within T2 (file edit sequence)
1. **FeatureFlags.cs** — establish correct record arity first (7 → 6 params)
2. **CopyEngine.cs** — remove ATR fields, methods, call-site patch
3. **TradeCopierPanel.cs** — remove ATR fields, methods, call sites
4. **TradeCopierAddOn.cs** — remove ATR fields, methods, call sites
5. **Delete AtrSizingEngine.cs**
6. **Delete AtrCalc.cs**
7. **Build** — verify 0 errors
8. **Grep confirmation** — verify 0 residual ATR references

---

### File 1: FeatureFlags.cs

**Current state (line count: 39)**

Remove `bool AtrSizing` parameter (line 13) from record declaration.
Update all 4 factory methods to 6-arg positional tuples:

```csharp
// BEFORE:
internal sealed record FeatureFlags(
    bool MultiRule,
    bool TrimFlatten,
    bool BreakEven,
    bool AtrSizing,      // <-- REMOVE
    bool ClickTrader,
    bool MirrorMode,
    bool QxGlobalExit
)

// AFTER:
internal sealed record FeatureFlags(
    bool MultiRule,
    bool TrimFlatten,
    bool BreakEven,
    bool ClickTrader,
    bool MirrorMode,
    bool QxGlobalExit
)
```

Factory method arity changes (all 4 must be updated atomically):

| Method | Before (7 args) | After (6 args) |
|--------|-----------------|----------------|
| `Starter()` | `new(false, false, false, false, false, false, false)` | `new(false, false, false, false, false, false)` |
| `Pro()` | `new(true, true, true, false, false, false, false)` | `new(true, true, true, false, false, false)` |
| `Elite()` | `new(true, true, true, true, true, true, true)` | `new(true, true, true, true, true, true)` |
| `FromFeatureList()` | 7 named args including `AtrSizing: ...` | 6 named args; remove `AtrSizing` line |

**Preserve**: `IsExternalInit` shim namespace block (lines 1-5) — required for NT8 Roslyn record support. Do NOT touch.

---

### File 2: CopyEngine.cs

**Pre-check before editing:**
```powershell
# Confirm GetSuggestedQty is EXCLUSIVELY called from ATR path (not by any other code)
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "GetSuggestedQty"
# Expected: only line ~1325 (definition) and ~1330+2753 (two call sites, both ATR-gated)
```

**Removals (7 items + 1 call-site patch):**

| Lines (approx) | Symbol | Action |
|----------------|--------|--------|
| ~136 | `private volatile bool _atrEnabled = false;` | DELETE line |
| ~137 | `private volatile AtrSizingEngine _atrEngine = null;` | DELETE line |
| ~640-648 | `internal void SetAtrEngine(AtrSizingEngine engine, bool enabled)` | DELETE method + comment block |
| ~649-655 | `internal void UpdateMaxRisk(double maxRiskDollars)` | DELETE method + comment |
| ~657-663 | `internal void UpdateAtrFraction(double fraction)` | DELETE method + comment |
| ~1324-1331 | `internal int GetSuggestedQty(NinjaTrader.Cbi.Instrument instrument)` | DELETE method + comment |
| ~2746-2755 | `private int ResolveBaseQty(NinjaTrader.Cbi.Instrument instrument, int signalQty)` | DELETE method + comment |
| DispatchCopy call site | `ResolveBaseQty(instrument, signalQty)` | REPLACE with `signalQty` |

**After edit — hunk count verification:**
```powershell
(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count
# Must be >= 4 (fields hunk + SetAtrEngine/UpdateMaxRisk/UpdateAtrFraction hunk + GetSuggestedQty hunk + ResolveBaseQty hunk)
# Must NOT equal 1 (Single-Hunk Ban)
```

**CYC impact**: DW-B183-10 DispatchCopy frozen at CYC=8 is LIFTED after T2. The `ResolveBaseQty` call removal reduces DispatchCopy's effective branch count. Record in ticket: "DispatchCopy CYC=8 freeze lifted by ATR removal."

---

### File 3: TradeCopierPanel.cs

**Fields to remove (6):**

| Line (approx) | Field |
|---------------|-------|
| ~274 | `private UniformGrid _atrRow = null;` |
| ~295 | `private double _maxRiskDollars = 200.0;` |
| ~296 | `private double _atrFraction = 0.75;` |
| ~297 | `private TextBox _riskDollarsBox;` |
| ~298 | `private TextBox _atrFractionBox;` |
| ~301 | `private TextBlock _atrDisplayLabel;` |

**Methods to remove (10):**

| Method | Approximate lines |
|--------|-------------------|
| `BuildRiskAtrRow(StackPanel root)` | ~2762-2791 |
| `BuildAtrDisplayRow()` | ~2837-2851 |
| `OnRiskUp(object, RoutedEventArgs)` | ~2864-2870 |
| `OnRiskDown(object, RoutedEventArgs)` | ~2873-2879 |
| `OnRiskTextLostFocus(object, RoutedEventArgs)` | ~2891-2899 |
| `OnAtrFractionUp(object, RoutedEventArgs)` | ~2902-2908 |
| `OnAtrFractionDown(object, RoutedEventArgs)` | ~2911-2917 |
| `OnAtrFractionTextLostFocus(object, RoutedEventArgs)` | ~2920-2928 |
| `NotifyRiskChanged()` | ~2931-2937 |
| `NotifyAtrFractionChanged()` | ~2939-2945 |

**Call sites to remove (4):**

| Location | Line (approx) | Code to remove |
|----------|---------------|----------------|
| `BuildUI()` | ~866 | `BuildRiskAtrRow(_contentPanel);` and its comment |
| `OnLoaded()` | ~766 | `NotifyRiskChanged();` |
| `OnLoaded()` | ~767 | `NotifyAtrFractionChanged();` |
| `ApplyRowVisibilityFlags()` | ~2975-2978 | `if (_atrRow != null) _atrRow.Visibility = f.AtrSizing ...` block (3 lines) |

**Do NOT touch**: Lines referencing `BeState`, `PendingBeArmed`, `GlobalBeAllDisarmed`, `DisarmPendingBe`, `IsPendingSlotsEmpty`, `RaiseBeAllDisarmed` — these are the BE event infrastructure from T1.

---

### File 4: TradeCopierAddOn.cs

**Fields to remove (2):**

| Line (approx) | Field |
|---------------|-------|
| ~44-45 | `private static readonly ConcurrentDictionary<Chart, AtrSizingEngine> _atrEngines = new ConcurrentDictionary<Chart, AtrSizingEngine>();` |
| ~66 | `private DispatcherTimer _atrPollTimer = null;` |

**Methods to remove (4):**

| Method | Approximate lines |
|--------|-------------------|
| `StartAtrEngine(Chart chart, NinjaTrader.Cbi.Instrument instr)` | ~219-257 |
| `StopAtrEngine(Chart chart)` | ~261-273 |
| `UpdateAtrOverlay(string atrDisplay)` | ~279-288 |
| `OnAtrUpdated(string display)` | ~292-294 |

**Call sites to remove (2):**

| Location | Line (approx) | Code to remove |
|----------|---------------|----------------|
| Chart close handler | ~109 | `StopAtrEngine(chart);` |
| `DoInject` method | ~495 | `StartAtrEngine(chart, instr);` |

**Pre-check before removing UpdateAtrOverlay:**
```powershell
# Confirm no other callers of UpdateAtrOverlay besides OnAtrUpdated
Select-String -Path src/PropTraderTools/ -Pattern "UpdateAtrOverlay" -Recurse
# If any file other than TradeCopierAddOn.cs references it, escalate to Director.
```

---

### File 5 & 6: Delete AtrSizingEngine.cs and AtrCalc.cs

**Pre-check before deletion:**
```powershell
# Confirm AtrCalc has zero callers outside AtrSizingEngine.cs
Select-String -Path src/PropTraderTools/ -Pattern "AtrCalc" -Recurse -Include "*.cs" | Where-Object { $_.Filename -notlike "*AtrSizingEngine*" }
# Must return 0 matches.

# Confirm AtrSizingEngine has zero callers outside files already edited
Select-String -Path src/PropTraderTools/ -Pattern "AtrSizingEngine" -Recurse -Include "*.cs" | Where-Object { $_.Filename -notlike "*AtrSizingEngine*" -and $_.Filename -notlike "*AtrCalc*" }
# Must return 0 matches after the above edits are applied.
```

**Deletion commands:**
```powershell
Remove-Item src/PropTraderTools/AtrSizingEngine.cs
Remove-Item src/PropTraderTools/AtrCalc.cs
```

---

### T2 Post-Edit Grep Confirmation
```powershell
# All of these must return 0 matches:
Select-String -Path src/ -Pattern "AtrSizingEngine|AtrCalc|_riskDollarsBox|_atrFractionBox|BuildRiskAtrRow|_atrRow|_atrDisplayLabel|UpdateMaxRisk|SetAtrEngine|AtrSizing|_atrEngine|_atrEngines|_atrPollTimer|StartAtrEngine|StopAtrEngine|UpdateAtrOverlay|OnAtrUpdated|NotifyRiskChanged|NotifyAtrFractionChanged" -Recurse -Include "*.cs"
```

### T2 JS Rule Constraints

| Rule | Constraint | Check |
|------|-----------|-------|
| JS-021 | No lock() introduced | Not applicable (removal-only, no new code) |
| JS-066 | DispatchCopy CYC must still be <= 8 after ResolveBaseQty removal | CYC can only go DOWN from removal — verified OK |
| ASCII | No new string literals introduced | Removal-only — N/A |
| Record arity | FeatureFlags 6-param record compiles at all 4 factory sites | Verify atomically |

### T2 Acceptance Criteria
1. `dotnet build` exits 0 errors.
2. Post-edit grep (above) returns 0 matches.
3. `AtrSizingEngine.cs` absent: `Test-Path src/PropTraderTools/AtrSizingEngine.cs` returns False.
4. `AtrCalc.cs` absent: `Test-Path src/PropTraderTools/AtrCalc.cs` returns False.
5. `FeatureFlags.cs` compiles — record arity 6 matches all factory sites.
6. `powershell -File .\scripts\verify_critical_symbols.ps1` exits 0 (16 B183 symbols still present).
7. `powershell -File .\scripts\ptt-sync.ps1` runs clean.
8. `(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count -ne 1` (Single-Hunk Ban).

---

## Data Flow Summary

### T1 — Restored BE Event Signal Path
```
CopyEngine: BE slot armed
  → fires PendingBeArmed(instr, accountName)
  → Panel.OnPendingBeArmedDispatch
  → Dispatcher.InvokeAsync
  → IsPendingSlotsEmpty() == false
  → UpdateBeAllVisuals(BeState.Armed)

Position goes flat:
  Panel.DisarmBeAllOnFlat
  → IsPendingSlotsEmpty() == false
  → DisarmPendingBe(_leaderAccount)    // CopyEngine: update pending slot state
  → RaiseBeAllDisarmed()               // CopyEngine: fires GlobalBeAllDisarmed
  → Panel.OnGlobalBeAllDisarmed
  → Dispatcher.InvokeAsync
  → UpdateBeAllVisuals(BeState.Idle)
```

### T2 — ATR Path After Removal
```
DispatchCopy:
  BEFORE: baseQty = ResolveBaseQty(instrument, signalQty)
           └─ if _atrEnabled → GetSuggestedQty(instrument)
              else           → signalQty
  AFTER:  baseQty = signalQty  (direct — no ATR gating)
```

---

## Threading Model

### T1
- `PendingBeArmed` and `GlobalBeAllDisarmed` events are fired from CopyEngine on whatever thread executes the BE state machine. Panel subscribers ALREADY use `Dispatcher.InvokeAsync` (confirmed at lines 3025-3031, 3037-3040 of TradeCopierPanel.cs). No additional marshaling needed.
- `DisarmPendingBe` is called from Panel UI thread. Must be lock-free in CopyEngine implementation — confirm in f01b5155 source. If f01b5155 uses `lock()`, escalate to Director immediately.
- `IsPendingSlotsEmpty` is called from UI thread (inside Dispatcher.InvokeAsync lambda at line 3029). Must be thread-safe read (ConcurrentDictionary.IsEmpty or equivalent).

### T2
- All removed fields (`_atrEnabled` volatile, `_atrEngine` volatile, `_atrPollTimer` DispatcherTimer) are straightforward deletions.
- `DispatcherTimer _atrPollTimer` — only referenced within `StartAtrEngine` and `StopAtrEngine`. Both being removed. No timer cleanup needed.
- No new threading contracts introduced.

---

## 7-Scan Checklist Preview

### T1 Scan Checklist
- **SCAN-01 (lock ban, JS-021)**: Grep restored diff for `lock(` → must be 0 matches.
- **SCAN-02 (throw ban, JS-001)**: Grep restored diff for `throw new` → must be 0 matches in hot paths.
- **SCAN-03 (CYC, JS-066)**: DisarmPendingBe CYC <= 8; IsPendingSlotsEmpty CYC <= 2; RaiseBeAllDisarmed CYC <= 2.
- **SCAN-04 (ASCII, JS-081)**: No Unicode in identifiers or string literals.
- **SCAN-05 (B180-2 exclusion)**: Grep for `ResetBeStateOnFlat|ExecuteBeIdle|ExecuteBeArmed|UpdateBeVisuals|OnBeConnected` → 0 matches in diff.
- **SCAN-06 (Single-Hunk Ban)**: Hunk count != 1 on CopyEngine.cs diff.
- **SCAN-07 (Guardian script)**: `verify_critical_symbols.ps1` exits 0.

### T2 Scan Checklist
- **SCAN-01 (lock ban, JS-021)**: No lock() introduced in any edited file.
- **SCAN-02 (arity consistency, JS-002)**: FeatureFlags 6-param record compiles at all 4 factory sites.
- **SCAN-03 (CYC, JS-066)**: DispatchCopy CYC confirmed <= 8 after ResolveBaseQty removal.
- **SCAN-04 (ASCII, JS-081)**: No new string literals introduced.
- **SCAN-05 (no residual ATR refs)**: Post-edit grep returns 0 matches.
- **SCAN-06 (Single-Hunk Ban)**: Hunk count != 1 on CopyEngine.cs diff.
- **SCAN-07 (Guardian script)**: `verify_critical_symbols.ps1` exits 0. `ptt-sync.ps1` clean.

---

## Risk Register

| # | Risk | Probability | Impact | Mitigation |
|---|------|-------------|--------|------------|
| R1 | T1 Single-Hunk Ban: all 6 items clustered → 1 hunk | Medium | VERIFY_FAIL | Engineer inserts each item at its original structural location in CopyEngine.cs. Verify hunk count before commit. |
| R2 | BeState enum duplicate definition: if Panel already declares BeState → CS0101 | Low | Build fail | Engineer greps both Panel and f01b5155 for `enum BeState` before inserting. If Panel has it, restore to Panel region only; if CopyEngine has it in f01b5155, restore to CopyEngine. |
| R3 | B180-2 contamination: excluded symbols re-introduced with the 6 | Low | Protocol violation | Grep diff for 5 excluded symbols before commit. Any match = abort and re-edit. |
| R4 | FeatureFlags partial arity update | Low | Build fail | Edit all 4 factory sites atomically in a single apply_diff call. |
| R5 | ResolveBaseQty call site missed in DispatchCopy | Medium | Build fail | Grep CopyEngine.cs for `ResolveBaseQty` after edit — must return 0 matches. |
| R6 | UpdateAtrOverlay has undiscovered callers | Low | Build fail (orphan call) | Pre-check grep entire solution for `UpdateAtrOverlay` before removing method. |
| R7 | AtrCalc has callers outside AtrSizingEngine.cs | Low | Build fail after file delete | Pre-check grep for `AtrCalc` in all .cs files except AtrSizingEngine.cs. |
| R8 | IsExternalInit shim removed from FeatureFlags.cs | Low | Build fail (CS0518) | Explicitly preserve lines 1-5 (shim block) in FeatureFlags.cs edit. |
| R9 | T2 edits introduced into T1 branch (cross-contamination) | Low | Protocol violation | T1 and T2 are separate commits. T2 work starts only after T1 VERIFY_PASS. |
| R10 | f01b5155 source uses lock() in DisarmPendingBe | Low | DNA violation if used | Engineer reads restored method body and greps for `lock(`. If found, STOP and escalate to Director before committing. |
| R11 | T1 diff exceeds 300 changed lines on CopyEngine.cs | Low | Protocol violation (Commit Size Gate) | Run `(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^[+-]").Count` before commit. If > 300, STOP and escalate to Director for explicit approval. T1 scope estimate (~30-80 lines) is far below this threshold; a count > 300 signals unintended scope creep. |

---

## Files Modified / Deleted Per Ticket

### T1 Files
| File | Action | Scope |
|------|--------|-------|
| `src/PropTraderTools/CopyEngine.cs` | EDIT — add 6 symbols | ~30-80 lines added (verbatim from f01b5155) |

### T2 Files
| File | Action | Scope |
|------|--------|-------|
| `src/PropTraderTools/FeatureFlags.cs` | EDIT — remove AtrSizing param + 4 factory sites | ~5 lines removed |
| `src/PropTraderTools/CopyEngine.cs` | EDIT — remove 2 fields, 5 methods, 1 call-site patch | ~24 lines removed |
| `src/PropTraderTools/TradeCopierPanel.cs` | EDIT — remove 6 fields, 10 methods, 4 call sites | ~87 lines removed |
| `src/PropTraderTools/TradeCopierAddOn.cs` | EDIT — remove 2 fields, 4 methods, 2 call sites | ~32 lines removed |
| `src/PropTraderTools/AtrSizingEngine.cs` | DELETE | File deleted entirely |
| `src/PropTraderTools/AtrCalc.cs` | DELETE | File deleted entirely |

---

## GUARDIAN / SYNC (after every CopyEngine.cs edit)
```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
powershell -File .\scripts\ptt-sync.ps1
```

Both must exit 0. Any failure = block the commit.

---

## Commit Protocol

| Commit | Scope | Verification gate |
|--------|-------|-------------------|
| T1 commit | CopyEngine.cs only | VERIFY_PASS: build + 6 symbol checks + guardian script |
| T2 commit | 4 edits + 2 deletes | VERIFY_PASS: build + ATR grep + guardian script |

T2 commit happens ONLY after T1 VERIFY_PASS. Director approval required before Ph5.

---

*Written by: ptt-architect (Phase 1) — 2026-07-07*
*Revised by: ptt-architect (Phase 1 cycle 2) — 2026-07-07 — V-01 resolved: Commit Size Gate added to T1 acceptance criteria (item 12), T1 engineer step instructions (Step 6), and risk register (R11).*
*Pre-flight: 9-thought sequential analysis completed.*
*Status: PLAN_COMPLETE — ready for ptt-plan-reviewer Phase 2.*
