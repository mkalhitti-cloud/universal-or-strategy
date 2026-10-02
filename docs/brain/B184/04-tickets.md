# B184 Engineering Tickets
## ptt-architect Phase 3 — Ticket Generation

**Date**: 2026-07-07
**Author**: ptt-architect
**Epic**: B184 — Restore CopyEngine.cs BE Event Infrastructure + Full ATR Removal
**Plan source**: `docs/brain/B184/02-architecture-plan.md` — REVIEW_PASS (cycle 2)
**Status**: TICKETS_COMPLETE

---

## EXECUTION ORDER MANDATE

> **T1 MUST be executed and reach VERIFY_PASS before T2 work begins.**
> T1 resolves 4 P0 compile-blocking defects. The build is broken until T1 commits.
> T2 cannot use a broken build as its baseline.
> Each ticket gets its own commit. Do NOT combine into a single commit.

---

---

# B184-T1: Restore CopyEngine.cs BE Event Infrastructure (P0 Compile Blocker)

**Priority**: P0 — Compile-blocking  
**Closes**: DW-B183-06, DW-B183-07, DW-B183-08, DW-B183-09

---

## SCOPE LOCK

**SCOPE: This ticket modifies ONLY `src/PropTraderTools/CopyEngine.cs`. Do not touch any other file.**

---

## 1. Spec Requirement IDs

From `docs/brain/B184/00-orchestrator-prompt.md` — Ticket 1 Acceptance Criteria:

| AC # | Requirement |
|------|-------------|
| AC1 | `dotnet build` exits 0 errors (CS0436 warnings pre-existing — OK) |
| AC2 | `BeState` enum is present in `CopyEngine.cs` |
| AC3 | `PendingBeArmed` and `GlobalBeAllDisarmed` event declarations present in `CopyEngine.cs` |
| AC4 | `DisarmPendingBe`, `IsPendingSlotsEmpty`, `RaiseBeAllDisarmed` method bodies present in `CopyEngine.cs` |
| AC5 | None of the 5 B180-2 approved removals are re-introduced |
| AC6 | `scripts/verify_critical_symbols.ps1` exits 0 (all 16 B183 symbols still present) |
| AC7 | `powershell -File .\scripts\ptt-sync.ps1` runs clean after edit |

From `docs/brain/B183/06-deferred-backlog.md`:

| DW ID | Description | Closed by |
|-------|-------------|-----------|
| DW-B183-06 | `PendingBeArmed` event absent from `CopyEngine.cs` | T1 |
| DW-B183-07 | `GlobalBeAllDisarmed` event absent from `CopyEngine.cs` | T1 |
| DW-B183-08 | `DisarmPendingBe` method absent from `CopyEngine.cs` | T1 |
| DW-B183-09 | `IsPendingSlotsEmpty` + `RaiseBeAllDisarmed` absent from `CopyEngine.cs` | T1 |

---

## 2. Method Signatures to Restore

> **AUTHORITATIVE SOURCE**: `git show f01b5155:src/PropTraderTools/CopyEngine.cs`
> Run the source lookup commands in Step 1 before any edit. If any signature below differs from f01b5155, the f01b5155 version is the source of truth.

```csharp
// 1. BeState enum
// Panel references: lines 3039, 3045, 3049 of TradeCopierPanel.cs
// Insert near other inner enum/class definitions in CopyEngine.cs
internal enum BeState { Idle, Armed, Connected }

// 2. PendingBeArmed event
// Panel subscribe: _engine.PendingBeArmed += OnPendingBeArmedDispatch (line 790)
// Panel unsubscribe: (line 569)
// OnPendingBeArmedDispatch signature: private void OnPendingBeArmedDispatch(string instr, string accountName)
internal event Action<string, string> PendingBeArmed;

// 3. GlobalBeAllDisarmed event
// Panel subscribe: _engine.GlobalBeAllDisarmed += OnGlobalBeAllDisarmed (line 791)
// Panel unsubscribe: (line 570)
// OnGlobalBeAllDisarmed signature: private void OnGlobalBeAllDisarmed()
internal event Action GlobalBeAllDisarmed;

// 4. DisarmPendingBe method
// Panel call: CopyEngine.Instance.DisarmPendingBe(_leaderAccount) (line 3070)
// _leaderAccount is NinjaTrader.Cbi.Account
// Must null-guard, NOT throw (JS-001)
internal void DisarmPendingBe(NinjaTrader.Cbi.Account leader);

// 5. IsPendingSlotsEmpty method
// Panel call: CopyEngine.Instance.IsPendingSlotsEmpty() (line 3067)
// Must be thread-safe read (ConcurrentDictionary.IsEmpty or equivalent)
internal bool IsPendingSlotsEmpty();

// 6. RaiseBeAllDisarmed method
// Panel call: CopyEngine.Instance.RaiseBeAllDisarmed() (line 3071)
// Fires GlobalBeAllDisarmed event
internal void RaiseBeAllDisarmed();
```

