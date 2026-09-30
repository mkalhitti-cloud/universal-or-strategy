# DW-B94 Ticket-1 Completion Report

## Edit Summary

**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `IsNonFlatDispatchName` (~L1728)
**Change**: Added `IsAtmBracketName` guard as branch (3) to block follower flatten when NT8 OCO-cancels an ATM bracket order (Stop1..Stop9 / Target1..Target9) after a target fill.

## Root Cause

When leader ATM Target1 fills, NT8 OCO-cancels Stop1. That Cancelled event hits
`TryDispatchLeaderFlat` with `Name="Stop1"`:
- `IsNonFlatDispatchName("Stop1")` returned `false` (missing guard)
- `IsNativeExitName("Stop1")` returned `false`
- `hasOpenPosition()` returned `false` (NT8 position update race, NT8_FULL_REFERENCE line 1721)
- `FlattenFollower` fired on all followers
- PTT-Flatten dispatched to Sim102/Sim103/Sim104
- All PTT-BE brackets cancelled (followers lost stop protection)

## Before / After Diff

### Before (CYC=2)
```csharp
// IsNonFlatDispatchName: CYC=2. Returns true when orderName must NOT trigger follower flatten.
// Combines HOTFIX-B63-FLATTEN-01 (PTT- prefix) and HOTFIX-B64-ENTRY-FLATTEN-01 ("Entry").
// Both represent orders that mean "open a position" or "manage exit" - never "go flat now".
// JS-001: no throw. JS-002: returns bool (never null). JS-021: no lock. ASCII-only.
// TESTABILITY: internal static, string parameter, no NT8 runtime deps.
internal static bool IsNonFlatDispatchName(string orderName)
{
    if (orderName != null && orderName.StartsWith("PTT-", StringComparison.Ordinal))
        return true; // (1)
    if (orderName == "Entry")
        return true; // (2)
    return false;
}
```

### After (CYC=3)
```csharp
// IsNonFlatDispatchName: CYC=3. Returns true when orderName must NOT trigger follower flatten.
// Combines HOTFIX-B63-FLATTEN-01 (PTT- prefix), HOTFIX-B64-ENTRY-FLATTEN-01 ("Entry"),
// and DW-B94 (ATM bracket names Stop1..Stop9 / Target1..Target9).
// ATM bracket cancel events arrive during NT8 position update gap (NT8_FULL_REFERENCE line 1721)
// and must never trigger a follower flatten -- the position is still live.
// JS-001: no throw. JS-002: returns bool. JS-021: no lock. ASCII-only.
internal static bool IsNonFlatDispatchName(string orderName)
{
    if (orderName != null && orderName.StartsWith("PTT-", StringComparison.Ordinal))
        return true; // (1)
    if (orderName == "Entry")
        return true; // (2)
    if (IsAtmBracketName(orderName))
        return true; // (3) DW-B94: Stop1..Stop9 / Target1..Target9 -- ATM cancel must not flatten followers
    return false;
}
```

## Sync Result

```
COPIED:   CopyEngine.cs
Done. Copied: 1  Skipped (in sync): 15  Excluded (tests/obj/bin): 36
```

## F5 Result

PENDING -- Director must F5 in NinjaTrader to confirm GREEN compile.

## CYC Change

| Metric | Before | After |
|--------|--------|-------|
| CYC    | 2      | 3     |

## JS-DNA Compliance

- [x] Zero `lock()` added
- [x] Zero `throw new` added
- [x] Zero `return null` added
- [x] Zero `async void` added
- [x] ASCII-only strings and comments
- [x] No LINQ in changed code
- [x] No new heap allocations on hot path

## Status

EDIT_DONE -- awaiting Director F5 compile confirmation.
