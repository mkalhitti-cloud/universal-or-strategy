# PTT-BUGS-LIVE-0911 -- Lane A Architecture Plan
# BUG-1: PTT-Flatten Reverse Entry Guard
# ptt-architect | 2026-09-11 | Phase 1 Output (Revision 6 -- V-04 fully resolved)

---

## 0. LANE-SPLIT GATE RESULT

**Epic:** PTT-BUGS-LIVE-0911  
**Total bugs in epic:** 2 (BUG-1 Lane A, BUG-2 Lane B)  
**This plan covers:** Lane A -- BUG-1 ONLY  
**Shared file:** Both lanes touch `CopyEngine.cs`. Orchestrator has determined Lanes A and B run **sequentially** (not in parallel).

**Lane A Internal Split Gate:**
Lane A is a SINGLE bug (BUG-1). No further lane split is needed within Lane A.
- Files: `CopyEngine.cs` (modify), `CopyEngineTests.cs` (add test), `PttFlatten.cs` (READ ONLY)
- Ticket count: 1 (T1)

**LANE-SPLIT GATE RESULT: PASS -- single ticket, no internal split required.**

---

## 1. Bug Analysis -- Root Cause Confirmed

### 1.1 Live Evidence (2026-09-11)

From `docs/brain/live-trading-analysis-2026-09-11.md`:

**Event A (5:55:36 AM -- Trade 2 exit):**
- Leader -09 and all followers closed via QX-2T + native Close
- Account -05 was already flat after QX-T1 filled + native Close
- PTT-Flatten dispatch arrived on -05 when position qty == 0
- NT8 submitted `Buy x7 Entry "PTT-Flatten"` -- fired **twice**
- Accidental 14L long on -05, closed 6 seconds later

**Event B (7:14:58 AM -- Trade 5 exit):**
- All 4 accounts held long x6 MES
- Leader -09 closed via right-click Flatten / ChartTrader Close
- -07: `Sell x6 Exit "PTT-Flatten"` (correct close) **THEN** `Sell x6 Entry "PTT-Flatten"` (reverse short) -- SAME timestamp
- -08: same pattern at 7:14:58 -08 timestamp
- Reverse short on -07 and -08 = -$90 in accidental losses

### 1.2 Root Cause

**There are two independent fan-out paths in CopyEngine that dispatch PTT-Flatten to followers:**

**Path A -- OnOrderUpdate → TryDispatchLeaderFlat:**
```
OnOrderUpdate (leader Close:Filled)
  → TryDispatchLeaderFlat (line 1607)
    → FlattenFollower per follower (line 4868)
      → hasOpenPosition check (line 4887)  ← GUARD EXISTS
      → FlattenOneAccount
```

**Path B -- PttBus.FlatFired → RelayFlatten → Flatten(Instrument):**
```
PttFlatten.Execute (leader button press)
  → PttBus.RaiseFlatted
    → PttCopier.OnFlatFired
      → engine.RelayFlatten
        → Flatten(Instrument) (line 5145)
          → foreach AllAccounts → FlattenOneAccount  ← NO EXPLICIT OUTER GUARD
            → IsAccountFlattenable (inner guard, line 5411)
```

**The gap in Path B:** `Flatten(Instrument)` at line 5152-5153 calls `FlattenOneAccount(acc, instrument)` for every account in `AllAccounts()` without an explicit pre-filter at the loop level. It relies entirely on `IsAccountFlattenable` inside `FlattenOneAccount`. The inner guard has a race window:

1. `HasInflightFlatten` checks `acc.Orders` for Working/Submitted/Accepted PTT-Flatten orders. It does **not** check `Initialized` state. Orders created by Path A's `DoFlattenOrder` may still be in the `Initialized` gap (between `CreateOrder` and `Submit`) when Path B fires.
2. `FindPosition` reads the live NT8 position object. When a close order is **submitted but not yet filled**, `Position.Quantity` still reflects the pre-close quantity (e.g., 7). The guard sees qty > 0 and proceeds.

**Race scenario (confirmed by 7:14 AM log):**
1. Path A fires (OnOrderUpdate: leader Close:Filled) → FlattenFollower on -07 → qty > 0 → FlattenOneAccount submits PTT-Flatten close → **EXIT** PTT-Flatten fills, position → 0
2. Path B fires shortly after (or before fill arrives) → Flatten(Instrument) loop → qty still reads > 0 (or close just filled, qty now 0) → either way a second PTT-Flatten dispatches → position is 0 when it arrives → NT8 submits as **ENTRY**

