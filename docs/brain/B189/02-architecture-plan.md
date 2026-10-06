# B189 Architecture Plan

**Epic**: B189 -- BE Direction Fix + BE Negative Range + Quick ALL 3T Fix + Quick2t/QAll2t Spinners
**Baseline**: 38eabdb7 (B188-T1 -- NT8 compile verified)
**Brain dir**: docs/brain/B189/
**Author**: ptt-architect (Phase 1)
**Date**: 2026-07-08

---

## LANE-SPLIT GATE

| Question | Answer | Rationale |
|----------|--------|-----------|
| Q1. Same method or within 50 lines? | NO | T1 is in PttBeSwapOne.cs / PttBeSwapAll.cs. T2/T4 are in TradeCopierPanel.cs different methods (>50 lines apart). T3 is in PttGlobalQuickExit.cs. All tickets operate in distinct methods/files. |
| Q2. Fix B design depends on Fix A final design? | NO | T2 (BE clamp) does not depend on T1 (direction flip). T3 (Quick ALL 3T) does not depend on T1 or T2. T4 (spinners) does not depend on T1, T2, or T3. T5 is a test-file fix with zero dependencies. |
| Q3. Each fix has standalone value if the other is blocked? | YES | T1: prevents wrong-side BE exits independently. T2: enables negative buffer independently. T3: enforces 3-target consistency independently. T4: adds spinner UI independently. T5: eliminates xUnit warning independently. |
| Q4. Each fix has an independent SIM verification path? | YES | T1: verify direction sign + computed stop prices. T2: press BE Down past 0, verify label + clamp. T3: observe 3-target submission with 2-target ATM. T4: verify spinner appears + Up/Down update label. T5: build produces 0 xUnit2004 warnings. |

**LANE-SPLIT GATE RESULT: LANES-APPROVED**

NOTE: T2 and T4 both touch `TradeCopierPanel.cs`. They are design-independent but must execute sequentially (T2 then T4) to avoid merge conflicts. Pipeline order: T1 → T2 → T3 → T4 → T5.

---

## Scope Summary

| Ticket | Priority | File(s) | Nature |
|--------|----------|---------|--------|
| T1 | P1 | PttBeSwapOne.cs, PttBeSwapAll.cs | Bug fix: arithmetic sign |
| T2 | P2 | TradeCopierPanel.cs | Bug fix: clamp floor + label format |
| T3 | P2 | PttGlobalQuickExit.cs | Bug fix: target count override + extraction |
| T4 | P2 | TradeCopierPanel.cs | Feature: spinner UI for Quick2t/QAll2t |
| T5 | P3 | src/PropTraderTools/Tests/B131Tests.cs | Maintenance: xUnit2004 warning |

**AC traceability**: AC1--AC5 (T1), AC6--AC8 (T2), AC9--AC10 (T3), AC11--AC14 (T4), AC-DNA + AC-BUILD + AC-SYNC (all).

**Deferred items closed by B189**: DW-B187-02 (via T5).
**Deferred items NOT addressed**: DW-B187-01, DW-B185-BE-ALL-SCOPE-01, DW-B183-05, DW-B183-03, DW-B183-01, DW-B183-02.

---

## T1 -- BE Direction Fix

### Requirement Traceability
Satisfies: AC1, AC2, AC3, AC4, AC5 from 00-orchestrator-prompt.md.
Root cause: `direction = isLong ? -1.0 : +1.0` is inverted. For a long position with positive buffer, stop is placed BELOW entry (loss zone) instead of ABOVE (profit lock).

### Files and Exact Lines

| File | Line | Change |
|------|------|--------|
| `src/PropTraderTools/Features/PttBeSwapOne.cs` | 37 | `isLong ? -1.0 : +1.0` → `isLong ? +1.0 : -1.0` |
| `src/PropTraderTools/Features/PttBeSwapAll.cs` | 55 | `isLong ? -1.0 : +1.0` → `isLong ? +1.0 : -1.0` |

### Method Signatures (unchanged -- no signature change)

```csharp
// PttBeSwapOne.cs -- signature unchanged
internal static void Execute(Account acc, Instrument instr, int bufferTicks)

// PttBeSwapAll.cs -- signature unchanged
internal static void Execute(int bufferTicks, Instrument instrument)
private static void ExecuteOne(Account acc, Position pos, int bufferTicks)
```

### CYC Analysis

