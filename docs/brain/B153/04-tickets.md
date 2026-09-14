# B153 Tickets — Remove Hardcoded 3-Leg Ceiling

**Block:** B153
**Phase:** 3 — Ticket Generation
**Status:** TICKETS_COMPLETE
**Plan source:** docs/brain/B153/02-architecture-plan.md (REVIEW_PASS confirmed)
**Plan review:** docs/brain/B153/02-plan-review.md (non-blocking CYC observations incorporated)
**Author:** ptt-architect
**Date:** 2026-09-13

---

## Global Constraints (apply to ALL tickets, no exceptions)

| Constraint | Rule | Enforcement |
|-----------|------|-------------|
| No `lock()` blocks | JS-021 | SCAN-8: grep -rn "lock\s*(" src/PropTraderTools/ = 0 |
| ASCII-only in all C# literals and identifiers | JS project mandate | SCAN-9: 0 bytes > 127 in any edited file |
| No `DateTime.Now` | Project mandate | Use `DateTime.UtcNow` or `(long)(int)Environment.TickCount` |
| No StrategyBase-only APIs | NT8-008/009/010 | `AtmStrategyCreate`, `AtmStrategyChangeStopTarget`, `Account.Change` BANNED |
| No Unicode in C# string literals | JS project mandate | SCAN-9 |
| All CreateOrder calls must use "PTT-" prefix | NT8-003 | Existing naming preserved, not changed |
| CYC <= 8 on all methods | JS-013 | SCAN-7: lizard --CCN 8 = Warning cnt: 0 |
| Build clean | Delivery gate | SCAN-10: dotnet build = 0 errors, 0 warnings |

---

## Ticket DW-B153-01

### Title
Remove QX target ceiling (Quick + Quick ALL)

### Spec Requirement
B153-REQ-01 — QX ceiling removal

### File Affected
`src/PropTraderTools/Features/PttQuickExit.cs`

### Symbol Affected
`ResolveTargetCount` (~line 434)

### Method Signature (unchanged)
```csharp
private static int ResolveTargetCount(int raw)
```

### Change — Before

```csharp
// Hard cap: never return more than 3. QX-ALL contract is always exactly 3 targets.
return Math.Min(raw, 3);
```

### Change — After

```csharp
// No ceiling -- NT8 supports N targets.
return raw;
```

**Instructions for engineer:**
1. Locate `ResolveTargetCount` at approximately line 434 in `PttQuickExit.cs`.
2. Remove the comment line containing "Hard cap: never return more than 3. QX-ALL contract is always exactly 3 targets."
3. Replace `return Math.Min(raw, 3);` with `return raw;`.
4. Add the replacement comment `// No ceiling -- NT8 supports N targets.` on the line above `return raw;`.
5. Do NOT change the method signature.
6. Do NOT touch any other code in the file.

### CYC Analysis (corrected per OBS-CYC-01)
- `Math.Min` is a method call, not a McCabe decision point (JS-013 strict count).
- CYC delta = **0** (not -1 as stated in plan; plan-review OBS-CYC-01 corrects this).
- Pre-fix CYC = 1 (single return, no branches). Post-fix CYC = **1**.
- Engineer must annotate: `// CYC=1`
- Well within JS-013 limit (<=8).

### xUnit [Fact] Tests

| Test Name | Asserts |
|-----------|---------|
| `ResolveTargetCount_WithFourTargets_ReturnsFour` | Pass raw=4, assert result==4 (new capability, previously capped to 3) |
| `ResolveTargetCount_WithTwentyTwoTargets_ReturnsTwentyTwo` | Pass raw=22, assert result==22 (N-target ceiling removed) |
| `ResolveTargetCount_WithThreeTargets_ReturnsThree` | Pass raw=3, assert result==3 (regression — boundary preserved) |
| `ResolveTargetCount_WithOneTarget_ReturnsOne` | Pass raw=1, assert result==1 (lower bound preserved) |

### 7-Scan Checklist