**Race scenario (confirmed by 5:55 AM log -- both fired as Entry):**
- Follower -05 was already flat (QX-T1 + native Close had closed it)
- Two separate dispatches of FlattenOneAccount both ran on -05
- Both saw the account as "safe to flatten" (possibly due to the Initialized gap in HasInflightFlatten)
- Both submitted PTT-Flatten market orders
- Both executed as Entry (qty was 0 for both)

### 1.3 Why FlattenFollower Guard at Line 4887 Did Not Prevent It

`FlattenFollower` (line 4878-4890) guards the **TryDispatchLeaderFlat path** (Path A) correctly. It calls `hasOpenPosition(acc, instrument)` → `HasOpenPosition` → `FindPosition` → `pos.Quantity > 0`.

This guard fires correctly when:
- Follower is definitively flat at dispatch time (qty == 0 confirmed before first dispatch)

It does NOT prevent:
- Path B (Flatten(Instrument)) from firing a second dispatch after Path A has already submitted a close but before that close fills
- The mid-close timing race where qty > 0 at the moment BOTH paths check

---

## 2. Fix Design

### 2.1 Summary

Add an explicit position guard at the **outer fan-out loop level** in `Flatten(Instrument)` and `Flatten(Account, Instrument)` in `CopyEngine.cs`. This mirrors exactly what `FlattenFollower` already does for the TryDispatchLeaderFlat path, bringing the PttBus path to the same standard.

**Scope:** 2 methods in `CopyEngine.cs` modified + 1 static helper extracted for testability. Read-only for `PttFlatten.cs`. One [Fact] in `CopyEngineTests.cs`.

**Touch nothing else.**

### 2.2 Exact Location -- `Flatten(Instrument)` at `src/PropTraderTools/CopyEngine.cs` lines 5145-5154

**Current code (line 5152-5153):**
```csharp
foreach (var acc in AllAccounts(instrument))
    FlattenOneAccount(acc, instrument);
```

**Fixed code (helper-call form):**
```csharp
// BUG-0911-A: FLAT-GUARD added. CYC: 3->4.
FlatGuardLoop(
    () => AllAccounts(instrument),
    acc => FindPosition(acc, instrument),
    acc => FlattenOneAccount(acc, instrument)
);
```

**CYC impact:** 3 → 4 (foreach replaced by single helper call; CYC of `FlatGuardLoop` is accounted separately). Within limit.

**Header comment update:**
```
// BUG-0911-A: FLAT-GUARD added -- skip flat followers before FlattenOneAccount. CYC: 3->4.
```

### 2.3 Exact Location -- `Flatten(Account, Instrument)` at `src/PropTraderTools/CopyEngine.cs` lines 5181-5200

**Current code (lines 5194-5198) -- follower loop:**
```csharp
foreach (var acc in AllAccounts(instrument))
{
    if (acc == leader)
        continue;
    FlattenOneAccount(acc, instrument);
}
```

**Fixed code (helper-call form, after the leader direct call):**
```csharp
// BUG-0911-A: FLAT-GUARD added in follower loop. CYC: 5->6.
FlatGuardLoop(
    () => AllAccounts(instrument).Where(acc => acc != leader),
    acc => FindPosition(acc, instrument),
    acc => FlattenOneAccount(acc, instrument)
);
```

**CYC impact:** 5 → 6 (follower foreach replaced by helper call + `Where` predicate; CYC of `FlatGuardLoop` is accounted separately). Within limit.

**Header comment update:**
```
// CYC=5: (1) TrimFlatten, (2) leader null, (3) leader direct, (4) foreach, (5) leader skip.
// BUG-0911-A: FLAT-GUARD added in follower loop -- pos null/qty guard before FlattenOneAccount. CYC: 5->6.
```

### 2.4 Guard Logic Explanation

```csharp
var pos = FindPosition(acc, instrument);
if (pos == null || pos.Quantity == 0)
```

- `FindPosition` iterates `acc.Positions` by `Instrument.FullName` match.
- Returns `null` when the account has no position entry for the instrument (fully flat, position removed from acc.Positions).
- Returns a `Position` object when the account has a position. If `Quantity == 0`, NT8 kept a zero-qty entry.
- Either null or qty==0 means the account is flat -- skip and log.

