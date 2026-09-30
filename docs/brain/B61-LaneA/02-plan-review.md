# B61-LaneA Plan Review

**Block**: B61-LaneA
**Phase**: 2 (Plan Review)
**Written by**: ptt-plan-reviewer
**Date**: 2026-08-10
**Plan file**: docs/brain/B61-LaneA/02-architecture-plan.md

---

## Check 1: State filter present -- PASS

Plan Section 3 NEW body, line 1:
```csharp
if (state != OrderState.Filled && state != OrderState.Cancelled) return false; // (1)
```
`OrderState state` parameter is present in the new signature. All states except `Filled` and
`Cancelled` are rejected at the first guard. PASS.

---

## Check 2: No phantom leader order -- PASS

Plan Section 3 NEW body iterates `rule.FollowerAccounts` and calls `flattenOne(acc, instrument)`
per follower only. The leader `account` is never passed to `flattenOne`. The old
`Flatten(account, instrument)` call is absent from the new body; Section 3 rationale states
"Old `Flatten(account, instrument)` call is removed entirely." PASS.

---

## Check 3: CopyRule parameter present -- PASS

Plan Section 3 NEW signature:
```csharp
internal static bool TryDispatchLeaderFlat(
    Account account, Instrument instrument, OrderState state, CopyRule rule,
    Func<Account, bool> isFollower, Func<Account, Instrument, bool> hasOpenPosition,
    Action<Account, Instrument> flattenOne)
```
`CopyRule rule` is present as the fourth positional parameter. PASS.

---

## Check 4: IsFollowerAccount guard present -- PASS

Plan Section 3 NEW body, line 2:
```csharp
if (isFollower(account)) return false; // (2)
```
The guard is present as the second early-return check. At the call site (Section 4), the
delegate is bound to `IsFollowerAccount` (existing instance method, live at CopyEngine.cs:400).
PASS.

---

## Check 5: OLD text matches live source -- PASS

**Change 1 (lines 970-980)** — Plan OLD block compared to CopyEngine.cs lines 970-980
(read from live source):

Live source lines 970-980:
```
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
Exact character-for-character match with plan Section 3 OLD block. PASS.

**Change 2 (line 646)** — Plan OLD: `if (TryDispatchLeaderFlat(e.Order.Account, e.Order.Instrument)) return;`

Live source line 646:
```
            if (TryDispatchLeaderFlat(e.Order.Account, e.Order.Instrument)) return;
```
Text content matches exactly (leading whitespace is indentation). PASS.

---

## Check 6: No lock() in new code (JS-021) -- PASS

Plan Section 3 NEW body contains three `return false` early exits, one `foreach` loop, one
`if (acc == null) continue`, and one `return true`. Zero `lock(` occurrences. Plan Section 1
explicitly confirms: "Zero `lock()` statements in new method body." JS-021 satisfied. PASS.

---

## Check 7: No throw new in new code (JS-001) -- PASS

Plan Section 3 NEW body contains zero `throw` statements. Plan Section 1 confirms: "Zero
`throw` statements in new method body." JS-001 satisfied. PASS.

---

## Check 8: No return null in new code (JS-002) -- PASS

Return type is `bool` (value type). Returns `false` or `true` only. `null` return is
structurally impossible for a `bool` return type. JS-002 satisfied. PASS.

---

## Check 9: CYC <= 8 for new TryDispatchLeaderFlat (Plan Section 5) -- PASS

Plan Section 5 enumerates all decision points:

| # | Branch | Type |
|---|--------|------|
| 1 | `if (state != OrderState.Filled && state != OrderState.Cancelled)` | State guard |
| 2 | `if (isFollower(account))` | Follower guard |
| 3 | `if (hasOpenPosition(account, instrument))` | Open-position guard |
| 4 | `foreach (var acc in rule.FollowerAccounts)` | Loop |
| 5 | `if (acc == null) continue` | Null guard inside loop |

Strict McCabe CYC = 1 (base) + 5 = **6**. Limit is 8. 6 <= 8. PASS.

---

## Check 10: xUnit [Fact] only, four test methods T_B61_01..T_B61_04 (Plan Section 6) -- PASS

Plan Section 6 defines exactly four test methods:
- `T_B61_01_LeaderHasOpenPosition_ReturnsFalse` — `[Fact]`
- `T_B61_02_WrongState_Working_ReturnsFalse` — `[Fact]`
- `T_B61_03_AccountIsFollower_ReturnsFalse` — `[Fact]`
- `T_B61_04_HappyPath_FlattenOnlyFollowers_ReturnsTrue` — `[Fact]`

No `[Theory]`, no NUnit `[Test]`, no MSTest `[TestMethod]`. xUnit-only. PASS.

---

## Check 11: 7-scan checklist present with all 7 scans (Plan Section 8) -- PASS

Plan Section 8 contains exactly 7 scans:

| Scan | Verifies |
|------|---------|
| SCAN-01 | New `internal static` signature present |
| SCAN-02 | Zero `lock(` in new/modified lines |
| SCAN-03 | Zero `throw new` in new/modified lines |
| SCAN-04 | `Flatten(account, instrument)` absent from new body |
| SCAN-05 | Exactly 4 T_B61_ test methods present |
| SCAN-06 | `dotnet build` 0 errors, 0 new warnings |
| SCAN-07 | `dotnet test --filter T_B61_` 4 pass, 0 fail |

All 7 scans present. PASS.

---

## Check 12: Diff estimate <= 10,000 chars (Plan Section 7) -- PASS

Plan Section 7 estimate:
- CopyEngine.cs changes: ~618 chars
- CopyEngineTests.cs additions: ~3,500 chars
- **Grand total: ~4,118 chars** (41% of 10,000-char limit)

PASS.

---

## Violations

None.

---

## Result

REVIEW_PASS
