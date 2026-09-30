# Completion: IsMasterReplaceCascadeCancellation

## CYC Gate Output

```
CYC_GATE: PASS  EPIC-W7-OVERRUN-IsMasterReplaceCascadeCancellation  IsMasterReplaceCascadeCancellation  CYC=8
```

## Summary

| Field | Value |
|---|---|
| Epic ID | EPIC-W7-OVERRUN-IsMasterReplaceCascadeCancellation |
| Method | IsMasterReplaceCascadeCancellation |
| File | src/V12_002.Orders.Callbacks.AccountOrders.cs |
| CYC before | 13 |
| CYC after | 8 |
| cyc_achieved | 8 |
| final_cyc | 8 |
| build_passed | true |
| wave_ready | true |

## Extraction

### Helper method added (same class, same file)

**`IsFollowerSpecMatchForMasterReplace(string followerEntry, string masterEntryName) -> bool`**

Encapsulates the 4-branch inner loop body from the original foreach:
1. `_followerReplaceSpecs.TryGetValue` + null guard
2. `spec.State` is PendingCancel or Submitting check
3. `spec.MasterSignalName` equals masterEntryName check
4. `spec.SignalName` equals followerEntry check

### CYC reduction logic

Original main method had the foreach body inline with 6 decision points inside the loop
(TryGetValue||null = 2, State&&State = 2, MasterSignalName = 1, SignalName = 1).
Extracting these into `IsFollowerSpecMatchForMasterReplace` reduced the main method
from 12 decision points (CYC=13) to 7 decision points (CYC=8).

## Build

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Build: 0 errors
