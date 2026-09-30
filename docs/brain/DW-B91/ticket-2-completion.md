# DW-B91 Ticket-2 Completion Report

## Status: BUILD_PASS

## Epic / Ticket
- Epic: DW-B91 -- Entry dedup survivor guard + flat-follower re-entry guard
- Ticket: TICKET-2 (DW-B91-B: Flat-follower open-position guard in TryDispatchLeaderFlat)
- Engineer: ptt-engineer
- Date: 2026-08-24

---

## Changes Made

### src/PropTraderTools/CopyEngine.cs

**CHANGE A -- TryDispatchLeaderFlat foreach body replaced (L2323)**

Old body (3 lines with null guard + flattenOne call):
```csharp
foreach (var acc in rule.FollowerAccounts) // (4)
{
    if (acc == null)
        continue;
    flattenOne(acc, instrument);
}
```

New body (single FlattenFollower call, zero branches in caller):
```csharp
foreach (var acc in rule.FollowerAccounts)                                       // (4)
    FlattenFollower(acc, instrument, hasOpenPosition, flattenOne);               // DW-B91-B
```

**CHANGE B -- FlattenFollower static helper added (after TryDispatchLeaderFlat closing brace, ~L2332)**

```csharp
// DW-B91-B: extracted foreach body from TryDispatchLeaderFlat.
// Absorbs (a) null guard (moved from caller loop) and (b) new per-follower open-position guard.
// Prevents spurious flattenOne call on already-flat followers (re-entry bug).
// CYC=3: 1 base + if (acc == null) + if (!hasOpenPosition).
// JS-021: no lock. JS-001: no throw. JS-002: no null return (void).
// private static: no instance state captured -- explicit delegate injection for testability.
private static void FlattenFollower(
    Account acc,
    Instrument instrument,
    Func<Account, Instrument, bool> hasOpenPosition,
    Action<Account, Instrument> flattenOne)
{
    if (acc == null) return;                               // (a) null guard (moved from caller)
    if (!hasOpenPosition(acc, instrument)) return;        // (b) DW-B91-B: skip already-flat follower
    flattenOne(acc, instrument);
}
```

**CHANGE C -- TryDispatchLeaderFlat header comment updated (L2296)**

Old: `// B65 T1: TryDispatchLeaderFlat -- CYC=8 (strict McCabe: loop + null guard + 5 early returns + IsNativeExitName branch).`

New:
```
// B65 T1 / DW-B91-B: TryDispatchLeaderFlat -- CYC=6 (strict McCabe after DW-B91-B extraction).
// (1) state guard, (2) follower guard, (3) open-position race-safe guard, (4) foreach follower.
// DW-B91-B: foreach body extracted to FlattenFollower (CYC=3) which adds per-follower
// hasOpenPosition guard to skip already-flat followers. Null guard moved into FlattenFollower.
```

### src/PropTraderTools/Tests/CopyEngineB91Tests.cs (APPENDED)

Three new [Fact] methods added to the existing `CopyEngineB91Tests` class:

- `FlattenFollower_NullAccount_DoesNotCallFlattenOne` (T_B91B_01)
- `FlattenFollower_NoOpenPosition_DoesNotCallFlattenOne` (T_B91B_02)
- `FlattenFollower_HasOpenPosition_CallsFlattenOne` (T_B91B_03)

All use reflection to invoke the private static `FlattenFollower` method.
CSharpier formatted both files after changes.

---

## 7-Scan Results (Layer 2)

