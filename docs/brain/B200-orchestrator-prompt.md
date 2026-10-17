# B200 Orchestrator Prompt

**Epic**: B200 — Follower Quantity Corruption + SFB Slate-Gate Miss + Lane E PartFilled Race
**Date**: 2026-10-14 (updated 2026-10-15)
**Prepared by**: copier-spec mode (code investigation + log analysis)
**Status**: TICKETS 1 & 2 SIM-CONFIRMED. TICKET 3 ADDED — CLOSE-LANED-01 PartFilled gap.

---

## Code Investigation Results (copier-spec confirmed)

### Bug 1 — B200-QTY-01: Follower Quantity Corruption

**Root cause confirmed in code** — `CopyEngine.cs` line 4508:

```
HandleEntryChange -> DrainThenDispatch(acc, instrument, fo.Quantity, ...)
```

`fo` is the existing follower working entry order (`FindFollowerEntryOrder`).
When the leader entry is drag-moved during or after a PartFill:
- `fo.Quantity` = remaining unfilled contracts on the follower entry order (NOT the original)
- This contaminated qty is stored in `PendingDispatchDrain.Qty`
- `SubmitDrainedEntry` (line 7320) reads `payload.Qty` and passes it into `SendCopyWithAtm`
- `SendCopyWithAtm` logs `[PTT-ATM-SEND] qty=X` — the corrupted value seen in SIM

SIM evidence:
- Sim102: `fo.Quantity`=5 (leader PartFilled 5 of 10) -> ATM-SEND qty=5
- Sim104: `fo.Quantity`=9 (different fill timing) -> ATM-SEND qty=9
- Sim103: processed on Filled event (qty=10 already stable) -> ATM-SEND qty=10 (correct)

**Note**: `DispatchCopy` (line 2752) also uses `order.Quantity` from the leader order directly.
For the initial dispatch this fires at `Accepted` state (pre-fill) so it is correct.
The corruption occurs exclusively through the drag/drain path (`HandleEntryChange`).

**`IsDispatchTriggerState`** (line 2612) gates on `Submitted` (market) and `Accepted`/`Working`
(limit). `PartFilled` is NOT a dispatch trigger — so `DispatchCopy` itself does not fire on
PartFill. The bug lives entirely in `HandleEntryChange -> DrainThenDispatch -> fo.Quantity`.

---

### Bug 2 — B200-SFB-NULL-01: Bracket Sync Slate-Gate Miss for Direct ATM Path

**Root cause confirmed in code** — `CopyEngine.cs` line 4231:

```csharp
if (_followerSlateReady.TryGetValue(follower.Name, out ready) && !ready)
    return null;
```

`_followerSlateReady` is a per-follower gate. After a bracket wipe, slate is set to `false`
by `TryMarkFollowerSlateNotReady` (line 1075). It is only reset to `true` by three paths:

| Path | Method | When |
|------|--------|------|
| Drain fill | `TrySetSlateReadyOnDrainFill` (line 1110) | Drain-owned PTT-Copy order fills |
| Position flat | `TrySetFollowerSlateReady` (line 1094) | Position confirmed flat, no active orders |
| Lane E re-arm | `TryLaneEHook` (line 2970) | Post-QX/BE re-arm path |

**The missing path**: `TryLaneDDispatchFollower` (line 2871, direct ATM dispatch for Clone mode)
calls `SendCopyWithAtm` successfully but **never sets `_followerSlateReady[acc.Name] = true`**.

Consequence:
- Sim103 used the drain path (`DrainThenDispatch` -> `SubmitDrainedEntry` -> its PTT-Copy entry
  filled -> `TrySetSlateReadyOnDrainFill` -> slate = true) -> `FindFollowerBracketOrder` returns Target1
- Sim102 and Sim104 used the direct ATM path (no drain, no working PTT-Copy entry to cancel):
  `TryLaneDDispatchFollower` -> `SendCopyWithAtm` -> slate NEVER set to true -> `FindFollowerBracketOrder` returns null

