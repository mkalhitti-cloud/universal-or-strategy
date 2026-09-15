# B158 Architecture Plan
# ptt-architect Phase 1 output
# Block: B158
# Status: PLAN_COMPLETE
# Output: docs/brain/B158/02-architecture-plan.md

---

## 1. Block Summary

| Field | Value |
|-------|-------|
| Block ID | B158 |
| Tickets | T1 (P0), T2 (P3) |
| Source file | src/PropTraderTools/CopyEngine.cs |
| Test file | src/PropTraderTools/CopyEngineTests.cs |
| Spec | file:///C:/WSGTA/universal-or-strategy-director/specs/002-trade-copier-spec.html |

| Ticket | Defect Card | Priority | Title | Nature |
|--------|-------------|----------|-------|--------|
| T1 | DW-B156-01 | P0 | Leader-flat drain abort (ghost re-entry fix) | Verification-only (see §3) |
| T2 | DW-B157-COMMENT-01 | P3 | Stale comment at IsReversalToFlatFollower | Comment change only |

Deferred items this block closes:
- DW-B156-01 (P0): closed by T1 SIM gate (if PASS).
- DW-B157-COMMENT-01 (P3): closed by T2 comment correction.

---

## 2. LANE-SPLIT GATE RESULT: LANES-APPROVED

| Q | Question | Answer |
|---|----------|--------|
| Q1 | Same method or within 50 lines? | NO -- T1 region ~L1929-1974, T2 region ~L6517; delta ~4543 lines |
| Q2 | Fix B design depends on Fix A final design? | NO -- T2 is annotation-only with zero dependency on T1 |
| Q3 | Each fix has standalone value if the other is blocked? | YES |
| Q4 | Each fix has an independent SIM/verification path? | YES |

Verdict: All four LANES-APPROVED conditions satisfied (NO on Q1+Q2, YES on Q3+Q4).
T1 and T2 may be executed in parallel. T2 must not modify T1's region (L1929-1974).

---

## 3. T1 Architecture: DW-B156-01 Leader-Flat Drain Abort

### 3.1 Source Investigation Result

**IMPLEMENTATION STATUS: Fix3 is ALREADY IMPLEMENTED.**

Before any design work, the following source investigation was conducted (see AGENTS.md
investigate-before-answering requirement):

| Method | File | Lines | Status |
|--------|------|-------|--------|
| IsLeaderFlatDrainAbort | CopyEngine.cs | 1950-1974 | Present. Implements Fix3 exactly. |
| TryEvictFollowerBeSlot | CopyEngine.cs | 1929-1942 | Thin router. Calls IsLeaderFlatDrainAbort at L1938. |
| AbortDrainOnFill | CopyEngine.cs | 8343-8348 | Present. Signature: `private void AbortDrainOnFill(string acctKey)` |
| _pendingDispatchDrains | CopyEngine.cs | 387-388 | ConcurrentDictionary<string, PendingDispatchDrain>. Key = follower acc.Name (StringComparer.Ordinal). |

IsLeaderFlatDrainAbort (CopyEngine.cs:1950) exactly matches the DW-B156-01 Fix3 spec:

```csharp
private void IsLeaderFlatDrainAbort(Order o, string instrFull)
{
    if (IsFollowerAccount(o.Account))               // guard: skip follower accounts
        return;
    if (o.OrderState != OrderState.Filled)
        return;
    if (!IsFlat(FindPosition(o.Account, o.Instrument)))
        return;
    int abortCount = 0;
    foreach (var kv in _pendingDispatchDrains)
    {
        if (kv.Value.Instrument?.FullName == instrFull)
        {
            AbortDrainOnFill(kv.Key);
            abortCount++;
        }
    }
    if (abortCount > 0)
        NinjaTrader.Code.Output.Process(
            "[DRAIN-ABORT-LEADER-FLAT] leader=" + o.Account.Name
            + " instr=" + instrFull
            + " aborted=" + abortCount, ...);
}
```

