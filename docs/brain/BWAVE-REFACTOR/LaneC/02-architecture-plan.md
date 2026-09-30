# BWAVE-REFACTOR LaneC -- Architecture Plan

**Epic**: BWAVE-REFACTOR LaneC
**Phase**: 1 (Architecture)
**Status**: PLAN_COMPLETE
**Date**: 2026-09-06
**Architect**: ptt-orchestrator (pre-pipeline synthesis from lizard scan + source review)
**Workspace**: `C:\WSGTA\ptt-lane-c\` (git worktree, branch `bwave-refactor-lane-c`)
**Brain dir**: `docs/brain/BWAVE-REFACTOR/LaneC/`

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

**Q1. Same method or within 50 lines?** No (14 violations across 6 files) -- but methods are distributed across multiple files.
**Q2. Fix B design depends on Fix A final design?** YES -- all extractions share the same test file `BwaveRefactorLaneCTests.cs` and the CCN measurement after each extraction may reveal downstream cascades in the same file.
**GATE RESULT: SINGLE-PIPELINE** (Q2=YES mandates single pipeline regardless of Q1).

---

## 1. Scope

**Goal**: Reduce ALL `Features/*.cs` methods with CCN > 8 to CCN <= 8 via guard + loop-body extraction.

**14 violations measured by lizard 2026-09-06**:

| File | Method | CCN |
|------|--------|-----|
| `PttQuickExit.cs` | `Execute(Account, Instrument, int, List<(double,int)>, bool, double, int)` | 32 |
| `PttGlobalQuickExit.cs` | `SnapshotTargetOrders` | 20 |
| `PttBreakEven.cs` | `CancelStaleBracketsLocal` | 16 |
| `PttBreakEven.cs` | `SubmitBeTargetsLocal` | 15 |
| `PttBreakEvenSwap.cs` | `Execute` | 15 |
| `PttBreakEven.cs` | `SnapshotTargetsLocal` | 13 |
| `PttTrim.cs` | `TrimPositionLocal` | 13 |
| `PttFlatten.cs` | `FlattenPositionLocal` | 13 |
| `PttBreakEven.cs` | `IsPttQxTarget` | 12 |
| `PttGlobalQuickExit.cs` | `WaitForPttBeCancelled` | 10 |
| `PttCancel.cs` | `CancelWorkingEntriesLocal` | 10 |
| `PttGlobalQuickExit.cs` | `Execute()` | 9 |
| `PttGlobalQuickExit.cs` | `CancelPttBeOrders` | 9 |
| `PttBreakEven.cs` | `SubmitBeStopLocal` | 9 |

**Note on discrepancy**: Doc comments in some methods (e.g. `TrimPositionLocal` says CYC=5, `IsPttQxTarget` says CYC=2) were written when the methods were simpler. Lizard counts `&&`, `||`, ternaries in boolean expressions that the doc comments under-counted. Lizard is authoritative.

**Out of scope** (dismissed per prompt):
- `CopyEngine.cs` methods -- Lane B scope only
- `TradeCopierPanel.cs` / `TradeCopierWindow.cs` -- Lane A scope only

---

## 2. Extraction Strategy

**Extraction rules** (canonical, non-negotiable):
1. Extract guards + loop bodies to `private`/`private static` named helpers.
2. NEVER delete logic. Every branch survives.
3. No `lock()`. No `async void`. No `return null`. ASCII-only names.
4. Do not change `public`/`internal` method signatures.
5. Add 1 structural `[Fact]` test per NEW extracted helper.

---

## 3. Per-Method Extraction Plans

### 3.1 `PttQuickExit::Execute` (CCN=32 → target ≤8)

The method has these logical sections:
1. **Null/flat guard** (pos-find loop + qty check) -- lines 49-65
2. **Follower guard** -- lines 67-75
3. **Stop resolution** -- line 79
4. **QX snapshot + cancel** -- lines 92-97
5. **Direction + tick computation** -- lines 99-101
6. **Target count resolution** -- line 105 (already extracted to `ResolveTargetCount`)
7. **OCO submit loop** (for i = 0..targetCount) -- lines 111-200

The loop body is massive (the stop-submit try/catch block + target-submit try/catch block). Extract:

**JS-002 COMPLIANCE NOTE**: `FindLeaderPosition` is NOT extracted because it would require `return null` (violating JS-002). The position-finding foreach stays inline in `Execute`. The foreach loop itself contributes 1 CCN point; the if-guard contributes 1 more. This is accounted for in the Execute budget below.

**New helpers in `PttQuickExit`**:

| Helper | CYC | Description |
|--------|-----|-------------|
| `private void SubmitStopOrder(Account leader, Instrument instr, bool isLong, int tNQty, double snapshotStop, string ocoId_i, string stopName)` | 2 | The stop try/catch block (lines 136-167) -- extracts the `if (snapshotStop > 0)` block + try/catch |
| `private void SubmitTargetOrder(Account leader, Instrument instr, bool isLong, int tNQty, double tNPrice, string ocoId_i, string targetName)` | 2 | The target try/catch block (lines 168-199) |

After extraction, `Execute` body:
- pos-find foreach (kept inline) -- +1 (foreach) +1 (if p.Instrument==instr) = 2
- qty==0 guard -- +1
- follower guard (if+&&) -- +2
- stop resolution + log -- +1 (ternary in log string excluded by lizard)
- snapshot + cancel -- +0
- direction ternary -- +1
- targetCount resolution -- +0 (call to existing helper)
- for-loop header -- +1
- tNQty ternary -- +1
- tNQty<=0 continue -- +1 (if inside loop)
- i==0 firstOcoId branch -- +1

Total branches above base(1) = 11. But we remove the two large try/catch blocks (stop: 4 branches, target: 3 branches) = remove 7 branches. **Execute CCN after extraction ≈ base(1) + (11-7) = 5 + old Execute overhead.**

Re-count from scratch for post-extraction Execute:
- base = 1
- pos-find foreach = +1
- `if (p.Instrument == instr)` = +1
- `if (pos == null || pos.Quantity == 0)` = +1 (||) +1 = +2
- `if (skipIfFollower && ...)` = +1 (&&) +1 = +2
- `if (CopyEngine.Instance?.IsFollowerAccount(leader) == true)` -- subsumed in above `&&`
- for-loop = +1
- `(targets != null && i < targets.Count) ? ... : ...` ternary = +1 (&&) +1 (?) = +2
- `if (tNQty <= 0)` = +1
- `if (i == 0)` firstOcoId = +1
- `SubmitStopOrder` call = +0
- `SubmitTargetOrder` call = +0

Total = 1 + 1 + 1 + 2 + 2 + 1 + 2 + 1 + 1 = **12**. Still over 8.

**Revised plan**: Also extract the entire OCO-pair loop body (the per-iteration block) into a helper that calls SubmitStopOrder + SubmitTargetOrder:

| Helper | CYC | Description |
|--------|-----|-------------|
| `private void SubmitStopOrder(Account acc, Instrument instr, bool isLong, int qty, double snapshotStop, string ocoId, string stopName)` | 2 | The stop submit try/catch |
| `private void SubmitTargetOrder(Account acc, Instrument instr, bool isLong, int qty, double tNPrice, string ocoId, string targetName)` | 2 | The target submit try/catch |
| `private string SubmitQxOcoPair(Account acc, Instrument instr, bool isLong, double entryPx, double snapshotStop, double tick, int t1Ticks, int i, int targetCount, System.Collections.Generic.List<(double Price, int Qty)> targets)` | 8 | Per-iteration: compute tNTicks, tNPrice, tNQty, ocoId, stopName, targetName; call SubmitStopOrder + SubmitTargetOrder; return ocoId string |

`SubmitQxOcoPair` returns a `string` (the ocoId -- never null due to Guid fallback). JS-002 satisfied: string is a non-nullable value in this context (always assigned).

After extraction, `Execute` body:
- base = 1
- pos-find foreach = +1
- `if (p.Instrument == instr)` = +1
- `if (pos == null || pos.Quantity == 0)` (||) = +1 +1 = +2
- `if (skipIfFollower && ...)` (&&) = +1 +1 = +2
- for-loop = +1
- `if (i == 0)` firstOcoId branch -- removed (moved into SubmitQxOcoPair)
- SubmitQxOcoPair call = +0

Total = 1 + 1 + 1 + 2 + 2 + 1 = **8. CCN=8 exactly. PASS.**

`SubmitQxOcoPair` CYC: base(1) + tNQty ternary (&&+?) = +2 + tNQty<=0 continue = +1 + firstOcoId branch(i==0) = +1 + SubmitStopOrder call = +0 + SubmitTargetOrder call = +0 = **5**. Under 8. PASS.

### 3.2 `PttGlobalQuickExit::SnapshotTargetOrders` (CCN=20 → target ≤8)

Inspecting the method: it builds `nativeTargets` and `pttTargets` lists, then deduplicates. The dedup section (lines 482-493) has its own loop. Extract:

**New helpers in `PttGlobalQuickExit`**:

| Helper | CYC | Description |
|--------|-----|-------------|
| `private static bool IsTargetOrder(Order o, Instrument instr)` | 3 | State+instr+name checks (lines 449-470, the filter block) |
| `private static System.Collections.Generic.List<(double Price, int Qty)> DeduplicateByPrice(System.Collections.Generic.List<(double Price, int Qty)> targets)` | 2 | The dedup dictionary loop (lines 482-493) |

After extraction, `SnapshotTargetOrders` body: null-guard(1) + foreach(1) + o-null-continue(0) + IsTargetOrder branch(1) + isNative/isPtt branch(1) + nativeCount check(1) + pttReturn(0) + DeduplicateByPrice call(0) + result-build loop(1) = base(1) + 5 = **CCN≈6**.

### 3.3 `PttBreakEven::CancelStaleBracketsLocal` (CCN=16 → target ≤8)

The method iterates orders with 5 conditions stacked in `stateOk`. Extract:

**New helpers in `PttBreakEven`**:

| Helper | CYC | Description |
|--------|-----|-------------|
| `private static bool IsCancellableState(OrderState s)` | 5 | The stateOk bool expression (Working\|Initialized\|Submitted\|Accepted\|TriggerPending) |
| `private static bool IsStaleOrder(Order o, Instrument instr)` | 3 | The combined stateOk+instrOk+notBe compound check |

After extraction, `CancelStaleBracketsLocal`: null-guard(1) + foreach(1) + o-null-continue(0) + IsStaleOrder branch(1) + count==0 return(1) + try/catch(1) = base(1) + 5 = **CCN≈6**.

### 3.4 `PttBreakEven::SubmitBeTargetsLocal` (CCN=15 → target ≤8)

This method has a 0-targets branch (bare-stop path) AND a per-pair loop. Extract:

**New helpers in `PttBreakEven`**:

| Helper | CYC | Description |
|--------|-----|-------------|
| `private static void SubmitBareStop(Account acc, Instrument instr, OrderAction stopDirection, double bePrice)` | 3 | The 0-targets try/catch block (lines 488-522) |
| `private static void SubmitBePair(Account acc, Instrument instr, OrderAction stopDirection, double bePrice, string ocoId_i, int i, (double Price, int Qty, OrderAction Action) t)` | 3 | One OCO pair: stop+target submits (lines 532-628) |

After extraction: null-guards(1+1) + stopDirection assign(0) + targets-count==0 branch(1) + SubmitBareStop call(0) + return(0) + for-loop(1) + SubmitBePair call(0) = base(1) + 3 = **CCN≈4**.

### 3.5 `PttBreakEvenSwap::Execute` (CCN=15 → target ≤8)

Same pattern as `SubmitBeTargetsLocal`. Extract:

**New helpers in `PttBreakEvenSwap`**:

| Helper | CYC | Description |
|--------|-----|-------------|
| `private static void SubmitBareStopSwap(Account acc, Instrument instr, bool isLong, OrderAction stopDir, double newStop)` | 3 | The 0-targets IsStopPriceSubmittable + try/catch + else-log block |
| `private static void SubmitSwapPair(Account acc, Instrument instr, bool isLong, OrderAction stopDir, double newStop, string ocoId_i, int i, (double Price, int Qty, OrderAction Action) t)` | 3 | The per-pair stop+target submits |

After extraction: null-guard(1) + flat-guard(1) + cancel(0) + isLong ternary(1) + 0-targets branch(1) + SubmitBareStopSwap call(0) + return(0) + seq assign(0) + for-loop(1) + SubmitSwapPair call(0) = base(1) + 5 = **CCN≈6**.

### 3.6 `PttBreakEven::SnapshotTargetsLocal` (CCN=13 → target ≤8)

The stateOk expression has 5 terms. Extract:

**New helper in `PttBreakEven`**:

| Helper | CYC | Description |
|--------|-----|-------------|
| `private static bool IsSnapshotEligibleState(OrderState s)` | 5 | The stateOk bool (Working\|Accepted\|Submitted\|Initialized\|TriggerPending) |

After extraction: null-guard(1) + foreach(1) + o-null-continue(0) + IsSnapshotEligibleState+instrOk+nameFilter compound check(3) = base(1) + 4 = **CCN≈5**. Note: the compound filter line `if (!stateOk || !instrOk || ...)` still contains `||` but lizard counts the individual boolean operators so extracting `stateOk` removes 4 count points.

### 3.7 `PttTrim::TrimPositionLocal` (CCN=13 → target ≤8)

Lizard counts more than the doc says. Looking carefully: `useLimitOrder` has `tickSize > 0.0 && (... ? ... : ...)` = 2 ternaries + 1 `&&` = 3 points. Plus the `if (useLimitOrder)` branch with `pos.MarketPosition == MarketPosition.Long ? ...` inside = another point.

Extract:

**New helper in `PttTrim`**:

| Helper | CYC | Description |
|--------|-----|-------------|
| `private static (OrderType orderType, double limitPrice, double stopPrice) ResolveOrderParams(Position pos, int buffer, double ask, double bid, double tickSize)` | 5 | The useLimitOrder computation + branch (lines 113-136) |

After extraction: null-guard(1) + direction ternary(1) + ResolveOrderParams call(0) + try/catch(1) + order-null check(1) = base(1) + 4 = **CCN≈5**.

### 3.8 `PttFlatten::FlattenPositionLocal` (CCN=13 → target ≤8)

Same structure as TrimPositionLocal. Extract same pattern:

**New helper in `PttFlatten`**:

| Helper | CYC | Description |
|--------|-----|-------------|
| `private static (OrderType orderType, double limitPrice, double stopPrice) ResolveOrderParams(Position pos, int buffer, double ask, double bid, double tickSize)` | 5 | The useLimitOrder computation + branch |

After extraction: CCN≈5 same as PttTrim.

### 3.9 `PttBreakEven::IsPttQxTarget` (CCN=12 → target ≤8)

Lizard counts each `&&` as +1. The method has: `name == null || name.Length != 9` = 2 points, then `name[0] == 'P' && name[1] == 'T' && ... && name[8] <= '3'` = 8 `&&` operators = 8 points. Total = base(1) + 1(||) + 8(&&) = 10. Lizard says 12 -- there may also be `>=` and `<=` counted.

Extract:

**New helper in `PttBreakEven`**:

| Helper | CYC | Description |
|--------|-----|-------------|
| `private static bool HasQxTargetBody(string name)` | 8 | The char-index body checks `name[0]..name[8]` (lines 370-380) -- cap at 8 |

After extraction: `IsPttQxTarget` becomes null/length guard(2) + `HasQxTargetBody` call(0) = base(1) + 1 = **CCN≈2**.
`HasQxTargetBody`: 8 `&&` comparisons = base(1) + 8 = **CCN≈9** -- still over 8.

Revised: split the char checks across two helpers:

| Helper | CYC | Description |
|--------|-----|-------------|
| `private static bool IsQxTargetPrefix(string name)` | 4 | `name[0..6]` checks: P,T,T,-,Q,X,- (4 &&) |
| `private static bool IsQxTargetSuffix(string name)` | 3 | `name[7]=='T' && name[8]>='1' && name[8]<='3'` (3 &&+comparison) |

After extraction: `IsPttQxTarget` = guard(2) + IsQxTargetPrefix(0) + IsQxTargetSuffix(0) = **CCN≈3**.

But wait -- 12 - 3 helpers with combined ~8+8 seems like over-extraction. Let me re-examine: 
Actually the simpler approach for IsPttQxTarget: replace the long char-by-char check with a single `StartsWith` + `Char.IsDigit` check that reads the same way but with 3 comparisons:

```csharp
private static bool IsPttQxTarget(string name)
{
    if (name == null || name.Length != 9)  // (1)(2)
        return false;
    return name.StartsWith("PTT-QX-T", StringComparison.Ordinal) // 0 new branches
        && name[8] >= '1' && name[8] <= '3'; // (3)(4)
}
```

This collapses 8 `&&` into 0 branches (StartsWith is 1 method call = 0 cyclomatic branches) + 2 comparisons. Lizard would see: `||` (1) + `&&` (1) + `&&` (1) + `>=` (1) + `<=` (1) = base(1) + 5 = **CCN≈6**. Still a simplification.

**Extraction plan for IsPttQxTarget**: Replace the 8-char char-index checks with `name.StartsWith("PTT-QX-T", StringComparison.Ordinal) && name[8] >= '1' && name[8] <= '3'`. No new extracted helper needed -- pure simplification of the expression.

### 3.10 `PttGlobalQuickExit::WaitForPttBeCancelled` (CCN=10 → target ≤8)

Lizard says CCN=10 but doc says CYC=7. The difference is likely `&&` in boolean expressions. Extract:

**New helper in `PttGlobalQuickExit`**:

| Helper | CYC | Description |
|--------|-----|-------------|
| `private static bool IsNonTerminalForInstr(Order o, Instrument instr)` | 4 | The null+instrOk+IsPttBeOrder+IsNonTerminalPttBeState compound check |

After extraction: guard(1) + while(1) + foreach(1) + o-null-continue(0) + IsNonTerminalForInstr branch(1) + nonTerminal==0 check(1) = base(1) + 5 = **CCN≈6**.

### 3.11 `PttCancel::CancelWorkingEntriesLocal` (CCN=10 → target ≤8)

Same pattern. Extract:

**New helper in `PttCancel`**:

| Helper | CYC | Description |
|--------|-----|-------------|
| `private static bool IsWorkingEntryOrder(Order o, Instrument instr)` | 4 | The null+stateOk+instrOk compound check |

After extraction: null-guard(1) + foreach(1) + o-null-continue(0) + IsWorkingEntryOrder branch(1) + count==0 return(1) + try/catch(1) = base(1) + 5 = **CCN≈6**.

### 3.12 `PttGlobalQuickExit::Execute()` (CCN=9 → target ≤8)

Current count: flag-guard(1) + acc-loop(1) + follower-guard(1) + pos-loop(1) + null/flat-continue(1) + DIAG for-loop(1) + flatten-guard(1) + ExecuteFollowers(1) = base(1) + 8 = **CCN=9**. The DIAG for-loop is the culprit. Extract:

**New helper in `PttGlobalQuickExit`**:

| Helper | CYC | Description |
|--------|-----|-------------|
| `private static void LogLeaderDiag(Account acc, System.Collections.Generic.List<(double Price, int Qty)> targets, int posQty)` | 2 | The `_sb` StringBuilder + for-loop DIAG block (lines 82-100) |

After extraction: flag-guard(1) + acc-loop(1) + follower-guard(1) + pos-loop(1) + null/flat-continue(1) + LogLeaderDiag call(0) + flatten-guard(1) + ExecuteFollowers call(0) = base(1) + 6 = **CCN≈7**.

### 3.13 `PttGlobalQuickExit::CancelPttBeOrders` (CCN=9 → target ≤8)

Current: null-guards(2, from `&&`) + foreach(1) + o-null(1) + instrOk(1) + IsPttBeOrder(1) + IsNonTerminalPttBeState(1) + count==0 return(1) = base(1) + 8 = **CCN=9**. Extract:

**New helper** (reuse the helper from 3.10):
Actually `IsNonTerminalForInstr` already covers instr check + IsPttBe + state. So:

| Helper | CYC | Description |
|--------|-----|-------------|
| Re-use `IsNonTerminalForInstr` | 4 | Already defined for WaitForPttBeCancelled |

After extraction: null-guard (acc||instr, 2) + foreach(1) + IsNonTerminalForInstr(0) + count==0 return(1) + acc.Cancel(0) = base(1) + 4 = **CCN≈5**.

But wait -- `CancelPttBeOrders` needs ALL non-terminal orders, not just the instr-matching ones -- it actually does the same instr+IsPttBe+nonTerminal checks as WaitForPttBeCancelled. Let me verify this reuse is safe. Looking at lines 579-591: the loop is `instrOk (line 584) + IsPttBeOrder (line 586) + IsNonTerminalPttBeState (line 588)` -- same triple check as WaitForPttBeCancelled. Re-using `IsNonTerminalForInstr` is correct.

### 3.14 `PttBreakEven::SubmitBeStopLocal` (CCN=9 → target ≤8)

Current: null-guard(1, `&&`) + pos-guard(1) + direction ternary(1) + try/catch(1) + pos-qty-check(1) + stop-order-submit(1) = base(1) + 6 = ~7. Lizard says 9. The `&&` in null-guard is +1, the `(acc != null ? acc.Name : "null")` in catch is +1 = 9 total.

The null-guard `acc == null || instr == null` contributes `||` = +1. The ternary in the catch `(acc != null ? acc.Name : "null")` = +1. That gets to 9.

Extract:

**New helper in `PttBreakEven`**:

| Helper | CYC | Description |
|--------|-----|-------------|
| `private static string SafeName(Account acc)` | 1 | `acc != null ? acc.Name : "null"` -- eliminates the inline ternary |

After extraction: null-guard(||)(1) + pos-guard(1) + direction ternary(1) + try/catch(1) + order-null check(1) + SafeName call(0) = base(1) + 5 = **CCN≈6**. But lizard counted 9 -- we remove 2 counted branches (the `||` becomes a method call, and the catch ternary). Wait -- `||` is still in `acc == null || instr == null` (1 point), direction ternary (1 point), still there. We only remove the catch ternary (1 point removed). New count: 9 - 1 = 8. At CCN=8, it passes the ≤8 threshold.

Alternatively, extract the entire null-guard check into `private static bool IsInvalidInput(Account acc, Instrument instr)` to also remove the `||`. That gets to 9-2=7. **Use this approach.**

---

## 4. Ticket Plan

All extractions across 6 files. Files affected by each:

**Ticket C-1**: PttQuickExit.cs + PttGlobalQuickExit.cs + PttBreakEven.cs
**Ticket C-2**: PttBreakEvenSwap.cs + PttTrim.cs + PttFlatten.cs + PttCancel.cs

**Rationale for 2-ticket split**: The methods in the second group (PttBreakEvenSwap, PttTrim, PttFlatten, PttCancel) are structurally similar (smaller, simpler extractions) and share no cross-file dependencies. They can be batched safely in one ticket. The first group contains the most complex extractions (PttQuickExit CCN=32) and should be isolated to minimize engineer cognitive load.

**Test file**: `src/PropTraderTools/Tests/BwaveRefactorLaneCTests.cs` (NEW FILE) -- 1 `[Fact]` per extracted helper (structural, not behavioral -- verifies the helper exists via reflection or direct instantiation).

---

## 5. Non-Goals

- No changes to `public`/`internal` method signatures.
- No changes to behavior -- pure structural extraction.
- No changes outside `Features/*.cs` and the new test file.
- No changes to `CopyEngine.cs`, `TradeCopierPanel.cs`, `TradeCopierWindow.cs`.

---

## 6. Build / Sync / CYC Verification

After each ticket:
1. `dotnet build C:\WSGTA\ptt-lane-c\src\PropTraderTools\PropTraderTools.csproj` -- 0 errors
2. 7-scan checklist (see tickets)
3. lizard CCN scan -- 0 Features/*.cs methods with CCN > 8
4. `powershell -File scripts\ptt-sync-and-verify.ps1` -- 18/18 OK
5. F5 in NinjaTrader 8

---

**PLAN_COMPLETE**
