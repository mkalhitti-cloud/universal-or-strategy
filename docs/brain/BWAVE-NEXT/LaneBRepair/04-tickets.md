# BWAVE-NEXT LaneBRepair — Tickets

**Epic**: BWAVE-NEXT LaneBRepair
**Phase**: 3 (Ticket Generation)
**Status**: TICKETS_COMPLETE
**Plan source**: docs/brain/BWAVE-NEXT/LaneBRepair/02-architecture-plan.md (REVIEW_PASS)
**Branch**: bwave-next-lane-b
**Date**: 2026-09-05

---

## RULES CATALOG GATE RESULT: PASS

Catalog read: docs/standards/jane-street/RULES_CATALOG.md (UTF-8 clean).
P0 rules confirmed applicable: JS-021 (lock ban), JS-033 (async void ban),
JS-001 (throw ban in hot paths), JS-002 (return null ban).
Zero P0 violations in new code designed below. Gate: PASS.

---

# TICKET T1 — PR43-F1 through PR43-F5 + F7/F8/F9 Test Renames

## Spec Requirement IDs

F1 (Filled event triggers drain ack -> double-entry), F2 (entryCandidates cancels
brackets and stops), F3 (TryReplaceOnAtmCancel double-replacement conflict), F4 (TOCTOU
payload initialization race), F5 (dead cancelCount==0 branch), F7 (ActiveOrders test
rename), F8 (NakedDetector test rename), F9 (Drain tests rename x3).

Sources: docs/brain/BWAVE-NEXT/LaneB-repair-mission-brief.md sections F1-F5, F7/F8/F9.

## Scope Lock

SCOPE LOCK -- TICKET 1 ONLY.
Do NOT read, reference, or implement any other ticket in this session.
Do NOT fix any other finding from PR #43 beyond F1-F5 and the 5 test renames.
Out-of-scope items are documented in mission brief (TickCount64, ToList, DW-NEXT-B-01/B-02).
Do NOT change these.

## Files to Edit

- `src/PropTraderTools/CopyEngine.cs`
  (F1-F5, new field _drainOwnedOrderIds, PendingDispatchDrain class update)
- `src/PropTraderTools/Tests/BwaveDwLaneATests.cs`
  (F7/F8: 2 renames)
- `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs`
  (F9: 3 renames)

## Implementation Steps (in order — engineer follows exactly)

### Pre-Read (MANDATORY before any edit)

Read these BEFORE writing any code. Confirm exact content at each range.
Record the exact content found — do not rely on plan summaries.

```
1. CopyEngine.cs lines 855-875       (TryReplaceOnAtmCancel full body)
2. CopyEngine.cs lines 1395-1430     (OnOrderUpdate drain section context)
3. CopyEngine.cs lines 6480-6720     (DrainThenDispatch + PendingDispatchDrain ctor)
4. CopyEngine.cs lines 3660-3710     (FindFollowerEntryOrder -- confirm name prefix)
5. BwaveDwLaneATests.cs              (locate ActiveOrders_ThreadSafetyVerification
                                      and NakedDetector_DebounceField_UsesLongArithmetic)
6. BwaveNextLaneBTests.cs            (locate DrainThenDispatch_CancelsExistingEntryBeforeSubmit,
                                      OnDrainCancelAck_SubmitsDrainedEntry_WhenPendingCountReachesZero,
                                      DrainWatchdog_ClearsStuckDrain_AfterTimeout)
```

After each pre-read, confirm line numbers match plan. If they differ by more than 5 lines,
adjust the target lines for the edits below to match actual source. Do NOT proceed blindly.

---

### Fix F1 — OnOrderUpdate drain routing

**Location confirmed by plan**: CopyEngine.cs lines 1412-1416.

**Current code to replace** (confirm this exact text at target lines before editing):
```csharp
if ((e.Order.OrderState == OrderState.Cancelled
     || e.Order.OrderState == OrderState.Rejected
     || e.Order.OrderState == OrderState.Filled)
    && _pendingDispatchDrains.ContainsKey(e.Order.Account.Name))
    OnDrainCancelAck(e.Order.Account.Name);
```