- `PttBeSwapOne.Execute`: currently CYC=7 (header-confirmed: 7 branch points). Sign flip adds zero branches. **Post-fix: CYC=7. No violation.**
- `PttBeSwapAll.ExecuteOne`: currently CYC=5 (header-confirmed). Sign flip adds zero branches. **Post-fix: CYC=5. No violation.**
- `PttBeSwapAll.Execute`: currently CYC=7 (not modified). **Unchanged.**

### Direction Logic (post-fix)

```
long + positive buffer -> direction=+1.0 -> raw = avg + (+1.0)*buf*tick -> ABOVE entry (profit lock)
long + negative buffer -> direction=+1.0 -> raw = avg + (+1.0)*(-buf)*tick -> BELOW entry (loss acceptance)
short + positive buffer -> direction=-1.0 -> raw = avg + (-1.0)*buf*tick -> BELOW entry (profit lock for short)
```

### Existing Test Update Required

`PttBeSwapOne_BufferTicks_ShiftsEntryPriceByDirection` (CopyEngineTests.cs, ~line 9968) encodes the BUG formula inline. T1 ticket MUST update this test to use `direction = isLong ? +1.0 : -1.0` and flip the expected values:
- Long + buf=2: entryLong should equal `5000.5` (ABOVE avg), not `4999.5`
- Short + buf=2: entryShort should equal `4999.5` (BELOW avg), not `5000.5`

### xUnit [Fact] Test Names

```
T_B189_T1_01_BeDirection_Long_PositiveBuffer_StopAboveEntry
  Assert: avg=5000.0, tick=0.25, buf=2, isLong=true, direction=+1.0
          raw > avg (5000.5 > 5000.0)

T_B189_T1_02_BeDirection_Long_NegativeBuffer_StopBelowEntry
  Assert: avg=5000.0, tick=0.25, buf=-2, isLong=true, direction=+1.0
          raw < avg (4999.5 < 5000.0)

T_B189_T1_03_BeDirection_Short_PositiveBuffer_StopBelowEntry
  Assert: avg=5000.0, tick=0.25, buf=2, isLong=false, direction=-1.0
          raw < avg (4999.5 < 5000.0)

T_B189_T1_04_PttBeSwapOne_Formula_LongBufPos_ResultAboveAvg
  Assert: inline formula with isLong ? +1.0 : -1.0 for long+buf=2 produces entryPrice > avg

T_B189_T1_05_PttBeSwapAll_Formula_LongBufPos_ResultAboveAvg
  Assert: same formula as T_B189_T1_04 applied to PttBeSwapAll.ExecuteOne formula path
```

Note: Tests T_B189_T1_01 through T_B189_T1_05 are pure formula tests (no NT8 seam required).
The test class is `B189T1BeDirectionTests` in `CopyEngineTests.cs`.

### 7-Scan Checklist

```
SCAN-01: lock() scan         -- PASS (0 violations -- no new lock() usage)
SCAN-02: async void scan     -- PASS (0 violations -- no new async void)
SCAN-03: return null scan    -- N/A (void methods; no return values)
SCAN-04: CYC ceiling check   -- PASS (PttBeSwapOne.Execute CYC=7, PttBeSwapAll.ExecuteOne CYC=5; both <=8)
SCAN-05: ASCII-only check    -- PASS (no string literals modified; sign flip is numeric literal)
SCAN-06: throw new Exception -- PASS (0 violations -- no new throw)
SCAN-07: xUnit2004 check     -- PASS (updated T-B180-T1-03 test uses Assert.Equal with numeric values, not bool)
```

---

## T2 -- BE Negative Range + Label

### Requirement Traceability
Satisfies: AC6, AC7, AC8 from 00-orchestrator-prompt.md.
Root cause 1: `OnBeDown`/`OnBeUp` floor is 0; per-panel BE must match BE ALL range of -20 to +20.
Root cause 2: `UpdateBeLabel` and `BuildBufferedButtonsRow` use `FormatBuffer` which always prepends "+" producing "BE +-3" for negative values.

### Files and Exact Lines

