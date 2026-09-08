TICKET: 1
FINDINGS: C1, C2, C3
FILE: src/PropTraderTools/Features/PttGlobalQuickExit.cs
BUILD: 0 errors / 0 warnings (referencing changed file)
LIZARD CCN>8: none
LOCK SCAN: 0 matches
LINQ SCAN: 0 matches
ASCII SCAN: 0 matches
STATUS: BUILD_PASS

---

## Implementation Summary

### C1 -- IsTargetOrder state filter (FIXED)

`IsTargetOrder` previously checked only `Working` and `Accepted` states.
After a BE cancellation, targets in `Submitted`, `Initialized`, or `TriggerPending`
state were excluded, causing global exit to flatten instead of swap brackets.

Fix: The 5-way OR state check was extracted into a new private helper
`IsTargetOrderState(OrderState state)` (CYC=5), and `IsTargetOrder` delegates
to it (CYC=4). This keeps both methods within the CYC<=8 Jane Street standard.
Lizard confirmed no method in the file exceeds CCN=8.

New helper added:
  private static bool IsTargetOrderState(NinjaTrader.Cbi.OrderState state)
  -- checks: Working || Accepted || Submitted || Initialized || TriggerPending

### C2 -- acc.Orders.ToList() LINQ violation NT8-006 (FIXED)

Two violations removed:
1. `CancelPttBeOrders` (was line 672): replaced `acc.Orders.ToList()` with manual
   snapshot loop using variable `_snap` / `snapshot`.
2. `WaitForPttBeCancelled` (was line 723): replaced `acc.Orders.ToList()` with manual
   snapshot loop using variable `_snap` / `pollSnapshot`.

`using System.Linq;` removed from file top (was the sole LINQ consumer).

XML doc comments updated in both methods to include:
  "NT8-006: no LINQ -- manual snapshot"

### C3 -- acc.Cancel not wrapped in try/catch (FIXED)

`acc.Cancel(toCancel)` in `CancelPttBeOrders` is now wrapped:
  try { acc.Cancel(toCancel); }
  catch (Exception ex) { NinjaTrader.Code.Output.Process("[PTT-QX-ALL] CancelPttBeOrders: acc=" + acc.Name + " Cancel exception: " + ex.Message, ...) }

Mirrors existing catch-and-log pattern. CYC of `CancelPttBeOrders` unchanged.

---

## Scan Results

SCAN-01 (lock scan):
  Command: Select-String -Path ...PttGlobalQuickExit.cs -Pattern "lock\("
  Result:  0 matches -- PASS

SCAN-02 (LINQ scan):
  Command: Select-String -Path ...PttGlobalQuickExit.cs -Pattern ".ToList()|System.Linq|.Where(|.Select(|.Any(|.Count("
  Result:  0 matches -- PASS

SCAN-03 (ASCII scan):
  Command: [System.IO.File]::ReadAllBytes(...) | Where-Object { $_ -gt 127 }
  Result:  0 non-ASCII bytes -- PASS

SCAN-04 (CCN scan):
  Command: lizard ...PttGlobalQuickExit.cs -x "*/bin/*" -x "*/obj/*"
  Result:  No method exceeds CCN=8 -- PASS
  Notable: IsTargetOrderState CCN=5, IsTargetOrder CCN=4, CancelPttBeOrders CCN=6,
           WaitForPttBeCancelled CCN=6 (unchanged from pre-fix baselines)

BUILD:
  Command: dotnet build C:\WSGTA\ptt-features\Linting.csproj
  Result:  Build succeeded -- 0 errors, 0 warnings in changed file -- PASS
