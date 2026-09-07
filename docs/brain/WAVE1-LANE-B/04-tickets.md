# WAVE1-LANE-B Tickets
# Phase 3 Output — ptt-architect
# Input: docs/brain/WAVE1-LANE-B/02-architecture-plan.md (REVIEW_PASS)
#        docs/brain/WAVE1-LANE-B/04-ticket-review.md (TICKET_REVIEW_FAIL — rework applied)
# Rework: T-1 and T-3 converted to VERIFICATION-ONLY per reviewer mandate.
# All 5 tickets are now VERIFICATION-ONLY (no code changes in any ticket).
# Date: 2026-08

---

## AUTHORITATIVE CCN BASELINE (from 02-plan-review.md)

| File | Method | Lizard CCN | Status |
|------|--------|------------|--------|
| PttBreakEven.cs | Execute | 8 | AT-LIMIT, COMPLIANT |
| PttBreakEven.cs | CancelStaleBracketsLocal | 8 | AT-LIMIT, COMPLIANT |
| PttGlobalQuickExit.cs | Execute (no-arg) | 7 | COMPLIANT |
| PttGlobalQuickExit.cs | Execute (List overload) | 8 | AT-LIMIT, COMPLIANT |
| PttGlobalQuickExit.cs | ExecuteFollowers | 8 | AT-LIMIT, COMPLIANT |
| PttGlobalQuickExit.cs | SnapshotTargetOrders | 8 | AT-LIMIT, COMPLIANT |
| All other methods, all 5 files | — | <=8 | COMPLIANT |

**Conclusion: Methods CCN > 8: NONE. All 5 tickets are VERIFICATION-ONLY.**

CCN Decision Rule applied: IF all methods in a ticket are CCN <= 8 (lizard-confirmed) → Engineering
work is UNJUSTIFIED. Modifying live-trading code with zero CCN benefit = regression risk, BANNED.

---

## T-1 — PttBreakEven.cs (Verification Only)

### Ticket ID
T-1

### File
`src/PropTraderTools/Features/PttBreakEven.cs`

### Spec Requirements Covered
- B-01: `PttBreakEven.SubmitBePair` — verified CCN = 5 (COMPLIANT)
- B-06: `PttBreakEven.SubmitBeStopLocal` — verified CCN = 7 (COMPLIANT)
- All other methods in file: verified CCN <= 8

### Methods in Scope
- `Execute` — CCN 8 (AT-LIMIT, COMPLIANT)
- `ExecuteOneAccount` — CCN 7
- `SubmitBeStopLocal` — CCN 7
- `SubmitBePair` — CCN 5
- `SubmitBareStop` — CCN 5
- `SubmitBeTargetsLocal` — CCN 7
- `SnapshotTargetsLocal` — CCN 7
- `CancelStaleBracketsLocal` — CCN 8 (AT-LIMIT, COMPLIANT)
- `FindPositionLocal` — CCN 5
- `RaiseBeNotify` — CCN 2
- `BuildBeRejectMsg` — CCN 3

### Engineering Work
NONE — All methods CCN <= 8 (lizard verified). Engineering in live-trading code with no CCN
benefit is BANNED (regression risk to BE-ALL button + Clone mode, primary trading workflow).

### Verification Steps
a. Read `src/PropTraderTools/Features/PttBreakEven.cs` — confirm no regressions since baseline.
   Spot-check: `Execute` retains the original body (no new helpers inserted); `CancelStaleBracketsLocal`
   retains its lambda `stale.RemoveAll(o => ...)` pattern unchanged.
b. Run lizard and confirm all methods CCN <= 8:
   `lizard src/PropTraderTools/Features/PttBreakEven.cs --csv`
   Expected: every row in col 1 (CCN) is <= 8. If any row > 8, treat as blocking defect and escalate.
c. Run build:
   `dotnet build src/PropTraderTools/PropTraderTools.csproj`
   Expected: 0 errors, 0 new warnings.
d. Run tests:
   `dotnet test tests/PropTraderTools.Tests/`
   Expected: >= 117 pass, 0 fail.

### 7-SCAN CHECKLIST

SCAN-01: P0 grep — lock() check
  `grep -n "lock(" src/PropTraderTools/Features/PttBreakEven.cs`
  Expected result: document all hits. Confirm zero actual `lock(` statements (comment-only hits are
  acceptable; actual lock() usage = P0 BLOCKER, stop and report).