**Replace with**:
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

**CYC verification** (mandatory, report in completion artifact):
Count branches in OnOrderUpdate before and after the edit.
Plan baseline: pre-fix CYC = 7, post-fix CYC = 8.
If manual count yields a different result, report the discrepancy and explain.
CYC must be <= 8 to proceed. If CYC would exceed 8, STOP and report to Director.

---

### Fix F2 — entryCandidates predicate

**Location confirmed by plan**: CopyEngine.cs lines 6507-6511.

**BEFORE writing**: Confirm from pre-read of lines 3660-3710 the exact string used in
FindFollowerEntryOrder for PTT-Copy orders. Plan confirms: `"PTT-Copy"` (exact, not a prefix
variant). Confirm from SubmitEntryDirect (line ~6575) that orders are created with name
`"PTT-Copy"`. If the source shows a different prefix, use that confirmed string -- do NOT
invent or assume.

**Current code to replace**:
```csharp
var entryCandidates = ActiveOrders(follower)
    .Where(o =>
        o.Instrument == instrument
        && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted))
    .ToList();
```

**Replace with**:
```csharp
var entryCandidates = ActiveOrders(follower)
    .Where(o =>
        o.Instrument == instrument
        && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted)
        && (o.OrderType == OrderType.Limit || o.OrderType == OrderType.StopLimit)
        && o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal))
    .ToList();
```

**CYC note**: Where predicate is a LINQ lambda. This does NOT increase method-body CYC.
DrainThenDispatch CYC after all fixes = 3 (F5 removes the dead branch).

---

### Fix F3 — _drainOwnedOrderIds field + guard + cleanup

This fix has five sub-steps (A through E). Complete all five before building.

**Step A — New field in CopyEngine**

Add adjacent to `_pendingDispatchDrains` field (plan confirms: line ~379).
Insert immediately after the `_pendingDispatchDrains` field declaration:
```csharp
private readonly ConcurrentDictionary<long, byte> _drainOwnedOrderIds =
    new ConcurrentDictionary<long, byte>();
```

**Step B — New property + constructor parameter in PendingDispatchDrain**

PendingDispatchDrain is a nested or inner class within CopyEngine.cs (confirmed by plan at
lines 6662-6671). Apply both changes atomically.

Add a new property after the existing `OrderType` property:
```csharp
internal IReadOnlyList<long> DrainedOrderIds { get; private set; }
```

Extend the constructor signature by inserting `IReadOnlyList<long> drainedOrderIds` as
parameter 7 (after `orderType`, before `followerAccount`):

Current constructor signature (from plan, confirmed at lines 6662-6671):
```csharp
internal PendingDispatchDrain(
    string followerAcctKey,
    Instrument instrument,
    int qty,
    double price,
    OrderAction action,
    OrderType orderType,
    Account followerAccount,
    int pendingCancelCount,
    long timestampTicks)
```

New constructor signature:
```csharp
internal PendingDispatchDrain(
    string followerAcctKey,
    Instrument instrument,
    int qty,
    double price,
    OrderAction action,
    OrderType orderType,
    IReadOnlyList<long> drainedOrderIds,
    Account followerAccount,
    int pendingCancelCount,
    long timestampTicks)
```

Add to constructor body:
```csharp
DrainedOrderIds = drainedOrderIds;
```

**Step C — Guard in TryReplaceOnAtmCancel**

Read TryReplaceOnAtmCancel at lines ~855-875 (pre-read step 1 above).
Confirmed structure from plan (lines 863-868):
```csharp
private void TryReplaceOnAtmCancel(Order order)
{
    if (!IsPttEntryOrderCancelTrigger(order))
        return; // (1)
    ReplaceFollowerCopyOnAtmCancel(order); // (2 - no branch)
}
```

