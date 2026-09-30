# NEW-F5 Verification Report

**Finding**: PurgeFollowerStopScanStopOrders -- OrderId fallback for NT reconnect
**File**: src/V12_002.Orders.Callbacks.AccountOrders.cs
**Method**: PurgeFollowerStopScanStopOrders
**Engineer Commit**: 76a270b6be20b56e47d628052b4005dd0811d2c1
**Branch**: wave7/pr20-deferred-repairs
**Verifier**: Phase 5.V independent verifier
**Date**: 2026-06-22

---

## VERIFY_DONE NEW-F5

**verification_verdict: FAIL**
**fix_confirmed: true**
**build_passed: true**
**gate_passed: false**

---

## Step-by-Step Results

### Step 1 -- Branch Confirmation
- Branch: wave7/pr20-deferred-repairs -- CONFIRMED

### Step 2 -- Code Read (lines 822-844)
- File read successfully at lines 822-844

### Step 3 -- Old Text GONE
- `if (sc.Value == order)` (pure reference equality) is ABSENT -- CONFIRMED

### Step 4 -- New Text PRESENT
Line 826 now reads:
```csharp
if (sc.Value == order || (sc.Value != null && sc.Value.OrderId == order.OrderId))
```
OrderId fallback is PRESENT and functionally correct -- CONFIRMED

### Step 5 -- Build
```
dotnet build Linting.csproj
Build succeeded. 0 Warning(s) 0 Error(s)
```
BUILD: PASS

### Step 6 -- wave7_prepush_gate.py
```
[PASS] Check 0 -- CS-only
[PASS] Check 1 -- ASCII-only
[PASS] Check 2 -- DateTime.Now (none introduced)
[PASS] Check 3 -- lock() (none found)
[PASS] Check 4 -- underscore locals (none found)
[PASS] Check 5 -- diff size (613 raw / 613 stripped, under 150,000 limit)
GATE PASSED. Ready to push.
```
PREPUSH GATE: PASS

### Step 7 -- lock() Audit
`Select-String "lock(" src/V12_002.Orders.Callbacks.AccountOrders.cs` -- no output
LOCK CHECK: PASS (zero lock() calls found)

### Step 8 -- CYC Gate
```
CYC_GATE: FAIL  EPIC-W7-NF5  PurgeFollowerStopScanStopOrders  CYC=9
(threshold=8, still need to reduce by 1)
```
CYC measured by complexity_audit.py: **9**

McCabe breakdown:
- Base entry: +1
- foreach loop: +1
- outer if (sc.Value == order || ...): +1 for if, +1 for || operator = +2
- inner if (TryGetValue && scPos != null && scPos.PendingCleanup && scPos.RemainingContracts <= 0):
  +1 for if, +3 for three && operators = +4
- Total: 1+1+2+4 = **CYC=8** (manual) or **CYC=9** (lizard/complexity_audit.py tool)

The tool measures CYC=9. The fix added one logical branch (the || OrderId fallback) which pushed
a previously borderline method from 8 to 9. The fix is SEMANTICALLY CORRECT but requires one
more extraction to meet the CYC<=8 mandate.

---

## Required Action

The fix content is correct. To obtain a PASS verdict, extract the compound fallback condition
into a private helper:

```csharp
private static bool MatchesOrder(Order candidate, Order target) =>
    candidate == target || (candidate != null && candidate.OrderId == target.OrderId);
```

Then change line 826 to:
```csharp
if (MatchesOrder(sc.Value, order))
```

This collapses the || + && into a single bool call, reducing CYC in
PurgeFollowerStopScanStopOrders by 2 (removes the || branch and the && null guard),
bringing it to CYC=7.

After extracting, re-run:
```
python scripts/wave7_cyc_gate.py EPIC-W7-NF5 PurgeFollowerStopScanStopOrders
```
Gate must return exit 0 before re-submitting.

---

## Summary

| Check | Result |
|-------|--------|
| Branch correct | PASS |
| Old code removed | PASS |
| New fix present | PASS |
| Build | PASS |
| Prepush gate | PASS |
| lock() absent | PASS |
| CYC <= 8 | **FAIL** (CYC=9, threshold=8) |

**Overall verdict: FAIL -- fix is semantically correct but CYC=9 exceeds the Jane Street CYC<=8 mandate.**
**Action required: extract MatchesOrder helper, re-run CYC gate, re-verify.**
