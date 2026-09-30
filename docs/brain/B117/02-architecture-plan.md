# B117 Architecture Plan

**Block**: B117
**Defect closed**: DW-B125 (P0) — ResolveFollowerTargets branch (1) returns partial follower snapshot unchanged
**Phase**: REVIEW_PASS pending
**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**Method**: `ResolveFollowerTargets` (line 364)
**Date**: 2026-08-28

---

## 1. Executive Summary

B117 is a **single-branch, single-method, single-file** fix.

Branch (1) of `ResolveFollowerTargets` currently returns `followerSnapshot` whenever
`followerSnapshot.Count > 0`. This fires correctly when the snapshot is **complete** (count
equals `leaderTargets.Count`), but also fires — incorrectly — when the snapshot is **partial**
(0 < count < leaderCount). A partial snapshot means some `PTT-BE-Target-*` orders are still
in-flight when `SnapshotTargetOrders` runs; returning it causes `PttQuickExit` to use wrong
quantities and miss targets (observed: T3 missed, 4 contracts residual on Sim104).

The fix tightens branch (1) to reject partial snapshots by adding a count-equality guard.
Partial snapshots fall through to `ScaleLeaderTargets`, which produces the correct split from
the leader baseline. No other branch, method, or file is touched.

---

## 2. Root Cause Analysis

### Defect: DW-B125 (P0)

**Trigger**: Combo C session (BE-ALL then QX-ALL), Sim104 with 3 leader targets
(T1=4, T2=2, T3=1, leaderCount=3).

**Race condition**: `SnapshotTargetOrders` on Sim104 captures only 2 of 3
`PTT-BE-Target-*` orders before the third reaches `Working` state.
Result: `followerSnapshot = [(p,2),(p,1)]`, count=2.

**B116 branch (1) guard fires**: `followerSnapshot.Count > 0` → `2 > 0` → **true** →
returns the 2-entry partial snapshot unchanged.

**Downstream effect**: `PttQuickExit` submits exits for T1 and T2 only. T3 is never
submitted. Residual: 4 contracts open (Sim104 net position not flat).

**Prior context**: `PARTIAL-SNAPSHOT-VARIANT` in `docs/brain/B116/06-deferred-backlog.md`
identified this as a P1 deferred item. Live evidence from Sim104 now escalates it to P0.

---

## 3. Scope Boundary

| Dimension | In Scope | Out of Scope |
|-----------|----------|-------------|
| File | `PttGlobalQuickExit.cs` only | All other files |
| Method | `ResolveFollowerTargets` only | `Execute`, `ScaleLeaderTargets`, `CalcTNQty`, all others |
| Branch | Branch (1) guard condition only | Branch (2), branch (3) delegate |
| Tests | `src/PropTraderTools/Tests/B117Tests.cs` (T1, T2) | All other test files |
| CYC impact | `ResolveFollowerTargets` 3 -> 4 | All other methods unchanged |

---

## 4. Before / After Code

### BEFORE (B116, line 370)

```csharp
if (followerSnapshot.Count > 0) return followerSnapshot;  // (1)
```

### AFTER (B117, lines 370-374)

```csharp
// DW-B125: reject partial snapshots -- only trust follower snapshot
// when it has the same count as the leader snapshot.
// Partial count (0 < count < leaderCount) means some PTT-BE-Target-*
// orders are still in-flight; treat as empty and scale from leader.
if (followerSnapshot.Count > 0
    && (leaderTargets.Count == 0
        || followerSnapshot.Count == leaderTargets.Count))
    return followerSnapshot;  // (1) full match or no leader baseline
```

Full method after B117 (lines 364-373):

