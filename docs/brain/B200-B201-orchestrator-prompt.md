# B200 + B201 Consolidated Pipeline Orchestrator Prompt

**Epic**: PTT-COPIER-B200 + B201 — Direct Repair Formalisation + Entry-Order Cancel Bug
**Date**: 2026-10-15
**Status**: B200 all 4 repairs SIM confirmed. B201 root cause diagnosed. Single pipeline covers both.

---

## SITUATION BRIEF

B200 has four direct repairs, ALL already applied to CopyEngine.cs and SIM confirmed:

| ID | Root Cause | Status |
|----|-----------|--------|
| B200-QTY-01 | `fo.Quantity` -> `fo.Quantity + fo.Filled` in HandleEntryChange | SIM CONFIRMED |
| B200-SFB-NULL-01 | `_followerSlateReady[acc.Name] = true` missing in TryLaneDDispatchFollower | SIM CONFIRMED |
| B200-NAKED-01 | TryNakedDetect skips "Entry"-named orders on follower accounts | SIM CONFIRMED |
| B200-CLOSE-LANED-01 | V13 sentinel anchor (_flattenInFlightOrderId = acc.Name+"#pending" before flag) | SIM CONFIRMED V13 |

B201 has one new bug, root cause confirmed from code (no .cs changes applied yet):

| ID | Root Cause | Status |
|----|-----------|--------|
| B201-ENTRY-CANCEL-01 | IsQxCancelCandidate L994 `o.Name == "Entry"` not scoped to follower accounts | DIAGNOSED — FIX PENDING |

**This pipeline covers**:
- Ticket 1: B200Tests.cs — xUnit tests for all 4 B200 repairs
- Ticket 2: B201-ENTRY-CANCEL-01 code fix (IsQxCancelCandidate isFollower guard)
- Ticket 3: B201Tests.cs — xUnit tests for B201 fix
- Ticket 4: Code quality audit (diagnostic log cleanup, CYC confirmation, deferred backlog)

---

## PIPELINE (ALL 7 PHASES MANDATORY — NONE SKIPPABLE — NONE COMBINABLE)

```
Ph1   ptt-architect       -> docs/brain/B200/02-architecture-plan.md
Ph2   ptt-plan-reviewer   -> docs/brain/B200/02-plan-review.md          (REVIEW_PASS gate)
Ph3   ptt-architect       -> docs/brain/B200/04-tickets.md
Ph3.5 ptt-ticket-reviewer -> docs/brain/B200/04-ticket-review.md        (TICKET_REVIEW_PASS gate)
Ph4a  ptt-engineer        -> src .cs edits + docs/brain/B200/ticket-N-completion.md (x4)
Ph4b  ptt-verifier        -> docs/brain/B200/ticket-N-verification.md   (VERIFY_PASS gate x4)
Ph5   ptt-plan-reviewer   -> docs/brain/B200/05-final-review.md
                              docs/brain/B200/06-deferred-backlog.md
```

No shortcuts. No direct .cs edits outside Ph4a.
Small one-off edits require explicit Director approval (case-by-case).

---

## JANE STREET GATES (run before ANY .cs edit)

```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "^\s*lock\s*\(" | Measure-Object
# Must be 0

Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+\(" | Measure-Object
# Must be 0

powershell -File .\scripts\ptt-sync.ps1
# Must be 19/0
```

---

## TICKET 1 — B200Tests.cs: xUnit Tests for All 4 B200 Repairs

**File to create**: `src/PropTraderTools/Tests/B200Tests.cs`
**Type**: New file — xUnit tests only. No .cs src changes in this ticket.

### Test T1: B200-QTY-01
```
T_B200_01_HandleEntryChange_PartFilledFollowerOrder_UsesOriginalQty
Scenario: follower entry order Quantity=5, Filled=5 (PartFilled: 10 total)
Assert: drain payload qty = 10 (Quantity + Filled), NOT 5 (Quantity alone)
Assert: SendCopyWithAtm called with signal.Quantity = 10
```

