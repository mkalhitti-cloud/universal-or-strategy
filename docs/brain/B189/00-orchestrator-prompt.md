# B189 Orchestrator Prompt
# Paste into ptt-orchestrator session to execute B189.

$execute-pipeline | PTT-COPIER B189 | BE Direction Fix + BE Negative Range + Quick ALL 3T Fix + Quick2t/QAll2t Spinners

## Context

Post-B188 NT8 validation revealed 4 issues across the BE and Quick button families.
All bugs are in UI panel + feature files. No CopyEngine structural changes.

## Baseline

- Commit: 38eabdb7 (B188-T1 -- last clean commit, NT8 compile verified)
- Spec: c:/WSGTA/universal-or-strategy-director/specs/002-trade-copier-spec.html
- Brain dir: docs/brain/B189/
- DW cards: docs/brain/B189/DW-*.md (to be written in Ph1)

## Files in scope

| File | Tickets |
|------|---------|
| `src/PropTraderTools/Features/PttBeSwapOne.cs` | T1 |
| `src/PropTraderTools/Features/PttBeSwapAll.cs` | T1 |
| `src/PropTraderTools/TradeCopierPanel.cs` | T2, T4 |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | T3 |
| `src/PropTraderTools/CopyEngineTests.cs` | T1, T2, T3, T4 (structural tests) |

## Ticket Scope

### T1 -- BE direction fix [P1]
**Files**: `PttBeSwapOne.cs` + `PttBeSwapAll.cs`
**Bug**: `direction = isLong ? -1.0 : +1.0` is inverted in both files.
For a long position, `buf=+2` moves the BE stop 2 ticks BELOW entry (loss zone) instead of ABOVE (profit lock).
Confirmed by output.md line 38: buf=2t, entry=7831.5, avg=7832.25 -- stop placed below average.
Expected: long + positive buffer = stop above entry; long + negative buffer = stop below entry.
**Fix**: Change `direction = isLong ? -1.0 : +1.0` to `direction = isLong ? +1.0 : -1.0` in both files.
- `PttBeSwapOne.cs` line 37
- `PttBeSwapAll.cs` line 55
CYC: unchanged (pure sign flip, no new branches).

### T2 -- BE button negative range + label [P2]
**File**: `TradeCopierPanel.cs`
**Bug 1**: `OnBeDown` clamps `_beBuffer` floor at 0 (line 1340). BE ALL allows -10 via `PttGlobalBreakEven.DecrementBuffer`. Per-panel BE must match.
**Fix**: Change `Math.Max(Math.Min(_beBuffer - 1, 20), 0)` to `Math.Max(Math.Min(_beBuffer - 1, 20), -20)` in `OnBeDown`.
Also fix `OnBeUp` upper clamp to be symmetric: `Math.Max(Math.Min(_beBuffer + 1, 20), -20)`.
**Bug 2**: `FormatBuffer` (line 1216) always prepends `"+"`:
```csharp
return name + " +" + ticks;  // produces "BE +-3" for negative -- wrong
```
**Fix**: Replace `UpdateBeLabel()` call to use `FormatGlobalBeBuffer("BE", _beBuffer)` instead of `FormatBuffer("BE", _beBuffer)`. `FormatGlobalBeBuffer` already handles 0/positive/negative correctly.
Also update the initial label in `BuildBufferedButtonsRow` specs array (line 1065) from `FormatBuffer("BE", _beBuffer)` to `FormatGlobalBeBuffer("BE", _beBuffer)`.
CYC: unchanged.

### T3 -- Quick ALL always 3 targets [P2]
**File**: `PttGlobalQuickExit.cs`
**Bug**: `Execute(Instrument)` calls `SnapshotTargetOrders` which returns whatever ATM targets are live.
With a 2-target ATM, snapshot returns 2 → `ResolveTargetCount(2-list, 0) = 2`. Quick ALL gives 2 targets.
Quick (per-chart) always gives 3 (fallback default in `ResolveTargetCount`).
**Fix**: In `Execute(Instrument)`, after `SnapshotTargetOrders`, pass `leaderTargetCount: 3` to `ExecuteOne`:
```csharp
// BEFORE:
ExecuteOne(acc, pos.Instrument, ticks.t1, targets, leaderStop: leaderStop);
// AFTER:
ExecuteOne(acc, pos.Instrument, ticks.t1, targets, leaderStop: leaderStop, leaderTargetCount: 3);
```
This means `ResolveTargetCount` receives `own.Count=2, leaderCount=3` and returns `Math.Min(max(2,3),3) = 3`.
Wait -- `ResolveTargetCount` logic is `own?.Count > 0 ? own.Count : leaderCount > 0 ? leaderCount : 3`.
When own.Count=2 > 0 it returns own.Count=2, ignoring leaderCount.
Correct fix: also pass `targets` with count forced to 0 when count < 3, so ResolveTargetCount falls through to leaderCount=3.
Simpler: override `targets` with an empty list when `targets.Count < 3` before calling ExecuteOne:
```csharp
var effectiveTargets = targets.Count >= 3 ? targets : new System.Collections.Generic.List<(double, int)>();
ExecuteOne(acc, pos.Instrument, ticks.t1, effectiveTargets, leaderStop: leaderStop, leaderTargetCount: 3);
```
This preserves the 3-target PTT-QX re-click path (when 3 PTT-QX targets are live, count=3, effectiveTargets=targets).
And forces 3 when native ATM has 2 targets (count=2, effectiveTargets=empty, leaderCount=3 → fallback=3).
CYC: +1 in Execute(Instrument) for the ternary. Still CYC=8 (one of the existing 8 decision points can be
absorbed if the instrument filter is counted in the null/flat continue -- architect to verify CYC budget).
If CYC would exceed 8: extract to a helper `ResolveEffectiveTargets(targets, minCount)` CYC=1.