**Structural placement** (each symbol at its original structural location — required for Single-Hunk Ban compliance):
- `BeState` enum → near other inner class/enum definitions in CopyEngine.cs
- `PendingBeArmed` event + `GlobalBeAllDisarmed` event → adjacent to other event declarations in the class
- `DisarmPendingBe`, `IsPendingSlotsEmpty`, `RaiseBeAllDisarmed` → in the BE-related method cluster

---

## 3. JS Rule Constraints

| Rule | Constraint | Enforcement |
|------|-----------|-------------|
| JS-021 | No `lock()` in restored methods | SCAN-01: grep restored diff for `lock(` — 0 results. If f01b5155 uses `lock()` in any restored method, STOP and escalate to Director before committing. |
| JS-001 | No `throw` in hot paths — `DisarmPendingBe` must null-guard, not throw | SCAN-06: grep restored diff for `throw new` — 0 matches in hot paths |
| JS-002 | No `return null` — restored methods return `void` or `bool` — N/A | SCAN-02: verify; N/A for these return types |
| JS-033 | No `async void` — restored methods are not async | SCAN-03: grep modified file for `async void` — 0 matches in restored methods |
| JS-066 | CYC <= 8 per method | SCAN-04: DisarmPendingBe CYC ~2-4; IsPendingSlotsEmpty CYC ~1; RaiseBeAllDisarmed CYC ~1 |
| JS-081 | ASCII-only identifiers and string literals | SCAN-05: no Unicode in restored diff |

---

## 4. xUnit [Fact] Test Names

> No test project exists at time of B184. These tests are specified for future implementation. The engineer does NOT write these tests in T1 — record the names in the ticket-1-completion.md as "Planned — no test project."

```csharp
[Fact]
public void BeState_Enum_HasIdle_Armed_Connected_Values()
// Verifies: Enum.GetValues(typeof(CopyEngine.BeState)) contains Idle, Armed, Connected

[Fact]
public void PendingBeArmed_Event_IsActionStringString_Delegate()
// Verifies: typeof(CopyEngine).GetEvent("PendingBeArmed").EventHandlerType == typeof(Action<string, string>)

[Fact]
public void GlobalBeAllDisarmed_Event_IsAction_Delegate()
// Verifies: typeof(CopyEngine).GetEvent("GlobalBeAllDisarmed").EventHandlerType == typeof(Action)

[Fact]
public void IsPendingSlotsEmpty_ReturnsTrue_WhenNoPendingSlots()
// Verifies: CopyEngine.Instance.IsPendingSlotsEmpty() returns true on fresh instance

[Fact]
public void DisarmPendingBe_NullLeader_DoesNotThrow()
// Verifies: DisarmPendingBe(null) does not throw any exception (null-guard honored, JS-001)

[Fact]
public void RaiseBeAllDisarmed_FiresGlobalBeAllDisarmed_Event()
// Verifies: subscribing to GlobalBeAllDisarmed, calling RaiseBeAllDisarmed() fires the event handler
```

---

## 5. Step-by-Step Engineer Instructions

### Step 1 — Locate all 6 symbols in f01b5155

```powershell
git show f01b5155:src/PropTraderTools/CopyEngine.cs | Select-String -Pattern "enum BeState|PendingBeArmed|GlobalBeAllDisarmed|DisarmPendingBe|IsPendingSlotsEmpty|RaiseBeAllDisarmed" -Context 3
```

Read and note: the exact line ranges for each symbol. These define what to extract verbatim.

### Step 2 — Check for BeState enum conflict (duplicate definition risk)

```powershell
# Check if BeState already exists anywhere in current source
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "enum BeState"
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "enum BeState"
```

- If `TradeCopierPanel.cs` already declares `BeState` → **DO NOT add to CopyEngine.cs**. Escalate to Director.
- If neither declares it (expected) → restore from f01b5155 into `CopyEngine.cs`.

### Step 3 — Confirm excluded symbols are NOT in the adjacent context

```powershell
# Verify the 5 B180-2 approved removals are absent from f01b5155 context adjacent to the 6 symbols
git show f01b5155:src/PropTraderTools/CopyEngine.cs | Select-String "ResetBeStateOnFlat|ExecuteBeIdle|ExecuteBeArmed|UpdateBeVisuals|OnBeConnected" -Context 2
```

If any excluded symbol appears adjacent to a symbol you are restoring — be surgical. Extract ONLY the 6 symbols. Do NOT copy any excluded symbol.

### Step 4 — Insert each symbol at its original structural position in CopyEngine.cs

Using `apply_diff` (preferred) or `search_and_replace`:
- Insert each of the 6 symbols at its original structural location in the file.
- Do NOT cluster all 6 at one location — they must appear in structurally correct positions.
- This is mandatory for Single-Hunk Ban compliance (multiple diff hunks required).

### Step 5 — SCAN-01: Check for lock() in restored methods

```powershell
git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^\+" | Select-String "lock\("
# Must return 0 matches.
# If ANY lock() is found in a restored method body: STOP. Do NOT commit. Escalate to Director.
```

### Step 6 — Commit Size Gate (MANDATORY pre-commit check)

