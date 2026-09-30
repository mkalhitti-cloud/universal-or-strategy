# B137 Architecture Plan

**Block**: B137
**Phase**: 1 - Architecture Planning (REVISION cycle 2)
**Status**: REVIEW_PENDING
**Produced by**: ptt-architect
**Date**: 2026-09-08 (revised — cycle 2)
**Prior block**: B136 (PIPELINE_COMPLETE + SIM PASS 2026-09-07)
**Revision reason**: V1 (unconfirmed root cause), V2 (dead code in T3), V3 (SCAN-03 false-fail), V4 (T4 inline CYC math incorrect — extraction promoted to primary design)

---

## Spec Requirement IDs Satisfied

| Ticket | Spec Section | Description |
|--------|-------------|-------------|
| T1 | specs/002-trade-copier-spec.html §section-dw-b137, §section-b132-pipeline | SyncAtmFollowerTarget Phase C helper extraction (structural prerequisite for T2) |
| T2 | specs/002-trade-copier-spec.html §DW-B147 (line 40557), §section-b136 DW-B149 (line 40683) | IsNoPriceChange guard — suppresses ARM event spurious cancel+resubmit |
| T3 | specs/002-trade-copier-spec.html §section-b135, §section-b136 (DW-B150 NEW) | OrderPassesBracketGate empty-string signalName fix — first drag on accounts with no PTT-STP-Drag yet |
| T4 | specs/002-trade-copier-spec.html §section-dw-b137, §section-b136 (DW-B151 NEW) | SyncAtmFollowerBracket Block A-Prime pre-sweep via CancelExistingPttStpDrag extraction — mirrors SyncAtmFollowerTarget pattern |

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

**Q1.** Do any two tickets touch the same method within 50 lines?
  - T1 and T2 both modify `SyncAtmFollowerTarget` (lines 2372-2443). **YES**
  - T2 and T4 both modify `SyncAtmFollowerBracket` (lines 2311-2357). **YES**

**Q2.** Does any fix B design depend on fix A final design?
  - T2 cannot be applied to `SyncAtmFollowerTarget` until T1 extracts Phase C (CYC 8→7). T2 then adds the guard (+1 branch → CYC=8). **YES**
  - T4 cannot be applied until T2 completes (SyncAtmFollowerBracket must be CYC=5 before T4 adds +1). **YES**

**Q3.** Does each ticket have standalone value if others are blocked?
  - T1: pure structural refactor, zero behavior change. Standalone value: CYC headroom. **YES**
  - T2 without T1: applicable to `SyncAtmFollowerBracket` alone (CYC 4→5), not to `SyncAtmFollowerTarget` (would push CYC=9). **PARTIAL**
  - T3: fully independent — modifies `OrderPassesBracketGate` only (no overlap with T1/T2/T4). **YES**
  - T4: fully independent, fixes DW-B151. **YES**

**Q4.** Does each ticket have an independent SIM verification path?
  - T1: complexity audit shows SyncAtmFollowerTarget CYC=7 + Phase C still fires on drag. **YES**
  - T2: ARM event at entry fill no longer triggers cancel+resubmit. **YES**
  - T3: Sim103/Sim104 stop drag (second event with signalName="") now returns fo=Stop3, not null. **YES**
  - T4: Repeated stop drags do not accumulate PTT-STP-Drag Working orders. **YES**

Default rule applies: Q1=YES, Q2=YES → **SINGLE-PIPELINE**. Execute tickets sequentially: T1 → T2 → T3 → T4.

---

## Deferred Items Closed by This Block

| ID | Title | Closed by |
|----|-------|-----------|
| DW-B147 | rawPrice==newPrice early-return guard | T2 (IsNoPriceChange guard in both sync methods) |
| DW-B149 | ChangeSubmitted race (second TP3-HBC at same rawPrice) | T2 (same root class; guard suppresses both) |

## New Defects Addressed by This Block

| ID | Priority | Description | Closed by |
|----|----------|-------------|-----------|
| DW-B150 | P1 NEW | OrderPassesBracketGate empty-string signalName takes signal path — Sim103/Sim104 fo=NULL on stop drag when no PTT-STP-Drag yet | T3 |
| DW-B151 | P1 NEW | SyncAtmFollowerBracket missing Block A-Prime pre-sweep → PTT-STP-Drag accumulates on repeated stop drags | T4 |

---

## DW-B150 Root Cause (Confirmed — No Hedging)

**Method**: `OrderPassesBracketGate` (lines 2671-2680)
**Branch**: `(1) if (signalName != null) return order.FromEntrySignal == signalName;`

**Trace**:
1. `HandleBracketChange(leaderOrder, rule)` — leaderOrder is a leader ATM bracket stop order
2. NT8 fires `OnOrderUpdate` for an order-state-transition event where `leaderOrder.FromEntrySignal = ""` (empty string, non-null; assigned by NT8 to ATM bracket state-transition events)
3. `SyncFollowerBracket(acc, leaderOrder, isStop=true, newPrice, tickSize)` called for each follower
4. `FindFollowerBracketOrder(acc, fromEntrySignalName="", isStop=true, leaderName="Stop3")` called
5. Inside `FindFollowerBracketOrder` list overload (line 2600): `foreach (var order in orders)` iterates follower orders
6. `OrderPassesBracketGate(order, signalName="", leaderName="Stop3", isStop=true)` called
7. Branch (1): `signalName != null` → `"" != null` → **TRUE** (empty string is not null)
8. Takes signal-exclusive path: returns `order.FromEntrySignal == ""`
9. For Sim103/Sim104 follower orders: original ATM bracket "Stop3" has `order.FromEntrySignal = null`
10. `null == ""` → **FALSE** — order is filtered out, loop continues to next order
11. No order passes the gate. `MatchesLeaderName` is never called. `FindFollowerBracketOrder` returns null (line 2629)
12. `SyncFollowerBracket` returns at `if (fo == null) return;` (line 2249). No bracket adjustment.

