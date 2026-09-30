# BWAVE-NEXT Lane B -- Tickets

**Block**: BWAVE-NEXT Lane B -- Cancel-Before-Dispatch Drain + Post-PR-42 Repairs
**Phase**: Phase 3 (Ticket Generation)
**Architect**: ptt-architect
**Date**: 2026-09-04
**Plan status**: REVIEW_PASS (02-plan-review.md -- 17/17 checks PASS)

---

## PRE-REQUISITE (MANDATORY BEFORE ANY TICKET STARTS)

**Commit `92a44332` (BWAVE-NEXT Lane A T1/T2/T3/T4/T5) MUST be on `main` HEAD.**

Current state confirmed: CopyEngine.cs is at 6,369 lines. T4/T5 symbols
(`TryNakedDetect`, `NakedPositionDetector`, `_nakedDetectLastQueuedTicks`,
`ActiveOrders`, `ActiveOrdersTestable`) are NOT present in source -- they come from
commit `92a44332`.

Engineer MUST verify BEFORE starting T1:
```powershell
git log --oneline HEAD..92a44332
# must return empty output -- if not, coordinate with Director to merge
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "TryNakedDetect" | Select-Object LineNumber
# must return line 6403
```

If commit is not on HEAD: STOP. Do not proceed. Escalate to Director.

---

## Pipeline Sequence

```
Commit 92a44332 on main
  --> T1 (DW-NEXT-A-07 + DW-NEXT-A-06)
        --> T1 VERIFY_PASS
              --> T2 (DW-NEW-08 Option D)
                    --> T2 VERIFY_PASS
```

T3 is Director-action documentation only. No code pipeline.

---

## TICKET 1 -- DW-NEXT-A-07 + DW-NEXT-A-06: Post-PR-42 Repairs

**Ticket ID**: T1
**DW Items**: DW-NEXT-A-07, DW-NEXT-A-06
**Spec requirement IDs**: Mission brief T1 acceptance criteria (LaneB-mission-brief.md lines 102-108)
**Files changed**:
- `src/PropTraderTools/CopyEngine.cs` (2 surgical edits)
- `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` (add 2 [Fact] methods)

---

### SCOPE LOCK

This ticket implements ONLY the T1 changes described below.
T2 (DW-NEW-08 Option D) MUST NOT be touched in this ticket.
Do NOT add PendingDispatchDrain, _pendingDispatchDrains, DrainThenDispatch, or any
drain-related code. Those belong to T2.

---

### PRE-CONDITION

Before editing any file, verify all of the following are true:

```powershell
# 1. T4/T5 commit is on main -- check key symbols exist
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "private void TryNakedDetect" | Select-Object LineNumber
# Expected: LineNumber 6403

Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "_nakedDetectLastQueuedTicks" | Select-Object LineNumber
# Expected: first hit at line 373

Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "private static IEnumerable.*Order.*ActiveOrders" | Select-Object LineNumber
# Expected: line 3437

# 2. Confirm exact line numbers of TickCount reads before editing
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\(long\)Environment\.TickCount" | Select-Object LineNumber, Line
# Expected: hits in NakedPositionDetector (~lines 6434, 6439)
```

If ANY pre-condition fails: STOP. Do not proceed. Escalate to Director.

---

### SUB-A: DW-NEXT-A-07 -- ActiveOrders Thread Safety

**Determination**: AMBIGUOUS-ADDED-TOLIST (R-02 in plan-review.md)

**Rationale**: NT8_FULL_REFERENCE.md Orders Collection (lines 2800-2844) does not
explicitly confirm acc.Orders is safe for lazy LINQ enumeration during OnOrderUpdate
callbacks from a background thread. Three bot reviews (Greptile, cubic, CodeRabbit)
all flagged lazy enumeration. Director decision: "If ambiguous -- add .ToList()."

**Location**: `ActiveOrders` method, confirmed line 3437 (post-T4/T5 commit).

**Current code** (lines 3437-3441, post-T4/T5):
```csharp
private static IEnumerable<Order> ActiveOrders(Account acc) =>
    acc.Orders.Where(static o =>
        o.OrderState != OrderState.Filled
        && o.OrderState != OrderState.Cancelled
        && o.OrderState != OrderState.Rejected);
```

**Change to**:
```csharp
private static IEnumerable<Order> ActiveOrders(Account acc) =>
    acc.Orders.Where(static o =>
        o.OrderState != OrderState.Filled
        && o.OrderState != OrderState.Cancelled
        && o.OrderState != OrderState.Rejected).ToList();
```

**Constraints**:
- Return type stays `IEnumerable<Order>` -- no caller changes required
- Callers at line 3468 (`FindFollowerBracketOrder` overload) and line 3668
  (`FindFollowerEntryOrder`) are UNCHANGED
- CYC stays 1 (expression body, single LINQ chain -- no new branches)
- `List<T>` is returned but typed as `IEnumerable<T>` -- no API surface change
- JS-036 (zero-alloc hot paths): NOT applicable -- called once per OnOrderUpdate
  per follower account, not per bar tick. Allocation is trivial.
- ASCII-only: `.ToList()` -- no non-ASCII characters

---

### SUB-B: DW-NEXT-A-06 -- TickCount Wraparound

**Bug**: `(long)Environment.TickCount` zero-extends int32 to int64. After ~24.9 days
uptime, TickCount wraps negative. The cast produces a large positive int64. Result:
`now - last` becomes huge, suppressing detection.

**Fix**: Change every `(long)Environment.TickCount` that feeds into the `long` debounce
dictionary to:
```csharp
(long)(int)Environment.TickCount
```
The explicit `(int)` intermediate forces sign-extension (int32 -> int64) correctly.

**Location**: `NakedPositionDetector` method (confirmed line 6424, post-T4/T5 commit).
Specific lines confirmed by T4 verification artifact:
- Line 6434: `_nakedDetectLastQueuedTicks.GetOrAdd` -- contains `(long)Environment.TickCount`
- Line 6439: `_nakedDetectLastQueuedTicks.AddOrUpdate` -- contains `(long)Environment.TickCount`

