# B197 Ticket 1 Completion

**Engineer**: ptt-engineer
**Epic**: B197 -- Gap Restore
**Ticket**: T1 -- TrySetSlateReadyOnDrainFill (GAP-5, DW-B157-01)
**Phase**: 4a (Implementation)
**Date**: 2026-10-12
**Status**: BUILD_PASS

---

## Scope Confirmation

SCOPE LOCK honored. Only Ticket 1 was implemented in this session.

Files modified:
- `src/PropTraderTools/CopyEngine.cs` -- new method + call site
- `src/PropTraderTools/Tests/B197Tests.cs` -- created with 2 [Fact]s (T1 only)
- `src/PropTraderTools/PropTraderTools.Tests.csproj` -- added Tests\B197Tests.cs reference

---

## Implementation Summary

### New Method: TrySetSlateReadyOnDrainFill

Added at line 1070 (after closing brace of TrySetFollowerSlateReady, ~line 1062).

```csharp
private void TrySetSlateReadyOnDrainFill(OrderEventArgs e)
{
    if (e?.Order == null) return;                                          // (1)
    if (e.Order.OrderState != OrderState.Filled) return;                   // (2)
    if (!_drainOwnedOrderIds.ContainsKey(e.Order.OrderId)) return;         // (3)
    if (!IsFollowerAccount(e.Order.Account)) return;                       // (4)
    _followerSlateReady[e.Order.Account.Name] = true;
    NinjaTrader.Code.Output.Process(
        "[SLATE-READY-DRAIN] acct=" + e.Order.Account.Name,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
```

### Call Site Wiring: OnOrderUpdate Filled branch

Replaced the single AbortDrainOnFill call block with the ordered 3-call block.
TrySetSlateReadyOnDrainFill is FIRST (before AbortDrainOnFill) per ordering invariant.
T3 call site added as TODO placeholder (TryCancelStaleGtcExitOnEntry not yet implemented):

```csharp
else if (e.Order.OrderState == OrderState.Filled)
{
    TrySetSlateReadyOnDrainFill(e);                  // T1: set slate-ready FIRST
    AbortDrainOnFill(e.Order.Account.Name);          // R2-F1: clean _drainOwnedOrderIds
    // TODO B197-T3: TryCancelStaleGtcExitOnEntry(e);
}
```

Ordering invariant satisfied: TrySetSlateReadyOnDrainFill runs before AbortDrainOnFill.
Guard (3) (_drainOwnedOrderIds.ContainsKey) executes while dict still contains the ID.

---

## DW IDs Closed

| DW ID | Status |
|-------|--------|
| DW-B157-01 | CLOSED -- TrySetSlateReadyOnDrainFill now wired into OnOrderUpdate Filled branch |

---

## 7-Scan Results

### Scan 1 -- lock() grep
Command: `Get-ChildItem -Path "src\PropTraderTools" -Filter "*.cs" -Recurse | Select-String -Pattern "lock\("`
Result: All 36 hits are in comments only (e.g., "// JS-021: no lock()" -- no executable lock() calls).
**PASS -- 0 new lock() occurrences introduced by T1**

### Scan 2 -- CYC check (Lizard)
Command: `python -m lizard src/PropTraderTools/CopyEngine.cs --CCN 8`
Result: TrySetSlateReadyOnDrainFill CCN=6 (Lizard counts e?.Order as +1 extra branch vs ticket estimate of 5).
CCN=6 is <= 8. PASS.
Note: e?.Order == null guard adds 2 Lizard branches (if + ?.) = base(1)+guard1_if(1)+guard1_null_cond(1)+guard2(1)+guard3(1)+guard4(1) = 6.
**PASS -- CYC=6 <= 8 ceiling**

### Scan 3 -- ASCII check
Command: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "[^\x00-\x7F]"`
Result: No output (0 non-ASCII characters).
**PASS -- 0 non-ASCII characters**

### Scan 4 -- throw new check
Command: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "throw new"`
Result: No output (0 throw new occurrences).
**PASS -- 0 throw new in entire file**

### Scan 5 -- nullable guard check
Guard 1: `if (e?.Order == null) return;` -- covers both e==null and e.Order==null. Present at line 1072.
Guard 4: `if (!IsFollowerAccount(e.Order.Account)) return;` -- e.Order confirmed non-null by Guard 1. Present at line 1075.
**PASS -- both guards present before body execution**

### Scan 6 -- dotnet build
Command: `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj`
Result: 0 Error(s), 1367 Warning(s) (all pre-existing xUnit1004 Skip warnings, none introduced by T1).
**PASS -- 0 errors**

### Scan 7 -- dotnet test --filter B197
Command: `dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "B197"`
Result: Passed! Failed: 0, Passed: 2, Skipped: 0, Total: 2
- TrySetSlateReadyOnDrainFill_SourceContainsFollowerSlateReady: PASS
- TrySetSlateReadyOnDrainFill_SourceContainsSlateReadyDrainMarker: PASS
**PASS -- 2/2 B197 tests pass**

---

## Success Criteria Checklist

| # | Criterion | Result |
|---|-----------|--------|
| 1 | TrySetSlateReadyOnDrainFill method body exists in CopyEngine.cs | PASS |
| 2 | Method body contains _followerSlateReady[ write | PASS |
| 3 | Method body contains [SLATE-READY-DRAIN] log marker | PASS |
| 4 | TrySetSlateReadyOnDrainFill(e) appears FIRST in else if (OrderState.Filled) block, before AbortDrainOnFill | PASS |
| 5 | All 7 scans pass (0 lock, CYC<=8, ASCII-only, no new throw, guards present, build clean, T1 tests pass) | PASS |
| 6 | dotnet test --filter B197 shows both Facts PASS | PASS |

---

## BUILD_PASS