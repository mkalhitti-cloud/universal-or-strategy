# WAVE2-LANE-E -- Implementation Tickets

**Epic**: WAVE2-LANE-E
**Phase**: 3 (Ticket Generation)
**Status**: TICKETS_COMPLETE
**Date**: 2026-09-06
**Architect**: ptt-architect (Phase 3)
**Plan Source**: docs/brain/WAVE2-LANE-E/02-architecture-plan.md (REVIEW_PASS)
**Plan Review**: docs/brain/WAVE2-LANE-E/02-plan-review.md (REVIEW_PASS)
**Workspace**: `C:\WSGTA\universal-or-strategy\`
**Branch**: `fix/DW-LB-GR-01-DW-BWAVE-UI-01`

---

## Ticket Overview

| Ticket | File(s) | Target Methods | Current CCN | Projected CCN | Helpers |
|--------|---------|----------------|-------------|---------------|---------|
| E-1 | PttQuickExit.cs | Execute, SubmitQxOcoPair | 17, 9 | 6, 7 | 6 |
| E-2 | PttGlobalQuickExit.cs | SnapshotTargetOrders, Execute(forcedTargets) | 13, 9 | 8, 8 | 3 |
| E-3 | PttBreakEven.cs + PttBreakEvenSwap.cs + PttFlatten.cs + PttTrim.cs | SnapshotTargetsLocal, Execute, FlattenPositionLocal, TrimPositionLocal | 9, 9, 9, 9 | 7, 8, 8, 8 | 4 |

**Total**: 3 tickets, 8 target methods, 13 extracted helpers, 1 new test file (BwaveLaneETests.cs).

**Ordering Constraint**: SEQUENTIAL. E-1 must reach 4a PASS + 4b PASS before E-2 starts.
E-2 must reach 4a PASS + 4b PASS before E-3 starts. All tickets share one test file and
one lizard scan baseline -- serialized execution is mandatory.

---

## TICKET E-1: PttQuickExit Complexity Reduction

**Spec Req IDs**: WAVE2-LANE-E-1
**File**: `src/PropTraderTools/Features/PttQuickExit.cs`
**Test File**: `src/PropTraderTools/Tests/BwaveLaneETests.cs` -- CREATE NEW in this ticket

### Target Methods

| Method | Current CCN | Projected CCN | Status |
|--------|-------------|---------------|--------|
| `Execute` (7-param) | 17 | 6 | AT-LIMIT post-LaneC; +9 delta; headroom=2 post-E-1 |
| `SubmitQxOcoPair` | 9 | 7 | No prior AT-LIMIT constraint |

---

### Method Signatures for Extracted Helpers

All six helpers are `private static` members of the `PttQuickExit` class.

```csharp
private static bool IsFlatOrMissing(Account leader, Instrument instr, out Position pos);
private static bool IsFollowerSkip(bool skipIfFollower, Account leader);
private static string LeaderName(Account leader);
private static double ResolveTick(Instrument instr);
private static (double t1Price, double t2Price) ComputeExitPrices(
    double entryPx, bool isLong, int t1Ticks, double tick);