### T4 -- Quick2t + QAll2t +/- spinner [P2]
**File**: `TradeCopierPanel.cs`
**Feature**: Quick2t and QAll2t currently have no +/- spinners (plain buttons in `BuildInstrRow()`).
Add spinners that control tick spacing using the EXISTING fields:
- Quick2t uses `_quickT1` (same field as Quick button, per-panel)
- QAll2t uses `CopyEngine.Instance.GlobalQuickAllT1` (same singleton as Quick ALL button)
Targets remain fixed at exactly 2.

**BuildInstrRow changes**:
Replace the plain `UniformGrid` with two DockPanel+Grid spinner clusters (same pattern as `BuildBufferedButtonsRow`).
- Quick2t cluster: Up=OnQuick2tUp, Down=OnQuick2tDown, Click=OnInstr2tClick
  Label: `FormatBuffer("Quick2t", _quickT1)`  -- shares _quickT1 with Quick button
- QAll2t cluster: Up=OnQAll2tUp, Down=OnQAll2tDown, Click=OnInstrQAll2tClick
  Label: `FormatBuffer("QAll2t", CopyEngine.Instance.GlobalQuickAllT1)`

**New handlers**:
- `OnQuick2tUp`: `_quickT1 = Math.Max(1, Math.Min(_quickT1 + 1, 100)); UpdateQuickLabels();`
  (UpdateQuickLabels updates both Quick and Quick2t labels -- or inline update both buttons)
- `OnQuick2tDown`: same with -1
- `OnQAll2tUp`: `CopyEngine.Instance.IncrementQuickAll();` (broadcast updates Quick ALL + QAll2t labels)
- `OnQAll2tDown`: `CopyEngine.Instance.DecrementQuickAll();`

**OnQuickAllBufferChanged** already broadcasts to `_quickAllBtn`. Also update `_instrQAll2tBtn` label there.

**OnInstr2tClick** fix: change hardcoded `t1=4` to `_quickT1`:
```csharp
// BEFORE:
new PttQuickExit().Execute(_leaderAccount, _instrument, 4, targets);
// AFTER:
new PttQuickExit().Execute(_leaderAccount, _instrument, _quickT1, targets);
```

**OnInstrQAll2tClick**: spacing already comes from `GlobalQuickAllT1` via `ResolveQuickTicks` in
`PttGlobalQuickExit.Execute(List,Instrument)`. No change needed to the execute call.

**QAll2t label sync**: `OnQuickAllBufferChanged` must also update `_instrQAll2tBtn.Content`.

Note: Quick2t Up/Down share `_quickT1` with Quick Up/Down. When the user presses Quick2t Up, Quick button
label also updates (they share the field). This is CORRECT and expected -- one tick value controls both.
So `OnQuick2tUp` and `OnQuick2tDown` are identical to `OnQuickUp` and `OnQuickDown` -- they can be the
SAME handlers. Wire Quick2t Up/Down to `OnQuickUp`/`OnQuickDown`. No new handlers needed for Quick2t.
Similarly QAll2t Up/Down wire to `OnQuickAllUp`/`OnQuickAllDown` (same singleton). No new handlers.

**CYC**: No new decision points. `BuildInstrRow` CYC stays at 1 (sequential construction).

## Acceptance Criteria

