# B196 Ticket 2 Completion Report

**SCOPE LOCK -- TICKET 2 ONLY**
**Epic**: B196
**Ticket**: T2 -- DW-B170-01: FindLeaderStopPrice Missing OrderState.Accepted
**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-10-11
**Phase**: 4a (Implementation)

---

## Implementation Summary

Method modified: `FindLeaderStopPrice` in `src/PropTraderTools/CopyEngine.cs`
Lines affected: 3701 (CYC comment), 3714-3715 (predicate)
Tests added: 2 [Fact] methods in `src/PropTraderTools/Tests/B196Tests.cs`

---

## Exact Lines Modified

### Change 1 -- CYC Comment (line 3701)

**BEFORE**:
```csharp
// CYC=5: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working.
```

**AFTER**:
```csharp
// CYC=6: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working; (6) state==Accepted. DW-B170-01.
```

### Change 2 -- Predicate (lines 3712-3715)

**BEFORE**:
```csharp
        if (
            order.Name == targetName // (4)
            && order.OrderState == OrderState.Working
        ) // (5)
            return order.StopPrice;
```

**AFTER**:
```csharp
        if (
            order.Name == targetName // (4)
            && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted) // (5)(6)
        ) // DW-B170-01
            return order.StopPrice;
```

**IMPORTANT**: `OrderState.Working` retained as regression guard.

---

## Final State (lines 3699-3719)

```csharp
        // B132 LaneA -- FindLeaderStopPrice: scan leader account orders for Working "Stop{N}".
        // Returns StopPrice of the match, or 0.0 if not found.
        // CYC=6: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working; (6) state==Accepted. DW-B170-01.
        // JS-002: returns 0.0 on all failure paths. JS-021: no lock (NT8 Orders collection is thread-safe).
        private static double FindLeaderStopPrice(Account? leaderAccount, int bracketIndex)
        {
            if (leaderAccount == null) // (1)
                return 0.0;
            if (bracketIndex <= 0) // (2)
                return 0.0;
            var targetName = "Stop" + bracketIndex.ToString();
            foreach (var order in leaderAccount.Orders.ToList()) // (3)
            {
                if (
                    order.Name == targetName // (4)
                    && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted) // (5)(6)
                ) // DW-B170-01
                    return order.StopPrice;
            }
            return 0.0;
        }
```

---

## CYC Analysis

| Method | Before | After | Limit | Status |
|--------|--------|-------|-------|--------|
| `FindLeaderStopPrice` | 5 | 6 | 8 | PASS |

**Branch count (McCabe)**:
1. `if (leaderAccount == null)` -- null account guard
2. `if (bracketIndex <= 0)` -- zero index guard
3. `foreach (var order ...)` -- loop
4. `order.Name == targetName` -- name match condition
5. `order.OrderState == OrderState.Working` -- Working state branch
6. `order.OrderState == OrderState.Accepted` -- Accepted state branch (new, DW-B170-01)

Total: base(1) + 5 decision points = CYC 6. Within limit of 8.

---

## [Fact] Tests Added

**File**: `src/PropTraderTools/Tests/B196Tests.cs`
**Test class**: `B196Tests`

| Test Name | Pattern | What It Asserts |
|-----------|---------|-----------------|
| `FindLeaderStopPrice_AcceptedStop_ReturnsPrice` | BCL source-text (File.ReadAllText) | `OrderState.Accepted` is present in `CopyEngine.cs`; compound predicate `(order.OrderState == OrderState.Working \|\| order.OrderState == OrderState.Accepted)` present. |
| `FindLeaderStopPrice_WorkingStop_StillReturnsPrice` | BCL source-text (File.ReadAllText) | `order.OrderState == OrderState.Working \|\| order.OrderState == OrderState.Accepted` still present (regression guard); CYC comment updated to `CYC=6` with `DW-B170-01`. |

---

## 7-SCAN RESULTS

### SCAN 1 -- JS-021 lock() (no new lock in FindLeaderStopPrice)
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\(" | Where-Object { $_.Line -notmatch "//.*lock" }`
**Result**: 0 matches
**Status**: PASS

### SCAN 2 -- JS-033 async void (no new async void)
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async void " | Where-Object { $_.Line -notmatch "//" }`
**Result**: 0 matches
**Status**: PASS

### SCAN 3 -- JS-066 CYC check (FindLeaderStopPrice after change)
**Method**: `FindLeaderStopPrice`
**CYC before**: 5
**CYC after**: 6
**Branches**:
  1. `if (leaderAccount == null)` -- null account
  2. `if (bracketIndex <= 0)` -- zero index
  3. `foreach (var order ...)` -- loop iteration
  4. `order.Name == targetName` -- name match condition
  5. `order.OrderState == OrderState.Working` -- Working state
  6. `order.OrderState == OrderState.Accepted` -- Accepted state (DW-B170-01)
**Status**: PASS (CYC=6 <= 8)

### SCAN 4 -- JS-001 throw check (no new throw new)
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw new "`
**Result**: 0 matches in entire file
**Status**: PASS

### SCAN 5 -- JS-002 return null (no return null in FindLeaderStopPrice)
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null" | Where-Object LineNumber in 3699..3720`
**Result**: 0 matches in target region
**Method returns**: `double` (0.0 on miss, `order.StopPrice` on match -- never null)
**Status**: PASS

### SCAN 6 -- ASCII-only (modified lines are ASCII)
**Check**: Lines 3699-3720 scanned for non-ASCII characters (`[^\x00-\x7F]`)
**Result**: 0 non-ASCII characters found
**Comment tag**: `"DW-B170-01"` is ASCII. No Unicode in any modified line.
**Status**: PASS

### SCAN 7 -- DateTime.UtcNow ban (no new DateTime usage)
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "DateTime\.Now[^U]|DateTime\.Now$"`
**Result**: 1 comment-only match at line 4391 (`// ASCII-only. No DateTime.Now. No lock().`) -- not actual usage
**New DateTime usage in FindLeaderStopPrice**: NONE
**Status**: PASS

---

## All 7 Scans: ZERO violations

| Scan | Check | Result | Status |
|------|-------|--------|--------|
| SCAN 1 | JS-021 lock() | 0 actual lock calls | PASS |
| SCAN 2 | JS-033 async void | 0 async void | PASS |
| SCAN 3 | JS-066 CYC <= 8 | CYC=6 (was 5) | PASS |
| SCAN 4 | JS-001 no throw | 0 throw new | PASS |
| SCAN 5 | JS-002 no return null | 0 null returns in region | PASS |
| SCAN 6 | ASCII-only | 0 non-ASCII in modified lines | PASS |
| SCAN 7 | DateTime.UtcNow ban | 0 new DateTime.Now usage | PASS |

---

## Methods NOT Modified

Per SCOPE LOCK -- Ticket 2 only. No other methods were touched.
- `FindLeaderStopPriceTestable` (line 4062) -- unchanged (delegates to `FindLeaderStopPrice`, inherits fix)
- All other methods in `CopyEngine.cs` -- unchanged

---

## DW Item Closed

**DW-B170-01**: `FindLeaderStopPrice` now returns `order.StopPrice` when `OrderState == Accepted`,
covering the NT8 SIM broker's brief intermediate state before `OrderState.Working`. Previously,
a stop order in `Accepted` state returned 0.0, causing follower stop price = 0 -> naked position.

---

*Engineer: ptt-engineer*
*Epic: B196*
*Phase: 4a (Implementation)*
*Return: BUILD_PASS*