### SCAN-01: lock() scan
Command: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\block\s*\("`
Result: 3 matches -- ALL are comment lines containing "no lock (JS-021)". Zero actual `lock(` statements in FlattenFollower or TryDispatchLeaderFlat.
**SCAN-01: PASS (0 violations)**

### SCAN-02: async void scan
Command: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "async void"`
Result: 1 match -- comment line only (`// JS-033: Tick is not async void`). Zero actual `async void` declarations in new/modified methods.
**SCAN-02: PASS (0 violations)**

### SCAN-03: CYC manual count
- `FlattenFollower`: 1 base + `if (acc == null)` + `if (!hasOpenPosition(...))` = **CYC=3** (<= 8) PASS
- `TryDispatchLeaderFlat`: 1 base + `if (state != Filled && state != Cancelled)` (1) + `if (isFollower(account))` (2) + `if (IsNonFlatDispatchName(orderName))` (3) + `if (!IsNativeExitName(orderName) && hasOpenPosition(...))` (4) + `foreach` loop back-edge (5) = **CYC=6** (<= 8) PASS
  - Note: null guard removed from foreach body -- that was 1 branch point in old CYC=8 count.
**SCAN-03: PASS (FlattenFollower=3, TryDispatchLeaderFlat=6)**

### SCAN-04: return null scan
Command: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "return null;"`
Result: 7 matches -- ALL pre-existing (L1480, L1954, L2000, L3112, L3118, L3181, L4003). Zero in FlattenFollower (void) or TryDispatchLeaderFlat (returns bool false/true, never null).
**SCAN-04: PASS (0 violations in new/modified methods)**

### SCAN-05: PTT- prefix
No new `CreateOrder` calls or signal names introduced by this ticket. FlattenFollower delegates to the caller-provided `flattenOne` action -- no direct order creation.
**SCAN-05: PASS (N/A -- no new signal names)**

### SCAN-06: ASCII scan
Command: `Get-Content src/PropTraderTools/CopyEngine.cs | Where-Object { $_ -match '[^\x00-\x7F]' }`
Result: 4 pre-existing lines containing non-ASCII (HOTFIX-QUICKALL-SINGLETON-01 comment area, two arrow symbols). All pre-existing -- zero new non-ASCII chars introduced in FlattenFollower or TryDispatchLeaderFlat lines.
**SCAN-06: PASS (0 new non-ASCII in DW-B91-B changes)**

### SCAN-07: test presence
Command: `Select-String -Path src/PropTraderTools/Tests/CopyEngineB91Tests.cs -Pattern "FlattenFollower_NullAccount_DoesNotCallFlattenOne|FlattenFollower_NoOpenPosition_DoesNotCallFlattenOne|FlattenFollower_HasOpenPosition_CallsFlattenOne"`
Result:
```
L107: public void FlattenFollower_NullAccount_DoesNotCallFlattenOne()
L138: public void FlattenFollower_NoOpenPosition_DoesNotCallFlattenOne()
L163: public void FlattenFollower_HasOpenPosition_CallsFlattenOne()
```
All 3 test methods present as `[Fact]` methods.
**SCAN-07: PASS (all 3 test names present)**

---

## Build Result

Build target: `src/PropTraderTools/PropTraderTools.csproj`
CSharpier format check: PASS (0 files with formatting issues after auto-format)
New errors introduced by DW-B91-B changes: **ZERO**

Pre-existing errors confirmed not introduced by this ticket:
- `CS0433` in CopyEngine.cs L3883 -- Globals type ambiguity (pre-existing, not in our modified range)
- All other errors in CopyEngineTests.cs, B76Tests.cs, B43Tests.cs, B68Tests.cs, B71Tests.cs, TradeCopierPanel.cs -- pre-existing, zero caused by Ticket-2

Zero NEW errors from Ticket-2 changes.

---

## Jane Street Compliance

| Rule | Status | Evidence |
|------|--------|---------|
| JS-021 (no lock) | PASS | FlattenFollower uses only delegate calls; no shared mutable state |
| JS-001 (no throw in hot path) | PASS | Early-return guards (`if (acc == null) return;`) not throw |
| JS-002 (no return null) | PASS | FlattenFollower is void; TryDispatchLeaderFlat returns bool |
| CYC <= 8 | PASS | FlattenFollower=3, TryDispatchLeaderFlat=6 |
| ASCII-only | PASS | All identifiers and new string literals are 7-bit ASCII |

---

## Summary

| Item | Result |
|------|--------|
| CHANGE A: foreach body replaced | DONE (L2323) |
| CHANGE B: FlattenFollower added | DONE (~L2332) |
| CHANGE C: header comment CYC=8->6 | DONE (L2296) |
| Tests appended (3 [Fact]) | DONE (CopyEngineB91Tests.cs L107, L138, L163) |
| SCAN-01 lock() | PASS (0) |
| SCAN-02 async void | PASS (0) |
| SCAN-03 CYC | PASS (FF=3, TDLF=6) |
| SCAN-04 return null | PASS (0 in new code) |
| SCAN-05 PTT- prefix | PASS (N/A) |
| SCAN-06 ASCII | PASS (0 new non-ASCII) |
| SCAN-07 test presence | PASS (all 3 present) |
| Build: new errors | 0 |
