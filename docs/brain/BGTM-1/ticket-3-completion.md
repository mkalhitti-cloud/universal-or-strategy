# BGTM-1 Ticket 3 — Completion Report

**Ticket**: BGTM-1 / Ticket 3  
**File Modified**: `src/PropTraderTools/TradeCopierAddOn.cs`  
**Engineer**: ptt-engineer  
**Date**: 2026-08-26  
**Result**: BUILD_PASS

---

## What Was Implemented

### Change 1 — State.Configure block in OnStateChange() (L71–L76)

Added `if (State == State.Configure)` block immediately after the `State.SetDefaults` block.

```csharp
if (State == State.Configure)
{
    var flags = LoadAndValidateLicense();
    CopyEngine.Instance.SetFlags(flags);
}
```

**Location**: Lines 71–76 (after original L70 closing brace of SetDefaults block).

### Change 2 — ClickTrader gate prepended to RegisterClickTrader() (L294–L301)

Added feature-flag gate as the absolute first executable statement of `RegisterClickTrader`.
Uses `NinjaTrader.Code.Output.Process` since `TradeCopierAddOn` has no `StatusUpdate` method
(this is an `AddOnBase` subclass; the `StatusUpdate` pattern lives on `CopyEngine`).

```csharp
if (!CopyEngine.Instance.Flags.ClickTrader)
{
    NinjaTrader.Code.Output.Process(
        "Click Trader requires Elite tier",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    return;
}
```

**Location**: Lines 294–301 (prepended inside `RegisterClickTrader` before the existing `chart == null` guard).

**CYC impact**: `RegisterClickTrader` was CYC=2 (per L284 comment). After gate: CYC=3. PASS (≤8).

### Change 3 — LoadAndValidateLicense() private static helper (L629–L648)

Added after the last visual-tree helper (`FindVisualChildByName`) and before the closing class brace.

```csharp
// BGTM-1: Read license.txt, validate via LicenseClient. CYC=2.
// JS-001: no throw -- any I/O error returns Starter().
// NT8: File.ReadAllText is safe in State.Configure (not the hot path).
private static FeatureFlags LoadAndValidateLicense()
{
    try
    {
        var licenseTxt = System.IO.Path.Combine(
            NinjaTrader.Core.Globals.UserDataDir,
            "PropTraderTools",
            "license.txt");
        var key = System.IO.File.Exists(licenseTxt)
            ? System.IO.File.ReadAllText(licenseTxt).Trim()
            : string.Empty;
        return LicenseClient.Validate(key);
    }
    catch (Exception)
    {
        return FeatureFlags.Starter();
    }
}
```

**Location**: Lines 629–648.  
**CYC**: 2 (try body = branch 1, catch = branch 2; base = 1; total = 2). PASS.

---

## Deviations from Ticket Spec

| Item | Ticket Spec | Actual | Reason |
|------|-------------|--------|--------|
| Gate log call | `StatusUpdate("Click Trader requires Elite tier")` | `NinjaTrader.Code.Output.Process(...)` | `TradeCopierAddOn` (AddOnBase) has no `StatusUpdate` method. Ticket step instructions explicitly list this as the correct fallback: "use NinjaTrader.Code.Output.Process(...)" |

---

## CYC Audit

| Method | CYC (before) | CYC (after) | Status |
|--------|-------------|-------------|--------|
| `OnStateChange` | 2 (SetDefaults + Terminated) | 3 (+Configure block) | PASS (≤8) |
| `RegisterClickTrader` | 2 (per L284 comment) | 3 (+ClickTrader gate) | PASS (≤8) |
| `LoadAndValidateLicense` | n/a (new) | 2 (try/catch) | PASS (≤8) |

---

## 7-Scan Results

### SCAN-01 — lock() scan
**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierAddOn.cs" -Pattern "lock\s*\("` (filtered comments)  
**Result**: 0 matches  
**Status**: PASS ✅

### SCAN-02 — throw new scan
**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierAddOn.cs" -Pattern "throw new "` (filtered comments)  
**Result**: 0 matches  
**Status**: PASS ✅

### SCAN-03 — LoadAndValidateLicense presence check
**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierAddOn.cs" -Pattern "LoadAndValidateLicense"`  
**Result**: 2 matches — definition at L629, call at L73  
**Status**: PASS ✅

### SCAN-04 — RegisterClickTrader presence check
**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierAddOn.cs" -Pattern "RegisterClickTrader"`  
**Result**: definition at L292, unregister at L313, call at L105  
**Status**: PASS ✅

### SCAN-05 — Non-ASCII byte scan
**Command**: PowerShell byte scan over entire file  
**Result**: 0 non-ASCII bytes  
**Status**: PASS ✅

### SCAN-06 — ClickTrader / SetFlags / LoadAndValidate wiring
**Command**: `Select-String ... -Pattern "ClickTrader|SetFlags|LoadAndValidate"`  
**Result**:
- `LoadAndValidateLicense()` called at L73, defined at L629
- `CopyEngine.Instance.SetFlags(flags)` at L74
- `CopyEngine.Instance.Flags.ClickTrader` gate at L294
**Status**: PASS ✅

### SCAN-07 — sealed record / lock() final check
**Command**: `Select-String ... -Pattern "sealed record|lock\s*\("` (filtered comments)  
**Result**: 0 matches  
**Status**: PASS ✅

---

## Summary

All 3 implementation steps completed. All 7 scans at zero. CYC within bounds on all affected methods.

**BUILD_PASS**
