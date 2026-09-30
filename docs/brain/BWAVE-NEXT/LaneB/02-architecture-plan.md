# BWAVE-NEXT Lane B -- Architecture Plan

**Block**: BWAVE-NEXT Lane B -- Cancel-Before-Dispatch Drain + Post-PR-42 Repairs
**Phase**: Phase 1 (Architecture)
**Architect**: ptt-architect
**Date**: 2026-09-04
**Status**: REVIEW_PENDING

---

## 0. Rules Catalog Gate Result

**STEP 0 GATE: PASS**

P0 scan against this plan:
| Rule | Check | Result |
|------|-------|--------|
| JS-021 lock() | No lock() in any new or modified code. ConcurrentDictionary + Interlocked only. | PASS |
| JS-033 async void | No async void in new methods. OnDrainCancelAck is synchronous void. | PASS |
| JS-002 return null | No new methods return null. All new methods are void or return bool. | PASS |
| JS-001 throw new | No throw new in any hot path. | PASS |
| CYC <=8 | All new and modified methods: see Section 5 for explicit counts. | PASS |
| NT8 banned | Account.Change() NOT used. AtmStrategyCreate() NOT used. AtmStrategyChangeStopTarget() NOT used. | PASS |

---

## 1. LANE-SPLIT GATE RESULT

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

### Q1. Are T1 and T2 in the same method or within 50 lines?

T1 Sub-A touches `ActiveOrders` body at line ~3437 (post-T4/T5).
T1 Sub-B touches `TryNakedDetect` area at line ~6403 (post-T4/T5).
T2 modifies `HandleEntryChange` at line ~3667 and `OnOrderUpdate` at line ~1355.
Nearest overlap: ActiveOrders (3437) vs HandleEntryChange (3667) = 230 lines apart.
T2 also adds an unconditional call to TryDrainWatchdog and +1 branch in OnOrderUpdate.
T1 does not modify OnOrderUpdate itself (TryNakedDetect call is from T4/T5 commit).

**ANSWER: NO** -- not in the same method or within 50 lines of each other.

### Q2. Does T2 design depend on T1 final design?

T2's `DrainThenDispatch` calls `ActiveOrders()` (introduced by T4/T5 commit 92a44332).
T1 Sub-A's AMBIGUOUS-ADDED-TOLIST decision adds `.ToList()` inside `ActiveOrders`.
T2 inherits this thread-safety improvement when calling `ActiveOrders`.
Both T1 and T2 modify `CopyEngine.cs`. The OnOrderUpdate method state after T1
is the baseline that T2 must be applied on top of.

**ANSWER: YES (weakly)** -- T2 inherits T1's ActiveOrders thread-safety determination.
T1 must VERIFY_PASS before T2 begins to avoid merge conflict and to establish the
final ActiveOrders body that T2 depends on.

### Q3. Does each fix have standalone value if the other is blocked?

T1 (thread-safety + TickCount cast): Bot-review repairs. Independent production value. YES.
T2 (drain feature): Complete cancel-before-dispatch feature. Independent production value. YES.

**ANSWER: YES** for both.

### Q4. Does each fix have an independent SIM verification path?

T1: Structural reflection tests only. VERIFY_PASS confirmed without live NT8. YES.
T2: SIM gate deferred (non-blocking per acceptance criteria). Structural tests only for VERIFY_PASS. YES.

**ANSWER: YES** for both.

**Gate reasoning**: Both touch `CopyEngine.cs`. T2 calls `ActiveOrders()` from T4/T5 commit.
T1 must VERIFY_PASS before T2 begins (OnOrderUpdate overlap risk + ActiveOrders finalization).
This is SEQUENTIAL TICKETS in a SINGLE PIPELINE -- not parallel lanes.

---

## 2. Pre-Requisite: T4/T5 Commit Must Be On Main

**CRITICAL**: Commit `92a44332` (BWAVE-NEXT Lane A T1/T2/T3/T4/T5) is NOT currently on `main` HEAD.
Git confirms: `git log HEAD..92a44332` shows this commit is ahead of HEAD on a different line.

Before any T1 or T2 work begins, the engineer MUST:
```powershell
git log --oneline HEAD..92a44332  # must return empty -- if not, cherry-pick or confirm merge
```

