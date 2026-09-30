# BWAVE-REFACTOR LaneC -- Tickets

**Epic**: BWAVE-REFACTOR LaneC
**Phase**: 3 (Ticket Generation)
**Status**: TICKETS_COMPLETE
**Date**: 2026-09-06
**Architect**: ptt-architect
**Workspace**: `C:\WSGTA\ptt-lane-c\` (git worktree, branch `bwave-refactor-lane-c`)
**Brain dir**: `docs/brain/BWAVE-REFACTOR/LaneC/`
**Plan source**: `02-architecture-plan.md` (REVIEW_PASS confirmed -- FindLeaderPosition excluded per §3.1 JS-002 compliance note; position-finding foreach stays inline in Execute)

---

## Ticket C-1: CCN Reduction -- PttQuickExit + PttGlobalQuickExit + PttBreakEven

---

### 1. Ticket ID and Title

**C-1**: CCN reduction in `PttQuickExit.cs`, `PttGlobalQuickExit.cs`, `PttBreakEven.cs`

---

### 2. Spec Req IDs

CCN violations addressed by this ticket:

| Method | File | Lizard CCN (before) | Target CCN (after) |
|--------|------|---------------------|--------------------|
| `Execute(Account, Instrument, int, List<...>, bool, double, int)` | `PttQuickExit.cs` | 32 | <=8 |
| `SnapshotTargetOrders` | `PttGlobalQuickExit.cs` | 20 | <=8 |
| `WaitForPttBeCancelled` | `PttGlobalQuickExit.cs` | 10 | <=8 |
| `Execute()` | `PttGlobalQuickExit.cs` | 9 | <=8 |
| `CancelPttBeOrders` | `PttGlobalQuickExit.cs` | 9 | <=8 |
| `CancelStaleBracketsLocal` | `PttBreakEven.cs` | 16 | <=8 |
| `SubmitBeTargetsLocal` | `PttBreakEven.cs` | 15 | <=8 |
| `SnapshotTargetsLocal` | `PttBreakEven.cs` | 13 | <=8 |
| `IsPttQxTarget` | `PttBreakEven.cs` | 12 | <=8 |
| `SubmitBeStopLocal` | `PttBreakEven.cs` | 9 | <=8 |

---

### 3. Files Touched

**Modify** (extraction only -- no signature changes, no behavior changes):
- `src/PropTraderTools/Features/PttQuickExit.cs`
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
- `src/PropTraderTools/Features/PttBreakEven.cs`

**Create** (new test file -- all C-1 structural tests go here):
- `src/PropTraderTools/Tests/BwaveRefactorLaneCTests.cs`

**Do NOT touch**:
- `CopyEngine.cs`, `TradeCopierPanel.cs`, `TradeCopierWindow.cs`
- Any Ticket C-2 file (`PttBreakEvenSwap.cs`, `PttTrim.cs`, `PttFlatten.cs`, `PttCancel.cs`)

---

### 4. Method Signatures

All **new private helpers** to add. No existing `public`/`internal` signature changes.

#### 4.1 `PttQuickExit.cs` -- 3 new helpers

```csharp
/// <summary>
/// Submit StopMarket order for one OCO pair.
/// Extracted from PttQuickExit.Execute loop body (lines 134-167).
/// CYC=2: (1) snapshotStop>0 guard, (2) stopOrd null check.
/// JS-001: no throw -- catch logs. JS-002: void. JS-021: no lock. ASCII-only.
/// NT8-049: StopMarket arg6=0, arg7=snapshotStop (NEVER swap).
/// NT8-007: arg11=(CustomOrder)null. NT8-013: DateTime.MaxValue. NT8-014: stopName starts PTT-.
/// </summary>
private void SubmitStopOrder(
    Account acc,
    Instrument instr,
    bool isLong,
    int qty,
    double snapshotStop,
    string ocoId,
    string stopName
)

/// <summary>
/// Submit Limit target order for one OCO pair.
/// Extracted from PttQuickExit.Execute loop body (lines 168-199).
/// CYC=2: (1) try/catch -- only 1 branch (null check on tNOrd). (2) tNOrd null check.
/// JS-001: no throw -- catch logs. JS-002: void. JS-021: no lock. ASCII-only.
/// NT8-049: Limit arg6=tNPrice, arg7=0 (NEVER swap).
/// NT8-007: arg11=(CustomOrder)null. NT8-013: DateTime.MaxValue. NT8-014: targetName starts PTT-.
/// </summary>
private void SubmitTargetOrder(
    Account acc,
    Instrument instr,
    bool isLong,
    int qty,
    double tNPrice,
    string ocoId,
    string targetName
)

/// <summary>
/// Compute per-iteration OCO pair params and dispatch SubmitStopOrder + SubmitTargetOrder.
/// Extracted from PttQuickExit.Execute for-loop body (lines 111-199, minus headers).
/// Returns the ocoId string for this pair (never null -- Guid fallback satisfies JS-002).
/// CYC=5 (see CYC Pre-Check §6).
/// JS-002: returns string, Guid fallback ensures never null.
/// JS-001: no throw. JS-021: no lock. ASCII-only.
/// </summary>
private string SubmitQxOcoPair(
    Account acc,
    Instrument instr,
    bool isLong,
    double entryPx,
    double snapshotStop,
    double tick,
    int t1Ticks,
    int i,
    int targetCount,
    System.Collections.Generic.List<(double Price, int Qty)> targets
)
```

#### 4.2 `PttGlobalQuickExit.cs` -- 3 new helpers

```csharp
/// <summary>
/// Determine if an order is a valid target for the given instrument.
/// Extracted from SnapshotTargetOrders inner filter block (lines 449-470).
/// CYC=3: (1) stateOk (||), (2) instrOk, (3) name-prefix checks.
/// JS-002: returns bool. JS-021: no lock. ASCII-only.
/// </summary>
private static bool IsTargetOrder(
    NinjaTrader.Cbi.Order o,
    NinjaTrader.Cbi.Instrument instr
)

/// <summary>
/// Deduplicate (Price, Qty) list by Price, keeping highest Qty per price level.
/// Extracted from SnapshotTargetOrders dedup dictionary loop (lines 482-493).
/// CYC=2: (1) foreach, (2) TryGetValue branch.
/// JS-002: returns non-null List. JS-021: no lock. NT8-006: no LINQ. ASCII-only.
/// </summary>
private static System.Collections.Generic.List<(double Price, int Qty)> DeduplicateByPrice(
    System.Collections.Generic.List<(double Price, int Qty)> targets
)

