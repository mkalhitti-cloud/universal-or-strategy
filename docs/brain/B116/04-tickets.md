# B116 Implementation Tickets — DW-B124 Fix (Option B)

**Pipeline**: B116 Ph3 (ptt-architect)
**Date**: 2026-08-28
**Defect**: DW-B124 (P0) — CalcTNQty fallback wrong split when BE-ALL consumes native ATM brackets before QX-ALL
**Plan**: `docs/brain/B116/02-architecture-plan.md` (REVIEW_PASS)
**Review**: `docs/brain/B116/02-plan-review.md` (REVIEW_PASS — 15/15 items PASS)

---

## TICKET 1 — B116-T1

### Title
Add ScaleLeaderTargets + ResolveFollowerTargets + substitution call

### File
`src/PropTraderTools/Features/PttGlobalQuickExit.cs`

### Spec Requirements
- DW-B124 (P0) — Option B from `docs/brain/B116/02-architecture-plan.md`
- Architecture plan Sec 3 Changes 1-4
- Architecture plan Sec 5 Updated CYC Table

### Changes — Surgical (exactly these four sub-changes, nothing else)

#### Change 1a — Promote _fPosQty

Move the block `int _fPosQty = 0;` and its `foreach (var fPos in ...)` loop that calculates `_fPosQty`
from **inside** the DIAG block to **above** the DIAG block as a named local variable.
The DIAG block may still reference `_fPosQty` unchanged. Do not duplicate the loop.
Do not remove or modify any other DIAG block content.

**Before** (conceptual — currently inside DIAG block):
```csharp
// DIAG block
{
    int _fPosQty = 0;
    foreach (var fPos in follower.Positions)
    {
        if (fPos.Instrument == pos.Instrument)
            _fPosQty += fPos.Quantity;
    }
    // ... rest of DIAG block referencing _fPosQty ...
}
```

**After** (promote above DIAG block):
```csharp
int _fPosQty = 0;
foreach (var fPos in follower.Positions)
{
    if (fPos.Instrument == pos.Instrument)
        _fPosQty += fPos.Quantity;
}
// DIAG block (unchanged, still references _fPosQty)
{
    // ... original DIAG content unchanged ...
}
```

---

#### Change 1b — Add ScaleLeaderTargets static helper

Add the following private static method **after** the `SnapshotTargetOrders` method in the file:

```csharp
private static List<(double Price, int Qty)> ScaleLeaderTargets(
    List<(double Price, int Qty)> leaderTargets,
    int followerPosQty,
    int leaderPosQty)
{
    var result = new List<(double Price, int Qty)>(leaderTargets.Count);
    if (leaderPosQty <= 0) return result;
    int allocated = 0;
    for (int i = 0; i < leaderTargets.Count; i++)
    {
        int qty;
        if (i == leaderTargets.Count - 1)
            qty = Math.Max(1, followerPosQty - allocated);
        else
            qty = Math.Max(1, (int)Math.Round(
                (double)leaderTargets[i].Qty * followerPosQty / leaderPosQty));
        allocated += qty;
        result.Add((leaderTargets[i].Price, qty));
    }
    return result;
}
```

CYC=3. ASCII-only. No lock. No throw. No return null. No async void.

---

#### Change 1c — Add ResolveFollowerTargets static helper

Add the following private static method **after** `ScaleLeaderTargets`:

```csharp
private static List<(double Price, int Qty)> ResolveFollowerTargets(
    List<(double Price, int Qty)> followerSnapshot,
    List<(double Price, int Qty)> leaderTargets,
    int followerPosQty,
    int leaderPosQty)
{
    if (followerSnapshot.Count > 0) return followerSnapshot;
    if (leaderTargets.Count == 0 || followerPosQty <= 0) return followerSnapshot;
    return ScaleLeaderTargets(leaderTargets, followerPosQty, leaderPosQty);
}
```

CYC=3. ASCII-only. No lock. No throw. No return null. No async void.

---

#### Change 1d — Insert substitution call in Execute

After the DIAG block and **before** the `ExecuteOne` call, insert:

