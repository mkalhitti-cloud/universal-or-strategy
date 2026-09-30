# B61-LaneA Tickets

**Block**: B61-LaneA
**Phase**: 3 (Ticket Generation)
**Written by**: ptt-architect
**Date**: 2026-08-10
**Plan**: docs/brain/B61-LaneA/02-architecture-plan.md (REVIEW_PASS)
**Defect closed**: DW-B61-01 (P0)

---

## TICKET-1: DW-B61-01 — TryDispatchLeaderFlat state guard + follower-only flatten

**Spec requirement ID**: DW-B61-01 (P0 regression — spurious leader-flatten fires on non-terminal
order states; leader account phantom order risk; no follower-scoping via CopyRule)

---

### Change 1 — Method replacement

**File**: `src/PropTraderTools/CopyEngine.cs`
**Location**: lines 969-980 (inclusive — comment block starts at 969, closing brace at 980)

**OLD** (verbatim from live file read, lines 969-980):

```csharp
        // DW-B60-01: Detect leader-flat and fan out PTT-Flatten to followers.
        // CYC=2: (1) follower guard, (2) position guard.
        // Only called from OnOrderUpdate after Gates 1+2+2.5 (copy enabled, rule matched).
        // JS-001: no throw. JS-002: returns bool. JS-021: no lock.
        // TESTABILITY: private instance -- testable via CopyEngine harness.
        private bool TryDispatchLeaderFlat(Account account, Instrument instrument)
        {
            if (IsFollowerAccount(account)) return false;           // (1) guard: not a follower
            if (HasOpenPosition(account, instrument)) return false; // (2) guard: leader is flat
            Flatten(account, instrument);
            return true;
        }
```

**NEW** (verbatim from plan Section 3):

```csharp
        // CYC=4 (spec-comment) / CYC=6 (strict McCabe, counting loop + null guard):
        // (1) state guard, (2) follower guard, (3) open-position guard, (4) foreach follower.
        // Fires only on Filled or Cancelled. Skips if account is a follower.
        // Skips if leader still has an open position.
        // Loops rule.FollowerAccounts directly -- does NOT touch the leader account.
        // JS-021: no lock. JS-001: no throw. JS-002: no null return.
        internal static bool TryDispatchLeaderFlat(
            Account account, Instrument instrument, OrderState state, CopyRule rule,
            Func<Account, bool> isFollower, Func<Account, Instrument, bool> hasOpenPosition,
            Action<Account, Instrument> flattenOne)
        {
            if (state != OrderState.Filled && state != OrderState.Cancelled) return false; // (1)
            if (isFollower(account)) return false;                                           // (2)
            if (hasOpenPosition(account, instrument)) return false;                          // (3)
            foreach (var acc in rule.FollowerAccounts)                                       // (4)
            {
                if (acc == null) continue;
                flattenOne(acc, instrument);
            }
            return true;
        }
```

---

### Change 2 — Call site update

**File**: `src/PropTraderTools/CopyEngine.cs`
**Location**: line 646

**OLD** (verbatim from live file read, line 646):

```csharp
            if (TryDispatchLeaderFlat(e.Order.Account, e.Order.Instrument)) return;
```

**NEW** (verbatim from plan Section 4):

```csharp
            if (TryDispatchLeaderFlat(
                    e.Order.Account, e.Order.Instrument, e.Order.OrderState, matchedRule.Value,
                    IsFollowerAccount, HasOpenPosition, FlattenOneAccount)) return;
```

---

### Test additions

**File**: `tests/PropTraderTools.Tests/CopyEngineTests.cs`
**Action**: Append four `[Fact]` methods to the existing test class (verbatim from plan Section 6)