**MANDATORY step before editing**:
```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\(long\)Environment\.TickCount" | Select-Object LineNumber, Line
```
Apply the fix at EVERY line returned by this scan. Do not hard-code line numbers.
If the pattern appears outside the NakedPositionDetector method range (lines 6424-6451),
do NOT change those occurrences -- scope is NakedPositionDetector only.

**Constraints**:
- CYC of TryNakedDetect confirmed=3 from T4 verification. Unchanged by this fix.
- CYC of NakedPositionDetector: unchanged (cast change, no new branches).
- Pure cast change. No new methods. No new fields.
- ASCII-only: `(long)(int)Environment.TickCount` -- no non-ASCII characters.

---

### T1 TESTS

**File**: `src/PropTraderTools/Tests/BwaveDwLaneATests.cs`
(The file from T4/T5 commit -- currently has 14 [Fact] at lines 16, 28, 79, 94, 109,
130, 157, 177, 202, 218, 233, 249, 280, 319. Add 2 new [Fact] methods AFTER line 319.)

#### Test 1: `[Fact] public void ActiveOrders_ThreadSafetyVerification()`

**Approach**: Use `ActiveOrdersTestable` internal seam (line 3446, post-T4/T5).
The seam accepts `IEnumerable<Order>` so no live NT8 Account is needed.

**What it asserts**:
1. `ActiveOrdersTestable` method exists on `CopyEngine` as `internal static`
   (verify via reflection: `typeof(CopyEngine).GetMethod("ActiveOrdersTestable",
   BindingFlags.NonPublic | BindingFlags.Static)` is not null)
2. Functional filter verification: arrange a list with 1 Filled order + 1 Working order.
   Call `CopyEngine.ActiveOrdersTestable(orders)`. Assert the result contains exactly
   1 order and that order's OrderState is Working.
   (This verifies the filter still works correctly after `.ToList()` addition.)
3. Result is materializable without error (calling `.ToList()` on the returned value
   does not throw -- verifies no double-enumeration issue).

**Skeleton**:
```csharp
[Fact]
public void ActiveOrders_ThreadSafetyVerification()
{
    // Arrange: 1 Filled + 1 Working fake orders
    // (Use mock or concrete Order stubs -- follow pattern from existing T5 tests at lines 280-319)
    // Act: var result = CopyEngine.ActiveOrdersTestable(orders).ToList();
    // Assert.Single(result);
    // Assert.Equal(OrderState.Working, result[0].OrderState);
    // Assert non-null via reflection:
    var seam = typeof(CopyEngine).GetMethod(
        "ActiveOrdersTestable",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    Assert.NotNull(seam);
}
```

#### Test 2: `[Fact] public void NakedDetector_DebounceField_UsesLongArithmetic()`

**Approach**: Structural reflection only. No live NT8 Account needed.

**What it asserts**:
1. Field `_nakedDetectLastQueuedTicks` exists on `CopyEngine` as private instance field.
   Type is `ConcurrentDictionary<string, long>`. Readonly.
   ```csharp
   var field = typeof(CopyEngine).GetField(
       "_nakedDetectLastQueuedTicks",
       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
   Assert.NotNull(field);
   Assert.Equal(typeof(ConcurrentDictionary<string, long>), field.FieldType);
   Assert.True(field.IsInitOnly); // readonly
   ```
2. `TryNakedDetect` method exists on `CopyEngine` as private instance void with 1
   parameter of type `OrderEventArgs`.
   ```csharp
   var method = typeof(CopyEngine).GetMethod(
       "TryNakedDetect",
       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
   Assert.NotNull(method);
   Assert.Equal(typeof(void), method.ReturnType);
   var parms = method.GetParameters();
   Assert.Single(parms);
   Assert.Equal(typeof(OrderEventArgs), parms[0].ParameterType);
   ```

---

### 7-SCAN CHECKLIST (T1)

The engineer MUST run all 7 scans and report zero violations before declaring T1 complete.
Paste verbatim scan output in ticket-1-completion.md.

| # | Scan | Command | Required Result |
|---|------|---------|-----------------|
| SCAN-01 | JS-021 lock() | `Select-String -Path src/PropTraderTools/*.cs -Pattern "lock\s*\(" \| Where-Object { $_.Line -notmatch "^\s*//" }` | 0 results |
| SCAN-02 | JS-033 async void | `Select-String -Path src/PropTraderTools/*.cs -Pattern "async void [A-Z]" \| Where-Object { $_.Line -notmatch "^\s*//" }` | 0 results |
| SCAN-03 | JS-002 return null (new) | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "return null"` -- verify 0 new occurrences in ActiveOrders (line 3437) and NakedPositionDetector (line 6424+) | 0 new in T1 methods |
| SCAN-04 | JS-001 throw new | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "throw new" \| Where-Object { $_.Line -notmatch "^\s*//" }` | 0 results |
| SCAN-05 | CYC | ActiveOrders: expression body + single LINQ chain = CYC 1. TryNakedDetect = CYC 3 (unchanged from T4). NakedPositionDetector: cast change only, no new branches. | All <=8 |
| SCAN-06 | ASCII-only | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]"` | 0 results |
| SCAN-07 | xUnit only | `Select-String -Path src/PropTraderTools/Tests/BwaveDwLaneATests.cs -Pattern "\[Fact\]\|\[Test\]"` | [Fact] only, 0 [Test] |

---

### ACCEPTANCE CRITERIA (T1)

- [ ] AC-01: NT8 thread-safety determination documented in ticket-1-completion.md as AMBIGUOUS-ADDED-TOLIST
- [ ] AC-02: `.ToList()` added at the end of `ActiveOrders` body (line 3437+3 area). Return type stays `IEnumerable<Order>`. CYC stays 1.
- [ ] AC-03: `(long)(int)Environment.TickCount` applied at ALL TickCount-to-long reads in `NakedPositionDetector` (confirmed lines ~6434, ~6439 -- use Select-String scan to find exact lines, do not hard-code).
- [ ] AC-04: Callers of `ActiveOrders` at lines 3468 and 3668 are UNCHANGED.
- [ ] AC-05: All 7 scans: zero violations (verbatim output in completion report).
- [ ] AC-06: `dotnet build src/PropTraderTools` -- 0 errors.
- [ ] AC-07: Both T1 [Fact] tests pass: `ActiveOrders_ThreadSafetyVerification` and `NakedDetector_DebounceField_UsesLongArithmetic`.
- [ ] AC-08: NT8 sync: `powershell -File scripts\ptt-sync-and-verify.ps1` shows 18/18 OK, 0 MISMATCH.
- [ ] AC-09: No lock(), no async void (non-handler), no return null in new/modified T1 code, ASCII-only.

