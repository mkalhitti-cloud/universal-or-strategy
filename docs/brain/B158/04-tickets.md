# B158 Tickets
# ptt-architect Phase 3 output
# Block: B158
# Status: TICKETS_COMPLETE
# Input: docs/brain/B158/02-architecture-plan.md (REVIEW_PASS)
# Output: docs/brain/B158/04-tickets.md
# Lanes: LANES-APPROVED (T1 and T2 are independent, may be executed in parallel)

---

## Ticket Index

| Ticket | Defect Card | Priority | Title | Nature | Lane |
|--------|-------------|----------|-------|--------|------|
| T1 | DW-B156-01 | P0 | Leader-flat drain abort SIM verification gate | Verification-only (no code change) | Independent |
| T2 | DW-B157-COMMENT-01 | P3 | Fix stale comment at IsReversalToFlatFollower | Comment change only | Independent |

Lane independence confirmed: T1 region = CopyEngine.cs L1929-1974. T2 region = CopyEngine.cs L6517.
Separation = ~4543 lines. Zero shared design dependency. Each ticket is completable without the other.

---

---

# T1 -- DW-B156-01: Leader-Flat Drain Abort SIM Verification Gate

## Header

| Field | Value |
|-------|-------|
| Ticket ID | T1 |
| Defect Card | DW-B156-01 |
| Priority | P0 |
| Title | Leader-flat drain abort SIM verification gate |
| Source file | src/PropTraderTools/CopyEngine.cs |
| Test file | src/PropTraderTools/CopyEngineTests.cs |
| Spec req IDs | DW-B156-01 (ghost re-entry on follower after leader-flat BE fill) |
| Nature | VERIFICATION-ONLY. Zero code changes permitted. |
| Block | B158 |

---

## Problem Statement

Block B156 introduced Fix3 (IsLeaderFlatDrainAbort) to abort pending follower drain-dispatch
sequences when the leader account reaches a flat position via a filled order. The fix was
committed and documented through the B156 pipeline (FINAL_PASS 2026-09-14). The DW-B156-DEFER-1
CYC=14 decomposition was resolved in B157-T2. The B157-T3 ticket added the unit test
TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString at CopyEngineTests.cs:9481.

DW-B156-01 cannot be formally closed until a live SIM gate confirms that:
(a) the source implementation matches the Fix3 design, and
(b) the [DRAIN-ABORT-LEADER-FLAT] log marker appears in OutputTab1 during the 5-step SIM scenario.

This ticket is the SIM verification gate. If PASS, DW-B156-01 is CLOSED.
If FAIL (timing race), create defect card DW-B158-DRAIN-RACE-01 and escalate to Director.

---

## Root Cause (Reference)

The ghost re-entry bug: when a leader exits flat via a BE stop fill, the NT8 background
thread processes the fill event on the leader account. If at that moment one or more follower
drain-cancel sequences are pending in _pendingDispatchDrains (awaiting cancel-ack before
re-submitting the copied entry), the drain state is already queued. Without Fix3,
SubmitDrainedEntry would fire for each follower when the cancel-ack arrived, submitting a
new copy entry on the follower even though the leader is now flat -- the ghost re-entry.

Fix3 intercepts on the leader fill event (pre-Gate-1, via TryEvictFollowerBeSlot) and aborts
all matching pending drains before SubmitDrainedEntry can execute.

---

## Fix Design (Verification Targets)

**T1 introduces zero code changes. All items below describe what the engineer must READ and CONFIRM.**

### Step 1: Confirm IsLeaderFlatDrainAbort Implementation

Read CopyEngine.cs lines 1929-1974.

Method to confirm -- `IsLeaderFlatDrainAbort`:
```
Signature: private void IsLeaderFlatDrainAbort(Order o, string instrFull)
CYC: 8 (AT LIMIT -- confirm no changes have been made)
Location: CopyEngine.cs:1950
```