| Scan | Command | Expected | Applies? |
|------|---------|----------|---------|
| SCAN-1 | `grep -n "n > 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | NO — different file |
| SCAN-2 | `grep -n "i <= 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | NO — different file |
| SCAN-3 | `grep -n "new double\[3\]" src/PropTraderTools/CopyEngine.cs` | 0 matches | NO — different file |
| **SCAN-4** | `grep -n "Math.Min.*3" src/PropTraderTools/Features/PttQuickExit.cs` | **0 matches** | **YES — primary gate** |
| SCAN-5 | `grep -n "targets.Count > 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | NO — different file |
| SCAN-6 | `grep -n "name.Length != 9" src/PropTraderTools/Features/PttBreakEven.cs` | 0 matches | NO — different file |
| **SCAN-7** | `lizard src/PropTraderTools/Features/PttQuickExit.cs --CCN 8` | Warning cnt: 0 | **YES** |
| **SCAN-8** | `grep -rn "lock\s*(" src/PropTraderTools/` | 0 actual lock() blocks | **YES** |
| **SCAN-9** | ASCII-only byte check: `PttQuickExit.cs` | 0 bytes > 127 | **YES** |
| **SCAN-10** | `dotnet build` | 0 errors, 0 warnings | **YES** |

### Acceptance Criteria (Ph4b Verifier Pass/Fail)

| # | Criterion | Pass | Fail |
|---|-----------|------|------|
| AC-01 | SCAN-4: `grep -n "Math.Min.*3" src/PropTraderTools/Features/PttQuickExit.cs` | 0 matches | Any match |
| AC-02 | SCAN-7: lizard PttQuickExit.cs --CCN 8 | Warning cnt: 0 | Any warning |
| AC-03 | SCAN-8: grep lock\s*( src/PropTraderTools/ | 0 actual lock() blocks | Any lock() found |
| AC-04 | SCAN-9: ASCII-only byte check PttQuickExit.cs | 0 bytes > 127 | Any byte > 127 |
| AC-05 | SCAN-10: dotnet build | 0 errors, 0 warnings | Any error or warning |
| AC-06 | xUnit: `ResolveTargetCount_WithFourTargets_ReturnsFour` | PASS | FAIL |
| AC-07 | xUnit: `ResolveTargetCount_WithTwentyTwoTargets_ReturnsTwentyTwo` | PASS | FAIL |
| AC-08 | xUnit: `ResolveTargetCount_WithThreeTargets_ReturnsThree` | PASS | FAIL |
| AC-09 | xUnit: `ResolveTargetCount_WithOneTarget_ReturnsOne` | PASS | FAIL |

### Architecture-Locked Constraints for This Ticket
- `ResolveTargetCount` method signature must not change.
- Quick2t and QAll2t buttons are NOT in scope — do not touch those paths.
- No new NT8 API calls introduced.
- `DeduplicateByPrice` (in caller chain) is the confirmed residue guard for this path. Do not add any replacement ceiling.
- No threading changes required. Method is pure arithmetic, thread-safe by construction.

---

## Ticket DW-B153-02

### Title
Remove BE target ceiling (BE + BE ALL)

### Spec Requirement
B153-REQ-02 — BE ceiling removal (closes DW-B107)

### File Affected
`src/PropTraderTools/CopyEngine.cs`

### Symbol Affected
`MoveStopToBreakEven` (~line 6705)

### Method Signature (unchanged)
```csharp
private void MoveStopToBreakEven(/* existing params unchanged */)
```

### Change — Before

```csharp
// DW-B107: hard cap -- BE/QX contract is always exactly 3 targets max.
while (targets.Count > 3)
    targets.RemoveAt(targets.Count - 1);
```

### Change — After

```csharp
// No ceiling -- SnapshotBeTargets DeduplicateByPrice handles residue inflation.
```

**Instructions for engineer:**
1. Locate `MoveStopToBreakEven` at approximately line 6705 in `CopyEngine.cs`.
2. Find the block containing the comment "DW-B107: hard cap -- BE/QX contract is always exactly 3 targets max." and the `while (targets.Count > 3)` loop below it.
3. Remove the entire comment line AND the `while` block (2-3 lines total: comment + `while` + `targets.RemoveAt(...)`).
4. Insert the replacement comment `// No ceiling -- SnapshotBeTargets DeduplicateByPrice handles residue inflation.` in place of the removed block.
5. Do NOT change the method signature.
6. Do NOT touch any other code in the file.

### CYC Analysis
- Removing the `while` statement removes one McCabe decision point (JS-013 loop = +1 branch).
- CYC delta = **-1** (while branch removed; confirmed correct by plan-review).
- Pre-fix CYC <= 8 (confirmed). Post-fix CYC = pre-fix - 1.
- Engineer must verify with `lizard --CCN 8` and update `// CYC=N` annotation on the method.
- Well within JS-013 limit (<=8).

### xUnit [Fact] Tests

| Test Name | Asserts |
|-----------|---------|
| `MoveStopToBreakEven_WithFiveTargets_ProcessesAllFive` | With snapshot of 5 targets, all 5 are processed; none are dropped (new capability) |
| `MoveStopToBreakEven_WithThreeTargets_UnchangedBehavior` | With snapshot of 3 targets, all 3 are processed; regression — existing 3-target path unaffected |

### 7-Scan Checklist

