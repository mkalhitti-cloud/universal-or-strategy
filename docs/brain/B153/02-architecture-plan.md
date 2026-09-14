# B153 Architecture Plan — Remove Hardcoded 3-Leg Ceiling

**Block:** B153  
**Phase:** 1 — Architecture  
**Status:** PLAN_COMPLETE  
**Date:** 2026-09-13  
**Author:** ptt-architect  

---

## LANE-SPLIT GATE RESULT

```
Q1. Same method or within 50 lines?
    T1 (PttQuickExit.cs ~434) vs T2 (CopyEngine.cs ~6705): different files. NO.
    T1 vs T3 (CopyEngine.cs ~3414-3619): different files. NO.
    T1 vs T4 (PttBreakEven.cs ~588): different files. NO.
    T2 vs T3: same file, ~3000 lines apart. NO.
    All other pairs: different files. NO.
    Q1 = NO. Continue.

Q2. Fix B design depends on Fix A final design?
    T1 and T2: independent paths, independent files. NO.
    T1 and T3: independent paths, independent files. NO.
    T1 and T4: T4 parses names T1 produces but is not design-dependent on T1. NO.
    T2 and T3: independent paths in same file. NO.
    All cross-ticket pairs: NO.
    Q2 = NO. Continue.

Q3. Each fix has standalone value if the other is blocked?
    T1 alone: QX/QX-ALL correct for N targets. YES.
    T2 alone: BE/BE-ALL correct for N targets. YES.
    T3 alone: stop drag syncs N collateral legs. YES.
    T4 alone: IsPttQxTarget recognizes T4+ names. YES.
    Q3 = YES. Continue.

Q4. Each fix has an independent SIM verification path?
    T1: QX with 5-target template -> T4/T5 QX pairs created. YES.
    T2: BE with 5-target template -> T4/T5 BE moves executed. YES.
    T3: drag stop with 5-target template -> legs 4/5 sync. YES.
    T4: QX-ALL then BE-ALL -> T4/T5 recognized. YES.
    Q4 = YES.

LANE-SPLIT GATE RESULT: LANES-APPROVED
```

> NOTE: The Epic Brief and context file label this "1 lane, sequential." The gate evaluation above yields LANES-APPROVED because all 4 tickets have distinct files or line regions, no cross-ticket design dependency, independent value, and independent SIM paths. The architect records LANES-APPROVED. The orchestrator may schedule execution sequentially within a single lane at their discretion; the gate result stands as the architectural ruling.

---

## 1. Epic Summary

Block B153 removes four independent hardcoded 3-leg ceilings from bracket management paths in three files. NT8 ATM templates support N targets (tested to 22+; no NT8 ceiling exists). With more than 3 targets, Target4+ are silently ignored on stop drag, QX, and BE. The fix is architectural — not button-specific, not mode-specific. Each ceiling site is an independent code path, each with one fix point that covers all buttons on that path.

**Closing prior backlog items:** DW-B107 (BE cap) and DW-B106 (QX cap) residue-protection ceiling guards are obsoleted by DeduplicateByPrice, which already handles residue inflation. Both ceilings are removed in this block.

---

## 2. Problem Statement

### Ceiling Sites (code-confirmed 2026-09-13)

| # | File | Symbol | Line | Root Cause |
|---|------|--------|------|------------|
| 1 | PttQuickExit.cs | ResolveTargetCount | ~434 | `Math.Min(raw, 3)` caps QX target count |
| 2 | CopyEngine.cs | MoveStopToBreakEven | ~6705 | `while (targets.Count > 3)` trims BE snapshot |
| 3a | CopyEngine.cs | TryParseStopSuffix | ~3451 | `\|\| n > 3` rejects stop suffix > 3 |
| 3b | CopyEngine.cs | CaptureOtherLegTargetPrices | ~3414 | `new double[3]` + `for i <= 3` fixed array |
| 3c | CopyEngine.cs | ResubmitCollateralLegs | ~3619 | `double[]` param + `for i <= 3` fixed loop |
| 4 | PttBreakEven.cs | IsPttQxTarget | ~588 | `name.Length != 9` + `name[8] <= '3'` reject T4+ |