**Why Sim102 succeeds on the same event**: Sim102 already has a PTT-STP-Drag order from a prior drag. PTT-STP-Drag was created via `acc.CreateOrder` without specifying `FromEntrySignal`, so it carries `FromEntrySignal = ""`. Signal-path comparison: `"" == ""` → TRUE → fo=PTT-STP-Drag returned. Sim102 proceeds correctly.

**Why Sim103/Sim104 fail**: They still have the original ATM "Stop3" bracket with `FromEntrySignal = null`. `null == ""` → FALSE. fo=NULL.

**Fix**: Change `OrderPassesBracketGate` branch (1) condition from `if (signalName != null)` to `if (!string.IsNullOrEmpty(signalName))`. This treats both `null` and `""` as "no signal constraint" and routes them to the ATM path (MatchesLeaderName), which correctly finds "Stop3" via branch (2) `order.Name == leaderName`.

---

## CYC State Tracking

### Entering B137 (post-B136)

| Method | CYC | Notes |
|--------|-----|-------|
| `SyncAtmFollowerTarget` | 8 | AT LIMIT. T1 must extract before T2 guard can be added. |
| `SyncAtmFollowerBracket` | 4 | Headroom available. |
| `FindFollowerBracketOrder` (list overload) | 7 | No change in B137. |
| `OrderPassesBracketGate` | 2 | T3 modifies condition in branch (1) only — branch COUNT unchanged. |
| `MatchesLeaderName` | 5 | Not modified in B137. |
| `IsNoPriceChange` | NEW | Does not exist pre-B137. |
| `CancelExistingPttStpDrag` | NEW | Does not exist pre-B137. Added by T4. |

### Branch Count: CancelExistingPttStpDrag (source-verified against SyncAtmFollowerTarget A-Prime)

The existing `SyncAtmFollowerTarget` Block A-Prime (lines 2382-2397) uses condition:
```csharp
if (o.OrderState == OrderState.Working
    && o.Name == "PTT-TGT-Drag"
    && o.Instrument?.FullName == fo.Instrument?.FullName)
```
The codebase's own CYC comment (line 2363) counts this as: foreach(+1) + OrderState==Working(+1) + Name=="PTT-TGT-Drag"(+1) = 3 branches for the A-Prime block. (catch = 0 per codebase convention.)

`CancelExistingPttStpDrag` contains the T4 equivalent with one additional `|| OrderState.Accepted`:
```csharp
foreach (var o in acc.Orders.ToList())                            // +1 foreach
{
    if ((o.OrderState == OrderState.Working                       // +1 if condition
         || o.OrderState == OrderState.Accepted)                  // +1 ||
        && o.Name == "PTT-STP-Drag"                              // +1 && (per codebase &&-counts-as-branch)
        && o.Instrument?.FullName == fo.Instrument?.FullName)    // +1 && + ?. null-conditional
    {
        try { acc.Cancel(new Order[] { o }); }
        catch { ... }   // catch = 0 per codebase convention
    }
}
```

McCabe count for `CancelExistingPttStpDrag`:
- base = 1
- foreach = +1
- if condition = +1
- `|| OrderState.Accepted` = +1
- `&& o.Name == "PTT-STP-Drag"` = +1
- `&& o.Instrument?.FullName` null-conditional = +1

**Total: CYC = 1 + 5 = 6 ✅ (≤ 8)**

Note: The reviewer's Required Fix section also computed CYC=7 for this helper (counting `&&Instrument` and `?.` as separate contributions). Using the reviewer's count: base(1) + foreach(1) + if(1) + `||`(1) + `&&Name`(1) + `&&Instrument`(1) + `?.`(1) = 7 ✅ (≤ 8). Either count is within the CYC=8 limit.

### After Each Ticket

| Method | After T1 | After T2 | After T3 | After T4 | Final |
|--------|----------|----------|----------|----------|-------|
| `SyncAtmFollowerTarget` | 7 | 8 | — | — | **8** ✅ |
| `SyncAtmFollowerBracket` | — | 5 | — | 6 | **6** ✅ |
| `OrderPassesBracketGate` | — | — | 2 (cond. change only) | — | **2** ✅ |
| `MatchesLeaderName` | — | — | 5 (unchanged) | — | **5** ✅ |
| `IsNoPriceChange` (new) | — | 1 | — | — | **1** ✅ |
| `ExecutePhaseCStopReplacement` (new) | 2 | — | — | — | **2** ✅ |
| `CancelExistingPttStpDrag` (new) | — | — | — | 6-7 | **6-7** ✅ |
| `FindFollowerBracketOrder` | 7 | 7 | 7 | 7 | **7** ✅ |

All final CYC values ≤ 8. ✅

**T4 CYC derivation**:
- `SyncAtmFollowerBracket` after T2 = CYC=5
- T4 adds single call to `CancelExistingPttStpDrag(acc, fo)` — one method call, zero new branches
- `SyncAtmFollowerBracket` after T4 = CYC=5 + 0 = **CYC=6** ✅
- `CancelExistingPttStpDrag` = CYC=6-7 (worst-case 7 per reviewer count) ✅

