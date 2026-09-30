# PR-20-deferred Fix Queue
# Wave 7 NEEDS_DIRECTOR items -- deferred from Phase 7 Lane L1 (PR #20)
# Branch to create: wave7/pr20-deferred-repairs (fresh from main)
# Source record: docs/brain/wave7-pr-repairs/PR-20/lane-L1-complete.md

---

## EXCLUDED -- promote to Wave 8 epic (do NOT fix in this lane)

### F6 -- PropagateMasterTargetMove FSM routing
- **File**: src/V12_002.Orders.Callbacks.Propagation.cs L490-547
- **Reason**: Non-trivial 4-step architectural change (build FollowerTargetReplaceSpec,
  store CancellingOrderId, cancel-only, await Phase 2 on cancel confirm). Live position
  exposure risk if done incorrectly. The existing Cancel+CreateOrder+Submit path is
  intentional and documented at L551-555.
- **Action**: Create EPIC-W8-PR20-F6 in Wave 8 roadmap. Do not touch in this lane.

---

## FINDING 1 -- NEW-F3: CaptureTargetSnapshot / RefreshTargetSnapshot deduplication

**File**: src/V12_002.Orders.Management.StopSync.cs
**Classification**: VALID-MECHANICAL
**OKF Rule**: Rule 7 -- zero-alloc hot path (no redundant allocation per call)
**Priority**: P3

**Issue**: CodeRabbit finding. CaptureTargetSnapshot and RefreshTargetSnapshot may
rebuild the same snapshot data redundantly. If RefreshTargetSnapshot is called
immediately after CaptureTargetSnapshot (or vice versa) within the same call chain,
the snapshot is constructed twice for no reason.

**Fix approach**: Read the live implementations of both methods. If one calls the
other or if their results are identical in content, consolidate: have one method
delegate to the other, or extract the shared snapshot-build logic into a private
helper that both call. Eliminate the redundant allocation.

**Constraint**: CYC of both methods must remain <=8 after fix. No lock() permitted.
Minimal change only -- do not restructure unrelated logic.

---

## FINDING 2 -- NEW-F5: OrderId fallback in PurgeFollowerStopScanStopOrders

**File**: src/V12_002.Orders.Callbacks.AccountOrders.cs
**Classification**: VALID-LOGIC-BUG
**OKF Rule**: Rule 5 -- production safety (independent_tracking, no ghost orders)
**Priority**: P2

**Issue**: PurgeFollowerStopScanStopOrders matches follower stop orders using object
reference equality. NinjaTrader's broker layer can return a new Order object instance
for the same logical order (e.g. after a connection drop/reconnect). When this happens,
the reference match fails silently -- the follower stop is not purged, leaving a ghost.

**Fix approach**: Read the live method. Where it matches orders by reference, add an
OrderId-based fallback: if `ReferenceEquals(tracked, candidate)` fails, fall back to
`tracked?.OrderId != null && tracked.OrderId == candidate?.OrderId`. Use the same
pattern used elsewhere in AccountOrders.cs for order matching.

**Constraint**: CYC must remain <=8. No lock(). Null-safe -- both sides of the
OrderId comparison must guard against null.

---

## FINDING 3 -- NEW-F6: Cascade suppression active-state guard

**File**: src/V12_002.Orders.Callbacks.AccountOrders.cs
**Classification**: VALID-LOGIC-BUG
**OKF Rule**: Rule 5 -- production safety (defense in depth)
**Priority**: P2

**Issue**: ExecuteFollowerCascadeCleanup (or its suppression branch) does not verify
that the follower FSM is in an active state before suppressing the cascade. A follower
whose FSM is null or already in a terminal state (Filled, Cancelled, Rejected) could
be incorrectly suppressed instead of cleaned up, leaving the position in an
inconsistent state.

**Fix approach**: Read the live method and the suppression path it calls. Before
the suppression action fires, add a guard: if the follower FSM is null or its state
is terminal, skip suppression and proceed to cleanup instead. Define "terminal" as
the same state set used elsewhere in the file (check for Filled/Cancelled/Rejected
or whatever FollowerReplaceState.IsTerminal equivalent exists).

**Constraint**: CYC must remain <=8. No lock(). Guard must be a simple null-or-state
check -- do not restructure the cascade logic.

---

## FINDING 4 -- NEW-F7: Ghost-order window in stopOrders Enqueue path

**File**: src/V12_002.Orders.Management.StopSync.cs
**Classification**: VALID-LOGIC-BUG
**OKF Rule**: Rule 5 -- production safety (staleness_guard, no ghost orders)
**Priority**: P2

**Issue**: Stop orders are inserted into the tracking dictionary keyed or matched by
object reference before the broker confirms the order. If the broker returns a
different Order object instance for the same logical stop (reconnect scenario or
order replace), the original reference becomes a ghost -- it blocks future cancels
and stop replacements because the lookup by reference finds the stale object, not
the live one.

**Fix approach**: Read the live stop-order tracking insertion point in StopSync.cs.
Where stop orders are stored/looked up by object reference, switch to an OrderId
(string) key or add an OrderId reconciliation step on lookup: if the stored
reference differs from the broker-returned reference but OrderIds match, update
the reference in-place (ConcurrentDictionary TryUpdate pattern). This eliminates
the ghost window without changing the overall tracking architecture.

**Constraint**: CYC must remain <=8 in all modified methods. No lock() -- use
ConcurrentDictionary atomic ops (TryUpdate, TryGetValue, TryAdd). No new
allocations on the hot path.

---

## Gate Requirements (apply after all 4 findings fixed)

- [ ] `dotnet build Linting.csproj` -- 0 errors
- [ ] `python scripts/wave7_prepush_gate.py --base origin/main` -- GATE PASSED
- [ ] `dotnet csharpier check src/` -- 0 issues
- [ ] `powershell -File .\scripts\verify_pr_hygiene.ps1` -- PASS
- [ ] `Select-String -Pattern "lock\s*\(" src/ -Include "*.cs" -Recurse` -- 0 functional hits
- [ ] All modified methods CYC <= 8 (verify with complexity_audit.py)
- [ ] No DateTime.Now introduced
- [ ] No underscore-prefix locals introduced
- [ ] No Unicode characters introduced

---

## Artifacts to write

- docs/brain/wave7-pr-repairs/PR-20-deferred/triage.md (confirm each finding still present in main)
- docs/brain/wave7-pr-repairs/PR-20-deferred/verify-NEW-F3.md
- docs/brain/wave7-pr-repairs/PR-20-deferred/verify-NEW-F5.md
- docs/brain/wave7-pr-repairs/PR-20-deferred/verify-NEW-F6.md
- docs/brain/wave7-pr-repairs/PR-20-deferred/verify-NEW-F7.md
- docs/brain/wave7-pr-repairs/PR-20-deferred/repair-log.md
- docs/brain/wave7-pr-repairs/PR-20-deferred/lane-complete.md