```csharp
// -- B61 tests: TryDispatchLeaderFlat state guard + follower-only flatten --

[Fact]
public void T_B61_01_LeaderHasOpenPosition_ReturnsFalse()
{
    // Arrange: state=Filled, not a follower, but leader still has an open position.
    // Expect: returns false, flattenOne never called.
    var rule = new CopyRule { FollowerAccounts = new[] { (Account)null } };
    int flattenCallCount = 0;

    // Act
    var result = CopyEngine.TryDispatchLeaderFlat(
        account:         null,
        instrument:      null,
        state:           OrderState.Filled,
        rule:            rule,
        isFollower:      _ => false,
        hasOpenPosition: (_, __) => true,           // leader has open position
        flattenOne:      (_, __) => flattenCallCount++);

    // Assert
    Assert.False(result);
    Assert.Equal(0, flattenCallCount);
}

[Fact]
public void T_B61_02_WrongState_Working_ReturnsFalse()
{
    // Arrange: state=Working (non-terminal) -- state guard must block.
    // Expect: returns false, flattenOne never called.
    var rule = new CopyRule { FollowerAccounts = new[] { (Account)null } };
    int flattenCallCount = 0;

    // Act
    var result = CopyEngine.TryDispatchLeaderFlat(
        account:         null,
        instrument:      null,
        state:           OrderState.Working,
        rule:            rule,
        isFollower:      _ => false,
        hasOpenPosition: (_, __) => false,
        flattenOne:      (_, __) => flattenCallCount++);

    // Assert
    Assert.False(result);
    Assert.Equal(0, flattenCallCount);
}

[Fact]
public void T_B61_03_AccountIsFollower_ReturnsFalse()
{
    // Arrange: state=Filled, but the account is a follower (not a leader).
    // Expect: returns false, flattenOne never called.
    var rule = new CopyRule { FollowerAccounts = new[] { (Account)null } };
    int flattenCallCount = 0;

    // Act
    var result = CopyEngine.TryDispatchLeaderFlat(
        account:         null,
        instrument:      null,
        state:           OrderState.Filled,
        rule:            rule,
        isFollower:      _ => true,                 // account is a follower
        hasOpenPosition: (_, __) => false,
        flattenOne:      (_, __) => flattenCallCount++);

    // Assert
    Assert.False(result);
    Assert.Equal(0, flattenCallCount);
}

[Fact]
public void T_B61_04_HappyPath_FlattenOnlyFollowers_ReturnsTrue()
{
    // Arrange: state=Filled, not a follower, no open position, 2 follower accounts.
    // Expect: returns true; flattenOne called exactly twice (once per follower).
    // flattenOne must NOT be called with the leader account (null here).
    var follower1 = new Account();  // stub or null-safe in test harness
    var follower2 = new Account();
    var rule = new CopyRule { FollowerAccounts = new[] { follower1, follower2 } };

    var flattenedAccounts = new System.Collections.Generic.List<Account>();

    // Act
    var result = CopyEngine.TryDispatchLeaderFlat(
        account:         null,                      // leader account (must NOT appear in flattenedAccounts)
        instrument:      null,
        state:           OrderState.Filled,
        rule:            rule,
        isFollower:      _ => false,
        hasOpenPosition: (_, __) => false,
        flattenOne:      (acc, _) => flattenedAccounts.Add(acc));

    // Assert
    Assert.True(result);
    Assert.Equal(2, flattenedAccounts.Count);
    Assert.Contains(follower1, flattenedAccounts);
    Assert.Contains(follower2, flattenedAccounts);
    Assert.DoesNotContain(null, flattenedAccounts); // leader (null) never flattened
}
```

---

### JS rule constraints

| Rule | Requirement | Status in new code |
|------|-------------|-------------------|
| JS-001 | No `throw new XxxException` in hot paths | Zero `throw` statements in new method body |
| JS-002 | No `return null` for missing values | Method returns `bool` — null return structurally impossible |
| JS-021 | No `lock()` anywhere | Method is pure static with delegates — zero `lock()` |

---

### CYC constraint

New `TryDispatchLeaderFlat` strict McCabe CYC = **6** (5 decision points + 1 base).
Limit: ≤ 8. **PASS**.

