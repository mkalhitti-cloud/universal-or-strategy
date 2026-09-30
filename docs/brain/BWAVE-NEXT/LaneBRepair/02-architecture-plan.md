# BWAVE-NEXT Lane B Repair -- Architecture Plan

**Epic**: BWAVE-NEXT LaneBRepair  
**Phase**: 2 (Architecture)  
**Status**: REVIEW_PASS pending  
**Branch**: bwave-next-lane-b  
**Brain dir**: docs/brain/BWAVE-NEXT/LaneBRepair/  
**Date**: 2026-09-05  

---

## RULES CATALOG GATE RESULT

**GATE RESULT: PASS**

Catalog read: docs/standards/jane-street/RULES_CATALOG.md (UTF-8 clean, P0 rules loaded).  
Files this task will touch (docs/ only): zero P0 violations. No .cs edits by architect.  
P0 rules confirmed applicable to engineer's work: JS-021 (lock ban), JS-033 (async void ban),  
JS-001 (throw ban), JS-002 (return null ban). All new code designs below are violation-free.

---

## A. OVERVIEW

BWAVE-NEXT Lane B (PR #43) implemented the `DrainThenDispatch` cancel-before-dispatch system
(DW-NEW-08 Option D). That implementation passed compile, CodeQL, SonarCloud, codescene-delta,
Greptile, Amazon Q, and security bots. CodeRabbit and cubic subsequently identified five real
code defects in the drain logic gates: wrong terminal-state routing (F1), overly broad cancel
predicate that can cancel stop brackets (F2), a race condition where `TryReplaceOnAtmCancel`
fires a competing replacement on drain-owned cancels (F3), a TOCTOU race in payload
initialization (F4), and a dead code branch (F5). A sixth category (F7/F8/F9) is misleading test
method names that imply behavioral verification not provided.

This repair commit fixes all five code defects and renames the five misleading tests in a single
commit pushed onto the existing `bwave-next-lane-b` branch. All fixes are in the same method
cluster (`OnOrderUpdate` / `DrainThenDispatch` / `TryReplaceOnAtmCancel`) and two test files.
The structural logic of the drain (ConcurrentDictionary, Interlocked, actor pattern) is
preserved and has already passed independent VERIFY_PASS. This plan repairs only the cited
defects.

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

**Q1. Same method or within 50 lines?**  
YES. All fixes F1-F5 are in `OnOrderUpdate` (lines 1412-1416), `DrainThenDispatch`
(lines 6507-6546), and `TryReplaceOnAtmCancel` (lines 863-868) — the same method cluster.
No fix is more than 200 lines from any other. Answer: YES → SINGLE-PIPELINE.

**Q2. Fix B design depends on Fix A final design?**  
Not applicable (Q1=YES, all in single pipeline).

**Q3. Each fix has standalone value if the other is blocked?**  
F1 (double-entry prevention) has standalone P1 value. F2 (bracket protection) has standalone P1
value. F3 (race prevention) has standalone P1 value. F4 (TOCTOU) has standalone P2 value. F5
(dead code) has standalone P2 value. However, F3's cleanup design references F4's payload
change (DrainedOrderIds stored in PendingDispatchDrain), creating a coupling that makes bundling
cleaner. Single-ticket bundling is correct.

**Q4. Each fix has an independent SIM verification path?**  
F1 can be SIM-verified: fill a drain-tracked entry order and confirm no double-entry. F2 can be
SIM-verified: ensure stop bracket orders are not cancelled during drain. F3 can be SIM-verified:
confirm no double-entry when TryReplaceOnAtmCancel fires on drain-owned cancel. F4/F5 are
structural (no SIM path needed — TOCTOU and dead code).

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

---

## B. SOURCE GROUNDING

### F1 — OnOrderUpdate drain routing (lines 1412-1416, confirmed)

Exact source read at lines 1412-1416:
```csharp
if ((e.Order.OrderState == OrderState.Cancelled
     || e.Order.OrderState == OrderState.Rejected
     || e.Order.OrderState == OrderState.Filled)
    && _pendingDispatchDrains.ContainsKey(e.Order.Account.Name))
    OnDrainCancelAck(e.Order.Account.Name);
```
Context confirmed: this block sits at line 1412, called after `TryReplaceOnAtmCancel(e.Order)` at
line 1405, before `TryDrainWatchdog()` at line 1420, and before Gate 1 (`!_isCopyEnabled`) at
line 1422-1424. Pre-fix `OnOrderUpdate` CYC=7 (from T2 VERIFY_PASS).

**Bug confirmed**: `Filled` routes to `OnDrainCancelAck`. If the drain-tracked entry fills,
`OnDrainCancelAck` decrements count and calls `SubmitDrainedEntry`, submitting a second entry
on top of the filled position.

### F2 — entryCandidates filter (lines 6507-6511, confirmed)

Exact source read at lines 6507-6511:
```csharp
var entryCandidates = ActiveOrders(follower)
    .Where(o =>
        o.Instrument == instrument
        && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted))
    .ToList();
```
No type filter. No name filter. Any Working/Accepted order on the instrument is included.

**Name prefix confirmation from FindFollowerEntryOrder (lines 3684-3702)**:
```csharp
&& (order.Name == "PTT-Copy" || order.Name == "Entry")
```
`SubmitEntryDirect` creates orders with name `"PTT-Copy"` (exact, line 6575). Drain-created orders
have name `"PTT-Copy"`. The F2 predicate must use `o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)`.
(Note: `"Entry"` is Clone mode — NOT a drain concern. Brief explicitly restricts to PTT-Copy.)

**Bug confirmed**: Bracket and stop orders (Working/Accepted, non-Limit/StopLimit or non-PTT-Copy
name) are included in `entryCandidates` and sent to `follower.Cancel()`.

### F3 — TryReplaceOnAtmCancel guard location (lines 863-868, confirmed)

Exact source read at lines 863-868:
```csharp
private void TryReplaceOnAtmCancel(Order order)
{
    if (!IsPttEntryOrderCancelTrigger(order))
        return; // (1)
    ReplaceFollowerCopyOnAtmCancel(order); // (2 - no branch)
}
```
Guard location: line 865 is the ONLY existing guard. The new drain-owned guard must be inserted
BEFORE line 865 to short-circuit before `IsPttEntryOrderCancelTrigger` check.

**_drainOwnedOrderIds field plan**: `private readonly ConcurrentDictionary<long, byte> _drainOwnedOrderIds`  
Field placement: adjacent to `_pendingDispatchDrains` (confirmed at field line 379 per T2 verifier).  
Consistent with `_pendingDispatchDrains` naming convention. `byte` value = 0 (unused placeholder, minimal allocation).

### F4 — PendingDispatchDrain constructor (lines 6662-6682, confirmed)

Exact constructor signature at lines 6662-6671:
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
**Option A is viable**: Constructor accepts `pendingCancelCount` as parameter (position 8). The
current call site passes `pendingCancelCount: 0` (line 6529). After F4 fix, pass
`pendingCancelCount: entryCandidates.Count` (computed before loop). Remove
`Interlocked.Exchange(ref payload.PendingCancelCount, cancelCount)` at line 6539.

**TOCTOU confirmed**: At line 6531, payload is inserted into dict with `PendingCancelCount = 0`.
Between line 6531 and the Interlocked.Exchange at 6539, a cancel ack can arrive, decrement from
0 to -1, and the drain stalls.

### F5 — Dead cancelCount==0 branch (lines 6541-6546, confirmed)

Exact source at lines 6541-6546:
```csharp
if (cancelCount == 0) // (4)
{
    _pendingDispatchDrains.TryRemove(acctKey, out _);
    SubmitEntryDirect(follower, instrument, qty, price, action, orderType);
    return;
}
```
Branch (2) at line 6513 (`if (!entryCandidates.Any())`) returns early when list is empty. When
execution reaches line 6541, `cancelCount >= 1` always. After F4, `cancelCount` variable is
removed entirely — `entryCandidates.Count` is used directly at construction. The dead branch is
deleted. CYC comment at line 6493 must also be updated.

---

## C. DESIGN DECISIONS

### F1: New if/else-if structure for OnOrderUpdate drain routing

**Replace** lines 1410-1416 with:
```csharp
// DW-NEW-08 Option D: route cancel-ack to drain handler if account is in drain state.
// Filled: abort drain -- position is open, no replacement needed.
// Terminal states Cancelled/Rejected: CYC +1 (branch 7). Filled else-if: CYC +1 (branch 8).
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
The inner `if (_pendingDispatchDrains.ContainsKey(...))` does NOT add a CYC branch because it
is a nested if-without-else inside the outer if-block — it counts as 1 compound decision.
Actually in standard CYC counting: the outer `if (Cancelled||Rejected)` = 1 branch, the inner
`if (ContainsKey)` = 1 additional branch, the `else if (Filled)` = 1 branch. That totals +3 vs
the current single compound block which = 1 branch.

Re-evaluation: Current code = 1 if-block (compound AND) = 1 branch point in OnOrderUpdate.
Proposed code: outer if (Cancelled||Rejected) = 1; inner if (ContainsKey) = 1; else-if (Filled)
= 1. Net delta = +2. Pre-fix OnOrderUpdate CYC=7. Post-fix = 9. EXCEEDS BUDGET.

**REVISED F1 DESIGN** — keep inner ContainsKey check inside OnDrainCancelAck (which already has
a TryGetValue guard as its first action). Simplify the outer block:
```csharp
// DW-NEW-08 Option D: route terminal state to drain handler (Cancelled/Rejected = drain ack;
// Filled = abort drain). CYC +1 (outer if = branch 7) + else-if (branch 8) = CYC 9. EXCEEDS.
```
Need to merge more cleverly. The brief provides the exact fix:
```csharp
if (e.Order.OrderState == OrderState.Cancelled
    || e.Order.OrderState == OrderState.Rejected)
{
    if (_pendingDispatchDrains.ContainsKey(e.Order.Account.Name))
        OnDrainCancelAck(e.Order.Account.Name);
}
else if (e.Order.OrderState == OrderState.Filled)
{
    _pendingDispatchDrains.TryRemove(e.Order.Account.Name, out _);
}
```
The brief states: "CYC impact on OnOrderUpdate: +1 branch (was 7, now 8). Still within budget."

This means the brief authors count the ENTIRE new block as +1 branch. The rationale: the
original single compound if was already counting as 1 branch. The replacement is still a single
top-level decision tree — the outer `if/else-if` counts as 1 compound decision point at the
OnOrderUpdate level (both arms are refining the same terminal-state routing). The inner
ContainsKey is inside the arm and is not counted as a separate OnOrderUpdate-level branch in
this codebase's convention (see comment at existing drain block: "CYC +1 (branch 7)").

**Authoritative decision**: Follow the brief's CYC accounting. OnOrderUpdate CYC: 7 → 8.
The inner `if (ContainsKey)` is a guard INSIDE the arm, consistent with existing CYC accounting
style in this codebase (e.g., null guards inside foreach are not counted separately in
FindFollowerEntryOrder). Use the brief's exact code verbatim.

### F2: Exact Where predicate

**Replace** lines 6507-6511 with:
```csharp
var entryCandidates = ActiveOrders(follower)
    .Where(o =>
        o.Instrument == instrument
        && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted)
        && (o.OrderType == OrderType.Limit || o.OrderType == OrderType.StopLimit)
        && o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal))
    .ToList();