| Scan | Command | Expected | Applies? |
|------|---------|----------|---------|
| SCAN-1 | `grep -n "n > 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | NO — different symbol |
| SCAN-2 | `grep -n "i <= 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | NO — different symbol |
| SCAN-3 | `grep -n "new double\[3\]" src/PropTraderTools/CopyEngine.cs` | 0 matches | NO — different symbol |
| SCAN-4 | `grep -n "Math.Min.*3" src/PropTraderTools/Features/PttQuickExit.cs` | 0 matches | NO — different file |
| **SCAN-5** | `grep -n "targets.Count > 3" src/PropTraderTools/CopyEngine.cs` | **0 matches** | **YES — primary gate** |
| SCAN-6 | `grep -n "name.Length != 9" src/PropTraderTools/Features/PttBreakEven.cs` | 0 matches | NO — different file |
| **SCAN-7** | `lizard src/PropTraderTools/CopyEngine.cs --CCN 8` | Warning cnt: 0 | **YES** |
| **SCAN-8** | `grep -rn "lock\s*(" src/PropTraderTools/` | 0 actual lock() blocks | **YES** |
| **SCAN-9** | ASCII-only byte check: `CopyEngine.cs` | 0 bytes > 127 | **YES** |
| **SCAN-10** | `dotnet build` | 0 errors, 0 warnings | **YES** |

### Acceptance Criteria (Ph4b Verifier Pass/Fail)

| # | Criterion | Pass | Fail |
|---|-----------|------|------|
| AC-01 | SCAN-5: `grep -n "targets.Count > 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | Any match |
| AC-02 | SCAN-7: lizard CopyEngine.cs --CCN 8 | Warning cnt: 0 | Any warning |
| AC-03 | SCAN-8: grep lock\s*( src/PropTraderTools/ | 0 actual lock() blocks | Any lock() found |
| AC-04 | SCAN-9: ASCII-only byte check CopyEngine.cs | 0 bytes > 127 | Any byte > 127 |
| AC-05 | SCAN-10: dotnet build | 0 errors, 0 warnings | Any error or warning |
| AC-06 | xUnit: `MoveStopToBreakEven_WithFiveTargets_ProcessesAllFive` | PASS | FAIL |
| AC-07 | xUnit: `MoveStopToBreakEven_WithThreeTargets_UnchangedBehavior` | PASS | FAIL |

### Architecture-Locked Constraints for This Ticket
- `MoveStopToBreakEven` method signature must not change.
- `DeduplicateByPrice` (called on the snapshot, upstream of this symbol) is the confirmed residue guard. Do not add any replacement ceiling.
- This closes DW-B107 (the residue-protection ceiling guard is now obsolete).
- No new NT8 API calls introduced.
- No threading changes required. Operates on caller-scoped snapshot list.

---

## Ticket DW-B153-03

### Title
Remove stop drag ceiling (all stop drags, all modes)

### Spec Requirement
B153-REQ-03 — stop drag ceiling removal

### File Affected
`src/PropTraderTools/CopyEngine.cs`

### Symbols Affected (tightly coupled — ALL must change in one atomic commit)

| Symbol | Line (approx.) | Change type |
|--------|---------------|------------|
| `TryParseStopSuffix` | ~3451 | Guard condition edit |
| `CaptureOtherLegTargetPrices` | ~3414 | Return type change + body rewrite |
| `ParseTargetLegIndex` | NEW (add near ~3414) | New private static helper |
| `ResubmitCollateralLegs` | ~3619 | Parameter type change + loop rewrite |
| `HandleAtmStopSync` call site | ~3175-3181 | Local variable type update only |

> **ATOMIC COMMIT REQUIREMENT:** All 5 change points MUST be in the same commit. If `CaptureOtherLegTargetPrices` return type changes to `Dictionary<int,double>` without `ResubmitCollateralLegs` and `HandleAtmStopSync` call site updated in the same commit, the build will fail with type mismatch errors. SCAN-10 is the hard gate.

---

### Change (A) — TryParseStopSuffix

**Method signature (unchanged):**
```csharp
private static bool TryParseStopSuffix(string orderName, out int n)
```

**Before:**
```csharp
if (n < 1 || n > 3)
    return false;
```

**After:**
```csharp
if (n < 1)
    return false;
```

**Instructions for engineer:**
1. Locate `TryParseStopSuffix` at approximately line 3451 in `CopyEngine.cs`.
2. Find the guard `if (n < 1 || n > 3)` (may appear as `if (n > 3 || n < 1)` — order may vary).
3. Remove the `|| n > 3` condition only. Leave the `if (n < 1)` guard intact.
4. The resulting guard must be exactly `if (n < 1)`.
5. Do NOT change the method signature.

**CYC Analysis:**
- Removing `|| n > 3` removes one McCabe decision point (the `||` operator introduces a decision in strict JS-013 count).
- CYC delta = **-1**.
- Post-fix CYC = pre-fix - 1. Engineer must verify and annotate `// CYC=N`.

---

### Change (B) — CaptureOtherLegTargetPrices

**Method signature — Before:**
```csharp
private double[] CaptureOtherLegTargetPrices(string excludeSuffix, Account account)
```