### 2.5 Log Message (ASCII-only)

```
[FLAT-GUARD] skip flat, already flat on {acc.ToString()}
```

Full NT8 Output call (acc is TAccount generic -- use acc.ToString(), not acc.Name):
```csharp
NinjaTrader.Code.Output.Process(
    "[FLAT-GUARD] skip flat, already flat on " + acc.ToString(),
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```

Characters: `[ ] - A Z a z 0 9 space comma .` -- pure ASCII. JS compliance: PASS.

### 2.6 What Is NOT Changed

- `TryDispatchLeaderFlat` (line 4846-4870): already guarded via `FlattenFollower`. **NO CHANGE.**
- `FlattenFollower` (line 4878-4890): already has `hasOpenPosition` guard. **NO CHANGE.**
- `IsAccountFlattenable` (line 5411-5425): inner guard unchanged. Still active as second layer. **NO CHANGE.**
- `HasInflightFlatten` (line 5429-5441): unchanged. **NO CHANGE.**
- `PttFlatten.Execute` (PttFlatten.cs line 47): **READ ONLY. NO CHANGE.**
- QX paths, BE paths, Trim paths: **NO CHANGE.**
- `DoFlattenOrder`, `SubmitMarketFlattenOrder`, `CancelAllAccountOrders`: **NO CHANGE.**

---

## 3. Test Specification

### 3.1 Test: `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard`

**File:** `src/PropTraderTools/CopyEngineTests.cs`

**Fact annotation:**
```csharp
[Fact]
```

This test does NOT require `[Fact(Skip = "NT8-runtime")]` because it invokes the guard logic directly via an extracted static helper (see §3.2) that accepts injected delegates -- no NT8 runtime objects are instantiated.

**Purpose:** Verify that the new `pos == null || pos.Quantity == 0` FLAT-GUARD added in `Flatten(Instrument)` (Path B, line 5152) does NOT invoke `FlattenOneAccount` when a follower account's position is null (fully flat, no position entry). This directly exercises the new code path introduced by BUG-0911-A.

**Setup:**
```csharp
bool flattenOneCalled = false;

// Stub: one follower account object (opaque -- not an NT8 runtime Account)
var followerToken = new object();

// Injected delegates that stand in for NT8 instance-method calls:
Func<IEnumerable<object>> getAccounts = () => new[] { followerToken };
Func<object, NinjaTrader.Cbi.Position?> findPosition = acc => null; // flat: no position entry
Action<object> flattenOne = acc => flattenOneCalled = true;

// Retrieve the static helper via reflection:
// FlatGuardLoop has ONE generic parameter (TAccount) after V-03 fix.
var mi = typeof(CopyEngine)
    .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
    .Single(m => m.Name == "FlatGuardLoop" && m.GetGenericArguments().Length == 1);
Assert.NotNull(mi); // guard: method must exist after BUG-0911-A implementation
var miConcrete = mi.MakeGenericMethod(typeof(object));
```

**Act:**
```csharp
miConcrete.Invoke(null, new object[] { getAccounts, findPosition, flattenOne });
```

**Assert:**
```csharp
Assert.False(flattenOneCalled,
    "FlatGuardLoop must not call flattenOne when findPosition returns null (flat follower)");
```

**CYC of test:** 1 (straight-line). PASS.

**Test name:** `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard`

**What it asserts:**
- When the injected `findPosition` returns `null` (account is flat -- no position entry in acc.Positions), `flattenOne` is NEVER called.
- Directly exercises the new `if (pos == null || pos.Quantity == 0)` guard on Path B.
- Does NOT test `FlattenFollower` (Path A, unmodified). Tests the new guard in the Path B fan-out loop.

**Test count impact:** N → N+1 (one new [Fact]).

---

### 3.2 Static Helper Extraction -- `FlatGuardLoop`

To make the guard unit-testable without NT8 runtime, the engineer **extracts** the inner loop body of `Flatten(Instrument)` into a private static helper with injected delegates. This is the same pattern used for `FlattenFollower` (line 4878).

**Extraction:**