```powershell
(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^[+-]").Count
# T1 scope estimate: ~30-80 lines added. This should return well under 300.
# If result > 300: STOP. Do NOT commit. Obtain Director explicit approval before proceeding.
```

### Step 7 — Single-Hunk Ban verification (MANDATORY pre-commit check)

```powershell
(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count
# Must NOT equal 1.
# If = 1: the insertion was squashed into one block. Re-edit to place symbols at structural positions.
```

### Step 8 — B180-2 exclusion grep on the diff

```powershell
git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "ResetBeStateOnFlat|ExecuteBeIdle|ExecuteBeArmed|UpdateBeVisuals|OnBeConnected"
# Must return 0 matches. Any match = abort, re-edit to remove the excluded symbol.
```

### Step 9 — Run guardian script

```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
# Must exit 0 (all 16 B183 symbols still present).
```

### Step 10 — Build verification

```powershell
dotnet build
# Must exit 0 errors. CS0436 NT8 artifact warnings (~172) are pre-existing and OK.
```

### Step 11 — Run ptt-sync.ps1

```powershell
powershell -File .\scripts\ptt-sync.ps1
# Must run clean with no errors.
```

### Step 12 — Verification grep commands

```powershell
# Confirm all 6 symbols are present (all must return matches):
grep -n "BeState\|PendingBeArmed\|GlobalBeAllDisarmed\|DisarmPendingBe\|IsPendingSlotsEmpty\|RaiseBeAllDisarmed" src/PropTraderTools/CopyEngine.cs

# Confirm excluded symbols are absent (all must return 0 matches):
grep -n "ResetBeStateOnFlat\|ExecuteBeIdle\|ExecuteBeArmed\|UpdateBeVisuals\|OnBeConnected" src/PropTraderTools/CopyEngine.cs
```

### Step 13 — Commit

```
git add src/PropTraderTools/CopyEngine.cs
git commit -m "B184-T1: Restore BE event infrastructure (BeState, PendingBeArmed, GlobalBeAllDisarmed, DisarmPendingBe, IsPendingSlotsEmpty, RaiseBeAllDisarmed)"
```

---

## 6. Acceptance Criteria

1. `dotnet build` exits 0 errors. (CS0436 warnings pre-existing — OK.)
2. `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "enum BeState"` returns a match.
3. `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "PendingBeArmed"` returns a match.
4. `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "GlobalBeAllDisarmed"` returns a match.
5. `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "DisarmPendingBe"` returns a match.
6. `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "IsPendingSlotsEmpty"` returns a match.
7. `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "RaiseBeAllDisarmed"` returns a match.
8. None of the 5 B180-2 excluded symbols introduced: `grep "ResetBeStateOnFlat\|ExecuteBeIdle\|ExecuteBeArmed\|UpdateBeVisuals\|OnBeConnected" src/PropTraderTools/CopyEngine.cs` returns 0 matches.
9. `powershell -File .\scripts\verify_critical_symbols.ps1` exits 0.
10. `powershell -File .\scripts\ptt-sync.ps1` runs clean.
11. **Single-Hunk Ban**: `(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count -ne 1`.
12. **Commit Size Gate**: `(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^[+-]").Count` — if result > 300, **STOP. Do NOT commit. Obtain Director explicit approval first.**

---

## 7. Hard Constraints

| Constraint | Rule | Enforcement |
|-----------|------|-------------|
| B180-2 exclusion: `ResetBeStateOnFlat`, `ExecuteBeIdle`, `ExecuteBeArmed`, `UpdateBeVisuals`, `OnBeConnected` MUST NOT appear in diff | Protocol mandate | Step 8 + AC8 |
| Single-Hunk Ban: diff with exactly 1 hunk on `CopyEngine.cs` = automatic VERIFY_FAIL | Protocol mandate | Step 7 + AC11 |
| Commit Size Gate: >300 changed lines on `CopyEngine.cs` requires Director explicit approval | Spec hard constraint | Step 6 + AC12 |
| lock() in restored methods: STOP and escalate | JS-021 (DNA) | Step 5 + SCAN-01 |
| Fix source is f01b5155 verbatim — no cherry-pick of full file | Protocol mandate | Step 1 |
| BeState enum: check for duplicate before inserting | Risk R2 | Step 2 |

---

## 7-Scan Checklist (Engineer Contract)

```
SCAN-01: lock() ban (JS-021)
  grep restored diff for lock( in added lines
  Command: git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^\+" | Select-String "lock\("
  Required: 0 results. If ANY result: STOP. Escalate to Director before committing.

SCAN-02: return null ban (JS-002)
  Restored methods return void or bool — return null is structurally impossible.
  Command: git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^\+" | Select-String "return null"
  Required: 0 results.

SCAN-03: async void ban (JS-033)
  Restored methods are not async.
  Command: git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^\+" | Select-String "async void"
  Required: 0 results.

SCAN-04: CYC <= 8 (JS-066)
  Measure cyclomatic complexity of all 3 restored methods.
  Expected: DisarmPendingBe CYC ~2-4; IsPendingSlotsEmpty CYC ~1; RaiseBeAllDisarmed CYC ~1.
  Required: All restored methods CYC <= 8.

SCAN-05: ASCII-only (JS-081)
  Command: git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "[^\x00-\x7F]"
  Required: 0 non-ASCII characters in diff.

SCAN-06: throw exception ban in hot paths (JS-001)
  DisarmPendingBe must null-guard the leader parameter, NOT throw.
  Command: git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^\+" | Select-String "throw new"
  Required: 0 results in restored method bodies.

SCAN-07: Single-Hunk Ban (protocol)
  Command: (git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count
  Required: Result must NOT equal 1.
  If = 1: VERIFY_FAIL. Re-edit so symbols are at structural positions (multiple hunks).
```