### Impact Without Fix
- **T1 not fixed:** QX and QX-ALL silently drop T4+ legs; user sees partial QX execution with no error.
- **T2 not fixed:** BE and BE-ALL silently drop T4+ targets; stop not moved to entry for T4+.
- **T3 not fixed:** Dragging stop only resubmits collateral legs 1-3; T4+ collateral remains at wrong price.
- **T4 not fixed:** After QX-ALL with 5+ targets, BE path cannot recognize T4/T5 as QX targets; BE-after-QX broken for T4+.

---

## 3. Architecture Decisions (Locked)

The following are confirmed locked and require no investigation or change:

| Item | Status | Rationale |
|------|--------|-----------|
| DeriveLeaderBracketIndex | NO CHANGE | Already accepts any positive integer |
| IsNativeTargetOrder / IsPttTargetOrder | NO CHANGE | Use `char.IsDigit` — already N-safe |
| Quick2t / QAll2t buttons | NO CHANGE | Intentionally fixed at 2 targets |
| NT8 AddOnBase API | CONFIRMED | `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` only |
| `Account.Change()` | NOT USED | Silent no-op on ATM-owned brackets |
| `AtmStrategyChangeStopTarget()` | NOT USED | StrategyBase-only, not AddOnBase |
| `AtmStrategyCreate()` | NOT USED | StrategyBase-only, not AddOnBase |
| Lock pattern | CONFIRMED | All state via FSM/Actor Enqueue — no `lock()` blocks |
| DeduplicateByPrice | CONFIRMED | Handles residue inflation for both BE and QX paths |
| ASCII-only | ENFORCED | No Unicode, emoji, curly quotes in any C# string literals |
| NT8 target ceiling | CONFIRMED NONE | NT8 ATM templates tested to 22+ targets |

---

## 4. Threading Model

No changes to the threading model are required.

- **DW-B153-01:** `ResolveTargetCount` is pure arithmetic. No shared state. Thread-safe by construction.
- **DW-B153-02:** `MoveStopToBreakEven` operates on a snapshotted target list. Snapshot ownership is caller-scoped. Removing the trim does not alter the threading boundary.
- **DW-B153-03:** `TryParseStopSuffix` is pure string parsing. `CaptureOtherLegTargetPrices` reads from orders (existing pattern). `ResubmitCollateralLegs` issues `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` (existing pattern, extended to N iterations). The type change `double[]` → `Dictionary<int,double>` is internal — no threading contract change.
- **DW-B153-04:** `IsPttQxTarget` is a pure string predicate. No shared state.

No `Dispatcher.InvokeAsync` additions required. No `ConcurrentQueue` ownership changes.

---

## 5. Data Flow

```
QX Path (DW-B153-01):
  Button click -> gate chain -> ResolveTargetCount(raw) [FIXED: return raw]
  -> for i=1..N: emit PTT-QX-Ti order -> NT8 processes N QX bracket pairs

BE Path (DW-B153-02):
  Button click -> gate chain -> SnapshotBeTargets() -> MoveStopToBreakEven [FIXED: no trim]
  -> DeduplicateByPrice -> N targets processed

Stop Drag Path (DW-B153-03):
  Stop dragged -> order-change event
  -> TryParseStopSuffix [FIXED: accept N>=1]
  -> CaptureOtherLegTargetPrices [FIXED: Dictionary<int,double>]
  -> HandleAtmStopSync [type change at call site]
  -> ResubmitCollateralLegs(Dictionary<int,double>) [FIXED: foreach kvp]
  -> Cancel+CreateOrder+Submit for each leg

BE-after-QX Path (DW-B153-04):
  BE path fires -> IsPttQxTarget per order name [FIXED: accept T4+, T10+]
  -> T4..TN recognized -> BE processes all N QX targets
```

---

## 6. Ticket Breakdown

### DW-B153-01: Remove QX Target Ceiling

**Spec req:** REQ-1 (QX ceiling removal)  
**File:** `src/PropTraderTools/Features/PttQuickExit.cs`  
**Symbol:** `ResolveTargetCount` (~line 434)  

**Change:**
```csharp
// BEFORE
// Hard cap: never return more than 3. QX-ALL contract is always exactly 3 targets.
return Math.Min(raw, 3);

// AFTER
// No ceiling -- NT8 supports N targets.
return raw;
```

**Method signature (unchanged):**
```csharp
private static int ResolveTargetCount(int raw)
```