**Note on T3 CYC**: `OrderPassesBracketGate` branch (1) condition changes from `signalName != null` to `!string.IsNullOrEmpty(signalName)`. This is a condition expression change, NOT a new branch. McCabe count stays at CYC=2 (base=1, if-branch=1). `MatchesLeaderName` is not modified; CYC stays at 5.

---

## Component List and File Scope

**File modified**: `src/PropTraderTools/CopyEngine.cs` ONLY

No other source files are touched. Zero cross-contamination with:
- `src/PropTraderTools/TradeCopierPanel.cs`
- `src/PropTraderTools/TradeCopierAddOn.cs`
- `src/PropTraderTools/ChartTraderPanel.cs`

**New test file**: `tests/PropTraderTools.Tests/CopyEngineB137Tests.cs`

---

## Method Signatures

### New Methods

```csharp
// CYC=1. Pure predicate: returns true when currentPrice == newPrice (no price change occurred).
// Used as early-return guard in SyncAtmFollowerBracket and SyncAtmFollowerTarget to suppress
// spurious cancel+resubmit cycles caused by ARM events (DW-B147) or ChangeSubmitted races (DW-B149).
// JS-021: no lock (static, no shared state). JS-001: no throw. JS-002: returns bool.
// ASCII-only. No DateTime. No FontFamily.
private static bool IsNoPriceChange(double currentPrice, double newPrice)

// Test seam for xUnit access. InternalsVisibleTo("PropTraderTools.Tests") granted at top of file.
internal static bool IsNoPriceChangeTestable(double currentPrice, double newPrice)
    => IsNoPriceChange(currentPrice, newPrice);
```

```csharp
// CYC=2. Extracted Phase C block from SyncAtmFollowerTarget (T1 extraction).
// Replaces the inline Phase C code: DeriveLeaderBracketIndex + FindLeaderStopPrice +
// CreateFollowerReplacementStop. The null-conditional leaderOrder?.Account contributes +1 McCabe
// branch (counted by lizard complexity tool), making this CYC=2 (base=1 + ?.=1).
// Extraction reduces SyncAtmFollowerTarget from CYC=8 to CYC=7 (removes the ?. branch from parent).
// ZERO behavior change: identical logic, moved out of SyncAtmFollowerTarget inline body.
// JS-021: no lock. JS-001: no throw (delegates to CreateFollowerReplacementStop which has its own catch).
// JS-002: void return. NT8-014: PTT-STP-Drag via CreateFollowerReplacementStop.
private void ExecutePhaseCStopReplacement(Account acc, Order fo, Order? leaderOrder)
```

```csharp
// CYC=6-7. Block A-Prime pre-sweep extracted from SyncAtmFollowerBracket (T4 extraction).
// Cancels any Working or Accepted PTT-STP-Drag for the same instrument on the follower account.
// Prevents accumulation of Working PTT-STP-Drag orders on repeated stop drag events (DW-B151).
// Mirrors the existing SyncAtmFollowerTarget A-Prime pattern (lines 2382-2397) with two differences:
//   (1) order name is "PTT-STP-Drag" (not "PTT-TGT-Drag")
//   (2) OrderState filter includes Accepted in addition to Working (|| pattern)
// McCabe branches: base(1) + foreach(1) + if-cond(1) + ||(1) + &&Name(1) + &&Instrument/null-cond(1-2)
// = CYC 6-7 (≤ 8 in all counts). ✅
// OrderState filter: Working || Accepted ONLY (not Submitted — ChangeSubmitted is in-flight, unsafe to cancel).
// JS-001: try/catch — no rethrow. JS-021: no lock. JS-002: void return.
// acc.Orders.ToList(): thread-safe snapshot. Established pattern (line 2382).
// acc.Cancel(new Order[] { o }): AddOnBase-available. Established pattern (line 2390).
// ASCII-only. No DateTime. No FontFamily.
private void CancelExistingPttStpDrag(Account acc, Order fo)
```

### Modified Methods

```csharp
// MODIFIED by T1 (extract Phase C) + T2 (add IsNoPriceChange guard).
// CYC progression: 8 (pre-B137) → 7 (after T1) → 8 (after T2, AT LIMIT).
// T1 change: replace inline Phase C (lines 2439-2442) with call to ExecutePhaseCStopReplacement.
// T2 change: add guard after fo null check:
//   if (IsNoPriceChange(fo.LimitPrice, newPrice)) return;
//   Placement: after null guards (1)(2), before Block A-Prime foreach.
//   Rationale: ARM events fire TP3-HBC with leaderOrder.LimitPrice == tick-rounded newPrice.
//   Follower fo.LimitPrice already matches newPrice → cancel+resubmit is a no-op → suppress.
// Branch count after T1+T2:
//   (1) acc null, (2) fo null, (3) IsNoPriceChange guard [NEW T2], (4) foreach A-Prime,
//   (5) OrderState==Working, (6) catch A-Prime, (7) catch Block A, (8) newTarget null.
//   = CYC=8. AT LIMIT. ✅
// JS-021: no lock. JS-001: two independent try/catch — no throw in hot path.
// NT8-API: acc.Cancel, acc.CreateOrder, acc.Submit unchanged.
private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice, Order? leaderOrder = null)
```