SCAN-02: async void check
  `grep -n "async void " src/PropTraderTools/Features/PttBreakEven.cs`
  Expected result: zero hits.

SCAN-03: return null check
  `grep -n "return null;" src/PropTraderTools/Features/PttBreakEven.cs`
  Expected result: document all hits. All methods return void, bool, int, or initialized collections;
  confirm no naked null return in a non-nullable context.

SCAN-04: lizard CCN — all methods <= 8
  `lizard src/PropTraderTools/Features/PttBreakEven.cs --csv`
  Expected result: all rows in CCN column <= 8. Execute and CancelStaleBracketsLocal expected at
  exactly 8 (AT-LIMIT). Any row > 8 = blocking defect.

SCAN-05: build — 0 errors
  `dotnet build src/PropTraderTools/PropTraderTools.csproj`
  Expected result: Build succeeded. 0 Error(s).

SCAN-06: test — >= 117 pass, 0 fail
  `dotnet test tests/PropTraderTools.Tests/`
  Expected result: >= 117 passed, 0 failed.

SCAN-07: sync verification
  `powershell -File scripts\ptt-sync-and-verify.ps1`
  Expected result: 0 MISMATCH lines.

### xUnit Test (Existing)
Since this ticket makes no code changes, point to existing tests that already cover this file.

```csharp
// Existing test — verify coverage is intact (do not add new tests for helpers that were not added)
[Fact]
public void SubmitBePair_WhenOrderIsNull_DoesNotThrow()
    // Asserts: acc.CreateOrder returns null → no exception thrown, method returns gracefully.

[Fact]
public void SubmitBeStopLocal_WhenPositionIsFlat_SkipsSubmit()
    // Asserts: pos.Quantity == 0 → method returns without calling CreateOrder.
```

These tests exercise the core guard paths of B-01 and B-06 respectively.
If these tests do not exist in the current suite, add them as the verification deliverable.

### Acceptance Criterion
`lizard src/PropTraderTools/Features/PttBreakEven.cs --csv` confirms all methods CCN <= 8.
`PttBreakEven.cs` is NOT in `git diff --name-only` (no changes made).

### Lane Isolation
This ticket makes ZERO edits to CopyEngine.cs.

### Completion Artifact
`docs/brain/WAVE1-LANE-B/ticket-1-completion.md`

---

## T-2 — PttBreakEvenSwap.cs (Verification Only)

### Ticket ID
T-2

### File
`src/PropTraderTools/Features/PttBreakEvenSwap.cs`

### Spec Requirements Covered
- B-02: `PttBreakEvenSwap.SubmitSwapPair` — verified CCN = 4 (COMPLIANT)
- B-08: `PttBreakEvenSwap.SubmitBareStopSwap` — verified CCN = 4 (COMPLIANT)
- All other methods in file: verified CCN <= 8

### Methods in Scope
- `Execute` — CCN 8 (AT-LIMIT, COMPLIANT)
- `SubmitSwapPair` — CCN 4
- `SubmitBareStopSwap` — CCN 4

### Engineering Work
NONE — All methods CCN <= 8 (lizard verified). AT-LIMIT on Execute is COMPLIANT per standard.

### Verification Steps
a. Read `src/PropTraderTools/Features/PttBreakEvenSwap.cs` — confirm no regressions since baseline.
b. Run lizard and confirm all methods CCN <= 8:
   `lizard src/PropTraderTools/Features/PttBreakEvenSwap.cs --csv`
   Expected: every row in col 1 (CCN) is <= 8. If any row > 8, treat as blocking defect and escalate.
c. Run build:
   `dotnet build src/PropTraderTools/PropTraderTools.csproj`
   Expected: 0 errors, 0 new warnings.
d. Run tests:
   `dotnet test tests/PropTraderTools.Tests/`
   Expected: >= 117 pass, 0 fail.

### 7-SCAN CHECKLIST

SCAN-01: P0 grep — lock() check
  `grep -n "lock(" src/PropTraderTools/Features/PttBreakEvenSwap.cs`
  Expected result: document all hits. Zero actual `lock(` statements.

SCAN-02: async void check
  `grep -n "async void " src/PropTraderTools/Features/PttBreakEvenSwap.cs`
  Expected result: zero hits.

SCAN-03: return null check
  `grep -n "return null;" src/PropTraderTools/Features/PttBreakEvenSwap.cs`
  Expected result: document all hits. Confirm no naked null return in non-nullable context.