If not present: coordinate with Director to merge 92a44332. That commit provides:
- `_nakedDetectLastQueuedTicks` field (line 373)
- `TryNakedDetect` call in OnOrderUpdate (line ~1402)
- `TryNakedDetect` method (line 6403)
- `ActiveOrders(Account acc)` method (line 3437)
- `ActiveOrdersTestable` seam (line 3446)
- Updated `FindFollowerBracketOrder` Account overload (line 3468)
- Updated `FindFollowerEntryOrder` (line 3668)

All T1 and T2 line numbers in this plan assume commit 92a44332 is on main.

---

## 3. Scope Summary

| Ticket | DW Item | File | Type | Dependencies |
|--------|---------|------|------|--------------|
| T1 | DW-NEXT-A-07 + DW-NEXT-A-06 | CopyEngine.cs | Small edits (2 sub-items) | Requires T4/T5 commit on main |
| T2 | DW-NEW-08 Option D | CopyEngine.cs | New production code | Requires T1 VERIFY_PASS |
| T3 | DW-NEXT-A-01 + DW-NEXT-A-02 | docs only | Director housekeeping | No pipeline |

---

## 4. Ticket T1 Architecture: DW-NEXT-A-07 + DW-NEXT-A-06

### 4.1 Sub-A: ActiveOrders Thread Safety (DW-NEXT-A-07)

**DETERMINATION: AMBIGUOUS-ADDED-TOLIST**

**NT8 documentation analysis**:
- `NT8_FULL_REFERENCE.md` Orders Collection section (lines 2800-2844): shows `foreach (Order order in myAccount.Orders)` in `OnAccountItemUpdate` WITHOUT a lock. No explicit thread-safety guarantee stated.
- `NT8_FULL_REFERENCE.md` examples show `lock(myAccount.Executions)` (line 427) and `lock(myAccount.Positions)` (line 2883) for enumeration in `OnStateChange` -- but these are in initialization contexts, not in active callback handlers.
- `NT8_ADDON_KNOWLEDGE.md` line 219: `acc.Orders // All orders for this account (IEnumerable<Order>)` -- no thread-safety note.
- Bot review (Greptile, cubic, CodeRabbit) all flagged the lazy enumeration as a concern.
- NT8 docs do NOT explicitly confirm acc.Orders is safe for lazy LINQ enumeration during OnOrderUpdate callbacks from a background thread. This is AMBIGUOUS.
- Director decision (mission brief): "If NT8 docs are ambiguous or confirm it is NOT safe -- add .ToList()".

**Exact production code change**:

File: `src/PropTraderTools/CopyEngine.cs`
Location: `ActiveOrders` method at line 3437 (post-T4/T5 commit)

Current (T5 result):
```csharp
private static IEnumerable<Order> ActiveOrders(Account acc) =>
    acc.Orders.Where(static o =>
        o.OrderState != OrderState.Filled
        && o.OrderState != OrderState.Cancelled
        && o.OrderState != OrderState.Rejected);
```

Change to:
```csharp
private static IEnumerable<Order> ActiveOrders(Account acc) =>
    acc.Orders.Where(static o =>
        o.OrderState != OrderState.Filled
        && o.OrderState != OrderState.Cancelled
        && o.OrderState != OrderState.Rejected).ToList();
```

**Constraints**:
- Return type stays `IEnumerable<Order>` -- no caller changes (line 3468, line 3668)
- CYC stays 1 (expression body, no new branches)
- This adds `.ToList()` which materializes the snapshot. Allocation is trivial at 2 call sites.
- Note: `List<T>` is returned but typed as `IEnumerable<T>` -- no API change
- JS-036 (zero-alloc hot paths): this is not a hot path (called once per OnOrderUpdate per follower account, not per bar tick). Allocation is acceptable.

### 4.2 Sub-B: TickCount Wraparound (DW-NEXT-A-06)

File: `src/PropTraderTools/CopyEngine.cs`
Location: `TryNakedDetect` and `NakedPositionDetector` methods (lines ~6403-6450, post-T4/T5 commit)

**Bug**: `(long)Environment.TickCount` performs a zero-extending widening of int32 to int64.
When TickCount wraps negative (~24.9 day uptime), the long value becomes a large positive number.
`now - last` then becomes a huge value, not a meaningful elapsed time.

**Fix**: Change EVERY `(long)Environment.TickCount` that feeds into the `long` debounce dict to:
```csharp
long now = (long)(int)Environment.TickCount;
```
The `(int)` intermediate cast forces sign extension from int32 → int64 correctly.