**CYC impact:** -1 (Math.Min branch removed). Pre-fix CYC confirmed ≤8; post-fix lower.  
**No new NT8 API calls.** No threading changes. No cross-file impact.

**xUnit tests:**
- `ResolveTargetCount_Returns3_WhenRawIs3` — assert returns 3 (regression)
- `ResolveTargetCount_Returns5_WhenRawIs5` — assert returns 5 (new capability)
- `ResolveTargetCount_Returns22_WhenRawIs22` — assert returns 22 (N-target ceiling removed)
- `ResolveTargetCount_Returns1_WhenRawIs1` — assert returns 1 (lower bound preserved)

**SCAN coverage:** SCAN-4 (`Math.Min.*3` → 0)

---

### DW-B153-02: Remove BE Target Ceiling

**Spec req:** REQ-2 (BE ceiling removal)  
**File:** `src/PropTraderTools/CopyEngine.cs`  
**Symbol:** `MoveStopToBreakEven` (~line 6705)  

**Change (remove 2 lines):**
```csharp
// REMOVE:
// DW-B107: hard cap -- BE/QX contract is always exactly 3 targets max.
while (targets.Count > 3)
    targets.RemoveAt(targets.Count - 1);

// ADD COMMENT (in place of removed block):
// No ceiling -- SnapshotBeTargets DeduplicateByPrice handles residue inflation.
```

**Method signature (unchanged):**
```csharp
private void MoveStopToBreakEven(/* existing params unchanged */)
```

**CYC impact:** -1 (while branch removed). Pre-fix CYC confirmed ≤8; post-fix lower.  
**No new NT8 API calls.** No threading changes. No cross-file impact.

**xUnit tests:**
- `MoveStopToBreakEven_Processes3Targets_WhenSnapshotHas3` — regression
- `MoveStopToBreakEven_Processes5Targets_WhenSnapshotHas5` — new capability; assert no trim
- `MoveStopToBreakEven_Processes5Targets_SnapshotCountUnchanged` — snapshot.Count == 5 after call

**SCAN coverage:** SCAN-5 (`targets.Count > 3` → 0)

---

### DW-B153-03: Remove Stop Drag Ceiling (3 tightly-coupled symbols)

**Spec req:** REQ-3 (stop drag ceiling removal)  
**File:** `src/PropTraderTools/CopyEngine.cs`  
**Symbols (must be committed together):**
- `TryParseStopSuffix` (~line 3451)
- `CaptureOtherLegTargetPrices` (~line 3414)
- `ParseTargetLegIndex` (NEW private static helper)
- `ResubmitCollateralLegs` (~line 3619)
- `HandleAtmStopSync` call site (type change propagation)

#### (A) TryParseStopSuffix

**Change:** remove `|| n > 3`

```csharp
// BEFORE
if (n < 1 || n > 3)
    return false;

// AFTER
if (n < 1)
    return false;
```

**Method signature (unchanged):**
```csharp
private static bool TryParseStopSuffix(string orderName, out int n)
```

**CYC impact:** -1 (branch removed).

#### (B) CaptureOtherLegTargetPrices

**Change:** replace fixed `double[3]` array and `for i <= 3` loop with dynamic Dictionary scan.

**Method signature change:**
```csharp
// BEFORE
private Dictionary<int, double> CaptureOtherLegTargetPrices(
    string excludeSuffix,
    Account account)
// NOTE: return type was double[] — changing to Dictionary<int, double>
// Actual before signature:
private double[] CaptureOtherLegTargetPrices(string excludeSuffix, Account account)

// AFTER
private Dictionary<int, double> CaptureOtherLegTargetPrices(
    string excludeSuffix,
    Account account)
```

**Implementation approach:**
```csharp
private Dictionary<int, double> CaptureOtherLegTargetPrices(
    string excludeSuffix,
    Account account)
{
    var prices = new Dictionary<int, double>();
    foreach (var order in account.Orders)
    {
        int idx = ParseTargetLegIndex(order.Name, excludeSuffix);
        if (idx >= 1)
            prices[idx] = order.LimitPrice;
    }
    return prices;
}
```

**CYC:** CCN = 3 (foreach + idx >= 1 check). Within budget (≤4 specified).

#### (C) NEW: ParseTargetLegIndex

**Signature:**
```csharp
private static int ParseTargetLegIndex(string orderName, string excludeSuffix)
```