---

---

# B184-T2: Full Removal of ATR Position Sizing Tool

**Priority**: P1 — Director-approved removal  
**Partially closes**: DW-B183-10 (DispatchCopy CYC=8 freeze partially lifted by ATR branch removal)  
**Prerequisite**: B184-T1 VERIFY_PASS must be confirmed before starting T2 work.

---

## SCOPE LOCK

**SCOPE: This ticket modifies ONLY the following files. Do not touch any other file.**
- `src/PropTraderTools/FeatureFlags.cs` — edit (record arity 7 → 6)
- `src/PropTraderTools/CopyEngine.cs` — edit (remove 2 fields, 5 methods, 1 call-site patch)
- `src/PropTraderTools/TradeCopierPanel.cs` — edit (remove 6 fields, 10 methods, 4 call sites)
- `src/PropTraderTools/TradeCopierAddOn.cs` — edit (remove 2 fields, 4 methods, 2 call sites)
- `src/PropTraderTools/AtrSizingEngine.cs` — DELETE (file deleted entirely)
- `src/PropTraderTools/AtrCalc.cs` — DELETE (file deleted entirely)

Do NOT modify any BE event infrastructure symbols (`BeState`, `PendingBeArmed`, `GlobalBeAllDisarmed`, `DisarmPendingBe`, `IsPendingSlotsEmpty`, `RaiseBeAllDisarmed`) restored in T1.

---

## 1. Spec Requirement IDs

From `docs/brain/B184/00-orchestrator-prompt.md` — Ticket 2 Acceptance Criteria:

| AC # | Requirement |
|------|-------------|
| AC1 | `dotnet build` exits 0 errors |
| AC2 | No reference to `AtrSizingEngine`, `AtrCalc`, `_riskDollarsBox`, `_atrFractionBox`, `BuildRiskAtrRow`, `_atrRow`, `_atrDisplayLabel`, `UpdateMaxRisk`, `SetAtrEngine`, `AtrSizing`, `_atrEngine` anywhere in the solution |
| AC3 | `FeatureFlags.cs` compiles — record arity matches all 4 factory sites |
| AC4 | `TradeCopierPanel.cs` has no `BuildRiskAtrRow` call in `BuildUI()` |
| AC5 | `scripts/verify_critical_symbols.ps1` exits 0 (B183 symbols unaffected) |
| AC6 | `powershell -File .\scripts\ptt-sync.ps1` runs clean (no missing-file errors) |
| AC7 | `AtrSizingEngine.cs` and `AtrCalc.cs` do not exist in `src/PropTraderTools/` |

From `docs/brain/B183/06-deferred-backlog.md`:

| DW ID | Description | Impact After T2 |
|-------|-------------|-----------------|
| DW-B183-10 | DispatchCopy CYC=8 frozen at budget | PARTIALLY LIFTED — ResolveBaseQty removal reduces DispatchCopy effective branch count |

---

## 2. Symbols to Remove (Complete Inventory)

### FeatureFlags.cs — Record Arity Change (7 → 6)

```csharp
// BEFORE (7 params):
internal sealed record FeatureFlags(
    bool MultiRule,
    bool TrimFlatten,
    bool BreakEven,
    bool AtrSizing,      // <-- REMOVE THIS LINE
    bool ClickTrader,
    bool MirrorMode,
    bool QxGlobalExit
)

// AFTER (6 params):
internal sealed record FeatureFlags(
    bool MultiRule,
    bool TrimFlatten,
    bool BreakEven,
    bool ClickTrader,
    bool MirrorMode,
    bool QxGlobalExit
)
```

**Factory site arity table — all 4 must be updated atomically:**

| Method | Before (7 args) | After (6 args) |
|--------|-----------------|----------------|
| `Starter()` | `new(false, false, false, false, false, false, false)` | `new(false, false, false, false, false, false)` |
| `Pro()` | `new(true, true, true, false, false, false, false)` | `new(true, true, true, false, false, false)` |
| `Elite()` | `new(true, true, true, true, true, true, true)` | `new(true, true, true, true, true, true)` |
| `FromFeatureList()` | 7 named args including `AtrSizing: System.Linq.Enumerable.Contains(feats, "atr_sizing")` | 6 named args; remove the `AtrSizing:` line entirely |

**PRESERVE**: `IsExternalInit` shim namespace block (top of file) — required for NT8 Roslyn record support. Do NOT touch lines 1-5.

---