Insert the new drain-owned guard as the FIRST statement in the method body,
BEFORE the existing `!IsPttEntryOrderCancelTrigger` check:
```csharp
if (_drainOwnedOrderIds.ContainsKey(order.OrderId))
    return; // drain-owned cancel -- skip replacement
```

After insertion the method body reads:
```csharp
private void TryReplaceOnAtmCancel(Order order)
{
    if (_drainOwnedOrderIds.ContainsKey(order.OrderId))
        return; // drain-owned cancel -- skip replacement
    if (!IsPttEntryOrderCancelTrigger(order))
        return;
    ReplaceFollowerCopyOnAtmCancel(order);
}
```
CYC: 2 -> 3.

**Step D — Cleanup in SubmitDrainedEntry**

Read SubmitDrainedEntry source before writing cleanup code.
Locate the `TryRemove` call that gives payload and the `remaining == 0` check.
Insert cleanup after the TryRemove succeeds, before the SubmitEntryDirect call:
```csharp
foreach (var id in payload.DrainedOrderIds)
    _drainOwnedOrderIds.TryRemove(id, out _);
```

**Step E — Cleanup in TryDrainWatchdog**

Read TryDrainWatchdog source before writing cleanup code.
Locate the timeout block (`now - kv.Value.TimestampTicks > 2000L` or equivalent).
Inside the timeout handling block, before or after the TryRemove from _pendingDispatchDrains:
```csharp
foreach (var id in kv.Value.DrainedOrderIds)
    _drainOwnedOrderIds.TryRemove(id, out _);
```

---

### Fix F4 — TOCTOU fix (set PendingCancelCount before payload is visible)

**Location confirmed by plan**: CopyEngine.cs lines 6521-6539.

This fix is integrated with F3's DrainThenDispatch changes. Apply as one combined edit.

**Before the cancel loop**, add:
```csharp
var drainedIds = entryCandidates.Select(static e => e.OrderId).ToList();
```

**Construct payload with correct count before inserting into dict**:
```csharp
var payload = new PendingDispatchDrain(
    acctKey,
    instrument,
    qty,
    price,
    action,
    orderType,
    drainedIds,
    follower,
    pendingCancelCount: entryCandidates.Count,
    now);
_pendingDispatchDrains[acctKey] = payload;
```

Note: Constructor parameter order matches Step B above (drainedOrderIds before followerAccount).
If the pre-read of the actual constructor reveals a different current order, adapt to match the
actual order -- do NOT break the constructor.

**Cancel loop** (after dict insert):
```csharp
foreach (var e in entryCandidates)
{
    _drainOwnedOrderIds.TryAdd(e.OrderId, 0);
    follower.Cancel(new Order[] { e });
}
```

**Remove** all of the following from the current code:
- `int cancelCount = 0;` variable declaration
- `cancelCount++` increment inside the loop
- `Interlocked.Exchange(ref payload.PendingCancelCount, cancelCount);` line

---

### Fix F5 — Remove dead cancelCount==0 block

**Location confirmed by plan**: CopyEngine.cs lines 6541-6546.

After applying F4, the `cancelCount` variable no longer exists. The dead block is:
```csharp
if (cancelCount == 0) // (4)
{
    _pendingDispatchDrains.TryRemove(acctKey, out _);
    SubmitEntryDirect(follower, instrument, qty, price, action, orderType);
    return;
}
```

**Delete** this entire block (lines 6541-6546 or adjusted range after F4 edit).

**Update** the CYC comment at the top of DrainThenDispatch (line ~6493) from:
```
// CYC=4: (1) null guard, (2) no-entry fast path, (3) foreach cancel loop, (4) cancelCount==0 edge guard.
```
to:
```
// CYC=3: (1) null guard, (2) no-entry fast path, (3) foreach cancel loop.
// F5-repair: dead (4) cancelCount==0 branch removed. entryCandidates always non-empty here.
```

If the actual comment text differs from above, update it to reflect: CYC=3, F5-repair note.

---

### Test Renames F7/F8/F9 (rename ONLY — do NOT change bodies)