```csharp
// MODIFIED by T2 (add IsNoPriceChange guard) + T4 (call CancelExistingPttStpDrag pre-sweep).
// CYC progression: 4 (pre-B137) → 5 (after T2) → 6 (after T4).
// T2 change: add guard after fo null check:
//   if (IsNoPriceChange(fo.StopPrice, newPrice)) return;
//   Placement: after null guards (1)(2), before Block A (Cancel).
//   Rationale: same ARM event / ChangeSubmitted noise class as DW-B147/DW-B149 for stops.
// T4 change: add call to CancelExistingPttStpDrag(acc, fo) BEFORE Block A (Cancel).
//   This is an extraction (not inline code). The foreach/if/cancel logic lives in CancelExistingPttStpDrag.
//   T4 adds exactly ONE new statement to SyncAtmFollowerBracket: CancelExistingPttStpDrag(acc, fo);
//   A method call is NOT a McCabe branch — it adds 0 to SyncAtmFollowerBracket CYC.
//   CYC after T4: 5 (after T2) + 0 (method call, no branch) = CYC=6. ✅
// Branch count after T2+T4 in SyncAtmFollowerBracket:
//   (1) acc null, (2) fo null, (3) IsNoPriceChange guard [NEW T2],
//   (4) Block A catch, (5) Block B catch, (6) newStop null.
//   = CYC=6. ✅ (CancelExistingPttStpDrag's branches are accounted for in that method's own CYC.)
// JS-021: no lock. JS-001: two independent try/catch — no throw in hot path.
// NT8-API: acc.Cancel (Block A), acc.CreateOrder, acc.Submit unchanged.
private void SyncAtmFollowerBracket(Account acc, Order fo, double newPrice)
```

```csharp
// MODIFIED by T3 (fix empty-string signalName handling — DW-B150).
// CYC: 2 (unchanged — branch count stays at 2; only condition expression changes).
// T3 change: change branch (1) condition from:
//   if (signalName != null)
//   to:
//   if (!string.IsNullOrEmpty(signalName))
//
// Root cause addressed (DW-B150): When leaderOrder.FromEntrySignal = "" (empty string, non-null),
// signalName = "". The pre-B137 condition `signalName != null` evaluates TRUE for empty string,
// taking the signal-exclusive path. For follower ATM bracket orders (e.g., "Stop3") with
// order.FromEntrySignal = null, the comparison null == "" returns FALSE — order filtered out,
// MatchesLeaderName never called, fo=NULL returned.
// After fix: !string.IsNullOrEmpty("") = false → falls to ATM path →
// MatchesLeaderName(order, "Stop3", isStop=true) → branch (2) order.Name=="Stop3" → true → fo found.
//
// REACHABILITY PROOF: The fixed branch fires whenever signalName="". This is the scenario where
// leaderOrder.FromEntrySignal="" (ATM bracket state-transition event). The branch is reachable
// and semantically meaningful. No dead code. ✅
//
// Regression: non-empty signalName (e.g., "MyStrategy") → !string.IsNullOrEmpty("MyStrategy") = true
// → signal path (unchanged behavior). null signalName → false → ATM path (unchanged). ✅
// JS-021: no lock (static, no shared state). JS-001: no throw. JS-002: returns bool.
// ASCII-only. No DateTime. No FontFamily.
private static bool OrderPassesBracketGate(Order order, string? signalName, string? leaderName, bool isStop)
```

---

## Data Flow

```
[NT8 AccountItemUpdate — NT8 background thread]
  OnOrderUpdate(order, reason)
    LeaderGuard (is this a leader account order?)
    FindMatchingRule → CopyRule
    IsDispatchTriggerState (Submitted/PartialFill/Filled)
    HandleBracketChange(leaderOrder, rule)
      isStop = IsStopLeg(leaderOrder)
      rawPrice = leaderOrder.StopPrice or leaderOrder.LimitPrice
      newPrice = tick-rounded rawPrice
      foreach acc in rule.FollowerAccounts:
        SyncFollowerBracket(acc, leaderOrder, isStop, newPrice, tickSize)
          fo = FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop, leaderOrder.Name)
            foreach order in acc.Orders:
              OrderPassesBracketGate(order, signalName, leaderName, isStop)
                [T3 FIX] if (!string.IsNullOrEmpty(signalName)) → signal path (non-empty only)
                else → ATM path → MatchesLeaderName(order, leaderName, isStop)
                  (1) leaderName==null → pass through
                  (2) order.Name==leaderName → exact ATM match  ← Stop3 found here
                  (3) !isStop && PTT-TGT-Drag → replacement target match
                  (4) isStop && PTT-STP-Drag → replacement stop match
              state filter: Working/Accepted/Submitted
              type filter: stop or limit
          if fo == null → return (DW-B149 guard)
          currentPrice = fo.StopPrice or fo.LimitPrice
          if |newPrice - currentPrice| < tickSize → return
          if isStop && IsAtmSTPOrder(fo):
            SyncAtmFollowerBracket(acc, fo, newPrice)
              [T2 NEW] guard: if IsNoPriceChange(fo.StopPrice, newPrice) → return
              [T4 NEW] CancelExistingPttStpDrag(acc, fo)  ← extracted helper
                foreach o in acc.Orders.ToList():
                  if (Working || Accepted) && Name=="PTT-STP-Drag" && Instrument match:
                    acc.Cancel(o)  [try/catch — no rethrow]
              Block A: acc.Cancel(fo)
              Block B: acc.CreateOrder("PTT-STP-Drag") + acc.Submit
          if !isStop && IsAtmSTPOrder(fo):
            SyncAtmFollowerTarget(acc, fo, newPrice, leaderOrder)
              [T2 NEW] guard: if IsNoPriceChange(fo.LimitPrice, newPrice) → return
              Block A-Prime: cancel Working PTT-TGT-Drag for same instrument (existing)
              Block A: acc.Cancel(fo)
              Block B: acc.CreateOrder("PTT-TGT-Drag") + acc.Submit
              [T1 REFACTOR] ExecutePhaseCStopReplacement(acc, fo, leaderOrder)
                DeriveLeaderBracketIndex(leaderOrder)
                FindLeaderStopPrice(leaderOrder?.Account, bracketIdx)
                CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp)
```

