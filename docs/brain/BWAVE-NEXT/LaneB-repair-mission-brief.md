# BWAVE-NEXT Lane B Repair -- Mission Brief

**Lane**: B-Repair -- PR #43 Bot-Confirmed Code Defects
**Status**: SPEC_READY -- awaiting wave launch
**Source**: PR #43 bot review (CodeRabbit + cubic), 2026-09-05
**Brain dir**: `docs/brain/BWAVE-NEXT/LaneBRepair/`
**Base**: branch `bwave-next-lane-b` (PR #43 open, awaiting repair commit)

---

## Context

BWAVE-NEXT Lane B (PR #43) passed `Compile NinjaScript`, `CodeQL`, `SonarCloud`, `codescene-delta`,
`Greptile`, `Amazon Q`, and all security bots. CodeRabbit and cubic identified real P1/P2 code defects
in the `DrainThenDispatch` cancel-before-dispatch implementation (`CopyEngine.cs`). These must be
fixed via a repair commit pushed onto the existing `bwave-next-lane-b` branch before PR #43 can merge.

The drain implementation structure is correct. The bugs are in specific logic gates:
1. Wrong terminal state routed to drain ack handler
2. Over-broad cancel predicate cancels protective orders
3. Legacy cancel handler conflicts with drain-owned cancels
4. TOCTOU race in payload initialization
5. Dead code branch (unreachable, not a safety issue but a correctness smell)
6. Misleading test names

**SRC CODE BAN**: No direct .cs edits. All changes go through the full 7-phase pipeline.

---

## Scope -- 1 Ticket (all fixes in one commit on bwave-next-lane-b)

All 6 findings are in `CopyEngine.cs` (same method cluster) + 4 test renames.
They are tightly coupled -- fix them together in one engineer session.

---

## Ticket T1 -- PR43-F1 through PR43-F5 + PR43-F7/8/9: Drain Logic Repairs

### F1 -- Filled event triggers drain ack → double-entry (P1 CRITICAL)

**Location**: `CopyEngine.cs` lines 1412–1416

**Bug**: `OnOrderUpdate` routes `OrderState.Filled` to `OnDrainCancelAck`. If the
tracked entry **fills** instead of cancelling, the drain submits a second entry on
top of the filled position — doubling follower position size.

**Current code**:
```csharp
if ((e.Order.OrderState == OrderState.Cancelled
     || e.Order.OrderState == OrderState.Rejected
     || e.Order.OrderState == OrderState.Filled)
    && _pendingDispatchDrains.ContainsKey(e.Order.Account.Name))
    OnDrainCancelAck(e.Order.Account.Name);
```

**Fix**: Remove `Filled` from the drain-ack routing. On `Filled`: remove the drain
entry and abort -- the position is already open, no replacement needed.
```csharp
if (e.Order.OrderState == OrderState.Cancelled
    || e.Order.OrderState == OrderState.Rejected)
{
    if (_pendingDispatchDrains.ContainsKey(e.Order.Account.Name))
        OnDrainCancelAck(e.Order.Account.Name);
}
else if (e.Order.OrderState == OrderState.Filled)
{
    // Drain-tracked entry filled -- abort replacement, position is open.
    _pendingDispatchDrains.TryRemove(e.Order.Account.Name, out _);
}
```
CYC impact on `OnOrderUpdate`: +1 branch (was 7, now 8). Still within budget.

---

### F2 -- `entryCandidates` cancels brackets and stops (P1 CRITICAL)

**Location**: `CopyEngine.cs` lines 6507–6511

**Bug**: The filter in `DrainThenDispatch` matches all Working/Accepted orders on
the instrument -- including stop brackets and targets. Cancelling a live stop
bracket leaves the follower's existing position unprotected.

**Current code**:
```csharp
var entryCandidates = ActiveOrders(follower)
    .Where(o =>
        o.Instrument == instrument
        && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted))
    .ToList();
```

**Fix**: Add entry-order predicate -- restrict to the same name/type pattern used by
`FindFollowerEntryOrder` (PTT-Copy name, Limit or StopLimit order type):
```csharp
var entryCandidates = ActiveOrders(follower)
    .Where(o =>
        o.Instrument == instrument
        && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted)
        && (o.OrderType == OrderType.Limit || o.OrderType == OrderType.StopLimit)
        && o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal))
    .ToList();
```
Before writing this, verify the exact name prefix used in `FindFollowerEntryOrder`
and `SubmitEntryDirect` -- use the same string. No assumptions.
CYC impact on `DrainThenDispatch`: +1 predicate clause (inline Where, no branch in
method body). CYC unchanged at 3 (after F5 removes dead branch).

---

### F3 -- `TryReplaceOnAtmCancel` double-replacement conflict (P1 CRITICAL)

**Location**: `CopyEngine.cs` line 1405 (called before drain ack at 1412) and
line 6531 (drain dict insertion).

**Bug**: When `DrainThenDispatch` cancels an entry order, `TryReplaceOnAtmCancel`
(line 1405) sees the cancel event and fires its own direct replacement before the
drain ack handler runs. Two entries submitted for the same cancel event.

**Fix**: `TryReplaceOnAtmCancel` must detect and skip drain-owned order cancels.
Track which order IDs are drain-owned. Before the cancel loop in `DrainThenDispatch`,
record the order IDs being cancelled:

Option A -- simplest, minimal change:
Add a `ConcurrentDictionary<long, byte> _drainOwnedOrderIds` (OrderId → 0) field.
Before `follower.Cancel(new Order[] { e })` in the loop: add `e.OrderId`.
In `TryReplaceOnAtmCancel`: check `_drainOwnedOrderIds.ContainsKey(order.OrderId)` and
return early if true.
Clean up: remove from `_drainOwnedOrderIds` in `OnDrainCancelAck` when
drain completes (or in `TryDrainWatchdog` on timeout).

Before implementing, read `TryReplaceOnAtmCancel` source (line ~863) to confirm
the exact guard location. Use `InternedCopyOracle` style if one already exists.

---

### F4 -- TOCTOU: payload visible before `PendingCancelCount` set (P2)

**Location**: `CopyEngine.cs` lines 6531 and 6539

**Bug**:
```csharp
_pendingDispatchDrains[acctKey] = payload;  // line 6531 -- visible NOW
int cancelCount = 0;
foreach (var e in entryCandidates)          // line 6534
    { follower.Cancel(...); cancelCount++; }
Interlocked.Exchange(ref payload.PendingCancelCount, cancelCount);  // line 6539
```
If a cancel ack arrives between 6531 and 6539, `OnDrainCancelAck` decrements
`PendingCancelCount` from 0 to -1. The `remaining == 0` guard never fires.
Drain stalls until 2s watchdog drops the entry.

**Fix**: Initialize count before the payload becomes visible. Two equivalent options:

Option A (change initialization order):
```csharp
int cancelCount = entryCandidates.Count;  // known before loop
var payload = new PendingDispatchDrain(..., pendingCancelCount: cancelCount, now);
_pendingDispatchDrains[acctKey] = payload;  // visible AFTER count is set
foreach (var e in entryCandidates)
    follower.Cancel(new Order[] { e });
// No Interlocked.Exchange needed -- count was set correctly at construction.
```

Option B (keep structure, hoist Exchange):
Set `PendingCancelCount` to `entryCandidates.Count` immediately after dict insert,
before the foreach. Requires the field to be settable before the loop starts.

Option A is cleaner -- choose it unless `PendingDispatchDrain` constructor prevents it.
Verify the constructor signature at line ~6670 before deciding.

---

### F5 -- Dead `cancelCount == 0` branch (P2)

**Location**: `CopyEngine.cs` lines 6541–6546

**Bug**: Branch (2) at line 6513 already returns early if `entryCandidates` is empty.
When the code reaches line 6541, `cancelCount >= 1` always. The `if (cancelCount == 0)`
block is unreachable dead code. The `SubmitEntryDirect` inside it never fires.

**Fix**: Remove lines 6541–6546 entirely. Update the CYC comment from `CYC=4` to `CYC=3`.
This is a cleanup, not a behavior change -- the code path did not exist at runtime.

---

### F7/F8/F9 -- Misleading test names (P2)

**Location**: `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` (T1 tests) +
`src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` (T2 tests)

All 4 tests are **structural reflection guards** -- they verify method/field existence
and signatures, not behavioral correctness. Their current names imply behavioral
verification they do not provide.

Rename only -- no body changes:

| Current name | Rename to |
|---|---|
| `ActiveOrders_ThreadSafetyVerification` | `ActiveOrders_FilterBehavior_AfterToListAddition` |
| `NakedDetector_DebounceField_UsesLongArithmetic` | `NakedDetector_DebounceState_FieldTypeIsLong` |
| `DrainThenDispatch_CancelsExistingEntryBeforeSubmit` | `DrainThenDispatch_MethodExists_WithExpectedSignature` |
| `OnDrainCancelAck_SubmitsDrainedEntry_WhenPendingCountReachesZero` | `OnDrainCancelAck_MethodExists_WithExpectedSignature` |
| `DrainWatchdog_ClearsStuckDrain_AfterTimeout` | `DrainWatchdog_MethodExists_WithExpectedSignature` |

Note: `DrainThenDispatch`, `OnDrainCancelAck`, `DrainWatchdog` are 3 tests in
`BwaveNextLaneBTests.cs`. `ActiveOrders` and `NakedDetector` are 2 tests in
`BwaveDwLaneATests.cs`. Total: 5 renames.

---

## Acceptance Criteria (T1)

- [ ] F1: `OnOrderUpdate` drain routing: only `Cancelled`/`Rejected` → `OnDrainCancelAck`. `Filled` → `TryRemove` + abort. `OnOrderUpdate` CYC ≤ 8 post-fix.
- [ ] F2: `entryCandidates` restricted to entry orders (PTT-Copy name, Limit/StopLimit type). No stop brackets cancelled by drain.
- [ ] F3: `_drainOwnedOrderIds` (or equivalent) field added. `TryReplaceOnAtmCancel` skips drain-owned order IDs.
- [ ] F4: `PendingCancelCount` initialized to `entryCandidates.Count` before payload inserted into dict. No Interlocked.Exchange after loop.
- [ ] F5: Dead `if (cancelCount == 0)` block removed. CYC comment updated to 3.
- [ ] F7/8/9: 5 test methods renamed. Bodies unchanged. All renamed tests still pass.
- [ ] `dotnet build` 0 errors after all changes.
- [ ] `dotnet test --filter "DrainThenDispatch|OnDrainCancelAck|DrainWatchdog|ActiveOrders|NakedDetector"` all pass under new names.
- [ ] NT8 sync 18/18 OK.
- [ ] F5 (dead branch removal): CYC of `DrainThenDispatch` = 3 confirmed.
- [ ] No new lock(), no async void, no return null, ASCII-only, xUnit-only.
- [ ] NT8 banned APIs (Account.Change, AtmStrategyCreate, AtmStrategyChangeStopTarget): 0 in new code.

---

## Out of Scope (do NOT implement in this repair)

| Finding | Reason excluded |
|---------|----------------|
| `TickCount64` suggestion | .NET 4.8 target -- `TickCount64` is .NET 5+ only. `(long)(int)` is the correct pattern. **Do not change.** |
| Remove `.ToList()` from `ActiveOrders` | Thread-safety fix (DW-NEXT-A-07). `.ToList()` stays. |
| Drain key acct-only (F5-cubic P1) | Single-instrument operational scope. Multi-instrument is future backlog (DW-NEXT-B-01). |
| GTC/Day order metadata not preserved | Future backlog (DW-NEXT-B-02). |
| Watchdog resubmit vs drop | Original spec decision -- watchdog drops on timeout. Not changing in this repair. |
| Docstring coverage 50% | Not a V12 requirement. Dismiss. |
| qlty fmt markdownlint issues | Brain .md docs, not source. Advisory only. |

---

## Branch and PR

**Target branch**: `bwave-next-lane-b` (push repair commit here, updates open PR #43)
**Do NOT merge yet** -- repair commit triggers re-run of all bots. Wait for Director confirmation.

---

## New Backlog Items Created (do not implement, just record)

| ID | Description | Priority |
|----|-------------|----------|
| DW-NEXT-B-01 | Drain key is acct-only -- second instrument overwrites first drain intent. Extend key to `acct.Name + "|" + instrument.FullName` when multi-instrument trading is added. | P2 (future) |
| DW-NEXT-B-02 | GTC/Day TIF and native-ATM Entry name not preserved in `SubmitEntryDirect` replacement. Carry original TIF + name in `PendingDispatchDrain` payload and use when creating replacement. | P2 (future) |

---

*Spec written: 2026-09-05 | copier-spec mode | Grounded against PR #43 bot findings + source verification*