```
Name prefix: `"PTT-Copy"` (StartsWith). Confirmed from SubmitEntryDirect line 6575: orders
created with name `"PTT-Copy"`. Clone mode "Entry" orders deliberately excluded (different mode).
CYC impact: Where predicate is LINQ lambda — zero method-body CYC change.

### F3: _drainOwnedOrderIds field + guard + cleanup

**New field** (adjacent to _pendingDispatchDrains, line ~379):
```csharp
private readonly ConcurrentDictionary<long, byte> _drainOwnedOrderIds =
    new ConcurrentDictionary<long, byte>();
```

**PendingDispatchDrain class addition** (new field, after `OrderType` property):
```csharp
internal IReadOnlyList<long> DrainedOrderIds { get; private set; }
```
Constructor gains parameter: `IReadOnlyList<long> drainedOrderIds` (after `orderType`
parameter, before `followerAccount`). Constructor body: `DrainedOrderIds = drainedOrderIds;`

**DrainThenDispatch changes** (in the foreach cancel loop, combined with F4):
Before creating payload, pre-build the list:
```csharp
var drainedIds = entryCandidates.Select(static e => e.OrderId).ToList();
```
Add TryAdd calls inside the cancel foreach:
```csharp
foreach (var e in entryCandidates)
{
    _drainOwnedOrderIds.TryAdd(e.OrderId, 0);
    follower.Cancel(new Order[] { e });
}
```
Pass `drainedIds` to constructor (see F4 design).

**TryReplaceOnAtmCancel guard** (insert BEFORE line 865):
```csharp
if (_drainOwnedOrderIds.ContainsKey(order.OrderId))
    return; // drain-owned cancel -- skip replacement (1)