**Implementation:**
```csharp
private static int ParseTargetLegIndex(string orderName, string excludeSuffix)
{
    if (orderName == null) return -1;
    const string prefix = "PTT-TGT-Drag-";
    if (!orderName.StartsWith(prefix, StringComparison.Ordinal)) return -1;
    string suffix = orderName.Substring(prefix.Length);
    if (suffix == excludeSuffix) return -1;
    return int.TryParse(suffix, out int idx) ? idx : -1;
}
```

**CYC:** CCN = 4 (null, prefix, excludeSuffix, TryParse). Within budget (≤4 specified).

#### (D) ResubmitCollateralLegs

**Method signature change:**
```csharp
// BEFORE
private void ResubmitCollateralLegs(double[] otherLegPrices, Account account, /* ... */)

// AFTER
private void ResubmitCollateralLegs(
    Dictionary<int, double> otherLegPrices,
    Account account,
    /* other params unchanged */)
```

**Body change:**
```csharp
// BEFORE
for (int i = 1; i <= 3; i++)
{
    // cancel + recreate PTT-TGT-Drag-{i} at otherLegPrices[i-1]
}

// AFTER
foreach (var kvp in otherLegPrices)
{
    // cancel + recreate PTT-TGT-Drag-{kvp.Key} at kvp.Value
}
```

**CYC impact:** 0 (for-loop replaced with equivalent foreach; branch count unchanged).

#### (E) HandleAtmStopSync call site

```csharp
// BEFORE
double[] prices = CaptureOtherLegTargetPrices(excludeSuffix, account);
ResubmitCollateralLegs(prices, account, /* ... */);

// AFTER
Dictionary<int, double> prices = CaptureOtherLegTargetPrices(excludeSuffix, account);
ResubmitCollateralLegs(prices, account, /* ... */);
```

No logic change at call site — type flows through. Both methods are private, so compiler enforces all callers.

**xUnit tests:**
- `TryParseStopSuffix_ReturnsFalse_WhenSuffixIs0` — boundary at 0
- `TryParseStopSuffix_ReturnsTrue_WhenSuffixIs4` — new: n=4 accepted
- `TryParseStopSuffix_ReturnsTrue_WhenSuffixIs22` — new: n=22 accepted
- `ParseTargetLegIndex_ReturnsNegative1_WhenNullInput` — null guard
- `ParseTargetLegIndex_ReturnsNegative1_WhenWrongPrefix` — prefix guard
- `ParseTargetLegIndex_ReturnsNegative1_WhenExcludeSuffixMatches` — exclude guard
- `ParseTargetLegIndex_Returns4_WhenOrderNameIsPttTgtDrag4` — happy path
- `CaptureOtherLegTargetPrices_Returns5Entries_WhenAccountHas5TargetDragOrders`
- `ResubmitCollateralLegs_Processes5Legs_WhenDictionaryHas5Entries`
- `ResubmitCollateralLegs_DoesNotProcess_WhenDictionaryIsEmpty`

**SCAN coverage:** SCAN-1 (`n > 3` → 0), SCAN-2 (`i <= 3` → 0), SCAN-3 (`new double[3]` → 0)

---

### DW-B153-04: Remove IsPttQxTarget Single-Digit Ceiling

**Spec req:** REQ-4 (IsPttQxTarget ceiling removal)  
**File:** `src/PropTraderTools/Features/PttBreakEven.cs`  
**Symbol:** `IsPttQxTarget` (~line 588)  

**Change:**
```csharp
// BEFORE
private static bool IsPttQxTarget(string name)
{
    if (name == null || name.Length != 9)
        return false;
    return name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
        && name[8] >= '1'
        && name[8] <= '3';
}

// AFTER
private static bool IsPttQxTarget(string name)
{
    if (name == null || name.Length < 9)
        return false;
    if (!name.StartsWith("PTT-QX-T", StringComparison.Ordinal))
        return false;
    return int.TryParse(name.Substring(8), out int n) && n >= 1;
}
```

**Method signature (unchanged):**
```csharp
private static bool IsPttQxTarget(string name)
```

**Why `name.Length < 9` (not `!= 9`):**  
`PTT-QX-T` is 8 chars. The shortest valid name is `PTT-QX-T1` (9 chars). Names `PTT-QX-T10`, `PTT-QX-T22` are 10-11 chars and must be accepted. The `< 9` guard remains a fast rejection of obviously-invalid inputs before the more expensive StartsWith + TryParse.

