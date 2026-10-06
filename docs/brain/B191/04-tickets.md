# B191 Engineer Tickets
# QAll2t Per-Account Qty Fix (formalize) + DW-B187-01 Close

**Epic**: B191
**Author**: ptt-architect
**Phase**: 3 (Ticket Generation)
**Plan source**: docs/brain/B191/02-architecture-plan.md (REVIEW_PASS)
**Review source**: docs/brain/B191/02-plan-review.md (REVIEW_PASS — zero violations)
**Spec requirements**: Orchestrator prompt (docs/brain/B191/00-orchestrator-prompt.md)

---

## PIPELINE ORDER (MANDATORY — sequential, no parallel .cs edits)

```
T1 (read-only verification) -> T2 (new test file) -> T3 (.cs edit on top of T1 commit)
```

T1 and T3 both touch `PttGlobalQuickExit.cs`. SINGLE-PIPELINE protocol: T3 must apply
on top of the already-committed T1 (commit 365b8779). T2 is independent and may run
before or after T3, but T3 must not begin until T1 completion note is written.

---

## T1 — Formalize B191-DIRECT (code already committed at 365b8779)

### Spec Requirements Satisfied

- DW-B191-QTY-01 closure (P0 — root cause of Oct 6 live incident)
- Confirms `perAccTargets = TradeCopierPanel.Build2TargetList(pos.Quantity)` is live
- Confirms `forcedTargets` retained only for guard check and diagnostic log

### File Affected

```
src/PropTraderTools/Features/PttGlobalQuickExit.cs  (READ-ONLY — no .cs write permitted)
```

### Engineer Action

**No .cs edit.** Code is already committed at 365b8779. T1 is a verification-only ticket.
The engineer MUST NOT modify `PttGlobalQuickExit.cs` for this ticket.

Steps:
1. Run each SCAN check below.
2. Write `docs/brain/B191/ticket-1-completion.md` recording all SCAN results.

### Method Signatures (context only — DO NOT EDIT)

```csharp
// PttGlobalQuickExit.cs:114  (unchanged — no edit)
internal void Execute(
    System.Collections.Generic.List<(double Price, int Qty)> forcedTargets,
    Instrument instrument
)
// CYC = 8 (JS-066 ceiling, unchanged after T1)
```

### JS Rule Constraints

| Rule | Constraint | Applies To |
|------|-----------|------------|
| JS-021 | lock() BANNED | PttGlobalQuickExit.cs — confirmed zero occurrences |
| JS-033 | async void BANNED | PttGlobalQuickExit.cs — confirmed zero occurrences |
| JS-066 | CYC <= 8 | Execute() CYC = 8 (ceiling, unchanged — T1 adds 0 branches) |
| JS-081 | ASCII-only identifiers | `perAccTargets` — ASCII. PASS. |

### DW Closed

```
DW-B191-QTY-01 (NEW, P0)
Root cause: forcedTargets (built from leader panel qty) was passed verbatim to all
accounts in the Execute() per-account loop. Accounts with smaller positions (e.g.
PA-01 at 5c) received targets sized for the leader (e.g. PAAPEX10 at 20c).
Fix: perAccTargets = Build2TargetList(pos.Quantity) per iteration (commit 365b8779).
```

### xUnit Tests

None for T1. T2 provides arithmetic coverage of `Build2TargetList`.

### 7-Scan Checklist

```
SCAN-01: Select-String "lock\(" src/PropTraderTools/Features/PttGlobalQuickExit.cs
         Expected: 0 results.

SCAN-02: Select-String "async void " src/PropTraderTools/Features/PttGlobalQuickExit.cs
         Expected: 0 results.

SCAN-03: git show 365b8779 -- src/PropTraderTools/Features/PttGlobalQuickExit.cs |
         Select-String "perAccTargets"
         Expected: line ~180 shows:
           var perAccTargets = TradeCopierPanel.Build2TargetList(pos.Quantity);
         AND line ~182 shows:
           ExecuteFollowers(acc, pos, perAccTargets, ticks, leaderStop);
         AND forcedTargets still present at an earlier guard line (Count check)
         AND forcedTargets still present at an earlier diagnostic log line.
         Expected: all 4 patterns present, non-zero match count.

SCAN-04: dotnet build src/PropTraderTools/PropTraderTools.csproj
         Expected: 0 errors, 0 warnings.

SCAN-05: Execute() CYC = 8 confirmed (no new branches introduced by T1).
         Verify by reviewing the committed diff: single variable assignment statement,
         no if/else/while/for/switch/catch/||/&& added.
         Expected: CYC delta = 0. CYC after T1 = 8.

SCAN-06: dotnet test src/PropTraderTools/PropTraderTools.csproj
         --filter "FullyQualifiedName~B116|FullyQualifiedName~B120|
                   FullyQualifiedName~B123|FullyQualifiedName~B128"
         Expected: all pass, 0 failed, 0 skipped.

SCAN-07: powershell -File .\scripts\ptt-sync.ps1
         Expected: 19 linked, 0 errors.
```