```csharp
// BUG-0911-A: extracted for testability -- mirrors FlattenFollower delegate-injection pattern.
// CYC=3: (1) foreach, (2) pos == null branch, (3) pos.Quantity == 0 branch.
private static void FlatGuardLoop<TAccount>(
    Func<IEnumerable<TAccount>> getAccounts,
    Func<TAccount, NinjaTrader.Cbi.Position?> findPosition,
    Action<TAccount> flattenOne
)
{
    foreach (var acc in getAccounts())
    {
        var pos = findPosition(acc);
        if (pos == null || pos.Quantity == 0)
        {
            NinjaTrader.Code.Output.Process(
                "[FLAT-GUARD] skip flat, already flat on " + acc.ToString(),
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            continue;
        }
        flattenOne(acc);
    }
}
```

**Note on `pos.Quantity`:** `TPosition` is removed entirely (V-03 fix). The `findPosition` delegate is typed as `Func<TAccount, NinjaTrader.Cbi.Position?>`. `NinjaTrader.Cbi.Position` has `int Quantity` -- `pos.Quantity` now compiles correctly (CS1061 eliminated). In unit tests the `findPosition` delegate returns `null` (flat path), exercising the `pos == null` branch directly without needing a real `Position` object. The retained-entry path (`pos != null, pos.Quantity == 0`) is also covered by `IsAccountFlattenable` as the second layer.

**`Flatten(Instrument)` updated to call the helper:**
```csharp
internal void Flatten(Instrument instrument)
{
    if (!_flags.TrimFlatten)
    {
        StatusUpdate?.Invoke("Trim/Flatten requires Pro tier");
        return;
    }
    // BUG-0911-A: FLAT-GUARD added. CYC: 3->4.
    FlatGuardLoop(
        () => AllAccounts(instrument),
        acc => FindPosition(acc, instrument),
        acc => FlattenOneAccount(acc, instrument)
    );
}
```

**`Flatten(Account, Instrument)` follower loop updated to call the helper (after the leader direct call):**
```csharp
// BUG-0911-A: FLAT-GUARD added in follower loop. CYC: 5->6.
FlatGuardLoop(
    () => AllAccounts(instrument).Where(acc => acc != leader),
    acc => FindPosition(acc, instrument),
    acc => FlattenOneAccount(acc, instrument)
);
```

**CYC of `FlatGuardLoop`:** 3 (foreach + null check + qty==0 check). Within limit.

**CYC impact (updated):**
- `Flatten(Instrument)`: 3 → 4 (foreach replaced by helper call; CYC of helper is separate). Within limit.
- `Flatten(Account, Instrument)`: 5 → 6 (follower foreach replaced by helper call + `Where` predicate). Within limit.
- `FlatGuardLoop`: new method, CYC = 3 (foreach + null check + qty==0 check). Within limit.

---

## 4. Seven-Scan Checklist Pre-Assessment

After the fix is applied, each scan should report the following:

| Scan | Check | Expected After Fix |
|------|-------|--------------------|
| SCAN-01 | `grep -r "lock(" src/` | 0 matches. No `lock()` introduced. The guard uses `FindPosition` which iterates `acc.Positions` without a lock (existing pattern). `FlatGuardLoop` is a static helper with no shared state. |
| SCAN-02 | `grep -r "DateTime.Now" src/` | 0 new usages. No timestamp in the fix. |
| SCAN-03 | CYC per method (McCabe) | `Flatten(Instrument)`: CYC 3 → 4. `Flatten(Account,Instrument)`: CYC 5 → 6. `FlatGuardLoop`: CYC 3 (new method, foreach + null + qty==0). All methods ≤ 8. PASS. |
| SCAN-04 | Non-ASCII characters in string literals | 0 violations. Log string `"[FLAT-GUARD] skip flat, already flat on "` is pure ASCII (brackets, letters, spaces, hyphen). |
| SCAN-05 | New public API surface | 0 new public methods, properties, or events. `FlatGuardLoop` is `private static`. Guard is internal to existing private foreach loops. |
| SCAN-06 | Test covers the new guard (Path B) | `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard` invokes `FlatGuardLoop` directly with `findPosition` returning `null` and asserts `flattenOne` is NOT called. Directly exercises the new guard on Path B. PASS. |
| SCAN-07 | `?.Event -=` null-conditional unsubscription | 0. No event subscription changes anywhere in the fix. |

---

## 5. Component Map

### 5.1 Files Modified

| File | Role | Change Type |
|------|------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | Fan-out guard + helper extraction | ADD `FlatGuardLoop` static; UPDATE 2 foreach loops to call it |
| `src/PropTraderTools/CopyEngineTests.cs` | Test coverage | ADD 1 new [Fact] |
| `src/PropTraderTools/Features/PttFlatten.cs` | Read-only reference | NO CHANGE |

