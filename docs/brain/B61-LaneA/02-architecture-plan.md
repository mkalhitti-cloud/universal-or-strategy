# B61-LaneA Architecture Plan

**Block**: B61-LaneA
**Phase**: 1 (Architecture)
**Written by**: ptt-architect
**Date**: 2026-08-10
**Defect closed**: DW-B61-01 (P0)
**Status**: REVIEW_PENDING

---

## Section 1 -- Rules Catalog Gate

**Rules checked**: JS-001, JS-002, JS-021 per `docs/standards/jane-street/RULES_CATALOG.md`.

| Rule | Description | New TryDispatchLeaderFlat | Result |
|------|-------------|--------------------------|--------|
| JS-001 | No `throw new XxxException` in hot paths | Zero `throw` statements in new method body | PASS |
| JS-002 | No `return null` for missing values | Method returns `bool` -- null return impossible | PASS |
| JS-021 | No `lock()` anywhere | Method is pure static with delegates -- zero lock() | PASS |

**Pre-flight scans on existing modified region (CopyEngine.cs:646 and :974-980)**:

- `grep "lock(" src/PropTraderTools/CopyEngine.cs` -- zero hits in changed lines
- `grep "throw new" src/PropTraderTools/CopyEngine.cs` -- zero hits in changed lines
- `grep "return null" src/PropTraderTools/CopyEngine.cs` -- zero hits in changed lines (method returns bool)

**GATE RESULT: PASS**

---

## Section 2 -- Problem Statement (DW-B61-01)

### What B60 implemented

Block B60 added `TryDispatchLeaderFlat(Account account, Instrument instrument)` at
`CopyEngine.cs:974` to propagate leader-flat events to follower accounts. The call site
at line 646 fires after the Gate 2.5 Mirror relay and the B56 Cancelled block.

### Bug 1 -- Wrong OrderState filter (fires on all states)

The B60 implementation has no `OrderState` guard. `TryDispatchLeaderFlat` is called
unconditionally for every order state that reaches line 646: Working, Accepted, PartFilled,
Change, ChangeSubmitted, etc. A follower flatten should only trigger when the leader order
is truly terminal (`Filled` or `Cancelled`). Firing on non-terminal states can trigger
spurious flattens while the leader position is still active.

Note: `OrderState.Cancelled` already causes an early `return` at line 642 (B56 block),
so Cancelled is practically unreachable at line 646 today. The new state guard is
defensive and future-proof for any future reordering of the gate chain.

### Bug 2 -- Phantom order on leader account

The B60 implementation calls `Flatten(account, instrument)` -- the leader-account overload
at `CopyEngine.cs:1151`. That overload calls `FlattenOneAccount(leader, instrument)` first
(line 1158) before iterating `AllAccounts`. This issues a market order on the **leader
account** even though the leader is already flat (the `HasOpenPosition` guard would return
false if position == 0, so `FlattenOneAccount` would skip -- but if the position check is
evaluated differently due to timing, a phantom market order on the leader account is
possible). Correct behaviour is to iterate **only `rule.FollowerAccounts`** and call
`FlattenOneAccount` per follower. The leader must never be touched.

### Bug 3 -- No CopyRule parameter

The B60 method signature is `TryDispatchLeaderFlat(Account account, Instrument instrument)`.
It has no access to the matched `CopyRule` and therefore cannot iterate
`rule.FollowerAccounts`. The call to `Flatten(account, instrument)` is a workaround that
bypasses follower-scoping entirely.

### Fix summary

Replace the method with an `internal static` helper that:
1. Accepts `OrderState state` -- rejects all states except `Filled` / `Cancelled`.
2. Accepts `CopyRule rule` -- iterates `rule.FollowerAccounts` directly.
3. Never calls `Flatten(account, instrument)` (leader overload) -- calls `flattenOne`
   delegate per follower only.
4. Uses injected `Func`/`Action` delegates for NT8-free testability.

---

## Section 3 -- Change 1: TryDispatchLeaderFlat Method Replacement

**File**: `src/PropTraderTools/CopyEngine.cs`
**Lines**: 970-980 (inclusive)

### OLD (exact text from file read, lines 970-980):

```csharp
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

### NEW (exact replacement):

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

### Change 1 rationale

- `internal static`: enables direct xUnit testing with no NT8 runtime. No `CopyEngine`
  instance needed in tests.
- Delegate parameters `isFollower`, `hasOpenPosition`, `flattenOne`: decouple the logic
  from NT8 instance methods. Tests pass lightweight lambdas; production call site passes
  `IsFollowerAccount`, `HasOpenPosition`, `FlattenOneAccount`.
- `rule.FollowerAccounts` loop: only follower accounts are flattened. The leader account
  (`account`) is never passed to `flattenOne`.
- Old `Flatten(account, instrument)` call is removed entirely.

---

## Section 4 -- Change 2: Call Site Update

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line**: 646

### OLD (exact text from file read, line 646):

```csharp
            if (TryDispatchLeaderFlat(e.Order.Account, e.Order.Instrument)) return;