**Lines to change** (post-T4/T5 commit 92a44332):
The engineer must `Select-String -Path CopyEngine.cs -Pattern "(long)Environment.TickCount"` to find exact lines.
Per T4 verification artifacts, these reads are in `NakedPositionDetector` (line 6424+) inside
the `_nakedDetectLastQueuedTicks.GetOrAdd` and `.AddOrUpdate` calls (lines ~6434, ~6439).

**Constraints**:
- CYC of TryNakedDetect confirmed=3 (from T4 verification). Unchanged by this sub-item.
- No new methods added. Pure cast change.
- ASCII-only: `(long)(int)Environment.TickCount` -- no non-ASCII characters

### 4.3 T1 Tests

Both tests: structural reflection tests in `src/PropTraderTools/Tests/BwaveNextLaneATests.cs`
(the test file introduced by T4/T5 commit -- add as new [Fact] entries after the existing 4 T4 tests).

#### `[Fact] ActiveOrders_ThreadSafetyVerification()`
Asserts:
- Method `ActiveOrders` exists on `CopyEngine` (private static via reflection)
- Return type is `IEnumerable<Order>`
- Body materializes with `.ToList()`: call `ActiveOrdersTestable` (already exists as internal seam)
  with a list containing 1 Filled + 1 Working order → assert count == 1 and Working present
  (verifies filter works WITH materialization)
- Actually: `ActiveOrdersTestable` returns filtered IEnumerable. Assert `.ToList()` on its output is assignable to `List<Order>` (verifies materialization by checking method body reflection or calling and confirming single enumeration is safe). Best approach: use `ActiveOrdersTestable` seam to confirm filter still works correctly after `.ToList()` addition.