### Engineer Output

```
docs/brain/B191/ticket-1-completion.md
```

Content must include:
- SCAN-01 through SCAN-07 results (PASS/FAIL for each)
- Confirmation of the exact lines found at SCAN-03 (perAccTargets and ExecuteFollowers calls)
- Build output summary (errors/warnings count)
- No .cs file path listed in "files written" section (read-only ticket)

---

## T2 — B191Tests.cs (new xUnit test file)

### Spec Requirements Satisfied

- Test coverage for `Build2TargetList` arithmetic (pure unit tests, no NT8 runtime)
- Structural verification that `Execute(List, Instrument)` overload exists as `internal`
- Boundary case coverage: single-contract position (`Build2TargetList(1)`)
- Sum invariant verification: no qty lost across the ceiling-split

### File Affected

```
src/PropTraderTools/Tests/B191Tests.cs  (NEW — create this file)
```

### Engineer Action

Create `src/PropTraderTools/Tests/B191Tests.cs` with exactly 5 `[Fact]` tests.
No other files are modified by T2.

### Method Signatures to Implement (test class)

```csharp
namespace PropTraderTools.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Xunit;
    // No NinjaTrader.* using directives permitted in this file.

    public class B191Tests
    {
        [Fact]
        public void Build2TargetList_Qty5_ReturnsT1_3_T2_2()

        [Fact]
        public void Build2TargetList_Qty20_ReturnsT1_10_T2_10()

        [Fact]
        public void Build2TargetList_Qty5_SumEqualsInput()

        [Fact]
        public void Execute_ListInstrument_Overload_ExistsAsInternal()

        [Fact]
        public void Build2TargetList_Qty1_ReturnsT1_1_T2_0()
    }
}
```

### Test Oracles (exact expected values — engineer MUST NOT deviate)

| Test Method | Input | Expected | Formula Basis |
|-------------|-------|----------|---------------|
| `Build2TargetList_Qty5_ReturnsT1_3_T2_2` | `Build2TargetList(5)` | first.Qty=3, second.Qty=2 | `(5+1)/2=3`; `5-3=2` (integer ceiling-split) |
| `Build2TargetList_Qty20_ReturnsT1_10_T2_10` | `Build2TargetList(20)` | first.Qty=10, second.Qty=10 | `(20+1)/2=10` (integer division); `20-10=10` |
| `Build2TargetList_Qty5_SumEqualsInput` | `Build2TargetList(5).Sum(t => t.Qty)` | `== 5` | sum invariant: t1+t2 == totalQty always |
| `Execute_ListInstrument_Overload_ExistsAsInternal` | `typeof(PttGlobalQuickExit).GetMethod("Execute", BindingFlags.NonPublic \| BindingFlags.Instance, ...)` | `mi != null` | overload must exist and not be accidentally renamed/removed |
| `Build2TargetList_Qty1_ReturnsT1_1_T2_0` | `Build2TargetList(1)` | first.Qty=1, second.Qty=0 | `(1+1)/2=1`; `1-1=0` (boundary: single-contract) |

### T2-4 Reflection Test Implementation Guidance

```csharp
// Parameter types for the Execute(List<(double, int)>, Instrument) overload:
var paramTypes = new Type[]
{
    typeof(System.Collections.Generic.List<(double Price, int Qty)>),
    typeof(NinjaTrader.Cbi.Instrument)   // type reference only — no instantiation
};
var mi = typeof(PttGlobalQuickExit).GetMethod(
    "Execute",
    BindingFlags.NonPublic | BindingFlags.Instance,
    null,
    paramTypes,
    null
);
Assert.NotNull(mi);
```