/// <summary>
/// Build and append DW-B115-DIAG log string for leader targets.
/// Extracted from PttGlobalQuickExit.Execute DIAG block (lines 83-100).
/// CYC=2: (1) for-loop, (2) always-execute append (no branch inside loop body).
/// JS-002: void. JS-021: no lock. ASCII-only.
/// </summary>
private static void LogLeaderDiag(
    NinjaTrader.Cbi.Account acc,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    int posQty
)
```

**Reuse note for `CancelPttBeOrders` and `WaitForPttBeCancelled`**: These methods will use a new helper `IsNonTerminalForInstr` (defined once in the file, shared by both -- see §4.2 detail below).

```csharp
/// <summary>
/// Returns true if order o is a non-terminal PTT-BE order for the given instrument.
/// Used by both WaitForPttBeCancelled (collect count) and CancelPttBeOrders (filter).
/// CYC=4: (1) o null check, (2) instrOk, (3) IsPttBeOrder, (4) IsNonTerminalPttBeState.
/// JS-002: returns bool. JS-021: no lock. ASCII-only.
/// </summary>
private static bool IsNonTerminalForInstr(
    NinjaTrader.Cbi.Order o,
    NinjaTrader.Cbi.Instrument instr
)
```

#### 4.3 `PttBreakEven.cs` -- 7 new helpers

```csharp
/// <summary>
/// Returns true if order state is cancellable (Working|Initialized|Submitted|Accepted|TriggerPending).
/// Extracted from CancelStaleBracketsLocal stateOk expression.
/// CYC=5: five || terms in the boolean expression (one per state).
/// JS-002: returns bool. JS-021: no lock. ASCII-only.
/// </summary>
private static bool IsCancellableState(OrderState s)

/// <summary>
/// Returns true if order o should be cancelled: stateOk, instrOk, and not a PTT-BE order.
/// Extracted from CancelStaleBracketsLocal compound filter.
/// CYC=3: (1) IsCancellableState call [0 branches -- delegate], (2) instrOk, (3) notBe.
/// JS-002: returns bool. JS-021: no lock. ASCII-only.
/// </summary>
private static bool IsStaleOrder(Order o, Instrument instr)

/// <summary>
/// Submit single bare StopMarket stop for 0-targets case in SubmitBeTargetsLocal.
/// Extracted from the targets.Count==0 branch (lines 487-521).
/// Includes FindPositionLocal call for qty lookup (JS-002: no null-returning helper extracted).
/// CYC=3: (1) barePos null|qty guard, (2) try/catch (3) bareStop null check.
/// JS-001: no throw -- try/catch. JS-002: void. JS-021: no lock. ASCII-only.
/// NT8-049: arg6=0, arg7=bePrice. NT8-007: (CustomOrder)null. NT8-013: DateTime.MaxValue.
/// NT8-014: "PTT-BE-Stop". Note: FindPositionLocal called internally (pre-existing null-return
/// helper in this file -- caller has null guard, no new null-returning helper introduced).
/// </summary>
private static void SubmitBareStop(
    Account acc,
    Instrument instr,
    OrderAction stopDirection,
    double bePrice
)

/// <summary>
/// Submit one OCO stop+target pair in SubmitBeTargetsLocal for-loop body.
/// Extracted from per-pair block (lines 530-628).
/// CYC=3: (1) stop-ord null check, (2) target-ord null check, (3) try/catch (counted once for pair).
/// JS-001: no throw -- try/catch per order. JS-002: void. JS-021: no lock. ASCII-only.
/// NT8-049: stop arg6=0, arg7=bePrice; target arg6=t.Price, arg7=0.
/// NT8-007: (NinjaTrader.Cbi.CustomOrder)null. NT8-013: DateTime.MaxValue. NT8-014: PTT-BE- prefix.
/// </summary>
private static void SubmitBePair(
    Account acc,
    Instrument instr,
    OrderAction stopDirection,
    double bePrice,
    string ocoId_i,
    int i,
    (double Price, int Qty, OrderAction Action) t
)

/// <summary>
/// Returns true if order state is eligible for ATM target snapshot.
/// Extracted from SnapshotTargetsLocal stateOk expression.
/// CYC=5: five || terms (Working|Accepted|Submitted|Initialized|TriggerPending).
/// JS-002: returns bool. JS-021: no lock. ASCII-only.
/// </summary>
private static bool IsSnapshotEligibleState(OrderState s)

/// <summary>
/// Returns true if acc and instr are both non-null (input validity check).
/// Extracted from SubmitBeStopLocal null guard to remove the || operator from CCN count.
/// CYC=1: simple &&-based positive check (no || branches for lizard to count).
/// JS-002: returns bool. JS-021: no lock. ASCII-only.
/// </summary>
private static bool IsInvalidInput(Account acc, Instrument instr)

/// <summary>
/// Returns acc.Name or "null" when acc is null -- eliminates inline ternary from catch blocks.
/// Extracted to reduce CCN in SubmitBeStopLocal catch block.
/// CYC=1: single ternary. JS-002: returns string (never null -- "null" literal as sentinel).
/// JS-021: no lock. ASCII-only.
/// </summary>
private static string SafeName(Account acc)
```

---

### 5. Precise Change Description

#### 5.1 `PttQuickExit::Execute` (CCN 32 -> 8)

**What moves:**
- Lines 134-167 (the `if (snapshotStop > 0)` stop try/catch block) --> `SubmitStopOrder`.
- Lines 168-199 (the target try/catch block) --> `SubmitTargetOrder`.
- Lines 111-199 (entire for-loop body: tNTicks, tNQty, ocoId assignment, stopName/targetName, `if (i==0)` firstOcoId, calls to SubmitStopOrder + SubmitTargetOrder) --> `SubmitQxOcoPair`.

**Execute body after extraction (pseudocode):**
```
// pos-find foreach stays inline (JS-002: cannot extract null-returning helper)
Position pos = null;
foreach (Position p in leader.Positions) if (p.Instrument==instr) { pos=p; break; }
if (pos==null || pos.Quantity==0) return;
if (skipIfFollower && CopyEngine.Instance?.IsFollowerAccount(leader)==true) return;
double snapshotStop = ResolveStop(SnapshotStopPrice(leader, instr), leaderStop);
// ... log ...
var snapshot = CopyEngine.BuildQxSnapshot(leader, instr);
// ... log ...
CopyEngine.Instance?.CancelQxBrackets(leader, instr, snapshot);
bool isLong = pos.MarketPosition==MarketPosition.Long;
double entryPx = pos.AveragePrice;
double tick = instr.MasterInstrument?.TickSize ?? 0.25;
int targetCount = ResolveTargetCount(targets, leaderTargetCount);
string firstOcoId = string.Empty;
for (int i = 0; i < targetCount; i++)
    firstOcoId = SubmitQxOcoPair(leader, instr, isLong, entryPx, snapshotStop, tick, t1Ticks, i, targetCount, targets);