### Test T2: B200-SFB-NULL-01
```
T_B200_02_LaneDDispatch_SetsSlateReady_AfterAtmSuccess
Scenario: _followerSlateReady[acc.Name] = false before TryLaneDDispatchFollower
Assert: _followerSlateReady[acc.Name] == true after successful SendCopyWithAtm
Assert: FindFollowerBracketOrder does NOT return null for a Working bracket after dispatch
```

### Test T3: B200-NAKED-01
```
T_B200_03_TryNakedDetect_SkipsEntryNamedOrders_OnFollower
Scenario: follower account receives Filled event for order named "Entry"
Assert: TryNakedDetect returns without firing PTT-Flatten
Assert: no flatten submitted for follower when order.Name == "Entry"
```

### Test T4: B200-CLOSE-LANED-01 (V13 sentinel anchor)
```
T_B200_04_FlattenInFlight_SentinelAnchor_BlocksStaleFilledEvent
Scenario: _flattenInFlightOrderId set to acc.Name + "#pending" (sentinel)
          OnOrderUpdate fires with PTT-Flatten Filled from prior cycle (different OrderId)
Assert: stale Filled event does NOT clear _flattenInFlight[acc.Name]
Assert: IsFlattenInFlightOrRecent returns true while sentinel is set
Assert: after real submit, _flattenInFlightOrderId overwritten with real OrderId
Assert: Filled event with matching OrderId correctly clears the flag
```

**Verifier check for Ticket 1**:
- All 4 tests pass (`dotnet test --filter "B200"`)
- No existing test regressions
- No .cs src files modified (test file only)
- ptt-sync 19/0

---

## TICKET 2 — B201-ENTRY-CANCEL-01: IsQxCancelCandidate isFollower Guard

**File**: `src/PropTraderTools/CopyEngine.cs`
**Type**: Code fix — 3 touch points, all in CopyEngine.cs.

### Root Cause
`IsQxCancelCandidate` (~L994):
```csharp
if (o.Name == "Entry")
    return true;  // DW-B93: follower entry orders
```
NT8 names ALL ATM pending limit orders "Entry" — including the leader's own add-to-position
limit. `CancelQxBrackets` is called on both leader AND follower accounts. The leader's
add-to-position "Entry" order is incorrectly cancelled by every QX/BE button.

### Fix — 3 touch points, CopyEngine.cs only

**Touch point 1** — `IsQxCancelCandidate` signature (~L982):
```csharp
// BEFORE:
internal static bool IsQxCancelCandidate(Order o)

// AFTER:
internal static bool IsQxCancelCandidate(Order o, bool isFollower)
```

**Touch point 2** — `IsQxCancelCandidate` body (~L994):
```csharp
// BEFORE:
if (o.Name == "Entry")
    return true; // (6) DW-B93: Named ATM follower entry Limit

// AFTER:
if (isFollower && o.Name == "Entry")
    return true; // (6) DW-B93: Named ATM follower entry Limit -- scoped to follower only
```

**Touch point 3a** — `CancelQxBrackets` 2-arg overload (~L1007), add at top of method body:
```csharp
bool isFollower = IsFollowerAccount(acc);
```
Then thread `isFollower` into every call to `IsQxCancelCandidate(o)` -> `IsQxCancelCandidate(o, isFollower)`.

**Touch point 3b** — `CancelQxBrackets` 3-arg overload (~L1201), same pattern:
```csharp
bool isFollower = IsFollowerAccount(acc);
```
Then thread `isFollower` into every call to `IsQxCancelCandidate(o)` -> `IsQxCancelCandidate(o, isFollower)`.

**Touch point 3c** — `BuildQxSnapshot` (~L1050), same pattern:
```csharp
bool isFollower = IsFollowerAccount(acc);
```
Then thread into `IsQxCancelCandidate(o)` calls within that method.

### CYC impact
- `IsQxCancelCandidate`: +0 delta. The `&&` extends an existing branch, no new decision point by Roslyn.
- `CancelQxBrackets` 2-arg: +0 delta. `isFollower` is a local variable assignment, not a branch.
- `CancelQxBrackets` 3-arg: +0 delta. Same.
- `BuildQxSnapshot`: +0 delta. Same.