Provenance: DW-B156-01 was pre-committed and documented through B156 pipeline (FINAL_PASS
2026-09-14). DW-B156-DEFER-1 (CYC=14 decomposition) was closed in B157-T2 (FINAL_PASS
2026-08-10). The inline block at original L1934-1957 was extracted into IsLeaderFlatDrainAbort.

### 3.2 Hook Point Analysis

**Candidate hook in brief:** TryClearLeaderDirectionOnFlat
**Confirmed correct hook:** TryEvictFollowerBeSlot -> IsLeaderFlatDrainAbort

Reason TryClearLeaderDirectionOnFlat is NOT the correct hook:

```
OnOrderUpdate (NT8 bg thread):
  L1712: TryEvictFollowerBeSlot(e)        <-- IsLeaderFlatDrainAbort fires here (pre-Gate-1)
  L1763: if (!TryResolveEnabledRule(...)) return;   <-- Gate 1
  L1767: TryFirePositionState(e)          <-- TryClearLeaderDirectionOnFlat fires HERE (post-Gate-1)
```

TryClearLeaderDirectionOnFlat (L4741) fires from TryFirePositionState (L4690), which is invoked
at L1767 AFTER Gate 1 (L1763). If no matching rule exists, Gate 1 returns early and
TryClearLeaderDirectionOnFlat never fires. For drain abort purposes, firing before Gate 1
is strictly better. IsLeaderFlatDrainAbort via TryEvictFollowerBeSlot (L1712) is the
correct and already-implemented hook.

TryClearLeaderDirectionOnFlat also performs different cleanup: _lastLeaderDirection removal +
ClearLiveEntryForInstrument. It is NOT the drain abort path.

### 3.3 Key Format Confirmed

From CopyEngine.cs L384-388:
```csharp
// DW-NEW-08 Option D: cancel-before-dispatch drain state.
// Key = follower account name. ConcurrentDictionary: no lock (JS-021).
// StringComparer.Ordinal: deterministic key comparison.
private readonly ConcurrentDictionary<string, PendingDispatchDrain> _pendingDispatchDrains =
    new ConcurrentDictionary<string, PendingDispatchDrain>(StringComparer.Ordinal);
```

Key format: `follower acc.Name` (NOT leader name). Confirmed matches the Fix3 spec.

### 3.4 AbortDrainOnFill Signature Confirmed

From CopyEngine.cs L8343:
```csharp
// CYC=3: base(1) + TryRemove guard(1) + foreach(1). JS-021: no lock().
private void AbortDrainOnFill(string acctKey)
{
    if (_pendingDispatchDrains.TryRemove(acctKey, out var payload))
        foreach (var id in payload.DrainedOrderIds)
            _drainOwnedOrderIds.TryRemove(id, out _);
}
```

Signature: `private void AbortDrainOnFill(string acctKey)`. CYC=3. No lock.

### 3.5 T1 Implementation Design

**T1 is VERIFICATION-ONLY. Zero code changes.**

The DW-B156-01 Fix3 is already in production source as IsLeaderFlatDrainAbort. The T1
engineer task is:

1. **Source verification** (SCAN-07 proxy): Read CopyEngine.cs lines 1929-1974.
   Confirm IsLeaderFlatDrainAbort:
   - Called unconditionally from TryEvictFollowerBeSlot at line 1938
   - Guards: not follower, Filled state, IsFlat
   - Iterates _pendingDispatchDrains by instrument match
   - Calls AbortDrainOnFill(kv.Key) for each match
   - Logs [DRAIN-ABORT-LEADER-FLAT] when abortCount > 0

2. **Existing test confirmation**: Verify CopyEngineTests.cs line 9481
   `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString` is present
   and PASSES. This test was added by B157-T3 (DW-B156-DEFER-3).

3. **SIM gate execution**: Run 5-step SIM (see §5).

4. **Closure**: If SIM PASS, DW-B156-01 is CLOSED. Document in completion report.
   If SIM FAIL, do NOT implement a new fix within B158. Create a defect card for
   escalation (see §7 Risk/Dependency notes).