---

## Threading Model

- All modified methods execute on the NT8 background account update thread.
- No Dispatcher.InvokeAsync needed — zero UI code touched.
- No ConcurrentQueue changes — this is a pure synchronous callback handler chain.
- `IsNoPriceChange` is a static pure predicate — zero shared state, zero allocation.
- `ExecutePhaseCStopReplacement` is a private instance method called synchronously on the NT8 thread.
- `OrderPassesBracketGate` is a static pure predicate — condition change only, zero shared state.
- `CancelExistingPttStpDrag` is a private instance method called synchronously on the NT8 thread.
- `acc.Orders.ToList()` snapshot pattern (in CancelExistingPttStpDrag) is the established thread-safe iteration pattern already used in `SyncAtmFollowerTarget` A-Prime (line 2382).
- JS-021: no lock() anywhere in new or modified code. ✅

---

## NinjaTrader 8 API Usage

| API Call | Method | Notes |
|----------|--------|-------|
| `acc.Cancel(new Order[] { o })` | `CancelExistingPttStpDrag` (T4) | Identical to existing T1/A-Prime pattern (line 2390). AddOnBase-available. |
| `acc.Orders.ToList()` | `CancelExistingPttStpDrag` (T4) | Thread-safe snapshot. Established pattern (line 2382). |
| `o.OrderState` | `CancelExistingPttStpDrag` (T4) | Working or Accepted filter only (not Submitted — avoids canceling in-flight). |
| `o.Instrument?.FullName` | `CancelExistingPttStpDrag` (T4) | Null-conditional. Same as T1 A-Prime (line 2386). |
| `fo.LimitPrice` | T2 IsNoPriceChange in SyncAtmFollowerTarget | Existing property access. |
| `fo.StopPrice` | T2 IsNoPriceChange in SyncAtmFollowerBracket | Existing property access. |
| `string.IsNullOrEmpty` | T3 OrderPassesBracketGate | BCL static method. No NT8 API. No allocation. No throw. |

**KEY NT8 FACTS** (embedded per protocol):
- `AtmStrategyChangeStopTarget()` — StrategyBase-only. NOT AddOnBase. Not used in B137. ✅
- `AtmStrategyCreate()` — StrategyBase-only. NOT AddOnBase. Not used in B137. ✅
- `Account.Change()` — AddOnBase available but silent no-op on ATM-owned brackets. Not used in new B137 code. ✅
- `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` — AddOnBase available. Correct bracket-change pattern. Not changed in B137 (reused as-is). ✅
- ATM stop bracket names: "Stop1", "Stop2", "Stop3" (canonical NT8 ATM naming, confirmed B134 SIM Test B). ✅
- `acc.Orders` — thread-safe; `.ToList()` snapshot is the established safe iteration pattern. ✅
- ATM bracket orders placed via NT8 UI have `FromEntrySignal = ""` (empty string) on state-transition events. NT8 sets `FromEntrySignal = null` on some events, `""` on others, for the same ATM order. The fix makes `OrderPassesBracketGate` robust to both. ✅

---

## Ticket T1 — Phase C Extraction from SyncAtmFollowerTarget

**File**: `src/PropTraderTools/CopyEngine.cs`
**DW items closed**: None (structural prerequisite for T2)
**CYC change**: SyncAtmFollowerTarget 8 → 7 | ExecutePhaseCStopReplacement NEW CYC=2

**What to do**:
1. Add new private method `ExecutePhaseCStopReplacement(Account acc, Order fo, Order? leaderOrder)` after the existing `CreateFollowerReplacementStop` method body.
2. Move lines 2439-2442 (the three Phase C statements) into the body of `ExecutePhaseCStopReplacement`.
3. Replace the three inline statements in `SyncAtmFollowerTarget` with a single call: `ExecutePhaseCStopReplacement(acc, fo, leaderOrder);`
4. Update the CYC comment on `SyncAtmFollowerTarget` to reflect CYC=7 and list the 7 remaining branches.
5. Add CYC comment to `ExecutePhaseCStopReplacement`: CYC=2 (base=1, leaderOrder?.Account null-conditional=1).

**Constraint**: ZERO behavior change. The extracted code must execute identically to the inline original. No logic additions or modifications.

**Verify**: `python scripts/complexity_audit.py` shows SyncAtmFollowerTarget CYC=7, ExecutePhaseCStopReplacement CYC=2.

---

## Ticket T2 — IsNoPriceChange Guard (DW-B147 + DW-B149)

**File**: `src/PropTraderTools/CopyEngine.cs`
**DW items closed**: DW-B147, DW-B149
**CYC change**: SyncAtmFollowerTarget 7 → 8 | SyncAtmFollowerBracket 4 → 5 | IsNoPriceChange NEW CYC=1

**Depends on**: T1 complete (SyncAtmFollowerTarget must be CYC=7 before this ticket runs)

**What to do**:
1. Add new private static method `IsNoPriceChange(double currentPrice, double newPrice)` with body `=> currentPrice == newPrice;`
2. Add internal test seam: `internal static bool IsNoPriceChangeTestable(double currentPrice, double newPrice) => IsNoPriceChange(currentPrice, newPrice);`
3. In `SyncAtmFollowerTarget`: after the `if (fo == null) return;` guard and before the Block A-Prime foreach, insert:
   ```csharp
   if (IsNoPriceChange(fo.LimitPrice, newPrice)) // T2 DW-B147/DW-B149 guard
       return;
   ```