```csharp
/// <summary>
/// ResolveFollowerTargets: returns follower snapshot when complete; otherwise scales leader targets.
/// Partial snapshot (0 < count &lt; leaderCount) falls through to ScaleLeaderTargets (DW-B125 fix).
/// Preserves DW-B120 CalcTNQty fallback path when both snapshot and leader are empty.
/// CYC=4: partial-snapshot guard(1a+1b), empty-leader/zero-qty guard(2), delegate(3).
/// JS-002: never returns null. JS-021: no lock. JS-001: no throw. ASCII-only.
/// </summary>
internal static System.Collections.Generic.List<(double Price, int Qty)> ResolveFollowerTargets(
    System.Collections.Generic.List<(double Price, int Qty)> followerSnapshot,
    System.Collections.Generic.List<(double Price, int Qty)> leaderTargets,
    int followerPosQty,
    int leaderPosQty)
{
    // DW-B125: reject partial snapshots -- only trust follower snapshot
    // when it has the same count as the leader snapshot.
    // Partial count (0 < count < leaderCount) means some PTT-BE-Target-*
    // orders are still in-flight; treat as empty and scale from leader.
    if (followerSnapshot.Count > 0
        && (leaderTargets.Count == 0
            || followerSnapshot.Count == leaderTargets.Count))
        return followerSnapshot;  // (1) full match or no leader baseline
    if (leaderTargets.Count == 0 || followerPosQty <= 0) return followerSnapshot;
    return ScaleLeaderTargets(leaderTargets, followerPosQty, leaderPosQty);
}
```

---

## 5. Logic Table — All 4 Cases

| Case | `followerSnapshot.Count` | `leaderTargets.Count` | Branch (1) fires? | Outcome |
|------|--------------------------|-----------------------|-------------------|---------|
| **Empty snapshot** | 0 | any | No (0 > 0 = false) | Falls through to branch (2)/(3): `ScaleLeaderTargets` or `CalcTNQty` — **unchanged (DW-B124)** |
| **Partial snapshot** (B117 fix) | 0 < count < leaderCount | > 0 | No (count > 0 AND count != leaderCount) | **Falls through** to `ScaleLeaderTargets` — NEW BEHAVIOR |
| **Full match** | count == leaderCount | > 0 | Yes | Returns `followerSnapshot` — **unchanged** |
| **No leader baseline** | > 0 | 0 | Yes (count > 0 AND leaderCount == 0) | Returns `followerSnapshot` — **unchanged safe fallback** |

The compound condition `followerSnapshot.Count > 0 && (leaderTargets.Count == 0 || followerSnapshot.Count == leaderTargets.Count)` resolves to:

- count=0: outer AND short-circuits to false → fall through (correct)
- count=2, leaderCount=3: `2 > 0` AND `(3==0 OR 2==3)` = true AND (false OR false) = **false** → fall through (fix)
- count=3, leaderCount=3: `3 > 0` AND `(3==0 OR 3==3)` = true AND (false OR true) = **true** → return snapshot (correct)
- count=2, leaderCount=0: `2 > 0` AND `(0==0 OR 2==0)` = true AND (true OR false) = **true** → return snapshot (correct)

---

## 6. CYC Table

| Method | CYC Before | CYC After | Limit | Status |
|--------|-----------|-----------|-------|--------|
| `ResolveFollowerTargets` | 3 | 4 | 8 | PASS |
| `Execute` | 8 | 8 | 8 | PASS (unchanged) |
| `ScaleLeaderTargets` | — | — | — | Unchanged, not touched |
| `CalcTNQty` | — | — | — | Unchanged, not touched |

CYC breakdown for `ResolveFollowerTargets` after B117:
- Decision 1a: `followerSnapshot.Count > 0` (outer AND left operand)
- Decision 1b: `leaderTargets.Count == 0 || followerSnapshot.Count == leaderTargets.Count` (inner OR is 1 decision)
- Decision 2: `leaderTargets.Count == 0 || followerPosQty <= 0` (branch (2))
- Base path: 1
- **CYC = 1 + 3 = 4**

---

## 7. Test Definitions

### T1: `ResolveFollowerTargets_PartialSnapshot_count2of3_ReturnsScaled`

**Scenario**: Sim104 partial snapshot — 2 of 3 targets snapshotted before third order working.

**Input**:
```
followerSnapshot = [(price, 2), (price, 1)]   // count=2
leaderTargets    = [(price, 4), (price, 2), (price, 1)]  // count=3
followerPosQty   = 7
leaderPosQty     = 7
```