---

### POST-GATES (T1)

Run in this order. All must pass before declaring T1 complete.

```powershell
# Gate 1: NT8 sync
powershell -File scripts\ptt-sync-and-verify.ps1
# Required: 18/18 OK, 0 MISMATCH

# Gate 2: Build
dotnet build src/PropTraderTools
# Required: Build succeeded, 0 Error(s)

# Gate 3: T1 tests
dotnet test src/PropTraderTools --filter "ActiveOrders_ThreadSafetyVerification|NakedDetector_DebounceField_UsesLongArithmetic"
# Required: Failed: 0, Passed: 2

# Gate 4: Full suite (regression guard)
dotnet test src/PropTraderTools
# Required: 0 new failures vs baseline
```

**Record verbatim output of all gates in ticket-1-completion.md.**

---

### SIM GATE (T1)

**Not applicable.** T1 consists of structural repairs and reflection tests only.
No live NT8 Account or SIM session is required for VERIFY_PASS.

---

## TICKET 2 -- DW-NEW-08 Option D: Cancel-Before-Dispatch Drain

**Ticket ID**: T2
**DW Items**: DW-NEW-08 Option D
**Spec requirement IDs**: DW-NEW-08-naked-fill-race.md Layer 2 acceptance criteria; mission brief T2 acceptance criteria (LaneB-mission-brief.md lines 176-188)
**Files changed**:
- `src/PropTraderTools/CopyEngine.cs` (nested class + field + 5 new methods + 2 method modifications)
- `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` (NEW FILE -- 3 [Fact] methods)
- `src/PropTraderTools/PropTraderTools.csproj` (add compile entry for BwaveNextLaneBTests.cs)

---

### SCOPE LOCK

This ticket implements ONLY the T2 changes described below.
T1 MUST have VERIFY_PASS before this ticket begins.
Do NOT re-implement or modify T1 changes (ActiveOrders.ToList(), TickCount cast).
Do NOT read or reference ticket-1-completion.md.

---

### PRE-CONDITION

Before editing any file, verify ALL of the following are true:

```powershell
# 1. T1 has VERIFY_PASS status
#    Check: docs/brain/BWAVE-NEXT/LaneB/ticket-1-verification.md exists and contains VERIFY_PASS

# 2. ActiveOrders uses .ToList() (T1 Sub-A)
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\.ToList\(\);" | Where-Object { $_.LineNumber -ge 3437 -and $_.LineNumber -le 3442 }
# Expected: 1 hit at line ~3441

# 3. _nakedDetectLastQueuedTicks field is present
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "_nakedDetectLastQueuedTicks" | Select-Object -First 1 | Select-Object LineNumber
# Expected: line 373

# 4. HandleEntryChange method is present with cancel+create+submit block
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "private void HandleEntryChange" | Select-Object LineNumber
# Expected: line 3667

# 5. Exact line numbers for HandleEntryChange cancel block
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "acc\.Cancel\(new Order\[\] \{ fo \}\)" | Select-Object LineNumber, Line
# Expected: 1 hit in HandleEntryChange body (~line 3701)

# 6. Build is clean
dotnet build src/PropTraderTools
# Expected: 0 errors
```

If ANY pre-condition fails: STOP. Do not proceed. Escalate to Director.

---

### NT8 API CONSTRAINTS (embed verbatim -- non-negotiable)

These constraints apply to EVERY line of T2 code. No exceptions.

| API | Status |
|-----|--------|
| `Account.Change()` | BANNED -- silent no-op on ATM-owned orders. NEVER call. |
| `AtmStrategyCreate()` | BANNED -- StrategyBase-only. NOT available in AddOnBase. NEVER call. |
| `AtmStrategyChangeStopTarget()` | BANNED -- StrategyBase-only. NOT available in AddOnBase. NEVER call. |
| `Account.Cancel(Order[])` | ALLOWED -- AddOnBase available. Used in DrainThenDispatch. |
| `Account.CreateOrder(...)` | ALLOWED -- AddOnBase available. Used in SubmitEntryDirect. |
| `Account.Submit(Order[])` | ALLOWED -- AddOnBase available. Used in SubmitEntryDirect. |
| `lock()` | BANNED -- JS-021. Use ConcurrentDictionary + Interlocked only. |

---

### NEW TYPE: PendingDispatchDrain

**Placement**: Nested `private sealed class` inside the `CopyEngine` class body.
Add before the first new method (DrainThenDispatch), typically at the very bottom of
the CopyEngine class just before the closing `}`.
No new file needed. NT8 compiler rule NT8-001: no `{ get; init; }` syntax (CS0518).
Use explicit constructor.

```csharp
// DW-NEW-08 Option D: payload for cancel-before-dispatch drain.
// Stores the dispatch intent while cancels are in-flight.
// CYC=0 (data class -- no logic methods).
// PendingCancelCount is a plain int field (not property) because
// Interlocked.Decrement requires ref int; properties cannot be passed by ref.
private sealed class PendingDispatchDrain
{
    internal string FollowerAcctKey    { get; private set; }
    internal Instrument Instrument     { get; private set; }
    internal int Qty                   { get; private set; }
    internal double Price              { get; private set; }
    internal OrderAction Action        { get; private set; }
    internal OrderType OrderType       { get; private set; }
    internal Account FollowerAccount   { get; private set; }
    internal int PendingCancelCount;   // mutable -- Interlocked.Decrement/Increment
    internal long TimestampTicks       { get; private set; }

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
    {
        FollowerAcctKey    = followerAcctKey;
        Instrument         = instrument;
        Qty                = qty;
        Price              = price;
        Action             = action;
        OrderType          = orderType;
        FollowerAccount    = followerAccount;
        PendingCancelCount = pendingCancelCount;
        TimestampTicks     = timestampTicks;
    }
}
```

