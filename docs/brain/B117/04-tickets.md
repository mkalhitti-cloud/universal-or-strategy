# B117 Tickets

**Block**: B117
**Phase**: 3 (Ticket Generation)
**Plan source**: `docs/brain/B117/02-architecture-plan.md` (REVIEW_PASS)
**Review source**: `docs/brain/B117/02-plan-review.md` (REVIEW_PASS, zero violations)
**Date**: 2026-08-28
**Spec requirement closed**: DW-B125 (P0) — ResolveFollowerTargets branch (1) returns partial follower snapshot unchanged

---

## T1 — PttGlobalQuickExit.cs: ResolveFollowerTargets branch (1) fix

### Spec Requirements Satisfied
- **DW-B125 (P0)**: partial follower snapshot returned unchanged, causing wrong exit quantities and missed targets

### File
`src/PropTraderTools/Features/PttGlobalQuickExit.cs`

### Method
`ResolveFollowerTargets` (internal static, line ~370)

### Exact Change

**BEFORE** (single line, ~L370):
```csharp
if (followerSnapshot.Count > 0) return followerSnapshot;  // (1)
```

**AFTER** (replace that single line with the following 7 lines):
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

### XML Doc Comment Update

Locate the `<summary>` block immediately above the `ResolveFollowerTargets` method signature.

**BEFORE** (existing CYC=3 line):
```
/// CYC=3: non-empty snapshot guard(1), empty-leader/zero-qty guard(2), delegate(3).
```

**AFTER** (update to CYC=4):
```
/// CYC=4: partial-reject guard(1a), count-match guard(1b), empty-leader/zero-qty guard(2), delegate(3).
```

### Do NOT Touch
- Branch (2): `if (leaderTargets.Count == 0 || followerPosQty <= 0) return followerSnapshot;`
- Branch (3): `return ScaleLeaderTargets(leaderTargets, followerPosQty, leaderPosQty);`
- `Execute` method
- `ScaleLeaderTargets` method
- `CalcTNQty` method
- Any other file in `src/PropTraderTools/`

### JS Rules Applied

| Rule | Constraint | Applies To |
|------|-----------|------------|
| JS-001 | No `throw new XxxException` in hot path | All code in this method |
| JS-002 | No `return null` | Method must return `List<(double, int)>`, never null |
| JS-021 | No `lock()` | Entire file — zero lock calls |
| JS-033 | No `async void` | Method is `internal static` synchronous |
| JS-066 | ASCII-only identifiers and strings | All new comment text must be ASCII-only |
| JS-080 | CYC <= 8 | `ResolveFollowerTargets` CYC = 4 after change (3 decisions + base) |

### CYC Verification
- Decision 1a: `followerSnapshot.Count > 0` (outer AND left operand)
- Decision 1b: `leaderTargets.Count == 0 || followerSnapshot.Count == leaderTargets.Count` (inner OR is 1 decision)
- Decision 2: `leaderTargets.Count == 0 || followerPosQty <= 0` (branch (2))
- Base path: 1
- **CYC = 1 + 3 = 4** (limit 8, PASS)

### 7-SCAN CHECKLIST (engineer must confirm all 7 before BUILD_PASS)

```
[ ] SCAN-01  grep "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs
             Expected: 0 matches

[ ] SCAN-02  grep "throw new" src/PropTraderTools/Features/PttGlobalQuickExit.cs
             Expected: 0 matches

[ ] SCAN-03  grep "return null" src/PropTraderTools/Features/PttGlobalQuickExit.cs
             Expected: 0 matches

[ ] SCAN-04  grep "async void" src/PropTraderTools/Features/PttGlobalQuickExit.cs
             Expected: 0 matches

[ ] SCAN-05  python scripts/complexity_audit.py
             Expected: ResolveFollowerTargets CYC == 4
                       Execute CYC == 8 (unchanged)

[ ] SCAN-06  dotnet build src/PropTraderTools/PropTraderTools.csproj
             Expected: 0 errors, 0 warnings (new)

[ ] SCAN-07  powershell -File scripts\ptt-sync-and-verify.ps1
             Expected: 0 MISMATCH lines
```

### BUILD_PASS — T1

After all 7 scans pass, write `docs/brain/B117/ticket-1-completion.md` containing:

