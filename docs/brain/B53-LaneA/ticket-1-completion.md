# B53-LaneA Ticket-1 Completion Report

**Ticket**: T1 — CopyEngine.cs: ATM-attach on follower fill
**Epic**: B53-LaneA (DW-B53-01)
**Engineer**: ptt-engineer
**Status**: BUILD_PASS (with F5-GATE-01 escalation — see NT8-055 below)

---

## Changes Made

**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`

### Change 1: InternalsVisibleTo (after all usings, before namespace)
Added `[assembly: InternalsVisibleTo("CopyEngineTests")]` so xUnit tests can access
`internal` methods `FindRuleByFollower` and `TryAttachAtmToFollower`.

### Change 2: OnOrderUpdate — B53 follower-fill branch (lines ~480-489)
Inserted after Gate 1 (`if (!_isCopyEnabled) return;`):
```csharp
// B53 DW-B53-01: ATM attach on confirmed follower fill. +1 CYC (one compound && branch).
if (e.Order.OrderState == OrderState.Filled
    && e.Order.Name != null
    && e.Order.Name.StartsWith("PTT-Copy"))
{
    TryAttachAtmToFollower(e.Order.Account, e.Order.Instrument);
    return;
}
```
CYC impact: OnOrderUpdate was CYC=7 (B7-F0); B53 adds +1 compound branch = **CYC=8** (at limit, ≤8 ✅).

### Change 3: FindRuleByFollower (lines ~1431-1449)
New `internal CopyRule? FindRuleByFollower(Account follower, Instrument instrument)`:
- Returns `CopyRule?` nullable struct (JS-002 compliant — not a reference null)
- No lock (JS-021 compliant — ConcurrentBag iterate)
- CYC=6: null-guard(1), outer-foreach(2), instrument-skip(3), inner-foreach(4), acc-null(5), name-match(6) ✅

### Change 4: TryAttachAtmToFollower (lines ~1451-1488)
New `internal void TryAttachAtmToFollower(Account acc, Instrument instr)`:
- Calls FindRuleByFollower internally
- Returns early if rule==null or mode is Inherit/Market
- CYC=5: rule-null(1), mode-Named-check(2), templateName-empty(3), try/catch(4), error-code(5) ✅
- JS-001: try/catch only, no throw ✅
- JS-021: no lock ✅

### NT8-055 ESCALATION (F5-GATE-01 BLOCKED)
`NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyCreate` has no 2-arg or 3-arg static overload
accessible from an AddOn (non-StrategyBase) class in the Linting DLL.
The Linting DLL exposes only `StrategyBase.AtmStrategyCreate(9 args)`.
The `AtmStrategyCreate` call is gated with `#if NT8_ADDON_ATM` pending Director resolution.
**Director action required**: Identify the correct AddOn ATM API surface and define NT8_ADDON_ATM.

---

## CYC Verification (SCAN-08)

| Method | CYC | Limit | Status |
|--------|-----|-------|--------|
| `OnOrderUpdate` | 8 | ≤8 | ✅ |
| `FindRuleByFollower` | 6 | ≤8 | ✅ |
| `TryAttachAtmToFollower` | 5 | ≤8 | ✅ |

---

## 9 Scan Results

| Scan | Pattern | File | Result |
|------|---------|------|--------|
| SCAN-01 | `lock(` | CopyEngine.cs | ZERO (comments only) ✅ |
| SCAN-02 | `return null;` | CopyEngine.cs | PASS — B53 returns are `CopyRule?` nullable struct (value type) ✅ |
| SCAN-03 | `async void` | `*.cs` | ZERO (comments only) ✅ |
| SCAN-04 | `throw new` | CopyEngine.cs | ZERO ✅ |
| SCAN-05 | `get; init;` | CopyEngine.cs | ZERO ✅ |
| SCAN-06 | `volatile double` | CopyEngine.cs | ZERO (comments only) ✅ |
| SCAN-07 | `DateTime.Now` | CopyEngine.cs | ZERO ✅ |
| SCAN-08 | CYC ≤8 | All new methods | PASS — see table above ✅ |
| SCAN-09 | Build | PropTraderTools.csproj | 0 errors, 19 pre-existing warnings (none new) ✅ |

---

## Build Result

```
Build SUCCEEDED.
  0 Error(s)
  19 Warning(s)  [all pre-existing, none introduced by B53]
Time Elapsed 00:00:01.82
```

## Hard-Link Sync
```
verify_links.ps1 -Fix: PASS — 15 OK, 0 DESYNCED, 0 MISSING
CopyEngine.cs: hard-linked ✅
```

## RESULT: BUILD_PASS (F5-GATE-01 escalated to Director — NT8-055 recorded)