---

### NEW FIELD: _pendingDispatchDrains

**Placement**: Immediately after the `_nakedDetectLastQueuedTicks` field declaration
(confirmed line 373-374, post-T1 commit). Insert at line 375.

```csharp
// DW-NEW-08 Option D: cancel-before-dispatch drain state.
// Key = follower account name. ConcurrentDictionary: no lock (JS-021).
// StringComparer.Ordinal: deterministic key comparison.
private readonly ConcurrentDictionary<string, PendingDispatchDrain> _pendingDispatchDrains =
    new ConcurrentDictionary<string, PendingDispatchDrain>(StringComparer.Ordinal);
```

---

### NEW METHOD: DrainThenDispatch

**Placement**: After all existing naked-detect methods at end of CopyEngine class.

**Signature**:
```csharp
private void DrainThenDispatch(
    Account follower,
    Instrument instrument,
    int qty,
    double price,
    OrderAction action,
    OrderType orderType)
```

**CYC target**: 4
- Branch (1): null guard on follower or instrument
- Branch (2): no entry candidates -- direct submit path
- Branch (3): foreach cancel loop
- Branch (4): cancelCount == 0 after loop (edge case guard)

**Implementation flow**:
```
1. if (follower == null || instrument == null) return;                         // (1)
2. Build entryCandidates using ActiveOrders(follower) -- already materialized
   via .ToList() from T1. No additional ToList() needed here.
   Filter for Working or Accepted state AND Limit or StopLimit order type.
3. if (!entryCandidates.Any())                                                 // (2)
   { SubmitEntryDirect(follower, instrument, qty, price, action, orderType); return; }
4. string acctKey = follower.Name;
   long now = (long)(int)Environment.TickCount;   // same cast as DW-NEXT-A-06
   var payload = new PendingDispatchDrain(acctKey, instrument, qty, price,
       action, orderType, follower, pendingCancelCount: 0, now);
   _pendingDispatchDrains[acctKey] = payload;      // overwrite if stale drain exists
5. int cancelCount = 0;
   foreach (var e in entryCandidates)              // (3)
   { follower.Cancel(new Order[] { e }); cancelCount++; }
   Interlocked.Exchange(ref payload.PendingCancelCount, cancelCount);
6. if (cancelCount == 0)                           // (4)
   { _pendingDispatchDrains.TryRemove(acctKey, out _);
     SubmitEntryDirect(follower, instrument, qty, price, action, orderType); return; }
7. Print("[DRAIN] acct=" + acctKey + " cancel-sent=" + cancelCount);
```

**Constraints**:
- NO `Account.Change()`. NO `Account.Submit()` at this point. Only `Account.Cancel()`.
- NO `lock()`. ConcurrentDictionary index operator is thread-safe for overwrites.
- All log strings: ASCII-only.

---

### NEW METHOD: SubmitEntryDirect

**Placement**: After DrainThenDispatch.

**Signature**:
```csharp
private void SubmitEntryDirect(
    Account follower,
    Instrument instrument,
    int qty,
    double price,
    OrderAction action,
    OrderType orderType)
```

**CYC target**: 2
- Branch (1): null guard on created order
- Branch (2): orderType conditional for limitPx/stopPx (ternary -- counts as 1 branch)

**Implementation flow**:
```
1. double limitPx = orderType == OrderType.StopLimit ? 0.0 : price;          // (2)
   double stopPx  = orderType == OrderType.StopLimit ? price : 0.0;
2. var order = follower.CreateOrder(
       instrument, action, orderType, OrderEntry.Manual,
       TimeInForce.Day, qty, limitPx, stopPx,
       null, "PTT-Copy", DateTime.MaxValue, null);
3. if (order == null) return;                                                 // (1)
4. follower.Submit(new[] { order });
5. Print("[DRAIN-SUBMIT] acct=" + follower.Name + " instr=" + instrument.FullName
       + " price=" + price);
```

**Note on order name**: Use `"PTT-Copy"` (matches existing HandleEntryChange pattern
at line 3712: `fo.Name` which is "PTT-Copy" for all PTT-placed entry orders).

---

### NEW METHOD: OnDrainCancelAck

**Placement**: After SubmitEntryDirect.

**Signature**:
```csharp
private void OnDrainCancelAck(string acctKey)
```

**Note**: This is NOT an event handler. It is called directly from `OnOrderUpdate`.
Synchronous void. NOT async void (JS-033 compliant).

**CYC target**: 3
- Branch (1): drain not found -- early return
- Branch (2): unexpected underflow guard
- Branch (3): remaining == 0 -- fire SubmitDrainedEntry

**Implementation flow**:
```
1. if (!_pendingDispatchDrains.TryGetValue(acctKey, out var payload)) return; // (1)
2. int remaining = Interlocked.Decrement(ref payload.PendingCancelCount);
3. if (remaining < 0)                                                         // (2)
   { Print("[DRAIN-UNDERFLOW] acct=" + acctKey); return; }
4. if (remaining == 0) SubmitDrainedEntry(acctKey);                          // (3)
```

---

### NEW METHOD: SubmitDrainedEntry

**Placement**: After OnDrainCancelAck.

**Signature**:
```csharp
private void SubmitDrainedEntry(string acctKey)
```

**CYC target**: 3
- Branch (1): TryRemove fails -- stale key, early return
- Branch (2): FollowerAccount is null -- account deregistered, early return
- Branch (3): inside SubmitEntryDirect (order null guard -- delegated)

**Implementation flow**:
```
1. if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) return;  // (1)
2. var follower = payload.FollowerAccount;
   if (follower == null) return;                                              // (2)
3. SubmitEntryDirect(follower, payload.Instrument, payload.Qty, payload.Price,
       payload.Action, payload.OrderType);
   // [DRAIN-SUBMIT] log is emitted inside SubmitEntryDirect
```