```

### NEW:

```csharp
            if (TryDispatchLeaderFlat(
                    e.Order.Account, e.Order.Instrument, e.Order.OrderState, matchedRule.Value,
                    IsFollowerAccount, HasOpenPosition, FlattenOneAccount)) return;
```

### Change 2 rationale

- `e.Order.OrderState` -- passes live order state; state guard now filters wrong states.
- `matchedRule.Value` -- passes the matched `CopyRule`; follower loop now scoped correctly.
- `IsFollowerAccount` -- existing instance method; delegate signature `Func<Account, bool>` matches.
- `HasOpenPosition` -- existing instance method; delegate signature `Func<Account, Instrument, bool>` matches.
- `FlattenOneAccount` -- existing private instance method; delegate signature `Action<Account, Instrument>` matches.
  Because `TryDispatchLeaderFlat` is now `internal static`, the method group references
  (`IsFollowerAccount`, `HasOpenPosition`, `FlattenOneAccount`) are bound as instance
  delegates from within the same class -- no change required to those methods.

---

## Section 5 -- CYC Analysis

### New TryDispatchLeaderFlat decision points

| # | Branch | Description |
|---|--------|-------------|
| 1 | `if (state != OrderState.Filled && state != OrderState.Cancelled)` | State guard (compound -- counts as 1 decision) |
| 2 | `if (isFollower(account))` | Follower guard |
| 3 | `if (hasOpenPosition(account, instrument))` | Open-position guard |
| 4 | `foreach (var acc in rule.FollowerAccounts)` | Loop entry/exit |
| 5 | `if (acc == null) continue;` | Null guard inside loop |

**Strict McCabe CYC** = 1 (base) + 5 = **6** -- within ≤ 8 limit. PASS.
**Spec-comment CYC** = 4 (counting only major semantic branches). PASS.

### Unchanged methods (CYC unaffected)

| Method | Existing CYC | Change |
|--------|-------------|--------|
| `IsFollowerAccount` | 3 (confirmed line 400) | None -- passed as delegate |
| `HasOpenPosition` | unchanged | None -- passed as delegate |
| `FlattenOneAccount` | 3 (confirmed line 1248) | None -- passed as delegate |
| `Flatten(Account, Instrument)` | 4 (confirmed line 1151) | Not called from TryDispatchLeaderFlat any more |

---

## Section 6 -- Test Plan

**Test file**: `tests/PropTraderTools.Tests/CopyEngineTests.cs` (add to existing file)
**Framework**: xUnit [Fact] only. No NUnit. No MSTest.
**Strategy**: All four tests call `CopyEngine.TryDispatchLeaderFlat` as a static method
with injected lambdas -- no NT8 runtime required.

The tests use `NinjaTrader.Cbi.Account` and `NinjaTrader.Cbi.Instrument` stubs that are
already present in the test harness (see existing tests). If those stubs are not available,
the delegate contract allows passing `null` for account/instrument and adapting the lambda
to ignore them -- the static method itself never dereferences account or instrument directly
(only passes them through to delegates).

```csharp
// ── B61 tests: TryDispatchLeaderFlat state guard + follower-only flatten ──

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

## Section 7 -- Diff Size Estimate

### CopyEngine.cs changes

| Change | Old chars | New chars | Delta |
|--------|-----------|-----------|-------|
| Change 1 (lines 970-980, 11 lines) | ~430 | ~870 | +440 |
| Change 2 (line 646, 1 line) | ~62 | ~240 | +178 |
| **CopyEngine.cs subtotal** | | | **~618** |

### CopyEngineTests.cs additions

| Addition | Chars |
|----------|-------|
| 4x [Fact] test methods (~115 lines) | ~3,500 |

### Grand total: ~4,118 characters

**Limit**: 10,000 characters. **PASS** (4,118 / 10,000 = 41%).

---

## Section 8 -- 7-Scan Checklist (Engineer Contract)

The following scans MUST be run by ptt-engineer after implementing the changes.
Zero-failure threshold unless otherwise noted.

| Scan | Command | Expected Result |
|------|---------|-----------------|
| SCAN-01 | `grep -n "TryDispatchLeaderFlat" src/PropTraderTools/CopyEngine.cs` | New `internal static bool TryDispatchLeaderFlat(Account account, Instrument instrument, OrderState state, CopyRule rule, Func<Account, bool> isFollower, Func<Account, Instrument, bool> hasOpenPosition, Action<Account, Instrument> flattenOne)` signature present |
| SCAN-02 | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 hits in new or modified lines (pre-existing hits, if any, are acceptable) |
| SCAN-03 | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 hits in new or modified lines |
| SCAN-04 | Inspect new TryDispatchLeaderFlat body | `Flatten(account, instrument)` call is GONE -- 0 hits inside this method |
| SCAN-05 | `grep -n "T_B61_" tests/PropTraderTools.Tests/CopyEngineTests.cs` | Exactly 4 hits (T_B61_01, T_B61_02, T_B61_03, T_B61_04) |
| SCAN-06 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 0 new warnings |
| SCAN-07 | `dotnet test tests/PropTraderTools.Tests/ --filter "T_B61_"` | 4 tests pass, 0 fail |

