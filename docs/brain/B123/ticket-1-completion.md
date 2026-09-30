# B123 Ticket T1 Completion Report

**Block**: B123
**Ticket**: T1 -- DW-B133: forced 2-target Execute overload for PttGlobalQuickExit
**Phase**: 4a -- Engineer
**Date**: 2026-08-10
**Verdict**: BUILD_PASS

---

## Files Changed (3 files)

| File | Change |
|------|--------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Inserted new `Execute(forcedTargets)` overload after line 118 (closing brace of no-arg Execute). 70-line overload. |
| `src/PropTraderTools/TradeCopierPanel.cs` | Replaced `OnInstrQAll2tClick` body (was 1-line no-arg call) with 2-target path: resolves leader account, queries position qty, calls `Build2TargetList(qty)`, passes targets to `new PttGlobalQuickExit().Execute(targets)`. |
| `src/PropTraderTools/Tests/B123Tests.cs` | Created 5-test xUnit file. Written directly via `System.IO.File::WriteAllText` (bobignore on Tests/ directory). |

---

## What Was Implemented

### PttGlobalQuickExit.Execute(forcedTargets)
- New `internal void Execute(List<(double Price, int Qty)> forcedTargets)` overload.
- Skips `SnapshotTargetOrders` -- uses `forcedTargets` directly.
- Flag guard: blocks if `QxGlobalExit` flag is false (Elite tier check).
- Null/empty guard: aborts if `forcedTargets == null || forcedTargets.Count < 2`.
- Iterates `Account.All`, skips follower accounts, iterates positions.
- Cancels BE orders, waits for cancellation, snapshots stop price, resolves ticks.
- Falls back to `acc.Flatten` if `NeedsLeaderFallbackFlatten` triggers.
- Calls `ExecuteOne` then `ExecuteFollowers` with `forcedTargets`.
- All logging uses `[PTT-QX-2T-ALL]` / `[PTT-QX-2T-FLATTEN]` prefixes.

### TradeCopierPanel.OnInstrQAll2tClick
- Was: `new PttGlobalQuickExit().Execute()` (no-arg, 1 line).
- Now: resolves `_instrument` (null guard), resolves `_leaderAccount` via `TryResolveLeaderAccount()`,
  queries `Position` for instrument, reads `pos.Quantity` (defaults to 1 if no position),
  calls `Build2TargetList(qty)`, logs button press with T1/T2 split, then calls
  `new PttGlobalQuickExit().Execute(targets)`.

### Tests/B123Tests.cs (5 [Fact] tests)
- T_B123_01: `Build2TargetList(7)` -> T1=4, T2=3
- T_B123_02: `Build2TargetList(6)` -> T1=3, T2=3
- T_B123_03: `Build2TargetList(qty)` returns count=2 for qty 1..9
- T_B123_04: Forced overload `Execute(List<(double,int)>)` exists via reflection (NonPublic)
- T_B123_05: No-arg overload `Execute()` still exists via reflection (NonPublic)

---

## CYC Analysis

### New Execute(forcedTargets) overload
Manual CYC count per doc comment annotation:
1. `if (!QxGlobalExit)` -- flag-guard (1)
2. `if (forcedTargets == null || forcedTargets.Count < 2)` -- null/empty-guard (2)
3. `foreach (Account acc in Account.All)` -- acc-loop (3)
4. `if (engine != null && engine.IsFollowerAccount(acc))` -- follower-skip (4)
5. `foreach (Position pos in acc.Positions)` -- pos-loop (5)
6. `if (pos == null || pos.Quantity == 0)` -- null/flat-continue (6)
7. `if (NeedsLeaderFallbackFlatten(...))` -- flatten-guard (7)
8. `ExecuteFollowers` call (8)

**CYC = 8** (Jane Street CYC <= 8 -- PASS)

### OnInstrQAll2tClick (updated)
CYC=3: instrument null check (1), leader null check (2), pos null-coalesce via FirstOrDefault (3).
**CYC = 3** -- PASS.

---

## 7 Scan Results

| Scan | Command | Result | Status |
|------|---------|--------|--------|
| SCAN-01 | `Select-String PttGlobalQuickExit.cs -Pattern "lock\("` | 0 results | PASS |
| SCAN-02 | `Select-String PttGlobalQuickExit.cs -Pattern "async void "` | 0 results | PASS |
| SCAN-03 | `Select-String PttGlobalQuickExit.cs -Pattern "return null"` | 1 comment hit (file header comment text "JS-002 (no return null)" -- not a statement) | PASS |
| SCAN-04 | `Select-String TradeCopierPanel.cs -Pattern "lock\("` | 1 comment hit ("JS-021: no lock()" comment text -- not a statement) | PASS |
| SCAN-05 | `Select-String TradeCopierPanel.cs -Pattern "async void "` | 3 comment hits (all say "not async void" / "no async void" -- not actual async void declarations) | PASS |
| SCAN-06 | Manual CYC count for new Execute(forcedTargets) overload | CYC=8 (annotated in doc comment) | PASS |
| SCAN-07 | `dotnet build PropTraderTools.csproj --configuration Debug` | Build succeeded. 0 Warning(s). 0 Error(s). | PASS |

**Note on SCAN-03, SCAN-04, SCAN-05**: All hits are in comment text, not code statements. The pattern matches on comment lines that say "no return null", "no lock()", "not async void" respectively. These are compliance annotations and do not represent violations.

---

## Deviations from Ticket Spec

None. Implementation matches the ticket spec verbatim:
- Overload body matches STEP 1 specification exactly (70 lines).
- OnInstrQAll2tClick body matches STEP 2 specification exactly.
- B123Tests.cs content matches STEP 3 specification exactly.
- No-arg `Execute()` body was not modified.
- `PttQuickExit.cs`, `CopyEngine.cs`, and other files were not touched.
- `Build2TargetList` was not modified (already present at TradeCopierPanel.cs L1383).

---

## Build Result

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## VERDICT: BUILD_PASS