// ... PttBus.RaiseQuickExit ...
```

**SubmitQxOcoPair body (pseudocode):**
```
int tNTicks = t1Ticks * (i + 1);
double rawTN = isLong ? entryPx + tNTicks*tick : entryPx - tNTicks*tick;
double tNPrice = Math.Round(rawTN/tick)*tick;
int tNQty = (targets!=null && i<targets.Count) ? targets[i].Qty : CalcTNQty(pos.Quantity, targetCount, i);
// NOTE: pos.Quantity not in scope -- pass targetCount and use existing CalcTNQty pattern.
// Engineer note: SubmitQxOcoPair must accept posQty OR derive qty using targets+targetCount.
// PREFERRED: tNQty = (targets!=null && i<targets.Count) ? targets[i].Qty : CalcTNQty(posQty, targetCount, i)
// So signature includes int posQty as parameter.
if (tNQty <= 0) return string.Empty;  // skip -- first pair still tracked via outer firstOcoId logic
string ocoId_i = CopyEngine.Instance?.NextQxOcoId() ?? ("PTT-QX-"+Guid.NewGuid().ToString("N").Substring(0,8));
string stopName = i==0 ? "PTT-QX-Stop" : "PTT-QX-Stop"+(i+1);
string targetName = "PTT-QX-T"+(i+1);
SubmitStopOrder(acc, instr, isLong, tNQty, snapshotStop, ocoId_i, stopName);
SubmitTargetOrder(acc, instr, isLong, tNQty, tNPrice, ocoId_i, targetName);
return ocoId_i;
```

**CORRECTION for Execute signature of SubmitQxOcoPair**: The helper needs `posQty` to compute CalcTNQty. Add `int posQty` as a parameter. Final signature:

```csharp
private string SubmitQxOcoPair(
    Account acc,
    Instrument instr,
    bool isLong,
    double entryPx,
    double snapshotStop,
    double tick,
    int t1Ticks,
    int i,
    int targetCount,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    int posQty
)
```

**Execute outer loop change**: The loop collects `firstOcoId` by checking `i==0` now moved inside `SubmitQxOcoPair` (which returns the ocoId string). In Execute, store the return value for i==0 only:
```csharp
string firstOcoId = string.Empty;
for (int i = 0; i < targetCount; i++)
{
    string pairId = SubmitQxOcoPair(leader, instr, isLong, entryPx, snapshotStop, tick, t1Ticks, i, targetCount, targets, pos.Quantity);
    if (i == 0) firstOcoId = pairId;
}
```
**Wait** -- this re-introduces `if (i==0)` back into Execute (+1 CCN). To keep Execute at CCN=8, the `if(i==0)` must remain extracted. Re-check Execute CCN:

- base=1, pos-find foreach=+1, p.Instrument==instr if=+1, pos==null||pos.Quantity==0 (||)=+1 +1=+2, skipIfFollower && ... (&&)=+1 +1=+2, for-loop=+1, if(i==0)firstOcoId=+1 = 1+1+1+2+2+1+1 = **9. Over 8.**

**Final resolution**: Move `if (i==0) firstOcoId = pairId` INSIDE `SubmitQxOcoPair` so Execute loop becomes just `SubmitQxOcoPair(...)` with no per-iteration branch in Execute. Achieve by passing `firstOcoId` by ref or by having SubmitQxOcoPair return only the ocoId and Execute uses the return only for the bus event (which only needs T1 ocoId). Since Execute only uses `firstOcoId` for `PttBus.RaiseQuickExit`, engineer should:

```csharp
string firstOcoId = string.Empty;
for (int i = 0; i < targetCount; i++)
{
    string id = SubmitQxOcoPair(leader, instr, isLong, entryPx, snapshotStop, tick, t1Ticks, i, targetCount, targets, pos.Quantity);
    if (i == 0 && id.Length > 0) firstOcoId = id;  // still +1 branch
}
```

This adds 1 CCN back. The only way to reach CCN=8 without the `if(i==0)` branch in Execute is to have SubmitQxOcoPair always return the first iteration's ocoId (or track it across iterations). The simplest approach: let SubmitQxOcoPair return the ocoId it generated (already in the plan), and in Execute, initialize `firstOcoId` before the loop and only overwrite it if still empty:

```csharp
string firstOcoId = string.Empty;
for (int i = 0; i < targetCount; i++)
{
    string id = SubmitQxOcoPair(...);
    if (firstOcoId.Length == 0) firstOcoId = id;  // (branch i, +1 CCN)
}
```

The `if (firstOcoId.Length == 0)` branch is 1 CCN point. Execute total = 1+1+1+2+2+1+1 = 9. Still over.

**Final definitive approach (matches plan §3.1 exactly)**: The for-loop-level `if(i==0)` guard is absorbed INSIDE `SubmitQxOcoPair` by having that helper directly manage a passed-by-ref `firstOcoId` parameter OR by removing the `if(i==0)` from Execute entirely and having Execute read the return only once after the loop using the last non-empty value or by using a dedicated capture:

```csharp
// Execute outer: no if(i==0) branch at all
for (int i = 0; i < targetCount; i++)
    SubmitQxOcoPair(leader, instr, isLong, entryPx, snapshotStop, tick, t1Ticks, i, targetCount, targets, pos.Quantity, ref firstOcoId);