The log swap in cycle 2 (Sim103 fails, Sim104 succeeds) is explained by:
- Whether each follower had an existing entry to drain (stochastic based on order accumulation)
- Whichever follower happened to drain gets slate=true; others remain gated-out

---

## Confirmed Bug Summary

**SIM SESSION STATUS (2026-10-15 session, docs/output.md lines 1-387)**:
- B200-QTY-01: **SIM CONFIRMED FIXED** — lines 9-11: `qty=10` for all 3 followers; no qty corruption observed.
- B200-SFB-NULL-01: **SIM CONFIRMED FIXED** — lines 92-145: `fo=Target1/fo=Target2` for all 3 followers; `fo=NULL` absent.
- B200-NAKED-01: **SIM CONFIRMED FIXED** — lines 180+: PTT-Flatten fires correctly only after leader Close, not at entry.
- B200-CLOSE-LANED-01: **STILL FAILING** — lines 228-234: `[PTT-ATM-SEND] qty=7/6/6` fire during PTT-Flatten. Root cause: `HasActivePttFlatten` does not include `OrderState.PartFilled`. See Ticket 3.

| ID | Location | Line | Root Cause | Status |
|----|----------|------|------------|--------|
| B200-QTY-01 | `HandleEntryChange` | 4517 | `fo.Quantity` (remaining qty) used as drain payload qty | SIM CONFIRMED FIXED |
| B200-SFB-NULL-01 | `TryLaneDDispatchFollower` | 2911 | Missing `_followerSlateReady[acc.Name] = true` after direct ATM dispatch | SIM CONFIRMED FIXED |
| B200-NAKED-01 | `TryNakedDetect` | 7063 | NakedDetect fired on `"Entry"` fills before ATM brackets armed | SIM CONFIRMED FIXED |
| B200-CLOSE-LANED-01 | `HasActivePttFlatten` | 2968 | `OrderState.PartFilled` missing from in-flight guard | TICKET 3 — FIX PENDING |

---

## Pre-conditions for Ph1

Before the pipeline starts, the following must be confirmed:

- [ ] `NT8 compile clean` — `PropTraderTools.csproj` builds with 0 errors (run `msbuild` or F5 NT8)
- [ ] `PTT sync 19/0` — `powershell -File .\scripts\ptt-sync.ps1` reports 19 synced, 0 errors
- [ ] `P0 gate pass` — `grep -r "lock(" src/PropTraderTools/ --include="*.cs"` returns 0 results
- [ ] `async void gate` — `grep -rn "async void " src/PropTraderTools/ --include="*.cs"` returns 0 results
- [ ] `Rules catalog` — `docs/standards/jane-street/RULES_CATALOG.md` readable (UTF-8 clean)
- [ ] B200-diagnosis.md confirms: MGC DEC26, 3 followers, Clone mode, ATM template blank (`tmpl=`)

---

## Tickets

### Ticket 1 — B200-QTY-01: Fix Drag-Path Qty Corruption in HandleEntryChange

**File**: `src/PropTraderTools/CopyEngine.cs`

**Problem**:
`HandleEntryChange` at line 4508 passes `fo.Quantity` as the drain qty.
`fo` is the follower's existing working entry order. In NT8, `Order.Quantity` is
the REMAINING unfilled quantity when the order is in `PartFilled` or post-partial state.
Original order size = `fo.Quantity + fo.Filled`.

**Fix**:
Replace line 4508 in `HandleEntryChange`:
```csharp
// BEFORE (bug):
DrainThenDispatch(
    acc,
    instrument,
    fo.Quantity,       // BUG: remaining qty after partial fill
    newPrice,
    fo.OrderAction,
    fo.OrderType
);

// AFTER (fix):
DrainThenDispatch(
    acc,
    instrument,
    fo.Quantity + fo.Filled,   // FIX: original order size = remaining + filled
    newPrice,
    fo.OrderAction,
    fo.OrderType
);
```