Required structure (all items must be present and in order):
1. Guard: `if (IsFollowerAccount(o.Account)) return;`  -- skip follower accounts
2. Guard: `if (o.OrderState != OrderState.Filled) return;`  -- Filled state only
3. Guard: `if (!IsFlat(FindPosition(o.Account, o.Instrument))) return;`  -- leader must be flat
4. Loop: `foreach (var kv in _pendingDispatchDrains)` -- iterate all follower drain entries
5. Instrument match: `kv.Value.Instrument?.FullName == instrFull` -- filter by instrument
6. Abort call: `AbortDrainOnFill(kv.Key)` -- remove drain state for matched follower
7. Counter: `abortCount++` for each abort
8. Log: `[DRAIN-ABORT-LEADER-FLAT]` marker when `abortCount > 0`

### Step 2: Confirm TryEvictFollowerBeSlot Call Site

Read CopyEngine.cs lines 1929-1942 (TryEvictFollowerBeSlot body).

Confirm: `IsLeaderFlatDrainAbort(o, instrFull)` is called unconditionally at approximately L1938.
This call must appear BEFORE Gate 1 (TryResolveEnabledRule at OnOrderUpdate L1763).

Hook-point context in OnOrderUpdate:
```
L1712: TryEvictFollowerBeSlot(e)        <-- IsLeaderFlatDrainAbort fires here (pre-Gate-1)
L1763: if (!TryResolveEnabledRule(...)) return;   <-- Gate 1
L1767: TryFirePositionState(e)          <-- TryClearLeaderDirectionOnFlat fires here (post-Gate-1)
```

Pre-Gate-1 placement is strictly correct: if no enabled rule exists, Gate 1 returns early
and TryClearLeaderDirectionOnFlat never fires. The drain abort must fire unconditionally.

### Step 3: Confirm AbortDrainOnFill Signature

Read CopyEngine.cs lines 8343-8348.

Signature to confirm:
```
private void AbortDrainOnFill(string acctKey)   // CYC=3
```

Confirm: TryRemove from _pendingDispatchDrains, no lock(). CYC=3.

### Step 4: Confirm Existing Unit Test

Read CopyEngineTests.cs line 9481.

Test to confirm present and without [Skip] attribute:
```
TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString
Asserts: Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src)
```

Run: `dotnet test --filter "TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString"`
Required result: PASSED.

### Step 5: SIM Gate Execution

Run the 5-step SIM scenario (see SIM Verification Steps section below).
Document all output evidence in the completion report.

---

## SIM Verification Steps

**Setup**: 1 leader account (Sim101) + 2 follower accounts (Sim103, Sim104).
ATM strategy enabled. CopyEngine copy mode active for the instrument under test.

**CRITICAL MARKER DISTINCTION**:
- `[DRAIN-ABORT-LEADER-FLAT]` = Fix3 marker (IsLeaderFlatDrainAbort). THIS is what T1 verifies.
- `[DRAIN-ABORT-FLAT]` = Fix1 marker (SubmitDrainedEntry follower-flat guard, L8319). DIFFERENT fix.
  Do NOT confuse these. Check for [DRAIN-ABORT-LEADER-FLAT] only in Step 3.

| Step | Action | PASS Criteria | FAIL Criteria |
|------|--------|---------------|---------------|
| 1 | Enter Long on leader (Sim101) using ATM strategy | Followers (Sim103, Sim104) receive Long copy entry. No [PTT-COPY-GUARD] in output. | Followers do not copy. Check rule config. |
| 2 | Trigger BE All (move price to stop level or click BE button) | Followers enter drain-cancel sequence. Leader PTT-BE-Stop moves to break-even price. | BE All does not trigger. Check ATM and BE config. |
| 3 | Wait for leader PTT-BE-Stop to fill (leader goes flat at break-even) | `[DRAIN-ABORT-LEADER-FLAT] leader=Sim101 instr=<instrument> aborted=N` appears in OutputTab1. N >= 1 when follower drains were pending. | Log absent AND ghost entry present = timing race. See SIM FAIL escalation path below. |
| 4 | Check follower accounts (Sim103, Sim104) | No ghost re-entry: followers do NOT receive a new Long copy entry after leader goes flat. Followers' positions are flat or closing normally. | Ghost Sell Entry (or any new entry order) appears on Sim103/Sim104 after leader flat = ghost re-entry bug present. |
| 5 | Informational: check for [DRAIN-ABORT-FLAT] | [DRAIN-ABORT-FLAT] may or may not appear (Fix1 = follower-flat guard in SubmitDrainedEntry). Its presence means the follower was flat at submission time -- a different scenario. No action required. | N/A (informational only). |