```

Add `ref string firstOcoId` as the last parameter to `SubmitQxOcoPair`. Inside the helper: `if (i==0) firstOcoId = ocoId_i;`. This moves the `if(i==0)` branch INTO SubmitQxOcoPair (+1 to its CCN, from 5 to 6), and Execute has NO per-iteration branch, keeping Execute CCN=8.

**FINAL SIGNATURES (authoritative):**

```csharp
// SubmitQxOcoPair with ref firstOcoId
private void SubmitQxOcoPair(
    Account acc,
    Instrument instr,
    bool isLong,
    double entryPx,
    double snapshotStop,
    double tick,
    int t1Ticks,
    int i,
    int targetCount,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    int posQty,
    ref string firstOcoId
)
```

This is now `void` (not `string` return). CYC recalculated at §6 CYC Pre-Check.

#### 5.2 `PttGlobalQuickExit::SnapshotTargetOrders` (CCN 20 -> <=8)

**What moves:**
- The `stateOk`, `instrOk`, `isNative`, `isPtt` filter block (lines 449-470) --> `IsTargetOrder`.
- The dedup dictionary foreach (lines 482-493) --> `DeduplicateByPrice`.

**After extraction body (pseudocode):**
```
var nativeTargets = ...; var pttTargets = ...;
if (acc==null || instr==null) return nativeTargets;
foreach (Order o in acc.Orders)
{
    if (o==null) continue;
    if (!IsTargetOrder(o, instr)) continue;
    bool isNative = o.Name.StartsWith("Target",...) && o.Name.Length>6 && char.IsDigit(o.Name[6]);
    bool isPtt = (o.Name.StartsWith("PTT-QX-T",...) ...) || o.Name.StartsWith("PTT-BE-Target-",...);
    if (isNative) nativeTargets.Add(...); else if (isPtt) pttTargets.Add(...);
}
if (nativeTargets.Count==0) return pttTargets;
return DeduplicateByPrice(nativeTargets);
```

**Note**: `isNative` and `isPtt` classification branches remain in the caller (2 branches). `IsTargetOrder` handles the state+instr+Limit filter (early-out guard).

#### 5.3 `PttGlobalQuickExit::Execute()` (CCN 9 -> <=8)

**What moves:**
- Lines 82-100 (the `_sb` StringBuilder + for-loop DIAG block) --> `LogLeaderDiag(acc, targets, pos.Quantity)`.

**After extraction**: Execute body removes 1 for-loop branch. CCN: 9 - 1 = 8 (the DIAG for-loop was the only excess point).

#### 5.4 `PttGlobalQuickExit::WaitForPttBeCancelled` (CCN 10 -> <=8) and `CancelPttBeOrders` (CCN 9 -> <=8)

**What moves:**
- The compound `o==null || instrOk || IsPttBeOrder || IsNonTerminalPttBeState` check in BOTH methods --> `IsNonTerminalForInstr(o, instr)`.

`WaitForPttBeCancelled` after extraction: guard(1) + while(1) + foreach(1) + IsNonTerminalForInstr branch(1) + nonTerminal==0 check(1) = base(1)+5 = **CCN=6**.

`CancelPttBeOrders` after extraction: null-guard(||)(1) + foreach(1) + IsNonTerminalForInstr branch(1) + count==0 return(1) + acc.Cancel(0) = base(1)+4 = **CCN=5**.

#### 5.5 `PttBreakEven::CancelStaleBracketsLocal` (CCN 16 -> <=8)

**What moves:**
- `stateOk` 5-term expression --> `IsCancellableState(o.OrderState)`.
- The combined `stateOk && instrOk && notBe` compound filter --> `IsStaleOrder(o, instr)` (calls `IsCancellableState` internally).

**After extraction**: null-guard(1) + foreach(1) + IsStaleOrder branch(1) + count==0 return(1) + try/catch(1) = base(1)+5 = **CCN=6**.

#### 5.6 `PttBreakEven::SubmitBeTargetsLocal` (CCN 15 -> <=8)

**What moves:**
- Lines 487-521 (the `targets.Count==0` block) --> `SubmitBareStop(acc, instr, stopDirection, bePrice)`.
- Lines 530-628 (per-pair block inside the for-loop) --> `SubmitBePair(acc, instr, stopDirection, bePrice, ocoId_i, i, t)`.

**After extraction**: null-guards(2) + stopDirection assign(0) + targets.Count==0 branch(1) + SubmitBareStop(0) + return(0) + for-loop(1) + SubmitBePair(0) = base(1)+3 = **CCN=4**.

#### 5.7 `PttBreakEven::SnapshotTargetsLocal` (CCN 13 -> <=8)

**What moves:**
- `stateOk` 5-term expression --> `IsSnapshotEligibleState(o.OrderState)`.

**After extraction**: null-guard(1) + foreach(1) + o-null-continue(0) + compound filter now has IsSnapshotEligibleState(0) + instrOk(1) + name-filter calls(1) = base(1)+4 = **CCN=5**.

#### 5.8 `PttBreakEven::IsPttQxTarget` (CCN 12 -> <=8)

**In-place simplification** (no new helper, no behavior change):

Replace the 8-char `&&` chain (`name[0]=='P' && name[1]=='T' && ...`) with `StartsWith` + suffix checks:

```csharp
private static bool IsPttQxTarget(string name)
{
    if (name == null || name.Length != 9)       // (1)(||)=2
        return false;
    return name.StartsWith("PTT-QX-T", StringComparison.Ordinal)  // 0 new CCN (method call)
        && name[8] >= '1' && name[8] <= '3';    // (&&)(>=)(<=)=3? lizard counts && and comparisons
}
```

Lizard counts: `||` in guard = 1, `&&` after return = 1, `>=` = 1, `<=` = 1 = base(1)+4 = **CCN=5**.

The 8 `&&` char-index checks are replaced by one `StartsWith` call (0 CCN) + 2 comparisons. This is a pure in-place rewrite: logic is identical, no new helper needed, no `[Fact]` test required for this change per plan §3.9.

#### 5.9 `PttBreakEven::SubmitBeStopLocal` (CCN 9 -> <=8)

**What moves:**
- `acc == null || instr == null` null guard --> `IsInvalidInput(acc, instr)` call (moves `||` branch out of method).
- `(acc != null ? acc.Name : "null")` inline ternary in catch --> `SafeName(acc)` call.

**After extraction**: IsInvalidInput call(0) + pos-guard(1) + direction ternary(1) + try/catch(1) + order-null check(1) + SafeName call(0) = base(1)+4+IsInvalidInput's own branch = **CCN=6** (lizard sees no `||` in this method body, IsInvalidInput encapsulates it, SafeName removes the ternary from catch).

---

### 6. CYC Pre-Check

| Method | File | CCN Before | CCN After | PASS? |
|--------|------|-----------|-----------|-------|
| `Execute(Account,Instrument,int,List,bool,double,int)` | PttQuickExit | 32 | 8 | YES |
| `SubmitStopOrder` | PttQuickExit | NEW | 2 | YES |
| `SubmitTargetOrder` | PttQuickExit | NEW | 2 | YES |
| `SubmitQxOcoPair` | PttQuickExit | NEW | 6 | YES |
| `SnapshotTargetOrders` | PttGlobalQuickExit | 20 | 6 | YES |
| `IsTargetOrder` | PttGlobalQuickExit | NEW | 3 | YES |
| `DeduplicateByPrice` | PttGlobalQuickExit | NEW | 2 | YES |
| `Execute()` | PttGlobalQuickExit | 9 | 8 | YES |
| `LogLeaderDiag` | PttGlobalQuickExit | NEW | 2 | YES |
| `WaitForPttBeCancelled` | PttGlobalQuickExit | 10 | 6 | YES |
| `IsNonTerminalForInstr` | PttGlobalQuickExit | NEW | 4 | YES |
| `CancelPttBeOrders` | PttGlobalQuickExit | 9 | 5 | YES |
| `CancelStaleBracketsLocal` | PttBreakEven | 16 | 6 | YES |
| `IsCancellableState` | PttBreakEven | NEW | 5 | YES |
| `IsStaleOrder` | PttBreakEven | NEW | 3 | YES |
| `SubmitBeTargetsLocal` | PttBreakEven | 15 | 4 | YES |
| `SubmitBareStop` | PttBreakEven | NEW | 3 | YES |
| `SubmitBePair` | PttBreakEven | NEW | 3 | YES |
| `SnapshotTargetsLocal` | PttBreakEven | 13 | 5 | YES |
| `IsSnapshotEligibleState` | PttBreakEven | NEW | 5 | YES |
| `IsPttQxTarget` | PttBreakEven | 12 | 5 | YES (in-place rewrite) |
| `SubmitBeStopLocal` | PttBreakEven | 9 | 6 | YES |
| `IsInvalidInput` | PttBreakEven | NEW | 1 | YES |
| `SafeName` | PttBreakEven | NEW | 1 | YES |

**SubmitQxOcoPair CCN detail** (void, ref firstOcoId version):
- base=1
- tNQty ternary `(targets!=null && i<targets.Count)?...` = +1 (&&) +1 (?) = +2
- `if (tNQty <= 0)` = +1
- `if (i == 0) firstOcoId = ocoId_i` = +1 (absorbed here from Execute)
- SubmitStopOrder call = +0
- SubmitTargetOrder call = +0
- Total = 1+2+1+1 = **6. PASS.**

---

### 7. JS Rules

| Rule | Applies to | How satisfied |
|------|-----------|---------------|
| **JS-002** (P0 -- no return null) | `SubmitQxOcoPair` (now void, no return value) | void return -- no null concern. All other new helpers return `void`, `bool`, or value types. `DeduplicateByPrice` returns an initialized `List` (never null). |
| **JS-002** | `IsInvalidInput` | returns `bool`. |
| **JS-002** | `SafeName` | returns `string` -- never null (literal `"null"` as sentinel). |
| **JS-002** | `DeduplicateByPrice` | returns initialized `List<(double Price, int Qty)>` (never null). |
| **JS-021** (P0 -- no lock) | All new helpers | No `lock()` anywhere. ConcurrentQueue/Interlocked owned by CopyEngine, not by these helpers. |
| **JS-001** (P0 -- no throw) | `SubmitStopOrder`, `SubmitTargetOrder`, `SubmitBareStop`, `SubmitBePair` | All submit helpers use try/catch. No `throw` statements. |
| **JS-033** (P0 -- no async void) | All new helpers | All helpers are synchronous. No `async` keyword. |
| **JS-006** (no LINQ) | `IsTargetOrder`, `DeduplicateByPrice`, `IsNonTerminalForInstr`, `IsCancellableState`, `IsStaleOrder`, `IsSnapshotEligibleState` | Pure boolean logic. No `.Where()`, `.Select()`, `.Any()`. |
| **ASCII-only** | All new names | `SubmitStopOrder`, `SubmitTargetOrder`, `SubmitQxOcoPair`, `IsTargetOrder`, `DeduplicateByPrice`, `LogLeaderDiag`, `IsNonTerminalForInstr`, `IsCancellableState`, `IsStaleOrder`, `SubmitBareStop`, `SubmitBePair`, `IsSnapshotEligibleState`, `IsInvalidInput`, `SafeName` -- all ASCII-only. |

**NT8 constraints in extracted helpers** (preserved exactly):
- NT8-049: `SubmitStopOrder` uses `0` for arg6 (limitPrice) and `snapshotStop` for arg7 (stopPrice). `SubmitTargetOrder` uses `tNPrice` for arg6 and `0` for arg7. Never swapped.
- NT8-049: `SubmitBareStop` uses `0` arg6 and `bePrice` arg7. `SubmitBePair` stop uses `0` arg6 and `bePrice` arg7; target uses `t.Price` arg6 and `0` arg7.
- NT8-007: All CreateOrder calls use `(NinjaTrader.Cbi.CustomOrder)null` for arg11.
- NT8-013: All CreateOrder calls use `DateTime.MaxValue` for arg10.
- NT8-014: All signal names in extracted helpers start with `"PTT-"`.

---

### 8. Acceptance Criteria

1. `dotnet build C:\WSGTA\ptt-lane-c\src\PropTraderTools\PropTraderTools.csproj` -- **0 errors, 0 warnings**.
2. All 7 scans pass (see §11 SCAN checklist).
3. Lizard CCN scan shows **0 rows** with CCN > 8 in `Features/*.cs` methods.
4. `powershell -File scripts\ptt-sync-and-verify.ps1` -- **0 MISMATCH lines** (all files in sync).
5. F5 in NinjaTrader 8 -- **green compile, no script errors**.
6. `BwaveRefactorLaneCTests.cs` compiles and all `[Fact]` tests PASS (1 per new helper).
7. No public or internal method signatures changed from original source.
8. No logic deleted -- every branch from original source preserved in extracted helpers.
9. No `lock()`, `async void`, `return null`, or non-ASCII identifiers introduced.

---

### 9. NT8 Sync Required

**YES**

```powershell
powershell -File C:\WSGTA\ptt-lane-c\scripts\ptt-sync-and-verify.ps1
```

Verify output: 0 MISMATCH lines. Then press **F5** in NinjaTrader 8.

---

### 10. F5 Required

**YES** -- press F5 in NinjaTrader 8 after sync to recompile the AddOn and verify no NinjaScript compilation errors.

---

### 11. 7-Scan Checklist

Run all scans from `C:\WSGTA\ptt-lane-c\` after completing implementation:

| Scan | Command | Expected Result |
|------|---------|-----------------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | **0 results** |
| SCAN-02 | `Get-Content src/PropTraderTools/*.cs \| Where-Object {$_ -match '[^\x00-\x7F]'}` | **0 results** |
| SCAN-03 | `Select-String -Path src/PropTraderTools/*.cs -Pattern "FontFamily"` | **0 results** |
| SCAN-04 | `Select-String -Path src/PropTraderTools/*.cs -Pattern "#[0-9A-Fa-f]{6}"` | **0 results** |
| SCAN-05 | Verify all `CreateOrder` calls in new helpers use "PTT-" prefix in signal name arg9 | **0 violations** |
| SCAN-06 | `Select-String -Path src/PropTraderTools/*.cs -Pattern "DateTime\.Now[^U]"` | **0 results** |
| SCAN-07 (CYC) | See lizard command below | **0 rows output** |

**SCAN-07 full lizard command:**
```powershell
$files = Get-ChildItem src/PropTraderTools/ -Filter "*.cs" -Recurse |
  Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' }
lizard $files --csv 2>&1 |
  ConvertFrom-Csv -Header @("NLOC","CCN","Tokens","Params","Length","Location","MethodName","MethodLongName","StartLine","EndLine") |
  Where-Object { [int]$_.CCN -gt 8 } |
  Sort-Object { [int]$_.CCN } -Descending
```

Expected: **0 rows output**. Any row = BUILD_FAIL. Do not proceed to Ticket C-2 until this is 0 rows.

---

### 12. Test Stubs for C-1 Helpers

File: `src/PropTraderTools/Tests/BwaveRefactorLaneCTests.cs` (NEW FILE -- create in Ticket C-1)

One structural `[Fact]` per extracted helper (reflection-based -- verifies the helper exists and has the expected parameter count). Use `xUnit` ONLY.

```csharp
// src/PropTraderTools/Tests/BwaveRefactorLaneCTests.cs
// BWAVE-REFACTOR LaneC -- structural existence tests.
// 1 [Fact] per extracted helper. Reflection-based only -- no behavioral test.
// xUnit only. No NUnit. No MSTest. ASCII-only identifiers.

using System.Reflection;
using Xunit;

namespace PropTraderTools.Tests
{
    public class BwaveRefactorLaneCTests
    {
        // C-1: PttQuickExit helpers
        [Fact] public void PttQuickExit_SubmitStopOrder_Exists()
        { var m = typeof(PttQuickExit).GetMethod("SubmitStopOrder", BindingFlags.NonPublic | BindingFlags.Instance); Assert.NotNull(m); Assert.Equal(7, m.GetParameters().Length); }

        [Fact] public void PttQuickExit_SubmitTargetOrder_Exists()
        { var m = typeof(PttQuickExit).GetMethod("SubmitTargetOrder", BindingFlags.NonPublic | BindingFlags.Instance); Assert.NotNull(m); Assert.Equal(7, m.GetParameters().Length); }

        [Fact] public void PttQuickExit_SubmitQxOcoPair_Exists()
        { var m = typeof(PttQuickExit).GetMethod("SubmitQxOcoPair", BindingFlags.NonPublic | BindingFlags.Instance); Assert.NotNull(m); Assert.NotNull(m); }

        // C-1: PttGlobalQuickExit helpers
        [Fact] public void PttGlobalQuickExit_IsTargetOrder_Exists()
        { var m = typeof(PttGlobalQuickExit).GetMethod("IsTargetOrder", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(2, m.GetParameters().Length); }

        [Fact] public void PttGlobalQuickExit_DeduplicateByPrice_Exists()
        { var m = typeof(PttGlobalQuickExit).GetMethod("DeduplicateByPrice", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(1, m.GetParameters().Length); }

        [Fact] public void PttGlobalQuickExit_LogLeaderDiag_Exists()
        { var m = typeof(PttGlobalQuickExit).GetMethod("LogLeaderDiag", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(3, m.GetParameters().Length); }

        [Fact] public void PttGlobalQuickExit_IsNonTerminalForInstr_Exists()
        { var m = typeof(PttGlobalQuickExit).GetMethod("IsNonTerminalForInstr", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(2, m.GetParameters().Length); }

        // C-1: PttBreakEven helpers
        [Fact] public void PttBreakEven_IsCancellableState_Exists()
        { var m = typeof(PttBreakEven).GetMethod("IsCancellableState", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(1, m.GetParameters().Length); }

        [Fact] public void PttBreakEven_IsStaleOrder_Exists()
        { var m = typeof(PttBreakEven).GetMethod("IsStaleOrder", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(2, m.GetParameters().Length); }

        [Fact] public void PttBreakEven_SubmitBareStop_Exists()
        { var m = typeof(PttBreakEven).GetMethod("SubmitBareStop", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(4, m.GetParameters().Length); }

        [Fact] public void PttBreakEven_SubmitBePair_Exists()
        { var m = typeof(PttBreakEven).GetMethod("SubmitBePair", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(7, m.GetParameters().Length); }

        [Fact] public void PttBreakEven_IsSnapshotEligibleState_Exists()
        { var m = typeof(PttBreakEven).GetMethod("IsSnapshotEligibleState", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(1, m.GetParameters().Length); }

        [Fact] public void PttBreakEven_IsInvalidInput_Exists()
        { var m = typeof(PttBreakEven).GetMethod("IsInvalidInput", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(2, m.GetParameters().Length); }

        [Fact] public void PttBreakEven_SafeName_Exists()
        { var m = typeof(PttBreakEven).GetMethod("SafeName", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(1, m.GetParameters().Length); }
    }
}
```

---

---

## Ticket C-2: CCN Reduction -- PttBreakEvenSwap + PttTrim + PttFlatten + PttCancel

---

### 1. Ticket ID and Title

**C-2**: CCN reduction in `PttBreakEvenSwap.cs`, `PttTrim.cs`, `PttFlatten.cs`, `PttCancel.cs`

---

### 2. Spec Req IDs

CCN violations addressed by this ticket:

| Method | File | Lizard CCN (before) | Target CCN (after) |
|--------|------|---------------------|--------------------|
| `Execute` | `PttBreakEvenSwap.cs` | 15 | <=8 |
| `TrimPositionLocal` | `PttTrim.cs` | 13 | <=8 |
| `FlattenPositionLocal` | `PttFlatten.cs` | 13 | <=8 |
| `CancelWorkingEntriesLocal` | `PttCancel.cs` | 10 | <=8 |

---

### 3. Files Touched

**Modify** (extraction only -- no signature changes, no behavior changes):
- `src/PropTraderTools/Features/PttBreakEvenSwap.cs`
- `src/PropTraderTools/Features/PttTrim.cs`
- `src/PropTraderTools/Features/PttFlatten.cs`
- `src/PropTraderTools/Features/PttCancel.cs`

**Modify** (append C-2 test stubs to existing file created in Ticket C-1):
- `src/PropTraderTools/Tests/BwaveRefactorLaneCTests.cs`

**Do NOT touch**:
- `CopyEngine.cs`, `TradeCopierPanel.cs`, `TradeCopierWindow.cs`
- Any Ticket C-1 file (`PttQuickExit.cs`, `PttGlobalQuickExit.cs`, `PttBreakEven.cs`)

---

### 4. Method Signatures

All **new private/private static helpers** to add. No existing `public`/`internal` signature changes.

#### 4.1 `PttBreakEvenSwap.cs` -- 2 new helpers

```csharp
/// <summary>
/// Submit bare PTT-BE-Stop StopMarket for 0-targets path.
/// Extracted from PttBreakEvenSwap.Execute 0-targets block (lines 78-119).
/// CYC=3: (1) IsStopPriceSubmittable check, (2) try/catch, (3) bareStop null guard in submit.
/// JS-001: no throw -- try/catch. JS-002: void. JS-021: no lock. ASCII-only.
/// NT8-049: arg6=0, arg7=newStop. NT8-007: (CustomOrder)null. NT8-013: DateTime.MaxValue.
/// NT8-014: "PTT-BE-Stop" starts with PTT-.
/// </summary>
private static void SubmitBareStopSwap(
    Account acc,
    Instrument instr,
    bool isLong,
    OrderAction stopDir,
    double newStop,
    int posQty
)

/// <summary>
/// Submit one OCO stop+target pair for with-targets path.
/// Extracted from PttBreakEvenSwap.Execute for-loop body (lines 122-204).
/// CYC=3: (1) IsStopPriceSubmittable check, (2) stop try/catch, (3) target try/catch.
/// JS-001: no throw -- try/catch per order. JS-002: void. JS-021: no lock. ASCII-only.
/// NT8-049: stop arg6=0, arg7=newStop; target arg6=t.Price, arg7=0.
/// NT8-007: (NinjaTrader.Cbi.CustomOrder)null. NT8-013: DateTime.MaxValue. NT8-014: PTT-BE- prefix.
/// </summary>
private static void SubmitSwapPair(
    Account acc,
    Instrument instr,
    bool isLong,
    OrderAction stopDir,
    double newStop,
    string ocoId_i,
    int i,
    (double Price, int Qty, OrderAction Action) t
)
```

#### 4.2 `PttTrim.cs` -- 1 new helper

```csharp
/// <summary>
/// Compute order type, limit price, and stop price for trim/flatten close order.
/// Extracted from TrimPositionLocal useLimitOrder block (lines 113-136).
/// CYC=5: (1) useLimitOrder: tickSize>0 (2) &&, (3) isLong ternary in useLimitOrder,
///         (4) if(useLimitOrder) branch, (5) MarketPosition ternary for limitPrice.
/// JS-002: returns value tuple (never null). JS-001: no throw. JS-021: no lock. ASCII-only.
/// NT8-049: Limit orderType uses limitPrice in arg6, stopPrice=0 in arg7 (preserved in caller).
/// </summary>
private static (OrderType orderType, double limitPrice, double stopPrice) ResolveOrderParams(
    Position pos,
    int buffer,
    double ask,
    double bid,
    double tickSize
)
```

#### 4.3 `PttFlatten.cs` -- 1 new helper

```csharp
/// <summary>
/// Compute order type, limit price, and stop price for flatten close order.
/// Structurally identical to PttTrim.ResolveOrderParams -- same extraction pattern.
/// CYC=5: same as PttTrim.ResolveOrderParams.
/// JS-002: returns value tuple (never null). JS-001: no throw. JS-021: no lock. ASCII-only.
/// NT8-049: Limit orderType uses limitPrice in arg6, stopPrice=0 in arg7 (preserved in caller).
/// </summary>
private static (OrderType orderType, double limitPrice, double stopPrice) ResolveOrderParams(
    Position pos,
    int buffer,
    double ask,
    double bid,
    double tickSize
)
```

#### 4.4 `PttCancel.cs` -- 1 new helper

```csharp
/// <summary>
/// Returns true if order o should be added to the cancel list: non-null, correct instrument,
/// and in Working or Initialized state.
/// Extracted from CancelWorkingEntriesLocal compound filter.
/// CYC=4: (1) o null, (2) stateOk: Working||Initialized (||), (3) instrOk: o.Instrument!=null,
///         (4) instrOk: FullName comparison.
/// JS-002: returns bool. JS-021: no lock. ASCII-only.
/// </summary>
private static bool IsWorkingEntryOrder(Order o, Instrument instr)
```

---

### 5. Precise Change Description

#### 5.1 `PttBreakEvenSwap::Execute` (CCN 15 -> <=8)

**What moves:**
- Lines 78-119 (the `if (targets==null || targets.Count==0)` 0-targets block, including `IsStopPriceSubmittable` call, try/catch, and else-log) --> `SubmitBareStopSwap(acc, instr, isLong, stopDir, newStop, pos.Quantity)`.
- Lines 122-204 (per-pair body of the `for (int i=0; ...)` loop, including `IsStopPriceSubmittable` check for stop and target try/catch) --> `SubmitSwapPair(acc, instr, isLong, stopDir, newStop, ocoId_i, i, t)`.

**Execute body after extraction (pseudocode):**
```
if (acc==null || instr==null) return;                        // (1) null guard
var pos = CopyEngine.Instance.FindPositionPublic(acc, instr);
if (pos==null || pos.Quantity==0) return;                   // (2) flat guard
CopyEngine.Instance.CancelQxBrackets(acc, instr);
bool isLong = pos.MarketPosition==MarketPosition.Long;      // (3) isLong ternary
OrderAction stopDir = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
if (targets==null || targets.Count==0)                      // (4) 0-targets branch
{
    SubmitBareStopSwap(acc, instr, isLong, stopDir, newStop, pos.Quantity);
    return;
}
int seq = CopyEngine.Instance.NextBeOcoSeq();
for (int i=0; i<targets.Count; i++)                        // (5) for-loop
{
    var t = targets[i];
    string ocoId_i = "PTT-BE-"+acc.Name.Substring(0, Math.Min(8,acc.Name.Length))+"-"+seq.ToString("D7")+"-"+i;
    SubmitSwapPair(acc, instr, isLong, stopDir, newStop, ocoId_i, i, t); // (0 branches)
}
```

CCN = base(1) + null-guard(||)=1 + flat-guard(||)=1 + isLong-ternary=1 + targets-null-||=1 + targets-Count-branch=1 + for-loop=1 = **7. PASS.**

Wait -- `null guard (acc==null || instr==null)` has one `||` = +1. `flat guard (pos==null || pos.Quantity==0)` has one `||` = +1. `isLong ternary` = +1. `targets null||targets.Count==0` has `||` = +1 plus a branch = +1. for-loop = +1. base = 1. Total = 1+1+1+1+1+1+1 = **8. Exactly at limit. PASS.**

**SubmitBareStopSwap** receives `posQty` so it can pass it to `CreateOrder`. It does NOT call `FindPositionPublic` (avoids re-acquiring position reference outside Execute). This is already in scope via `pos.Quantity` passed in.

#### 5.2 `PttTrim::TrimPositionLocal` (CCN 13 -> <=8)

**What moves:**
- Lines 113-136 (the `useLimitOrder` bool assignment + the entire `if (useLimitOrder)` branch with `orderType`, `limitPrice`, `stopPrice` assignments) --> `ResolveOrderParams(pos, buffer, ask, bid, tickSize)`.

**TrimPositionLocal body after extraction:**
```
if (acc==null || instr==null || qty<=0) return;    // (1) null+qty guard (||)(||)
OrderAction direction = pos.MarketPosition==MarketPosition.Long ? ... : ...;  // (2) ternary
var (orderType, limitPrice, stopPrice) = ResolveOrderParams(pos, buffer, ask, bid, tickSize); // 0
try { ... CreateOrder(...) ... if(order!=null) acc.Submit(...) ... } catch { ... }  // (3)(4) try+null
```

CCN = base(1) + `acc==null || instr==null` (||)=1 + `|| qty<=0` (||)=1 + direction ternary=1 + try/catch=1 + order-null check=1 = **6. PASS.**

**Note**: The `(acc != null ? acc.Name : "null")` inline ternary in the EXCEPTION catch log is **1 CCN point** for lizard. If this brings the count to 7 that is still within limit. If engineer finds CCN is exactly 7 after extraction, that is PASS (<=8).

#### 5.3 `PttFlatten::FlattenPositionLocal` (CCN 13 -> <=8)

Identical structural change to TrimPositionLocal. Extract the same `useLimitOrder` block into `ResolveOrderParams`.

**FlattenPositionLocal after extraction**: CCN = base(1) + `acc==null || instr==null || pos==null` (two ||)=2 + direction ternary=1 + try/catch=1 + order-null check=1 = **6. PASS.**

#### 5.4 `PttCancel::CancelWorkingEntriesLocal` (CCN 10 -> <=8)

**What moves:**
- The compound `stateOk && instrOk` filter inside the foreach --> `IsWorkingEntryOrder(o, instr)`.

**CancelWorkingEntriesLocal after extraction:**
```
if (acc==null || instr==null) return;      // (1) (||)
var toCancel = new List<Order>();
foreach (Order o in acc.Orders)            // (2) foreach
{
    if (o==null) continue;                  // no CCN (continue = not a branch for lizard)
    if (IsWorkingEntryOrder(o, instr))     // (3) -- 0 internal branches visible here
        toCancel.Add(o);
}
if (toCancel.Count==0) return;             // (4) count guard
try { acc.Cancel(toCancel.ToArray()); ... } catch { }  // (5) try/catch
```

CCN = base(1) + `||` in null-guard=1 + foreach=1 + IsWorkingEntryOrder=1 + count==0=1 + try/catch=1 = **6. PASS.**

---

### 6. CYC Pre-Check

| Method | File | CCN Before | CCN After | PASS? |
|--------|------|-----------|-----------|-------|
| `Execute` | PttBreakEvenSwap | 15 | 8 | YES |
| `SubmitBareStopSwap` | PttBreakEvenSwap | NEW | 3 | YES |
| `SubmitSwapPair` | PttBreakEvenSwap | NEW | 3 | YES |
| `TrimPositionLocal` | PttTrim | 13 | 6 | YES |
| `ResolveOrderParams` | PttTrim | NEW | 5 | YES |
| `FlattenPositionLocal` | PttFlatten | 13 | 6 | YES |
| `ResolveOrderParams` | PttFlatten | NEW | 5 | YES |
| `CancelWorkingEntriesLocal` | PttCancel | 10 | 6 | YES |
| `IsWorkingEntryOrder` | PttCancel | NEW | 4 | YES |

**`SubmitBareStopSwap` CCN detail**:
- base=1
- `IsStopPriceSubmittable` call = 0 branches visible in this method
- but the `if (IsStopPriceSubmittable)` = +1 branch
- try/catch = +1
- bareStop null check (or Submit call -- no null guard needed since existing code submits without null check) = depends on whether engineer keeps null check
- Effectively: if(IsStopPriceSubmittable)(1) + try/catch(1) + else-log path(1) = base(1)+3 = **4.** Still PASS.

**`SubmitSwapPair` CCN detail**:
- base=1
- `if (IsStopPriceSubmittable)` = +1
- stop try/catch = +1
- target try/catch = +1
= base(1)+3 = **4.** PASS.

**`IsWorkingEntryOrder` CCN detail**:
- base=1
- `o==null` = +1 (or handled as early return)
- `stateOk: Working||Initialized` = +1
- `instrOk: o.Instrument!=null` = implied by &&
- `instrOk: FullName` = one more comparison
= base(1)+3 = **4.** PASS.

---

### 7. JS Rules

| Rule | Applies to | How satisfied |
|------|-----------|---------------|
| **JS-002** (P0 -- no return null) | `SubmitBareStopSwap`, `SubmitSwapPair`, `SubmitBePair for C2` | All are `void` -- no return value. `ResolveOrderParams` returns value tuple (never null). |
| **JS-002** | `IsWorkingEntryOrder` | returns `bool`. |
| **JS-002** | `ResolveOrderParams` (both PttTrim and PttFlatten) | returns `(OrderType, double, double)` value tuple (never null). |
| **JS-021** (P0 -- no lock) | All new helpers | No `lock()`. |
| **JS-001** (P0 -- no throw) | `SubmitBareStopSwap`, `SubmitSwapPair` | try/catch in each submit block. No `throw`. |
| **JS-033** (P0 -- no async void) | All new helpers | Synchronous only. |
| **ASCII-only** | All new names | `SubmitBareStopSwap`, `SubmitSwapPair`, `ResolveOrderParams`, `IsWorkingEntryOrder` -- all ASCII-only. |

**NT8 constraints in extracted helpers** (preserved exactly):
- NT8-049: `SubmitBareStopSwap` passes `0` arg6, `newStop` arg7. `SubmitSwapPair` stop uses `0` arg6 `newStop` arg7; target uses `t.Price` arg6 `0` arg7.
- NT8-007: All `CreateOrder` calls use `(NinjaTrader.Cbi.CustomOrder)null` for arg11.
- NT8-013: All `CreateOrder` calls use `DateTime.MaxValue`.
- NT8-014: Signal names "PTT-BE-Stop", "PTT-BE-Stop-N", "PTT-BE-Target-N" all start with "PTT-".

---

### 8. Acceptance Criteria

1. `dotnet build C:\WSGTA\ptt-lane-c\src\PropTraderTools\PropTraderTools.csproj` -- **0 errors, 0 warnings**.
2. All 7 scans pass (see §11 SCAN checklist).
3. Lizard CCN scan shows **0 rows** with CCN > 8 in ALL `Features/*.cs` methods (includes Ticket C-1 files).
4. `powershell -File scripts\ptt-sync-and-verify.ps1` -- **0 MISMATCH lines** (all files in sync).
5. F5 in NinjaTrader 8 -- **green compile, no script errors**.
6. C-2 `[Fact]` tests appended to `BwaveRefactorLaneCTests.cs` PASS.
7. No public or internal method signatures changed from original source.
8. No logic deleted -- every branch from original source preserved in extracted helpers.
9. No `lock()`, `async void`, `return null`, or non-ASCII identifiers introduced.

---

### 9. NT8 Sync Required

**YES**

```powershell
powershell -File C:\WSGTA\ptt-lane-c\scripts\ptt-sync-and-verify.ps1
```

Verify output: 0 MISMATCH lines. Then press **F5** in NinjaTrader 8.

---

### 10. F5 Required

**YES** -- press F5 in NinjaTrader 8 after sync to recompile the AddOn and verify no NinjaScript compilation errors.

---

### 11. 7-Scan Checklist

Run all scans from `C:\WSGTA\ptt-lane-c\` after completing implementation:

| Scan | Command | Expected Result |
|------|---------|-----------------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | **0 results** |
| SCAN-02 | `Get-Content src/PropTraderTools/*.cs \| Where-Object {$_ -match '[^\x00-\x7F]'}` | **0 results** |
| SCAN-03 | `Select-String -Path src/PropTraderTools/*.cs -Pattern "FontFamily"` | **0 results** |
| SCAN-04 | `Select-String -Path src/PropTraderTools/*.cs -Pattern "#[0-9A-Fa-f]{6}"` | **0 results** |
| SCAN-05 | Verify all `CreateOrder` calls in new helpers use "PTT-" prefix in signal name arg9 | **0 violations** |
| SCAN-06 | `Select-String -Path src/PropTraderTools/*.cs -Pattern "DateTime\.Now[^U]"` | **0 results** |
| SCAN-07 (CYC) | See lizard command below | **0 rows output** |

**SCAN-07 full lizard command:**
```powershell
$files = Get-ChildItem src/PropTraderTools/ -Filter "*.cs" -Recurse |
  Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' }
lizard $files --csv 2>&1 |
  ConvertFrom-Csv -Header @("NLOC","CCN","Tokens","Params","Length","Location","MethodName","MethodLongName","StartLine","EndLine") |
  Where-Object { [int]$_.CCN -gt 8 } |
  Sort-Object { [int]$_.CCN } -Descending
```

Expected: **0 rows output**. Any row = BUILD_FAIL.

---

### 12. Test Stubs for C-2 Helpers

Append these `[Fact]` tests to the existing `BwaveRefactorLaneCTests.cs` class body (inside the `BwaveRefactorLaneCTests` class, after the C-1 stubs):

```csharp
        // C-2: PttBreakEvenSwap helpers
        [Fact] public void PttBreakEvenSwap_SubmitBareStopSwap_Exists()
        { var m = typeof(PttBreakEvenSwap).GetMethod("SubmitBareStopSwap", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(6, m.GetParameters().Length); }

        [Fact] public void PttBreakEvenSwap_SubmitSwapPair_Exists()
        { var m = typeof(PttBreakEvenSwap).GetMethod("SubmitSwapPair", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(8, m.GetParameters().Length); }

        // C-2: PttTrim helper
        [Fact] public void PttTrim_ResolveOrderParams_Exists()
        { var m = typeof(PttTrim).GetMethod("ResolveOrderParams", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(5, m.GetParameters().Length); }

        // C-2: PttFlatten helper
        [Fact] public void PttFlatten_ResolveOrderParams_Exists()
        { var m = typeof(PttFlatten).GetMethod("ResolveOrderParams", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(5, m.GetParameters().Length); }

        // C-2: PttCancel helper
        [Fact] public void PttCancel_IsWorkingEntryOrder_Exists()
        { var m = typeof(PttCancel).GetMethod("IsWorkingEntryOrder", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(m); Assert.Equal(2, m.GetParameters().Length); }
```

---

## Summary

| Ticket | Files | Violations Addressed | New Helpers | New Tests |
|--------|-------|---------------------|-------------|-----------|
| C-1 | PttQuickExit.cs, PttGlobalQuickExit.cs, PttBreakEven.cs | 10 CCN violations | 14 | 14 [Fact] |
| C-2 | PttBreakEvenSwap.cs, PttTrim.cs, PttFlatten.cs, PttCancel.cs | 4 CCN violations | 5 | 5 [Fact] |
| **Total** | **7 files + 1 test file** | **14 CCN violations** | **19 helpers** | **19 [Fact]** |

All 14 lizard violations from the scan resolved. Post-extraction: **0 methods with CCN > 8** in `Features/*.cs`.
