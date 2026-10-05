# B188 Orchestrator Prompt
# Paste into ptt-orchestrator session to stamp the direct-repair pipeline.

$execute-pipeline | PTT-COPIER B188 | leaderStop Forward Fix (Direct-Repair Stamp)

## Context

B187 post-deploy NT8 validation revealed a P1 residual bug: when BE ALL fires before
QAll2t, PTT-QX-Stop orders are not submitted (stop resolved: 0).

Root cause: B187 correctly moved leaderStop capture BEFORE CancelPttBeOrders, but the
captured value was forwarded to ExecuteFollowers only -- NOT to the leader's own
ExecuteOne call. ExecuteOne defaulted leaderStop=0. PttQuickExit.Execute then called
ResolveStop(0, 0) = 0 and SubmitStopOrder silently skipped all stops.

Director-approved direct repair was applied and verified working in NT8.
Ph1 through Ph4a are COMPLETE (Director approval + direct edit + NT8 confirmation).
This pipeline resumes at Ph4b (formal verifier stamp) then Ph5 (final review).

## Baseline

- Commit: ec99eb4f (B187-T1/T2/T3 -- last clean commit, NT8 compile verified)
- Repair commit: working tree (uncommitted -- one file changed, 2 lines)
- Spec: c:/WSGTA/universal-or-strategy-director/specs/002-trade-copier-spec.html
- Brain dir: docs/brain/B188/
- DW card: docs/brain/B188/DW-B188-LEADER-STOP-FORWARD-01.md

## Files in scope

| File | Change |
|------|--------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | 2-line edit (lines 94 and 175) |
| `src/PropTraderTools/CopyEngineTests.cs` | 4 new structural [Fact] tests (T_B188_01..04) |

## The repair (already applied to working tree)

### Line 94 -- Execute(Instrument) overload

```csharp
// BEFORE:
ExecuteOne(acc, pos.Instrument, ticks.t1, targets);

// AFTER:
ExecuteOne(acc, pos.Instrument, ticks.t1, targets, leaderStop: leaderStop);
```

### Line 175 -- Execute(List, Instrument) overload

```csharp
// BEFORE:
ExecuteOne(acc, pos.Instrument, ticks.t1, forcedTargets);

// AFTER:
ExecuteOne(acc, pos.Instrument, ticks.t1, forcedTargets, leaderStop: leaderStop);
```

## Acceptance Criteria

| AC | Condition |
|----|-----------|
| AC1 | Line 94: `ExecuteOne` call includes `leaderStop: leaderStop` named argument |
| AC2 | Line 175: `ExecuteOne` call includes `leaderStop: leaderStop` named argument |
| AC3 | Both ExecuteFollowers calls still receive `leaderStop` (DW-B63-01 unchanged) |
| AC4 | `dotnet build` 0 errors, 0 new warnings |
| AC5 | `ptt-sync.ps1` 19 linked, 0 errors |
| AC6 | 4 new structural [Fact] tests T_B188_01..04 present and passing |
| DNA | SCAN-01..06: 0 new violations; CYC Execute(Instrument)=8, Execute(List,Instrument)=8 |

## Tests to write (Ph4a already done -- engineer writes tests in CopyEngineTests.cs)