**OVERALL PASS**: Step 3 shows [DRAIN-ABORT-LEADER-FLAT] with aborted >= 1, AND Step 4 shows no ghost entry.
DW-B156-01 is CLOSED. Document in completion report.

**OVERALL FAIL (timing race)**: Step 3 log absent AND Step 4 shows ghost entry.
The follower cancel-acks completed before the leader fill event was processed.
See SIM FAIL Escalation Path below.

**NOTE**: If Step 3 log is absent but Step 4 also shows NO ghost entry, this means the drains
were already consumed before the leader filled. This is a partial-coverage scenario.
Document it in the completion report as inconclusive. Do not close DW-B156-01. Escalate to Director.

---

## SIM FAIL Escalation Path

If the SIM gate produces FAIL (timing race):

1. Do NOT implement a new fix within B158 scope.
2. Document the failure in the completion report with exact OutputTab1 content.
3. Create defect card `DW-B158-DRAIN-RACE-01`:
   - Title: Leader-flat drain abort timing race: cancel-ack pre-consumes drain before IsLeaderFlatDrainAbort fires
   - Root cause hypothesis: follower cancel-ack sequence completes before NT8 background thread
     processes leader fill event. _pendingDispatchDrains entry already removed by SubmitDrainedEntry.
   - Architectural fix required: leader-flat guard inside SubmitDrainedEntry (check whether any
     rule leader for the payload instrument is flat at the moment of submission). Requires new
     private helper and CYC analysis. Out of scope for B158.
   - Reproduction steps: (copy the exact SIM evidence from the completion report)
4. Forward DW-B158-DRAIN-RACE-01 to Director for prioritization.

---

## Acceptance Criteria

**Implementation-level (source verification)**:
- [ ] IsLeaderFlatDrainAbort confirmed present at CopyEngine.cs:1950 with all 8 structural items verified.
- [ ] TryEvictFollowerBeSlot confirmed to call IsLeaderFlatDrainAbort at approximately L1938 (pre-Gate-1).
- [ ] AbortDrainOnFill confirmed at L8343 with signature `private void AbortDrainOnFill(string acctKey)`.
- [ ] `dotnet test` run. TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString = PASSED.
- [ ] Zero new test failures vs B157 baseline.

**SIM-level**:
- [ ] [DRAIN-ABORT-LEADER-FLAT] marker confirmed in OutputTab1 during Step 3.
- [ ] No ghost re-entry on Sim103/Sim104 during Step 4.
- [ ] If SIM FAIL: DW-B158-DRAIN-RACE-01 defect card created and forwarded to Director.

**Completion report**:
- [ ] All 7 scans documented with results.
- [ ] SIM evidence (OutputTab1 content) copied verbatim into completion report.
- [ ] DW-B156-01 closure status stated (CLOSED or ESCALATED).

---

## 7-SCAN CHECKLIST (MANDATORY)

T1 is verification-only. All scans are read-only checks against the existing source.
Zero code changes means zero new violations can be introduced. Each scan confirms
the existing implementation passes the project constraint.

### SCAN-01: Non-ASCII byte check

Command:
```powershell
$bytes = [System.IO.File]::ReadAllBytes("src\PropTraderTools\CopyEngine.cs")
($bytes | Where-Object { $_ -gt 127 } | Measure-Object).Count
```
Required result: `0`
Region: file-wide (existing source; T1 adds no bytes).
FAIL action: Flag pre-existing ASCII violation. Report in completion report. Do not add bytes.

### SCAN-02: lock() grep

Command:
```powershell
Select-String -Pattern '^\s+lock\s*\(' src\PropTraderTools\CopyEngine.cs
```
Required result: 0 matches
Region: file-wide. T1 adds no code. JS-021: no lock().
FAIL action: Pre-existing violation. Report in completion report. Do not modify.

### SCAN-03: DateTime.Now grep

Command:
```powershell
Select-String -Pattern '\bDateTime\.Now\b' src\PropTraderTools\CopyEngine.cs
```
Required result: 0 executable-code matches (comment occurrences acceptable)
Region: file-wide. T1 adds no code.
FAIL action: Pre-existing violation. Report in completion report. Do not modify.

