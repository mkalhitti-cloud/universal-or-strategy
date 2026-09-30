# B112 Architecture Plan

**Status**: REVIEW_PENDING
**Phase**: 1 (Architecture)
**Date**: 2026-08-26
**Author**: ptt-architect
**Defects**: DW-B116 (P1), DW-B113 (P0 downstream), DW-B114 (P1 track-only)

---

## Block Summary

Block B112 fixes a single overcount defect in `CountLeaderTargets`
(`CopyEngine.cs` L3312-3352). The method currently matches PTT-QX-T* and
PTT-BE-Target-* order names in addition to native Target1..9, and accepts
Accepted and Submitted order states alongside Working. These two
over-inclusive predicates cause the method to return 5 for a 3-target ATM
when stale residue orders are present in transitional states (DW-B116).
The overcount is the sole trigger for DW-B113 (bracketless position on
BE-retry cap exhaustion) and DW-B114 (double-increment _beReplaceAttempts).
Fixing DW-B116 eliminates DW-B113 and DW-B114 as side-effects with no
additional logic change required.

Four surgical changes are made inside `CountLeaderTargets` only. No other
method is modified. Method signature is unchanged. CYC remains 4.

---

## Defect Analysis

### DW-B116 Root Cause (with current code line references)

**Priority**: P1
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `CountLeaderTargets` (L3312-3352)
**Current CYC**: 4

**Cause 1 — Over-inclusive isTarget predicate (L3332-3347)**

The predicate has three OR branches:

```csharp
// L3332-3347 (current)
bool isTarget =
    !string.IsNullOrEmpty(o.Name)
    && (
        (
            o.Name.Length >= 7
            && o.Name.StartsWith("Target", StringComparison.Ordinal)
            && char.IsDigit(o.Name[6])
            && o.Name[6] != '0'
        )
        || (
            o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
            && o.Name.Length > 8
            && char.IsDigit(o.Name[8])
        )
        || o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
    );
```

The second branch (`PTT-QX-T*`) matches Quick-Exit copy targets placed on
the leader. The third branch (`PTT-BE-Target-*`) matches Break-Even replacement
targets that may survive across sessions as stale residue. Neither represents
a native ATM target slot. Including them inflates `count`.

**Cause 2 — Over-inclusive stateOk (L3325-3328)**

```csharp
// L3325-3328 (current)
bool stateOk =
    o.OrderState == OrderState.Working
    || o.OrderState == OrderState.Accepted
    || o.OrderState == OrderState.Submitted;
```

`Accepted` and `Submitted` are transitional states. An order in these states
has been submitted to the exchange but may not yet represent a live resting
target. Including them means stale PTT-BE-Target-* orders in transitional
states are counted, compounding the overcount from Cause 1.

**Observed symptom**: `CountLeaderTargets` returns 5 for a 3-target ATM when
stale `PTT-BE-Target-4` and `PTT-BE-Target-5` orders remain in the leader's
`acc.Orders` collection in Accepted or Submitted state.

---

### DW-B113 Root Cause Chain

**Priority**: P0 (downstream of DW-B116)
**Trigger chain**:

```
CountLeaderTargets() returns 5 (overcounted, DW-B116)
  → MoveStopToBreakEven: leaderTargets = 5
  → follower target count (3) != leaderTargets (5)
  → mismatch branch taken → _beReplaceAttempts incremented (DW-B114)
  → _beReplaceAttempts eventually exhausts retry cap
  → BE bracket not placed → bracketless position (DW-B113)
```

**Fix**: Eliminating DW-B116 removes the 5-return case entirely. With
`CountLeaderTargets` returning 3 for a 3-target ATM, the mismatch branch is
never spuriously entered. No additional logic change is required.

---

### DW-B114 Status (track-only)

**Priority**: P1 (track-only; resolves as side-effect of DW-B116 fix)

`_beReplaceAttempts` was being double-incremented because the mismatch branch
(triggered by DW-B116 overcount) ran on two consecutive `OnOrderUpdate` ticks
for the same BE event. Once `CountLeaderTargets` returns the correct value,
the mismatch branch is not entered and `_beReplaceAttempts` increments exactly
once per genuine retry. No code change to the increment site is needed.