**Rule**: Change only the method declaration line (`public void <OldName>()`).
Do NOT change: `[Fact]` attribute, method body, assertions, comments inside body.

**In BwaveDwLaneATests.cs**:

Rename 1:
```
public void ActiveOrders_ThreadSafetyVerification()
```
->
```
public void ActiveOrders_FilterBehavior_AfterToListAddition()
```

Rename 2:
```
public void NakedDetector_DebounceField_UsesLongArithmetic()
```
->
```
public void NakedDetector_DebounceState_FieldTypeIsLong()
```

**In BwaveNextLaneBTests.cs**:

Rename 3:
```
public void DrainThenDispatch_CancelsExistingEntryBeforeSubmit()
```
->
```
public void DrainThenDispatch_MethodExists_WithExpectedSignature()
```

Rename 4:
```
public void OnDrainCancelAck_SubmitsDrainedEntry_WhenPendingCountReachesZero()
```
->
```
public void OnDrainCancelAck_MethodExists_WithExpectedSignature()
```

Rename 5:
```
public void DrainWatchdog_ClearsStuckDrain_AfterTimeout()
```
->
```
public void DrainWatchdog_MethodExists_WithExpectedSignature()
```

---

## Method Signatures Affected

All existing methods — no public signature changes except PendingDispatchDrain ctor.

```csharp
// CopyEngine.cs -- existing methods, signatures unchanged:
private void OnOrderUpdate(NinjaTrader.Cuas.OrderEventArgs e)          // CYC 7->8 (F1)
private void TryReplaceOnAtmCancel(Order order)                         // CYC 2->3 (F3)
private void DrainThenDispatch(Account follower, Instrument instrument,
    int qty, double price, OrderAction action, OrderType orderType)     // CYC 4->3 (F2+F3+F4+F5)
private void SubmitDrainedEntry(string acctKey)                         // CYC +1 (F3 cleanup)
private void TryDrainWatchdog()                                          // CYC 3->4 (F3 cleanup)

// New field in CopyEngine:
private readonly ConcurrentDictionary<long, byte> _drainOwnedOrderIds =
    new ConcurrentDictionary<long, byte>();

// PendingDispatchDrain ctor -- modified (new drainedOrderIds param at position 7):
internal PendingDispatchDrain(
    string followerAcctKey,
    Instrument instrument,
    int qty,
    double price,
    OrderAction action,
    OrderType orderType,
    IReadOnlyList<long> drainedOrderIds,
    Account followerAccount,
    int pendingCancelCount,
    long timestampTicks)

// New property in PendingDispatchDrain:
internal IReadOnlyList<long> DrainedOrderIds { get; private set; }
```

---

## JS Rule Constraints

| Rule | Constraint | Applies To |
|------|-----------|------------|
| JS-021 | No `lock()` anywhere in new or modified code. ConcurrentDictionary + Interlocked only. | All edits in CopyEngine.cs |
| JS-033 | No `async void` in new code. All modified methods are synchronous void. | All edits |
| JS-002 | No `return null` in new code. | All edits |
| JS-001 | No `throw new XxxException` in hot paths. | All edits |
| CYC<=8 | All new and modified methods must be <=8 branches. Verify each before BUILD. | OnOrderUpdate, DrainThenDispatch, TryReplaceOnAtmCancel, SubmitDrainedEntry, TryDrainWatchdog |
| ASCII-only | No Unicode, emoji, curly quotes in any string literal or identifier. | All edits |
| xUnit-only | `[Fact]` + `Assert.*` only. No `[Test]`, no NUnit, no MSTest. | Test file renames |
| NT8 banned | No `Account.Change()`, `AtmStrategyCreate()`, `AtmStrategyChangeStopTarget()` in new code. | All edits |
| DO NOT CHANGE | `Environment.TickCount64` -> `(long)(int)` pattern stays as-is. | CopyEngine.cs |
| DO NOT CHANGE | `.ToList()` on `ActiveOrders` stays as-is (thread-safety fix). | CopyEngine.cs |

---

## 7-Scan Checklist (engineer runs ALL before reporting BUILD_PASS)