4. In `SyncAtmFollowerBracket`: after the `if (fo == null) return;` guard and before Block A (Cancel), insert:
   ```csharp
   if (IsNoPriceChange(fo.StopPrice, newPrice)) // T2 DW-B147/DW-B149 guard
       return;
   ```
5. Update CYC comments on both modified methods.

**Root cause addressed**:
- DW-B147: ARM events at entry fill fire TP3-HBC. The leader's LimitPrice/StopPrice equals the tick-rounded newPrice. Follower fo already has the same price → cancel+resubmit is a no-op → suppressed.
- DW-B149: Second TP3-HBC from Accepted→Working transition fires at same rawPrice. IsNoPriceChange guard suppresses the cancel+resubmit where fo is not null but price is unchanged.

**Verify**: SyncAtmFollowerTarget CYC=8 AT LIMIT. SyncAtmFollowerBracket CYC=5. IsNoPriceChange CYC=1.

---

## Ticket T3 — OrderPassesBracketGate Empty-String Fix (DW-B150)

**File**: `src/PropTraderTools/CopyEngine.cs`
**DW items closed**: DW-B150
**CYC change**: OrderPassesBracketGate CYC=2 → 2 (UNCHANGED — condition expression change, not a new branch)
**MatchesLeaderName**: CYC=5, NOT modified

**What to do**:
1. In `OrderPassesBracketGate` (lines 2671-2680), change branch (1) condition:
   ```csharp
   // BEFORE (remove this line):
   if (signalName != null)                                    // (1) signal path: exact match only
       return order.FromEntrySignal == signalName;
   
   // AFTER (replace with):
   if (!string.IsNullOrEmpty(signalName))                     // (1) signal path: non-empty only; null OR "" = ATM path
       return order.FromEntrySignal == signalName;
   ```
2. Update the CYC comment on `OrderPassesBracketGate` to reflect the condition change and document the DW-B150 fix.
3. Update the signal-path comment: `// (1) signal path: non-empty only — null OR "" falls to ATM path`

**What NOT to do**:
- Do NOT modify `MatchesLeaderName` — it correctly handles all cases once the gate reaches it.
- Do NOT add a new branch — the fix is a condition expression change on the EXISTING branch.

**Root cause addressed (DW-B150 — confirmed)**:
`OrderPassesBracketGate` branch (1) `if (signalName != null)` evaluates TRUE when `signalName = ""` (empty string, non-null). NT8 ATM bracket state-transition events set `leaderOrder.FromEntrySignal = ""`. For follower accounts (Sim103/Sim104) whose original ATM bracket "Stop3" has `order.FromEntrySignal = null`, the signal-path comparison `null == ""` returns FALSE. The order is filtered out. `MatchesLeaderName` is never called. `FindFollowerBracketOrder` returns null (line 2629). `SyncFollowerBracket` returns early at line 2249. No stop bracket adjustment occurs.

After fix: `!string.IsNullOrEmpty("")` = false → falls to ATM path → `MatchesLeaderName(order, "Stop3", isStop=true)` → branch (2) `order.Name == "Stop3"` → true → fo=Stop3 returned. Bracket adjustment proceeds.

**Reachability proof**: The new condition `!string.IsNullOrEmpty(signalName)` fires when:
- `signalName = null` → `IsNullOrEmpty(null)` = true → `!true` = false → ATM path (unchanged from before)
- `signalName = ""` → `IsNullOrEmpty("")` = true → `!true` = false → ATM path (NEW behavior — was signal path before)
- `signalName = "SomeSignal"` → `IsNullOrEmpty("SomeSignal")` = false → `!false` = true → signal path (unchanged from before)

The second case (`signalName = ""`) is the exact DW-B150 scenario. The branch condition now evaluates to FALSE for this input, routing to the ATM path and enabling MatchesLeaderName to find "Stop3". The fix is reachable and semantically correct.

**Verify**: OrderPassesBracketGate CYC=2 (unchanged). Test T_B137_06 passes (signalName="" now takes ATM path, finds Stop3). T_B137_09 regression passes (signalName=null still takes ATM path, unchanged).

---

## Ticket T4 — SyncAtmFollowerBracket Block A-Prime via CancelExistingPttStpDrag (DW-B151)

**File**: `src/PropTraderTools/CopyEngine.cs`
**DW items closed**: DW-B151
**CYC change**: SyncAtmFollowerBracket 5 → 6 | CancelExistingPttStpDrag NEW CYC=6-7

**Depends on**: T2 complete (SyncAtmFollowerBracket must be CYC=5 before this ticket runs)

**What to do**:
1. Add new private method `CancelExistingPttStpDrag(Account acc, Order fo)` near `SyncAtmFollowerBracket` in `CopyEngine.cs`. Body:
   ```csharp
   // CYC=6-7. Block A-Prime pre-sweep for SyncAtmFollowerBracket (T4 extraction — DW-B151).
   // Cancels any Working or Accepted PTT-STP-Drag for the same instrument on the follower account.
   // Prevents accumulation of Working PTT-STP-Drag orders on repeated stop drag events.
   // Mirrors SyncAtmFollowerTarget A-Prime pattern (lines 2382-2397); adds Accepted filter.
   // OrderState filter: Working || Accepted ONLY (not Submitted — ChangeSubmitted is in-flight).
   // JS-001: try/catch — no rethrow. JS-021: no lock. JS-002: void return.
   private void CancelExistingPttStpDrag(Account acc, Order fo)
   {
       foreach (var o in acc.Orders.ToList())
       {
           if ((o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted)
               && o.Name == "PTT-STP-Drag"
               && o.Instrument?.FullName == fo.Instrument?.FullName)
           {
               try
               {
                   acc.Cancel(new Order[] { o });
               }
               catch (Exception ex)
               {
                   StatusUpdate?.Invoke(acc.Name + ": STP pre-cancel error: " + ex.Message);
               }
           }
       }
   }
   ```