**Method signature — After:**
```csharp
private Dictionary<int, double> CaptureOtherLegTargetPrices(string excludeSuffix, Account account)
```

**Full body — After:**
```csharp
private Dictionary<int, double> CaptureOtherLegTargetPrices(
    string excludeSuffix,
    Account account)
{
    // No ceiling -- SnapshotBeTargets DeduplicateByPrice handles residue inflation.
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

**Instructions for engineer:**
1. Locate `CaptureOtherLegTargetPrices` at approximately line 3414 in `CopyEngine.cs`.
2. Change the return type from `double[]` to `Dictionary<int, double>`.
3. Replace the entire method body:
   - Remove: `var prices = new double[3]` and the `for (int i = 1; i <= 3; i++)` loop.
   - Replace with: the `Dictionary<int, double>` + `foreach` pattern shown above.
4. The `ParseTargetLegIndex` call site in this method depends on the NEW helper added in Change (C) — both changes must land in the same commit.
5. Use `order.LimitPrice` as the price value (preserves existing price-capture pattern).
6. Add namespace `using System.Collections.Generic;` at the top of the file if not already present.

**CYC Analysis:**
- Post-fix CCN = 3 (base=1, foreach=+1, `if (idx >= 1)`=+1).
- Well within JS-013 limit (<=8).
- Engineer must annotate: `// CYC=3`

---

### Change (C) — NEW: ParseTargetLegIndex (private static helper)

**Method signature:**
```csharp
private static int ParseTargetLegIndex(string orderName, string excludeSuffix)
```

**Full implementation:**
```csharp
// CYC=5
private static int ParseTargetLegIndex(string orderName, string excludeSuffix)
{
    // ASCII-only. No lock(). JS-002 compliant (returns -1 on invalid, not throw).
    if (orderName == null) return -1;
    // Accept NT8 native "TargetN" and PTT drag "PTT-TGT-Drag-N"
    string suffix;
    const string pttPrefix = "PTT-TGT-Drag-";
    const string ntPrefix = "Target";
    if (orderName.StartsWith(pttPrefix, StringComparison.Ordinal))
        suffix = orderName.Substring(pttPrefix.Length);
    else if (orderName.StartsWith(ntPrefix, StringComparison.Ordinal))
        suffix = orderName.Substring(ntPrefix.Length);
    else
        return -1;
    if (suffix == excludeSuffix) return -1;
    return int.TryParse(suffix, out int idx) && idx >= 1 ? idx : -1;
}
```

**Instructions for engineer:**
1. Add `ParseTargetLegIndex` as a new `private static` method in `CopyEngine.cs`, placed near `CaptureOtherLegTargetPrices` (~line 3414) for proximity.
2. This is NOT a new file. Add as a method within the existing `CopyEngine` class.
3. The method must accept both "TargetN" (NT8 native) and "PTT-TGT-Drag-N" (PTT drag naming).
4. Returns -1 on any invalid/excluded input (name does not match either prefix, suffix matches excludeSuffix, or TryParse fails). Callers check `idx >= 1` (since -1 < 1, invalid inputs are skipped).
5. PTT prefix is checked first to implement the existing PTT-preferred rule.
6. All strings are ASCII-only. No Unicode. No lock().
7. Return type is `int`. JS-002 compliant (returns -1 as default, not null, not throw).

**CYC Analysis (corrected per OBS-CYC-02):**
- Strict JS-013 McCabe count: base=1, `if (orderName == null)`=+1, `if (StartsWith pttPrefix)`=+1, `else if (StartsWith ntPrefix)`=+1, `if (suffix == excludeSuffix)`=+1, ternary `&&`/`?:`=+1 = CCN = **5** (not 4 as stated in plan; plan-review OBS-CYC-02 corrects this).
- Engineer MUST use `// CYC=5` comment on this method (per OBS-CYC-02 mandate).
- Well within JS-013 limit (<=8).

---

### Change (D) — ResubmitCollateralLegs

**Method signature — Before:**
```csharp
private void ResubmitCollateralLegs(double[] otherLegPrices, Account account, /* other params unchanged */)
```

**Method signature — After:**
```csharp
private void ResubmitCollateralLegs(Dictionary<int, double> otherLegPrices, Account account, /* other params unchanged */)
```

**Loop body — Before:**
```csharp
for (int i = 1; i <= 3; i++)
{
    // cancel + recreate PTT-TGT-Drag-{i} at otherLegPrices[i-1]
    string s = i.ToString();
    double price = otherLegPrices[i - 1];
    // skip + cancel/create logic using s and price
}
```

**Loop body — After:**
```csharp
foreach (var kvp in otherLegPrices)
{
    string s = kvp.Key.ToString();
    double price = kvp.Value;
    // skip + cancel/create logic using s and price (unchanged)
}
```