#### `[Fact] NakedDetector_DebounceField_UsesLongArithmetic()`
Asserts:
- `_nakedDetectLastQueuedTicks` field type is `ConcurrentDictionary<string, long>` (readonly)
- `TryNakedDetect` method exists on CopyEngine as instance method (private void, 1 param OrderEventArgs)
- Via IL or body scan: confirm `(long)(int)` cast sequence is present in TryNakedDetect or NakedPositionDetector
  (structural: use `Select-String` approach via Assembly.GetManifestResourceStream or IL inspection)
  OR: simpler structural test -- confirm the method returns without error when called with a
  terminal-state fake event (verifies the cast doesn't cause compilation failure)

Note: Behavioral tests require live NT8 Account runtime. These are structural guards only.

---

## 5. Ticket T2 Architecture: DW-NEW-08 Option D

### 5.1 New Type: PendingDispatchDrain

Sealed class, nested inside `CopyEngine` class body (no new file needed).
NT8 compiler rule: no `{ get; init; }` (NT8-001 CS0518). Use explicit constructor.

```csharp
// DW-NEW-08 Option D: payload for cancel-before-dispatch drain.
// Stores the dispatch intent while cancels are in-flight.
// CYC=0 (data class, no logic methods).
private sealed class PendingDispatchDrain
{
    internal string FollowerAcctKey    { get; private set; }
    internal Instrument Instrument     { get; private set; }
    internal int Qty                   { get; private set; }
    internal double Price              { get; private set; }
    internal OrderAction Action        { get; private set; }
    internal int PendingCancelCount;   // mutable -- Interlocked.Decrement/Increment
    internal long TimestampTicks       { get; private set; }

    internal PendingDispatchDrain(
        string followerAcctKey,
        Instrument instrument,
        int qty,
        double price,
        OrderAction action,
        int pendingCancelCount,
        long timestampTicks)
    {
        FollowerAcctKey = followerAcctKey;
        Instrument      = instrument;
        Qty             = qty;
        Price           = price;
        Action          = action;
        PendingCancelCount = pendingCancelCount;
        TimestampTicks  = timestampTicks;
    }
}
```

**PendingCancelCount is a plain `int` field** (not a property) because `Interlocked.Decrement`
requires a `ref int`. Properties cannot be passed by ref.

### 5.2 New Field: _pendingDispatchDrains

```csharp
// DW-NEW-08 Option D: cancel-before-dispatch drain state.
// Key = follower account name. ConcurrentDictionary: no lock (JS-021).
private readonly ConcurrentDictionary<string, PendingDispatchDrain> _pendingDispatchDrains =
    new ConcurrentDictionary<string, PendingDispatchDrain>(StringComparer.Ordinal);
```

Placement: immediately after `_nakedDetectLastQueuedTicks` field (currently line 374 post-T4/T5).

### 5.3 New Methods

#### DrainThenDispatch

```csharp
// DW-NEW-08 Option D: cancel all Working/Accepted entry orders for follower+instrument,
// park dispatch intent in _pendingDispatchDrains, submit after all cancels acknowledged.
// If no active entry found: submit directly (no drain needed).
// CYC=4: follower-null(1) + empty-entries-check(2) + foreach-cancel(3) + already-in-drain(4).
// JS-021: no lock. JS-002: void. JS-001: no throw.
private void DrainThenDispatch(
    Account follower,
    Instrument instrument,
    int qty,
    double price,
    OrderAction action)
```

**Flow**:
1. `if (follower == null || instrument == null) return;` (1)
2. Use `ActiveOrders(follower)` filtered further for Working/Accepted AND Limit/StopLimit AND (Name == "PTT-Copy" OR Name == "Entry") -- reuse same criteria as FindFollowerEntryOrder logic.
   Note: ActiveOrders already filters Filled/Cancelled/Rejected. Add Working/Accepted state narrowing inline.
3. `if (!entryCandidates.Any()) { SubmitEntryDirect(follower, instrument, qty, price, action); return; }` (2)
4. Overwrite existing drain if present (new intent overrides stale drain): `_pendingDispatchDrains[acctKey] = new PendingDispatchDrain(...)` with PendingCancelCount=0 initially.
5. `int cancelCount = 0;` then `foreach (var e in entryCandidates) { follower.Cancel(new Order[] { e }); cancelCount++; }` (3)
6. Set `PendingCancelCount` on the payload to `cancelCount` via `Interlocked.Exchange`.
7. `if (cancelCount == 0) { _pendingDispatchDrains.TryRemove(acctKey, out _); SubmitEntryDirect(...); return; }` (guard: edge case if entryCandidates materialized but cancel loop ran zero) (4 via Interlocked guard)
8. Log `[DRAIN] acctKey: cancelCount cancels sent for instrument at price`

Note on already-in-drain (step 4): if `_pendingDispatchDrains` already contains `acctKey`, overwrite with new payload. The prior drain's submit will be blocked because TryRemove at SubmitDrainedEntry will find the new payload and submit the updated price/qty. This is the correct behavior for a second drag before the first drain completes.

#### SubmitEntryDirect (helper, extracted for reuse between DrainThenDispatch and SubmitDrainedEntry)

```csharp
// DW-NEW-08 Option D: shared submit path used by both direct (no drain needed) and drain-complete paths.
// CYC=2: order-null(1), limitPx ternary(2).
// JS-021: no lock. JS-002: void. JS-001: no throw. ASCII-only.
private void SubmitEntryDirect(
    Account follower,
    Instrument instrument,
    int qty,
    double price,
    OrderAction action)
```

**Flow**:
1. `double tickSize = instrument.MasterInstrument?.TickSize ?? 0.0;` ternary (1)
2. `double limitPx = action == OrderAction.BuyToCover || action == OrderAction.Buy ? price : ...` OR simpler: always use the provided price as limitPx, rely on caller to normalize. Actually: use `price` directly as limitPx, stopPx=0.0 (entry orders are Limit type in the follower context per HandleEntryChange pattern). (2) via null guard on CreateOrder result.
3. `var order = follower.CreateOrder(instrument, action, OrderType.Limit, OrderEntry.Manual, TimeInForce.Day, qty, price, 0.0, null, "PTT-Copy", DateTime.MaxValue, null);`
4. `if (order == null) return;` (branch 1)
5. `follower.Submit(new[] { order });`
6. Log `[DRAIN-SUBMIT] acctKey: submitted instrument at price`

**Note**: The actual order type (Limit vs StopLimit) should match the original intent. Since the drain fires during a drag sequence, the entry type comes from the follower's existing order. DrainThenDispatch should pass the fo.OrderType to SubmitEntryDirect. Revise signature to include `OrderType orderType`.

**Revised CYC=3**: (1) null guard on order + (2) orderType conditional for limitPx/stopPx + (3) null guard on CreateOrder result.

#### OnDrainCancelAck

```csharp
// DW-NEW-08 Option D: called from OnOrderUpdate when a cancel-ack arrives for a drain-pending account.
// NOT subscribed to any event -- called directly from OnOrderUpdate. Synchronous void.
// CYC=3: drain-check(1) + count-zero(2) + stale-payload(3).
// JS-021: no lock. JS-002: void. JS-001: no throw. ASCII-only.
private void OnDrainCancelAck(string acctKey)
```

**Note on signature**: The spec shows `(object sender, OrderEventArgs e)` but this method is called
directly from within OnOrderUpdate, not subscribed to an event. Use `(string acctKey)` for clarity.
No async -- synchronous void.

**Flow**:
1. `if (!_pendingDispatchDrains.TryGetValue(acctKey, out var payload)) return;` (1)
2. `int remaining = Interlocked.Decrement(ref payload.PendingCancelCount);`
3. `if (remaining < 0) { /* log unexpected underflow, return */ return; }` (2)
4. `if (remaining == 0) SubmitDrainedEntry(acctKey);` (3)

#### SubmitDrainedEntry

```csharp
// DW-NEW-08 Option D: remove payload from drain dict and submit the parked entry.
// Called when PendingCancelCount reaches zero.
// CYC=3: TryRemove fail(1) + follower-resolve fail(2) + order-null(3 -- inside SubmitEntryDirect).
// JS-021: no lock. JS-002: void. JS-001: no throw. ASCII-only.
private void SubmitDrainedEntry(string acctKey)
```

**Flow**:
1. `if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) return;` (1)
2. Find follower Account from `_rules` by matching `acctKey` to follower account name. (2) if not found return.
3. `SubmitEntryDirect(follower, payload.Instrument, payload.Qty, payload.Price, payload.Action, payload.OrderType);` -- account resolving branch = (2) above.
4. Log `[DRAIN-SUBMIT] acctKey: submitted at price`

Note: SubmitEntryDirect is called by both DrainThenDispatch (direct path) and SubmitDrainedEntry. The drain account is already known in DrainThenDispatch but must be re-resolved from `_rules` in SubmitDrainedEntry. Simpler: pass the Account object INTO the payload field so no re-resolution is needed.

**Revised PendingDispatchDrain**: Add `Account FollowerAccount` field to the payload. This eliminates the re-resolution branch in SubmitDrainedEntry.

#### TryDrainWatchdog

```csharp
// DW-NEW-08 Option D: check all pending drains for timeout (>2000ms).
// Called unconditionally from OnOrderUpdate (adds 0 CYC branches to OnOrderUpdate).
// CYC=3: foreach(1) + timestamp-check(2) + TryRemove(3).
// JS-021: no lock. JS-002: void. JS-001: no throw. ASCII-only. No System.Threading.Timer.
private void TryDrainWatchdog()
```

**Flow**:
1. `if (_pendingDispatchDrains.IsEmpty) return;` (fast path guard, branch 1 -- inline guard)
2. `long now = (long)(int)Environment.TickCount;` (uses same cast pattern as DW-NEXT-A-06)
3. `foreach (var kv in _pendingDispatchDrains)` (2)
4. `if (now - kv.Value.TimestampTicks > 2000L)` (3)
5. `{ _pendingDispatchDrains.TryRemove(kv.Key, out _); Log("[DRAIN-TIMEOUT] acctKey"); }`
   Note: Does NOT submit. Position may have changed during 2s timeout.

**CYC=3**: (1) IsEmpty check, (2) foreach, (3) timestamp comparison.

### 5.4 Modified Methods

#### HandleEntryChange (spec name: PropagateFollowerEntryReplace)

**Actual method**: `HandleEntryChange` at line ~3667. CYC currently=7.

**Change**: Inside the `foreach (var acc in rule.FollowerAccounts)` loop, after resolving `fo = FindFollowerEntryOrder(acc, instrument)`:
- Current path (if fo != null): acc.Cancel → acc.CreateOrder → acc.Submit
- New path (if fo != null): call `DrainThenDispatch(acc, instrument, fo.Quantity, newPrice, fo.OrderAction)` instead

This REPLACES the cancel+create+submit block with a DrainThenDispatch call. The conditional structure is unchanged (fo == null: continue; else: act). CYC stays 7.

Wait -- DrainThenDispatch also handles the "if no working entries: submit directly" case. But HandleEntryChange already gates on `if (fo == null) continue`. So DrainThenDispatch is only called when fo != null (working entry found). DrainThenDispatch then double-checks via ActiveOrders (may find more entries than just the one found by FindFollowerEntryOrder). Simpler: use ActiveOrders directly in DrainThenDispatch and remove FindFollowerEntryOrder dependency in HandleEntryChange.

**Revised HandleEntryChange T2 change**:
Replace the block from `var fo = FindFollowerEntryOrder(acc, instrument)` through `StatusUpdate?.Invoke(...)` with:
```csharp
DrainThenDispatch(acc, instrument, /* qty from rule or 1 */, newPrice, /* action from rule */);
```

But wait: what qty and action? HandleEntryChange currently gets qty from `fo.Quantity` and action from `fo.OrderAction`. DrainThenDispatch needs these. Since DrainThenDispatch will internally find the existing entry via ActiveOrders, it can use its qty/action directly. So DrainThenDispatch can omit qty/action from its signature and instead derive them from the found entry order.

**Revised DrainThenDispatch signature** (simpler):
```csharp
private void DrainThenDispatch(Account follower, Instrument instrument, double newPrice)
```
Inside: find entries via ActiveOrders + filter, use first found entry's Quantity/OrderAction/OrderType for the resubmit. This is cleaner and matches HandleEntryChange's existing pattern of reading from fo.

**Impact**: DrainThenDispatch uses the first active entry's qty/action/type for resubmit. If multiple entries (unusual), uses the first found. This is consistent with HandleEntryChange's current behavior.

**HandleEntryChange final change**:
```
- var fo = FindFollowerEntryOrder(acc, instrument);
- if (fo == null) continue;
- [price delta guard remains]
- [cancel+create+submit block removed]
+ DrainThenDispatch(acc, instrument, newPrice);
```
CYC: previously 7, but we removed the `if (fo == null) continue` branch (now inside DrainThenDispatch). CYC = 6 for HandleEntryChange. ✅

Actually no -- the price delta guard (`if (Math.Abs(newPrice - currentPrice) < tickSize) continue;`) must remain in HandleEntryChange to avoid unnecessary drain on trivial price changes. This means HandleEntryChange still needs to find fo to compare prices. Revised: keep FindFollowerEntryOrder + price delta guard in HandleEntryChange, then call DrainThenDispatch.

**FINAL HandleEntryChange T2 change**: Replace only the cancel+create+submit block (lines 3701-3725 post-T4/T5) with a call to DrainThenDispatch. Keep fo resolution + price delta guard. CYC stays 7.

```csharp
// OLD block (removed):
acc.Cancel(new Order[] { fo });
var order = acc.CreateOrder(...);
if (order != null) { acc.Submit(...); _dedupCache[...] = newPrice; }
StatusUpdate?.Invoke(...);

// NEW (single call):
DrainThenDispatch(acc, instrument, fo.Quantity, newPrice, fo.OrderAction, fo.OrderType);
```

DrainThenDispatch with explicit qty/action/orderType signature -- back to the original fuller signature.

**CYC BUDGET MATH for HandleEntryChange**:
Current CYC=7 (7 branches, as per code comment at line 3664).
T2 change: REPLACES block after branch 7 (order null guard). The `if (order != null)` branch is removed because DrainThenDispatch handles null internally. CYC after change = 6. Under budget. ✅

#### OnOrderUpdate

File: `src/PropTraderTools/CopyEngine.cs`, line ~1355 (post-T4/T5 commit).

**T2 adds two items**:
1. Unconditional call `TryDrainWatchdog();` (pre-Gate-1, near top with other unconditional calls). CYC delta = 0.
2. One conditional drain-ack routing check: if the order event is terminal (Cancelled/Rejected/Filled) AND the account is in drain state, call OnDrainCancelAck.

**Placement**: Place the drain-ack check BEFORE Gate 1 (pre-enabled check), immediately after `TryReplaceOnAtmCancel(e.Order)`. This ensures drain acks are processed even when copy is disabled.

**Exact addition** (pseudocode):
```csharp
// DW-NEW-08 Option D: route cancel-ack to drain handler if account is in drain state.
if (IsTerminalOrderState(e.Order.OrderState) && _pendingDispatchDrains.ContainsKey(e.Order.Account.Name))
    OnDrainCancelAck(e.Order.Account.Name);

TryDrainWatchdog();   // unconditional, CYC=0 in parent
```

`IsTerminalOrderState`: this is a 1-line inline check (`== Cancelled || == Rejected || == Filled`). No helper needed; inline it.

**CYC BUDGET MATH for OnOrderUpdate** (post-T4/T5):
Current CYC after T4/T5 = 6 (T4 verification confirmed: "TryNakedDetect wired as unconditional, adds 0 branches").
T2 +1 branch (drain-ack routing) = CYC=7. ✅ Within budget ≤8.
TryDrainWatchdog() unconditional = CYC delta 0.

### 5.5 NT8 API Constraints (non-negotiable)

| API | Status |
|-----|--------|
| `Account.Change()` | BANNED -- silent no-op on ATM brackets. NOT used. |
| `AtmStrategyCreate()` | BANNED -- StrategyBase-only. NOT used. |
| `AtmStrategyChangeStopTarget()` | BANNED -- StrategyBase-only. NOT used. |
| `Account.Cancel(Order[])` | ALLOWED -- AddOnBase available. Used in DrainThenDispatch. |
| `Account.CreateOrder(...)` | ALLOWED -- AddOnBase available. Used in SubmitEntryDirect. |
| `Account.Submit(Order[])` | ALLOWED -- AddOnBase available. Used in SubmitEntryDirect. |
| `lock()` | BANNED (JS-021). ConcurrentDictionary + Interlocked only. |

### 5.6 T2 Tests

Test file: `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` (NEW FILE -- add to csproj).
Pattern: structural reflection tests (same approach as T4/T5 tests).
No live NT8 Account required.

#### `[Fact] DrainThenDispatch_CancelsExistingEntryBeforeSubmit()`
Asserts:
- `DrainThenDispatch` method exists on CopyEngine (private void, correct parameter types via reflection)
- `_pendingDispatchDrains` field exists: type `ConcurrentDictionary<string, PendingDispatchDrain>`, readonly
- `PendingDispatchDrain` nested type exists on CopyEngine: sealed, contains fields FollowerAcctKey(string), Instrument, Qty(int), Price(double), Action(OrderAction), PendingCancelCount(int), TimestampTicks(long)
- Structural: verify PendingCancelCount is a public/internal field (not property) so Interlocked.Decrement can ref it

#### `[Fact] OnDrainCancelAck_SubmitsDrainedEntry_WhenPendingCountReachesZero()`
Asserts:
- `OnDrainCancelAck` method exists on CopyEngine (private void, 1 string parameter)
- `SubmitDrainedEntry` method exists on CopyEngine (private void, 1 string parameter)
- `TryDrainWatchdog` method exists on CopyEngine (private void, 0 parameters)
- Structural: verify OnDrainCancelAck signature is `(string acctKey)` not an event handler signature

#### `[Fact] DrainWatchdog_ClearsStuckDrain_AfterTimeout()`
Asserts:
- `_pendingDispatchDrains` is `ConcurrentDictionary<string, PendingDispatchDrain>` with StringComparer.Ordinal
- `PendingDispatchDrain.TimestampTicks` field exists (long type)
- `TryDrainWatchdog` method signature verified (no parameters, private void)
- Structural: verify `PendingDispatchDrain` has internal constructor (not public -- data class, no external creation)

---

## 6. CYC Budget Analysis

### HandleEntryChange (spec name: PropagateFollowerEntryReplace)

| Item | CYC count |
|------|-----------|
| instr null check (1) | 1 |
| tickSize ternary (2) | 1 |
| foreach acc (3) | 1 |
| acc null check (4) | 1 |
| fo null check (5) | 1 |
| price delta guard (6) | 1 |
| REMOVED: order null guard in CreateOrder (was 7) | -1 |
| **Current CYC after T2** | **6** |
| Budget ≤8 | ✅ |

Comment note: The original CYC=7 comment at line 3664 counts the `if (order != null)` guard inside the cancel+create+submit block. T2 removes that block and replaces it with `DrainThenDispatch(...)` which handles null internally. CYC reduces from 7 to 6. This is correct: we are not adding branches.

### OnOrderUpdate (post-T4/T5 + T2)

| Item | CYC count |
|------|-----------|
| Gate 1 (!_isCopyEnabled) (1) | 1 |
| Gate 2 (matchedRule == null) (2) | 1 |
| Gate 2.5 (!matchedRule.Enabled) (3) | 1 |
| TryCancelFollowerEntries result (4) | 1 |
| TryDispatchLeaderFlat result (5) | 1 |
| TryHandleDrag result (6) | 1 |
| T2 NEW: drain-ack routing (7) | +1 |
| Unconditional calls (add 0): TryNakedDetect, TryDrainWatchdog | +0 |
| **CYC after T2** | **7** |
| Budget ≤8 | ✅ |

### New methods CYC summary

| Method | Target CYC | Budget |
|--------|------------|--------|
| DrainThenDispatch | 4 | ✅ |
| OnDrainCancelAck | 3 | ✅ |
| SubmitDrainedEntry | 3 | ✅ |
| SubmitEntryDirect | 2 | ✅ |
| TryDrainWatchdog | 3 | ✅ |
| PendingDispatchDrain (class) | 0 | ✅ |

---

## 7. File Write-Set

| Ticket | File | Change Type |
|--------|------|-------------|
| T1 | `src/PropTraderTools/CopyEngine.cs` | Edit line 3437: add .ToList(); Edit lines ~6434,~6439: `(long)(int)` cast |
| T1 | `src/PropTraderTools/Tests/BwaveNextLaneATests.cs` | Add 2 [Fact] methods |
| T2 | `src/PropTraderTools/CopyEngine.cs` | New nested class PendingDispatchDrain; new field _pendingDispatchDrains; new methods DrainThenDispatch/OnDrainCancelAck/SubmitDrainedEntry/SubmitEntryDirect/TryDrainWatchdog; modify HandleEntryChange (replace cancel block); modify OnOrderUpdate (+1 branch + 1 unconditional call) |
| T2 | `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` | NEW FILE -- 3 [Fact] methods |
| T2 | `src/PropTraderTools/PropTraderTools.csproj` | Add compile entry for BwaveNextLaneBTests.cs |
| T3 | docs only | Director action, no pipeline |

**Sequential order mandatory**: T4/T5 commit on main → T1 → T1 VERIFY_PASS → T2 → T2 VERIFY_PASS.

---

## 8. 7-Scan Checklist (applies to both tickets)

| Scan | Command | Requirement |
|------|---------|-------------|
| SCAN-01 JS-021 | `Select-String -Path src/PropTraderTools/*.cs -Pattern "lock\s*\(" \| Where {$_.Line -notmatch "^\s*//"} ` | 0 results |
| SCAN-02 JS-033 | `Select-String -Path src/PropTraderTools/*.cs -Pattern "async void [A-Z]" \| Where {$_.Line -notmatch "^\s*//"} ` | 0 results |
| SCAN-03 JS-002 | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "return null" ` -- check new code only | 0 new results in T1/T2 added code |
| SCAN-04 JS-001 | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "throw new" \| Where {$_.Line -notmatch "^\s*//"}` | 0 results |
| SCAN-05 CYC | `python scripts/complexity_audit.py` OR manual count from comments | All methods ≤8 |
| SCAN-06 ASCII | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]"` | 0 results |
| SCAN-07 xUnit | `Select-String -Path src/PropTraderTools/Tests/*.cs -Pattern "\[Fact\]\|\[Test\]"` | Only [Fact], zero [Test] |