### SCAN-04: CYC check

T1 modifies no methods. Verify by reading source comment.

| Method | Expected CYC | Source comment line | Source comment text |
|--------|-------------|---------------------|---------------------|
| IsLeaderFlatDrainAbort | 8 (AT LIMIT) | L1947 (approx) | CYC=8 |
| TryEvictFollowerBeSlot | 7 | L1926 (approx) | CYC=7 |
| AbortDrainOnFill | 3 | L8342 (approx) | CYC=3 |

Required result: Source comments confirm values above. No methods have CYC > 8.
FAIL action: CYC > 8 in any of these methods = pre-existing violation. Report and escalate.
Note: A stale comment at L1904 may state CYC=6 for TryEvictFollowerBeSlot -- this is a
pre-existing inconsistency from before the DW-B156-DEFER-1 decomposition. The L1926 comment
(CYC=7) is the authoritative value. Note this in the completion report for future cleanup.

### SCAN-05: async void grep

Command:
```powershell
Select-String -Pattern '\basync void\b' src\PropTraderTools\CopyEngine.cs
```
Required result: 0 matches in executable code (NT8 lifecycle overrides excluded by convention)
Region: file-wide. T1 adds no code.
FAIL action: Pre-existing violation. Report in completion report.

### SCAN-06: xUnit test compile check

No new tests added by T1. Run existing test suite.

Command:
```powershell
dotnet test --filter "TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString"
```
Required result: 1 test PASSED. 0 failures.

Also run full suite baseline check:
```powershell
dotnet test
```
Required result: Zero new failures vs B157 baseline.

### SCAN-07: ptt-sync.ps1 / diff check

T1 introduces ZERO code changes. The diff must be empty.

Verify before marking BUILD_PASS:
```powershell
git diff src\PropTraderTools\CopyEngine.cs
```
Required result: No output (empty diff). If any diff appears, the engineer has made an
unintended edit. Revert before proceeding.

ptt-sync.ps1 is NOT required for a zero-diff ticket, but may be run as a sanity check:
```powershell
powershell -File .\scripts\ptt-sync.ps1
```

Build must pass:
```powershell
dotnet build
```
Required result: 0 errors, 0 new warnings.

---

## Scope Constraints (Do NOT Touch)

- Do NOT modify any executable code in CopyEngine.cs.
- Do NOT modify CopyEngineTests.cs.
- Do NOT modify TryEvictFollowerBeSlot, IsLeaderFlatDrainAbort, or AbortDrainOnFill.
- Do NOT attempt to fix the stale CYC=6 comment at L1904 (separate cleanup, not this ticket).
- Do NOT make any changes to T2's region (CopyEngine.cs ~L6516-6527).
- Do NOT create DW-B158-DRAIN-RACE-01 unless the SIM gate fails.
- If implementation gap is found during source verification (Step 1-3 above), STOP.
  Document the gap. Mark BUILD_FAIL with gap description. Do not attempt an inline fix.

---

---

# T2 -- DW-B157-COMMENT-01: Fix Stale Comment at IsReversalToFlatFollower

## Header

| Field | Value |
|-------|-------|
| Ticket ID | T2 |
| Defect Card | DW-B157-COMMENT-01 |
| Priority | P3 |
| Title | Fix stale comment at IsReversalToFlatFollower (~L6517) |
| Source file | src/PropTraderTools/CopyEngine.cs |
| Test file | src/PropTraderTools/CopyEngineTests.cs (read-only, no changes) |
| Spec req IDs | DW-B157-COMMENT-01 (stale comment at IsReversalToFlatFollower) |
| Nature | Comment change only. Zero executable code change. Zero logic change. |
| Block | B158 |

---

## Problem Statement

The comment at CopyEngine.cs:6517 describes the semantics of `IsReversalToFlatFollower`
incorrectly. The comment was written under pre-B157 semantics (DW-B157-RG-01 reversal guard
fix). Before B157, the method body was `return currentAction != lastAction && followerIsFlat`.
After B157, the logic was inverted to `return currentAction != lastAction && !followerIsFlat`.
The comment was not updated at the time of the fix.