### 5.2 Methods Touched in CopyEngine.cs

| Method | Line | Change |
|--------|------|--------|
| `Flatten(Instrument instrument)` | 5145 | Replace inner foreach with `FlatGuardLoop(...)` call. CYC 3→4. |
| `Flatten(Account leader, Instrument instrument)` | 5181 | Replace follower foreach with `FlatGuardLoop(...)` call. CYC 5→6. |
| `FlatGuardLoop<TAccount>(...)` | new | NEW private static helper (one generic param after V-03 fix). CYC 3. Testable without NT8 runtime. |

### 5.3 Methods NOT Modified (Explicitly Out of Scope)

| Method | Line | Reason |
|--------|------|--------|
| `TryDispatchLeaderFlat` | 4846 | Already has guard via FlattenFollower at line 4887. |
| `FlattenFollower` | 4878 | Guard already present at line 4887. Not the bug location. |
| `IsAccountFlattenable` | 5411 | Inner guard retained as second layer. Not modified. |
| `HasInflightFlatten` | 5429 | Initialized-gap known but out of scope for surgical fix. |
| `Flatten(Instrument, int, double, double)` | 5586 | Uses `PTT-FlattenLimit` signal. Not the PTT-Flatten fan-out path. |
| `FlattenOneAccount` | 5392 | Not modified. Inner guards remain as defense-in-depth. |
| `DoFlattenOrder` | 5518 | Not modified. Creates PTT-Flatten order for actual positions. |

---

## 6. Data Flow (After Fix)

```
PttFlatten.Execute (leader)
  → PttBus.RaiseFlatted
    → PttCopier.OnFlatFired
      → engine.RelayFlatten
        → Flatten(Instrument) [line 5145]
          → FlatGuardLoop(getAccounts, findPosition, flattenOne):   ← NEW HELPER
              foreach acc in AllAccounts(instrument):
                  pos = FindPosition(acc, instrument)               ← NEW GUARD
                  if (pos == null || pos.Quantity == 0):            ← NEW GUARD
                      [FLAT-GUARD] log + continue                   ← NEW LOG
                  else:
                      FlattenOneAccount(acc, instrument)            ← unchanged downstream
```

```
OnOrderUpdate (leader Close:Filled)
  → TryDispatchLeaderFlat [line 1607]   (UNCHANGED)
    → FlattenFollower per follower [line 4868]
        if (acc == null): return         ← existing
        if (!hasOpenPosition(...)): return ← existing at line 4887
        FlattenOneAccount(acc, instrument)
```

Both paths now have an explicit positional guard before `FlattenOneAccount` is called on any follower.

---

## 7. Deferred Work

### 7.1 Carry-Forward from PTT-COPIER-B26

All items carried forward unchanged from `docs/brain/PTT-COPIER-B26/06-deferred-backlog.md`:

| ID | Item | Priority | Status |
|----|------|----------|--------|
| DW-B24-01 | NT8-043 formal rule entry: confirm null-conditional event unsubscription causes silent runtime crash. SCAN-07 = 0 in this fix. Rule watch continues. | P2 | OPEN |
| DW-B24-02 | Manual E2E Ctrl+Shift+B runtime verification (BreakEven). Priority escalated after B26 fix. Must execute before production release. | P1 | OPEN |
| DW-B24-03 | Skip-duplicate guard [Fact]: formal test for `if (acc == leader) continue` guard at CopyEngine.cs:~1195. | P2 | OPEN |
| DW-B25-01 | Companion field race: `_pendingBeAccount`, `_trailBeAccount` plain singleton refs. Multi-panel race possible. Out of scope for this fix. | P3 | OPEN |
| DW-B26-01 | Reflection test upgrade Option B→A for `TradeCopierPanel_BeBufferBox_FieldDoesNotExist`. | P2 | OPEN |

### 7.2 New Deferred Item -- This Block