SCAN-04: lizard CCN — all methods <= 8
  `lizard src/PropTraderTools/Features/PttBreakEvenSwap.cs --csv`
  Expected result: all rows in CCN column <= 8. Execute expected at exactly 8 (AT-LIMIT).
  Any row > 8 = blocking defect.

SCAN-05: build — 0 errors
  `dotnet build src/PropTraderTools/PropTraderTools.csproj`
  Expected result: Build succeeded. 0 Error(s).

SCAN-06: test — >= 117 pass, 0 fail
  `dotnet test tests/PropTraderTools.Tests/`
  Expected result: >= 117 passed, 0 failed.

SCAN-07: sync verification
  `powershell -File scripts\ptt-sync-and-verify.ps1`
  Expected result: 0 MISMATCH lines.

### xUnit Test (Existing)
Since this ticket makes no code changes, point to existing tests that already cover this file.

```csharp
[Fact]
public void SubmitBareStopSwap_WhenPriceNotSubmittable_LogsAndSkips()
    // Asserts: IsStopPriceSubmittable returns false → no CreateOrder call, warning logged.

[Fact]
public void SubmitSwapPair_WhenPriceNotSubmittable_SkipsStop_SubmitsTarget()
    // Asserts: stop-price guard fires → stop order skipped; target order submission proceeds.
```

If these tests do not exist in the current suite, add them as the verification deliverable.

### Acceptance Criterion
`lizard src/PropTraderTools/Features/PttBreakEvenSwap.cs --csv` confirms all methods CCN <= 8.
`PttBreakEvenSwap.cs` is NOT in `git diff --name-only` (no changes made).

### Lane Isolation
This ticket makes ZERO edits to CopyEngine.cs.

### Completion Artifact
`docs/brain/WAVE1-LANE-B/ticket-2-completion.md`

---

## T-3 — PttGlobalQuickExit.cs (Verification Only)

### Ticket ID
T-3

### File
`src/PropTraderTools/Features/PttGlobalQuickExit.cs`

### Spec Requirements Covered
- B-03: `PttGlobalQuickExit.ExecuteFollowers` — verified CCN = 8 (AT-LIMIT, COMPLIANT)
- B-04: `PttGlobalQuickExit.Execute()` — verified CCN = 7 (COMPLIANT)
         `PttGlobalQuickExit.Execute(List)` — verified CCN = 8 (AT-LIMIT, COMPLIANT)
- B-07: `PttGlobalQuickExit.ExecuteOne` — verified CCN = 2 (COMPLIANT)
- All other methods in file: verified CCN <= 8

### Methods in Scope
- `Execute` (no-arg) — CCN 7
- `Execute` (List overload) — CCN 8 (AT-LIMIT, COMPLIANT)
- `ExecuteFollowers` — CCN 8 (AT-LIMIT, COMPLIANT)
- `ExecuteOne` — CCN 2
- `SnapshotTargetOrders` — CCN 8 (AT-LIMIT, COMPLIANT)
- `WaitForPttBeCancelled` — CCN 7
- `CancelPttBeOrders` — CCN 6
- `ScaleLeaderTargets` — CCN 4
- `LogLeaderDiag` — CCN 2
- `ResolveFollowerTargets` — CCN 6
- `DeduplicateByPrice` — CCN 5
- `IsNonTerminalForInstr` — CCN 5
- `IsTargetOrder` — CCN 7
- `IsPttTargetOrder` — CCN 6

### Engineering Work
NONE — All methods CCN <= 8 (lizard verified). Engineering in live-trading code with no CCN
benefit is BANNED (regression risk to QX-ALL path, highest-scope regression risk in this lane).
Methods Execute(no-arg)=7, Execute(List)=8, ExecuteFollowers=8, SnapshotTargetOrders=8 are all
at or below the limit. No helpers are to be added.

### Verification Steps
a. Read `src/PropTraderTools/Features/PttGlobalQuickExit.cs` — confirm no regressions since baseline.
   Spot-check: `Execute`, `Execute(List)`, `ExecuteFollowers`, and `SnapshotTargetOrders` retain
   their original bodies (no new helper calls inserted).
b. Run lizard and confirm all methods CCN <= 8:
   `lizard src/PropTraderTools/Features/PttGlobalQuickExit.cs --csv`
   Expected: every row in col 1 (CCN) is <= 8. If any row > 8, treat as blocking defect and escalate.