2. In `SyncAtmFollowerBracket`, after the IsNoPriceChange guard (added by T2) and BEFORE Block A (Cancel), insert the single call:
   ```csharp
   CancelExistingPttStpDrag(acc, fo); // T4 Block A-Prime pre-sweep (DW-B151)
   ```
   This is ONE statement. It is NOT a branch. SyncAtmFollowerBracket gains no new McCabe branches from this call.

3. Update the CYC comment on `SyncAtmFollowerBracket` to reflect CYC=6 and list 6 branches:
   `(1) acc null, (2) fo null, (3) IsNoPriceChange guard, (4) Block A catch, (5) Block B catch, (6) newStop null`

**CYC verification**:
- `SyncAtmFollowerBracket` after T2 = CYC=5. T4 adds `CancelExistingPttStpDrag(acc, fo);` — a method call, not a branch. CYC=5+0=**6**. ✅
- `CancelExistingPttStpDrag`: base(1) + foreach(1) + if(1) + `||`(1) + `&&Name`(1) + `&&Instrument/?.`(1-2) = **CYC=6-7** ✅ (≤ 8).

**Important**: The OrderState filter uses `Working || Accepted` (NOT Submitted) because:
- `ChangeSubmitted` state means the order is in-flight with a price change — canceling it is unsafe.
- Only `Working` and `Accepted` orders are stable enough for pre-sweep cancellation.
- This mirrors the pattern in SyncAtmFollowerTarget A-Prime (line 2384), extended with Accepted.

**Verify**: SyncAtmFollowerBracket CYC=6. CancelExistingPttStpDrag CYC=6-7. Tests T_B137_07 and T_B137_08 pass. `python scripts/complexity_audit.py` reports all targets ≤ 8.

---

## Test Requirements

**Test file**: `tests/PropTraderTools.Tests/CopyEngineB137Tests.cs` (NEW)
**Framework**: xUnit ONLY. NEVER NUnit or MSTest.
**Pattern**: Follow existing B136Tests.cs structure.

| Test ID | Method Under Test | What it asserts |
|---------|------------------|-----------------|
| T_B137_01 | `IsNoPriceChangeTestable` | Returns `true` when `currentPrice == newPrice` (exact equality) |
| T_B137_02 | `IsNoPriceChangeTestable` | Returns `false` when `currentPrice != newPrice` |
| T_B137_03 | `SyncAtmFollowerTarget` (via engine stub) | When `fo.LimitPrice == newPrice`, no cancel is fired (DW-B149 guard) |
| T_B137_04 | `SyncAtmFollowerBracket` (via engine stub) | When `fo.StopPrice == newPrice`, no cancel is fired (DW-B147 guard) |
| T_B137_05 | Both sync methods | When `rawPrice != newPrice` (real drag), cancel+resubmit proceeds through both methods |
| T_B137_06 | `OrderPassesBracketGateTestable` | signalName="", leaderName="Stop3", isStop=true, order.Name="Stop3", order.FromEntrySignal=null → returns **true** (ATM path taken; DW-B150 direct fix validation) |
| T_B137_07 | Pre-sweep via stub | `SyncAtmFollowerBracket` (via `CancelExistingPttStpDrag`) cancels a Working `PTT-STP-Drag` before placing new one (DW-B151) |
| T_B137_08 | Pre-sweep via stub | `SyncAtmFollowerBracket` (via `CancelExistingPttStpDrag`) cancels an Accepted `PTT-STP-Drag` before placing new one (DW-B151) |
| T_B137_09 | `OrderPassesBracketGateTestable` | signalName=null, leaderName="Stop3", isStop=true, order.Name="Stop3" → returns **true** (null signalName ATM-path regression; unchanged behavior) |

**Test implementation notes**:
- T_B137_01/02: Pure static predicate tests — no NT8 stubs needed. Direct call to `CopyEngine.IsNoPriceChangeTestable(currentPrice, newPrice)`.
- T_B137_03/04: Use existing `Account`/`Order` stub pattern from B136Tests.cs. Inject fo with matching price to trigger early return; verify no cancel call on acc stub.
- T_B137_05: Inject fo with different price; verify cancel call IS fired.
- T_B137_06: Direct call to `CopyEngine.OrderPassesBracketGateTestable(order, signalName: "", leaderName: "Stop3", isStop: true)` where order stub has `Name="Stop3"`, `FromEntrySignal=null`, `OrderType=StopMarket`, `OrderState=Working`. **FAILS on pre-B137 code (DW-B150 bug: `"" != null` → signal path → `null == ""` = false). PASSES after T3 fix (empty string → ATM path → MatchesLeaderName → true).**
- T_B137_07/08: Inject acc with one existing PTT-STP-Drag in Working/Accepted state; call SyncAtmFollowerBracket; verify the pre-sweep cancel was called (via CancelExistingPttStpDrag).
- T_B137_09: Same as T_B137_06 but with `signalName: null` — verifies null signalName still takes ATM path (regression guard for unchanged behavior).