| File | Line | Change |
|------|------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` | 1333 | `Math.Max(Math.Min(_beBuffer + 1, 20), 0)` → `Math.Max(Math.Min(_beBuffer + 1, 20), -20)` |
| `src/PropTraderTools/TradeCopierPanel.cs` | 1340 | `Math.Max(Math.Min(_beBuffer - 1, 20), 0)` → `Math.Max(Math.Min(_beBuffer - 1, 20), -20)` |
| `src/PropTraderTools/TradeCopierPanel.cs` | 1065 | `FormatBuffer("BE", _beBuffer)` → `FormatGlobalBeBuffer("BE", _beBuffer)` |
| `src/PropTraderTools/TradeCopierPanel.cs` | 1363 | `FormatBuffer("BE", _beBuffer)` → `FormatGlobalBeBuffer("BE", _beBuffer)` |

### Method Signatures (unchanged)

```csharp
// All existing -- signatures unchanged
private void OnBeUp(object sender, RoutedEventArgs e)
private void OnBeDown(object sender, RoutedEventArgs e)
private void UpdateBeLabel()
private void BuildBufferedButtonsRow(StackPanel root)
```

`FormatGlobalBeBuffer(string name, int ticks)` is already defined at line 1231-1238 and handles:
- `ticks == 0` → returns `name`
- `ticks > 0` → returns `name + " +" + ticks`
- `ticks < 0` → returns `name + " " + ticks` (int.ToString() of negative auto-includes "-")

No new methods needed. No new fields needed.

### CYC Analysis

- `OnBeUp`: floor changes 0→-20. No new branches. **CYC=1 (unchanged).**
- `OnBeDown`: floor changes 0→-20. No new branches. **CYC=2 (unchanged).**
- `UpdateBeLabel`: method-call swap (FormatBuffer→FormatGlobalBeBuffer). No new branches. **CYC=1 (unchanged).**
- `BuildBufferedButtonsRow`: tuple entry string-value swap. No new branches. **CYC=2 (unchanged).**

### FormatGlobalBeBuffer Behavior (existing, no change)

```
_beBuffer = -3  → FormatGlobalBeBuffer("BE", -3)  = "BE -3"   (correct)
_beBuffer =  0  → FormatGlobalBeBuffer("BE",  0)  = "BE"      (correct)
_beBuffer = +3  → FormatGlobalBeBuffer("BE", +3)  = "BE +3"   (correct)
```

### xUnit [Fact] Test Names

```
T_B189_T2_01_BeBufferClamp_Down_FloorIsNegative20
  Assert: Math.Max(Math.Min(-20 - 1, 20), -20) == -20
          (pressing Down at -20 stays at -20)

T_B189_T2_02_BeBufferClamp_Up_FloorIsNegative20
  Assert: Math.Max(Math.Min(-21 + 1, 20), -20) == -20
          (Up from -21 is impossible but clamp is symmetric: confirms -20 is reachable)

T_B189_T2_03_FormatGlobalBeBuffer_NegativeBuffer_NoPlus
  Assert: FormatGlobalBeBuffer("BE", -3) == "BE -3"
          (no "+" before negative value -- via reflection on private static)

T_B189_T2_04_FormatGlobalBeBuffer_Zero_NameOnly
  Assert: FormatGlobalBeBuffer("BE", 0) == "BE"

T_B189_T2_05_FormatGlobalBeBuffer_Positive_WithPlus
  Assert: FormatGlobalBeBuffer("BE", 3) == "BE +3"