c. Run build:
   `dotnet build src/PropTraderTools/PropTraderTools.csproj`
   Expected: 0 errors, 0 new warnings.
d. Run tests:
   `dotnet test tests/PropTraderTools.Tests/`
   Expected: >= 117 pass, 0 fail.

### 7-SCAN CHECKLIST

SCAN-01: P0 grep — lock() check
  `grep -n "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs`
  Expected result: document all hits. Zero actual `lock(` statements.

SCAN-02: async void check
  `grep -n "async void " src/PropTraderTools/Features/PttGlobalQuickExit.cs`
  Expected result: zero hits.

SCAN-03: return null check
  `grep -n "return null;" src/PropTraderTools/Features/PttGlobalQuickExit.cs`
  Expected result: document all hits. Confirm no naked null return in non-nullable context.

SCAN-04: lizard CCN — all methods <= 8
  `lizard src/PropTraderTools/Features/PttGlobalQuickExit.cs --csv`
  Expected result: all rows in CCN column <= 8. Execute(List), ExecuteFollowers, and
  SnapshotTargetOrders expected at exactly 8 (AT-LIMIT). Any row > 8 = blocking defect.

SCAN-05: build — 0 errors
  `dotnet build src/PropTraderTools/PropTraderTools.csproj`
  Expected result: Build succeeded. 0 Error(s).

SCAN-06: test — >= 117 pass, 0 fail
  `dotnet test tests/PropTraderTools.Tests/`
  Expected result: >= 117 passed, 0 failed.

SCAN-07: sync verification
  `powershell -File scripts\ptt-sync-and-verify.ps1`
  Expected result: 0 MISMATCH lines.

### xUnit Test (Existing)
Since this ticket makes no code changes, point to existing tests that already cover this file.

```csharp
[Fact]
public void ExecuteOne_WhenSkipIfFollowerFalse_ArmsQxCancelGuard()
    // Asserts: skipIfFollower=false path sets _qxCancelInProgress guard before delegating.

[Fact]
public void ExecuteFollowers_WhenFollowerIsNull_SkipsFollower()
    // Asserts: null follower entry is skipped without NullReferenceException.
```

These tests exercise the core guard paths of B-07 (ExecuteOne) and B-03 (ExecuteFollowers).
If these tests do not exist in the current suite, add them as the verification deliverable.

### Acceptance Criterion
`lizard src/PropTraderTools/Features/PttGlobalQuickExit.cs --csv` confirms all methods CCN <= 8.
`PttGlobalQuickExit.cs` is NOT in `git diff --name-only` (no changes made).

### Lane Isolation
This ticket makes ZERO edits to CopyEngine.cs.

### Completion Artifact
`docs/brain/WAVE1-LANE-B/ticket-3-completion.md`

---

## T-4 — PttQuickExit.cs (Verification Only)

### Ticket ID
T-4

### File
`src/PropTraderTools/Features/PttQuickExit.cs`

### Spec Requirements Covered
- B-05: `PttQuickExit.Execute` (main) — verified CCN = 5 (COMPLIANT)
- B-09: `PttQuickExit.SubmitStopOrder` — verified CCN = 5 (COMPLIANT)
- B-10: `PttQuickExit.SubmitTargetOrder` — verified CCN = 4 (COMPLIANT)
- All other methods in file: verified CCN <= 8
- InstrumentDefaults.GetQuickTicks (support method, same file scope): CCN = 4 (COMPLIANT)

### Methods in Scope
- `Execute` (main) — CCN 5
- `Execute` (2nd overload) — CCN 1
- `SubmitQxOcoPair` — CCN 6–7
- `SubmitStopOrder` — CCN 5
- `SubmitTargetOrder` — CCN 4
- `IsFlatOrMissing` — CCN 5
- `ComputeExitPrices` — CCN 3
- `SnapshotStopPrice` — CCN 8 (AT-LIMIT, COMPLIANT)
- `InstrumentDefaults.GetQuickTicks` — CCN 4

### Engineering Work
NONE — All methods CCN <= 8 (lizard verified). Prior WAVE2-LANE-E-1 extractions fully resolved
all CCN violations in this file. No further engineering required.