### CopyEngine.cs — Fields, Methods, Call-Site Patch

| Approx. Lines | Symbol | Action |
|---------------|--------|--------|
| ~136 | `private volatile bool _atrEnabled = false;` | DELETE line |
| ~137 | `private volatile AtrSizingEngine _atrEngine = null;` | DELETE line |
| ~640-648 | `internal void SetAtrEngine(AtrSizingEngine engine, bool enabled)` | DELETE method + comment block |
| ~649-655 | `internal void UpdateMaxRisk(double maxRiskDollars)` | DELETE method + comment |
| ~657-663 | `internal void UpdateAtrFraction(double fraction)` | DELETE method + comment |
| ~1324-1331 | `internal int GetSuggestedQty(NinjaTrader.Cbi.Instrument instrument)` | DELETE method + comment (ONLY if exclusive to ATR — see Step 1) |
| ~2746-2755 | `private int ResolveBaseQty(NinjaTrader.Cbi.Instrument instrument, int signalQty)` | DELETE method + comment |
| DispatchCopy call site | `ResolveBaseQty(instrument, signalQty)` | REPLACE with `signalQty` |

**After CopyEngine.cs edit — DispatchCopy CYC note**:
Removing the `ResolveBaseQty` call from `DispatchCopy` reduces DispatchCopy's effective branch count. DW-B183-10 CYC=8 freeze is partially lifted. Record this in ticket-2-completion.md.

---

### TradeCopierPanel.cs — Fields to Remove (6)

| Approx. Line | Field |
|--------------|-------|
| ~274 | `private UniformGrid _atrRow = null;` |
| ~295 | `private double _maxRiskDollars = 200.0;` |
| ~296 | `private double _atrFraction = 0.75;` |
| ~297 | `private TextBox _riskDollarsBox;` |
| ~298 | `private TextBox _atrFractionBox;` |
| ~301 | `private TextBlock _atrDisplayLabel;` |

### TradeCopierPanel.cs — Methods to Remove (10)

| Method | Approx. Lines |
|--------|---------------|
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

### TradeCopierPanel.cs — Call Sites to Remove (4)

| Location | Approx. Line | Code to Remove |
|----------|--------------|----------------|
| `BuildUI()` | ~866 | `BuildRiskAtrRow(_contentPanel);` and its adjacent comment |
| `OnLoaded()` | ~766 | `NotifyRiskChanged();` |
| `OnLoaded()` | ~767 | `NotifyAtrFractionChanged();` |
| `ApplyRowVisibilityFlags()` | ~2975-2978 | `if (_atrRow != null) _atrRow.Visibility = f.AtrSizing ...` block (~3 lines) |

---

### TradeCopierAddOn.cs — Fields to Remove (2)

| Approx. Line | Field |
|--------------|-------|
| ~44-45 | `private static readonly ConcurrentDictionary<Chart, AtrSizingEngine> _atrEngines = new ConcurrentDictionary<Chart, AtrSizingEngine>();` |
| ~66 | `private DispatcherTimer _atrPollTimer = null;` |

### TradeCopierAddOn.cs — Methods to Remove (4)

| Method | Approx. Lines |
|--------|---------------|
| `StartAtrEngine(Chart chart, NinjaTrader.Cbi.Instrument instr)` | ~219-257 |
| `StopAtrEngine(Chart chart)` | ~261-273 |
| `UpdateAtrOverlay(string atrDisplay)` | ~279-288 |
| `OnAtrUpdated(string display)` | ~292-294 |

### TradeCopierAddOn.cs — Call Sites to Remove (2)

| Location | Approx. Line | Code to Remove |
|----------|--------------|----------------|
| Chart close handler | ~109 | `StopAtrEngine(chart);` |
| `DoInject` method | ~495 | `StartAtrEngine(chart, instr);` |

---

### Files to Delete

```powershell
Remove-Item src/PropTraderTools/AtrSizingEngine.cs
Remove-Item src/PropTraderTools/AtrCalc.cs
```

---

## 3. JS Rule Constraints

| Rule | Constraint | Enforcement |
|------|-----------|-------------|
| JS-021 | No `lock()` introduced — T2 is removal-only | SCAN-01: grep new/modified lines for `lock(` — 0 results |
| JS-002 | No `return null` introduced — T2 is removal-only. Call-site patch `signalQty` is a variable not a null return | SCAN-02: grep new lines for `return null` — 0 results |
| JS-033 | No `async void` introduced — T2 is removal-only | SCAN-03: verify |
| JS-066 | `DispatchCopy` CYC must still be <= 8 after `ResolveBaseQty` removal. CYC can only go DOWN from removal. | SCAN-04: measure DispatchCopy CYC after edit — must be <= 8 |
| JS-081 | No new string literals or non-ASCII characters introduced | SCAN-05: verify |
| JS-001 | No `throw new` introduced — T2 is removal-only | SCAN-06: verify |
| Record arity | `FeatureFlags` 6-param record must compile at all 4 factory sites | No JS rule number covers record arity; `dotnet build` is the enforcement mechanism. All 4 factory sites must be updated atomically. |