NOTE: The 4 structural tests below were NOT written in the direct repair.
The ptt-engineer (Ph4a role, executed here by the verifier's test-write sub-step)
MUST add these 4 tests to CopyEngineTests.cs before Ph4b verification can proceed.

| Test ID | Description | Pattern |
|---------|-------------|---------|
| T_B188_01 | Execute(List,Instrument) line 175: contains `leaderStop: leaderStop` | Read source, find `ExecuteOne(acc, pos.Instrument, ticks.t1, forcedTargets, leaderStop: leaderStop)` |
| T_B188_02 | Execute(Instrument) line 94: contains `leaderStop: leaderStop` | Read source, find `ExecuteOne(acc, pos.Instrument, ticks.t1, targets, leaderStop: leaderStop)` |
| T_B188_03 | Execute(List,Instrument): ExecuteFollowers call still has leaderStop arg | Find `ExecuteFollowers(acc, pos, forcedTargets, ticks, leaderStop)` on line 176 |
| T_B188_04 | Execute(Instrument): ExecuteFollowers call still has leaderStop arg | Find `ExecuteFollowers(acc, pos, targets, ticks, leaderStop)` on line 96 |

All 4 use [CallerFilePath] + GetQxSourcePath() helper (same pattern as T_B187_01..04 in CopyEngineTests.cs).
Skip lines starting with `//` to prevent false matches on comment text.

## Pipeline (ABBREVIATED -- Ph1 through Ph4a Director-approved and complete)

Ph1  ptt-architect       -> SKIPPED (Director-approved direct repair)
Ph2  ptt-plan-reviewer   -> SKIPPED (Director-approved direct repair)
Ph3  ptt-architect       -> SKIPPED (Director-approved direct repair)
Ph3.5 ptt-ticket-reviewer -> SKIPPED (Director-approved direct repair)
Ph4a ptt-engineer        -> COMPLETE (direct edit applied; tests NOT yet written -- write them first)
Ph4b ptt-verifier        -> ticket-1-verification.md (VERIFY_PASS gate) <-- START HERE
Ph5  ptt-plan-reviewer   -> 05-final-review.md + 06-deferred-backlog.md

## Ph4b ptt-verifier instructions

You are ptt-verifier. Produce `docs/brain/B188/ticket-1-verification.md`.

### Step 0: Write the 4 tests first
Before verifying, add T_B188_01..04 to `src/PropTraderTools/CopyEngineTests.cs`.
Use [CallerFilePath] + GetQxSourcePath() exactly as T_B187_01..04 do.
Run `dotnet test ... --filter "T_B188_"` and confirm 4/4 pass.

### Step 1: Confirm the two-line edit

```powershell
# AC1: line 175 has leaderStop: leaderStop
Select-String -Path "src/PropTraderTools/Features/PttGlobalQuickExit.cs" `
    -Pattern "ExecuteOne\(acc, pos\.Instrument, ticks\.t1, forcedTargets, leaderStop: leaderStop\)"

# AC2: line 94 has leaderStop: leaderStop
Select-String -Path "src/PropTraderTools/Features/PttGlobalQuickExit.cs" `
    -Pattern "ExecuteOne\(acc, pos\.Instrument, ticks\.t1, targets, leaderStop: leaderStop\)"

# AC3: ExecuteFollowers still wired (both lines)
Select-String -Path "src/PropTraderTools/Features/PttGlobalQuickExit.cs" `
    -Pattern "ExecuteFollowers\(acc, pos,"
```

### Step 2: 7-scan DNA check (against working-tree diff)

```powershell
# SCAN-01: No new lock()
git diff HEAD -- src/PropTraderTools/Features/PttGlobalQuickExit.cs | Select-String "^\+" | Select-String "lock\("

# SCAN-02: No new return null
git diff HEAD -- src/PropTraderTools/Features/PttGlobalQuickExit.cs | Select-String "^\+" | Select-String "return null"

# SCAN-03: No new async void
git diff HEAD -- src/PropTraderTools/Features/PttGlobalQuickExit.cs | Select-String "^\+" | Select-String "async void "

# SCAN-04: CYC manual count
# Execute(Instrument): combined gate(1), acc loop(2), follower guard(3), pos loop(4),
#   null/flat continue(5), instrument filter(6), NeedsLeaderFallbackFlatten guard(7), ExecuteFollowers(8) = CYC=8
# Execute(List,Instrument): same 8 decision points = CYC=8
# No new branches added -- leaderStop: leaderStop is a named argument, not a branch.

# SCAN-05: No new non-ASCII
git diff HEAD -- src/PropTraderTools/Features/PttGlobalQuickExit.cs | Select-String "^\+" | Select-String "[^\x00-\x7F]"

# SCAN-06: No new throw new
git diff HEAD -- src/PropTraderTools/Features/PttGlobalQuickExit.cs | Select-String "^\+" | Select-String "throw new"

# SCAN-07: dotnet build
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 10
```

### Step 3: Guardian + PTT sync

```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
powershell -File .\scripts\ptt-sync.ps1
```

### Step 4: Test run

```powershell
dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "T_B188_" --no-build 2>&1 | Select-Object -Last 15
```

Issue VERIFY_PASS only when all AC checks and all 7 scans pass.

## Ph5 ptt-plan-reviewer instructions

You are ptt-plan-reviewer. After VERIFY_PASS, produce:
- `docs/brain/B188/05-final-review.md`
- `docs/brain/B188/06-deferred-backlog.md`

Section K of 05-final-review.md MUST include:
- All carry-forward open DW items from B187 (DW-B187-01, DW-B187-02, DW-B183-05,
  DW-B183-03, DW-B185-BE-ALL-SCOPE-01, DW-B183-01, DW-B183-02)
- DW-B188-LEADER-STOP-FORWARD-01 marked CLOSED by this ticket
- Any new items surfaced during verification

Issue FINAL_PASS only when all sections complete and 06-deferred-backlog.md is written.

---

*Handoff written: 2026-07-08*
*Baseline: ec99eb4f (B187-T1/T2/T3)*
*Direct repair: working tree (2 lines, PttGlobalQuickExit.cs)*
*Full pipeline spec: docs/brain/B188/00-orchestrator-prompt.md*