Note: `typeof(NinjaTrader.Cbi.Instrument)` is a type reference, not a runtime object.
This does NOT require the NT8 runtime to be loaded. The type must be resolvable from the
assembly reference. If the test runner cannot resolve NT8 types, substitute with a
`null` binder and use method name + parameter count check as the assertion fallback:
```csharp
var methods = typeof(PttGlobalQuickExit)
    .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
    .Where(m => m.Name == "Execute" && m.GetParameters().Length == 2);
Assert.Single(methods);
```

### JS Rule Constraints

| Rule | Constraint | Applies To |
|------|-----------|------------|
| JS-021 | lock() BANNED | B191Tests.cs — must have 0 occurrences |
| JS-033 | async void BANNED | B191Tests.cs — no async void test methods |
| JS-066 | CYC <= 8 | All 5 test methods are trivially CYC=1 (straight-line Assert calls) |
| JS-081 | ASCII-only identifiers and strings | All test method names, Assert messages — ASCII only |
| xUnit mandate | [Fact] only, no [Theory] with domain data, no [Fact(Skip=...)] | All 5 methods — no Skip attribute |

### DW Closed

None. T2 provides coverage only. DW items are closed by T1 and T3.

### Run Command

```powershell
dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "FullyQualifiedName~B191"
```

Expected output:
```
Passed: 5   Failed: 0   Skipped: 0
```

### 7-Scan Checklist

```
SCAN-01: (Get-Content src/PropTraderTools/Tests/B191Tests.cs | Select-String "\[Fact\]").Count
         Expected: 5  (exactly 5 [Fact] attributes present)
         AND: Select-String "Skip" B191Tests.cs -> 0 results (no Skip attribute on any test)

SCAN-02: Select-String "NinjaTrader\." src/PropTraderTools/Tests/B191Tests.cs
         Expected: 0 results (no NT8 runtime namespace imports or usage)
         EXCEPTION: typeof(NinjaTrader.Cbi.Instrument) in T2-4 is permitted if the
         engineer chooses the type-reference form. If the fallback (parameter count)
         form is used, this scan must return 0 unconditionally.

SCAN-03: dotnet test src/PropTraderTools/PropTraderTools.csproj
         --filter "FullyQualifiedName~B191"
         Expected: 5 passed, 0 failed, 0 skipped.

SCAN-04: dotnet test output for Build2TargetList_Qty5_ReturnsT1_3_T2_2 — PASS
         Confirms Build2TargetList(5) first.Qty=3, second.Qty=2.

SCAN-05: dotnet test output for Build2TargetList_Qty1_ReturnsT1_1_T2_0 — PASS
         Confirms boundary case: Build2TargetList(1) first.Qty=1, second.Qty=0.

SCAN-06: dotnet test output for Build2TargetList_Qty5_SumEqualsInput — PASS
         Confirms sum invariant: Build2TargetList(5).Sum(t => t.Qty) == 5.

SCAN-07: Select-String "lock\(" src/PropTraderTools/Tests/B191Tests.cs
         Expected: 0 results.
```

### Engineer Output

```
src/PropTraderTools/Tests/B191Tests.cs   (new file — PRIMARY deliverable)
docs/brain/B191/ticket-2-completion.md  (completion note)
```

`ticket-2-completion.md` must include:
- SCAN-01 through SCAN-07 results (PASS/FAIL for each)
- Exact `dotnet test` output line (Passed/Failed/Skipped counts)
- File path of new test file

---

## T3 — Restore followerPosQty in ExecuteFollowers (closes DW-B187-01)

### Spec Requirements Satisfied

- Restores live follower position qty as arg 3 to `ResolveFollowerTargets`
- Closes DW-B187-01 (P3, carried from B187 via B188, B189, B190)
- Reactivates `ScaleLeaderTargets` path for follower with open position and no snapshot
- Preserves zero-fallback: flat follower continues to short-circuit correctly

### File Affected

```
src/PropTraderTools/Features/PttGlobalQuickExit.cs  (EDIT — surgical, 2 lines changed)
```

### Engineer Action

Edit `ExecuteFollowers` in `PttGlobalQuickExit.cs`. Two changes only:
1. Add one new `int followerPosQty` local variable declaration BEFORE the `ResolveFollowerTargets` call.
2. Change the third argument to `ResolveFollowerTargets` from literal `0` to `followerPosQty`.

No other lines in the file may be changed for this ticket.

### Method Signatures (unchanged — context for locating the edit site)