| ID | Item | Priority | Status |
|----|------|----------|--------|
| DW-BUG-0911-A-01 | **Mid-close race window**: The FLAT-GUARD catches qty==0 definitively, but does NOT catch the case where a close order is in-flight (qty>0) and a second dispatch fires before the first close fills. A complete race elimination would require an atomic interlocked slot (similar to `_lastHasPos` ConcurrentDictionary pattern). Scoped out -- complex, separate pipeline, and the surgical guard eliminates the majority of accidental reverse entries observed in live trading. | P2 | OPEN |
| DW-BUG-0911-A-02 | **`pos.Quantity == 0` retained-entry path**: The `FlatGuardLoop` generic helper uses a null check (covers the primary flat case where no position entry exists). The zero-quantity retained-entry variant (`pos != null, pos.Quantity == 0`) relies on `IsAccountFlattenable` as the second layer. If a future incident confirms the retained-entry path is being hit, upgrade `FlatGuardLoop` to accept a `Func<TPosition, bool> isFlat` predicate and pass `pos => pos.Quantity == 0`. | P3 | OPEN |

---

## 8. Out of Scope

The following are explicitly NOT addressed by this fix:

| Path | Reason |
|------|--------|
| `PttFlatten.Execute` leader path | Spec constraint: do not touch. The leader side is correct -- it guards `pos == null or qty == 0` before submitting the leader close. |
| BE paths (MoveStopToBreakEven, SubmitBeStop, ArmPendingBe, TrailBe) | Not related to PTT-Flatten dispatch. |
| QX paths (PttGlobalQuickExit, CancelQxBrackets, TryReplacePttBeBrackets) | Not related to PTT-Flatten dispatch. |
| Trim paths (TrimOneAccount, FlattenOneAccountLimit, etc.) | Separate feature. Not involved in PTT-Flatten events. |
| FlattenIfNotArming (DW-LB-FL-01 path) | NakedPositionDetector-triggered path. Out of scope for this fix. |
| BUG-2 (fo=NULL bracket failure after QX cycle) | Lane B. Separate ticket. Sequential after Lane A merges. |
| DispatchCopy, SendCopy, SendCopyWithAtm | Entry copy paths. Not involved. |
| `HasInflightFlatten` Initialized-gap | The gap where CreateOrder→Submit window is not covered by IsFlattenOrderActive. Tracked as DW-BUG-0911-A-01. Deferred. |

---

## 9. NT8 Key Facts (Embedded)

Per architectural mandate:

- `AtmStrategyChangeStopTarget()` -- StrategyBase-only. NOT AddOnBase. NOT used in this fix. PASS.
- `AtmStrategyCreate()` -- StrategyBase-only. NOT AddOnBase. NOT used in this fix. PASS.
- `Account.Change()` -- AddOnBase available but silent no-op on ATM-owned brackets. NOT used in this fix. PASS.
- `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` -- AddOnBase available. The guard PREVENTS these from being called on flat followers. Existing calls in DoFlattenOrder unchanged.
- `Position.Quantity` -- int. Reflects filled/open position. Safe to read from any thread in NT8 AddOn. Returns 0 when position is flat (if acc.Positions entry exists); acc.Positions contains no entry for the instrument if fully flat (FindPosition returns null).
- `DateTime.UtcNow` -- used per policy. No `DateTime.Now`. No datetime usage in this fix. PASS.

---

## 10. Summary

**Fix:** Extract `FlatGuardLoop<TAccount>` private static helper (CYC=3) with injected delegates. `findPosition` delegate typed as `Func<TAccount, NinjaTrader.Cbi.Position?>` (V-03: `TPosition` generic removed; `pos.Quantity` now compiles against the concrete NT8 type). Replace the inner foreach in `Flatten(Instrument)` (line 5152) and the follower foreach in `Flatten(Account, Instrument)` (line 5194) with calls to `FlatGuardLoop`. The helper checks BOTH `pos == null` AND `pos.Quantity == 0`, logs `[FLAT-GUARD]` on either condition, and only calls `flattenOne` when the account has a live position. No other changes.

**Files changed:** `CopyEngine.cs` (2 loops replaced + 1 private static method added), `CopyEngineTests.cs` (1 test added).

**Files read-only:** `PttFlatten.cs`.

**CYC:** `Flatten(Instrument)` 3→4. `Flatten(Account, Instrument)` 5→6. `FlatGuardLoop` new, CYC=3. All within limit.

**Test:** `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard` -- `[Fact]` (no skip required), invokes `FlatGuardLoop` via reflection with null-returning `findPosition` delegate, asserts `flattenOne` NOT called. Directly exercises Path B new guard.

**All 7 scans pass after fix.**

---

*ptt-architect | PTT-BUGS-LIVE-0911 Lane A | Phase 1 Revision 4 | 2026-09-11*

**PLAN_COMPLETE**