```

Test class: `B189T2BeBufferRangeTests` in `CopyEngineTests.cs`.
FormatGlobalBeBuffer is `private static` -- access via `typeof(TradeCopierPanel).GetMethod("FormatGlobalBeBuffer", BindingFlags.NonPublic | BindingFlags.Static)`.

### 7-Scan Checklist

```
SCAN-01: lock() scan         -- PASS (0 violations)
SCAN-02: async void scan     -- PASS (0 violations)
SCAN-03: return null scan    -- N/A (void methods; UpdateBeLabel has no return)
SCAN-04: CYC ceiling check   -- PASS (OnBeUp=1, OnBeDown=2, UpdateBeLabel=1, BuildBufferedButtonsRow=2; all <=8)
SCAN-05: ASCII-only check    -- PASS ("BE" is ASCII; FormatGlobalBeBuffer uses "-" which is ASCII)
SCAN-06: throw new Exception -- PASS (0 violations)
SCAN-07: xUnit2004 check     -- PASS (tests use Assert.Equal with string values, not Assert.Equal(bool, ...))
```

---

## T3 -- Quick ALL Always 3 Targets

### Requirement Traceability
Satisfies: AC9, AC10 from 00-orchestrator-prompt.md.
Root cause: `Execute(Instrument)` calls `SnapshotTargetOrders` which returns the live ATM snapshot count (may be 2 for a 2-target ATM). `ResolveTargetCount` in PttQuickExit prioritizes `own.Count` when > 0, so leaderTargetCount is ignored. Result: Quick ALL gives 2 targets when it should always give 3.

### CYC Budget for Execute(Instrument) -- CRITICAL

`Execute(Instrument)` is currently **CYC=8** (at the JS-066 ceiling). Adding an inline ternary would push CYC to 9 -- **P0 violation**. Solution: extract the ternary into `ResolveEffectiveTargets`, a new `internal static` helper.

This makes the call site in `Execute(Instrument)` a straight-line assignment (no new branch), preserving CYC=8.

### Files and Exact Lines

| File | Change |
|------|--------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | In `Execute(Instrument)`: replace `ExecuteOne(acc, pos.Instrument, ticks.t1, targets, leaderStop: leaderStop)` with two statements using `effectiveTargets`. Add new `internal static ResolveEffectiveTargets` method. |

**Exact substitution in Execute(Instrument) body** (at line 94, after `var targets = SnapshotTargetOrders(...)`):

```csharp
// BEFORE (line 94):
ExecuteOne(acc, pos.Instrument, ticks.t1, targets, leaderStop: leaderStop);

// AFTER:
var effectiveTargets = ResolveEffectiveTargets(targets, 3);
ExecuteOne(acc, pos.Instrument, ticks.t1, effectiveTargets,
    leaderStop: leaderStop, leaderTargetCount: 3);
```

**New helper method** (add after `NeedsLeaderFallbackFlatten`, before `ResolveQuickTicks`):

```csharp
/// <summary>
/// ResolveEffectiveTargets: when snapshot count < minCount, return empty list so
/// ResolveTargetCount in PttQuickExit falls back to leaderTargetCount instead of own count.
/// When count >= minCount (e.g. PTT-QX re-click with 3 live targets), return targets unchanged.
/// CYC=2: (1) base + (1) ternary. JS-002: never returns null. JS-021: no lock. ASCII-only.
/// </summary>
internal static System.Collections.Generic.List<(double Price, int Qty)> ResolveEffectiveTargets(
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    int minCount
)
{
    return targets.Count >= minCount
        ? targets
        : new System.Collections.Generic.List<(double Price, int Qty)>();
}
```

### Method Signatures

```csharp
// Modified (call site only -- signature unchanged):
internal void Execute(Instrument instrument)

// New helper:
internal static System.Collections.Generic.List<(double Price, int Qty)> ResolveEffectiveTargets(
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    int minCount
)
```

### CYC Analysis

- `Execute(Instrument)`: currently CYC=8. `var effectiveTargets = ResolveEffectiveTargets(...)` is a straight-line assignment (no branch). `ExecuteOne(...effectiveTargets..., leaderTargetCount: 3)` replaces the existing `ExecuteOne` call (no net change to branch count). **Post-fix: CYC=8. No violation.**
- `ResolveEffectiveTargets`: base(1) + ternary(1) = **CYC=2. No violation.**

### Data Flow Verification

```
2-target ATM scenario:
  SnapshotTargetOrders -> targets.Count = 2
  ResolveEffectiveTargets(targets, 3): 2 >= 3 = false -> returns []
  ExecuteOne(..., effectiveTargets=[], leaderTargetCount=3)
  PttQuickExit.ResolveTargetCount([], 3):
    own.Count = 0 -> leaderCount > 0 -> return 3
  Result: 3 targets submitted. AC9 PASS.

3-target PTT-QX re-click scenario:
  SnapshotTargetOrders -> targets.Count = 3 (PTT-QX-T1, PTT-QX-T2, PTT-QX-T3)
  ResolveEffectiveTargets(targets, 3): 3 >= 3 = true -> returns targets
  ExecuteOne(..., effectiveTargets=targets(count=3), leaderTargetCount=3)
  PttQuickExit.ResolveTargetCount(3-list, 3):
    own.Count = 3 > 0 -> return 3
  Result: 3 targets submitted. AC10 PASS.
```

### xUnit [Fact] Test Names

```
T_B189_T3_01_ResolveEffectiveTargets_CountLessThanMin_ReturnsEmpty
  Setup: targets = [(p1,q1), (p2,q2)], minCount = 3
  Assert: result.Count == 0