---

### NEW METHOD: TryDrainWatchdog

**Placement**: After SubmitDrainedEntry.

**Signature**:
```csharp
private void TryDrainWatchdog()
```

**CYC target**: 3
- Branch (1): IsEmpty fast-path guard
- Branch (2): foreach loop
- Branch (3): timestamp comparison

**Implementation flow**:
```
1. if (_pendingDispatchDrains.IsEmpty) return;                               // (1)
2. long now = (long)(int)Environment.TickCount;  // DW-NEXT-A-06 cast pattern
3. foreach (var kv in _pendingDispatchDrains)                               // (2)
4.   if (now - kv.Value.TimestampTicks > 2000L)                             // (3)
     { _pendingDispatchDrains.TryRemove(kv.Key, out _);
       Print("[DRAIN-TIMEOUT] acct=" + kv.Key); }
     // NO submit on timeout -- position may have changed. Log and remove only.
```

**No System.Threading.Timer.** Fires as a cheap tail-call from OnOrderUpdate.

---

### MODIFIED METHOD: HandleEntryChange (actual name -- spec calls it PropagateFollowerEntryReplace)

**Location**: Line 3667 (post-T1 state, post-T4/T5 commit).
**Current CYC**: 7 (documented at line 3664: `CYC=7: instr null(1) + tickSize ternary(2)
+ foreach acc(3) + acc null(4) + fo null(5) + price delta guard(6) + order null guard
in CreateOrder(7)`).

**Change**: Replace the cancel+create+submit block (lines 3701-3725) with a single
`DrainThenDispatch(...)` call.

**Before** (lines 3701-3725, post-T1):
```csharp
// B67-LaneB DW-B67-02: Cancel+CreateOrder+Submit (acc.Change() is Apex/Rithmic no-op).
// NT8_FULL_REFERENCE.md lines 898-899: StopLimit price in StopPrice not LimitPrice.
double limitPx = fo.OrderType == OrderType.StopLimit ? 0.0 : newPrice; // (7a)
double stopPx = fo.OrderType == OrderType.StopLimit ? newPrice : 0.0; // (7b)
acc.Cancel(new Order[] { fo });
var order = acc.CreateOrder(
    instrument,
    fo.OrderAction,
    fo.OrderType,
    OrderEntry.Manual,
    fo.TimeInForce,
    fo.Quantity,
    limitPx,
    stopPx,
    null,
    fo.Name,
    DateTime.MaxValue,
    null
);
if (order != null) // (7)
{
    acc.Submit(new[] { order });
    // B69 DW-B69-03: preload new orderId into _dedupCache at newPrice.
    _dedupCache[order.OrderId.ToString()] = newPrice;
}
StatusUpdate?.Invoke(acc.Name + ": entry dragged -> " + newPrice);
```

**After** (replace entire block above with):
```csharp
// DW-NEW-08 Option D: cancel-before-dispatch drain instead of direct cancel+submit.
// DrainThenDispatch cancels all Working/Accepted entries, parks intent in
// _pendingDispatchDrains, submits via SubmitDrainedEntry when all cancels confirmed.
DrainThenDispatch(acc, instrument, fo.Quantity, newPrice, fo.OrderAction, fo.OrderType);
```

**CYC after T2 change**: The `if (order != null)` branch (branch 7) is REMOVED (now
handled inside DrainThenDispatch/SubmitEntryDirect). The `limitPx/stopPx` ternaries
(7a/7b) are also removed. CYC: 7 - 1 = 6. Within budget <=8. ✅

**Update CYC comment at line 3664** from:
```
// CYC=7: instr null(1) + tickSize ternary(2) + foreach acc(3) + acc null(4)
//   + fo null(5) + price delta guard(6) + order null guard in CreateOrder(7).
```
To:
```
// CYC=6: instr null(1) + tickSize ternary(2) + foreach acc(3) + acc null(4)
//   + fo null(5) + price delta guard(6).
// DW-NEW-08-D: order null guard removed -- DrainThenDispatch handles null internally.
```

**Note on StatusUpdate**: The `StatusUpdate?.Invoke(...)` call is also removed (it was
inside the cancel+submit block). DrainThenDispatch logs via Print() instead. This is
intentional -- the status event is for UI updates; drain logging goes to NT8 output.
If StatusUpdate is needed by downstream, add back after DrainThenDispatch call.

---

### MODIFIED METHOD: OnOrderUpdate

**Location**: Line 1355 (confirmed by T4 verification).
**Current CYC** (post-T4/T5): 6
- Gate 1: !_isCopyEnabled (1)
- Gate 2: matchedRule == null (2)
- Gate 2.5: !matchedRule.Value.Enabled (3)
- TryCancelFollowerEntries result (4)
- TryDispatchLeaderFlat result (5)
- TryHandleDrag result (6)

**T2 adds 2 items**:

**Item 1 -- Unconditional TryDrainWatchdog call** (CYC delta = 0):
Place after `TryReplaceOnAtmCancel(e.Order);` (line 1393) and before Gate 1 (line 1395).

```csharp
// DW-NEW-08 Option D: cheap piggybacked watchdog for stuck drains (>2s).
// Unconditional -- fires even when copy is disabled. CYC delta=0.
TryDrainWatchdog();
```

**Item 2 -- Drain-ack routing** (CYC delta = +1, branch 7):
Place immediately after `TryReplaceOnAtmCancel(e.Order);` (line 1393), before
`TryDrainWatchdog()` (just added).

```csharp
// DW-NEW-08 Option D: route cancel-ack to drain handler if account is in drain state.
// Terminal states: Cancelled, Rejected, Filled. CYC +1 (branch 7).
if ((e.Order.OrderState == OrderState.Cancelled
     || e.Order.OrderState == OrderState.Rejected
     || e.Order.OrderState == OrderState.Filled)
    && _pendingDispatchDrains.ContainsKey(e.Order.Account.Name))
    OnDrainCancelAck(e.Order.Account.Name);
```