**Instructions for engineer:**
1. Locate `ResubmitCollateralLegs` at approximately line 3619 in `CopyEngine.cs`.
2. Change the first parameter type from `double[]` to `Dictionary<int, double>`.
3. Replace the `for (int i = 1; i <= 3; i++)` loop with `foreach (var kvp in otherLegPrices)`.
4. Inside the loop body: replace `i.ToString()` with `kvp.Key.ToString()`, and `otherLegPrices[i - 1]` with `kvp.Value`.
5. The skip logic (e.g., `if (s == excludeSuffix)`, `if (price <= 0)`) is UNCHANGED — preserve it exactly as-is.
6. The Cancel + CreateOrder + Submit pattern inside the loop is UNCHANGED.

**CYC Analysis:**
- `foreach` replaces `for` — branch count unchanged. CYC delta = **0**.
- Post-fix CYC = pre-fix CYC.

---

### Change (E) — HandleAtmStopSync Call Site

**Location:** `HandleAtmStopSync` method, approximately lines 3175-3181 in `CopyEngine.cs`.

**Before:**
```csharp
double[] prices = CaptureOtherLegTargetPrices(excludeSuffix, account);
ResubmitCollateralLegs(prices, account, /* ... */);
```

**After:**
```csharp
Dictionary<int, double> prices = CaptureOtherLegTargetPrices(excludeSuffix, account);
ResubmitCollateralLegs(prices, account, /* ... */);
```

**Instructions for engineer:**
1. Locate the call to `CaptureOtherLegTargetPrices` inside `HandleAtmStopSync` (~lines 3175-3181).
2. Change the local variable type from `double[]` to `Dictionary<int, double>`.
3. The call to `CaptureOtherLegTargetPrices(...)` and `ResubmitCollateralLegs(...)` are otherwise unchanged.
4. This is a type-only propagation change — no logic change.

**CYC Analysis:**
- Type annotation change only. CYC delta = **0**.

---

### CYC Summary Table for DW-B153-03

| Symbol | Pre-fix CYC | Delta | Post-fix CYC | Comment |
|--------|------------|-------|-------------|---------|
| `TryParseStopSuffix` | N (confirmed <=8) | -1 | N-1 | Remove `\|\| n > 3` decision point |
| `CaptureOtherLegTargetPrices` | prev (<=8) | 0 net (rewrite) | **3** | New body: base+foreach+if |
| `ParseTargetLegIndex` | N/A (new) | N/A | **5** | OBS-CYC-02 corrected count |
| `ResubmitCollateralLegs` | N | 0 | N | foreach replaces for — same branch count |
| `HandleAtmStopSync` call site | N | 0 | N | Type-only change |

### xUnit [Fact] Tests

| Test Name | Asserts |
|-----------|---------|
| `TryParseStopSuffix_Stop4_ReturnsTrue` | Suffix "4" accepted (n=4 is valid, previously rejected by n>3) |
| `TryParseStopSuffix_Stop99_ReturnsTrue` | Suffix "99" accepted (arbitrary N) |
| `TryParseStopSuffix_Stop0_ReturnsFalse` | Suffix "0" rejected (n<1 guard preserved) |
| `ParseTargetLegIndex_NativeTarget5_Returns5` | "Target5" input returns 5 |
| `ParseTargetLegIndex_PttDragTarget10_Returns10` | "PTT-TGT-Drag-10" input returns 10 |
| `ParseTargetLegIndex_ExcludeSuffix_ReturnsNegative1` | Input where suffix == excludeSuffix returns -1 |
| `ParseTargetLegIndex_NullInput_ReturnsNegative1` | null input returns -1 (not throw) |
| `ParseTargetLegIndex_WrongPrefix_ReturnsNegative1` | "SomeOtherOrder5" returns -1 |
| `CaptureOtherLegTargetPrices_FiveLegs_ReturnsAllFive` | Account with 5 PTT-TGT-Drag orders returns Dictionary with 5 entries |
| `ResubmitCollateralLegs_FiveLegs_SubmitsAllFive` | Dictionary with 5 entries causes 5 Cancel+CreateOrder+Submit cycles |

### 7-Scan Checklist