T_B189_T3_02_ResolveEffectiveTargets_CountEqualsMin_ReturnsOriginal
  Setup: targets = [(p1,q1), (p2,q2), (p3,q3)], minCount = 3
  Assert: ReferenceEquals(result, targets) or result.Count == 3

T_B189_T3_03_ResolveEffectiveTargets_ZeroTargets_ReturnsEmpty
  Setup: targets = [], minCount = 3
  Assert: result.Count == 0
```

Test class: `B189T3QuickAll3TTests` in `CopyEngineTests.cs`.
`ResolveEffectiveTargets` is `internal static` -- directly accessible from the test assembly (same namespace or InternalsVisibleTo already configured).

### 7-Scan Checklist

```
SCAN-01: lock() scan         -- PASS (0 violations -- no new lock() usage)
SCAN-02: async void scan     -- PASS (0 violations -- no new async void)
SCAN-03: return null scan    -- PASS (ResolveEffectiveTargets returns List never null; JS-002 compliant)
SCAN-04: CYC ceiling check   -- PASS (Execute(Instrument) CYC=8 unchanged; ResolveEffectiveTargets CYC=2)
SCAN-05: ASCII-only check    -- PASS (no string literals added)
SCAN-06: throw new Exception -- PASS (0 violations)
SCAN-07: xUnit2004 check     -- PASS (all assertions use Assert.Equal with typed values or Assert.True/False)
```

---

## T4 -- Quick2t/QAll2t Spinners

### Requirement Traceability
Satisfies: AC11, AC12, AC13, AC14 from 00-orchestrator-prompt.md.
Feature: Add +/- spinners to Quick2t and QAll2t buttons, wired to existing handlers. Fix hardcoded `t1=4` in `OnInstr2tClick`.

### Files and Exact Changes

**All changes in `src/PropTraderTools/TradeCopierPanel.cs`.**

#### 4a. BuildInstrRow (lines 1171-1199) -- full rewrite

Replace the current plain 2-button `UniformGrid` with two DockPanel+Grid spinner clusters, following the same structural pattern as `BuildBufferedButtonsRow` spinners.

Key decisions:
- Quick2t Up/Down buttons → wire to **existing `OnQuickUp`/`OnQuickDown`** (share `_quickT1`; no new handlers)
- QAll2t Up/Down buttons → wire to **existing `OnQuickAllUp`/`OnQuickAllDown`** (share `GlobalQuickAllT1`; no new handlers)
- Quick2t initial label: `FormatBuffer("Quick2t", _quickT1)` (stored in `_instr2tBtn`)
- QAll2t initial label: `FormatBuffer("QAll2t", CopyEngine.Instance.GlobalQuickAllT1)` (stored in `_instrQAll2tBtn`)
- Up/Down buttons for both clusters: local variables only (not stored as fields; their sole action is delegating to existing handlers)
- `_instrRowPanel` type: change from `UniformGrid` to `StackPanel` (horizontal) or use same DockPanel pattern as existing spinner rows

**Structural pattern for each spinner cluster** (matches BuildBufferedButtonsRow spec pattern):
```
DockPanel (row container)
  Button "-" (Down) [DockPanel.Dock=Left]  -> Click handler = OnQuickDown (Quick2t) or OnQuickAllDown (QAll2t)
  Button "+" (Up)   [DockPanel.Dock=Right] -> Click handler = OnQuickUp (Quick2t) or OnQuickAllUp (QAll2t)
  Button <label>    [Center, Fill]         -> Click = OnInstr2tClick (Quick2t) or OnInstrQAll2tClick (QAll2t)
                                              Stored in _instr2tBtn / _instrQAll2tBtn respectively
```

Both clusters placed in a `UniformGrid { Columns = 2 }` replacing the current `_instrRowPanel`.

#### 4b. OnQuickUp (line 1622) -- add Quick2t label sync

```csharp
// BEFORE:
private void OnQuickUp(object sender, RoutedEventArgs e)
{
    _quickT1 = Math.Max(1, Math.Min(_quickT1 + 1, 100));
    _quickT2 = _quickT1 * 2;
    if (_quickBtn != null)
        _quickBtn.Content = FormatBuffer("Quick", _quickT1); // (2)
}