| AC | Ticket | Condition |
|----|--------|-----------|
| AC1 | T1 | Long + buf=+2: BE stop placed ABOVE entry (entry + 2t) |
| AC2 | T1 | Long + buf=-2: BE stop placed BELOW entry (entry - 2t) |
| AC3 | T1 | Short + buf=+2: BE stop placed BELOW entry (entry - 2t) |
| AC4 | T1 | `direction` sign in PttBeSwapOne line 37 = `isLong ? +1.0 : -1.0` |
| AC5 | T1 | `direction` sign in PttBeSwapAll line 55 = `isLong ? +1.0 : -1.0` |
| AC6 | T2 | `OnBeDown` clamp floor = -20 |
| AC7 | T2 | `UpdateBeLabel` uses `FormatGlobalBeBuffer("BE", _beBuffer)` |
| AC8 | T2 | Initial label in BuildBufferedButtonsRow uses `FormatGlobalBeBuffer("BE", _beBuffer)` |
| AC9 | T3 | Quick ALL with 2-target ATM active: submits 3 targets (T1+T2+T3) |
| AC10 | T3 | Quick ALL with 3 PTT-QX targets active (re-click): submits 3 targets (no regression) |
| AC11 | T4 | Quick2t has Up/Down spinner; clicking Up increments label and _quickT1 |
| AC12 | T4 | QAll2t has Up/Down spinner; clicking Up increments GlobalQuickAllT1 |
| AC13 | T4 | OnInstr2tClick passes `_quickT1` not hardcoded 4 |
| AC14 | T4 | QAll2t label updates when GlobalQuickAllT1 changes (broadcast) |
| AC-DNA | All | SCAN-01..06: 0 new violations; all modified methods CYC <= 8 |
| AC-BUILD | All | dotnet build 0 errors, 0 new warnings |
| AC-SYNC | All | ptt-sync.ps1 19 linked, 0 errors |

## Building Blocks (architect MUST read before planning)

### BE direction -- PttBeSwapOne.cs
```csharp
// CURRENT (wrong):
bool isLong = pos.MarketPosition == MarketPosition.Long;
double direction = isLong ? -1.0 : +1.0;
double raw = pos.AveragePrice + direction * bufferTicks * tickSize;
// FIX:
double direction = isLong ? +1.0 : -1.0;
```

### BE button negative range -- TradeCopierPanel.cs
```csharp
// CURRENT OnBeDown (wrong floor):
_beBuffer = Math.Max(Math.Min(_beBuffer - 1, 20), 0);
// FIX:
_beBuffer = Math.Max(Math.Min(_beBuffer - 1, 20), -20);

// CURRENT OnBeUp (wrong floor):
_beBuffer = Math.Max(Math.Min(_beBuffer + 1, 20), 0);
// FIX:
_beBuffer = Math.Max(Math.Min(_beBuffer + 1, 20), -20);

// CURRENT label (always "+"):
FormatBuffer("BE", _beBuffer)  // -> "BE +-3" for negative
// FIX:
FormatGlobalBeBuffer("BE", _beBuffer)  // -> "BE -3" for negative, "BE +3" for positive, "BE" for 0
```

### Quick ALL 3T -- PttGlobalQuickExit.cs Execute(Instrument)
```csharp
// After SnapshotTargetOrders, before ExecuteOne:
// Replace:
ExecuteOne(acc, pos.Instrument, ticks.t1, targets, leaderStop: leaderStop);
// With (force 3 when ATM snapshot < 3):
var effectiveTargets = targets.Count >= 3
    ? targets
    : new System.Collections.Generic.List<(double Price, int Qty)>();
ExecuteOne(acc, pos.Instrument, ticks.t1, effectiveTargets,
    leaderStop: leaderStop, leaderTargetCount: 3);
```

### Quick2t spinner -- TradeCopierPanel.cs BuildInstrRow
Quick2t Up/Down wire to EXISTING `OnQuickUp`/`OnQuickDown` (share _quickT1).
QAll2t Up/Down wire to EXISTING `OnQuickAllUp`/`OnQuickAllDown` (share GlobalQuickAllT1).
Initial Quick2t label: `FormatBuffer("Quick2t", _quickT1)`.
Initial QAll2t label: `FormatBuffer("QAll2t", CopyEngine.Instance.GlobalQuickAllT1)`.
`OnInstr2tClick` line 1696: change `4` to `_quickT1`.
`OnQuickAllBufferChanged`: add `_instrQAll2tBtn?.Content = FormatBuffer("QAll2t", newT1);`

## Pipeline (ALL phases mandatory -- NONE skippable -- NONE combinable)

Ph1  ptt-architect       -> 02-architecture-plan.md
Ph2  ptt-plan-reviewer   -> 02-plan-review.md       (REVIEW_PASS gate)
Ph3  ptt-architect       -> 04-tickets.md
Ph3.5 ptt-ticket-reviewer -> 04-ticket-review.md    (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src .cs edits + ticket-N-completion.md
Ph4b ptt-verifier        -> ticket-N-verification.md (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> 05-final-review.md + 06-deferred-backlog.md

Tickets execute sequentially: T1 -> T2 -> T3 -> T4.
Each ticket requires VERIFY_PASS before the next Ph4a begins.

## Deferred items to bundle if zero extra CYC cost

From B188 06-deferred-backlog.md:
- DW-B187-02: xUnit2004 in B131Tests.cs line 165 (Assert.Equal boolean). 1-line fix. Bundle as T5 if
  engineer confirms CYC=0 impact (it is a test file -- no CYC budget constraint).

---

*Written: 2026-07-08*
*Baseline: 38eabdb7 (B188-T1)*
*Brain dir: docs/brain/B189/*
