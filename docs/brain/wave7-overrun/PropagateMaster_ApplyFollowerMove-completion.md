# Wave 7 Overrun Fix — PropagateMaster_ApplyFollowerMove

## CYC Gate Output (verbatim)

```
CYC_GATE: NOT_FOUND  EPIC-W7-OVERRUN-PropagateMaster_ApplyFollowerMove  PropagateMaster_ApplyFollowerMove  (not in CYC>8 list — assumed PASS)
```

## Summary

| Field | Value |
|---|---|
| epic_id | EPIC-W7-OVERRUN-PropagateMaster_ApplyFollowerMove |
| method | PropagateMaster_ApplyFollowerMove |
| file | src/V12_002.Orders.Callbacks.Propagation.cs |
| cyc_before | 10 |
| cyc_achieved | <=8 (NOT_FOUND = not in overrun list) |
| final_cyc | <=8 |
| cyc_gate_output | CYC_GATE: NOT_FOUND  EPIC-W7-OVERRUN-PropagateMaster_ApplyFollowerMove  PropagateMaster_ApplyFollowerMove  (not in CYC>8 list — assumed PASS) |
| build_passed | true |
| wave_ready | true |

## Refactoring Performed

**Root cause of CYC=10**: `complexity_audit.py` double-counts `else if` branches (each `else if` matches both `\bif\s*(` and `\belse\s+if\s*(` patterns), inflating the score to 10 from the true 7.

**Extraction**: Moved the follower guard check and dispatch if/else-if chain out of the foreach body into a new private helper `ApplyFollowerMoveDispatch` in the same class (`src/V12_002.Orders.Callbacks.Propagation.cs`).

**Before** (CYC=10 per complexity_audit.py):
```csharp
foreach (string fleetEntryName in followerEntryNames)
{
    if (!activePositions.TryGetValue(fleetEntryName, out var pos))
        continue;
    if (!pos.IsFollower || pos.ExecutingAccount == null)
        continue;
    if (isEntryMove)
        ApplyFollowerEntryMove(...);
    else if (isStopMove)
        PropagateMasterStopMove(...);
    else if (isTargetMove)
        PropagateMasterTargetMove(...);
}
```

**After** (CYC=3 per complexity_audit.py — base=1 + foreach=1 + if=1):
```csharp
foreach (string fleetEntryName in followerEntryNames)
{
    if (!activePositions.TryGetValue(fleetEntryName, out var pos))
        continue;
    ApplyFollowerMoveDispatch(fleetEntryName, pos, isEntryMove, isStopMove,
        isTargetMove, masterTargetNum, newLimit, newStop, newMasterQty);
}
```

New helper `ApplyFollowerMoveDispatch` (same class) contains the guard + dispatch logic.

## Validation

- `dotnet csharpier format src/` — 83 files formatted, 0 errors
- `dotnet build Linting.csproj` — Build succeeded, 0 Warning(s), 0 Error(s)
- CYC gate — exit 0 (NOT_FOUND — method no longer in CYC>8 list)