**Assert**:
- `result.Count == 3` — ScaleLeaderTargets produced full 3-target list
- `result[0].Qty == 4` — first target quantity matches leader T1 scaled (7/7 = 1.0 scale)

**Why**: Verifies DW-B125 fix. Branch (1) must NOT fire (partial count 2 != leaderCount 3).
`ScaleLeaderTargets` must produce the correct split.

---

### T2: `ResolveFollowerTargets_PartialSnapshot_count1of3_ReturnsScaled`

**Scenario**: More extreme partial — only 1 of 3 targets snapshotted.

**Input**:
```
followerSnapshot = [(price, 4)]              // count=1
leaderTargets    = [(price, 4), (price, 2), (price, 1)]  // count=3
followerPosQty   = 7
leaderPosQty     = 7
```

**Assert**:
- `result.Count == 3` — ScaleLeaderTargets produced full 3-target list
- `result[0].Qty == 4` — first target quantity correct

**Why**: Extends DW-B125 coverage to count=1. Branch (1) must NOT fire (1 != 3).
Ensures the fix handles any partial count, not just count=2.

---

## 8. Regression Guard

The following B116 test cases MUST still pass after B117. They are not modified.

### B116-T2 (must pass): Full snapshot match returns self

**Input**: `followerSnapshot.Count == leaderTargets.Count` (e.g., count=3, leaderCount=3)
**Assert**: `result` is the same object reference as `followerSnapshot`
**Why passes**: `3 > 0 AND (3==0 OR 3==3)` = true → branch (1) fires → returns snapshot unchanged.

### B116-T3 (must pass): Empty snapshot falls through to ScaleLeaderTargets

**Input**: `followerSnapshot.Count == 0`, `leaderTargets.Count > 0`, `followerPosQty > 0`
**Assert**: `result` is scaled from `leaderTargets`
**Why passes**: `0 > 0` = false → branch (1) skipped → branch (2) check passes → `ScaleLeaderTargets` fires.

No changes to the B116 code paths for these cases. The B117 change is purely additive (tightens branch (1) guard without narrowing the two passing cases).

---

## 9. P0 Jane Street Compliance

| Rule | Requirement | Status |
|------|-------------|--------|
| JS-001 | No `throw new XxxException` in hot path | PASS — no throws added |
| JS-002 | No `return null` | PASS — returns list (never null) |
| JS-021 | No `lock()` | PASS — no lock anywhere in method |
| JS-033 | No `async void` | PASS — method is `static` synchronous |
| JS-066 | ASCII-only identifiers and strings | PASS — comment text is ASCII-only |
| JS-080 | CYC <= 8 | PASS — CYC=4 after change |

No new P0 violations introduced. Existing P0 compliance of `ResolveFollowerTargets` maintained.

---

## 10. Engineer Contract Summary

**File to edit**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**Line to change**: 370 (single line replacement, +4 lines net after comment block)

**Exact edit**:
- DELETE: `if (followerSnapshot.Count > 0) return followerSnapshot;  // (1)`
- INSERT: the 7-line AFTER block shown in Section 4 above (comment + compound condition)

**File to create**: `src/PropTraderTools/Tests/B117Tests.cs`
- T1: `ResolveFollowerTargets_PartialSnapshot_count2of3_ReturnsScaled`
- T2: `ResolveFollowerTargets_PartialSnapshot_count1of3_ReturnsScaled`
- Framework: xUnit `[Fact]` only. No NUnit. No MSTest.

**Do NOT touch**:
- Branch (2): `if (leaderTargets.Count == 0 || followerPosQty <= 0) return followerSnapshot;`
- Branch (3): `return ScaleLeaderTargets(leaderTargets, followerPosQty, leaderPosQty);`
- `Execute` method
- `ScaleLeaderTargets` method
- Any other file in `src/PropTraderTools/`

**Post-implementation gate**:
- `dotnet test` → all tests pass (B117 T1+T2 + B116 regression T2+T3)
- `powershell -File scripts\ptt-sync-and-verify.ps1` → 0 MISMATCH
- F5 in NinjaTrader 8 → Compilation succeeded, 0 errors

---

## Status

**PLAN_COMPLETE**