**Why `fo.Quantity + fo.Filled`**:
- `Order.Quantity` in NT8 = remaining (unfilled) quantity at current state
- `Order.Filled` = contracts filled so far
- Sum = original submitted quantity (the leader's intent)
- For an unfilled order: `Filled=0`, sum = `Quantity` (no change -- correct)
- For a PartFilled order: sum = original full qty (correct fix)
- NT8_FULL_REFERENCE.md confirms this semantics (do not re-check -- already confirmed in diagnosis)

**CYC impact**: 0 delta. No new branch introduced.

**Required xUnit test**:
`T_B200_01_HandleEntryChange_PartFilledFollowerOrder_UsesOriginalQty`

Test scenario:
- Create a mock follower entry order with `Quantity=5, Filled=5` (PartFilled: 10 total)
- Trigger `HandleEntryChange` path (or call `DrainThenDispatch` directly via seam)
- Assert: drain payload qty = 10 (not 5)
- Assert: `SendCopyWithAtm` is called with `signal.Quantity = 10`

Place in: `src/PropTraderTools/Tests/B200Tests.cs`

---

### Ticket 2 — B200-SFB-NULL-01: Set Slate-Ready After Direct ATM Dispatch in TryLaneDDispatchFollower

**File**: `src/PropTraderTools/CopyEngine.cs`

**Problem**:
`TryLaneDDispatchFollower` (line 2897) calls `SendCopyWithAtm` and marks the lane-D dedup
dict (`_laneDDispatched`) but never sets `_followerSlateReady[acc.Name] = true`.
This leaves the slate-gate closed for the follower. When `SyncFollowerBracket` later calls
`FindFollowerBracketOrder`, the gate at line 4231 returns `null` immediately (fo=NULL).
Bracket sync is skipped. PTT-Flatten fires incorrectly, opening reverse positions.

**Fix**:
In `TryLaneDDispatchFollower`, after `SendCopyWithAtm` succeeds, add:

```csharp
// BEFORE (bug -- missing slate ready on Lane D success):
bool ok = (SendCopyWithAtm(acc, order.Instrument, in scaledSig, namedMode) != null);
if (ok)
{
    _laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0);
    if (_laneDDispatched.Count > LaneDEvictThreshold)
        _laneDDispatched.Clear();
}
return ok;

// AFTER (fix -- slate gate opened on ATM dispatch success):
bool ok = (SendCopyWithAtm(acc, order.Instrument, in scaledSig, namedMode) != null);
if (ok)
{
    _laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0);
    if (_laneDDispatched.Count > LaneDEvictThreshold)
        _laneDDispatched.Clear();
    _followerSlateReady[acc.Name] = true;   // FIX: open bracket-sync gate for direct ATM path
}
return ok;
```

**Why `true` is safe here**:
- Slate-ready = true means "bracket sync is allowed to search for this follower's orders"
- At the moment `SendCopyWithAtm` returns non-null, the ATM order has been submitted to NT8
- The ATM strategy will place bracket orders (Target1/Target2/Stop1/Stop2) shortly after
- Setting true at this point mirrors the drain-fill path (`TrySetSlateReadyOnDrainFill` line 1110)
- The NOT-ready (false) state is set by `TryMarkFollowerSlateNotReady` before each bracket wipe
  (i.e., BEFORE a new entry is dispatched, not AFTER) -- so setting true here cannot re-open
  a stale wipe cycle

**CYC impact**: 0 delta. `_followerSlateReady[acc.Name] = true` is a dictionary write, not a branch.

**Required xUnit test**:
`T_B200_02_LaneDDispatch_SetsSlateReady_AfterAtmSuccess`

Test scenario:
- Set up a follower with `_followerSlateReady[acc.Name] = false` (simulating post-wipe state)
- Call `TryLaneDDispatchFollower` (or exercise via `TryLaneDHook` test seam) with a mock ATM
- Assert: `_followerSlateReady[acc.Name] == true` after successful `SendCopyWithAtm`
- Assert: `FindFollowerBracketOrder` called after the dispatch does NOT return null for a Working bracket

Place in: `src/PropTraderTools/Tests/B200Tests.cs`

---

### Ticket 3 — B200-CLOSE-LANED-01: Add PartFilled to HasActivePttFlatten Guard

**File**: `src/PropTraderTools/CopyEngine.cs`

**SIM Evidence (2026-10-15, docs/output.md lines 154-234)**:

```
Lines 154-169: Leader ATM brackets cancel (SimStop1/Target1/Target2 CancelPending/CancelSubmitted)
Lines 170-179: Leader Close order PartFills (Close state=PartFilled x2)
Lines 180-227: PTT-Flatten fires for all 3 followers (Accepted/Working callbacks)
Lines 228-229: [PTT-ATM-SEND] acct=Sim102 qty=7 / acct=Sim103 qty=6  <-- WRONG (Lane E re-arm)
Line  234:     [PTT-ATM-SEND] acct=Sim104 qty=6  <-- WRONG (Lane E re-arm)
Lines 243-290: New bracket orders from rogue Lane E entries (Target1/Target2 Working)
Lines 291-374: Second wave of PTT-Flatten fires (cascade from rogue entries)
```

**Root Cause Confirmed in Code**:

`HasActivePttFlatten` at `CopyEngine.cs` line 2962 checks:
```csharp
if (o.OrderState == OrderState.Submitted
    || o.OrderState == OrderState.Accepted
    || o.OrderState == OrderState.Working)
    return true;
```

**`OrderState.PartFilled` is NOT included.**

The PTT-Flatten sell order partially fills (e.g., sells 3 of 10 contracts). Its state transitions:
`Working -> PartFilled`. The follower position update fires with remaining qty=7.
`OnFollowerPositionUpdate` -> `TryLaneEHook` -> `IsLaneEEligible` check (6) calls `HasActivePttFlatten`.
The PTT-Flatten is in `PartFilled` state (not Working/Accepted/Submitted) -> returns false.
`IsLaneEEligible` returns true -> Lane E fires -> new "Entry" ATM placed for qty=7.

This creates competing orders: PTT-Flatten still working (selling remaining 7), while Lane E places
a new long Entry for 7. The position becomes mixed/reversed.

**The same gap exists in `FlattenOneAccount` L5561-5575** (the inline guard that `HasActivePttFlatten`
was extracted from). Both must be fixed together.

**Fix**:

In `HasActivePttFlatten` at line 2968, add `OrderState.PartFilled` to the active-state check:

```csharp
// BEFORE (bug -- PartFilled not treated as in-flight):
if (o.OrderState == OrderState.Submitted
    || o.OrderState == OrderState.Accepted
    || o.OrderState == OrderState.Working)
    return true;

// AFTER (fix -- PartFilled is still an in-flight order):
if (o.OrderState == OrderState.Submitted
    || o.OrderState == OrderState.Accepted
    || o.OrderState == OrderState.Working
    || o.OrderState == OrderState.PartFilled)  // FIX: PTT-Flatten partial fill is still in-flight
    return true;
```

In `FlattenOneAccount` at line 5567-5571, apply the same addition:

```csharp
// BEFORE:
if (
    o.OrderState == OrderState.Submitted
    || o.OrderState == OrderState.Accepted
    || o.OrderState == OrderState.Working
)

// AFTER:
if (
    o.OrderState == OrderState.Submitted
    || o.OrderState == OrderState.Accepted
    || o.OrderState == OrderState.Working
    || o.OrderState == OrderState.PartFilled  // FIX: mirrors HasActivePttFlatten fix
)
```

**Why `PartFilled` is safe to block on**:
- A `PartFilled` PTT-Flatten order has already sent part of the flatten. It will complete (fill remaining)
  or be cancelled. In either case, Lane E must NOT re-arm an ATM during this window.
- Adding `PartFilled` to the guard makes `HasActivePttFlatten` return true for the full lifecycle
  of a PTT-Flatten order: Submitted -> Accepted -> Working -> PartFilled -> Filled.
- Once fully Filled, the guard returns false (correct: flatten complete, no active order).
- This is identical to the semantic used by `TryNakedDetect` B200-NAKED-01 fix: skip during fill.

**CYC impact**: +1 delta in `HasActivePttFlatten` (new `||` branch).
CYC was 3 pre-fix; becomes 4. Budget <= 8. PASS.
In `FlattenOneAccount` inline guard: same +1 delta. FlattenOneAccount CYC was 7; becomes 8. Budget = 8. PASS (exactly at limit).

**Required xUnit test**:
`T_B200_03_HasActivePttFlatten_PartFilledOrder_ReturnsTrueBlocksLaneE`

Test scenario:
- Create a mock PTT-Flatten order in `OrderState.PartFilled` for account Sim102 on MGC DEC26
- Call `HasActivePttFlatten(acc, instr)` directly (it is `private static` -- expose via test seam or `InternalsVisibleTo`)
- Assert: returns `true` (was returning `false` before fix -- the bug)
- Secondary: call `IsLaneEEligible` with `_followerSlateReady=false`, `pos.Quantity=7`, and
  a PartFilled PTT-Flatten order in acc.Orders
- Assert: `IsLaneEEligible` returns `false` (Lane E correctly blocked)

Place in: `src/PropTraderTools/Tests/B200Tests.cs`

**Secondary Gap (non-blocking for Ticket 3)**:
`TryLaneEHook` is called from `OnFollowerPositionUpdate` which is an account event handler.
It is NOT gated on `_isCopyEnabled`. If the user disables copy mid-session, Lane E could still
fire. This is a lower-priority gap (does not cause financial harm in this scenario since
`PassesDailyCapCheck` provides an outer guard). Document in deferred backlog for B201.

---

## Pipeline Chain (MANDATORY — all 7 phases, none skippable, none combinable)

**NOTE: Tickets 1 and 2 are SIM-CONFIRMED FIXED. The pipeline for Ticket 3 is the active work.**

```
Ph1  ptt-architect       -> docs/brain/B200/02-architecture-plan.md
Ph2  ptt-plan-reviewer   -> docs/brain/B200/02-plan-review.md        (REVIEW_PASS gate)
Ph3  ptt-architect       -> docs/brain/B200/04-tickets.md
Ph3.5 ptt-ticket-reviewer -> docs/brain/B200/04-ticket-review.md     (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src .cs edits + docs/brain/B200/ticket-1-completion.md
                                           + docs/brain/B200/ticket-2-completion.md
                                           + docs/brain/B200/ticket-3-completion.md
Ph4b ptt-verifier        -> docs/brain/B200/ticket-1-verification.md (VERIFY_PASS gate)
                             docs/brain/B200/ticket-2-verification.md (VERIFY_PASS gate)
                             docs/brain/B200/ticket-3-verification.md (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> docs/brain/B200/05-final-review.md
                             docs/brain/B200/06-deferred-backlog.md
```

No shortcuts. No direct .cs edits outside Ph4a. Director explicit approval required for any
deviation from this chain.

---

## Jane Street Constraints (ALL mandatory)

| Rule | Constraint | Check |
|------|-----------|-------|
| JS-021 | No `lock()` in any new or modified code | `grep -r "lock(" src/PropTraderTools/ --include="*.cs"` = 0 |
| JS-033 | No `async void` (non-event-handler) | `grep -rn "async void " src/PropTraderTools/ --include="*.cs"` = 0 |
| JS-066 | CYC <= 8 per method | Ticket 1 delta=0; Ticket 2 delta=0. Verify with Lizard or manual McCabe |
| JS-001 | No throw in hot path | No new `throw` in `HandleEntryChange` or `TryLaneDDispatchFollower` |
| xUnit only | No NUnit/MSTest in new test file | `using Xunit;` only; no `[TestMethod]`, no `[Test]` |
| ASCII only | No Unicode/emoji/curly-quotes in .cs | All string literals in fix are ASCII-only |
| PTT-prefix | All signal names must start with `PTT-` | No new signal names introduced in these tickets |

---

## Ph4a Engineer Notes

### On Ticket 1 (QTY fix):

1. The fix is at `HandleEntryChange` line ~4508, inside the `DrainThenDispatch(...)` call.
   Only the `qty` parameter changes: `fo.Quantity` -> `fo.Quantity + fo.Filled`.

2. Do NOT touch `DispatchCopy` line 2752. That path reads `order.Quantity` from the leader
   order at `Accepted` state — that is correct (pre-fill, Quantity = original size).

3. Do NOT touch `SendCopyWithAtm`. The fix is upstream at the point where drain payload is
   constructed.

4. After the fix, run `CopyEngineTests` full suite (existing) to confirm no regressions.

### On Ticket 2 (SFB-NULL fix):

1. The fix is one line added to `TryLaneDDispatchFollower`, inside the `if (ok)` block.
   `_followerSlateReady[acc.Name] = true;` — ConcurrentDictionary indexer write, lock-free (JS-021).

2. Do NOT add a new branch. The assignment goes INSIDE the existing `if (ok)` block, so
   CYC delta = 0.

3. Do NOT modify `FindFollowerBracketOrder` or `OrderPassesBracketGate`. The slate-gate is
   the correct abstraction; the bug is that direct ATM dispatch does not set it.

4. After the fix, verify SIM: `[TP4-SFB]` log should show `fo=Target1` (not `fo=NULL`) for
   all three followers after Clone ATM dispatch.

---

## Ph4b Verifier Checklist

For each ticket, VERIFY_PASS requires ALL of the following:

**Ticket 1 (QTY)**:
- [ ] `T_B200_01_HandleEntryChange_PartFilledFollowerOrder_UsesOriginalQty` passes
- [ ] No existing test regressions (`dotnet test` clean)
- [ ] `grep "fo.Quantity" src/PropTraderTools/CopyEngine.cs` shows `fo.Quantity + fo.Filled` at line ~4508
- [ ] `grep -c "lock(" src/PropTraderTools/CopyEngine.cs` = 0 new occurrences
- [ ] CYC of `HandleEntryChange` <= 8 (was 6 pre-fix; delta=0 -> still 6)
- [ ] Ph4b deletion audit: no symbols deleted without DW-card authorisation
- [ ] Single-hunk ban: `git show HEAD -- CopyEngine.cs | grep "^@@" | wc -l` > 1 (not a full-file replace)
- [ ] Guardian symbol script: `powershell -File .\scripts\verify_critical_symbols.ps1` PASS

**Ticket 2 (SFB-NULL)**:
- [ ] `T_B200_02_LaneDDispatch_SetsSlateReady_AfterAtmSuccess` passes
- [ ] No existing test regressions (`dotnet test` clean)
- [ ] `grep "_followerSlateReady\[acc.Name\] = true" src/PropTraderTools/CopyEngine.cs` shows new line in `TryLaneDDispatchFollower`
- [ ] `grep -c "lock(" src/PropTraderTools/CopyEngine.cs` = 0 new occurrences
- [ ] CYC of `TryLaneDDispatchFollower` <= 8 (was 4 pre-fix; delta=0 -> still 4)
- [ ] Ph4b deletion audit: no symbols deleted without DW-card authorisation
- [ ] Single-hunk ban: confirmed not a full-file replace
- [ ] Guardian symbol script: `powershell -File .\scripts\verify_critical_symbols.ps1` PASS

**Ticket 3 (CLOSE-LANED-01 — PartFilled gap)**:
- [ ] `T_B200_03_HasActivePttFlatten_PartFilledOrder_ReturnsTrueBlocksLaneE` passes
- [ ] No existing test regressions (`dotnet test` clean)
- [ ] `grep "PartFilled" src/PropTraderTools/CopyEngine.cs` shows `OrderState.PartFilled` in BOTH `HasActivePttFlatten` AND `FlattenOneAccount` inline guard
- [ ] `grep -c "lock(" src/PropTraderTools/CopyEngine.cs` = 0 new occurrences
- [ ] CYC of `HasActivePttFlatten` <= 8 (was 3 pre-fix; +1 delta -> 4. Budget=8. PASS)
- [ ] CYC of `FlattenOneAccount` <= 8 (was 7 pre-fix; +1 delta -> 8. Exactly at budget. PASS)
- [ ] Ph4b deletion audit: no symbols deleted without DW-card authorisation
- [ ] Single-hunk ban: confirmed not a full-file replace (diff must show 2 separate hunks: one in HasActivePttFlatten, one in FlattenOneAccount)
- [ ] Guardian symbol script: `powershell -File .\scripts\verify_critical_symbols.ps1` PASS

---

## Ph5 Final Reviewer Checklist

- [ ] All 3 VERIFY_PASS gates issued (ticket-1-verification.md, ticket-2-verification.md, ticket-3-verification.md)
- [ ] SIM re-test: follower qty = leader qty (10) for all three accounts after drag+partial-fill
- [ ] SIM re-test: `[TP4-SFB-NULL]` does NOT appear after Clone ATM dispatch
- [ ] SIM re-test: no `[PTT-ATM-SEND]` appears AFTER leader Close (Lane E must NOT fire during flatten)
- [ ] SIM re-test: after director closes position via Positions tab, all 3 followers go flat. No new ATM entries. No reverse positions.
- [ ] PTT sync clean: `powershell -File .\scripts\ptt-sync.ps1` 19/0
- [ ] `docs/brain/B200/06-deferred-backlog.md` updated with deferred items (including Lane E _isCopyEnabled gap for B201)
- [ ] `specs/002-trade-copier-spec.html` bug log section updated with B200 block: all 4 DW cards, SIM-CONFIRMED status for Tickets 1-3

---

## Context for Ph1 Architect

**What was already tried and why it is not the fix**:

- B199 fixed ATM qty divide (`B199-R1`): removed the `H2 divide` in `SendCopyWithAtm`.
  That was a different bug (wrong denominator). B200 is a NEW distinct bug upstream.

- `DispatchCopy` qty read (line 2752) is NOT the bug for this cycle. The corruption comes
  from `HandleEntryChange -> DrainThenDispatch` using `fo.Quantity` from a partially-filled
  follower entry order (not from the leader order).

- `FindFollowerBracketOrder` logic (the 4-arg overload) and `OrderPassesBracketGate` are
  NOT the bug. The slate-gate at line 4231 is working correctly. The missing piece is that
  `TryLaneDDispatchFollower` does not set the slate to ready after a successful ATM dispatch.

**Deferred items (out of scope for B200)**:

- `ordCount=26` stale order accumulation: not a functional bug (search is O(n) but
  correctly finds or fails to find orders). Addressed when slate-gate is fixed.
- `PTT-Flatten cascade` (Bug 3): dependent on Bug 2. No independent fix needed.
- `B200-ATMSEND-TIMING-01` (Bug 4): compound of Bug 1 (drain path timing).
  Fixing Bug 1 fixes the root; timing asymmetry between Sim102/103/104 is not a
  separate root cause.

---

*Written by: copier-spec mode*
*Source investigation: src/PropTraderTools/CopyEngine.cs*
*Evidence: docs/brain/output.md (2026-10-14 SIM session)*
*Supersedes: B199 orchestrator prompt*