private static string NewQxOcoId();
```

Helper intent:
- `IsFlatOrMissing`: Iterates leader.Positions; sets pos; returns true when not found or qty=0. Removes 5 branches from Execute.
- `IsFollowerSkip`: `return skipIfFollower && CopyEngine.Instance?.IsFollowerAccount(leader) == true;` Removes && + ?. from Execute.
- `LeaderName`: `return leader != null ? leader.Name : "NULL";` Replaces 2 inline ternaries in log calls.
- `ResolveTick`: `return instr.MasterInstrument?.TickSize ?? 0.25;` Removes ?. + ?? from Execute.
- `ComputeExitPrices`: Computes t1Price and t2Price ternaries. Removes 2 ternaries from Execute.
- `NewQxOcoId`: `return CopyEngine.Instance?.NextQxOcoId() ?? ("PTT-QX-" + Guid.NewGuid().ToString("N").Substring(0, 8));` Removes ?. + ?? from SubmitQxOcoPair.

---

### Extraction Instructions

#### Execute (CCN 17 -> 6)

1. **Extract IsFlatOrMissing** (net -4 from Execute):
   - MOVE: `if (leader != null)` guard + `foreach (Position p in leader.Positions)` +
     `if (p.Instrument == instr)` + `if (pos == null || pos.Quantity == 0)` = 5 branches removed.
   - KEEP: `if (IsFlatOrMissing(leader, instr, out Position pos)) return;` (1 new if = +1).
   - Net: -4. Execute CCN: 17 -> 13.

2. **Extract IsFollowerSkip** (net -1 from Execute):
   - MOVE: the `&&` and `?.` operators inside the existing follower-skip `if` = 2 branches removed.
   - KEEP: `if (IsFollowerSkip(skipIfFollower, leader)) return;` (the if branch persists = +1).
   - Net: -1. Execute CCN: 13 -> 12.

3. **Extract LeaderName** (net -2 from Execute):
   - MOVE: Two inline `leader != null ? leader.Name : "NULL"` ternary expressions in Output.Process log calls.
   - REPLACE both with `LeaderName(leader)`.
   - Net: -2. Execute CCN: 12 -> 10.

4. **Extract ResolveTick** (net -2 from Execute):
   - MOVE: `instr.MasterInstrument?.TickSize ?? 0.25` -- the ?. and ?? = 2 branches.
   - REPLACE with `double tick = ResolveTick(instr);`
   - Net: -2. Execute CCN: 10 -> 8.

5. **Extract ComputeExitPrices** (net -2 from Execute):
   - MOVE: `double t1Price = isLong ? ... : ...` and `double t2Price = isLong ? ... : ...` = 2 ternaries.
   - REPLACE with `var (t1Price, t2Price) = ComputeExitPrices(entryPx, isLong, t1Ticks, tick);`
   - Net: -2. Execute CCN: 8 -> **6**. Headroom = 2.

#### SubmitQxOcoPair (CCN 9 -> 7)

6. **Extract NewQxOcoId** (net -2 from SubmitQxOcoPair):
   - MOVE: `CopyEngine.Instance?.NextQxOcoId() ?? ("PTT-QX-" + Guid.NewGuid().ToString("N").Substring(0, 8))` -- ?. and ?? = 2 branches.
   - REPLACE with `string ocoId = NewQxOcoId();`
   - Net: -2. SubmitQxOcoPair CCN: 9 -> **7**.

---

### DW-LC-01 Note (Execute)

`PttQuickExit::Execute` was AT-LIMIT (CCN=8) post-LaneC per DW-LC-01. Since LaneC, 9 new
branches were added (delta +9), bringing it to CCN=17. Post-E-1: CCN=6 (headroom=2).
**DW-LC-01 constraint LIFTED for this method. Future additions have 2 branches of headroom.**

---

### JS Rule Constraints

- **JS-021**: No `lock()` in any extracted helper. All 6 are pure functions. HARD BLOCK if violated.
- **JS-001**: No `throw new XxxException`. Errors expressed as bool returns. HARD BLOCK if violated.
- **JS-002**: No `return null`. `IsFlatOrMissing` uses `out Position pos` (TryXxx pattern -- bool return).
  `LeaderName` returns string literal `"NULL"` (not null). `NewQxOcoId` returns `"PTT-QX-"` fallback. HARD BLOCK if violated.
- **JS-033**: No `async void`. All helpers synchronous. HARD BLOCK if violated.
- **CYC constraint (per plan)**: each extracted helper must have cyclomatic complexity <=8 (Jane Street standard, verified by lizard)

### NT8 Constraints

- PTT- signal names preserved verbatim. No new `Account.CreateOrder` calls in any helper.
- All existing `CreateOrder` call sites remain unchanged in `SubmitQxOcoPair`.
- `NewQxOcoId` fallback string MUST start with `"PTT-QX-"` -- preserve exact prefix.
- `AtmStrategyChangeStopTarget()` / `AtmStrategyCreate()` / `Account.Change()` -- NOT used.

---

### 7-Scan Checklist (Engineer Contract -- E-1)

```
SCAN-1: lizard src/PropTraderTools/Features/ --CCN 8 -x "*/obj/*" -x "*/bin/*" -x "*/Tests/*"
         Expected: Warning count REDUCED vs pre-E-1 baseline.
         Execute(CCN17) and SubmitQxOcoPair(CCN9) must not appear.

SCAN-2: grep -rn "lock\s*(" src/PropTraderTools/Features/
         Expected: 0 results.