```csharp
// DW-B124: when follower snapshot is empty (BE-ALL consumed native brackets),
// derive qty array from leader snapshot scaled by posQty ratio.
// Prevents CalcTNQty arithmetic fallback from wrong tranche split.
followerTargets = ResolveFollowerTargets(
    followerTargets, targets, _fPosQty, pos.Quantity);
```

`targets` = leader snapshot (already in scope at L47).
`_fPosQty` = follower position qty (promoted in Change 1a).
`pos.Quantity` = leader position qty (already in scope).

**No changes** to `PttQuickExit.cs`, `CopyEngine.cs`, `SnapshotTargetOrders`, `CalcTNQty`, or any other file.

---

### Acceptance Criteria

- [ ] `Execute` CYC = 8 (unchanged — two-helper extraction keeps inline branch count unchanged)
- [ ] `ScaleLeaderTargets` CYC = 3
- [ ] `ResolveFollowerTargets` CYC = 3
- [ ] `dotnet build src/PropTraderTools/PropTraderTools.csproj` passes: 0 errors, 0 warnings on new code
- [ ] No `lock()` in new code
- [ ] No `throw new XxxException` in new code
- [ ] No `return null` in new code
- [ ] No `async void` in new code
- [ ] All string literals in new code are ASCII-only
- [ ] `_fPosQty` is a named local **above** the DIAG block (not declared inside it)
- [ ] DIAG block content is otherwise unchanged
- [ ] `PttQuickExit.cs` is untouched
- [ ] `CopyEngine.cs` is untouched

---

### 7-Scan Checklist (engineer must run all 7 to zero before BUILD_PASS)

1. `grep -n "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs` → 0 results in new code
2. `grep -n "throw new" src/PropTraderTools/Features/PttGlobalQuickExit.cs` → 0 results in new code
3. `grep -n "return null" src/PropTraderTools/Features/PttGlobalQuickExit.cs` → 0 results in new code
4. `grep -n "async void" src/PropTraderTools/Features/PttGlobalQuickExit.cs` → 0 results in new code
5. CYC audit: `Execute` CYC=8, `ScaleLeaderTargets` CYC=3, `ResolveFollowerTargets` CYC=3
6. `dotnet build src/PropTraderTools/PropTraderTools.csproj` → 0 errors
7. `dotnet test src/PropTraderTools/Tests/` → all tests pass

---

## TICKET 2 — B116-T2

### Title
Add 6 xUnit tests for ScaleLeaderTargets and ResolveFollowerTargets

### File
`src/PropTraderTools/Tests/B116Tests.cs` (new file)

### Spec Requirements
- Architecture plan Sec 6 (Testing Requirements)
- JS-051 — xUnit only; NO NUnit, NO MSTest, NO Moq

### Framework
xUnit only. The file must reference `Xunit` namespace. No NUnit, no MSTest, no Moq references anywhere in the file.

### Tests — Exactly These 6 [Fact] Methods

#### T2-1: `ScaleLeaderTargets_EqualQty_IdenticalSplit`

**Purpose**: Verifies that when followerPosQty == leaderPosQty, the output is identical to the input.

**Inputs**:
- `leaderTargets` = `[(0.0, 4), (0.0, 2), (0.0, 1)]`
- `leaderPosQty` = `7`
- `followerPosQty` = `7`

**Asserts**:
- `result[0].Qty == 4`
- `result[1].Qty == 2`
- `result[2].Qty == 1`
- Sum of all `Qty` == 7

---

#### T2-2: `ScaleLeaderTargets_HalfQty_SumEqualsFollowerQty`

**Purpose**: Verifies that when followerPosQty < leaderPosQty, the output sum equals followerPosQty and each Qty >= 1.

**Inputs**:
- `leaderTargets` = `[(0.0, 4), (0.0, 2), (0.0, 1)]`
- `leaderPosQty` = `7`
- `followerPosQty` = `4`

**Asserts**:
- `result.Count == 3`
- Sum of all `Qty` == 4
- Each individual `Qty >= 1`

---

#### T2-3: `ScaleLeaderTargets_ZeroLeaderPosQty_ReturnsEmpty`

**Purpose**: Verifies the degenerate guard — leaderPosQty=0 returns empty list (no divide-by-zero).

**Inputs**:
- `leaderTargets` = `[(0.0, 4), (0.0, 2), (0.0, 1)]`
- `leaderPosQty` = `0`
- `followerPosQty` = `7`