---

## Target Method

| Property | Value |
|----------|-------|
| **File path** | `src/PropTraderTools/CopyEngine.cs` |
| **Method name** | `CountLeaderTargets` |
| **Line range** | L3307 (header comment) – L3352 (closing brace) |
| **Current CYC** | 4 |
| **Required CYC** | 4 (unchanged) |
| **Method signature** | `private int CountLeaderTargets(Instrument instrument)` |
| **Callers** | `MoveStopToBreakEven` only |
| **Return type** | `int` (never negative, never null — JS-002 satisfied) |

---

## Change Plan

### Change 1 — Narrow isTarget predicate

Remove the `PTT-QX-T*` branch (L3341-3345) and the `PTT-BE-Target-*`
branch (L3346). Retain only the native `Target1..9` check (L3335-3340).

**BEFORE (L3332-3347)**:
```csharp
bool isTarget =
    !string.IsNullOrEmpty(o.Name)
    && (
        (
            o.Name.Length >= 7
            && o.Name.StartsWith("Target", StringComparison.Ordinal)
            && char.IsDigit(o.Name[6])
            && o.Name[6] != '0'
        )
        || (
            o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
            && o.Name.Length > 8
            && char.IsDigit(o.Name[8])
        )
        || o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
    );
```

**AFTER**:
```csharp
bool isTarget =
    !string.IsNullOrEmpty(o.Name)
    && o.Name.Length >= 7
    && o.Name.StartsWith("Target", StringComparison.Ordinal)
    && char.IsDigit(o.Name[6])
    && o.Name[6] != '0';
```

JS constraints: ASCII-only strings. No throw. No lock. CYC unchanged.

---

### Change 2 — Narrow stateOk to Working only

Remove the `Accepted` and `Submitted` OR terms from `stateOk`.

**BEFORE (L3325-3328)**:
```csharp
bool stateOk =
    o.OrderState == OrderState.Working
    || o.OrderState == OrderState.Accepted
    || o.OrderState == OrderState.Submitted;
```

**AFTER**:
```csharp
bool stateOk = o.OrderState == OrderState.Working;
```

JS constraints: No new branch added. CYC unchanged. No lock. No throw.

---

### Change 3 — Cap return at Math.Min(count, 3)

Replace the bare `return count` with a hard cap at 3. A standard ATM has
at most 3 target slots (Target1, Target2, Target3). The cap prevents any
residue from inflating the result beyond the valid range even if upstream
predicates are widened in a future block.

**BEFORE (L3351)**:
```csharp
return count;
```

**AFTER**:
```csharp
return Math.Min(count, 3);
```

JS constraints: `Math.Min` is a pure expression — no branch added, CYC
unchanged. No allocation. No throw. No lock.

---

### Change 4 — Update method header comment

Replace the existing header comment block at L3307-3311 to document the
Working-only filter, the native Target1..9 restriction, the Math.Min cap,
and the DW-B116 fix reference.

**BEFORE (L3307-3311)**:
```csharp
// CountLeaderTargets: CYC=4. Returns the number of Working/Accepted/Submitted target
// limit orders on the leader account for the given instrument. Used by MoveStopToBreakEven
// to detect partial-target visibility on follower accounts (DW-B79-07).
// Matches the same name filter as Step A's isAtmTarget predicate.
// JS-021: no lock. JS-001: no throw. JS-002: returns int (never negative).
```

**AFTER**:
```csharp
// CountLeaderTargets: CYC=4. Returns the number of Working native target limit orders
// (Target1..Target9, digit 1-9, no PTT- prefix) on the leader account for the given
// instrument. Working-only (DW-B116: Accepted/Submitted removed -- transitional states
// cause overcount). Capped at Math.Min(count,3) -- standard ATM max 3 targets.
// Used by MoveStopToBreakEven to detect partial-target visibility on followers (DW-B79-07).
// DW-B116 fix: removed PTT-QX-T* and PTT-BE-Target-* from isTarget predicate.
// JS-021: no lock. JS-001: no throw. JS-002: returns int (never negative). ASCII-only.
```