The current comment reads "the follower is flat", but the method returns true when the
follower is NOT flat (i.e., has an open position). Returning true means "skip this dispatch"
(the protection path fires). The comment is therefore logically backwards and misleading
to any engineer reading this predicate.

---

## Root Cause

DW-B157-RG-01 inverted `followerIsFlat` to `!followerIsFlat` in the return expression.
The single-line comment on the preceding line was not updated in the same commit.

---

## Fix Design

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line**: 6517
**Change type**: Replace comment text. ONE line only. No other lines change.

Method being annotated (for reference -- DO NOT MODIFY):
```csharp
// B119: DW-B128 -- direction-change guard predicate.
// [LINE 6517 -- THIS IS THE LINE TO CHANGE]
// CYC=2 (one && expression in a single return). JS-001: no throw. JS-021: no lock. ASCII-only.
// internal static: directly callable from B119Tests.cs without reflection.
internal static bool IsReversalToFlatFollower(
    OrderAction currentAction,
    OrderAction lastAction,
    bool followerIsFlat
)
{
    return currentAction != lastAction && !followerIsFlat;
}
```

**OLD line 6517 (exact string -- remove this)**:
```
// Returns true iff the current dispatch reverses the last direction AND the follower is flat.
```

**NEW line 6517 (exact string -- replace with this)**:
```
// Returns true iff the current dispatch reverses the last direction AND the follower is NOT flat (open position + reversal = protection path).
```

All characters in the new comment are printable ASCII (0x20-0x7E). ASCII-001 compliant.

Method signature (unchanged, for reference only):
```
internal static bool IsReversalToFlatFollower(
    OrderAction currentAction,
    OrderAction lastAction,
    bool followerIsFlat
)   // CYC=2 -- unchanged
```

---

## Acceptance Criteria

- [ ] CopyEngine.cs:6517 contains the new comment text (exact string above).
- [ ] No other lines in CopyEngine.cs changed.
- [ ] `git diff src\PropTraderTools\CopyEngine.cs` shows exactly one changed line: L6517.
- [ ] New comment text is ASCII-only (no Unicode, no curly quotes, no non-ASCII characters).
- [ ] `dotnet build` passes with 0 errors.
- [ ] `dotnet test` passes. All IsReversalToFlatFollower tests pass (CopyEngineTests.cs ~L8103-8175).
- [ ] Zero new test failures vs B157 baseline.
- [ ] ptt-sync.ps1 run after the edit.

---

## 7-SCAN CHECKLIST (MANDATORY)

### SCAN-01: Non-ASCII byte check

Command (run AFTER the edit):
```powershell
$bytes = [System.IO.File]::ReadAllBytes("src\PropTraderTools\CopyEngine.cs")
($bytes | Where-Object { $_ -gt 127 } | Measure-Object).Count
```
Required result: `0`
Specific concern for T2: the new comment text contains only ASCII. If any non-ASCII crept in
(e.g., from copy-paste of a curly quote), this scan will catch it.
FAIL action: Open the file, locate the non-ASCII byte, replace with ASCII equivalent.
Re-run scan until Count=0.

### SCAN-02: lock() grep

Command:
```powershell
Select-String -Pattern '^\s+lock\s*\(' src\PropTraderTools\CopyEngine.cs
```
Required result: 0 matches
Rationale: Comment change cannot introduce lock(). This is a baseline confirmation.
FAIL action: Pre-existing violation. Report in completion report.

### SCAN-03: DateTime.Now grep

Command:
```powershell
Select-String -Pattern '\bDateTime\.Now\b' src\PropTraderTools\CopyEngine.cs
```
Required result: 0 executable-code matches
Rationale: Comment change cannot introduce DateTime.Now. Baseline confirmation.
FAIL action: Pre-existing violation. Report in completion report.

### SCAN-04: CYC check

No executable code modified. CYC for IsReversalToFlatFollower remains 2.
Verify by reading the CYC comment on the line BELOW the changed line:

Expected (line 6519 or thereabouts):
```
// CYC=2 (one && expression in a single return). JS-001: no throw. JS-021: no lock. ASCII-only.
```

Required result: CYC=2 comment is still present and unchanged. No executable lines were touched.
FAIL action: If the CYC comment changed unexpectedly, revert and re-apply the edit to line 6517 only.