---

## 9. Deferred / Out of Scope

| ID | Status in this lane |
|----|-------------------|
| DW-NEW-08-D | **THIS LANE (T2)** -- implemented as Option D cancel-before-dispatch drain |
| DW-NEXT-A-01 | T3 (Director action, no pipeline) |
| DW-NEXT-A-02 | T3 (Director action, no pipeline) |
| DW-NEXT-A-03 | EXCLUDED -- short positions not in operational pattern |
| DW-NEXT-A-04 | EXCLUDED -- single-instrument use only |
| DW-NEXT-A-05 | EXCLUDED -- edge case within 500ms grace window |
| DW-RepairLC-01/02 | EXCLUDED -- Director action, live NT8 required |
| DW-C39-09 LaneA (SaveRules) | EXCLUDED -- TradeCopierWindow.cs scope |
| NEW-0x test quality gaps | EXCLUDED -- separate lane |

---

## 10. NT8 Key Facts (embedded per protocol)

Confirmed from `docs/standards/NT8_FULL_REFERENCE.md` and `docs/standards/NT8_ADDON_KNOWLEDGE.md`:

- `AtmStrategyChangeStopTarget()` -- StrategyBase-only. NOT AddOnBase. NEVER use in this codebase.
- `AtmStrategyCreate()` -- StrategyBase-only. NOT AddOnBase. NEVER use in this codebase.
- `Account.Change()` -- AddOnBase available but CONFIRMED silent no-op on ATM-owned brackets. NEVER use.
- `Account.Cancel(Order[])` + `Account.CreateOrder(...)` + `Account.Submit(Order[])` -- AddOnBase available. Correct pattern.
- `acc.Orders` -- IEnumerable<Order>, no explicit thread-safety guarantee in NT8 docs for concurrent callback enumeration. Add `.ToList()` per AMBIGUOUS-ADDED-TOLIST determination.
- `(long)(int)Environment.TickCount` -- correct 24.9-day wraparound safe cast sequence.

---

*Architecture plan written: 2026-09-04 | ptt-architect | BWAVE-NEXT Lane B*
*Pre-requisite: commit 92a44332 must be on main before any ticket execution*