### 3.6 QX Drain Path Note

The brief states Fix3 also protects the QX drain path. IsLeaderFlatDrainAbort fires on
ANY leader account fill that leaves the leader flat -- regardless of whether the exit was
via BE stop, QX stop, or manual close. The instrument-match loop covers all drain entries.
No QX-specific changes are needed.

### 3.7 Threading Model

IsLeaderFlatDrainAbort fires on the NT8 background thread (same thread as OnOrderUpdate).
All operations are lock-free:
- IsFollowerAccount: reads _rules (no mutation, safe read)
- FindPosition / IsFlat: NT8 AddOnBase Position query (thread-safe read)
- _pendingDispatchDrains.foreach: ConcurrentDictionary lock-free snapshot
- AbortDrainOnFill: _pendingDispatchDrains.TryRemove + _drainOwnedOrderIds.TryRemove (lock-free)
- Output.Process: NT8-safe from any thread

No Dispatcher.InvokeAsync needed (no UI updates).
No lock() anywhere. JS-021 compliant.

### 3.8 CYC Budget

| Method | CYC | Status |
|--------|-----|--------|
| IsLeaderFlatDrainAbort | 8 | AT LIMIT. No changes permitted without CYC analysis. |
| TryEvictFollowerBeSlot | 7 | UNDER LIMIT. |
| AbortDrainOnFill | 3 | UNDER LIMIT. |

No new methods. No CYC changes. All within JS-013 CYC<=8 constraint.

---

## 4. T2 Architecture: DW-B157-COMMENT-01 Stale Comment

### 4.1 Location Confirmed

From CopyEngine.cs L6516-6527 (verified by source read):