### SCAN-05: async void grep

Command:
```powershell
Select-String -Pattern '\basync void\b' src\PropTraderTools\CopyEngine.cs
```
Required result: 0 matches in executable code
Rationale: Comment change cannot introduce async void. Baseline confirmation.
N/A for a comment-only change, but must be recorded as PASS.

### SCAN-06: xUnit test compile check

No new tests added by T2. Run existing suite.

Command:
```powershell
dotnet test --filter "IsReversalToFlatFollower"
```
Required result: All IsReversalToFlatFollower tests PASSED. 0 failures.
(Tests are located at CopyEngineTests.cs ~L8103-8175. Do not modify them.)

Also run full suite baseline:
```powershell
dotnet test
```
Required result: Zero new failures vs B157 baseline.

### SCAN-07: ptt-sync.ps1 + diff check

Verify the diff is comment-only (exactly one line):
```powershell
git diff src\PropTraderTools\CopyEngine.cs
```
Required result: Exactly ONE changed line -- L6517. The diff must show:
```
-// Returns true iff the current dispatch reverses the last direction AND the follower is flat.
+// Returns true iff the current dispatch reverses the last direction AND the follower is NOT flat (open position + reversal = protection path).
```
No other lines. No whitespace changes on other lines. No executable code changes.
FAIL action: If diff shows other lines changed, revert and re-apply only the target line.

Run ptt-sync.ps1 after confirming the diff is clean:
```powershell
powershell -File .\scripts\ptt-sync.ps1
```
Required result: Script exits without error. Hard links re-synchronized.

Build must pass:
```powershell
dotnet build
```
Required result: 0 errors, 0 new warnings.

---

## xUnit Tests

No new [Fact] tests required. This is an annotation-only change with zero behavioral impact.

Existing tests that must continue to PASS (do not modify):
- All tests in the `IsReversalToFlatFollower` test group at CopyEngineTests.cs ~L8103-8175.

Run to confirm:
```powershell
dotnet test --filter "IsReversalToFlatFollower"
```

---

## Scope Constraints (Do NOT Touch)

- Do NOT modify any executable code in CopyEngine.cs.
- Do NOT modify CopyEngineTests.cs in any way.
- Do NOT change the CYC=2 comment line adjacent to IsReversalToFlatFollower.
- Do NOT change lines 6516, 6518, 6519, 6520, 6521, 6522, 6523, 6524, 6525, 6526, 6527
  (only line 6517 changes).
- Do NOT make any changes to T1's region (CopyEngine.cs L1929-1974).
- If the line number of IsReversalToFlatFollower has drifted from 6517 (possible if T1
  generated an unexpected diff), locate the stale comment by searching for the exact old
  comment string before editing:
  ```powershell
  Select-String -Pattern "the follower is flat\." src\PropTraderTools\CopyEngine.cs
  ```
  Edit the located line only.

---

---

## Deferred Items Carried Forward (Not Addressed in B158)

| ID | Item | Priority | Status |
|----|------|----------|--------|
| DW-B154-05 | IsPttCopyEntryFilled delegation refactor | P3 | OPEN -- not touched |
| DW-B154-06 | TryTriggerBeRecovery post-deployment monitor | P2 | OPEN -- no new evidence |
| DW-B155-01 | B155 [DRAIN-ABORT-FLAT] SIM gate pending | P2 | OPEN -- separate from B158 T1 SIM gate |
| DW-BUG-0911-A-01 | Mid-close race in OnOrderUpdate | P2 | OPEN -- not addressed |

Note: DW-B155-01 and B158 T1 are SEPARATE SIM gates.
DW-B155-01 = SubmitDrainedEntry flat guard (Fix1, marker [DRAIN-ABORT-FLAT]).
B158 T1 = IsLeaderFlatDrainAbort (Fix3, marker [DRAIN-ABORT-LEADER-FLAT]).
Do not conflate them.

---

_Tickets authored by ptt-architect (Phase 3). Block B158._
_Input plan: docs/brain/B158/02-architecture-plan.md (REVIEW_PASS)._
_Source lines verified via plan: CopyEngine.cs L387-388, L1712, L1763, L1767, L1929-1974, L6516-6527, L8342-8348._