**Final order** (post-T2 additions at the pre-Gate-1 area):
```
TryRecordBeTargetFill(e.Order);
TryTriggerBeRecovery(e.Order);
TryCleanupReArmedAtmBracket(e);
TryReplaceOnAtmCancel(e.Order);
// [NEW T2] drain-ack routing (+1 branch)
if ((e.Order.OrderState == ...) && _pendingDispatchDrains.ContainsKey(...))
    OnDrainCancelAck(e.Order.Account.Name);
// [NEW T2] drain watchdog (unconditional, +0 CYC)
TryDrainWatchdog();
// Gate 1: enabled check
if (!_isCopyEnabled)
    return;
```

**CYC after T2**: 6 + 1 = 7. Within budget <=8. ✅

**Update the CYC comment on OnOrderUpdate** if one exists, to reflect CYC=7.

---

### T2 TESTS (New file)

**File**: `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` (NEW FILE)

Note: `BwaveDwLaneBTests.cs` already exists with 1 test. The new file for Lane B
T2 tests is `BwaveNextLaneBTests.cs` (matching the naming pattern: `BwaveNext` prefix).

**File header template** (follow existing test file conventions):
```csharp
// BWAVE-NEXT Lane B T2 tests -- DW-NEW-08 Option D structural verification.
// xUnit only -- JS-051. No lock() -- JS-021. No async void -- JS-033.
// Structural reflection tests only -- no live NT8 Account required.
using System;
using System.Collections.Concurrent;
using System.Reflection;
using NinjaTrader.NinjaScript;
using Xunit;

namespace PropTraderTools.Tests
{
    public class BwaveNextLaneBTests
    {
        // ... [Fact] methods below
    }
}
```

#### Test 1: `[Fact] public void DrainThenDispatch_CancelsExistingEntryBeforeSubmit()`

**Approach**: Structural reflection. No live NT8 Account.

**What it asserts**:
1. `DrainThenDispatch` method exists on `CopyEngine` as `private` instance `void` with
   6 parameters: `(Account, Instrument, int, double, OrderAction, OrderType)`.
   ```csharp
   var method = typeof(CopyEngine).GetMethod(
       "DrainThenDispatch",
       BindingFlags.NonPublic | BindingFlags.Instance);
   Assert.NotNull(method);
   Assert.Equal(typeof(void), method.ReturnType);
   var parms = method.GetParameters();
   Assert.Equal(6, parms.Length);
   Assert.Equal(typeof(Account), parms[0].ParameterType);
   Assert.Equal(typeof(Instrument), parms[1].ParameterType);
   Assert.Equal(typeof(int), parms[2].ParameterType);
   Assert.Equal(typeof(double), parms[3].ParameterType);
   Assert.Equal(typeof(OrderAction), parms[4].ParameterType);
   Assert.Equal(typeof(OrderType), parms[5].ParameterType);
   ```
2. `_pendingDispatchDrains` field exists on `CopyEngine`, `private`, `readonly`,
   type `ConcurrentDictionary<string, ?>` (verify generic args match PendingDispatchDrain).
   ```csharp
   var field = typeof(CopyEngine).GetField(
       "_pendingDispatchDrains",
       BindingFlags.NonPublic | BindingFlags.Instance);
   Assert.NotNull(field);
   Assert.True(field.IsInitOnly); // readonly
   Assert.Equal(typeof(ConcurrentDictionary<,>),
       field.FieldType.GetGenericTypeDefinition());
   ```
3. `PendingDispatchDrain` nested type exists on `CopyEngine`, sealed.
   ```csharp
   var drainType = typeof(CopyEngine).GetNestedType(
       "PendingDispatchDrain",
       BindingFlags.NonPublic);
   Assert.NotNull(drainType);
   Assert.True(drainType.IsSealed);
   ```
4. `PendingCancelCount` field on `PendingDispatchDrain` is a plain `int` field
   (not a property) so Interlocked.Decrement can take its reference.
   ```csharp
   var countField = drainType.GetField("PendingCancelCount",
       BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
   Assert.NotNull(countField);
   Assert.Equal(typeof(int), countField.FieldType);
   Assert.False(countField.IsInitOnly); // must NOT be readonly -- Interlocked needs ref
   ```

#### Test 2: `[Fact] public void OnDrainCancelAck_SubmitsDrainedEntry_WhenPendingCountReachesZero()`

**Approach**: Structural reflection. No live NT8 Account.

**What it asserts**:
1. `OnDrainCancelAck` method exists on `CopyEngine` as `private` instance `void` with
   1 parameter of type `string`.
   ```csharp
   var method = typeof(CopyEngine).GetMethod(
       "OnDrainCancelAck",
       BindingFlags.NonPublic | BindingFlags.Instance);
   Assert.NotNull(method);
   Assert.Equal(typeof(void), method.ReturnType);
   var parms = method.GetParameters();
   Assert.Single(parms);
   Assert.Equal(typeof(string), parms[0].ParameterType);
   ```
   (Signature is `(string acctKey)` -- NOT an event handler signature.)
2. `SubmitDrainedEntry` method exists on `CopyEngine` as `private` instance `void` with
   1 parameter of type `string`.
   ```csharp
   var method2 = typeof(CopyEngine).GetMethod(
       "SubmitDrainedEntry",
       BindingFlags.NonPublic | BindingFlags.Instance);
   Assert.NotNull(method2);
   Assert.Equal(typeof(void), method2.ReturnType);
   var parms2 = method2.GetParameters();
   Assert.Single(parms2);
   Assert.Equal(typeof(string), parms2[0].ParameterType);
   ```
3. `TryDrainWatchdog` method exists on `CopyEngine` as `private` instance `void` with
   0 parameters.
   ```csharp
   var watchdog = typeof(CopyEngine).GetMethod(
       "TryDrainWatchdog",
       BindingFlags.NonPublic | BindingFlags.Instance);
   Assert.NotNull(watchdog);
   Assert.Equal(typeof(void), watchdog.ReturnType);
   Assert.Empty(watchdog.GetParameters());
   ```

#### Test 3: `[Fact] public void DrainWatchdog_ClearsStuckDrain_AfterTimeout()`