- **Ticket ID**: B117-T1
- **File edited**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
- **Change summary**: ResolveFollowerTargets branch (1) — compound guard added (DW-B125)
- **7-scan results**: each scan name + result (0/PASS)
  - SCAN-01 lock(: 0 matches / PASS
  - SCAN-02 throw new: 0 matches / PASS
  - SCAN-03 return null: 0 matches / PASS
  - SCAN-04 async void: 0 matches / PASS
  - SCAN-05 complexity_audit.py: ResolveFollowerTargets CYC == 4 / PASS
  - SCAN-06 dotnet build: 0 errors / PASS
  - SCAN-07 ptt-sync-and-verify: 0 MISMATCH / PASS
- **dotnet build result**: 0 errors
- **dotnet test result**: B117 T1+T2 PASS, all B116 tests PASS
- **ptt-sync-and-verify**: 0 MISMATCH

---

## T2 — B117Tests.cs: 2 new xUnit [Fact] tests

### Spec Requirements Satisfied
- **DW-B125 test coverage**: T1 covers partial count=2 of 3; T2 covers partial count=1 of 3

### File
`src/PropTraderTools/Tests/B117Tests.cs` **(NEW FILE — create from scratch)**

### Framework
xUnit only. NEVER NUnit or MSTest. No `[TestFixture]`, no `[TestMethod]`, no `[Test]` attributes.

### Exact File Content

Create `src/PropTraderTools/Tests/B117Tests.cs` with exactly the following content:

```csharp
using Xunit;
using System.Collections.Generic;
using NinjaTrader.Custom.AddOns.PropTraderTools.Features;

namespace PropTraderTools.Tests
{
    public class B117Tests
    {
        // T1: partial snapshot count=2, leader count=3 -> ScaleLeaderTargets fires
        [Fact]
        public void ResolveFollowerTargets_PartialSnapshot_count2of3_ReturnsScaled()
        {
            var follower = new List<(double, int)> { (100.0, 2), (99.0, 1) };
            var leader   = new List<(double, int)> { (100.0, 4), (99.0, 2), (98.0, 1) };
            var result = PttGlobalQuickExit.ResolveFollowerTargets(follower, leader, 7, 7);
            Assert.Equal(3, result.Count);
            Assert.Equal(4, result[0].Item2);
        }

        // T2: partial snapshot count=1, leader count=3 -> ScaleLeaderTargets fires
        [Fact]
        public void ResolveFollowerTargets_PartialSnapshot_count1of3_ReturnsScaled()
        {
            var follower = new List<(double, int)> { (100.0, 4) };
            var leader   = new List<(double, int)> { (100.0, 4), (99.0, 2), (98.0, 1) };
            var result = PttGlobalQuickExit.ResolveFollowerTargets(follower, leader, 7, 7);
            Assert.Equal(3, result.Count);
            Assert.Equal(4, result[0].Item2);
        }
    }
}
```

### Test Logic — T1

| Input | Value |
|-------|-------|
| `followerSnapshot` | `[(100.0, 2), (99.0, 1)]` — count = 2 |
| `leaderTargets` | `[(100.0, 4), (99.0, 2), (98.0, 1)]` — count = 3 |
| `followerPosQty` | 7 |
| `leaderPosQty` | 7 |

**Expected**: `result.Count == 3` AND `result[0].Item2 == 4`

**Why**: `2 > 0 AND (3==0 OR 2==3)` = `true AND false` = **false** → branch (1) does NOT fire → `ScaleLeaderTargets` runs with scale factor 7/7=1.0 → produces 3-entry list mirroring leader quantities.

### Test Logic — T2

| Input | Value |
|-------|-------|
| `followerSnapshot` | `[(100.0, 4)]` — count = 1 |
| `leaderTargets` | `[(100.0, 4), (99.0, 2), (98.0, 1)]` — count = 3 |
| `followerPosQty` | 7 |
| `leaderPosQty` | 7 |

**Expected**: `result.Count == 3` AND `result[0].Item2 == 4`

**Why**: `1 > 0 AND (3==0 OR 1==3)` = `true AND false` = **false** → branch (1) does NOT fire → `ScaleLeaderTargets` produces correct 3-entry result.

### Regression Guard

**Do NOT touch `B116Tests.cs`**. The following B116 tests must still pass after T1 and T2 are added:

- **B116-T2**: `followerSnapshot.Count == leaderTargets.Count` → branch (1) fires → returns snapshot reference unchanged
- **B116-T3**: `followerSnapshot.Count == 0`, `leaderTargets.Count > 0`, `followerPosQty > 0` → branch (1) skipped → `ScaleLeaderTargets` fires

These pass because the B117 fix is purely additive — it only narrows branch (1); it does not alter the count=0 or count==leaderCount paths.

### JS Rules Applied

| Rule | Constraint | Applies To |
|------|-----------|------------|
| JS-021 | No `lock()` | Test file must have zero lock calls |
| JS-001 | No `throw new XxxException` | Test file must not throw directly |
| JS-066 | ASCII-only identifiers and strings | All test names and comments ASCII-only |

### 7-SCAN CHECKLIST (engineer must confirm all 7 before BUILD_PASS)

```
[ ] SCAN-01  Verify xUnit only in B117Tests.cs: no [TestFixture], no [TestMethod], no [Test]
             No NUnit using statements (NUnit.Framework), no MSTest using statements (Microsoft.VisualStudio.TestTools.UnitTesting)
             Expected: only [Fact] from Xunit

[ ] SCAN-02  grep "lock(" src/PropTraderTools/Tests/B117Tests.cs
             Expected: 0 matches

[ ] SCAN-03  grep "throw new" src/PropTraderTools/Tests/B117Tests.cs
             Expected: 0 matches

[ ] SCAN-04  dotnet build src/PropTraderTools/PropTraderTools.csproj
             Expected: 0 errors

[ ] SCAN-05  dotnet test src/PropTraderTools/Tests/
             Expected: ResolveFollowerTargets_PartialSnapshot_count2of3_ReturnsScaled -> PASS
                       ResolveFollowerTargets_PartialSnapshot_count1of3_ReturnsScaled -> PASS

[ ] SCAN-06  dotnet test src/PropTraderTools/Tests/
             Expected: all B116 tests PASS (zero regressions)
             Minimum: B116-T2 (count==leaderCount) and B116-T3 (count==0) still PASS

[ ] SCAN-07  powershell -File scripts\ptt-sync-and-verify.ps1
             Expected: 0 MISMATCH lines (B117Tests.cs synced to NT8 AddOns directory)
```

### BUILD_PASS — T2

After all 7 scans pass, write `docs/brain/B117/ticket-2-completion.md` containing:

- **Ticket ID**: B117-T2
- **File created**: `src/PropTraderTools/Tests/B117Tests.cs`
- **Change summary**: 2 new xUnit [Fact] tests for DW-B125 partial snapshot rejection
- **7-scan results**: each scan name + result (0/PASS)
  - SCAN-01 xUnit-only: no NUnit/MSTest / PASS
  - SCAN-02 lock(: 0 matches / PASS
  - SCAN-03 throw new: 0 matches / PASS
  - SCAN-04 dotnet build: 0 errors / PASS
  - SCAN-05 dotnet test B117: T1 PASS, T2 PASS / PASS
  - SCAN-06 dotnet test B116 regression: all B116 tests PASS / PASS
  - SCAN-07 ptt-sync-and-verify: 0 MISMATCH / PASS
- **dotnet test result**: T1 PASS, T2 PASS, all B116 tests PASS
- **ptt-sync-and-verify**: 0 MISMATCH (B117Tests.cs synced)

---

## Execution Order

1. **Execute T1 first** — fix the source file (`PttGlobalQuickExit.cs`)
2. **Execute T2 second** — create the test file (`B117Tests.cs`)
3. **Run T2 SCAN-05 and SCAN-06** — both must pass before BUILD_PASS is declared

T1 and T2 are sequential (T2 tests depend on T1 fix being in place).

---

## BUILD_PASS Definition

Both tickets complete and all 14 scan items green:
- T1 SCAN-01 through SCAN-07: all pass
- T2 SCAN-01 through SCAN-07: all pass
- `dotnet test` output shows T1 PASS, T2 PASS, and zero B116 regressions
- `ptt-sync-and-verify.ps1` shows 0 MISMATCH for both changed files
- F5 in NinjaTrader 8: Compilation succeeded, 0 errors

---

**TICKETS_COMPLETE**