**Asserts**:
- `result.Count == 0`

---

#### T2-4: `ResolveFollowerTargets_NonEmptySnapshot_ReturnsSelf`

**Purpose**: Verifies that a non-empty follower snapshot is returned unchanged (not overwritten by leader scaling).

**Inputs**:
- `followerSnapshot` = `[(0.0, 4), (0.0, 2), (0.0, 1)]`
- `leaderTargets` = `[(0.0, 3), (0.0, 2), (0.0, 2)]`
- `followerPosQty` = `7`
- `leaderPosQty` = `7`

**Asserts**:
- `result[0].Qty == 4` (snapshot returned unchanged — first element proves it)

---

#### T2-5: `ResolveFollowerTargets_EmptySnapshotFullLeader_ReturnsScaled`

**Purpose**: Verifies the DW-B124 fix path — empty snapshot with valid leader data returns scaled leader targets.

**Inputs**:
- `followerSnapshot` = `[]` (empty)
- `leaderTargets` = `[(0.0, 4), (0.0, 2), (0.0, 1)]`
- `leaderPosQty` = `7`
- `followerPosQty` = `7`

**Asserts**:
- `result.Count == 3`
- `result[0].Qty == 4`
- `result[1].Qty == 2`
- `result[2].Qty == 1`

---

#### T2-6: `ResolveFollowerTargets_EmptySnapshotEmptyLeader_ReturnsEmpty`

**Purpose**: Verifies the DW-B120 fallback path is preserved — empty snapshot + empty leader returns empty list (CalcTNQty fires).

**Inputs**:
- `followerSnapshot` = `[]` (empty)
- `leaderTargets` = `[]` (empty)
- `followerPosQty` = `7`
- `leaderPosQty` = `7`

**Asserts**:
- `result.Count == 0`

---

### Acceptance Criteria

- [ ] Exactly 6 `[Fact]` methods present in `B116Tests.cs`
- [ ] xUnit framework only (`using Xunit;` present; no NUnit, no MSTest, no Moq)
- [ ] All 6 tests pass: `dotnet test src/PropTraderTools/Tests/`
- [ ] No NUnit, MSTest, or Moq references anywhere in the file
- [ ] All string literals and identifiers in the file are ASCII-only
- [ ] File compiles cleanly: `dotnet build src/PropTraderTools/PropTraderTools.csproj` → 0 errors

---

### 7-Scan Checklist (engineer must run all 7 to zero before BUILD_PASS)

1. `grep -n "using NUnit" src/PropTraderTools/Tests/B116Tests.cs` → 0 results
2. `grep -n "using Microsoft.VisualStudio" src/PropTraderTools/Tests/B116Tests.cs` → 0 results
3. `grep -n "lock(" src/PropTraderTools/Tests/B116Tests.cs` → 0 results
4. Verify exactly 6 `[Fact]` methods in file: `grep -c "\[Fact\]" src/PropTraderTools/Tests/B116Tests.cs` → 6
5. `dotnet build src/PropTraderTools/PropTraderTools.csproj` → 0 errors
6. `dotnet test src/PropTraderTools/Tests/` → all 6 B116 tests PASS
7. ASCII-only strings in test file: `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Tests/B116Tests.cs` → 0 results

---

## Ticket Summary

| Ticket | File | Changes | CYC Impact |
|--------|------|---------|-----------|
| B116-T1 | `PttGlobalQuickExit.cs` | Promote `_fPosQty`; add `ScaleLeaderTargets` (CYC=3); add `ResolveFollowerTargets` (CYC=3); insert substitution call | `Execute` CYC=8 (unchanged) |
| B116-T2 | `Tests/B116Tests.cs` | New file — 6 xUnit [Fact] tests | N/A |

**Out of scope (do NOT touch):**
- `PttQuickExit.cs` — no changes to `CalcTNQty` or `Execute`
- `CopyEngine.cs` — DW-B122 fix deployed, do not revert
- DIAG logging blocks — leave in place, Director removes when gate passes
- `SnapshotTargetOrders` — DW-B123 dedup deployed, do not change

---

*Ph3 ticket generation complete. TICKETS_COMPLETE.*