```csharp
// PttGlobalQuickExit.cs:194  (private, unchanged signature)
private void ExecuteFollowers(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Position pos,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    (int t1, int t2) ticks,
    double leaderStop
)
// CYC = 4 before T3, CYC = 4 after T3 (variable assignment = 0 McCabe branches)

// CopyEngine.cs:5539  (called by T3 — do not edit this method)
internal NinjaTrader.Cbi.Position FindPositionPublic(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Instrument instrument
)
```

### Exact Edit — Before (current state, lines ~216-221)

```csharp
followerTargets = ResolveFollowerTargets(
    followerTargets,
    targets,
    0,
    pos.Quantity
);
```

### Exact Edit — After (T3 target state)

```csharp
// B191-T3: restore followerPosQty from live position (DW-B187-01 fix).
// With followerPosQty>0, ScaleLeaderTargets fires when follower has no snapshot.
// Zero fallback preserved: flat follower -> ScaleLeaderTargets skipped -> empty list correct.
int followerPosQty = CopyEngine.Instance?.FindPositionPublic(follower, pos.Instrument)?.Quantity ?? 0;
followerTargets = ResolveFollowerTargets(
    followerTargets,
    targets,
    followerPosQty,
    pos.Quantity
);
```

**CRITICAL**: Use `FindPositionPublic` (internal wrapper), NOT `FindPosition` (private to
`CopyEngine` — inaccessible from `PttGlobalQuickExit`). This is an architectural correction
confirmed in the plan review (02-plan-review.md, Check 4 note on FindPositionPublic).

### JS Rule Constraints

| Rule | Constraint | Applies To |
|------|-----------|------------|
| JS-021 | lock() BANNED | New lines added — confirmed zero lock() in null-safe chain |
| JS-033 | async void BANNED | ExecuteFollowers is synchronous void (not event handler, not async) |
| JS-001 | No throw in hot path | `?.` propagates null safely; `?? 0` unwraps to int. No NullReferenceException possible. No throw introduced. |
| JS-002 | No return null at new sites | T3 adds zero new return sites. FindPositionPublic null return is pre-existing contract. |
| JS-066 | CYC <= 4 for ExecuteFollowers | `int followerPosQty = ...` is a variable assignment. `?.` and `??` are expressions, NOT McCabe branches. Delta = 0. CYC remains 4. |
| JS-081 | ASCII-only identifiers | `followerPosQty` — ASCII. PASS. New comment lines — ASCII-only. PASS. |

### CYC Verification

```
ExecuteFollowers CYC before T3: 4
Delta introduced by T3:
  - Line added: `int followerPosQty = CopyEngine.Instance?.FindPositionPublic(...)?.Quantity ?? 0;`
    This is a variable assignment (expression statement). No if/else/while/for/switch/catch/||/&&.
    The ?. and ?? operators are null-conditional EXPRESSIONS — they do NOT create McCabe branches.
    McCabe delta = 0.
  - Argument changed: `0` -> `followerPosQty` inside existing method call.
    No new statement. No new branch. McCabe delta = 0.
ExecuteFollowers CYC after T3: 4  (unchanged, well within JS-066 limit of 8)
```

### DW Closed

```
DW-B187-01 (P3, carried from B187 via B188, B189, B190)
Root cause: B187-T2 deleted the [DW-B115-DIAG] block that computed _fPosQty.
  The B187 plan incorrectly stated _fPosQty was only used within that block.
  In fact, _fPosQty was also passed as followerPosQty (arg 3) to ResolveFollowerTargets.
  The B187 engineer made the minimal fix: replaced _fPosQty with literal 0.
  With followerPosQty=0, ResolveFollowerTargets short-circuits before ScaleLeaderTargets,
  leaving ScaleLeaderTargets permanently unreachable.
Fix: int followerPosQty = CopyEngine.Instance?.FindPositionPublic(follower, pos.Instrument)
     ?.Quantity ?? 0 — sourced from the same data path used by PttBreakEvenSwap.cs:53
     and TradeCopierPanel.cs:1788.
```

### Run Commands

```powershell
# Build gate
dotnet build src/PropTraderTools/PropTraderTools.csproj
# Expected: 0 errors, 0 warnings

# Regression gate — ResolveFollowerTargets behavior tests
dotnet test src/PropTraderTools/PropTraderTools.csproj `
  --filter "FullyQualifiedName~B116|FullyQualifiedName~B120|FullyQualifiedName~B123|FullyQualifiedName~B128"