**Test count**: 9 [Fact] tests. All ≥ 8 minimum. ✅

---

## 7-Scan Checklist (Engineer Contract)

```
SCAN-01: grep -r "lock(" src/ --include="*.cs"                    → 0 matches required
SCAN-02: grep -rn "async void " src/ --include="*.cs"             → 0 matches required
SCAN-03: git diff HEAD src/PropTraderTools/CopyEngine.cs | grep "^+" | grep "return null;"
                                                                   → 0 matches required (no new return null; in B137-added lines;
                                                                      pre-existing Order? return null at line 2629 in
                                                                      FindFollowerBracketOrder is unchanged — excluded by git diff scope)
SCAN-04: dotnet build                                              → 0 errors 0 warnings
SCAN-05: python scripts/complexity_audit.py                        → all CYC ≤ 8 (verify modified methods explicitly)
SCAN-06: dotnet test                                               → 0 Failed 0 Errors (includes all 9 new B137 tests)
SCAN-07: dotnet csharpier check src/                               → clean
```

**SCAN-05 explicit CYC targets for B137**:
- `IsNoPriceChange`: CYC=1
- `ExecutePhaseCStopReplacement`: CYC=2
- `SyncAtmFollowerTarget`: CYC=8 (AT LIMIT — must not exceed)
- `SyncAtmFollowerBracket`: CYC=6
- `CancelExistingPttStpDrag`: CYC ≤ 8 (expect 6-7)
- `OrderPassesBracketGate`: CYC=2 (unchanged)
- `MatchesLeaderName`: CYC=5 (unchanged — verify no regression)
- `FindFollowerBracketOrder` (list overload): CYC=7 (unchanged — verify no regression)

---

## Deferred Items (Carry Forward — Do NOT Implement)

The following items are logged only. No code changes for any of them in B137.

| ID | Title | Priority | Status |
|----|-------|----------|--------|
| B135-DEFER-01 | Gap B runtime gate — two simultaneous leader entries | P1 | OPEN — carry to B138+ |
| B135-DEFER-02 | Stale orders multi-session match in FindFollowerBracketOrder | P2 | OPEN — carry to future |
| DW-B134-OCO-OBS A/B/C/D | Partial-fill race conditions (OBS-A/B/C/D) | P1 | OPEN — SIM data required |
| DW-B141 | Phase C re-confirmation — pending SIM Test A | P1 | OPEN — SIM Test A not yet run |
| DW-B138 | Follower stop drag confirmed — pending SIM Test B | P1 | OPEN per backlog |

These items require either SIM data, Director confirmation, or a future dedicated block. No code action in B137.

---

## JS Rules Applied

| Rule | Category | Application |
|------|----------|-------------|
| JS-001 | Type Safety | No throw in hot paths. All new code uses try/catch with StatusUpdate?.Invoke (no rethrow). IsNoPriceChange returns bool. `string.IsNullOrEmpty` does not throw. CancelExistingPttStpDrag uses try/catch, no rethrow. |
| JS-002 | Type Safety | No return null. All new methods return bool or void. Existing Order? return null contract at line 2629 unchanged and not added to by B137. |
| JS-021 | Concurrency | No lock() anywhere. Static predicates have zero shared state. Instance methods called on NT8 background thread, no synchronization needed. CancelExistingPttStpDrag uses acc.Orders.ToList() snapshot — no lock needed. |
| JS-023 | Concurrency | No ConcurrentQueue changes. Actor pattern not applicable to synchronous NT8 callbacks. |
| JS-036 | Performance | No heap allocation in hot path. IsNoPriceChange is stack-only. `string.IsNullOrEmpty` is a BCL intrinsic, no allocation. ExecutePhaseCStopReplacement delegates to existing methods. |
| JS-066 | Code Review | CYC ≤ 8 for all methods. CancelExistingPttStpDrag extraction ensures SyncAtmFollowerBracket stays at CYC=6. CancelExistingPttStpDrag itself CYC=6-7. All ≤ 8. ✅ |
| ASCII-only | Code Review | All new identifiers and string literals are ASCII. "PTT-STP-Drag", "STP pre-cancel error", "string.IsNullOrEmpty", "CancelExistingPttStpDrag" — all ASCII. |
| DateTime.UtcNow | Code Review | No time logic added. |

---

## Output Summary

**Plan status**: REVIEW_PENDING
**Violations fixed**:
- V1 (P1): Root cause of DW-B150 confirmed with specific method names, conditions, and line references. No hedged language.
- V2 (P0): T3 fix moved from MatchesLeaderName (dead code) to OrderPassesBracketGate branch (1) condition change. Fix is reachable, provably corrects the fo=NULL scenario for Sim103/Sim104.
- V3 (P1): SCAN-03 updated to `git diff`-scoped command targeting only B137-added lines. Pre-existing `return null;` at L2629 explicitly acknowledged as excluded.
- V4 (P1): T4 Block A-Prime inline design replaced with mandatory extraction to `CancelExistingPttStpDrag(Account acc, Order fo)`. SyncAtmFollowerBracket T4 change is a single method call (no new branch) → CYC=6. CancelExistingPttStpDrag CYC=6-7 ≤ 8. No CYC violation.

**Files to be modified** (by ptt-engineer only, not this architect):
- `src/PropTraderTools/CopyEngine.cs` — 4 targeted changes (T1 extraction, T2 guard, T3 condition, T4 extraction + call)
- `tests/PropTraderTools.Tests/CopyEngineB137Tests.cs` — NEW file, 9 xUnit tests

**Return value upon review pass**: PLAN_COMPLETE