### Verification Steps
a. Read `src/PropTraderTools/Features/PttQuickExit.cs` — confirm no regressions since baseline.
b. Run lizard and confirm all methods CCN <= 8:
   `lizard src/PropTraderTools/Features/PttQuickExit.cs --csv`
   Expected: every row in col 1 (CCN) is <= 8. If any row > 8, treat as blocking defect and escalate.
c. Run build:
   `dotnet build src/PropTraderTools/PropTraderTools.csproj`
   Expected: 0 errors, 0 new warnings.
d. Run tests:
   `dotnet test tests/PropTraderTools.Tests/`
   Expected: >= 117 pass, 0 fail.

### 7-SCAN CHECKLIST

SCAN-01: P0 grep — lock() check
  `grep -n "lock(" src/PropTraderTools/Features/PttQuickExit.cs`
  Expected result: document all hits. Zero actual `lock(` statements.

SCAN-02: async void check
  `grep -n "async void " src/PropTraderTools/Features/PttQuickExit.cs`
  Expected result: zero hits.

SCAN-03: return null check
  `grep -n "return null;" src/PropTraderTools/Features/PttQuickExit.cs`
  Expected result: document all hits. Confirm no naked null return in non-nullable context.

SCAN-04: lizard CCN — all methods <= 8
  `lizard src/PropTraderTools/Features/PttQuickExit.cs --csv`
  Expected result: all rows in CCN column <= 8. SnapshotStopPrice expected at exactly 8 (AT-LIMIT).
  Any row > 8 = blocking defect.

SCAN-05: build — 0 errors
  `dotnet build src/PropTraderTools/PropTraderTools.csproj`
  Expected result: Build succeeded. 0 Error(s).

SCAN-06: test — >= 117 pass, 0 fail
  `dotnet test tests/PropTraderTools.Tests/`
  Expected result: >= 117 passed, 0 failed.

SCAN-07: sync verification
  `powershell -File scripts\ptt-sync-and-verify.ps1`
  Expected result: 0 MISMATCH lines.

### xUnit Test (Existing)
Since this ticket makes no code changes, point to existing tests that already cover this file.

```csharp
[Fact]
public void IsFlatOrMissing_NullLeader_ReturnsTrue()
    // Asserts: leader==null → flat/missing detected, returns true.

[Fact]
public void IsFlatOrMissing_ZeroQty_ReturnsTrue()
    // Asserts: position.Quantity == 0 → flat, returns true.

[Fact]
public void GetQuickTicks_MesInstrument_Returns4And8()
    // Asserts: instrument name starting with "MES" → quickTicks=4, initialStop=8.

[Fact]
public void GetQuickTicks_MgcInstrument_Returns2And4()
    // Asserts: instrument name starting with "MGC" → quickTicks=2, initialStop=4.

[Fact]
public void GetQuickTicks_NullOrEmpty_Returns4And8()
    // Asserts: null/empty instrument name → default 4 and 8.

[Fact]
public void GetQuickTicks_UnknownInstrument_Returns4And8()
    // Asserts: unrecognised symbol → default 4 and 8.
```

If these tests do not exist in the current suite, add them as the verification deliverable.

### Acceptance Criterion
`lizard src/PropTraderTools/Features/PttQuickExit.cs --csv` confirms all methods CCN <= 8.
`PttQuickExit.cs` is NOT in `git diff --name-only` (no changes made).

### Lane Isolation
This ticket makes ZERO edits to CopyEngine.cs.

### Completion Artifact
`docs/brain/WAVE1-LANE-B/ticket-4-completion.md`

---

## T-5 — PttGlobalBreakEven.cs (Verification Only)

### Ticket ID
T-5

### File
`src/PropTraderTools/Features/PttGlobalBreakEven.cs`

### Spec Requirements Covered
Supporting file — no B-XX baseline methods target this file directly.
Scope is to confirm clean CCN state for the complete WAVE1-LANE-B surface.

### Methods in Scope
- `Execute(int)` — CCN 1 (straight delegation)
- `Execute(IEnumerable)` — CCN 5
- `ExecuteOne` — CCN 4
- `IncrementBuffer` — CCN 2
- `DecrementBuffer` — CCN 2

### Engineering Work
NONE — All methods CCN <= 5 (lizard verified). Maximum CCN in this file is 5.

### Verification Steps
a. Read `src/PropTraderTools/Features/PttGlobalBreakEven.cs` — confirm no regressions since baseline.
   Note: file header comment includes `// JS-021: no lock().` — this is a compliance annotation,
   not a lock() statement. SCAN-01 must document it as comment-only (not a violation).