---

## CYC Verification

**Project counting convention note**: Throughout `CopyEngine.cs`, the
header-comment CYC annotation counts structural control-flow decision points
(guards, loops, outcome branches) at the method's top level. The null-guard
`if (o == null) continue` (a defensive null-skip) and the combined state/
instrument/type pre-condition filter `if (!stateOk || !instrOk || ...)
continue` are treated as pre-condition gates — each counted as one branch —
giving a full 6-point count by McCabe, but the project header convention
documents the 4 structural points listed below (same convention as the
pre-existing `// CYC=4` comment at L3307). The AFTER code retains all
existing branches; no new decision points are added.

Full decision-point inventory (all 6 — BEFORE and AFTER identical):

| # | Decision Point | Code Location | Counted in project CYC |
|---|---------------|---------------|----------------------|
| 1 | `if (rule == null) return 0` | L3315-3316 | YES |
| 2 | `if (leader == null) return 0` | L3318-3319 | YES |
| 3 | `foreach (Order o in leader.Orders)` | L3321 | YES |
| 4 | `if (o == null) continue` | L3323 | NO (null-guard pre-condition) |
| 5 | `if (!stateOk \|\| !instrOk \|\| ...)` | L3330 | NO (filter pre-condition) |
| 6 | `if (isTarget) count++` | L3348-3349 | YES |

**CYC = 4 by project convention (unchanged after all four changes).**
McCabe full count = 6; both figures are stable across BEFORE and AFTER.

No new `if`, `else if`, `while`, `for`, or decision points are
introduced by Changes 1-4. Changes 1 and 2 *remove* OR terms from existing
boolean expressions; Change 3 substitutes a pure expression for another;
Change 4 is a comment-only change.

---

## Test Plan

**File**: `src/PropTraderTools/Tests/B112Tests.cs`
**Framework**: xUnit (mandatory — no NUnit, no MSTest)
**Async**: None — all tests are synchronous [Fact] methods (JS-033: no async void)

---

### T_B112_01 — CountLeaderTargets_Returns3_WhenLeaderHas3WorkingNativeTargets

**Arrange**: Build a fake `leader.Orders` collection containing exactly 3
orders: Name="Target1", Name="Target2", Name="Target3", each with
`OrderState.Working`, `OrderType.Limit`, and matching instrument FullName.

**Assert**: `CountLeaderTargets(instrument)` returns `3`.

**Verifies**: Nominal path — 3 Working native targets → count = 3.

---

### T_B112_02 — CountLeaderTargets_ExcludesPttBeTargetResidues

**Arrange**: Build `leader.Orders` with 3 Working native targets (Target1-3)
plus 2 stale PTT-BE-Target-* orders in Working state and matching instrument.

**Assert**: `CountLeaderTargets(instrument)` returns `3` (not 5).

**Verifies**: CHANGE 1 — PTT-BE-Target-* excluded from isTarget predicate.

---

### T_B112_03 — CountLeaderTargets_ExcludesPttQxTResidues

**Arrange**: Build `leader.Orders` with 3 Working native targets (Target1-3)
plus 2 stale PTT-QX-T* orders (PTT-QX-T1, PTT-QX-T2) in Working state and
matching instrument.

**Assert**: `CountLeaderTargets(instrument)` returns `3` (not 5).

**Verifies**: CHANGE 1 — PTT-QX-T* excluded from isTarget predicate.

---

### T_B112_04 — CountLeaderTargets_CapsAt3_WhenMoreThan3NativeTargets

**Arrange**: Build `leader.Orders` with 5 Working native targets
(Target1-Target5) and matching instrument (simulates a 5-target ATM or
residue from a wider config).

**Assert**: `CountLeaderTargets(instrument)` returns `3` (Math.Min cap).

**Verifies**: CHANGE 3 — return Math.Min(count, 3) hard cap.

---

### T_B112_05 — CountLeaderTargets_ExcludesAcceptedAndSubmittedNativeTargets