**CYC analysis:**
- Before: 3 decision points (null/length, StartsWith, char range — 2 comparisons folded into 1 short-circuit chain)
- After: 4 decision points (null/length, StartsWith, TryParse, n >= 1)
- Net: +1 branch. Post-fix CCN ≈ 4. Well within budget (≤8).

**xUnit tests:**
- `IsPttQxTarget_ReturnsFalse_WhenNull` — null guard
- `IsPttQxTarget_ReturnsFalse_WhenTooShort` — length < 9
- `IsPttQxTarget_ReturnsFalse_WhenWrongPrefix` — wrong prefix
- `IsPttQxTarget_ReturnsTrue_WhenNameIsPttQxT1` — T1 (regression)
- `IsPttQxTarget_ReturnsTrue_WhenNameIsPttQxT3` — T3 (regression)
- `IsPttQxTarget_ReturnsTrue_WhenNameIsPttQxT4` — T4 (new capability)
- `IsPttQxTarget_ReturnsTrue_WhenNameIsPttQxT10` — T10 (multi-digit)
- `IsPttQxTarget_ReturnsFalse_WhenSuffixIsZero` — PTT-QX-T0 rejected
- `IsPttQxTarget_ReturnsFalse_WhenSuffixIsNonNumeric` — PTT-QX-TX rejected

**SCAN coverage:** SCAN-6 (`name.Length != 9` → 0)

---

## 7. Cross-Ticket Dependencies

| Ticket | Depends On | Notes |
|--------|-----------|-------|
| DW-B153-01 | None | Fully independent |
| DW-B153-02 | None | Fully independent |
| DW-B153-03 | None externally | **Internal tight coupling:** TryParseStopSuffix + CaptureOtherLegTargetPrices + ParseTargetLegIndex (NEW) + ResubmitCollateralLegs + HandleAtmStopSync call site MUST be committed together in a single atomic commit |
| DW-B153-04 | None | Fully independent (semantic dependency on T1 for full SIM pass, but no code dependency) |

Tickets 1, 2, 4 are each single-symbol changes in isolated files. They may be executed in any order.  
Ticket 3 is a multi-symbol change in a single file; its internal symbols are tightly coupled by type flow and must be delivered atomically.

---

## 8. NT8 API Surface

No new NT8 API calls are introduced by any ticket. All changes are:
- Pure arithmetic removal (T1)
- Pure list trim removal (T2)
- Data structure upgrade + string parsing extension (T3)
- String predicate replacement (T4)

Confirmed AddOnBase-available APIs used by existing code (unchanged):
- `Account.Cancel(Order)` — cancel existing order
- `Account.CreateOrder(...)` — create new order
- `Order.Submit()` — submit created order
- `Account.Orders` — enumerate account orders (read-only, T3 uses this in CaptureOtherLegTargetPrices)

APIs confirmed NOT used (StrategyBase-only):
- `AtmStrategyChangeStopTarget()` — not used
- `AtmStrategyCreate()` — not used
- `Account.Change()` — not used (silent no-op on ATM-owned brackets)

---

## 9. JS Rule Compliance

| Rule | T1 | T2 | T3 | T4 |
|------|----|----|----|----|
| No `lock()` | PASS | PASS | PASS | PASS |
| No `DateTime.Now` | PASS | PASS | PASS | PASS |
| ASCII-only strings/comments | PASS | PASS | PASS | PASS |
| No FontFamily | PASS | PASS | PASS | PASS |
| No hardcoded hex colors | PASS | PASS | PASS | PASS |
| No throw in dispatch | PASS | PASS | PASS | PASS |
| No mutable struct | PASS | PASS | PASS | PASS |
| CCN <= 8 all methods | PASS (-1) | PASS (-1) | PASS (≤4 new) | PASS (+1, ≤4) |

---

## 10. Scan Checklist (Architect-Specified)

All 10 scans must be run by Phase 4b (ptt-verifier) after implementation. All must return 0.