b. Run lizard and confirm all methods CCN <= 8:
   `lizard src/PropTraderTools/Features/PttGlobalBreakEven.cs --csv`
   Expected: every row in col 1 (CCN) is <= 5. If any row > 8, treat as blocking defect and escalate.
c. Run build:
   `dotnet build src/PropTraderTools/PropTraderTools.csproj`
   Expected: 0 errors, 0 new warnings.
d. Run tests:
   `dotnet test tests/PropTraderTools.Tests/`
   Expected: >= 117 pass, 0 fail.

### 7-SCAN CHECKLIST

SCAN-01: P0 grep — lock() check
  `grep -n "lock(" src/PropTraderTools/Features/PttGlobalBreakEven.cs`
  Expected result: document all hits. The comment `// JS-021: no lock().` on line 4 is the ONLY
  expected hit — it is a comment, not a statement. Zero actual `lock(` statements.

SCAN-02: async void check
  `grep -n "async void " src/PropTraderTools/Features/PttGlobalBreakEven.cs`
  Expected result: zero hits.

SCAN-03: return null check
  `grep -n "return null;" src/PropTraderTools/Features/PttGlobalBreakEven.cs`
  Expected result: document all hits. Confirm no naked null return in non-nullable context.

SCAN-04: lizard CCN — all methods <= 8
  `lizard src/PropTraderTools/Features/PttGlobalBreakEven.cs --csv`
  Expected result: all rows in CCN column <= 5 (this file has no AT-LIMIT methods).
  Any row > 8 = blocking defect.

SCAN-05: build — 0 errors
  `dotnet build src/PropTraderTools/PropTraderTools.csproj`
  Expected result: Build succeeded. 0 Error(s).

SCAN-06: test — >= 117 pass, 0 fail
  `dotnet test tests/PropTraderTools.Tests/`
  Expected result: >= 117 passed, 0 failed.

SCAN-07: sync verification
  `powershell -File scripts\ptt-sync-and-verify.ps1`
  Expected result: 0 MISMATCH lines.

### xUnit Test (Existing)
Since this ticket makes no code changes, point to existing tests that already cover this file.

```csharp
[Fact]
public void Execute_IntOverload_DelegatesToIEnumerableOverload()
    // Asserts: Execute(int) calls Execute(IEnumerable) with a single-element sequence. CCN=1 path.

[Fact]
public void IncrementBuffer_IncreasesBufferByOneTick()
    // Asserts: buffer value increases by the configured tick size after IncrementBuffer call.

[Fact]
public void DecrementBuffer_DecreasesBufferByOneTick()
    // Asserts: buffer value decreases by the configured tick size after DecrementBuffer call.
```

If these tests do not exist in the current suite, add them as the verification deliverable.

### Acceptance Criterion
`lizard src/PropTraderTools/Features/PttGlobalBreakEven.cs --csv` confirms all methods CCN <= 8.
`PttGlobalBreakEven.cs` is NOT in `git diff --name-only` (no changes made).

### Lane Isolation
This ticket makes ZERO edits to CopyEngine.cs.

### Completion Artifact
`docs/brain/WAVE1-LANE-B/ticket-5-completion.md`

---

## Aggregate Spec Coverage

| Spec Req | Method | Ticket | Covered |
|----------|--------|--------|---------|
| B-01 | SubmitBePair | T-1 | yes |
| B-02 | SubmitSwapPair | T-2 | yes |
| B-03 | ExecuteFollowers | T-3 | yes |
| B-04 | Execute() and Execute(List) | T-3 | yes |
| B-05 | Execute main | T-4 | yes |
| B-06 | SubmitBeStopLocal | T-1 | yes |
| B-07 | ExecuteOne | T-3 | yes |
| B-08 | SubmitBareStopSwap | T-2 | yes |
| B-09 | SubmitStopOrder | T-4 | yes |
| B-10 | SubmitTargetOrder | T-4 | yes |

All 10 B-XX requirements covered. No gaps. No duplicates.

---

## Lane Isolation Summary

WAVE1-LANE-B ISOLATION CONFIRMED: ZERO edits to CopyEngine.cs across all 5 tickets.

All tickets are VERIFICATION-ONLY. No code is written to any file.
`git diff --name-only` must show NO files after all 5 tickets are executed.

---

## TICKETS_COMPLETE
