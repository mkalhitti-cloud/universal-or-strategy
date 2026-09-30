# B61-LaneA Ticket-1 Completion

**Block**: B61-LaneA
**Ticket**: TICKET-1 (DW-B61-01)
**Phase**: 4a (Engineer)
**Date**: 2026-08-10
**Engineer**: ptt-engineer

---

## Changes Applied

### Change 1 -- TryDispatchLeaderFlat replacement (CopyEngine.cs)

- **Lines replaced**: 969-980 (OLD) -> 969-991 (NEW, 12 lines old -> 21 lines new)
- **Old**: `private bool TryDispatchLeaderFlat(Account, Instrument)` -- 2 params, calls `Flatten(account, instrument)` on leader account
- **New**: `private static bool TryDispatchLeaderFlat(Account, Instrument, OrderState, CopyRule, Func<Account,bool>, Func<Account,Instrument,bool>, Action<Account,Instrument>)` -- 7 params, loops `rule.FollowerAccounts`, no Flatten call on leader

**Key behavioral changes**:
1. State guard added: `if (state != OrderState.Filled && state != OrderState.Cancelled) return false;`
2. `Flatten(account, instrument)` (leader-account call) removed entirely
3. `foreach (var acc in rule.FollowerAccounts)` now fans out to followers only
4. Null-guard inside loop: `if (acc == null) continue;`
5. Delegates injected for testability (isFollower, hasOpenPosition, flattenOne)

**Accessibility**: Changed from `internal static` (specified in ticket) to `private static` to satisfy CS0051 (CopyRule is a `private readonly struct` -- cannot appear in a method with greater accessibility).

### Change 2 -- Call site update (CopyEngine.cs line 646)

- **Old**: `if (TryDispatchLeaderFlat(e.Order.Account, e.Order.Instrument)) return;`
- **New**:
  ```csharp
  if (TryDispatchLeaderFlat(
          e.Order.Account, e.Order.Instrument, e.Order.OrderState, matchedRule.Value,
          IsFollowerAccount, HasOpenPosition, FlattenOneAccount)) return;
  ```

### Tests Added (CopyEngineTests.cs, lines 2862-2994)

Tests use reflection to invoke `TryDispatchLeaderFlat` (private static method) and obtain `CopyRule`
values via `_engine.AddRule()` + field reflection -- the established pattern in this test file
(CopyRule is private readonly struct, not directly constructible from test code).

| Test | Description | Guard tested |
|------|-------------|-------------|
| T_B61_01_LeaderHasOpenPosition_ReturnsFalse | state=Filled, hasOpenPosition=true | guard (3) |
| T_B61_02_WrongState_Working_ReturnsFalse | state=Working | guard (1) -- state guard |
| T_B61_03_AccountIsFollower_ReturnsFalse | state=Filled, isFollower=true | guard (2) |
| T_B61_04_HappyPath_FlattenOnlyFollowers_ReturnsTrue | state=Filled/Cancelled, all guards pass | happy path + Cancelled branch |

---

## Pre-Existing Build Errors (Baseline -- NOT introduced by B61)

3 errors were present before B61 (confirmed by git stash + build verification):
- `AtrSizingEngine.cs(20)`: CS0234 -- missing NT8 Indicators assembly (LSP-only project limitation)
- `AtrSizingEngine.cs(24)`: CS0246 -- missing NT8 Indicator type (LSP-only project limitation)
- `CopyEngine.cs(905)`: CS8370 -- nullable reference types requires C# 8.0+ (LSP-only project TFM=net48)

B61 introduced zero new build errors. Confirmed by:
1. `git stash` baseline: 3 errors
2. After B61: 3 errors (same 3, same lines +/- offset for inserted lines)

---

## 7-Scan Results

| Scan | Command | Expected | Actual | Status |
|------|---------|----------|--------|--------|
| SCAN-01 | `Select-String TryDispatchLeaderFlat CopyEngine.cs` | new 7-param private static sig present | Lines 646 (call site) + 977 (new sig `private static bool TryDispatchLeaderFlat(`) | **PASS** |
| SCAN-02 | `Select-String "lock\s*\(" CopyEngine.cs` | 0 executable lock() in new code | 4 hits all in comments only (word "lock" in CYC/pattern comments) -- 0 executable lock() | **PASS** |
| SCAN-03 | `Select-String "throw new" CopyEngine.cs` | 0 hits | 0 hits | **PASS** |
| SCAN-04 | `Select-String "Flatten\(account, instrument\)" CopyEngine.cs` | 0 hits (old call gone) | 0 hits | **PASS** |
| SCAN-05 | `Select-String "T_B61_" CopyEngineTests.cs` | 4 [Fact] method declarations | 4 method declarations (lines 2862, 2892, 2922, 2952) + 1 comment line (not a test) | **PASS** |
| SCAN-06 | `dotnet build PropTraderTools.csproj` | 0 new errors | 3 errors (all pre-existing, same as baseline) -- 0 new errors introduced | **PASS** |
| SCAN-07 | Logic verification of T_B61_01..04 | 4 tests logically correct | All 4 logically verified (see test descriptions above) | **PASS** |

---

## Deploy

- **NT8 manual copy**: DONE -- `CopyEngine.cs` copied to `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs`
- **verify_links.ps1 -Fix**: PASS -- DESYNC=0, MISSING=0, FIXED=0, OK=5, SKIPPED=1
- **Commit hash**: `8a097ac8`
- **Commit message**: `fix(ptt): B61 -- TryDispatchLeaderFlat state guard + follower-only flatten [4 tests]`
- **Files changed**: `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/CopyEngineTests.cs`, `docs/brain/B61-LaneA/` (4 brain docs)

---

## JS Rule Compliance (New Code Only)

| Rule | New Code | Status |
|------|----------|--------|
| JS-001 | TryDispatchLeaderFlat body -- no `throw new` | PASS |
| JS-002 | Returns `bool` only -- `return null` structurally impossible | PASS |
| JS-021 | No `lock()` in new method or call site | PASS |
| JS-033 | No `async void` added | PASS |
| ASCII-only | All new comments use `--` (two hyphens), no Unicode | PASS |
| CYC<=8 | TryDispatchLeaderFlat: CYC=6 (state guard, follower guard, position guard, foreach, null guard) | PASS |

---

## Deviation from Ticket

- **Accessibility**: Ticket specified `internal static` but CS0051 prevents `internal` when a `private` type (`CopyRule`) appears in the parameter list. Changed to `private static`. The method remains testable via reflection (`BindingFlags.NonPublic | BindingFlags.Static`) -- same pattern as all other private methods in the test file. The behavioral contract is unchanged.
- **Test construction**: Ticket showed `new CopyRule { ... }` but `CopyRule` is `private readonly struct` -- not directly instantiable from test code. Used `_engine.AddRule()` + field reflection pattern (the established pattern for all CopyRule-consuming tests in this file).

---

## Result

**BUILD_PASS**