# Expected: all pass, 0 failed, 0 skipped

# Hard-link sync (mandatory after every .cs edit)
powershell -File .\scripts\ptt-sync.ps1
# Expected: 19 linked, 0 errors
```

### 7-Scan Checklist

```
SCAN-01: Select-String "lock\(" src/PropTraderTools/Features/PttGlobalQuickExit.cs
         Expected: 0 results.

SCAN-02: Select-String "async void " src/PropTraderTools/Features/PttGlobalQuickExit.cs
         Expected: 0 results.

SCAN-03: Get-Content src/PropTraderTools/Features/PttGlobalQuickExit.cs |
         Select-String "followerPosQty"
         Expected: line shows:
           int followerPosQty = CopyEngine.Instance?.FindPositionPublic(follower, pos.Instrument)?.Quantity ?? 0;
         AND a second line shows:
           ResolveFollowerTargets(followerTargets, targets, followerPosQty, pos.Quantity)
         NOT: literal 0 as arg 3 to ResolveFollowerTargets.
         NOT: FindPosition (private) — must be FindPositionPublic (internal wrapper).

SCAN-04: ExecuteFollowers CYC <= 4 confirmed.
         Verify by reviewing the diff: one new variable assignment statement + one argument
         substitution in existing call. No if/else/switch/while/for/catch added.
         Expected: CYC delta = 0. CYC after T3 = 4.

SCAN-05: dotnet build src/PropTraderTools/PropTraderTools.csproj
         Expected: 0 errors, 0 warnings.

SCAN-06: dotnet test src/PropTraderTools/PropTraderTools.csproj
         --filter "FullyQualifiedName~B116|FullyQualifiedName~B120|
                   FullyQualifiedName~B123|FullyQualifiedName~B128"
         Expected: all pass, 0 failed, 0 skipped.

SCAN-07: powershell -File .\scripts\ptt-sync.ps1
         Expected: 19 linked, 0 errors.
```

### Engineer Output

```
src/PropTraderTools/Features/PttGlobalQuickExit.cs  (edited — PRIMARY deliverable)
docs/brain/B191/ticket-3-completion.md              (completion note)
```

`ticket-3-completion.md` must include:
- SCAN-01 through SCAN-07 results (PASS/FAIL for each)
- Exact git diff of the two changed lines (new variable declaration + argument substitution)
- Build output summary (errors/warnings count)
- Confirmation that `FindPositionPublic` (not `FindPosition`) was used
- ptt-sync output (linked count must be 19)

---

## DW Summary

| DW Item | Priority | Closed By | Ticket |
|---------|----------|-----------|--------|
| DW-B191-QTY-01 | P0 (live incident) | T1 (commit 365b8779) | T1 — verification only |
| DW-B187-01 | P3 (from B190 backlog) | T3 (ExecuteFollowers edit) | T3 — surgical edit |
| DW-B183-03 | P1 (from B190 backlog) | NOT closed by B191 | Carried forward |

---

## Completion Gate

Ph4b (ptt-verifier) may issue VERIFY_PASS only when ALL of the following are present:

```
[ ] docs/brain/B191/ticket-1-completion.md exists and all 7 SCAN results are PASS
[ ] docs/brain/B191/ticket-2-completion.md exists and all 7 SCAN results are PASS
[ ] src/PropTraderTools/Tests/B191Tests.cs exists with exactly 5 [Fact] methods
[ ] dotnet test --filter B191 -> 5 passed, 0 failed, 0 skipped (recorded in ticket-2-completion.md)
[ ] docs/brain/B191/ticket-3-completion.md exists and all 7 SCAN results are PASS
[ ] git diff for T3 shows exactly: 1 new line (followerPosQty assignment) + 1 arg substitution (0 -> followerPosQty)
[ ] FindPositionPublic (not FindPosition) confirmed in T3 edit
[ ] ptt-sync: 19 linked, 0 errors (recorded in ticket-3-completion.md)
[ ] dotnet build: 0 errors, 0 warnings (recorded in ticket-3-completion.md)
```

---

*Author: ptt-architect*
*Epic: B191*
*Phase: 3 (Ticket Generation)*
*Plan status at generation time: REVIEW_PASS*
*Preceding plan review: docs/brain/B191/02-plan-review.md*