All 7 scans must return zero violations before the completion artifact is written.

**SCAN 1 — JS-021 lock() check**
```powershell
grep -rn "lock(" src/PropTraderTools/CopyEngine.cs
```
Required: 0 matches in new/modified code.
If existing lock() calls are found in unmodified areas, note them as pre-existing (do not fix).

**SCAN 2 — JS-033 async void check**
```powershell
grep -rn "async void " src/PropTraderTools/CopyEngine.cs
```
Required: 0 matches in new/modified code.
(NT8 event-handler overrides that pre-exist are noted but not counted against new code.)

**SCAN 3 — JS-002 return null check**
```powershell
grep -n "return null;" src/PropTraderTools/CopyEngine.cs
```
Required: 0 matches in code added by this ticket.
(Pre-existing return null in unrelated methods does not block -- note line numbers.)

**SCAN 4 — CYC check (manual branch count or lizard)**
Methods to verify and their expected CYC post-fix:
```
OnOrderUpdate           expected: 8
DrainThenDispatch       expected: 3
TryReplaceOnAtmCancel   expected: 3
SubmitDrainedEntry      expected: <=8 (was 2-3, +1 = 3-4)
TryDrainWatchdog        expected: 4
OnDrainCancelAck        expected: <=8 (unchanged)
```
Required: All <=8. Report each method's CYC in completion artifact.
If lizard is available: `lizard src/PropTraderTools/CopyEngine.cs -l csharp -C 8`
If manual: count if/else/for/foreach/while/case/catch/? in method body; add 1 for entry.

**SCAN 5 — ASCII-only check**
```powershell
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
```
Required: 0 matches in new/modified lines. (Use line range if grepping full file produces noise.)

**SCAN 6 — NT8 banned API check**
```powershell
grep -n "Account\.Change\|AtmStrategyCreate\|AtmStrategyChangeStopTarget" src/PropTraderTools/CopyEngine.cs
```
Required: 0 matches in new code added by this ticket.

**SCAN 7 — Build check**
```powershell
dotnet build src/PropTraderTools 2>&1 | tail -5
```
Required: 0 errors, 0 new warnings (pre-existing warnings are noted, not counted against).

---

## xUnit Test Names (post-rename — confirm these exact names exist)

After renames are applied, confirm each method exists by running the test filter:
```powershell
dotnet test src/ --filter "DrainThenDispatch_MethodExists_WithExpectedSignature|OnDrainCancelAck_MethodExists_WithExpectedSignature|DrainWatchdog_MethodExists_WithExpectedSignature|ActiveOrders_FilterBehavior_AfterToListAddition|NakedDetector_DebounceState_FieldTypeIsLong"
```

Expected test names post-rename:

**BwaveDwLaneATests.cs**:
```csharp
[Fact] public void ActiveOrders_FilterBehavior_AfterToListAddition()
[Fact] public void NakedDetector_DebounceState_FieldTypeIsLong()
```

**BwaveNextLaneBTests.cs**:
```csharp
[Fact] public void DrainThenDispatch_MethodExists_WithExpectedSignature()
[Fact] public void OnDrainCancelAck_MethodExists_WithExpectedSignature()
[Fact] public void DrainWatchdog_MethodExists_WithExpectedSignature()
```

All 5 must pass (not just compile). If any fail after rename, check for typos in rename.

---

## Acceptance Criteria (T1)