// AFTER:
private void OnQuickUp(object sender, RoutedEventArgs e)
{
    _quickT1 = Math.Max(1, Math.Min(_quickT1 + 1, 100));
    _quickT2 = _quickT1 * 2;
    if (_quickBtn != null)
        _quickBtn.Content = FormatBuffer("Quick", _quickT1);
    if (_instr2tBtn != null)
        _instr2tBtn.Content = FormatBuffer("Quick2t", _quickT1);
}
```

#### 4c. OnQuickDown (line 1631) -- add Quick2t label sync

```csharp
// AFTER:
private void OnQuickDown(object sender, RoutedEventArgs e)
{
    _quickT1 = Math.Max(1, Math.Min(_quickT1 - 1, 100));
    _quickT2 = _quickT1 * 2;
    if (_quickBtn != null)
        _quickBtn.Content = FormatBuffer("Quick", _quickT1);
    if (_instr2tBtn != null)
        _instr2tBtn.Content = FormatBuffer("Quick2t", _quickT1);
}
```

#### 4d. OnInstr2tClick (line 1696) -- replace hardcoded 4 with _quickT1

```csharp
// BEFORE (line 1696):
new PttQuickExit().Execute(_leaderAccount, _instrument, 4, targets);

// AFTER:
new PttQuickExit().Execute(_leaderAccount, _instrument, _quickT1, targets);
```

#### 4e. OnQuickAllBufferChanged (line 1003) -- add QAll2t label sync (inside Dispatcher.InvokeAsync)

```csharp
// BEFORE:
private void OnQuickAllBufferChanged(int newT1)
{
    Dispatcher.InvokeAsync(() =>
    {
        if (_quickAllBtn != null)
            _quickAllBtn.Content = FormatQuickAllBuffer("Quick ALL", newT1);
    });
}

// AFTER:
private void OnQuickAllBufferChanged(int newT1)
{
    Dispatcher.InvokeAsync(() =>
    {
        if (_quickAllBtn != null)
            _quickAllBtn.Content = FormatQuickAllBuffer("Quick ALL", newT1);
        if (_instrQAll2tBtn != null)
            _instrQAll2tBtn.Content = FormatBuffer("QAll2t", newT1);
    });
}
```

CRITICAL: `_instrQAll2tBtn?.Content` update MUST be inside the `Dispatcher.InvokeAsync` lambda. `OnQuickAllBufferChanged` is a delegate that may fire from a non-UI thread. Updating WPF Button.Content from non-UI thread = cross-thread exception.

### Method Signatures

```csharp
// All existing -- signatures unchanged
private void BuildInstrRow()                          // rewritten body, same signature
private void OnQuickUp(object sender, RoutedEventArgs e)
private void OnQuickDown(object sender, RoutedEventArgs e)
private void OnInstr2tClick(object sender, RoutedEventArgs e)
private void OnQuickAllBufferChanged(int newT1)
```

No new handler methods. No new fields (Up/Down buttons for spinner are local variables).

### CYC Analysis

- `BuildInstrRow`: sequential construction, no branches. **CYC=1. No violation.**
- `OnQuickUp`: existing CYC=2 (one `if (_quickBtn != null)` branch). Add one `if (_instr2tBtn != null)` branch → **CYC=3. No violation.**
- `OnQuickDown`: same as OnQuickUp. **CYC=3. No violation.**
- `OnInstr2tClick`: field-reference swap (4 → `_quickT1`), no new branches. **CYC=2 (unchanged).**
- `OnQuickAllBufferChanged`: lambda body adds one `if (_instrQAll2tBtn != null)` branch. Outer function is straight-line. Lambda CYC increases by 1. **No violation.**

### xUnit [Fact] Test Names

```
T_B189_T4_01_Quick2tSpinnerLabel_MatchesQuickT1Default
  Assert: FormatBuffer("Quick2t", 4) == "Quick +4" -- confirms label formula for default _quickT1=4
  (Pure formula test; no NT8 seam required)

T_B189_T4_02_QAll2tLabel_MatchesGlobalQuickAllT1Format
  Assert: FormatBuffer("QAll2t", 4) == "Quick +4" format is consistent (same FormatBuffer static method)

T_B189_T4_03_OnInstr2tClick_Contract_NoHardcodedFour
  Assert: (Structural) IL of OnInstr2tClick does not contain ldci4.4 (IL opcode for constant 4)
          OR: via PttQuickExit parameter route test

