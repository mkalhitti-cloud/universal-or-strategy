# WAVE2-LANE-E -- Architecture Plan

**Epic**: WAVE2-LANE-E
**Phase**: 1 (Architecture)
**Status**: PLAN_COMPLETE
**Date**: 2026-09-06
**Architect**: ptt-architect (Phase 1)
**Workspace**: `C:\WSGTA\universal-or-strategy\`
**Branch**: `fix/DW-LB-GR-01-DW-BWAVE-UI-01`
**Brain dir**: `docs/brain/WAVE2-LANE-E/`

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

**Q1. Same method or within 50 lines?**
NO -- 8 violations across 6 different files (PttQuickExit.cs, PttGlobalQuickExit.cs, PttBreakEven.cs, PttBreakEvenSwap.cs, PttFlatten.cs, PttTrim.cs).

**Q2. Does fixing B depend on A?**
Within the same file: PttQuickExit.cs has two violations (Execute + SubmitQxOcoPair) in the same class -- they share the test file but the extractions are independent (no helper extracted from one affects the other). PttGlobalQuickExit.cs has two violations in the same class -- same independence. All 8 extractions produce private helpers; no cross-file dependencies exist.

However, all extractions share a single test file (`BwaveLaneETests.cs`) and the CCN measurement after each extraction must be verified before proceeding. Per LaneC protocol: **SINGLE-PIPELINE** (shared test file and CCN measurement coupling).

**Q3. Standalone value?** YES for all 8 -- each extraction independently reduces its parent to CCN <= 8.

**Q4. Independent SIM verification via lizard?** YES for all 8 -- lizard can report CCN per-method independently.

**GATE RESULT: SINGLE-PIPELINE**

---

## DW-LC-01 Constraint Acknowledgement

Three methods flagged in `docs/brain/BWAVE-REFACTOR/LaneC/06-deferred-backlog.md` as AT-LIMIT (CCN=8) post-LaneC have received new code since LaneC and now exceed CCN=8:

| Method | LaneC Post-CCN | Current CCN | Delta |
|--------|---------------|-------------|-------|
| `PttQuickExit::Execute` | 8 | **17** | +9 |
| `PttGlobalQuickExit::Execute(forcedTargets)` | 8 | **9** | +1 |
| `PttBreakEvenSwap::Execute` | 8 | **9** | +1 |

The architect has read the actual source for all three methods before proposing any extraction (see Section 3 Source Analysis). Extractions are sized to ensure the parent cannot exceed 8 paths after implementation.

---

## 1. Scope

**Goal**: Reduce ALL 8 target methods in `Features/*.cs` to CCN <= 8 via private helper extraction.

**8 violations (lizard confirmed 2026-09-06)**:

| File | Method | CCN | NLOC | Ticket |
|------|--------|-----|------|--------|
| `PttQuickExit.cs` | `Execute` (7-param) | 17 | 75 | E-1 |
| `PttQuickExit.cs` | `SubmitQxOcoPair` | 9 | 34 | E-1 |
| `PttGlobalQuickExit.cs` | `SnapshotTargetOrders` | 13 | 31 | E-2 |
| `PttGlobalQuickExit.cs` | `Execute(forcedTargets)` | 9 | 73 | E-2 |
| `PttBreakEven.cs` | `SnapshotTargetsLocal` | 9 | 31 | E-3 |
| `PttBreakEvenSwap.cs` | `Execute` | 9 | 34 | E-3 |
| `PttFlatten.cs` | `FlattenPositionLocal` | 9 | 68 | E-3 |
| `PttTrim.cs` | `TrimPositionLocal` | 9 | 69 | E-3 |

---

## 2. Extraction Rules (canonical, non-negotiable)

1. Extract guards, loop bodies, and boolean expressions to `private` / `private static` named helpers.
2. NEVER delete logic. Every branch survives verbatim.
3. No `lock()`. No `async void`. No `return null` from new helpers. ASCII-only names.
4. Do not change `public` / `internal` method signatures.
5. Add 1 structural `[Fact]` xUnit test per NEW extracted helper.
6. All helpers: `private static` unless instance state is required (none of the 13 helpers below require instance state).
7. PTT- signal names preserved verbatim -- no new `CreateOrder` calls in extracted helpers that originated in parents.

---

## 3. Source Analysis (per method)

### 3.1 PttQuickExit::Execute (CCN=17 -> target <=8)

**Source location**: `PttQuickExit.cs:39`, 75 NLOC.

**Source reading**: The method body (lines 49-135) contains:

| Branch source | Lizard +1 each | Count |
|---------------|---------------|-------|
| `if (leader != null)` guard before foreach | +1 | 1 |
| `foreach (Position p in leader.Positions)` | +1 | 1 |
| `if (p.Instrument == instr)` | +1 | 1 |
| `if (pos == null || pos.Quantity == 0)` (if + OR) | +2 | 2 |
| `if (skipIfFollower && CopyEngine.Instance?.IsFollowerAccount(leader) == true)` (if + && + ?.) | +3 | 3 |
| `CopyEngine.Instance?.CancelQxBrackets(leader, instr, snapshot)` (?.) | +1 | 1 |
| `instr.MasterInstrument?.TickSize ?? 0.25` (?. + ??) | +2 | 2 |
| `for (int i = 0; ...)` | +1 | 1 |
| `leader != null ? leader.Name : "NULL"` in log (line 61) | +1 | 1 |
| `leader != null ? leader.Name : "NULL"` in log (line 71) | +1 | 1 |
| `double t1Price = isLong ? ... : ...` | +1 | 1 |
| `double t2Price = isLong ? ... : ...` | +1 | 1 |
| Base | +1 | 1 |
| **Total** | | **17** |

**DW-LC-01 check**: This method was at CCN=8 post-LaneC. The +9 new branches come from: `if (leader != null)` guard (B78/B71 safety addition), `?.CancelQxBrackets` null-safe call, `?.TickSize ?? 0.25` tick resolution, two log-line ternaries, and two exit-price ternaries.

**Proposed extraction budget**:
- Extract `IsFlatOrMissing`: removes `if leader!=null`+`foreach`+`if instrument`+`if(pos==null||qty==0)` = 5 from Execute; adds back `if(IsFlatOrMissing)` = +1. Net: **-4**.
- Extract `IsFollowerSkip`: removes `&&` + `?.` = 2; adds `if(IsFollowerSkip)` = +1. Net: **-1**.
- Extract `LeaderName`: called twice; removes 2 ternaries. Net: **-2**.
- Extract `ResolveTick`: removes `?.` + `??` = 2. Net: **-2**.
- Extract `ComputeExitPrices`: removes 2 ternaries. Net: **-2**.

Total removal: 4+1+2+2+2 = **11**. New Execute CCN = 17 - 11 = **6**. PASS (<=8).

### 3.2 PttQuickExit::SubmitQxOcoPair (CCN=9 -> target <=8)

**Source location**: `PttQuickExit.cs:143`, 34 NLOC.

**Branch inventory** (lines 157-182):

| Branch source | Count |
|---------------|-------|
| `(targets != null && i < targets.Count)` (&&) | +1 |
| `? targets[i].Qty : CalcTNQty(...)` (ternary) | +1 |
| `double rawTN = isLong ? ... : ...` (ternary) | +1 |
| `if (tNQty <= 0)` | +1 |
| `CopyEngine.Instance?.NextQxOcoId()` (?.) | +1 |
| `?? ("PTT-QX-" + Guid...)` (??) | +1 |
| `if (i == 0)` firstOcoId | +1 |
| `string stopName = i == 0 ? ... : ...` (ternary) | +1 |
| Base | +1 |
| **Total** | **9** |

**Proposed extraction**:
- Extract `NewQxOcoId()`: removes `?.` + `??` = 2 from SubmitQxOcoPair. Net: **-2**. New CCN = 7. PASS.

### 3.3 PttGlobalQuickExit::SnapshotTargetOrders (CCN=13 -> target <=8)

**Source location**: `PttGlobalQuickExit.cs:421`, 31 NLOC.
**Context**: `IsTargetOrder` and `DeduplicateByPrice` were already extracted in LaneC. Remaining CCN=13 is from the `isNative`/`isPtt` classification expressions inside the forEach body.

**Branch inventory**:

| Branch source | Count |
|---------------|-------|
| `if (acc == null || instr == null)` (if + OR) | +2 |
| `foreach (Order o in acc.Orders)` | +1 |
| `if (o == null) continue` | +1 |
| `if (!IsTargetOrder(o, instr)) continue` | +1 |
| `bool isNative = ...StartsWith && .Length > 6 && IsDigit` (2x &&) | +2 |
| `bool isPtt = (...StartsWith && .Length > 8 && IsDigit) || ...StartsWith` (2x && + 1x OR) | +3 |
| `if (isNative)` | +1 |
| `if (nativeTargets.Count == 0)` | +1 |
| Base | +1 |
| **Total** | **13** |

**Proposed extraction**:
- Extract `IsNativeTargetOrder(string name)`: removes 2x && from caller. Net: **-2**.
- Extract `IsPttTargetOrder(string name)`: removes 2x && + 1x OR from caller. Net: **-3**.

New SnapshotTargetOrders CCN = 13 - 2 - 3 = **8**. AT-LIMIT, PASS.

### 3.4 PttGlobalQuickExit::Execute(forcedTargets) (CCN=9 -> target <=8)

**Source location**: `PttGlobalQuickExit.cs:115`, 73 NLOC.

**DW-LC-01 check**: Was at CCN=8 post-LaneC. The +1 new branch: `if (forcedTargets == null || forcedTargets.Count < 2)` -- the `||` = +1 (the `< 2` threshold change from the older guard adds one OR branch).

**Proposed extraction**:
- Extract `IsInvalidForcedTargets(targets)`: removes `||` = **-1**. New CCN = **8**. AT-LIMIT, PASS.

### 3.5 PttBreakEven::SnapshotTargetsLocal (CCN=9 -> target <=8)

**Source location**: `PttBreakEven.cs:608`, 31 NLOC.
**Context**: `IsSnapshotEligibleState` was already extracted in LaneC. Remaining CCN=9 is from the compound filter line and instrOk boolean.

**Branch inventory** (lines 612-637):

| Branch source | Count |
|---------------|-------|
| `if (acc == null || instr == null)` (if + OR) | +2 |
| `foreach (Order o in acc.Orders)` | +1 |
| `if (o == null) continue` | +1 |
| `instrOk = o.Instrument != null && o.Instrument.FullName == instr.FullName` (&&) | +1 |
| `if (!stateOk || !instrOk || (!IsAtmTargetName(o.Name) && !IsPttQxTarget(o.Name)))` (OR + OR + &&) | +3 |
| Base | +1 |
| **Total** | **9** |

**Proposed extraction**:
- Extract `IsSnapshotTargetOrder(Order o, Instrument instr)`: consolidates instrOk (&&) + name-filter compound (OR OR &&). Removes 4 branches from caller; replaces with `if (!stateOk || !IsSnapshotTargetOrder(o, instr)) continue` = +2. Net: **-2**. New CCN = 7. PASS.

`IsSnapshotTargetOrder` CCN: base(1) + OR(1) [instrOk] + OR(1) [name filter] = **3**. PASS.

### 3.6 PttBreakEvenSwap::Execute (CCN=9 -> target <=8)

**Source location**: `PttBreakEvenSwap.cs:53`, 34 NLOC.

**DW-LC-01 check**: Was at CCN=8 post-LaneC. Branch inventory from source:

| Branch source | Count |
|---------------|-------|
| `if (acc == null || instr == null)` (if + OR) | +2 |
| `if (pos == null || pos.Quantity == 0)` (if + OR) | +2 |
| `bool isLong` (ternary for stopDir) | +1 |
| `if (targets == null || targets.Count == 0)` (if + OR) | +2 |
| `for (int i = 0; ...)` | +1 |
| Base | +1 |
| **Total** | **9** |

**Proposed extraction**:
- Extract `HasNoTargets(targets)`: removes OR from `targets == null || targets.Count == 0`. Net: **-1**. New CCN = **8**. AT-LIMIT, PASS.

### 3.7 PttFlatten::FlattenPositionLocal (CCN=9 -> target <=8)

**Source location**: `PttFlatten.cs:85`, 68 NLOC.
**Context**: `ResolveOrderParams` was already extracted in LaneC. The remaining CCN=9 (not the CCN=6 predicted) is because the log-line ternary was not counted in LaneC's projection.

**Branch inventory** (lines 85-155):

| Branch source | Count |
|---------------|-------|
| `if (acc == null || instr == null || pos == null)` (if + 2x OR) | +3 |
| direction ternary (MarketPosition.Long ?) | +1 |
| `ResolveOrderParams` call | +0 |
| try/catch | +1 |
| `if (order != null)` | +1 |
| `orderType == OrderType.Limit ? limitPrice.ToString("F2") : "mkt"` (ternary in log) | +1 |
| `(acc != null ? acc.Name : "null")` ternary in catch | +1 |
| Base | +1 |
| **Total** | **9** |

**Proposed extraction**:
- Extract `FormatOrderPrice(OrderType orderType, double limitPrice)`: removes log-line ternary. Net: **-1**. New CCN = **8**. AT-LIMIT, PASS.

### 3.8 PttTrim::TrimPositionLocal (CCN=9 -> target <=8)

**Source location**: `PttTrim.cs:94`, 69 NLOC.
**Context**: Structurally identical to FlattenPositionLocal. Same CCN=9 cause (log-line ternary).

**Proposed extraction**:
- Extract `FormatOrderPrice(OrderType orderType, double limitPrice)`: same body as PttFlatten helper (see DW-LE-01). Net: **-1**. New CCN = **8**. AT-LIMIT, PASS.

---

## 4. Extraction Design (exact helper signatures)

### Ticket E-1: PttQuickExit.cs

| Helper | Signature | CYC | Description |
|--------|-----------|-----|-------------|
| `IsFlatOrMissing` | `private static bool IsFlatOrMissing(Account leader, Instrument instr, out Position pos)` | 4 | Iterates `leader.Positions`; sets `pos`; returns `true` when not found or `pos.Quantity == 0`. Removes 4 branches from Execute. |
| `IsFollowerSkip` | `private static bool IsFollowerSkip(bool skipIfFollower, Account leader)` | 3 | `return skipIfFollower && CopyEngine.Instance?.IsFollowerAccount(leader) == true;` Removes && + ?. from Execute. |
| `LeaderName` | `private static string LeaderName(Account leader)` | 2 | `return leader != null ? leader.Name : "NULL";` Replaces two inline ternaries in Output.Process log calls. |
| `ResolveTick` | `private static double ResolveTick(Instrument instr)` | 3 | `return instr.MasterInstrument?.TickSize ?? 0.25;` Removes ?. + ?? from Execute. |
| `ComputeExitPrices` | `private static (double t1Price, double t2Price) ComputeExitPrices(double entryPx, bool isLong, int t1Ticks, double tick)` | 3 | Computes t1Price and t2Price ternaries. Removes 2 ternaries from Execute. |
| `NewQxOcoId` | `private static string NewQxOcoId()` | 3 | `return CopyEngine.Instance?.NextQxOcoId() ?? ("PTT-QX-" + Guid.NewGuid().ToString("N").Substring(0, 8));` Removes ?. + ?? from SubmitQxOcoPair. |

**Projected CCN post-extraction**:
- `Execute`: 17 - 11 = **6**
- `SubmitQxOcoPair`: 9 - 2 = **7**

### Ticket E-2: PttGlobalQuickExit.cs

| Helper | Signature | CYC | Description |
|--------|-----------|-----|-------------|
| `IsNativeTargetOrder` | `private static bool IsNativeTargetOrder(string name)` | 4 | `!string.IsNullOrEmpty(name) && name.Length > 6 && name.StartsWith("Target", StringComparison.Ordinal) && char.IsDigit(name[6])` -- NOTE: intentionally omits `name[6] != '0'` guard (preserves existing behavior verbatim). See RISK-LE-04. |
| `IsPttTargetOrder` | `private static bool IsPttTargetOrder(string name)` | 5 | `if (string.IsNullOrEmpty(name)) return false; return (name.StartsWith("PTT-QX-T", StringComparison.Ordinal) && name.Length > 8 && char.IsDigit(name[8])) || name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal);` Removes 2x && + 1x OR from SnapshotTargetOrders. |
| `IsInvalidForcedTargets` | `private static bool IsInvalidForcedTargets(System.Collections.Generic.List<(double Price, int Qty)> targets)` | 2 | `return targets == null || targets.Count < 2;` Removes OR from Execute(forcedTargets). |

**Projected CCN post-extraction**:
- `SnapshotTargetOrders`: 13 - 5 = **8** (AT-LIMIT)
- `Execute(forcedTargets)`: 9 - 1 = **8** (AT-LIMIT)

### Ticket E-3: PttBreakEven.cs + PttBreakEvenSwap.cs + PttFlatten.cs + PttTrim.cs

| File | Helper | Signature | CYC | Description |
|------|--------|-----------|-----|-------------|
| `PttBreakEven.cs` | `IsSnapshotTargetOrder` | `private static bool IsSnapshotTargetOrder(Order o, Instrument instr)` | 3 | Checks instrument match AND name filter (`IsAtmTargetName || IsPttQxTarget`). Removes && + 2x OR from SnapshotTargetsLocal compound filter. |
| `PttBreakEvenSwap.cs` | `HasNoTargets` | `private static bool HasNoTargets(System.Collections.Generic.List<(double Price, int Qty, OrderAction Action)> targets)` | 2 | `return targets == null || targets.Count == 0;` Removes OR from Execute. |
| `PttFlatten.cs` | `FormatOrderPrice` | `private static string FormatOrderPrice(OrderType orderType, double limitPrice)` | 2 | `return orderType == OrderType.Limit ? limitPrice.ToString("F2") : "mkt";` Removes log-line ternary from FlattenPositionLocal. |
| `PttTrim.cs` | `FormatOrderPrice` | `private static string FormatOrderPrice(OrderType orderType, double limitPrice)` | 2 | Identical body. Removes log-line ternary from TrimPositionLocal. See DW-LE-01. |

**Projected CCN post-extraction**:
- `PttBreakEven::SnapshotTargetsLocal`: 9 - 2 = **7**
- `PttBreakEvenSwap::Execute`: 9 - 1 = **8** (AT-LIMIT)
- `PttFlatten::FlattenPositionLocal`: 9 - 1 = **8** (AT-LIMIT)
- `PttTrim::TrimPositionLocal`: 9 - 1 = **8** (AT-LIMIT)

---

## 5. Ticket Structure

### Ticket E-1: PttQuickExit.cs (2 violations, 6 helpers)

**File**: `src/PropTraderTools/Features/PttQuickExit.cs`
**Violations**: `Execute` (CCN=17 -> 6) + `SubmitQxOcoPair` (CCN=9 -> 7)
**New helpers**: `IsFlatOrMissing`, `IsFollowerSkip`, `LeaderName`, `ResolveTick`, `ComputeExitPrices`, `NewQxOcoId`
**Rationale**: Most complex extraction (CCN=17). Isolated to one file to limit engineer cognitive load.

### Ticket E-2: PttGlobalQuickExit.cs (2 violations, 3 helpers)

**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**Violations**: `SnapshotTargetOrders` (CCN=13 -> 8) + `Execute(forcedTargets)` (CCN=9 -> 8)
**New helpers**: `IsNativeTargetOrder`, `IsPttTargetOrder`, `IsInvalidForcedTargets`
**Rationale**: Same file, two violations. Helpers are pure string predicates with no interaction between them.

### Ticket E-3: 4 files (4 violations, 4 helpers)

**Files**: `PttBreakEven.cs`, `PttBreakEvenSwap.cs`, `PttFlatten.cs`, `PttTrim.cs`
**Violations**: SnapshotTargetsLocal (9->7), Execute (9->8), FlattenPositionLocal (9->8), TrimPositionLocal (9->8)
**New helpers**: `IsSnapshotTargetOrder` (BreakEven), `HasNoTargets` (BreakEvenSwap), `FormatOrderPrice` (Flatten), `FormatOrderPrice` (Trim)
**Rationale**: All are simple CCN=9->8 extractions, each requiring exactly 1 helper. Structurally similar; safe to batch.

---

## 6. Architecture Constraints

### JS Rules (mandatory)

| Rule | Application |
|------|-------------|
| JS-021 (no lock) | Zero new `lock()` statements. All new helpers are pure functions or read-only property access. |
| JS-001 (no throw in hot path) | No new `throw new XxxException`. All existing try/catch blocks remain at original call sites. |
| JS-002 (no return null) | `IsFlatOrMissing` uses `out Position pos` (TryXxx pattern -- returns bool, never null). All other helpers return bool, double, string, or value-tuple -- never null. |
| JS-033 (no async void) | All new helpers are synchronous. |
| NT8-014 (PTT- signal names) | No new `CreateOrder` calls in extracted helpers. All existing CreateOrder call sites unchanged. Signal names preserved verbatim. |
| NT8-049 (arg6/arg7 never swap) | Not affected. All CreateOrder calls remain in their existing helpers. |
| NT8-007 (arg11 cast) | Not affected. |
| NT8-013 (DateTime.MaxValue) | Not affected. |

### NT8 AddOn Constraints (architectural -- confirmed from NT8_FULL_REFERENCE.md)

- `AtmStrategyChangeStopTarget()` -- StrategyBase-ONLY. NOT used in any of these 8 methods.
- `AtmStrategyCreate()` -- StrategyBase-ONLY. NOT used.
- `Account.Change()` -- available but silent no-op on ATM-owned brackets. NOT used.
- `Account.Cancel() + Account.CreateOrder() + Submit()` -- AddOnBase pattern. Used in existing helpers; not touched by Lane-E extractions.

---

## 7. Test Coverage Plan

**Test file**: `src/PropTraderTools/Tests/BwaveLaneETests.cs` (NEW FILE)
**Framework**: xUnit only -- `using Xunit; using System.Reflection; Assert.NotNull; Assert.Equal; Assert.True; Assert.False`
**NUnit/MSTest**: BANNED.

One `[Fact]` per extracted helper (structural existence + parameter count via reflection):

| Test Name | Asserts |
|-----------|---------|
| `PttQuickExit_IsFlatOrMissing_Exists` | `GetMethod("IsFlatOrMissing", NonPublic+Static)` != null; param count = 3 (Account, Instrument, out Position) |
| `PttQuickExit_IsFollowerSkip_Exists` | `GetMethod("IsFollowerSkip", ...)` != null; param count = 2 |
| `PttQuickExit_LeaderName_Exists` | `GetMethod("LeaderName", ...)` != null; param count = 1; returns string |
| `PttQuickExit_ResolveTick_Exists` | `GetMethod("ResolveTick", ...)` != null; param count = 1; returns double |
| `PttQuickExit_ComputeExitPrices_Exists` | `GetMethod("ComputeExitPrices", ...)` != null; param count = 4 |
| `PttQuickExit_NewQxOcoId_Exists` | `GetMethod("NewQxOcoId", ...)` != null; param count = 0; returns string |
| `PttGlobalQuickExit_IsNativeTargetOrder_Exists` | `GetMethod("IsNativeTargetOrder", ...)` != null; param count = 1 |
| `PttGlobalQuickExit_IsPttTargetOrder_Exists` | `GetMethod("IsPttTargetOrder", ...)` != null; param count = 1 |
| `PttGlobalQuickExit_IsInvalidForcedTargets_Exists` | `GetMethod("IsInvalidForcedTargets", ...)` != null; param count = 1 |
| `PttBreakEven_IsSnapshotTargetOrder_Exists` | `GetMethod("IsSnapshotTargetOrder", ...)` != null; param count = 2 |
| `PttBreakEvenSwap_HasNoTargets_Exists` | `GetMethod("HasNoTargets", ...)` != null; param count = 1 |
| `PttFlatten_FormatOrderPrice_Exists` | `GetMethod("FormatOrderPrice", ...)` on `PttFlatten` != null; param count = 2 |
| `PttTrim_FormatOrderPrice_Exists` | `GetMethod("FormatOrderPrice", ...)` on `PttTrim` != null; param count = 2 |

**Total**: 13 `[Fact]` tests.

---

## 8. Risk Register

### RISK-LE-01: IsFlatOrMissing out-parameter -- JS-002 interaction

**Severity**: P1
**Description**: `IsFlatOrMissing` sets `out Position pos = null` when no position found. The helper returns bool (never null). Execute call site: `if (IsFlatOrMissing(leader, instr, out Position pos)) return;` -- when true (flat), returns early; when false, `pos` is guaranteed non-null with Quantity > 0.
**Mitigation**: The `out` parameter pattern (TryXxx) is standard C# and satisfies JS-002 (which applies to return values, not out parameters). Engineer test must exercise both return paths.

### RISK-LE-02: AT-LIMIT Methods Post-Extraction

**Severity**: P1
**Description**: After Lane-E, the following methods are at CCN=8 exactly:
- `PttGlobalQuickExit::SnapshotTargetOrders` (CCN=8)
- `PttGlobalQuickExit::Execute(forcedTargets)` (CCN=8)
- `PttBreakEvenSwap::Execute` (CCN=8)
- `PttFlatten::FlattenPositionLocal` (CCN=8)
- `PttTrim::TrimPositionLocal` (CCN=8)
Any future branch addition to these methods violates CCN<=8 without a prior extraction review.
**Mitigation**: Registered as DW-LE-02.

### RISK-LE-03: FormatOrderPrice Duplication

**Severity**: P2
**Description**: `FormatOrderPrice` in `PttFlatten` and `PttTrim` are structurally identical. Deferred to DW-LE-01.

### RISK-LE-04: IsNativeTargetOrder omits name[6] != '0' guard

**Severity**: P2
**Description**: The existing `isNative` expression in `SnapshotTargetOrders` does NOT have a `name[6] != '0'` guard (unlike `PttBreakEven::IsAtmTargetName`). `IsNativeTargetOrder` must preserve this behavior verbatim -- do NOT add the guard.
**Mitigation**: Note in the helper's doc comment: "Intentionally omits name[6] != '0' guard -- preserves existing SnapshotTargetOrders behavior."

### RISK-LE-05: IsPttTargetOrder scope vs existing helpers

**Severity**: P2
**Description**: `IsPttTargetOrder` covers BOTH QX targets AND BE targets (union). This is broader than `PttBreakEven::IsPttQxTarget` (QX only). Name is intentionally broader. Helper is `private static` in `PttGlobalQuickExit` only.

---

## 9. Non-Goals

- No changes to `public` or `internal` method signatures.
- No changes to behavior -- pure structural extraction.
- No changes outside `Features/*.cs` (six target files) and `Tests/BwaveLaneETests.cs`.
- No changes to `CopyEngine.cs`, `TradeCopierPanel.cs`, `TradeCopierWindow.cs`.
- No resolution of DW-LC-02 (ResolveOrderParams dedup), DW-LC-03 (FindPositionLocal null return), or any open LaneC DW item in this lane.

---

## 10. Build / Sync / CCN Verification

After each ticket:
1. `dotnet build src\PropTraderTools\PropTraderTools.csproj` -- 0 errors, 0 warnings
2. 7-scan checklist (SCAN-01 through SCAN-07 -- see tickets)
3. `lizard src/PropTraderTools/Features/*.cs -C 8` -- 0 rows CCN > 8
4. `powershell -File scripts\ptt-sync-and-verify.ps1` -- 18/18 OK, 0 MISMATCH
5. F5 in NinjaTrader 8 (compile gate -- mandatory before merge)

---

## 11. Deferred Items

| ID | Item | Priority | Status |
|----|------|----------|--------|
| DW-LE-01 | **FormatOrderPrice duplication**: `PttFlatten.FormatOrderPrice` and `PttTrim.FormatOrderPrice` are structurally identical. Future wave should extract to shared `PttOrderUtils` utility (same pattern as DW-LC-02 for ResolveOrderParams). Non-blocking. | P2 | OPEN |
| DW-LE-02 | **AT-LIMIT methods (CCN=8 exactly) post-Lane-E**: `PttGlobalQuickExit::SnapshotTargetOrders`, `PttGlobalQuickExit::Execute(forcedTargets)`, `PttBreakEvenSwap::Execute`, `PttFlatten::FlattenPositionLocal`, `PttTrim::TrimPositionLocal` are each at CCN=8 after extraction. Any future branch addition requires a prior extraction review (same protocol as DW-LC-01). | P1 | OPEN |

---

**PLAN_COMPLETE**