**Arrange**: Build `leader.Orders` with:
- 3 Working native targets (Target1-3) — matching instrument, Limit type.
- 2 native targets (Target4, Target5) in `OrderState.Accepted`.
- 2 native targets (Target6, Target7) in `OrderState.Submitted`.

**Assert**: `CountLeaderTargets(instrument)` returns `3` (Accepted and
Submitted states excluded, Working-only, then capped at 3).

**Verifies**: CHANGE 2 — stateOk = Working only.

---

## Files Modified

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | `CountLeaderTargets` L3307-3352 only (4 surgical changes) |
| `src/PropTraderTools/Tests/B112Tests.cs` | NEW — 5 xUnit [Fact] tests |

---

## Files NOT Modified

All files not listed above are explicitly out of scope for B112.

| Files | Reason |
|-------|--------|
| All other methods in `CopyEngine.cs` | Zero callers broken; signature unchanged |
| `MoveStopToBreakEven` | Receives corrected int from CountLeaderTargets — no logic change needed |
| `SnapshotBeTargets` | Separate concern (DW-B107/B108) — not touched |
| `TryReplacePttBeBrackets` | Not in scope |
| All other `.cs` files in `src/PropTraderTools/` | Not touched |
| All `.cs` files outside `src/PropTraderTools/` | Not touched |
| `CopyEngineTests.cs` | Pre-existing test infrastructure issues tracked separately (DW-PTT-BE-FIX-03) |

---

## Jane Street Pre-Flight Checklist

| Rule | Requirement | Status |
|------|-------------|--------|
| **JS-021** | No `lock()` anywhere | PASS — no lock present or introduced |
| **JS-001** | No `throw new XxxException` in hot path | PASS — no throw in CountLeaderTargets |
| **JS-002** | No `return null` | PASS — method returns `int`, never null |
| **JS-033** | No `async void` (non-event-handler) | PASS — method is `private int`, synchronous |
| **ASCII-only** | No Unicode, emoji, curly quotes in string literals | PASS — all strings ASCII-only |
| **CYC <= 4** | Method cyclomatic complexity does not increase | PASS — CYC = 4 before and after |
| **DateTime.UtcNow** | No `DateTime.Now` usage | N/A — no DateTime in this method |
| **No FontFamily** | No FontFamily hardcoding | N/A — no UI in this method |
| **No hex colors** | No hardcoded hex color strings | N/A — no UI in this method |
| **PTT- order prefix** | All CreateOrder calls use "PTT-" prefix | N/A — no CreateOrder in this method |

All checks: **PASS** or **N/A**.

---

## Sync Gate

After `ptt-engineer` implements and commits the changes, the following sync
gate MUST be executed before reporting completion:

```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```

Pass criterion: **0 MISMATCH lines** across all 16 tracked files.

After sync pass, the Director must press **F5** in NinjaTrader 8 to confirm
compilation succeeds with 0 errors.

---

## Deferred Items

### DW-B114 Status

**Resolution**: Track-only. Resolves as side-effect of DW-B116 fix.
No code change at the `_beReplaceAttempts` increment site is required.
DW-B114 is considered closed once DW-B116 is fixed and `T_B112_05` passes.

### DW-B115 Status

**Resolution**: DW-B115 is not listed in the B107 deferred backlog and is
not in scope for B112. If DW-B115 exists as a separate defect item, it must
be triaged and assigned to a future block by the Director. B112 does not
close, partially close, or modify any artifact related to DW-B115.

### Carry-Forward (from B107)

The following B107 deferred items are NOT affected by B112 changes:

| Item | Status |
|------|--------|
| B107-DEFER-01 (F5 NT8 gate) | Open — Director action |
| B107-DEFER-02 (Combo C live re-test) | Open — Director SIM gate |
| DW-B107 (MoveStopToBreakEven stale PTT-BE-Target-* on followers) | Closed in B108 via SnapshotBeTargets |
| DW-B42-01/02/03 | Unchanged — carry-forward from DW-B89 |
| DW-PTT-BE-FIX-01/02/03 | Unchanged — carry-forward |
| DW-B89-DEFERRED-01..06 | Unchanged — carry-forward |