| Scan | Command | Expected | Applies? |
|------|---------|----------|---------|
| **SCAN-1** | `grep -n "n > 3" src/PropTraderTools/CopyEngine.cs` | **0 matches** | **YES — TryParseStopSuffix gate** |
| **SCAN-2** | `grep -n "i <= 3" src/PropTraderTools/CopyEngine.cs` | **0 matches** | **YES — loop replacement gate** |
| **SCAN-3** | `grep -n "new double\[3\]" src/PropTraderTools/CopyEngine.cs` | **0 matches** | **YES — array replacement gate** |
| SCAN-4 | `grep -n "Math.Min.*3" src/PropTraderTools/Features/PttQuickExit.cs` | 0 matches | NO — different file |
| SCAN-5 | `grep -n "targets.Count > 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | NO — different symbol |
| SCAN-6 | `grep -n "name.Length != 9" src/PropTraderTools/Features/PttBreakEven.cs` | 0 matches | NO — different file |
| **SCAN-7** | `lizard src/PropTraderTools/CopyEngine.cs --CCN 8` | Warning cnt: 0 | **YES** |
| **SCAN-8** | `grep -rn "lock\s*(" src/PropTraderTools/` | 0 actual lock() blocks | **YES** |
| **SCAN-9** | ASCII-only byte check: `CopyEngine.cs` | 0 bytes > 127 | **YES** |
| **SCAN-10** | `dotnet build` | 0 errors, 0 warnings | **YES** |

### Acceptance Criteria (Ph4b Verifier Pass/Fail)

| # | Criterion | Pass | Fail |
|---|-----------|------|------|
| AC-01 | SCAN-1: `grep -n "n > 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | Any match |
| AC-02 | SCAN-2: `grep -n "i <= 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | Any match |
| AC-03 | SCAN-3: `grep -n "new double\[3\]" src/PropTraderTools/CopyEngine.cs` | 0 matches | Any match |
| AC-04 | SCAN-7: lizard CopyEngine.cs --CCN 8 | Warning cnt: 0 | Any warning |
| AC-05 | SCAN-8: grep lock\s*( src/PropTraderTools/ | 0 actual lock() blocks | Any lock() found |
| AC-06 | SCAN-9: ASCII-only byte check CopyEngine.cs | 0 bytes > 127 | Any byte > 127 |
| AC-07 | SCAN-10: dotnet build | 0 errors, 0 warnings | Any error or warning |
| AC-08 | xUnit: `TryParseStopSuffix_Stop4_ReturnsTrue` | PASS | FAIL |
| AC-09 | xUnit: `TryParseStopSuffix_Stop99_ReturnsTrue` | PASS | FAIL |
| AC-10 | xUnit: `TryParseStopSuffix_Stop0_ReturnsFalse` | PASS | FAIL |
| AC-11 | xUnit: `ParseTargetLegIndex_NativeTarget5_Returns5` | PASS | FAIL |
| AC-12 | xUnit: `ParseTargetLegIndex_PttDragTarget10_Returns10` | PASS | FAIL |
| AC-13 | xUnit: `ParseTargetLegIndex_ExcludeSuffix_ReturnsNegative1` | PASS | FAIL |
| AC-14 | xUnit: `ParseTargetLegIndex_NullInput_ReturnsNegative1` | PASS | FAIL |
| AC-15 | xUnit: `ParseTargetLegIndex_WrongPrefix_ReturnsNegative1` | PASS | FAIL |
| AC-16 | xUnit: `CaptureOtherLegTargetPrices_FiveLegs_ReturnsAllFive` | PASS | FAIL |
| AC-17 | xUnit: `ResubmitCollateralLegs_FiveLegs_SubmitsAllFive` | PASS | FAIL |
| AC-18 | Atomic commit: all 5 change points in one commit | Build clean after single commit | Build break from partial commit |

### Architecture-Locked Constraints for This Ticket
- All 5 change points MUST be in a single atomic commit. Any partial apply breaks the build.
- `ParseTargetLegIndex` is added as a `private static` method within `CopyEngine.cs` — NOT in a new file.
- PTT-preferred rule: "PTT-TGT-Drag-" prefix is checked before "Target" prefix in `ParseTargetLegIndex`.
- NT8 AddOnBase API only: existing `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` pattern preserved in `ResubmitCollateralLegs`. No new API calls introduced.
- `Account.Change()` BANNED. `AtmStrategyChangeStopTarget()` BANNED. Not used here.
- `using System.Collections.Generic;` must be present at top of file (may already exist).
- `ParseTargetLegIndex` returns `int` (not `int?`). Returns -1 on any invalid/excluded input. JS-002 compliant (returns -1 as default, not null, not throw).
- `// CYC=5` comment required on `ParseTargetLegIndex` (per plan-review OBS-CYC-02).
- No threading changes. `CaptureOtherLegTargetPrices` invocation-local Dictionary is not a class field and not shared between threads. JS-009 / JS-025 compliant.
- `DeriveLeaderBracketIndex` — NO CHANGE (already accepts any positive integer).
- `IsNativeTargetOrder` / `IsPttTargetOrder` — NO CHANGE (use `char.IsDigit`, already N-safe).
- Quick2t and QAll2t paths — NOT in scope, do not touch.

---

## Ticket DW-B153-04

### Title
Remove IsPttQxTarget single-digit ceiling (BE after QX)

### Spec Requirement
B153-REQ-04 — IsPttQxTarget ceiling removal

### File Affected
`src/PropTraderTools/Features/PttBreakEven.cs`

### Symbol Affected
`IsPttQxTarget` (~line 588)

### Method Signature (unchanged)
```csharp
private static bool IsPttQxTarget(string name)
```

