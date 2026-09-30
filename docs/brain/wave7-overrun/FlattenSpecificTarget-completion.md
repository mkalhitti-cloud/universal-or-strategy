# FlattenSpecificTarget - Overrun Completion

## CYC Gate Output
CYC_GATE: PASS  EPIC-W7-OVERRUN-FlattenSpecificTarget  FlattenSpecificTarget  CYC=7

## Summary

| Field | Value |
|-------|-------|
| Method | FlattenSpecificTarget |
| File | src/V12_002.UI.IPC.Commands.Misc.cs |
| CYC Before | 9 |
| CYC After | 7 |
| Build | 0 errors |
| Gate Exit | 0 (PASS) |

## New Helper Methods Added

- `FlattenSpecificTarget_IsPositionReady(string entryName, PositionInfo pos)` — combines the `activePositions.ContainsKey` guard and the `EntryFilled && RemainingContracts > 0` guard into a single boolean helper, placed in the same class and same file immediately before the parent method.

## Extraction Description

Extracted two guard conditions from the `foreach` body into a single private helper:

**Before (parent had CYC=9):**
```csharp
if (!activePositions.ContainsKey(kvp.Key))
    continue;
PositionInfo pos = kvp.Value;
string entryName = kvp.Key;

if (!pos.EntryFilled || pos.RemainingContracts <= 0)
    continue;
```

**After (parent now CYC=7):**
```csharp
PositionInfo pos = kvp.Value;
string entryName = kvp.Key;

if (!FlattenSpecificTarget_IsPositionReady(entryName, pos))
    continue;
```

**Helper added:**
```csharp
private bool FlattenSpecificTarget_IsPositionReady(string entryName, PositionInfo pos)
{
    if (!activePositions.ContainsKey(entryName))
        return false;
    return pos.EntryFilled && pos.RemainingContracts > 0;
}
```

## Build: 0 errors

`dotnet build Linting.csproj` → `Build succeeded. 0 Warning(s) 0 Error(s)`