### Do NOT touch
- `PttBreakEvenSwap.cs` — its call to `CancelQxBrackets(acc, instr)` does not change signature (2-arg overload already has acc).
- `PttGlobalQuickExit.cs` — calls go through `PttQuickExit.Execute` which calls `CancelQxBrackets` internally; no external signature exposure.
- Any `.cs` file outside `CopyEngine.cs`.

**Verifier check for Ticket 2**:
- `grep "isFollower && o.Name" src/PropTraderTools/CopyEngine.cs` shows 1 match at ~L994
- `grep "bool isFollower = IsFollowerAccount" src/PropTraderTools/CopyEngine.cs` shows 3 matches (2-arg, 3-arg, BuildQxSnapshot)
- `grep -c "lock(" src/PropTraderTools/CopyEngine.cs` = 0 new occurrences
- All existing CopyEngineTests pass
- ptt-sync 19/0
- Ph4b deletion audit: no symbols deleted
- Single-hunk ban: diff shows multiple hunks (not a full-file replace)
- Guardian symbol script: `powershell -File .\scripts\verify_critical_symbols.ps1` PASS

---

## TICKET 3 — B201Tests.cs: xUnit Tests for B201 Fix

**File to create**: `src/PropTraderTools/Tests/B201Tests.cs`
**Type**: New file — xUnit tests only.

### Test T1: Leader "Entry" NOT cancelled
```
T_B201_01_IsQxCancelCandidate_LeaderEntryOrder_NotCancelled
Scenario: order.Name = "Entry", isFollower = false
Assert: IsQxCancelCandidate(o, false) returns false
```

### Test T2: Follower "Entry" IS cancelled
```
T_B201_02_IsQxCancelCandidate_FollowerEntryOrder_Cancelled
Scenario: order.Name = "Entry", isFollower = true
Assert: IsQxCancelCandidate(o, true) returns true
```

### Test T3: Non-"Entry" names unaffected by isFollower
```
T_B201_03_IsQxCancelCandidate_AtmBracketNames_AlwaysCancelled
Scenario: order.Name = "Stop1" / "Target2" / "PTT-QX-Stop" / "PTT-BE-Target-1" / "PTT-CopyEntry"
          isFollower = false (leader account)
Assert: IsQxCancelCandidate(o, false) returns true for all of the above
Assert: isFollower=false does NOT protect ATM bracket names from cancellation
```

### Test T4: CancelQxBrackets leader path skips "Entry"
```
T_B201_04_CancelQxBrackets_LeaderAccount_DoesNotCancelEntryOrder
Scenario: leader account has one "Entry" Limit order (Working) + one "Stop1" StopMarket (TriggerPending)
          CancelQxBrackets(leaderAcc, instr) called
Assert: acc.Cancel called with ["Stop1"] only — "Entry" order NOT in cancel list
```

**Verifier check for Ticket 3**:
- All 4 tests pass (`dotnet test --filter "B201"`)
- No existing test regressions
- No .cs src files modified
- ptt-sync 19/0

---

## TICKET 4 — Code Quality: Diagnostic Log Cleanup + CYC Confirmation + Deferred Backlog

**Files**: `src/PropTraderTools/CopyEngine.cs` (diagnostic log removal only)

### Diagnostic logs to remove (all added as temporary B200 diagnostics)
Confirm each line still present before removing. Remove only these 5 tags:
1. `[PTT-FLAT-OPEN]` log line in `IsFlattenInFlightOrRecent` call site (inside Lane D/E check)
2. `[PTT-ATM-CALLER]` log in `TryLaneDHook` or equivalent (~L2921)
3. `[PTT-ATM-CALLER]` log in `TryLaneEHook` or equivalent (~L3080)
4. `[PTT-ATM-CALLER]` log in `ReplaceFollowerCopyOnAtmCancel` or equivalent (~L4767)
5. `[PTT-ATM-CALLER]` log in `DrainThenDispatch` fast-path or equivalent (~L7285)