**Approach**: Structural reflection on PendingDispatchDrain type and _pendingDispatchDrains field.

**What it asserts**:
1. `PendingDispatchDrain.TimestampTicks` property/field exists of type `long`.
   ```csharp
   var drainType = typeof(CopyEngine).GetNestedType(
       "PendingDispatchDrain", BindingFlags.NonPublic);
   Assert.NotNull(drainType);
   // TimestampTicks property
   var prop = drainType.GetProperty("TimestampTicks",
       BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
   Assert.NotNull(prop);
   Assert.Equal(typeof(long), prop.PropertyType);
   ```
2. `_pendingDispatchDrains` uses `StringComparer.Ordinal`: verify via field type
   generic type argument key is `string`.
   ```csharp
   var field = typeof(CopyEngine).GetField(
       "_pendingDispatchDrains",
       BindingFlags.NonPublic | BindingFlags.Instance);
   Assert.NotNull(field);
   Assert.Equal(typeof(string), field.FieldType.GetGenericArguments()[0]);
   ```
3. `TryDrainWatchdog` has 0 parameters and returns void (redundant with Test 2
   assertion 3 -- but confirms watchdog is accessible and callable via reflection).
   ```csharp
   var watchdog = typeof(CopyEngine).GetMethod(
       "TryDrainWatchdog",
       BindingFlags.NonPublic | BindingFlags.Instance);
   Assert.NotNull(watchdog);
   Assert.Empty(watchdog.GetParameters());
   Assert.Equal(typeof(void), watchdog.ReturnType);
   ```
4. `PendingDispatchDrain` has an internal constructor (not public -- data class,
   no external construction by non-CopyEngine code).
   ```csharp
   var ctors = drainType.GetConstructors(
       BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
   Assert.NotEmpty(ctors);
   Assert.All(ctors, c => Assert.False(c.IsPublic));
   ```

---

### CSPROJ ENTRY (new test file)

Add to `src/PropTraderTools/PropTraderTools.csproj`:

```xml
<Compile Include="Tests\BwaveNextLaneBTests.cs" />
```

Place it in the `<ItemGroup>` that contains the other test file compile entries
(near `BwaveDwLaneATests.cs` and `BwaveDwLaneBTests.cs`).

---

### 7-SCAN CHECKLIST (T2)

The engineer MUST run all 7 scans and report zero violations before declaring T2 complete.
Paste verbatim scan output in ticket-2-completion.md.

| # | Scan | Command | Required Result |
|---|------|---------|-----------------|
| SCAN-01 | JS-021 lock() | `Select-String -Path src/PropTraderTools/*.cs -Pattern "lock\s*\(" \| Where-Object { $_.Line -notmatch "^\s*//" }` | 0 results |
| SCAN-02 | JS-033 async void | `Select-String -Path src/PropTraderTools/*.cs -Pattern "async void [A-Z]" \| Where-Object { $_.Line -notmatch "^\s*//" }` | 0 results (OnDrainCancelAck is synchronous void, not async void) |
| SCAN-03 | JS-002 return null (new) | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "return null"` -- verify 0 new occurrences in any T2 method bodies (DrainThenDispatch, SubmitEntryDirect, OnDrainCancelAck, SubmitDrainedEntry, TryDrainWatchdog) | 0 new in T2 methods |
| SCAN-04 | JS-001 throw new | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "throw new" \| Where-Object { $_.Line -notmatch "^\s*//" }` | 0 results |
| SCAN-05 | CYC budget | Manual count: DrainThenDispatch<=4, OnDrainCancelAck<=3, SubmitDrainedEntry<=3, SubmitEntryDirect<=2, TryDrainWatchdog<=3, HandleEntryChange<=6, OnOrderUpdate<=7 | All <=8 |
| SCAN-06 | ASCII-only | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]"` | 0 results |
| SCAN-07 | xUnit only | `Select-String -Path src/PropTraderTools/Tests/BwaveNextLaneBTests.cs -Pattern "\[Fact\]\|\[Test\]"` | [Fact] only, 0 [Test] |

---

### ACCEPTANCE CRITERIA (T2)

- [ ] AC-01: `PendingDispatchDrain` sealed nested class present with all 9 fields/properties:
  FollowerAcctKey(string), Instrument(Instrument), Qty(int), Price(double), Action(OrderAction),
  OrderType(OrderType), FollowerAccount(Account), PendingCancelCount(int plain field), TimestampTicks(long).
- [ ] AC-02: `_pendingDispatchDrains` field: `private readonly ConcurrentDictionary<string,
  PendingDispatchDrain>`, initialized with `StringComparer.Ordinal`. Placed after
  `_nakedDetectLastQueuedTicks` field.
- [ ] AC-03: `DrainThenDispatch` present, CYC<=4, no lock(), uses `Account.Cancel()` only
  (no Account.Change(), no CreateOrder+Submit), logs [DRAIN].
- [ ] AC-04: `SubmitEntryDirect` present, CYC<=2, uses `Account.CreateOrder()` + `Account.Submit()`,
  logs [DRAIN-SUBMIT].
- [ ] AC-05: `OnDrainCancelAck` present, CYC<=3, synchronous void (not async void), 1 string
  parameter, uses `Interlocked.Decrement`, routes to `SubmitDrainedEntry` when remaining==0.
- [ ] AC-06: `SubmitDrainedEntry` present, CYC<=3, uses `_pendingDispatchDrains.TryRemove`,
  calls `SubmitEntryDirect`, logs via SubmitEntryDirect [DRAIN-SUBMIT].
- [ ] AC-07: `TryDrainWatchdog` present, CYC<=3, 2000L ms threshold, logs [DRAIN-TIMEOUT],
  does NOT submit on timeout, no System.Threading.Timer.
- [ ] AC-08: `HandleEntryChange` (line 3667): cancel+create+submit block replaced with
  `DrainThenDispatch(acc, instrument, fo.Quantity, newPrice, fo.OrderAction, fo.OrderType)`.
  CYC drops from 7 to 6. CYC comment updated.