---

## 4. xUnit [Fact] Test Names

> No test project exists at time of B184. These tests are specified for future implementation. The engineer does NOT write these tests in T2 — record the names in ticket-2-completion.md as "Planned — no test project."

```csharp
[Fact]
public void FeatureFlags_Starter_HasSixParameters_NoAtrSizing()
// Verifies: FeatureFlags.Starter() returns an instance with MultiRule=false, TrimFlatten=false,
//           BreakEven=false, ClickTrader=false, MirrorMode=false, QxGlobalExit=false
//           and no AtrSizing property exists on the record

[Fact]
public void FeatureFlags_Elite_HasSixParameters_NoAtrSizing()
// Verifies: FeatureFlags.Elite() returns an instance with all 6 params = true; no AtrSizing

[Fact]
public void FeatureFlags_Pro_HasSixParameters_NoAtrSizing()
// Verifies: FeatureFlags.Pro() returns correct 6-param instance

[Fact]
public void FeatureFlags_Record_DoesNotHaveAtrSizingProperty()
// Verifies: typeof(FeatureFlags).GetProperty("AtrSizing") == null (property removed at compile time)

[Fact]
public void CopyEngine_DoesNotHaveAtrEngine_Field()
// Verifies: no _atrEngine or _atrEnabled field accessible on CopyEngine

[Fact]
public void CopyEngine_DoesNotHave_SetAtrEngine_Method()
// Verifies: typeof(CopyEngine).GetMethod("SetAtrEngine") == null

[Fact]
public void CopyEngine_DoesNotHave_GetSuggestedQty_Method()
// Verifies: typeof(CopyEngine).GetMethod("GetSuggestedQty") == null

[Fact]
public void CopyEngine_DoesNotHave_ResolveBaseQty_Method()
// Verifies: typeof(CopyEngine).GetMethod("ResolveBaseQty", BindingFlags.NonPublic | BindingFlags.Instance) == null
```

---

## 5. Step-by-Step Engineer Instructions

### Pre-Edit Prerequisite Check

```powershell
# Confirm T1 VERIFY_PASS before starting. Build must be green.
dotnet build
# Must exit 0 errors before T2 work begins.
```

### Step 1 — GetSuggestedQty exclusive-caller verification (MANDATORY)

```powershell
# Confirm GetSuggestedQty is ONLY called from ATR-gated code path, not by any other logic
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "GetSuggestedQty"
# Expected: only the definition line (~1325) and ATR-gated call site(s).
# If any non-ATR call site found: escalate to Director before removing GetSuggestedQty.
```

### Step 2 — UpdateAtrOverlay exclusive-caller verification

```powershell
# Confirm no callers of UpdateAtrOverlay exist besides OnAtrUpdated
Select-String -Path src/PropTraderTools/ -Pattern "UpdateAtrOverlay" -Recurse
# If any file OTHER than TradeCopierAddOn.cs references it: escalate to Director.
```

### Step 3 — AtrCalc caller verification

```powershell
# Confirm AtrCalc has zero callers outside AtrSizingEngine.cs
Select-String -Path src/PropTraderTools/ -Pattern "AtrCalc" -Recurse -Include "*.cs" | Where-Object { $_.Filename -notlike "*AtrSizingEngine*" }
# Must return 0 matches.
```

### Step 4 — Edit FeatureFlags.cs (first — establish correct record arity)

Using `apply_diff` (preferred): remove `bool AtrSizing,` parameter from record declaration. Update all 4 factory methods to 6-arg form (see arity table in Section 2). Edit all 4 factory sites in a single `apply_diff` call — atomically.

```powershell
# Verify shim block is untouched (lines 1-5):
Select-String -Path src/PropTraderTools/FeatureFlags.cs -Pattern "IsExternalInit"
# Must return a match.
```

### Step 5 — Edit CopyEngine.cs (remove ATR fields, methods, call-site patch)

Remove in sequence using surgical `apply_diff` calls:
1. Delete field `_atrEnabled` (~line 136)
2. Delete field `_atrEngine` (~line 137)
3. Delete method `SetAtrEngine` + comment block (~lines 640-648)
4. Delete method `UpdateMaxRisk` + comment (~lines 649-655)
5. Delete method `UpdateAtrFraction` + comment (~lines 657-663)
6. Delete method `GetSuggestedQty` + comment (~lines 1324-1331) — only after Step 1 confirms exclusive caller
7. Delete method `ResolveBaseQty` + comment (~lines 2746-2755)
8. In `DispatchCopy`: replace `ResolveBaseQty(instrument, signalQty)` with `signalQty`

```powershell
# After edit: verify ResolveBaseQty is fully removed (call site + definition)
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "ResolveBaseQty"
# Must return 0 matches.
```

### Step 6 — CopyEngine.cs hunk count verification (Single-Hunk Ban)

```powershell
(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count
# Must NOT equal 1. Expected: >= 4 hunks
# (fields hunk + SetAtrEngine/UpdateMaxRisk/UpdateAtrFraction hunk + GetSuggestedQty hunk + ResolveBaseQty hunk)
```