### Change — Before

```csharp
private static bool IsPttQxTarget(string name)
{
    if (name == null || name.Length != 9)
        return false;
    return name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
        && name[8] >= '1'
        && name[8] <= '3';
}
```

### Change — After

```csharp
// CYC=5
private static bool IsPttQxTarget(string name)
{
    // "PTT-QX-T" is 8 chars. Minimum valid name is "PTT-QX-T1" (length 9).
    // Substring(8) extracts "1", "2", "10", "22" etc.
    if (name == null || name.Length < 9)
        return false;
    if (!name.StartsWith("PTT-QX-T", StringComparison.Ordinal))
        return false;
    return int.TryParse(name.Substring(8), out int n) && n >= 1;
}
```

**Instructions for engineer:**
1. Locate `IsPttQxTarget` at approximately line 588 in `PttBreakEven.cs`.
2. Replace the entire method body as shown above.
3. The method signature (`private static bool IsPttQxTarget(string name)`) is UNCHANGED.
4. Change `name.Length != 9` to `name.Length < 9` — this accepts T10 (length 10), T22 (length 11), etc.
5. Split the return expression: `name.StartsWith(...)` becomes a standalone guard (`if (!...)  return false;`).
6. Replace `name[8] >= '1' && name[8] <= '3'` with `int.TryParse(name.Substring(8), out int n) && n >= 1`.
7. Add the `// CYC=5` annotation on the line before the method signature (per OBS-CYC-03 mandate).
8. The two comment lines explaining the boundary logic must be present exactly as shown.
9. Do NOT change the method signature.
10. Do NOT touch any other code in the file.

**Boundary rationale (must be understood by engineer):**
- `"PTT-QX-T"` is exactly 8 characters.
- The minimum valid target name is `"PTT-QX-T1"` — length 9.
- `"PTT-QX-T10"` (length 10) and `"PTT-QX-T22"` (length 11) must be accepted.
- The old `name.Length != 9` guard rejected all multi-digit indices.
- `name.Substring(8)` extracts everything after the prefix: `"1"`, `"10"`, `"22"`, etc.
- `int.TryParse` parses the extracted suffix; `n >= 1` rejects T0 and negative.
- `"PTT-QX-TX"` — TryParse fails → method returns false. Correct.

### CYC Analysis (corrected per OBS-CYC-03)