- [ ] AC-09: `OnOrderUpdate` (line 1355): +1 branch (drain-ack routing) + unconditional
  `TryDrainWatchdog()` call. CYC = 7 (6 + 1). Within budget <=8.
- [ ] AC-10: Log markers present in source: `[DRAIN]`, `[DRAIN-SUBMIT]`, `[DRAIN-TIMEOUT]`.
- [ ] AC-11: NO `Account.Change()`, `AtmStrategyCreate()`, `AtmStrategyChangeStopTarget()`
  anywhere in new T2 code (verify via Select-String scan).
- [ ] AC-12: NO `lock()` anywhere in new T2 code.
- [ ] AC-13: All 7 scans: zero violations (verbatim output in completion report).
- [ ] AC-14: `dotnet build src/PropTraderTools` -- 0 errors.
- [ ] AC-15: All 3 T2 [Fact] tests pass:
  `DrainThenDispatch_CancelsExistingEntryBeforeSubmit`,
  `OnDrainCancelAck_SubmitsDrainedEntry_WhenPendingCountReachesZero`,
  `DrainWatchdog_ClearsStuckDrain_AfterTimeout`.
- [ ] AC-16: NT8 sync: `powershell -File scripts\ptt-sync-and-verify.ps1` shows 18/18 OK,
  0 MISMATCH.
- [ ] AC-17: SIM gate documented as DEFERRED (non-blocking for VERIFY_PASS). Record in
  ticket-2-completion.md: "SIM gate: deferred -- requires live NT8 SIM account to verify
  [DRAIN] before [DRAIN-SUBMIT] sequence and [DRAIN-TIMEOUT] after 2s stuck drain."

---

### POST-GATES (T2)

Run in this order. All must pass before declaring T2 complete.

```powershell
# Gate 1: NT8 sync
powershell -File scripts\ptt-sync-and-verify.ps1
# Required: 18/18 OK, 0 MISMATCH

# Gate 2: Build
dotnet build src/PropTraderTools
# Required: Build succeeded, 0 Error(s)

# Gate 3: T2 tests
dotnet test src/PropTraderTools --filter "DrainThenDispatch|OnDrainCancelAck|DrainWatchdog"
# Required: Failed: 0, Passed: 3

# Gate 4: Full suite (regression guard)
dotnet test src/PropTraderTools
# Required: 0 new failures vs T1 baseline

# Gate 5: Banned API scan
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "Account\.Change\(|AtmStrategyCreate|AtmStrategyChangeStopTarget" | Where-Object { $_.Line -notmatch "^\s*//" }
# Required: 0 results in executable code
```

**Record verbatim output of all gates in ticket-2-completion.md.**

---

### SIM GATE (T2)

**DEFERRED -- non-blocking for VERIFY_PASS.**

SIM gate requires live NT8 with SIM account. Cannot be verified by structural tests alone.

Document in ticket-2-completion.md and ticket-2-verification.md:
```
SIM GATE: DEFERRED
Requires: Live NT8 SIM session with leader performing 3+ drag-reposition cycles.
Evidence to record:
  1. NT8 output log shows [DRAIN] cancel-sent=N before [DRAIN-SUBMIT] on every dispatch cycle.
  2. NT8 output log shows [DRAIN-TIMEOUT] if a cancel is unacknowledged for >2s.
  3. Under 14+ drag cycles, follower ends every cycle with either flat
     OR Entry:Filled + brackets (no naked position).
Status: Pending Director-scheduled SIM session.
```

---

## TICKET 3 -- Documentation Housekeeping (Director Action, No Pipeline)

**DW Items**: DW-NEXT-A-02, DW-NEXT-A-01
**Engineer action**: NONE REQUIRED.
**Director action only**: See LaneB-mission-brief.md lines 200-218 for instructions.

This ticket has no pipeline, no code, no tests, no scans.
T3 does not block T1 or T2 VERIFY_PASS.

---

## CYC Budget Summary

| Method | Pre-T2 CYC | Post-T2 CYC | Budget |
|--------|-----------|-------------|--------|
| `HandleEntryChange` | 7 | 6 | <=8 ✅ |
| `OnOrderUpdate` | 6 | 7 | <=8 ✅ |
| `DrainThenDispatch` | NEW | 4 | <=8 ✅ |
| `SubmitEntryDirect` | NEW | 2 | <=8 ✅ |
| `OnDrainCancelAck` | NEW | 3 | <=8 ✅ |
| `SubmitDrainedEntry` | NEW | 3 | <=8 ✅ |
| `TryDrainWatchdog` | NEW | 3 | <=8 ✅ |
| `ActiveOrders` (T1) | 1 | 1 | <=8 ✅ |
| `TryNakedDetect` (unchanged) | 3 | 3 | <=8 ✅ |

---

## NT8 Key Facts (embedded per protocol)

Confirmed from `docs/standards/NT8_FULL_REFERENCE.md` and `docs/standards/NT8_ADDON_KNOWLEDGE.md`:

- `AtmStrategyChangeStopTarget()` -- StrategyBase-only. NOT AddOnBase. NEVER use.
- `AtmStrategyCreate()` -- StrategyBase-only. NOT AddOnBase. NEVER use.
- `Account.Change()` -- AddOnBase available but CONFIRMED silent no-op on ATM-owned brackets. NEVER use.
- `Account.Cancel(Order[])` + `Account.CreateOrder(...)` + `Account.Submit(Order[])` -- AddOnBase available. Correct pattern.
- `acc.Orders` -- IEnumerable<Order>, no explicit thread-safety guarantee in NT8 docs. Add `.ToList()` per AMBIGUOUS-ADDED-TOLIST (T1 Sub-A).
- `(long)(int)Environment.TickCount` -- correct 24.9-day wraparound safe cast sequence (T1 Sub-B + T2 TryDrainWatchdog).

---

*Tickets written: 2026-09-04 | ptt-architect | BWAVE-NEXT Lane B Phase 3*
*Plan status: REVIEW_PASS (17/17 checks) -- 02-plan-review.md*
*Pre-requisite: commit 92a44332 must be on main before any ticket execution*