### Step 7 — Edit TradeCopierPanel.cs (remove 6 fields, 10 methods, 4 call sites)

Remove each group using `apply_diff`. Be surgical — do NOT touch BE event infrastructure lines.

```powershell
# Confirm BE event lines are intact after TradeCopierPanel.cs edit:
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "PendingBeArmed\|GlobalBeAllDisarmed\|DisarmPendingBe\|IsPendingSlotsEmpty\|RaiseBeAllDisarmed"
# Must return matches (all BE references still present).
```

### Step 8 — Edit TradeCopierAddOn.cs (remove 2 fields, 4 methods, 2 call sites)

```powershell
# After edit: confirm no ATR references remain in TradeCopierAddOn.cs
Select-String -Path src/PropTraderTools/TradeCopierAddOn.cs -Pattern "AtrSizingEngine|AtrCalc|_atrEngines|_atrPollTimer|StartAtrEngine|StopAtrEngine|UpdateAtrOverlay|OnAtrUpdated"
# Must return 0 matches.
```

### Step 9 — Delete AtrSizingEngine.cs and AtrCalc.cs

```powershell
Remove-Item src/PropTraderTools/AtrSizingEngine.cs
Remove-Item src/PropTraderTools/AtrCalc.cs
```

### Step 10 — Build verification

```powershell
dotnet build
# Must exit 0 errors.
```

### Step 11 — Post-edit grep confirmation (all ATR references must be gone)

```powershell
# All of these must return 0 matches:
Select-String -Path src/ -Pattern "AtrSizingEngine|AtrCalc|_riskDollarsBox|_atrFractionBox|BuildRiskAtrRow|_atrRow|_atrDisplayLabel|UpdateMaxRisk|SetAtrEngine|AtrSizing|_atrEngine|_atrEngines|_atrPollTimer|StartAtrEngine|StopAtrEngine|UpdateAtrOverlay|OnAtrUpdated|NotifyRiskChanged|NotifyAtrFractionChanged" -Recurse -Include "*.cs"
```

PowerShell-style equivalent:
```powershell
# Must all return 0 results:
grep -rn "AtrSizingEngine\|AtrCalc\|_riskDollarsBox\|_atrFractionBox\|BuildRiskAtrRow\|_atrRow\|_atrDisplayLabel\|UpdateMaxRisk\|SetAtrEngine\|AtrSizing\|_atrEngine" src/
```

### Step 12 — Verify deleted files are gone

```powershell
Test-Path src/PropTraderTools/AtrSizingEngine.cs   # must return False
Test-Path src/PropTraderTools/AtrCalc.cs            # must return False
```

### Step 13 — Run guardian script

```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
# Must exit 0 (all 16 B183 symbols still present).
```

### Step 14 — Run ptt-sync.ps1

```powershell
powershell -File .\scripts\ptt-sync.ps1
# Must run clean with no missing-file errors.
# Note: ptt-sync.ps1 may reference AtrSizingEngine.cs or AtrCalc.cs by name.
# Verify the script does not error on their absence. If it does, escalate to Director.
```

### Step 15 — Commit

```
git add -A
git commit -m "B184-T2: Remove ATR position sizing tool (AtrSizingEngine, AtrCalc, panel UI, engine, feature flag)"
```

---

## 6. Acceptance Criteria

1. `dotnet build` exits 0 errors.
2. Post-edit grep (Step 11) returns 0 matches for all ATR identifiers.
3. `AtrSizingEngine.cs` absent: `Test-Path src/PropTraderTools/AtrSizingEngine.cs` returns `False`.
4. `AtrCalc.cs` absent: `Test-Path src/PropTraderTools/AtrCalc.cs` returns `False`.
5. `FeatureFlags.cs` compiles — record arity 6 matches all 4 factory sites (`Starter`, `Pro`, `Elite`, `FromFeatureList`).
6. `TradeCopierPanel.cs` has no `BuildRiskAtrRow` call in `BuildUI()`.
7. `powershell -File .\scripts\verify_critical_symbols.ps1` exits 0 (16 B183 symbols still present).
8. `powershell -File .\scripts\ptt-sync.ps1` runs clean.
9. **Single-Hunk Ban**: `(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count -ne 1`.
10. `BeState`, `PendingBeArmed`, `GlobalBeAllDisarmed`, `DisarmPendingBe`, `IsPendingSlotsEmpty`, `RaiseBeAllDisarmed` are still present in `CopyEngine.cs` (T1 symbols untouched).

---

## 7. Hard Constraints

| Constraint | Rule | Enforcement |
|-----------|------|-------------|
| `GetSuggestedQty` removal only if exclusively called by ATR sizing | Spec hard constraint | Step 1 — pre-check before removing |
| `AtrCalc` removal requires zero external callers | Spec hard constraint | Step 3 — pre-check before deleting |
| `FeatureFlags` record arity: all 4 factory sites updated atomically | Spec hard constraint | Step 4 — single `apply_diff` call for all 4 sites |
| `IsExternalInit` shim block preserved in `FeatureFlags.cs` | Protocol (NT8 Roslyn compat) | Step 4 post-check |
| Single-Hunk Ban on `CopyEngine.cs` | Protocol mandate | Step 6 |
| BE event infrastructure (T1 symbols) must not be removed | Protocol mandate | Step 7 post-check |
| `ptt-sync.ps1` must not error on deleted files | Spec hard constraint | Step 14 |