| # | Branch | Type |
|---|--------|------|
| 1 | `if (state != OrderState.Filled && state != OrderState.Cancelled)` | State guard |
| 2 | `if (isFollower(account))` | Follower guard |
| 3 | `if (hasOpenPosition(account, instrument))` | Open-position guard |
| 4 | `foreach (var acc in rule.FollowerAccounts)` | Loop |
| 5 | `if (acc == null) continue` | Null guard inside loop |

---

### 7-scan checklist (engineer must run ALL before BUILD_PASS)

| Scan | Command | Expected result |
|------|---------|----------------|
| SCAN-01 | `grep -n "TryDispatchLeaderFlat" src/PropTraderTools/CopyEngine.cs` | New `internal static bool TryDispatchLeaderFlat(Account account, Instrument instrument, OrderState state, CopyRule rule, Func<Account, bool> isFollower, Func<Account, Instrument, bool> hasOpenPosition, Action<Account, Instrument> flattenOne)` signature present |
| SCAN-02 | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 hits in new or modified lines (pre-existing hits elsewhere are acceptable) |
| SCAN-03 | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 hits in new or modified lines |
| SCAN-04 | Inspect new `TryDispatchLeaderFlat` body | `Flatten(account, instrument)` call is GONE — 0 hits inside this method |
| SCAN-05 | `grep -n "T_B61_" tests/PropTraderTools.Tests/CopyEngineTests.cs` | Exactly 4 hits (T_B61_01, T_B61_02, T_B61_03, T_B61_04) |
| SCAN-06 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 0 new warnings |
| SCAN-07 | `dotnet test tests/PropTraderTools.Tests/ --filter "T_B61_"` | 4 tests pass, 0 fail |

---

### Commit message

```
fix(ptt): B61 -- TryDispatchLeaderFlat state guard + follower-only flatten [4 tests]
```

---

### Deploy steps (after all 7 scans pass)

**Step 1** — Manual copy to NT8 path:

```powershell
Copy-Item `
    -Path  "src\PropTraderTools\CopyEngine.cs" `
    -Destination "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs" `
    -Force
```

**Step 2** — Re-synchronize links:

```powershell
powershell -File .\scripts\verify_links.ps1 -Fix
```

**Step 3** — F5 in NinjaTrader 8: must show **0 errors** before any live testing or PR merge.

**Step 4** — Commit:

```powershell
git add src/PropTraderTools/CopyEngine.cs
git add tests/PropTraderTools.Tests/CopyEngineTests.cs
git add docs/brain/B61-LaneA/
git status --short       # confirm everything staged, nothing unintended
git commit -m "fix(ptt): B61 -- TryDispatchLeaderFlat state guard + follower-only flatten [4 tests]"
```

**Step 5** — Single workspace verification (V12.40 mandate):

```powershell
git branch --show-current   # must show: main
git worktree list            # must show: 1 PTT worktree only
```

---

### Verification steps (ptt-verifier)

| Check | Command | Expected result |
|-------|---------|----------------|
| VERIFY-01 | `grep -n "TryDispatchLeaderFlat" CopyEngine.cs` | New signature present with 7 parameters |
| VERIFY-02 | `grep -n "Flatten(account, instrument)" CopyEngine.cs` | 0 hits inside `TryDispatchLeaderFlat` body (old call gone) |
| VERIFY-03 | `grep -n "OrderState.Filled\|OrderState.Cancelled" CopyEngine.cs` | State guard present in `TryDispatchLeaderFlat` |
| VERIFY-04 | `grep -n "lock(" CopyEngine.cs` | 0 hits in new code |
| VERIFY-05 | `grep -n "T_B61_" CopyEngineTests.cs` | 4 hits |
| VERIFY-06 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors |
| VERIFY-07 | `dotnet test tests/PropTraderTools.Tests/ --filter "T_B61_"` | All 4 T_B61_ tests pass |