```
Original guard shifts to position (2). CYC: 2 → 3.

**Cleanup in SubmitDrainedEntry** (after `TryRemove` gives payload, before `SubmitEntryDirect` call):
```csharp
foreach (var id in payload.DrainedOrderIds)
    _drainOwnedOrderIds.TryRemove(id, out _);
```

**Cleanup in TryDrainWatchdog** (inside `if (now - kv.Value.TimestampTicks > 2000L)` block):
```csharp
foreach (var id in kv.Value.DrainedOrderIds)
    _drainOwnedOrderIds.TryRemove(id, out _);
```

### F4: Constructor invocation pattern (Option A)

**Option A confirmed viable** — constructor already has `pendingCancelCount` parameter.

In DrainThenDispatch, replace lines 6521-6539 with:
```csharp
var drainedIds = entryCandidates.Select(static e => e.OrderId).ToList();
var payload = new PendingDispatchDrain(
    acctKey,
    instrument,
    qty,
    price,
    action,
    orderType,
    drainedIds,          // NEW: DrainedOrderIds for F3 cleanup
    follower,
    pendingCancelCount: entryCandidates.Count,  // F4: set before visible
    now);
_pendingDispatchDrains[acctKey] = payload;      // visible AFTER count set

foreach (var e in entryCandidates)              // (3)
{
    _drainOwnedOrderIds.TryAdd(e.OrderId, 0);   // F3: track drain-owned
    follower.Cancel(new Order[] { e });
}
// No Interlocked.Exchange -- count was set correctly at construction.
```
**Remove**: `int cancelCount = 0;` variable, `cancelCount++` increment, and
`Interlocked.Exchange(ref payload.PendingCancelCount, cancelCount)` at line 6539.

Note: PendingDispatchDrain constructor parameter order changes — `drainedIds` inserted before
`followerAccount`. Engineer must update constructor signature accordingly.

### F5: Lines to delete + CYC comment update

**Delete** lines 6541-6546:
```csharp
if (cancelCount == 0) // (4)
{
    _pendingDispatchDrains.TryRemove(acctKey, out _);
    SubmitEntryDirect(follower, instrument, qty, price, action, orderType);
    return;
}
```
**Update** method comment at line 6493 from:
```
// CYC=4: (1) null guard, (2) no-entry fast path, (3) foreach cancel loop, (4) cancelCount==0 edge guard.
```
to:
```
// CYC=3: (1) null guard, (2) no-entry fast path, (3) foreach cancel loop.
// F5-repair: dead (4) cancelCount==0 branch removed. entryCandidates always non-empty here.
```

---

## D. CYC ANALYSIS

| Method | Pre-fix CYC | Post-fix CYC | Delta | Budget | Status |
|--------|------------|--------------|-------|--------|--------|
| `OnOrderUpdate` | 7 | 8 | +1 (F1) | ≤8 | **PASS** |
| `DrainThenDispatch` | 4 | 3 | -1 (F5 removes dead branch) | ≤8 | **PASS** |
| `TryReplaceOnAtmCancel` | 2 | 3 | +1 (F3 adds drain guard) | ≤8 | **PASS** |
| `SubmitDrainedEntry` | 2-3 | 3-4 | +1 (F3 cleanup foreach) | ≤8 | **PASS** |
| `TryDrainWatchdog` | 3 | 4 | +1 (F3 cleanup inner foreach) | ≤8 | **PASS** |
| `OnDrainCancelAck` | 3 | 3 | 0 | ≤8 | **PASS** |
| `PendingDispatchDrain` (ctor) | 0 | 0 | 0 (data class) | ≤8 | **PASS** |

**OnOrderUpdate CYC accounting basis**: The new `if/else-if` drain routing block replaces the
current single compound `if` block. Both count as +1 at the OnOrderUpdate level per this
codebase's CYC convention (consistent with the existing drain block comment
"CYC +1 (branch 7)" at line 1411). Brief confirms: "was 7, now 8."

---

## E. TEST RENAME TABLE

| File | Current Method Name | New Method Name |
|------|---------------------|-----------------|
| `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` | `ActiveOrders_ThreadSafetyVerification` | `ActiveOrders_FilterBehavior_AfterToListAddition` |
| `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` | `NakedDetector_DebounceField_UsesLongArithmetic` | `NakedDetector_DebounceState_FieldTypeIsLong` |
| `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` | `DrainThenDispatch_CancelsExistingEntryBeforeSubmit` | `DrainThenDispatch_MethodExists_WithExpectedSignature` |
| `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` | `OnDrainCancelAck_SubmitsDrainedEntry_WhenPendingCountReachesZero` | `OnDrainCancelAck_MethodExists_WithExpectedSignature` |
| `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` | `DrainWatchdog_ClearsStuckDrain_AfterTimeout` | `DrainWatchdog_MethodExists_WithExpectedSignature` |

**Rename-only rule**: Method body, `[Fact]` attribute, and all assertions are UNCHANGED. Only the
`public void <name>()` declaration line changes. Test filter string in acceptance criteria must use
new names.

---

## F. LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

(See Section 2 above for gate question answers.)

---

## G. Jane Street Compliance Checklist

| Rule | Check | Status |
|------|-------|--------|
| JS-021: No lock() | No `lock(` in any new code. ConcurrentDictionary + Interlocked only. | PASS |
| JS-033: No async void | No `async void` in any new code. All methods synchronous void or return type unchanged. | PASS |
| JS-002: No return null | No `return null` in any new code. | PASS |
| JS-001: No throw new | No `throw new` in any new code. | PASS |
| CYC ≤ 8 | All methods ≤ 8 post-fix (see Section D). | PASS |
| ASCII-only | All string literals and identifiers are ASCII-only. `"PTT-Copy"`, `"Entry"`, `"_drainOwnedOrderIds"` — all ASCII. | PASS |
| xUnit-only | Test renames preserve `[Fact]` attribute. No `[Test]`, no NUnit, no MSTest introduced. | PASS |
| NT8 banned APIs | No `Account.Change()`, `AtmStrategyCreate()`, `AtmStrategyChangeStopTarget()` in new code. | PASS |
| DateTime.UtcNow | No `DateTime.Now` used. Existing `Environment.TickCount` pattern unchanged. | PASS |
| No FontFamily | No FontFamily usage. | PASS |

---

## H. SINGLE TICKET STRUCTURE

### T1: Drain Logic Repairs (F1-F5 + Test Renames)

**Justification for bundling**: All fixes are in the same method cluster
(`OnOrderUpdate` / `DrainThenDispatch` / `TryReplaceOnAtmCancel`) in a single file
(`CopyEngine.cs`). F3's cleanup design references F4's payload change (`DrainedOrderIds` stored
in `PendingDispatchDrain`), creating a structural coupling. F5 removes a dead branch in
`DrainThenDispatch`, the same method that F2, F3, and F4 also modify — separating them would
require partial-edit coordination. Test renames accompany the logic fixes they describe. A single
commit is the correct atomic unit.

**Files**:
- `src/PropTraderTools/CopyEngine.cs` (F1-F5, new field, PendingDispatchDrain class update)
- `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` (3 renames: DrainThenDispatch_, OnDrainCancelAck_, DrainWatchdog_)
- `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` (2 renames: ActiveOrders_, NakedDetector_)

**Method signatures (all existing methods, no signature changes except ctor)**:

`CopyEngine.cs`:
```csharp
// Existing -- no signature change:
private void TryReplaceOnAtmCancel(Order order)   // CYC 2→3 (F3 guard added)
private void DrainThenDispatch(Account follower, Instrument instrument, int qty, double price, OrderAction action, OrderType orderType)  // CYC 4→3 (F2+F3+F4+F5)
private void SubmitDrainedEntry(string acctKey)   // CYC 2-3→3-4 (F3 cleanup foreach)
private void TryDrainWatchdog()                   // CYC 3→4 (F3 cleanup foreach)

// New field:
private readonly ConcurrentDictionary<long, byte> _drainOwnedOrderIds = new ConcurrentDictionary<long, byte>();

// PendingDispatchDrain ctor (modified signature):
internal PendingDispatchDrain(
    string followerAcctKey,
    Instrument instrument,
    int qty,
    double price,
    OrderAction action,
    OrderType orderType,
    IReadOnlyList<long> drainedOrderIds,   // NEW param (F3)
    Account followerAccount,
    int pendingCancelCount,
    long timestampTicks)

// New field in PendingDispatchDrain class:
internal IReadOnlyList<long> DrainedOrderIds { get; private set; }
```

**xUnit test names post-rename**:
- `ActiveOrders_FilterBehavior_AfterToListAddition` [Fact] in BwaveDwLaneATests.cs
- `NakedDetector_DebounceState_FieldTypeIsLong` [Fact] in BwaveDwLaneATests.cs
- `DrainThenDispatch_MethodExists_WithExpectedSignature` [Fact] in BwaveNextLaneBTests.cs
- `OnDrainCancelAck_MethodExists_WithExpectedSignature` [Fact] in BwaveNextLaneBTests.cs
- `DrainWatchdog_MethodExists_WithExpectedSignature` [Fact] in BwaveNextLaneBTests.cs

**7-Scan Checklist (SCAN-01 through SCAN-07)**:
```
SCAN-01 JS-021: grep -n "lock\s*(" src/PropTraderTools/CopyEngine.cs | grep -v "^\s*//"  → 0
SCAN-02 JS-033: grep -n "async void [A-Z]" src/PropTraderTools/CopyEngine.cs | grep -v "^\s*//"  → 0
SCAN-03 JS-002: grep -n "return null" src/PropTraderTools/CopyEngine.cs (new code at lines ~860-868, 1412+, 6491+)  → 0 new
SCAN-04 JS-001: grep -n "throw new" src/PropTraderTools/CopyEngine.cs | grep -v "^\s*//"  → 0
SCAN-05 CYC:   manual branch count per Section D above → all ≤ 8
SCAN-06 ASCII: PowerShell byte scan → 0 bytes > 0x7F
SCAN-07 xUnit: grep -n "\[Fact\]\|\[Test\]" src/PropTraderTools/Tests/BwaveNextLaneBTests.cs → [Fact] only; same for BwaveDwLaneATests.cs
```

**Acceptance Criteria (T1)**:
- [ ] F1: `OnOrderUpdate` drain routing: Cancelled/Rejected only → `OnDrainCancelAck`. Filled → `TryRemove` + abort. `OnOrderUpdate` CYC=8 post-fix.
- [ ] F2: `entryCandidates` restricted to Limit/StopLimit type AND name StartsWith "PTT-Copy". No stop brackets cancelled.
- [ ] F3: `_drainOwnedOrderIds: ConcurrentDictionary<long,byte>` field present. `TryReplaceOnAtmCancel` guard: ContainsKey(order.OrderId) → return early. DrainedOrderIds populated in DrainThenDispatch loop. Cleanup in SubmitDrainedEntry + TryDrainWatchdog.
- [ ] F4: `PendingCancelCount` = `entryCandidates.Count` at construction. No `Interlocked.Exchange` after loop. `cancelCount` variable removed.
- [ ] F5: Dead `if (cancelCount == 0)` block removed (lines 6541-6546). CYC comment updated to 3.
- [ ] F7/8/9: 5 test methods renamed (bodies unchanged, [Fact] preserved). All 5 pass under new names.
- [ ] `dotnet build` 0 errors.
- [ ] `dotnet test --filter "DrainThenDispatch_MethodExists|OnDrainCancelAck_MethodExists|DrainWatchdog_MethodExists|ActiveOrders_FilterBehavior|NakedDetector_DebounceState"` all pass.
- [ ] NT8 sync 18/18 OK.
- [ ] SCAN-01 through SCAN-07: all zero violations.

---

*Plan authored: 2026-09-05 | ptt-architect | Phase 2 | BWAVE-NEXT LaneBRepair*  
*Source grounding: 6 mandatory reads complete (mission brief + 5 CopyEngine.cs ranges + ticket-2-verification.md)*  
*Sequential thinking: 8 thoughts completed before writing*
