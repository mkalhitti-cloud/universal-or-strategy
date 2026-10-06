# B187 Orchestrator Prompt
# Paste into ptt-orchestrator session to kick off B187.

$execute-pipeline | PTT-COPIER B187 | QX Stop Snapshot Fix + DIAG Cleanup + CS0067 Suppress

## Context

B186 NT8 validation revealed a P1 bug: when BE ALL fires before QAll2t or Quick ALL,
the PTT-QX-Stop orders are not submitted (snapshotStop = 0). Root cause: leaderStop is
snapshotted after CancelPttBeOrders destroys the only Working stop reference.
Two P3 deferred items from B186 backlog are bundled in the same pass (same files, zero risk).

## Baseline

- Commit: 9e5d949f (B186-T3 -- last clean, NT8 compile verified)
- Spec: c:/WSGTA/universal-or-strategy-director/specs/002-trade-copier-spec.html
- Continue prompt: docs/brain/B187/00-continue-prompt.md
- DW card: docs/brain/B187/DW-B187-QX-STOP-SNAP-01.md

## Files in scope

| File | Tickets |
|------|---------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | T1, T2 |
| `src/PropTraderTools/CopyEngine.cs` | T3 |
| `src/PropTraderTools/CopyEngineTests.cs` | T1 (4 tests) |

## Pipeline (ALL phases mandatory -- NONE skippable -- NONE combinable)