SCAN-3: [System.IO.File]::ReadAllBytes("src/PropTraderTools/Features/PttQuickExit.cs") | Where-Object { $_ -gt 127 }
         Expected: 0 bytes > 127.

SCAN-4: dotnet build src/PropTraderTools/PropTraderTools.csproj
         Expected: 0 errors, 0 warnings.

SCAN-5: dotnet test src/PropTraderTools/
         Expected: All 6 new [Fact] tests PASS. 0 regressions.

SCAN-6: grep -rn "PTT-" src/PropTraderTools/Features/PttQuickExit.cs
         Expected: All PTT- prefixed signal names unchanged.
         Verify "PTT-QX-" prefix in NewQxOcoId fallback string.

SCAN-7: lizard src/PropTraderTools/Features/PttQuickExit.cs -C 8
         Execute CCN = 6 (<=8 PASS). SubmitQxOcoPair CCN = 7 (<=8 PASS).
```

All 7 scans MUST PASS before engineer writes ticket-1-completion.md.

---

### Test Coverage (E-1)

**File**: `src/PropTraderTools/Tests/BwaveLaneETests.cs` -- CREATE NEW FILE.

```csharp
using Xunit;
using System.Reflection;

namespace PropTraderTools.Tests
{
    public class BwaveLaneETests
    {
        private static readonly BindingFlags NonPublicStatic =
            BindingFlags.NonPublic | BindingFlags.Static;