---

## 7-Scan Checklist (Engineer Contract)

```
SCAN-01: lock() ban (JS-021)
  T2 is removal-only — no new lock() should be introduced.
  Command: git diff HEAD -- src/ --name-only | ForEach-Object { git diff HEAD -- $_ | Select-String "^\+" | Select-String "lock\(" }
  Required: 0 results across all modified files.

SCAN-02: return null ban (JS-002)
  T2 is removal-only. Call-site patch replaces ResolveBaseQty(...) with signalQty (a variable), not null.
  Command: git diff HEAD -- src/ | Select-String "^\+" | Select-String "return null"
  Required: 0 results.

SCAN-03: async void ban (JS-033)
  T2 is removal-only — no async void introduced.
  Command: git diff HEAD -- src/ | Select-String "^\+" | Select-String "async void"
  Required: 0 results.

SCAN-04: CYC <= 8 (JS-066)
  Measure DispatchCopy cyclomatic complexity after ResolveBaseQty removal.
  Note: CYC can only go DOWN from removal. DW-B183-10 freeze partially lifted.
  Required: DispatchCopy CYC <= 8 (expected to be <= 7 after removal).

SCAN-05: ASCII-only (JS-081)
  T2 introduces no new string literals or identifiers.
  Command: git diff HEAD -- src/ | Select-String "^\+" | Select-String "[^\x00-\x7F]"
  Required: 0 non-ASCII characters in added lines.

SCAN-06: throw exception ban in hot paths (JS-001)
  T2 is removal-only — no new throw new introduced.
  Command: git diff HEAD -- src/ | Select-String "^\+" | Select-String "throw new"
  Required: 0 results.

SCAN-07: Single-Hunk Ban (protocol)
  Command: (git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count
  Required: Result must NOT equal 1.
  Expected: >= 4 hunks (fields hunk + SetAtr/UpdateMax/UpdateAtr cluster + GetSuggestedQty + ResolveBaseQty).
```

---

## Verification Grep Commands (Summary)

```powershell
# Must all return 0 results (post-edit confirmation):
grep -rn "AtrSizingEngine\|AtrCalc\|_riskDollarsBox\|_atrFractionBox\|BuildRiskAtrRow\|_atrRow\|_atrDisplayLabel\|UpdateMaxRisk\|SetAtrEngine\|AtrSizing\|_atrEngine" src/

# Files must not exist:
Test-Path src/PropTraderTools/AtrSizingEngine.cs   # must return False
Test-Path src/PropTraderTools/AtrCalc.cs            # must return False

# BE event infrastructure must be intact (must return matches):
grep -n "BeState\|PendingBeArmed\|GlobalBeAllDisarmed\|DisarmPendingBe\|IsPendingSlotsEmpty\|RaiseBeAllDisarmed" src/PropTraderTools/CopyEngine.cs
```

---

---

## Deferred Items Status After B184

| DW ID | Status Before B184 | Status After B184 |
|-------|-------------------|-------------------|
| DW-B183-06 | OPEN (P0) | CLOSED by T1 |
| DW-B183-07 | OPEN (P0) | CLOSED by T1 |
| DW-B183-08 | OPEN (P0) | CLOSED by T1 |
| DW-B183-09 | OPEN (P0) | CLOSED by T1 |
| DW-B183-10 | OPEN (P1) | PARTIALLY LIFTED by T2 (DispatchCopy CYC freeze reduced) |
| DW-B183-01 | OPEN (P2) | Remains OPEN — test coverage not in B184 scope |
| DW-B183-02 | OPEN (P2) | Remains OPEN — test coverage not in B184 scope |
| DW-B183-03 | OPEN (P1) | Remains OPEN — IsLaneEEligible freeze not in B184 scope |
| DW-B183-04 | OPEN (P2) | Remains OPEN — _laneDDispatched growth not in B184 scope |
| DW-B183-05 | OPEN (P1) | Remains OPEN — RULES_CATALOG.md truncation not in B184 scope |

---

## Commit Protocol

| Commit | Scope | Verification Gate |
|--------|-------|-------------------|
| T1 commit | `CopyEngine.cs` ONLY | VERIFY_PASS: build + 6 symbol checks + guardian script + hunk count |
| T2 commit | 4 edits + 2 deletes | VERIFY_PASS: build + ATR grep + file absence + guardian script |

T2 commit happens ONLY after T1 VERIFY_PASS. Both tickets must reach VERIFY_PASS before Ph5 may run.

---

*Written by: ptt-architect (Phase 3) — 2026-07-07*
*Plan source: `docs/brain/B184/02-architecture-plan.md` — REVIEW_PASS (cycle 2 — 2026-07-07)*
*Sequential thinking: 8 thoughts completed before writing.*