**Do NOT remove**: standard operational logs ([PTT-ATM-SEND], [PTT-DISPATCH], [TP1-OOU], etc.)

### CYC confirmation
Verify (read, do not change):
- `HasActivePttFlatten`: CYC = 4 (includes PartFilled from V11+)
- `FlattenOneAccount`: CYC <= 8
- `IsQxCancelCandidate`: CYC = 7 (after B201 fix, +0 delta)
- `IsFlattenInFlightOrRecent`: CYC <= 4

### Deferred backlog items for 06-deferred-backlog.md
Document these as deferred (do NOT implement):
- DW-B200-LANE-E-COPY-ENABLED: TryLaneEHook not gated on _isCopyEnabled. Low risk (PassesDailyCapCheck is outer guard). Deferred B202+.
- DW-B201-OPTION-B: Alternative rename follower dispatch "Entry" -> "PTT-Copy-Entry" at dispatch time. Simpler long-term; deferred pending Director decision.
- DW-B199-H2-CLEANUP-01: Conditional — if B199 H2 divide remnant code still present in SendCopyWithAtm, clean up. If already removed, close as N/A.

**Verifier check for Ticket 4**:
- `grep "\[PTT-ATM-CALLER\]" src/PropTraderTools/CopyEngine.cs` = 0 results
- `grep "\[PTT-FLAT-OPEN\]" src/PropTraderTools/CopyEngine.cs` = 0 results
- All existing tests pass after log removal
- `docs/brain/B200/06-deferred-backlog.md` created with 3 deferred items
- ptt-sync 19/0
- Single-hunk ban enforced on diagnostic log removal commit

---

## Ph5 FINAL REVIEWER CHECKLIST

- [ ] All 4 VERIFY_PASS gates issued (ticket-1 through ticket-4 verification.md)
- [ ] `dotnet test` clean — zero failures, zero new warnings
- [ ] `grep "lock(" src/PropTraderTools/CopyEngine.cs` = 0
- [ ] `grep "async void " src/PropTraderTools/CopyEngine.cs` = 0 (non-event-handlers)
- [ ] ptt-sync 19/0
- [ ] `grep "\[PTT-ATM-CALLER\]" src/PropTraderTools/CopyEngine.cs` = 0
- [ ] `grep "isFollower && o.Name" src/PropTraderTools/CopyEngine.cs` = 1 match at ~L994
- [ ] `specs/002-trade-copier-spec.html` B200 + B201 blocks confirm PIPELINE_COMPLETE
- [ ] `docs/brain/B200/06-deferred-backlog.md` written with 3 deferred DW items
- [ ] Guardian symbol script: `powershell -File .\scripts\verify_critical_symbols.ps1` PASS

---

## KEY FILES

```
src/PropTraderTools/CopyEngine.cs                     -- B201 fix (T2) + diagnostic removal (T4)
src/PropTraderTools/Tests/B200Tests.cs                -- create new (T1)
src/PropTraderTools/Tests/B201Tests.cs                -- create new (T3)
src/PropTraderTools/Features/PttBreakEvenSwap.cs      -- read only (call site, no change needed)
src/PropTraderTools/Features/PttGlobalQuickExit.cs    -- read only (call site, no change needed)
specs/002-trade-copier-spec.html                      -- update status to PIPELINE_COMPLETE on pass
docs/brain/B200/06-deferred-backlog.md                -- create with 3 deferred items
```

---

## CONTEXT FOR Ph1 ARCHITECT

B200 code is already in place and SIM confirmed. Ph1 architect does NOT redesign B200.
The architect's job is:
1. Review the 4 B200 repairs for CYC compliance and JS rule compliance (read-only).
2. Design the B201 fix (Option A scope guard) — confirm touch points, confirm CYC delta.
3. Produce 02-architecture-plan.md covering all 4 tickets.

B201 Option A is the approved fix direction. Option B (rename dispatch) is deferred to backlog.
Do not propose alternative architectures for B201 — the fix is a 3-touch-point scope guard.

*Prepared by: copier-spec mode, 2026-10-15*