Ph1  ptt-architect       -> 02-architecture-plan.md
Ph2  ptt-plan-reviewer   -> 02-plan-review.md       (REVIEW_PASS gate)
Ph3  ptt-architect       -> 04-tickets.md
Ph3.5 ptt-ticket-reviewer -> 04-ticket-review.md    (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src .cs edits + ticket-N-completion.md
Ph4b ptt-verifier        -> ticket-N-verification.md (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> 05-final-review.md + 06-deferred-backlog.md

---

## T1 spec (ptt-architect input)

### DW-B187-QX-STOP-SNAP-01: leaderStop snapshot order fix [P1]

File: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`

**Root cause**: `leaderStop = PttQuickExit.SnapshotStopPrice(...)` is called AFTER
`CancelPttBeOrders` + `WaitForPttBeCancelled`. When BE ALL fires first, the ATM Stop1
is already cancelled by PttBreakEvenSwap's `CancelQxBrackets`. The only remaining Working
stop reference is PTT-BE-Stop-*. After `WaitForPttBeCancelled`, those are Cancelled too.
SnapshotStopPrice returns 0. SubmitStopOrder guard `if (snapshotStop <= 0) return` skips stops.

**Fix: reorder leaderStop capture to BEFORE CancelPttBeOrders in both Execute overloads.**

### Change 1 -- Execute(Instrument) (current lines ~56-65)

Current order:
```csharp
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
var targets = SnapshotTargetOrders(acc, pos.Instrument);
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
var ticks = ResolveQuickTicks(pos.Instrument);
```

Fixed order:
```csharp
// B78 DW-B63-01 + DW-B187: capture leaderStop BEFORE CancelPttBeOrders.
// PTT-BE-Stop-* are the stop reference when BE ALL has fired first; cancel destroys them.
// SnapshotTargetOrders still runs after cancel (DW-B126 race prevention maintained).
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
// B118 DW-B126: cancel PTT-BE-* BEFORE SnapshotTargetOrders to eliminate BE/QX target race.
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
// PTT-BE-* are now terminal -- target snapshot sees clean order book.
var targets = SnapshotTargetOrders(acc, pos.Instrument);
var ticks = ResolveQuickTicks(pos.Instrument);
```

### Change 2 -- Execute(List, Instrument) (current lines ~140-143)

Current order:
```csharp
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
var ticks = ResolveQuickTicks(pos.Instrument);
```

Fixed order:
```csharp
// B78 DW-B63-01 + DW-B187: capture leaderStop BEFORE CancelPttBeOrders.
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
var ticks = ResolveQuickTicks(pos.Instrument);
```

CYC: unchanged. Execute(Instrument)=8, Execute(List,Instrument)=8. No new branches.
DW-B126 compliance: SnapshotTargetOrders still called AFTER cancel.

Tests:
- T_B187_01: structural -- leaderStop declaration line precedes CancelPttBeOrders line
  in Execute(Instrument) source (Select-String line number comparison)
- T_B187_02: structural -- same check in Execute(List, Instrument)
- T_B187_03: structural -- SnapshotTargetOrders still called after CancelPttBeOrders
  in Execute(Instrument) (DW-B126 regression guard)
- T_B187_04: structural -- same DW-B126 check in Execute(List, Instrument)

---

## T2 spec (ptt-architect input)

### DW-B186-DIAG-CLEANUP-01: Remove [DW-B115-DIAG] log blocks [P3]

File: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`

Prerequisite met: B186 instrument scope filter resolves the primary vector for the
DW-B115 qty-split discrepancy. The DIAG scaffolding is no longer needed.

**Removals** (pure deletion, no new code):

1. In `Execute(Instrument)` -- remove 2 lines (~line 76-78):
   ```csharp
   // DW-B115-DIAG: log leader per-target qty split for comparison against followers.
   // Remove when DW-B115 root cause confirmed and fix applied.
   LogLeaderDiag(acc, targets, pos.Quantity);
   ```

2. In `ExecuteFollowers` -- remove the entire DIAG block (~lines 212-244):
   ```csharp
   int _fPosQty = 0;
   foreach (NinjaTrader.Cbi.Position _p in follower.Positions) // (4)
   {
       if (_p != null && _p.Instrument != null
           && _p.Instrument.FullName == pos.Instrument.FullName) // (5)
       {
           _fPosQty = _p.Quantity;
           break;
       }
   }
   {
       var _sb = new System.Text.StringBuilder("[DW-B115-DIAG] follower targets: ");
       _sb.Append(follower.Name);
       _sb.Append(" count=");
       _sb.Append(followerTargets.Count);
       _sb.Append(" posQty=");
       _sb.Append(_fPosQty);
       for (int _i = 0; _i < followerTargets.Count; _i++) // (6)
       {
           _sb.Append(" T");
           _sb.Append(_i + 1);
           _sb.Append("=");
           _sb.Append(followerTargets[_i].Qty);
       }
       NinjaTrader.Code.Output.Process(
           _sb.ToString(),
           NinjaTrader.NinjaScript.PrintTo.OutputTab1);
   }
   ```
   Also remove the DW-B115-DIAG comment block above it (~lines 208-211):
   ```csharp
   // DW-B115-DIAG: log follower position qty + per-target qty split.
   // Determines: (A) whether follower ATM has same qty split as leader,
   // or (B) whether followerTargets is empty/partial (DW-B120 async lag).
   // Remove when DW-B115 root cause confirmed and fix applied.
   ```

3. Remove the `LogLeaderDiag` private static method entirely (~lines 496-519):
   ```csharp
   private static void LogLeaderDiag(
       NinjaTrader.Cbi.Account acc,
       System.Collections.Generic.List<(double Price, int Qty)> targets,
       int posQty)
   { ... }
   ```

4. Update `ExecuteFollowers` summary comment:
   - Was: `CYC=7: rule null-check(1), follower foreach(2), follower null continue(3),
     follower position foreach DIAG(4), DIAG _p null/instr guard(5), DIAG for-loop(6), delegate(7).`
   - Becomes: `CYC=4: rule null-check(1), follower foreach(2), follower null continue(3), delegate(4).`

CYC: Execute(Instrument) stays 8 (LogLeaderDiag call was not a branch).
     ExecuteFollowers: 7 -> 4 (removes DIAG branches 4, 5, 6).
Tests: 0 new tests. Pure deletion; no behavioral change.

---

## T3 spec (ptt-architect input)

### DW-B186-WARN-01: Suppress CS0067 PendingBeArmed warning [P3]

File: `src/PropTraderTools/CopyEngine.cs`

Current (line ~423):
```csharp
internal event Action<string, string> PendingBeArmed; // HOTFIX-BEALL-SYNC-01: instr, accName
```

After:
```csharp
#pragma warning disable CS0067  // DW-B186-WARN-01: Roslyn false positive -- event IS subscribed by TradeCopierPanel
internal event Action<string, string> PendingBeArmed; // HOTFIX-BEALL-SYNC-01: instr, accName
#pragma warning restore CS0067
```

CYC: 0 impact (pragma is not code).
Tests: 0 new tests. AC: dotnet build output contains 0 CS0067 warnings.

---

## DNA constraints (ALL tickets)

- JS-021: no lock()
- JS-001: no throw new in hot paths
- JS-002: no return null
- JS-033: no async void
- ASCII-only in all new string literals
- CYC <= 8 for all modified methods

## Validation checklist (ptt-verifier Ph4b, per ticket)

```powershell
# SCAN-01: no new lock()
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "lock\("

# SCAN-02: no new return null
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "return null"

# SCAN-03: no new async void
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "async void "

# SCAN-04: CYC manual count
# Execute(Instrument): 8 branches (T1 reorder -- count unchanged)
# Execute(List,Instrument): 8 branches (T1 reorder -- count unchanged)
# ExecuteFollowers: 4 branches (T2 removes DIAG branches 4,5,6)

# SCAN-05: no non-ASCII
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "[^\x00-\x7F]"

# SCAN-06: no throw new
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "throw new"

# SCAN-07: build (0 errors, 0 warnings after T3)
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 10

# T1 AC checks
Select-String -Path src/PropTraderTools/Features/PttGlobalQuickExit.cs -Pattern "SnapshotStopPrice" | Select-Object LineNumber
Select-String -Path src/PropTraderTools/Features/PttGlobalQuickExit.cs -Pattern "CancelPttBeOrders" | Select-Object LineNumber
# leaderStop line number must be LESS THAN CancelPttBeOrders line number in both overloads

# T2 AC checks
Select-String -Path src/PropTraderTools/Features/PttGlobalQuickExit.cs -Pattern "LogLeaderDiag"
# expected: 0 results
Select-String -Path src/PropTraderTools/Features/PttGlobalQuickExit.cs -Pattern "DW-B115-DIAG"
# expected: 0 results

# T3 AC check
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-String "CS0067"
# expected: 0 results

# Tests
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "T_B187_"

# Guardian + ptt-sync
powershell -File .\scripts\verify_critical_symbols.ps1
powershell -File .\scripts\ptt-sync.ps1
```

Expected: NT8 build tag `PTT-COPIER B187 | qx-stop-snap-fix | {date}`

## DW items closed by B187

| DW ID | Closed by |
|-------|-----------|
| DW-B187-QX-STOP-SNAP-01 | T1 |
| DW-B186-DIAG-CLEANUP-01 | T2 |
| DW-B186-WARN-01 + DW-B184-04 | T3 |

---
*Written: 2026-07-08*
*Baseline: 9e5d949f*