- [ ] F1: `OnOrderUpdate` drain routing: Cancelled/Rejected only -> `OnDrainCancelAck`. Filled -> `TryRemove` + abort. `OnOrderUpdate` CYC = 8 post-fix.
- [ ] F2: `entryCandidates` restricted to Limit/StopLimit type AND name StartsWith "PTT-Copy". No stop bracket orders cancelled during drain.
- [ ] F3: `_drainOwnedOrderIds: ConcurrentDictionary<long,byte>` field present in CopyEngine. `TryReplaceOnAtmCancel` guard: ContainsKey(order.OrderId) -> return early as first statement. `DrainedOrderIds` populated in DrainThenDispatch cancel loop. Cleanup in SubmitDrainedEntry and TryDrainWatchdog.
- [ ] F4: `PendingCancelCount` = `entryCandidates.Count` passed to constructor before dict insert. No `Interlocked.Exchange` after loop. `cancelCount` variable removed.
- [ ] F5: Dead `if (cancelCount == 0)` block removed. CYC comment updated to 3 with F5-repair note.
- [ ] F7/8/9: All 5 test methods renamed (bodies, [Fact] attributes, assertions unchanged). All 5 pass under new names.
- [ ] `dotnet build` 0 errors.
- [ ] `dotnet test --filter` with all 5 new names: all pass.
- [ ] NT8 sync: `powershell -File scripts\ptt-sync-and-verify.ps1` -- 0 MISMATCH lines.
- [ ] SCAN-01 through SCAN-07: all zero violations in new code.
- [ ] PendingDispatchDrain constructor updated: new `drainedOrderIds` param at position 7, `DrainedOrderIds` property set in body.

---

## Completion Artifact

After ALL edits and ALL 7 scans return zero violations:

Write: `docs/brain/BWAVE-NEXT/LaneBRepair/ticket-1-completion.md`

Include in completion artifact:
```
# Ticket T1 Completion Report

## 7-Scan Results
SCAN 1 lock():         [command] -> [output or "0 matches"]
SCAN 2 async void:     [command] -> [output or "0 matches"]
SCAN 3 return null:    [command] -> [output or "0 matches in new code"]
SCAN 4 CYC:
  OnOrderUpdate:          pre=7   post=[N]   <= 8: [PASS/FAIL]
  DrainThenDispatch:      pre=4   post=[N]   <= 8: [PASS/FAIL]
  TryReplaceOnAtmCancel:  pre=2   post=[N]   <= 8: [PASS/FAIL]
  SubmitDrainedEntry:     pre=[N] post=[N]   <= 8: [PASS/FAIL]
  TryDrainWatchdog:       pre=3   post=[N]   <= 8: [PASS/FAIL]
SCAN 5 ASCII:          [command] -> [output or "0 matches"]
SCAN 6 NT8 banned:     [command] -> [output or "0 matches"]
SCAN 7 Build:          [command output, last 5 lines]

## Test Renames Confirmed
1. ActiveOrders_ThreadSafetyVerification -> ActiveOrders_FilterBehavior_AfterToListAddition [PASS/FAIL]
2. NakedDetector_DebounceField_UsesLongArithmetic -> NakedDetector_DebounceState_FieldTypeIsLong [PASS/FAIL]
3. DrainThenDispatch_CancelsExistingEntryBeforeSubmit -> DrainThenDispatch_MethodExists_WithExpectedSignature [PASS/FAIL]
4. OnDrainCancelAck_SubmitsDrainedEntry_WhenPendingCountReachesZero -> OnDrainCancelAck_MethodExists_WithExpectedSignature [PASS/FAIL]
5. DrainWatchdog_ClearsStuckDrain_AfterTimeout -> DrainWatchdog_MethodExists_WithExpectedSignature [PASS/FAIL]

## Test Run Result
dotnet test filter command -> [N passed, 0 failed]

## Build Result
0 errors. [warnings if any noted as pre-existing]

## NT8 Sync Result
ptt-sync-and-verify.ps1 -> [0 MISMATCH / N OK]

## Result Summary
BUILD_PASS | BUILD_FAIL
```

Do NOT write the completion artifact until ALL scans are zero and build is clean.
If any scan fails, fix the violation, re-run the scan, then write the artifact.

---

*Tickets authored: 2026-09-05 | ptt-architect | Phase 3 | BWAVE-NEXT LaneBRepair*
*Source: 02-architecture-plan.md (REVIEW_PASS) + LaneB-repair-mission-brief.md*
*Single ticket: all F1-F5 + F7/F8/F9 renames bundled per SINGLE-PIPELINE gate result*