| # | Command | Expected | Ticket |
|---|---------|----------|--------|
| SCAN-1 | `grep -n "n > 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | DW-B153-03 |
| SCAN-2 | `grep -n "i <= 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | DW-B153-03 |
| SCAN-3 | `grep -n "new double\[3\]" src/PropTraderTools/CopyEngine.cs` | 0 matches | DW-B153-03 |
| SCAN-4 | `grep -n "Math.Min.*3" src/PropTraderTools/Features/PttQuickExit.cs` | 0 matches | DW-B153-01 |
| SCAN-5 | `grep -n "targets.Count > 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | DW-B153-02 |
| SCAN-6 | `grep -n "name.Length != 9" src/PropTraderTools/Features/PttBreakEven.cs` | 0 matches | DW-B153-04 |
| SCAN-7 | `lizard src/PropTraderTools/Features/PttQuickExit.cs src/PropTraderTools/CopyEngine.cs src/PropTraderTools/Features/PttBreakEven.cs --CCN 8` | Warning cnt: 0 | ALL |
| SCAN-8 | `grep -rn "lock\s*(" src/PropTraderTools/` | 0 actual `lock()` blocks | ALL |
| SCAN-9 | ASCII-only byte check on all 3 edited files | 0 bytes > 127 | ALL |
| SCAN-10 | `dotnet build` | 0 errors, 0 warnings | ALL |

---

## 11. Risk Analysis

### What breaks if any ceiling is NOT removed

**DW-B153-01 (QX) not fixed:**
- QX and QX-ALL silently drop T4+ legs
- User observes partial QX execution; no exception, no log, no indication
- Root cause: `Math.Min(raw, 3)` caps the for-loop to 3 iterations

**DW-B153-02 (BE) not fixed:**
- BE and BE-ALL silently drop T4+ targets
- Stop not moved to entry for T4+; risk remains on those legs
- Root cause: `while (targets.Count > 3)` trims before the BE loop runs

**DW-B153-03 (stop drag) not fixed:**
- Dragging stop only resubmits collateral legs 1-3
- T4+ collateral legs remain at original price after drag
- Root cause (3 sites, any one alone breaks it):
  - `|| n > 3` prevents parsing leg-4 stop orders
  - `new double[3]` cannot store prices for legs 4+
  - `for i <= 3` never processes legs 4+

**DW-B153-04 (IsPttQxTarget) not fixed:**
- After QX-ALL with 5+ targets, BE path cannot identify T4/T5 as QX targets
- BE-after-QX broken for T4+; those targets receive non-QX BE treatment
- Root cause: `name.Length != 9` rejects `PTT-QX-T10` (length 10); `name[8] <= '3'` rejects T4-T9

### Regression risks

**T3 partial commit risk:** If `CaptureOtherLegTargetPrices` return type is changed to `Dictionary<int,double>` without updating `ResubmitCollateralLegs` and `HandleAtmStopSync` in the same commit, the build will fail. SCAN-10 (`dotnet build → 0 errors`) is the hard gate. The architect mandates atomic commit for all 5 change points in DW-B153-03.

**Quick2t / QAll2t regression risk:** These paths are intentionally fixed at 2 targets and must NOT be touched. The fixes are all post-entry ceiling removals; the 2-target paths have different code routes and are not reached by any of the 4 fixes.

**DeduplicateByPrice dependency:** Both T1 and T2 rely on `DeduplicateByPrice` to handle residue inflation (the original reason ceilings existed). `DeduplicateByPrice` must remain functional and must be verified in SIM gate steps 2 and 3.

---

## 12. SIM Gate (Director-Owned, Post-F5)

1. Enter position with 5-target ATM template; drag stop → all 5 followers sync
2. Quick ALL → 5 QX bracket pairs (PTT-QX-T1..T5)
3. BE ALL → PTT-BE-Stop-1..5 + PTT-BE-Target-1..5
4. QX ALL, then BE ALL → IsPttQxTarget recognizes T4/T5

**PASS criterion:** All 5 legs processed.  
**FAIL criterion:** Any leg 4/5 silently dropped.

---

## 13. Files In Scope

| File | Tickets |
|------|---------|
| `src/PropTraderTools/Features/PttQuickExit.cs` | DW-B153-01 |
| `src/PropTraderTools/CopyEngine.cs` | DW-B153-02, DW-B153-03 |
| `src/PropTraderTools/Features/PttBreakEven.cs` | DW-B153-04 |

No other files are in scope. No new files are created (ParseTargetLegIndex is added as a private method within CopyEngine.cs, not in a new file).

---

*Plan written by ptt-architect. Ready for ptt-plan-reviewer (Phase 2).*