- **Before (strict JS-013 count):** base=1, `||` in first condition=+1, implicit `&&` in StartsWith chain (compound return) has multiple sub-conditions; strict count: +1 for null check path, +1 for StartsWith, +1 for char range. Total ~5.
- **After (strict JS-013 count):** base=1, `||` in null/length=+1, `if (!StartsWith)`=+1, `TryParse` in return (ternary-equivalent)=+1, `&& n >= 1`=+1. Total = **5**.
- Net CYC delta = **~0** (both before and after are ~5; OBS-CYC-03 corrects plan's stated 3→4 delta).
- Engineer MUST annotate `// CYC=5` (per OBS-CYC-03 mandate).
- Well within JS-013 limit (<=8).

### xUnit [Fact] Tests

| Test Name | Asserts |
|-----------|---------|
| `IsPttQxTarget_T4_ReturnsTrue` | "PTT-QX-T4" returns true (new capability, previously capped at T3) |
| `IsPttQxTarget_T10_ReturnsTrue` | "PTT-QX-T10" returns true (multi-digit support) |
| `IsPttQxTarget_T22_ReturnsTrue` | "PTT-QX-T22" returns true (N-ceiling removed) |
| `IsPttQxTarget_T0_ReturnsFalse` | "PTT-QX-T0" returns false (T0 invalid; n >= 1 guard) |
| `IsPttQxTarget_NullName_ReturnsFalse` | null returns false (null guard preserved) |
| `IsPttQxTarget_T3_StillReturnsTrue` | "PTT-QX-T3" returns true (regression — T3 still accepted) |

### 7-Scan Checklist

| Scan | Command | Expected | Applies? |
|------|---------|----------|---------|
| SCAN-1 | `grep -n "n > 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | NO — different file |
| SCAN-2 | `grep -n "i <= 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | NO — different file |
| SCAN-3 | `grep -n "new double\[3\]" src/PropTraderTools/CopyEngine.cs` | 0 matches | NO — different file |
| SCAN-4 | `grep -n "Math.Min.*3" src/PropTraderTools/Features/PttQuickExit.cs` | 0 matches | NO — different file |
| SCAN-5 | `grep -n "targets.Count > 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | NO — different file |
| **SCAN-6** | `grep -n "name.Length != 9" src/PropTraderTools/Features/PttBreakEven.cs` | **0 matches** | **YES — primary gate** |
| **SCAN-7** | `lizard src/PropTraderTools/Features/PttBreakEven.cs --CCN 8` | Warning cnt: 0 | **YES** |
| **SCAN-8** | `grep -rn "lock\s*(" src/PropTraderTools/` | 0 actual lock() blocks | **YES** |
| **SCAN-9** | ASCII-only byte check: `PttBreakEven.cs` | 0 bytes > 127 | **YES** |
| **SCAN-10** | `dotnet build` | 0 errors, 0 warnings | **YES** |

### Acceptance Criteria (Ph4b Verifier Pass/Fail)

| # | Criterion | Pass | Fail |
|---|-----------|------|------|
| AC-01 | SCAN-6: `grep -n "name.Length != 9" src/PropTraderTools/Features/PttBreakEven.cs` | 0 matches | Any match |
| AC-02 | SCAN-7: lizard PttBreakEven.cs --CCN 8 | Warning cnt: 0 | Any warning |
| AC-03 | SCAN-8: grep lock\s*( src/PropTraderTools/ | 0 actual lock() blocks | Any lock() found |
| AC-04 | SCAN-9: ASCII-only byte check PttBreakEven.cs | 0 bytes > 127 | Any byte > 127 |
| AC-05 | SCAN-10: dotnet build | 0 errors, 0 warnings | Any error or warning |
| AC-06 | xUnit: `IsPttQxTarget_T4_ReturnsTrue` | PASS | FAIL |
| AC-07 | xUnit: `IsPttQxTarget_T10_ReturnsTrue` | PASS | FAIL |
| AC-08 | xUnit: `IsPttQxTarget_T22_ReturnsTrue` | PASS | FAIL |
| AC-09 | xUnit: `IsPttQxTarget_T0_ReturnsFalse` | PASS | FAIL |
| AC-10 | xUnit: `IsPttQxTarget_NullName_ReturnsFalse` | PASS | FAIL |
| AC-11 | xUnit: `IsPttQxTarget_T3_StillReturnsTrue` | PASS | FAIL |

### Architecture-Locked Constraints for This Ticket
- `IsPttQxTarget` method signature must not change.
- `"PTT-QX-T"` prefix is the canonical PTT QX target naming — ASCII-only, no change to the prefix string.
- `name.Length < 9` (not `!= 9`) is the correct guard — accepts multi-digit suffixes. This is the key semantic change.
- No new NT8 API calls introduced.
- No threading changes required. Method is a pure string predicate.
- `// CYC=5` annotation is mandatory on this method (per OBS-CYC-03 mandate from plan-review).

---

## Global 10-Scan Master Table (Phase 4b Verifier Reference)

Phase 4b must run ALL 10 scans after all 4 tickets are implemented. All must return 0.

| # | Command | Expected | Ticket |
|---|---------|----------|--------|
| SCAN-1 | `grep -n "n > 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | DW-B153-03 |
| SCAN-2 | `grep -n "i <= 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | DW-B153-03 |
| SCAN-3 | `grep -n "new double\[3\]" src/PropTraderTools/CopyEngine.cs` | 0 matches | DW-B153-03 |
| SCAN-4 | `grep -n "Math.Min.*3" src/PropTraderTools/Features/PttQuickExit.cs` | 0 matches | DW-B153-01 |
| SCAN-5 | `grep -n "targets.Count > 3" src/PropTraderTools/CopyEngine.cs` | 0 matches | DW-B153-02 |
| SCAN-6 | `grep -n "name.Length != 9" src/PropTraderTools/Features/PttBreakEven.cs` | 0 matches | DW-B153-04 |
| SCAN-7 | `lizard src/PropTraderTools/Features/PttQuickExit.cs src/PropTraderTools/CopyEngine.cs src/PropTraderTools/Features/PttBreakEven.cs --CCN 8` | Warning cnt: 0 | ALL |
| SCAN-8 | `grep -rn "lock\s*(" src/PropTraderTools/` | 0 actual lock() blocks | ALL |
| SCAN-9 | ASCII-only byte check on all 3 edited files | 0 bytes > 127 | ALL |
| SCAN-10 | `dotnet build` | 0 errors, 0 warnings | ALL |

---

## SIM Gate (Director-Owned, Post-F5)

After all 4 tickets are implemented and all 10 scans pass, the Director runs the SIM gate:

1. Enter position with 5-target ATM template; drag stop → all 5 followers sync (T3 fix)
2. Quick ALL → 5 QX bracket pairs (PTT-QX-T1..T5) (T1 fix)
3. BE ALL → PTT-BE-Stop-1..5 + PTT-BE-Target-1..5 (T2 fix)
4. QX ALL, then BE ALL → IsPttQxTarget recognizes T4/T5 (T4 fix)

**PASS:** All 5 legs processed.
**FAIL:** Any leg 4/5 silently dropped.

---

*Tickets written by ptt-architect. Ready for ptt-ticket-reviewer (Phase 3.5).*

**TICKETS_COMPLETE**