---

## Section 9 -- Deferred Items Carry-Forward

All items below are carried forward unchanged from `docs/brain/B60-LaneA/06-deferred-backlog.md`.
**No items are closed by B61** (DW-B61-01 is NEW and is closed by this block's implementation phase).

| ID | Description | Priority | Status |
|----|-------------|----------|--------|
| DW-B58-01 | `SnapshotTargetsPublic` hardcoded order-name prefixes (`PTT-QX-T`, `PTT-TGT-`) must be updated when new PTT-prefixed target order names are added | P2 | OPEN |
| DW-B58-02 | `GlobalBe` non-atomic lazy init (`if (_globalBe == null) _globalBe = new ...`). Safe while UI-thread-only; requires `Interlocked.CompareExchange` if non-UI caller added | P2 | OPEN |
| DW-B58-03 | `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`. Future OcoId fan-out will require a new `SubmitBeStop` overload | P2 | OPEN |
| DW-B54-01 | ATM auto-inject -- blocked; `AtmStrategyCreate()` is `StrategyBase`-only, unavailable in `AddOnBase`. Deferred indefinitely pending Director decision | P1 | OPEN (blocked) |
| PRE-EXISTING-01 | Non-ASCII characters at `CopyEngine.cs` lines 395, 496 | P2 | OPEN |
| PRE-EXISTING-02 | Non-ASCII characters at `CopyEngine.cs` lines 1256, 1257 | P2 | OPEN |
| PRE-EXISTING-03 | `deploy-sync.ps1` archived; PropTraderTools sync is manual (SHA-256 copy + `verify_links.ps1 -Fix`) | P2 | OPEN |

---

## Section 10 -- Commit and Deploy Steps

### Step 1: Manual copy to NT8 path

After `dotnet build` passes with 0 errors, copy the modified file to the NT8 custom AddOns directory:

```powershell
Copy-Item `
    -Path  "src\PropTraderTools\CopyEngine.cs" `
    -Destination "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs" `
    -Force
```

### Step 2: Run verify_links.ps1 -Fix

```powershell
powershell -File .\scripts\verify_links.ps1 -Fix
```

This re-synchronizes any hard links and confirms the NT8 AddOns path is current.

### Step 3: F5 compilation gate in NinjaTrader 8

Open NinjaTrader 8, press F5 to compile. Must show **0 errors** before any live testing.
Do NOT merge the PR until F5 is green.

### Step 4: Commit

```powershell
git add src/PropTraderTools/CopyEngine.cs
git add tests/PropTraderTools.Tests/CopyEngineTests.cs
git add docs/brain/B61-LaneA/
git status --short       # confirm everything is staged, nothing unintended
git commit -m "fix(ptt): B61 -- TryDispatchLeaderFlat state guard + follower-only flatten [4 tests]"
```

### Step 5: Verify NT8 build (single workspace mandate)

Per V12.40 Single Workspace Mandate: all work is on `main` branch in
`C:\WSGTA\universal-or-strategy`. After commit, verify:

```powershell
git branch --show-current   # must show: main
git worktree list            # must show: 1 PTT worktree only
```

---

## Appendix -- Data Flow Diagram

```
OnOrderUpdate(e)
  |
  +-- Gate 1: copy enabled?               [existing]
  +-- Gate 2: rule matched?               [existing]
  +-- Gate 2.5: Mirror relay              [existing]
  +-- B56 Cancelled block                 [existing -- returns early on Cancelled]
  |
  +-- TryDispatchLeaderFlat(             [B61 CHANGE -- new signature]
  |       e.Order.Account,
  |       e.Order.Instrument,
  |       e.Order.OrderState,            <-- NEW: state guard
  |       matchedRule.Value,             <-- NEW: follower scope
  |       IsFollowerAccount,
  |       HasOpenPosition,
  |       FlattenOneAccount)
  |     |
  |     +-- state != Filled && != Cancelled --> return false (no-op)
  |     +-- isFollower(account)           --> return false (no-op)
  |     +-- hasOpenPosition(...)          --> return false (leader still has pos)
  |     +-- foreach follower in rule.FollowerAccounts
  |           flattenOne(follower, instrument)   <-- followers only, leader NEVER
  |     return true --> caller returns
  |
  +-- Gate B: bracket drag detection      [existing]
  +-- DispatchCopy                        [existing]
```