        [Fact]
        public void PttQuickExit_IsFlatOrMissing_Exists()
        {
            var m = typeof(NinjaTrader.NinjaScript.AddOns.PttQuickExit)
                .GetMethod("IsFlatOrMissing", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(3, m.GetParameters().Length);
            Assert.Equal(typeof(bool), m.ReturnType);
        }

        [Fact]
        public void PttQuickExit_IsFollowerSkip_Exists()
        {
            var m = typeof(NinjaTrader.NinjaScript.AddOns.PttQuickExit)
                .GetMethod("IsFollowerSkip", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(2, m.GetParameters().Length);
            Assert.Equal(typeof(bool), m.ReturnType);
        }

        [Fact]
        public void PttQuickExit_LeaderName_Exists()
        {
            var m = typeof(NinjaTrader.NinjaScript.AddOns.PttQuickExit)
                .GetMethod("LeaderName", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
            Assert.Equal(typeof(string), m.ReturnType);
        }

        [Fact]
        public void PttQuickExit_ResolveTick_Exists()
        {
            var m = typeof(NinjaTrader.NinjaScript.AddOns.PttQuickExit)
                .GetMethod("ResolveTick", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
            Assert.Equal(typeof(double), m.ReturnType);
        }

        [Fact]
        public void PttQuickExit_ComputeExitPrices_Exists()
        {
            var m = typeof(NinjaTrader.NinjaScript.AddOns.PttQuickExit)
                .GetMethod("ComputeExitPrices", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(4, m.GetParameters().Length);
            Assert.True(m.ReturnType.IsValueType);
        }

        [Fact]
        public void PttQuickExit_NewQxOcoId_Exists()
        {
            var m = typeof(NinjaTrader.NinjaScript.AddOns.PttQuickExit)
                .GetMethod("NewQxOcoId", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(0, m.GetParameters().Length);
            Assert.Equal(typeof(string), m.ReturnType);
        }
    }
}
```

---

### Acceptance Criteria (E-1)

- [ ] `Execute` CCN = 6 (SCAN-7)
- [ ] `SubmitQxOcoPair` CCN = 7 (SCAN-7)
- [ ] All 6 helpers exist as `private static` in `PttQuickExit`
- [ ] All 7 scans PASS
- [ ] `BwaveLaneETests.cs` created with 6 [Fact] tests, all PASS
- [ ] Engineer writes `docs/brain/WAVE2-LANE-E/ticket-1-completion.md` with scan results

**DO NOT START E-2 until ticket-1-completion.md is written and 4b verification passes.**

---

## TICKET E-2: PttGlobalQuickExit Complexity Reduction

**Spec Req IDs**: WAVE2-LANE-E-2
**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**Test File**: `src/PropTraderTools/Tests/BwaveLaneETests.cs` -- APPEND to existing file (3 new tests)

### Target Methods

| Method | Current CCN | Projected CCN | Status |
|--------|-------------|---------------|--------|
| `SnapshotTargetOrders` | 13 | 8 | AT-LIMIT post-extraction (DW-LE-02) |
| `Execute(forcedTargets)` | 9 | 8 | AT-LIMIT post-LaneC (+1 delta); AT-LIMIT post-E-2 (DW-LE-02) |

---

### Method Signatures for Extracted Helpers

All three helpers are `private static` members of the `PttGlobalQuickExit` class.

```csharp
// Removes 2x && from SnapshotTargetOrders.
// RISK-LE-04: intentionally omits name[6] != '0' guard -- preserves existing behavior verbatim.
private static bool IsNativeTargetOrder(string name);

// Removes 2x && + 1x OR from SnapshotTargetOrders.
// Covers PTT-QX-T naming AND PTT-BE-Target- naming (union).
private static bool IsPttTargetOrder(string name);

// Removes 1x OR from Execute(forcedTargets).
private static bool IsInvalidForcedTargets(
    System.Collections.Generic.List<(double Price, int Qty)> targets);
```

Helper bodies (reference):
- `IsNativeTargetOrder(string name)`: `return !string.IsNullOrEmpty(name) && name.StartsWith("Target", StringComparison.Ordinal) && name.Length > 6 && char.IsDigit(name[6]);`
- `IsPttTargetOrder(string name)`: `if (string.IsNullOrEmpty(name)) return false; return (name.StartsWith("PTT-QX-T", StringComparison.Ordinal) && name.Length > 8 && char.IsDigit(name[8])) || name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal);`
- `IsInvalidForcedTargets(targets)`: `return targets == null || targets.Count < 2;`

---

### Extraction Instructions

#### SnapshotTargetOrders (CCN 13 -> 8)

1. **Extract IsNativeTargetOrder** (net -2 from SnapshotTargetOrders):
   - CURRENT: `bool isNative = name.StartsWith("Target", StringComparison.Ordinal) && name.Length > 6 && char.IsDigit(name[6]);` (2x &&).
   - REPLACE with: `bool isNative = IsNativeTargetOrder(o.Name);`
   - Net: -2. CCN: 13 -> 11.
   - **RISK-LE-04**: Do NOT add `name[6] != '0'` guard. Omission is intentional.

2. **Extract IsPttTargetOrder** (net -3 from SnapshotTargetOrders):
   - CURRENT: `bool isPtt = (name.StartsWith("PTT-QX-T", ...) && name.Length > 8 && char.IsDigit(name[8])) || name.StartsWith("PTT-BE-Target-", ...);` (2x && + 1x OR).
   - REPLACE with: `bool isPtt = IsPttTargetOrder(o.Name);`
   - Net: -3. CCN: 11 -> **8**. AT-LIMIT (DW-LE-02).

#### Execute(forcedTargets) (CCN 9 -> 8)

3. **Extract IsInvalidForcedTargets** (net -1 from Execute):
   - CURRENT: `if (forcedTargets == null || forcedTargets.Count < 2) return;` (the OR = 1 branch).
   - REPLACE with: `if (IsInvalidForcedTargets(forcedTargets)) return;`
   - Net: -1. CCN: 9 -> **8**. AT-LIMIT (DW-LE-02).

---

### DW-LC-01 Note (Execute)

`PttGlobalQuickExit::Execute(forcedTargets)` was AT-LIMIT (CCN=8) post-LaneC per DW-LC-01.
A single `||` was added since LaneC (delta +1), bringing it to CCN=9. Post-E-2: CCN=8.
**Method returns to AT-LIMIT status. DW-LE-02 applies.**

---

### JS Rule Constraints

- **JS-021**: No `lock()`. All 3 helpers are pure string/list predicates. HARD BLOCK if violated.
- **JS-001**: No `throw new XxxException`. HARD BLOCK if violated.
- **JS-002**: No `return null`. All helpers return bool. `IsPttTargetOrder` returns `false` for null input. HARD BLOCK if violated.
- **JS-033**: No `async void`. HARD BLOCK if violated.
- **CYC constraint (per plan)**: each extracted helper must have cyclomatic complexity <=8 (Jane Street standard, verified by lizard)

### NT8 Constraints

- PTT- signal names preserved verbatim. No new `CreateOrder` calls in any helper.
- `IsNativeTargetOrder` and `IsPttTargetOrder`: string-only logic. No NT8 API calls.
- `IsInvalidForcedTargets`: list-only logic. No NT8 API calls.

---

### 7-Scan Checklist (Engineer Contract -- E-2)

```
SCAN-1: lizard src/PropTraderTools/Features/ --CCN 8 -x "*/obj/*" -x "*/bin/*" -x "*/Tests/*"
         Expected: Warning count reduced vs E-1. (This is not the final ticket.)

SCAN-2: grep -rn "lock\s*(" src/PropTraderTools/Features/ -- expected: 0 results

SCAN-3: powershell -c "[System.IO.File]::ReadAllBytes('src/PropTraderTools/Features/PttGlobalQuickExit.cs') | Where-Object { $_ -gt 127 } | Measure-Object"
         Expected: Count = 0

SCAN-4: dotnet build -- expected: 0 errors

SCAN-5: dotnet test -- expected: all prior tests pass (0 regressions)

SCAN-6: grep -rn "PTT-" src/PropTraderTools/Features/PttGlobalQuickExit.cs
         Expected: all PTT- signal prefixes preserved

SCAN-7: lizard src/PropTraderTools/Features/PttGlobalQuickExit.cs -C 8
         Expected: 0 warnings (both SnapshotTargetOrders and Execute below CCN 9)
```

All 7 scans MUST PASS before engineer writes ticket-2-completion.md.

---

### Test Coverage (E-2)

**File**: `src/PropTraderTools/Tests/BwaveLaneETests.cs` -- APPEND these 3 [Fact] methods to the
existing `BwaveLaneETests` class. Do NOT create a new file or a new class.

```csharp
        [Fact]
        public void PttGlobalQuickExit_IsNativeTargetOrder_Exists()
        {
            var m = typeof(NinjaTrader.NinjaScript.AddOns.PttGlobalQuickExit)
                .GetMethod("IsNativeTargetOrder", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
            Assert.Equal(typeof(bool), m.ReturnType);
        }

        [Fact]
        public void PttGlobalQuickExit_IsPttTargetOrder_Exists()
        {
            var m = typeof(NinjaTrader.NinjaScript.AddOns.PttGlobalQuickExit)
                .GetMethod("IsPttTargetOrder", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
            Assert.Equal(typeof(bool), m.ReturnType);
        }

        [Fact]
        public void PttGlobalQuickExit_IsInvalidForcedTargets_Exists()
        {
            var m = typeof(NinjaTrader.NinjaScript.AddOns.PttGlobalQuickExit)
                .GetMethod("IsInvalidForcedTargets", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
            Assert.Equal(typeof(bool), m.ReturnType);
        }
```

---

### Acceptance Criteria (E-2)

- [ ] `SnapshotTargetOrders` CCN = 8 (SCAN-7)
- [ ] `Execute(forcedTargets)` CCN = 8 (SCAN-7)
- [ ] All 3 helpers exist as `private static` in `PttGlobalQuickExit`
- [ ] `IsNativeTargetOrder` does NOT contain `name[6] != '0'` guard (RISK-LE-04 compliance)
- [ ] All 7 scans PASS
- [ ] BwaveLaneETests.cs has 9 passing [Fact] tests (6 from E-1 + 3 from E-2)
- [ ] Engineer writes `docs/brain/WAVE2-LANE-E/ticket-2-completion.md` with scan results

**DO NOT START E-3 until ticket-2-completion.md is written and 4b verification passes.**

---

## TICKET E-3: BreakEven / Flatten / Trim Complexity Reduction (4 Files)

**Spec Req IDs**: WAVE2-LANE-E-3
**Files**:
  - `src/PropTraderTools/Features/PttBreakEven.cs`
  - `src/PropTraderTools/Features/PttBreakEvenSwap.cs`
  - `src/PropTraderTools/Features/PttFlatten.cs`
  - `src/PropTraderTools/Features/PttTrim.cs`
**Test File**: `src/PropTraderTools/Tests/BwaveLaneETests.cs` -- APPEND to existing file (4 new tests)

**Note on ticket count**: E-3 touches 4 files but all 4 extractions are structurally identical
(CCN 9->8, single helper each, pure extraction). They are safe to batch as one ticket.
**Total lane ticket count: 3 (E-1, E-2, E-3).**

### Target Methods

| File | Method | Current CCN | Projected CCN | Status |
|------|--------|-------------|---------------|--------|
| `PttBreakEven.cs` | `SnapshotTargetsLocal` | 9 | 7 | No prior AT-LIMIT |
| `PttBreakEvenSwap.cs` | `Execute` | 9 | 8 | AT-LIMIT post-LaneC (+1); AT-LIMIT post-E-3 (DW-LE-02) |
| `PttFlatten.cs` | `FlattenPositionLocal` | 9 | 8 | AT-LIMIT post-E-3 (DW-LE-02) |
| `PttTrim.cs` | `TrimPositionLocal` | 9 | 8 | AT-LIMIT post-E-3 (DW-LE-02) |

---

### Method Signatures for Extracted Helpers

Each helper is `private static` in its respective class.

```csharp
// PttBreakEven class -- param count = 2
private static bool IsSnapshotTargetOrder(Order o, Instrument instr);

// PttBreakEvenSwap class -- param count = 1
private static bool HasNoTargets(
    System.Collections.Generic.List<(double Price, int Qty, NinjaTrader.Cbi.OrderAction Action)> targets);

// PttFlatten class -- param count = 2 (DW-LE-01: identical body to PttTrim.FormatOrderPrice)
private static string FormatOrderPrice(NinjaTrader.Cbi.OrderType orderType, double limitPrice);

// PttTrim class -- param count = 2 (DW-LE-01: identical body to PttFlatten.FormatOrderPrice)
private static string FormatOrderPrice(NinjaTrader.Cbi.OrderType orderType, double limitPrice);
```

Helper bodies (reference):
- `IsSnapshotTargetOrder(Order o, Instrument instr)`: Checks `o.Instrument != null && o.Instrument.FullName == instr.FullName` AND `(IsAtmTargetName(o.Name) || IsPttQxTarget(o.Name))`. Returns false for null inputs.
- `HasNoTargets(targets)`: `return targets == null || targets.Count == 0;`
- `FormatOrderPrice(orderType, limitPrice)`: `return orderType == OrderType.Limit ? limitPrice.ToString("F2") : "mkt";`

---

### Extraction Instructions

#### PttBreakEven::SnapshotTargetsLocal (CCN 9 -> 7)

1. **Extract IsSnapshotTargetOrder** (net -2 from SnapshotTargetsLocal):
   - CURRENT compound filter in foreach loop (after `stateOk` check):
     Line A: `bool instrOk = o.Instrument != null && o.Instrument.FullName == instr.FullName;` (1x &&)
     Line B: `if (!stateOk || !instrOk || (!IsAtmTargetName(o.Name) && !IsPttQxTarget(o.Name))) continue;` (1x ||, 1x &&)
   - IsSnapshotTargetOrder body consolidates instrOk check AND name filter.
   - REPLACE lines A and B with:
     `if (!stateOk || !IsSnapshotTargetOrder(o, instr)) continue;`
   - The `||` in the call site remains (stateOk check stays). Net: -2. CCN: 9 -> **7**.
   - **IMPORTANT**: `IsSnapshotTargetOrder` calls existing `IsAtmTargetName(o.Name)` and `IsPttQxTarget(o.Name)` helpers. Do NOT reimplement them.

#### PttBreakEvenSwap::Execute (CCN 9 -> 8)

2. **Extract HasNoTargets** (net -1 from Execute):
   - CURRENT: `if (targets == null || targets.Count == 0) return;` (the OR = 1 branch).
   - REPLACE with: `if (HasNoTargets(targets)) return;`
   - Net: -1. CCN: 9 -> **8**. AT-LIMIT (DW-LE-02).
   - Parameter tuple type must match Execute's `targets` variable exactly: `List<(double Price, int Qty, NinjaTrader.Cbi.OrderAction Action)>`.

#### PttFlatten::FlattenPositionLocal (CCN 9 -> 8)

3. **Extract FormatOrderPrice** (net -1 from FlattenPositionLocal):
   - CURRENT log call (inside try block):
     `Output.Process($"... {(orderType == OrderType.Limit ? limitPrice.ToString("F2") : "mkt")} ...", ...);`
   - FormatOrderPrice body: `return orderType == OrderType.Limit ? limitPrice.ToString("F2") : "mkt";`
   - REPLACE inline ternary with: `FormatOrderPrice(orderType, limitPrice)`
   - Net: -1. CCN: 9 -> **8**. AT-LIMIT (DW-LE-02).
   - **DW-LE-01**: Identical to PttTrim.FormatOrderPrice. Consolidation to shared utility deferred.

#### PttTrim::TrimPositionLocal (CCN 9 -> 8)

4. **Extract FormatOrderPrice** (net -1 from TrimPositionLocal):
   - Same pattern as PttFlatten above. Identical body.
   - REPLACE inline ternary in log call with: `FormatOrderPrice(orderType, limitPrice)`
   - Net: -1. CCN: 9 -> **8**. AT-LIMIT (DW-LE-02).
   - **DW-LE-01**: See PttFlatten note above.

---

### DW-LC-01 Note (Execute -- PttBreakEvenSwap)

`PttBreakEvenSwap::Execute` was AT-LIMIT (CCN=8) post-LaneC per DW-LC-01.
A single `||` was added since LaneC (delta +1), bringing it to CCN=9. Post-E-3: CCN=8.
**Method returns to AT-LIMIT status. DW-LE-02 applies.**

---

### JS Rule Constraints

- **JS-021**: No `lock()`. All 4 helpers are pure predicates or format functions. HARD BLOCK if violated.
- **JS-001**: No `throw new XxxException`. HARD BLOCK if violated.
- **JS-002**: No `return null`. `IsSnapshotTargetOrder` returns `false` for null inputs. `FormatOrderPrice` returns string literal `"mkt"` (not null). HARD BLOCK if violated.
- **JS-033**: No `async void`. HARD BLOCK if violated.
- **CYC constraint (per plan)**: each extracted helper must have cyclomatic complexity <=8 (Jane Street standard, verified by lizard)

### NT8 Constraints

- PTT- signal names preserved verbatim. No new `CreateOrder` calls in any helper.
- `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` -- untouched.
- `IsSnapshotTargetOrder` calls existing `IsAtmTargetName` and `IsPttQxTarget` in PttBreakEven.cs -- do NOT copy their bodies.
- `HasNoTargets` parameter type: must match the exact tuple type of `targets` in PttBreakEvenSwap.Execute.
- `FormatOrderPrice`: uses `NinjaTrader.Cbi.OrderType.Limit` -- use fully qualified name if `using NinjaTrader.Cbi;` is not already present.

---

### 7-Scan Checklist (Engineer Contract -- E-3)

```
SCAN-1: lizard src/PropTraderTools/Features/ --CCN 8 -x "*/obj/*" -x "*/bin/*" -x "*/Tests/*"
         Expected: Warning cnt: 0  (this is the FINAL ticket)

SCAN-2: grep -rn "lock\s*(" src/PropTraderTools/Features/ -- expected: 0 results

SCAN-3: powershell -c "@('PttBreakEven.cs','PttBreakEvenSwap.cs','PttFlatten.cs','PttTrim.cs') | ForEach-Object { [System.IO.File]::ReadAllBytes(\"src/PropTraderTools/Features/$_\") | Where-Object { $_ -gt 127 } } | Measure-Object"
         Expected: Count = 0

SCAN-4: dotnet build -- expected: 0 errors

SCAN-5: dotnet test -- expected: all prior tests pass (0 regressions)

SCAN-6: grep -rn "PTT-" src/PropTraderTools/Features/PttBreakEven.cs
                           src/PropTraderTools/Features/PttBreakEvenSwap.cs
                           src/PropTraderTools/Features/PttFlatten.cs
                           src/PropTraderTools/Features/PttTrim.cs
         Expected: all PTT- signal prefixes preserved

SCAN-7: lizard src/PropTraderTools/Features/PttBreakEven.cs src/PropTraderTools/Features/PttBreakEvenSwap.cs src/PropTraderTools/Features/PttFlatten.cs src/PropTraderTools/Features/PttTrim.cs -C 8
         Expected: 0 warnings (all 4 target methods below CCN 9)
```

All 7 scans MUST PASS before engineer writes ticket-3-completion.md.

---

### Test Coverage (E-3)

**File**: `src/PropTraderTools/Tests/BwaveLaneETests.cs` -- APPEND these 4 [Fact] methods to the
existing `BwaveLaneETests` class. Do NOT create a new file or a new class.

```csharp
        [Fact]
        public void PttBreakEven_IsSnapshotTargetOrder_Exists()
        {
            var m = typeof(NinjaTrader.NinjaScript.AddOns.PttBreakEven)
                .GetMethod("IsSnapshotTargetOrder", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(2, m.GetParameters().Length);
            Assert.Equal(typeof(bool), m.ReturnType);
        }

        [Fact]
        public void PttBreakEvenSwap_HasNoTargets_Exists()
        {
            var m = typeof(NinjaTrader.NinjaScript.AddOns.PttBreakEvenSwap)
                .GetMethod("HasNoTargets", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
            Assert.Equal(typeof(bool), m.ReturnType);
        }

        [Fact]
        public void PttFlatten_FormatOrderPrice_Exists()
        {
            var m = typeof(NinjaTrader.NinjaScript.AddOns.PttFlatten)
                .GetMethod("FormatOrderPrice", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(2, m.GetParameters().Length);
            Assert.Equal(typeof(string), m.ReturnType);
        }

        [Fact]
        public void PttTrim_FormatOrderPrice_Exists()
        {
            var m = typeof(NinjaTrader.NinjaScript.AddOns.PttTrim)
                .GetMethod("FormatOrderPrice", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(2, m.GetParameters().Length);
            Assert.Equal(typeof(string), m.ReturnType);
        }
```

---

### Acceptance Criteria (E-3)

- [ ] `SnapshotTargetsLocal` (PttBreakEven) CCN = 7 (SCAN-7)
- [ ] `Execute` (PttBreakEvenSwap) CCN = 8 (SCAN-7)
- [ ] `FlattenPositionLocal` CCN = 8 (SCAN-7)
- [ ] `TrimPositionLocal` CCN = 8 (SCAN-7)
- [ ] **SCAN-1 reports Warning cnt: 0 across all Features/*.cs** (final gate)
- [ ] All 4 helpers exist as `private static` in their respective classes
- [ ] `IsSnapshotTargetOrder` calls existing `IsAtmTargetName` and `IsPttQxTarget` (no reimplementation)
- [ ] `FormatOrderPrice` bodies in PttFlatten and PttTrim are structurally identical (DW-LE-01 noted)
- [ ] All 7 scans PASS
- [ ] BwaveLaneETests.cs has 13 passing [Fact] tests (6 + 3 + 4)
- [ ] Engineer writes `docs/brain/WAVE2-LANE-E/ticket-3-completion.md` with scan results

---

---

## Deferred Items Register (Lane-E)

| ID | Description | Priority | Status | Origin |
|----|-------------|----------|--------|--------|
| DW-LE-01 | `FormatOrderPrice` duplication in PttFlatten and PttTrim. Bodies are structurally identical. Future wave should extract to shared `PttOrderUtils` utility (same pattern as DW-LC-02 for ResolveOrderParams). Non-blocking for Lane-E. | P2 | OPEN | E-3 |
| DW-LE-02 | 5 methods AT-LIMIT (CCN=8 exactly) post-Lane-E: `PttGlobalQuickExit::SnapshotTargetOrders`, `PttGlobalQuickExit::Execute(forcedTargets)`, `PttBreakEvenSwap::Execute`, `PttFlatten::FlattenPositionLocal`, `PttTrim::TrimPositionLocal`. Any future branch addition to any of these 5 methods requires a prior extraction review before implementation (same DW-LC-01 protocol). | P1 | OPEN | E-2, E-3 |

---

## Cross-Ticket Summary

| Ticket | Files Changed | SCAN-1 Expected | Tests in BwaveLaneETests.cs |
|--------|---------------|-----------------|------------------------------|
| E-1 | PttQuickExit.cs + BwaveLaneETests.cs (new) | Warning count reduced; Execute(17) and SubmitQxOcoPair(9) cleared | 6 new [Fact] (total: 6) |
| E-2 | PttGlobalQuickExit.cs + BwaveLaneETests.cs (append) | Warning count reduced; SnapshotTargetOrders(13) and Execute(9) cleared | 3 new [Fact] (total: 9) |
| E-3 | PttBreakEven.cs + PttBreakEvenSwap.cs + PttFlatten.cs + PttTrim.cs + BwaveLaneETests.cs (append) | **Warning cnt: 0** -- all 8 violations cleared | 4 new [Fact] (total: 13) |

---

**TICKETS_COMPLETE**