```csharp
// B119: DW-B128 -- direction-change guard predicate.
// Returns true iff the current dispatch reverses the last direction AND the follower is flat.  <-- LINE 6517 (STALE)
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

### 4.2 Comment Incorrectness

The method returns `true` when `currentAction != lastAction && !followerIsFlat`, i.e., when:
- The direction changed (reversal), AND
- `followerIsFlat` is **false** (follower is NOT flat, i.e., follower has an open position)

Returning `true` means: "skip this dispatch" (protection path). The skip fires when the
follower has an OPEN position and the dispatch would reverse direction.

Current comment says "the follower is flat" -- this is the PRE-FIX (pre-B157) semantics
when the condition was `&& followerIsFlat` (reversed by DW-B157-RG-01).

### 4.3 Change Description

**File**: src/PropTraderTools/CopyEngine.cs
**Line**: 6517
**Change type**: Comment text replacement. Zero executable code change.

OLD (line 6517):
```csharp
// Returns true iff the current dispatch reverses the last direction AND the follower is flat.
```

NEW (line 6517):
```csharp
// Returns true iff the current dispatch reverses the last direction AND the follower is NOT flat (open position + reversal = protection path).
```

No other lines change. No logic change. No CYC change.

### 4.4 Constraints

- ASCII-only: new comment text uses only ASCII characters (0x20-0x7E). PASS.
- No lock(): annotation change. N/A.
- DateTime.Now: N/A.
- CYC: unchanged (0 executable lines modified).
- No new tests needed: annotation-only change with no behavioral impact.

---

## 5. Test Strategy for T1

### 5.1 Existing Unit Test Contract

The following test MUST pass (no regression from T1 verification):

| Test name | File | Line | Assertion |
|-----------|------|------|-----------|
| TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString | CopyEngineTests.cs | 9481 | Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src) |

This test was added by B157-T3 (DW-B156-DEFER-3). It validates the log marker string
matches the production code verbatim. No Skip attribute. Must pass via `dotnet test`.

Since T1 makes zero code changes, this test is expected to pass without modification.

### 5.2 SIM Verification Steps

The following 5-step SIM gate confirms DW-B156-01 in a live NT8 SIM environment.

**Setup**: 1 leader account (Sim101) + 2 follower accounts (Sim103, Sim104). ATM strategy
enabled. CopyEngine copy mode active for the instrument under test.

**CORRECT LOG MARKER TO CHECK**: `[DRAIN-ABORT-LEADER-FLAT]`
(NOT `[DRAIN-ABORT-FLAT]` -- that is the B155/B156 SubmitDrainedEntry marker. Different fix.)

| Step | Action | PASS Criteria | FAIL Criteria |
|------|--------|---------------|---------------|
| 1 | Enter Long on leader (Sim101) using ATM strategy | Followers (Sim103, Sim104) receive Long copy entry. No [PTT-COPY-GUARD] in output. | Followers do not copy. |
| 2 | Trigger BE All (move price to stop level or use BE button) | Followers enter drain-cancel sequence. Leader's PTT-BE-Stop moves to break-even price. | BE All does not trigger. |
| 3 | Wait for leader's PTT-BE-Stop to fill (leader goes flat at break-even) | `[DRAIN-ABORT-LEADER-FLAT] leader=Sim101 instr=<instrument> aborted=N` appears in OutputTab1. N >= 1 when follower drains were pending. | Log absent = Fix3 not triggered or drain pre-consumed. See §7 for escalation path. |
| 4 | Check follower accounts (Sim103, Sim104) | No ghost re-entry: followers do NOT receive a new Long copy entry after leader is flat. Followers' positions are flat (or closing normally). | Ghost Sell Entry (or any new entry order) appears on Sim103/Sim104 after leader flat = ghost re-entry bug present. |
| 5 | Confirm no DRAIN-ABORT-FLAT (old Fix1) fires unexpectedly | `[DRAIN-ABORT-FLAT]` may or may not appear (Fix1 = follower-flat guard in SubmitDrainedEntry). Its presence means the follower was flat at submission time, which is a different scenario. | N/A (informational check). |

**PASS**: Step 3 shows [DRAIN-ABORT-LEADER-FLAT] + Step 4 shows no ghost entry. DW-B156-01 CLOSED.

**FAIL / TIMING RACE**: Step 3 log absent AND Step 4 shows ghost entry. This indicates
the follower cancel acks completed before the leader fill event was processed (pre-consume
race). See §7 for escalation path.

### 5.3 No New Unit Tests for T2

T2 is annotation-only. No behavioral change. No new unit tests required.
SCAN-06 (zero new test failures) passes trivially.

---

## 6. 7-Scan Checklist Requirements

### T1 (Verification-only, zero code changes)

| Scan | Requirement | Implementation Path |
|------|-------------|---------------------|
| SCAN-01 | No non-ASCII bytes in CopyEngine.cs | `[System.IO.File]::ReadAllBytes(...) | Where {$_ -gt 127} | Measure-Object` -> Count=0 |
| SCAN-02 | No `lock(` in executable code in CopyEngine.cs | `Select-String -Pattern '^\s+lock\s*\(' CopyEngine.cs` -> 0 hits |
| SCAN-03 | No `DateTime.Now` in executable code in CopyEngine.cs | `Select-String -Pattern '\bDateTime\.Now\b' CopyEngine.cs` -> 0 executable hits |
| SCAN-04 | All new/modified methods CYC<=8 | N/A: no methods modified. IsLeaderFlatDrainAbort CYC=8 (pre-existing, at limit). |
| SCAN-05 | No `async void` in executable code | `Select-String -Pattern 'async void' CopyEngine.cs` -> 0 executable hits |
| SCAN-06 | Zero new test failures | `dotnet test` must show `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString` PASSED. Zero new failures vs B157 baseline. |
| SCAN-07 | Zero build errors + every diff line traces to DW-B156-01 | Zero code changes = empty diff. Build must PASS. |

### T2 (Comment-only change at L6517)

| Scan | Requirement | Implementation Path |
|------|-------------|---------------------|
| SCAN-01 | No non-ASCII bytes in CopyEngine.cs | `[System.IO.File]::ReadAllBytes(...) | Where {$_ -gt 127} | Measure-Object` -> Count=0. New comment text must be ASCII-only. |
| SCAN-02 | No `lock(` in executable code | `Select-String -Pattern '^\s+lock\s*\(' CopyEngine.cs` -> 0 hits. (Comment change cannot introduce lock.) |
| SCAN-03 | No `DateTime.Now` | `Select-String -Pattern '\bDateTime\.Now\b' CopyEngine.cs` -> 0 executable hits. |
| SCAN-04 | CYC unchanged | No executable code modified. CYC for IsReversalToFlatFollower remains 2. Verify via comment: `// CYC=2`. |
| SCAN-05 | No `async void` | N/A for comment change. |
| SCAN-06 | Zero new test failures | `dotnet test` must pass. Particularly: all IsReversalToFlatFollower tests at CopyEngineTests.cs L8103-8175 must PASS. |
| SCAN-07 | Zero build errors + diff is comment-only | Build must PASS. `git diff CopyEngine.cs` must show ONLY line 6517 changed (old comment -> new comment). No other lines. |

---

## 7. Risk and Dependency Notes

### Risk 1: T1 SIM FAIL (Timing Race)

**Description**: If the follower cancel-ack sequence completes before the NT8 background thread
processes the leader's fill event, the drain will be consumed (SubmitDrainedEntry called) before
IsLeaderFlatDrainAbort fires. In this case, `abortCount=0` and [DRAIN-ABORT-LEADER-FLAT] will
not appear in output. The ghost entry will still be submitted.

**Detection**: Step 3 log absent AND Step 4 ghost entry present in the SIM gate.

**Escalation path if SIM FAIL**: The timing race is NOT addressable within B158 scope.
The engineer must:
1. Document the failure in completion report with exact output evidence
2. Create defect card `DW-B158-DRAIN-RACE-01` with reproduction steps
3. Forward to Director for prioritization

The architectural fix for a timing race would be a leader-flat guard inside
SubmitDrainedEntry itself (checking whether any rule leader for the payload instrument
is flat at the moment of submission). This requires a new private helper and CYC analysis,
making it out of scope for B158's verification ticket.

**Note on 2026-09-13 SIM evidence**: The brief cites `[DRAIN-ABORT-FLAT]` absent from that SIM.
`[DRAIN-ABORT-FLAT]` is Fix1's marker (SubmitDrainedEntry follower-flat guard, L8319). It is
DIFFERENT from Fix3's marker `[DRAIN-ABORT-LEADER-FLAT]`. The 2026-09-13 SIM may have been
checking the wrong marker. The T1 SIM gate must check for `[DRAIN-ABORT-LEADER-FLAT]` specifically.

### Risk 2: IsLeaderFlatDrainAbort CYC=8 at Limit

IsLeaderFlatDrainAbort is at CYC=8 (the JS-013 limit). If any future change to this method
is needed (e.g., adding a QX-specific path), it must be extracted into a sub-helper first.
No changes needed by T1.

### Risk 3: T2 Line Drift

The IsReversalToFlatFollower comment is currently at line 6517. If T1's source verification
requires any edits to the file (e.g., adding attribution comments), lines could shift. Since T1
is VERIFICATION-ONLY (zero code changes), T2 can proceed with line 6517 as the target.

If B158 pipeline runs T1 and T2 in separate sessions and T1 somehow creates a diff (should not
happen), T2 engineer must re-confirm the line number before editing.

### Risk 4: Deferred Items Not Addressed

The following items remain OPEN after B158 and are NOT in scope:

| ID | Item | Priority | Status |
|----|------|----------|--------|
| DW-B154-05 | IsPttCopyEntryFilled delegation refactor | P3 | STILL OPEN -- not touched |
| DW-B154-06 | TryTriggerBeRecovery post-deployment monitor | P2 | STILL OPEN -- no new evidence |
| DW-B155-01 | B155 [DRAIN-ABORT-FLAT] SIM gate pending | P2 | STILL OPEN -- B158 T1 is a DIFFERENT SIM gate |
| DW-BUG-0911-A-01 | Mid-close race in OnOrderUpdate | P2 | STILL OPEN -- not addressed |

Note: DW-B155-01 and B158 T1 SIM are SEPARATE gates. DW-B155-01 is the SIM gate for B155's
Fix (SubmitDrainedEntry flat guard, marker [DRAIN-ABORT-FLAT]). B158 T1 is the SIM gate for
B156's Fix3 (IsLeaderFlatDrainAbort, marker [DRAIN-ABORT-LEADER-FLAT]).

### Risk 5: NT8 API Compliance (No New API Calls)

T1 introduces zero new code. T2 is annotation-only. No NT8 API compliance checks
are needed beyond the standard file-wide scans.

Key NT8 facts (embedded per protocol, for pipeline reference):
- AtmStrategyChangeStopTarget() -- StrategyBase-only. NOT used.
- AtmStrategyCreate() -- StrategyBase-only. NOT used.
- Account.Change() -- AddOnBase available but NOT used.
- Account.Cancel() + CreateOrder() -- AddOnBase pattern. NOT used in this block.
- NinjaTrader.Code.Output.Process() -- AddOnBase-safe log. Used by IsLeaderFlatDrainAbort (pre-existing).

---

## 8. Method Signatures Reference

All signatures confirmed by source read. No memory reliance.

```csharp
// CopyEngine.cs:1929 -- thin router
private void TryEvictFollowerBeSlot(OrderEventArgs e)   // CYC=7

// CopyEngine.cs:1950 -- Fix3 implementation (ALREADY PRESENT)
private void IsLeaderFlatDrainAbort(Order o, string instrFull)   // CYC=8

// CopyEngine.cs:1980 -- follower eviction (pre-existing)
private void RunFollowerEvictPath(Order o)   // CYC=4

// CopyEngine.cs:8343 -- drain abort helper
private void AbortDrainOnFill(string acctKey)   // CYC=3

// CopyEngine.cs:8307 -- drain submission (Fix1 already present at L8316)
private void SubmitDrainedEntry(string acctKey)

// CopyEngine.cs:6520 -- direction-guard predicate (T2 target)
internal static bool IsReversalToFlatFollower(
    OrderAction currentAction,
    OrderAction lastAction,
    bool followerIsFlat
)   // CYC=2
```

---

## Summary of Architectural Decisions

1. **T1 is verification-only**: Fix3 (IsLeaderFlatDrainAbort) is already implemented at CopyEngine.cs:1950. Zero code change required unless SIM gate fails.

2. **Correct hook is TryEvictFollowerBeSlot (pre-Gate-1)**: NOT TryClearLeaderDirectionOnFlat (post-Gate-1). TryClearLeaderDirectionOnFlat fires from TryFirePositionState at L1767, after Gate 1 at L1763. IsLeaderFlatDrainAbort fires at L1712, before Gate 1. The existing hook is strictly better.

3. **Key format confirmed**: _pendingDispatchDrains key = follower acc.Name (StringComparer.Ordinal). The abort loop iterates all follower drains by instrument match.

4. **Correct SIM marker**: `[DRAIN-ABORT-LEADER-FLAT]` (not `[DRAIN-ABORT-FLAT]`). The 2026-09-13 SIM may have checked the wrong marker.

5. **T2 is annotation-only**: Single comment line at L6517. Zero logic, zero CYC, zero tests.

6. **Lanes approved**: T1 and T2 are independent. T1 = pre-Gate-1 region. T2 = L6517 region. No shared code paths.

---

_Architecture plan authored by ptt-architect (Phase 1). Block B158._
_Source verified: CopyEngine.cs lines 385-395, 1712, 1763, 1767, 1929-1974, 4690-4757, 6516-6527, 8307-8348._
_Prior block docs read: docs/brain/B156/06-deferred-backlog.md, docs/brain/B157/06-deferred-backlog.md, docs/brain/B157/05-final-review.md, docs/brain/B156/ticket-1-completion.md, docs/brain/B156/ticket-1-verification.md._