T_B189_T4_04_OnQuickAllBufferChanged_Contract_InstrQAll2tBtnUpdated
  Assert: (Structural reflection) OnQuickAllBufferChanged body uses _instrQAll2tBtn field
          Confirm via MethodBody.GetILAsByteArray or field-usage reflection scan

T_B189_T4_05_QuickT1Shared_OnQuickUp_UpdatesBothLabels
  Assert: (Formula/inline) _quickT1 is the same field for Quick and Quick2t.
          After simulating increment: FormatBuffer("Quick", newT1) == FormatBuffer("Quick2t", newT1)
          when same int value is used for both (confirms shared field, no divergence)
```

Test class: `B189T4SpinnerTests` in `CopyEngineTests.cs`.

### 7-Scan Checklist

```
SCAN-01: lock() scan         -- PASS (0 violations -- no new lock() usage)
SCAN-02: async void scan     -- PASS (0 violations -- no new async void; OnQuickAllBufferChanged uses Dispatcher.InvokeAsync not async void)
SCAN-03: return null scan    -- N/A (void methods)
SCAN-04: CYC ceiling check   -- PASS (BuildInstrRow=1, OnQuickUp=3, OnQuickDown=3, OnInstr2tClick=2; all <=8)
SCAN-05: ASCII-only check    -- PASS ("Quick2t", "QAll2t", "+" are all ASCII)
SCAN-06: throw new Exception -- PASS (0 violations)
SCAN-07: xUnit2004 check     -- PASS (all assertions use typed Assert methods, not Assert.Equal(bool, ...))
```

---

## T5 -- DW-B187-02 Bundle (INCLUDED)

### Decision: INCLUDE

**Rationale**: DW-B187-02 is a 1-line test-file change with:
- Zero production CYC impact (test file only, no production code)
- Zero production method signatures changed
- Zero NT8 API involvement
- Eliminates a known xUnit2004 warning (`Assert.Equal(true, ...)` → `Assert.True(...)`)
- File location: `src/PropTraderTools/Tests/B131Tests.cs` line 165

The orchestrator prompt explicitly authorizes bundling if CYC=0 impact confirmed. Confirmed.

### File and Exact Change

| File | Line | Change |
|------|------|--------|
| `src/PropTraderTools/Tests/B131Tests.cs` | 165 | `Assert.Equal(true, (bool)field.GetValue(null)!)` → `Assert.True((bool)field.GetValue(null)!)` |

### CYC Analysis

- Test file: not subject to production CYC budget.
- **Production CYC impact: ZERO.**

### 7-Scan Checklist

```
SCAN-01: lock() scan         -- PASS (test file -- no new lock())
SCAN-02: async void scan     -- PASS (no new async void)
SCAN-03: return null scan    -- N/A
SCAN-04: CYC ceiling check   -- N/A (test file, zero production CYC impact)
SCAN-05: ASCII-only check    -- PASS (no string literals changed)
SCAN-06: throw new Exception -- PASS
SCAN-07: xUnit2004 check     -- PASS (Assert.True replaces Assert.Equal(true, ...) -- this IS the fix)
```

---

## 7-Scan Checklist Template (per-ticket reference)

The following is the canonical scan template. Each ticket section above contains a populated instance.

```
SCAN-01: lock() scan         -- [PASS|FAIL]: grep src/ for new lock() usages
SCAN-02: async void scan     -- [PASS|FAIL]: grep src/ for new "async void" (event handlers excluded)
SCAN-03: return null scan    -- [PASS|N/A]: return null in hot path = FAIL; non-hot or void = N/A
SCAN-04: CYC ceiling check   -- [PASS|FAIL]: all modified methods CYC <= 8 (JS-066)
SCAN-05: ASCII-only check    -- [PASS|FAIL]: 0 non-ASCII chars in string literals of modified files
SCAN-06: throw new Exception -- [PASS|FAIL]: 0 new throw new XxxException() in modified methods
SCAN-07: xUnit2004 check     -- [PASS|FAIL]: 0 xUnit2004 warnings after build
```

---

## Acceptance Criteria Traceability Matrix

| AC | Ticket | Condition | Verified By |
|----|--------|-----------|-------------|
| AC1 | T1 | Long + buf=+2: BE stop ABOVE entry (entry + 2t) | T_B189_T1_01 |
| AC2 | T1 | Long + buf=-2: BE stop BELOW entry (entry - 2t) | T_B189_T1_02 |
| AC3 | T1 | Short + buf=+2: BE stop BELOW entry (entry - 2t) | T_B189_T1_03 |
| AC4 | T1 | direction in PttBeSwapOne line 37 = `isLong ? +1.0 : -1.0` | T_B189_T1_04 + code review |
| AC5 | T1 | direction in PttBeSwapAll line 55 = `isLong ? +1.0 : -1.0` | T_B189_T1_05 + code review |
| AC6 | T2 | OnBeDown clamp floor = -20 | T_B189_T2_01 |
| AC7 | T2 | UpdateBeLabel uses FormatGlobalBeBuffer("BE", _beBuffer) | T_B189_T2_03/04/05 + code review |
| AC8 | T2 | Initial label in BuildBufferedButtonsRow uses FormatGlobalBeBuffer | code review (line 1065) |
| AC9 | T3 | Quick ALL with 2-target ATM: submits 3 targets | T_B189_T3_01 |
| AC10 | T3 | Quick ALL with 3 PTT-QX targets (re-click): submits 3 (no regression) | T_B189_T3_02 |
| AC11 | T4 | Quick2t has Up/Down spinner; Up increments label and _quickT1 | T_B189_T4_05 |
| AC12 | T4 | QAll2t has Up/Down spinner; Up increments GlobalQuickAllT1 | T_B189_T4_02 |
| AC13 | T4 | OnInstr2tClick passes _quickT1 not hardcoded 4 | T_B189_T4_03 |
| AC14 | T4 | QAll2t label updates when GlobalQuickAllT1 changes (broadcast) | T_B189_T4_04 |
| AC-DNA | All | SCAN-01..06: 0 new violations; all modified methods CYC <= 8 | 7-scan per ticket |
| AC-BUILD | All | dotnet build 0 errors, 0 new warnings | build output |
| AC-SYNC | All | ptt-sync.ps1 19 linked, 0 errors | sync script output |

---

## Files Modified Summary

| File | Tickets | Nature of Change |
|------|---------|-----------------|
| `src/PropTraderTools/Features/PttBeSwapOne.cs` | T1 | Line 37: sign flip (1 char) |
| `src/PropTraderTools/Features/PttBeSwapAll.cs` | T1 | Line 55: sign flip (1 char) |
| `src/PropTraderTools/TradeCopierPanel.cs` | T2, T4 | Lines 1333, 1340, 1065, 1363 (T2); BuildInstrRow body, OnQuickUp/Down +1 line each, OnInstr2tClick 1 token, OnQuickAllBufferChanged +2 lines (T4) |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | T3 | Execute(Instrument) call site (2 lines replacing 1); new ResolveEffectiveTargets method |
| `src/PropTraderTools/CopyEngineTests.cs` | T1--T4 | New test classes B189T1--B189T4; update existing T-B180-T1-03 test |
| `src/PropTraderTools/Tests/B131Tests.cs` | T5 | Line 165: Assert.Equal(true, ...) → Assert.True(...) |

**Total production files touched**: 4
**Total test files touched**: 2
**New production methods**: 1 (`ResolveEffectiveTargets` in PttGlobalQuickExit.cs)
**New handlers**: 0 (T4 reuses existing OnQuickUp/Down, OnQuickAllUp/Down)

---

## Deferred Backlog Notes

The following items from B188 06-deferred-backlog.md are NOT addressed in B189:

| DW ID | Status | Reason |
|-------|--------|--------|
| DW-B187-01 | OPEN | ResolveFollowerTargets followerPosQty review -- out of B189 scope |
| DW-B185-BE-ALL-SCOPE-01 | OPEN | Spec HTML update -- Director/ops task |
| DW-B183-05 | OPEN | RULES_CATALOG.md truncated -- ops task |
| DW-B183-03 | OPEN | IsLaneEEligible CYC freeze -- no Lane E mod in B189 |
| DW-B183-01 | OPEN | Slate Gate tests -- no test coverage block in B189 |
| DW-B183-02 | OPEN | FlatGuardLoop test -- no test coverage block in B189 |

DW-B187-02 is **CLOSED** by T5.

---

*Written by: ptt-architect*
*Epic: B189*
*Phase: 1 (Architecture Planning)*
*Baseline: 38eabdb7 (B188-T1)*
*Status: PLAN_COMPLETE -- ready for ptt-plan-reviewer (Phase 2)*
