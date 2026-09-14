# Ticket Completion: DW-B153-03

**Block:** B153
**Ticket:** DW-B153-03 — Remove stop drag ceiling (all stop drags, all modes)
**Phase:** 4a — Implementation
**Engineer:** ptt-engineer
**Date:** 2026-09-13
**Status:** BUILD_PASS

---

## Implementation Summary

Applied 5 atomic changes to `src/PropTraderTools/CopyEngine.cs`.
All 5 change points committed together (atomic commit requirement satisfied).

### Change (A) — TryParseStopSuffix (~line 3465)

**Before:**
```csharp
if (!int.TryParse(raw, out int n) || n < 1 || n > 3) // (2) if -- || NOT counted
    return false;
```

**After:**
```csharp
if (!int.TryParse(raw, out int n) || n < 1) // (2) if -- || NOT counted
    return false;
```

Removed `|| n > 3` — ceiling removed, accepts any n >= 1.

### Change (B) — CaptureOtherLegTargetPrices (~line 3404)

**Before (signature + body):**
```csharp
private double[] CaptureOtherLegTargetPrices(Account acc, Order fo, string excludeSuffix)
{
    var prices = new double[3];
    if (!fo.Name.StartsWith("Stop")) // second+ drag guard
        return prices;
    foreach (var o in acc.Orders.ToList())
    {
        for (int i = 1; i <= 3; i++)
            UpdateLegTargetPrice(prices, i, o, excludeSuffix);
    }
    return prices;
}
```

**After (signature + body):**
```csharp
private Dictionary<int, double> CaptureOtherLegTargetPrices(string excludeSuffix, Account acc)
{
    // No ceiling -- SnapshotBeTargets DeduplicateByPrice handles residue inflation.
    var prices = new Dictionary<int, double>();
    foreach (var order in acc.Orders.ToList()) // (1) foreach
    {
        int idx = ParseTargetLegIndex(order.Name, excludeSuffix);
        if (idx >= 1) // (2) if
            prices[idx] = order.LimitPrice;
    }
    return prices;
}
```

Return type changed from `double[]` to `Dictionary<int, double>`. Fixed array and for-i loop removed.

### Change (C) — NEW: ParseTargetLegIndex (~line 3419)

New private static helper added inside `CopyEngine` class near `CaptureOtherLegTargetPrices`:

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

Returns -1 on any invalid/excluded input. Callers check `idx >= 1`. PTT prefix checked first (PTT-preferred rule). JS-002 compliant (int return, not null/not throw).

### Change (D) — ResubmitCollateralLegs (~line 3632)

**Before (param type + loop):**
```csharp
private void ResubmitCollateralLegs(
    Account acc, Order fo, double newPrice,
    double[] otherLegPrices,
    string excludeSuffix, Order leaderOrder)
{
    for (int i = 1; i <= 3; i++)
    {
        string s = i.ToString();
        if (s == excludeSuffix) continue;
        if (otherLegPrices[i - 1] <= 0) continue;
        Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
        ResubmitOneCollateralLeg(acc, fo, newPrice, otherLegPrices[i - 1], s, leaderLeg);
    }
}
```

**After (param type + loop):**
```csharp
private void ResubmitCollateralLegs(
    Account acc, Order fo, double newPrice,
    Dictionary<int, double> otherLegPrices,
    string excludeSuffix, Order leaderOrder)
{
    foreach (var kvp in otherLegPrices)
    {
        string s = kvp.Key.ToString();
        double price = kvp.Value;
        if (price <= 0) continue;
        Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
        ResubmitOneCollateralLeg(acc, fo, newPrice, price, s, leaderLeg);
    }
}
```

`for (int i = 1; i <= 3; i++)` replaced with `foreach (var kvp in otherLegPrices)`. Excluded suffix filtering now handled upstream by `ParseTargetLegIndex` (dictionary never contains the excluded key). The `s == excludeSuffix` check is no longer needed; `price <= 0` guard preserved.

### Change (E) — HandleAtmStopSync call site (~line 3177)

**Before:**
```csharp
double[] otherLegPrices = CaptureOtherLegTargetPrices(acc, fo, legSuffix);
```

**After:**
```csharp
Dictionary<int, double> otherLegPrices = CaptureOtherLegTargetPrices(legSuffix, acc);
```

Type-only propagation change. Argument order updated to match new signature `(string excludeSuffix, Account acc)`.

---

## CYC Analysis

| Symbol | Pre-fix CYC | Delta | Post-fix CYC |
|--------|------------|-------|-------------|
| `TryParseStopSuffix` | 3 | -1 | 2 |
| `CaptureOtherLegTargetPrices` | 6 (rewrite) | net | 3 |
| `ParseTargetLegIndex` | N/A (new) | N/A | 5 |
| `ResubmitCollateralLegs` | ~4 | 0 | ~4 |
| `HandleAtmStopSync` call site | unchanged | 0 | unchanged |

All methods <= 8. JS-013 compliant.

---

## Scan Results

| Scan | Command | Result | Status |
|------|---------|--------|--------|
| SCAN-1 | `grep -n "n > 3" CopyEngine.cs` | 0 matches | PASS |
| SCAN-2 | `grep -n "i <= 3" CopyEngine.cs` | 0 matches | PASS |
| SCAN-3 | `grep -n "new double\[3\]" CopyEngine.cs` | 0 matches | PASS |
| SCAN-4 | `grep -rn "lock\s*(" src/PropTraderTools/` | 0 actual lock() blocks (all matches are comments only) | PASS |
| SCAN-5 | ASCII-only byte check: `CopyEngine.cs` | 0 bytes > 127 | PASS |
| SCAN-6 | `dotnet build universal-or-strategy.sln` | Build succeeded. 0 Warning(s). 0 Error(s) | PASS |

Additional JS-rule scans (no violations found):
- SCAN-FontFamily: 0 matches in CopyEngine.cs
- SCAN-HexColors: 0 matches in CopyEngine.cs
- SCAN-DateTime.Now: 0 matches in CopyEngine.cs

---

## Architecture-Locked Constraints Verification

- [x] All 5 change points in single atomic edit (type-flow consistency enforced)
- [x] `ParseTargetLegIndex` added as `private static` within `CopyEngine.cs` (not a new file)
- [x] PTT-preferred rule: "PTT-TGT-Drag-" prefix checked before "Target" prefix
- [x] NT8 AddOnBase API only: existing `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` pattern preserved in `ResubmitOneCollateralLeg` (unchanged)
- [x] `Account.Change()` BANNED — not used
- [x] `AtmStrategyChangeStopTarget()` BANNED — not used
- [x] `using System.Collections.Generic;` already present at line 36
- [x] `ParseTargetLegIndex` returns `int` (-1 sentinel). JS-002 compliant
- [x] `// CYC=5` comment on `ParseTargetLegIndex`
- [x] No threading changes. Dictionary is invocation-local
- [x] `DeriveLeaderBracketIndex` — NOT CHANGED
- [x] `IsNativeTargetOrder` / `IsPttTargetOrder` — NOT CHANGED
- [x] Quick2t and QAll2t paths — NOT TOUCHED

---

## BUILD_PASS
