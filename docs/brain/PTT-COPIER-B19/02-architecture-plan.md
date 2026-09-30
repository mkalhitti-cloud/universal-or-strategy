# PTT-COPIER-B19 Architecture Plan
**Ticket**: DW-B19-COPIER-BUG-01
**Priority**: P0 — copier produces zero follower orders
**Status**: REVIEW_PASS (revised Cycle 2 — added line 659 audit + DW-B19-02 deferred entry)
**Architect**: ptt-architect
**Date**: 2026-07-13

---

## Section 1: Scope

| Dimension | Value |
|-----------|-------|
| Epic | PTT-COPIER-B19 |
| Lane | Lane 1 only (Lane 2 is a separate parallel lane, out of scope) |
| Source file changed | `src/PropTraderTools/CopyEngine.cs` |
| Line changed | Line 381 — one sub-condition in Gate 2 of `OnOrderUpdate` |
| Test file changed | `src/PropTraderTools/CopyEngineTests.cs` |
| New tests added | 2 `[Fact]` methods |
| Files NOT touched | `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`, `AtrSizingEngine.cs` |

B18 deferred items (DW-B17-SYNC-01, DW-B17-ACCOUNT-NAME-01, DW-B17-NT8-041) are READ ONLY.
They are NOT planned or touched in this block.

---

## Section 2: Root Cause Analysis

### The Bug

`CopyEngine.OnOrderUpdate` contains a Gate 2 rule-matching loop (lines 378–389 of current
source). Gate 2 walks `_rules` (a `ConcurrentBag<CopyRule>`) to find the rule whose
`MasterAccount` matches the incoming order's account:

```csharp
// CopyEngine.cs line 379-386 (current)
foreach (var rule in _rules)
{
    if (e.Order.Instrument.FullName == rule.Instrument
        && e.Order.Account == rule.MasterAccount)      // <-- reference equality
    {
        matchedRule = rule;
        break;
    }
}
```

`CopyRule.MasterAccount` is of type `NinjaTrader.Cbi.Account` — a reference type.
`AddRule` stores the Account reference at the moment the rule is registered:

```csharp
// CopyEngine.cs line 301-303
internal void AddRule(string instrument, Account master, Account[] followers)
{
    _rules.Add(CopyRule.Create(instrument, master, followers));
}
```

### The Reconnect Scenario

At 16:43 on 2026-07-13 (log.20260713.00002.txt), the Rithmic connection dropped and
reconnected. On reconnect, NinjaTrader 8 internally recreates its `Account` object instances.
The new `Account` object represents the same brokerage account (same name, same credentials)
but is a **different C# reference** — a new heap allocation at a new address.

The `CopyRule._rules` bag still holds the old reference captured at `AddRule` time. After
reconnect:

- `rule.MasterAccount` — points to the old, stale Account object
- `e.Order.Account` — points to the new, fresh Account object
- `e.Order.Account == rule.MasterAccount` — compares two **different object references** → `false`

Gate 2 finds no matching rule. `matchedRule` stays `null`. The early return fires.
`DispatchCopy` is never reached. `SendCopy` is never called. **Zero follower orders** for every
leader trade placed after reconnect.

### Why Reference Equality Is Wrong Here

`NinjaTrader.Cbi.Account` does not override `Equals` or `==`. The default `object ==`
operator performs **reference equality** (pointer comparison), not value equality.
Two Account objects representing the same brokerage account are equal by name but not by
reference after a reconnect. Using reference equality for a long-lived stored reference is
therefore incorrect for this use case.

### The Correct Comparison

`Account.Name` is a `string` property on `NinjaTrader.Cbi.Account`. It returns the
human-readable account identifier (e.g. `"Rithmic2"`). String equality on `.Name`
is stable across reconnects because the string value does not change when NT8 recreates
the Account object.

---

## Section 3: The Fix

### Exact Before/After Diff

**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `OnOrderUpdate(object sender, OrderEventArgs e)`
**Line**: 381

**BEFORE** (current line 381):
```csharp
if (e.Order.Instrument.FullName == rule.Instrument && e.Order.Account == rule.MasterAccount)
```

**AFTER** (fixed line 381):
```csharp
if (e.Order.Instrument.FullName == rule.Instrument && e.Order.Account.Name == rule.MasterAccount?.Name)
```

### Rationale for `?.Name` (null-conditional)

The Director's spec states the fix as `.Name == .Name`. The null-conditional `?.Name` on
`rule.MasterAccount` is a required safety addition, not a deviation from intent:

1. `AddRule` accepts `(Account)null` as `master` — exercised by 5+ existing tests
   (CopyEngineTests.cs lines 68, 88, 125, 136).
2. Without `?.Name`, iterating over `_rules` when any rule holds a null `MasterAccount`
   would throw `NullReferenceException` on first evaluation of the Gate 2 condition.
3. `rule.MasterAccount?.Name` evaluates to `null` when `MasterAccount` is null.
4. `e.Order.Account.Name == null` evaluates to `false` — Gate 2 finds no match — early return
   — no exception, no copy dispatch. Correct no-op behavior.
5. `e.Order.Account` (the live order's account) is never null in NT8 runtime; no null guard
   needed on the left-hand side.

### No Other Changes to `OnOrderUpdate`

The entire Gate structure, all other gates, all method calls, and the surrounding logic are
**unchanged**. This is a single-sub-condition edit within one `if` expression.

---

## Section 4: Reference-Equality Audit (All Account == Locations in CopyEngine.cs)

A full audit of all `Account`-typed comparisons in `CopyEngine.cs` was performed.

### Audit Table

| Location | Comparison | Finding | Disposition |
|----------|------------|---------|-------------|
| Line 381 (Gate 2, `OnOrderUpdate`) | `e.Order.Account == rule.MasterAccount` | BROKEN after reconnect — stale reference | **FIXED in B19 T1** |
| Line 659 (`PopulateOrderMap`) | `b.FollowerAccount == followerAccount` | Reference equality; may accumulate duplicate FollowerBinding entries after reconnect | **DW-B19-02 DEFERRED — OUT OF SCOPE for B19** |

### Line 659 — `PopulateOrderMap` Dedup Guard

```csharp
// Line 653-661
private void PopulateOrderMap(string fromEntrySignalName, Account followerAccount)
{
    var bag = _orderMap.GetOrAdd(fromEntrySignalName, _ => new ConcurrentBag<FollowerBinding>());
    // Dedup guard: prevent accumulating duplicate bindings on repeated Working state events
    if (!bag.Any(b => b.FollowerAccount == followerAccount))   // line 659 -- reference equality
        bag.Add(new FollowerBinding(followerAccount, fromEntrySignalName));
}
```

`PopulateOrderMap` is called at line 403 with `e.Order.Account` (the live account reference).
`b.FollowerAccount` was stored from a prior call to `e.Order.Account`. After Rithmic reconnect,
both references may differ → dedup guard fails → duplicate `FollowerBinding` entries accumulate
→ bracket sync fires multiple times for the same follower.

**Severity**: P2 (duplicate bracket syncs, not zero orders — different from the P0 bug).
**Fix identified**: Change to `b.FollowerAccount.Name == followerAccount.Name`.
**Disposition (DW-B19-02 DEFERRED — 4 reasons)**:
1. B19 scope is the P0 zero-orders bug (Gate 2 only, as specified by Director).
2. No-Scope-Creep Protocol (AGENTS.md §11): one epic = one concern.
3. Director specified exactly one line change for B19 Lane 1.
4. Lane boundary: adding a second fix without Director approval violates protocol.

### `FindRule` (lines 1040–1050) — CONFIRMED OK

`FindRule` matches by `instrument.FullName` (string) only. No Account comparison. Not affected.

### `AllAccounts` (lines 1026–1038) — CONFIRMED OK

`AllAccounts` calls `FindRule` (string comparison) and yields Account references. No Account `==`
comparison performed. Not affected.

---

## Section 5: Test Design

### NT8 Instantiation Constraint

`NinjaTrader.Cbi.Account` cannot be instantiated in xUnit tests. NT8 Account objects are
created exclusively by the NT8 runtime. Both new tests use the structural contract approach:
assert properties of the type system and stored data, not runtime NT8 behavior.

### Test 1: `Gate2_UsesAccountName_SourceContractVerified`

**Purpose**: Verify the structural pre-conditions for the Gate 2 fix are in place.
Confirm `Account` has a `Name` property of type `string` (the property the fix depends on).

**Approach**: Reflection to verify `CopyRule.MasterAccount` is type `Account`, and
`Account.Name` is a public instance `string` property.

### Test 2: `Gate2_NullMasterAccount_NoCopyOrder`

**Purpose**: Verify that when a rule has a null `MasterAccount`, Gate 2 does not throw
`NullReferenceException` and no copy dispatch fires. Guards against regression to
`rule.MasterAccount.Name` (non-null-conditional).

**Approach**: AddRule with null master; simulate `?.Name` evaluation; verify no StatusUpdate.

---

## Section 6: Jane Street Rule Compliance

| Rule | Status |
|------|--------|
| JS-021 — No `lock()` | PASS — Gate 2 is read-only `foreach` over `ConcurrentBag`. Fix changes condition expression only. |
| JS-001 — No `throw` in hot paths | PASS — null-conditional `?.Name` evaluates to null on null input (no exception). |
| JS-002 — No `return null` | PASS — No new methods introduced. |
| CYC ≤ 8 | PASS — Fix changes type of comparison sub-expression. No branches added or removed. `OnOrderUpdate` CYC unchanged (7). |

---

## Section 7: NT8 Compiler Rule Compliance

| Check | Evidence | Result |
|-------|----------|--------|
| `Account.Name` valid string property | Used in 10+ existing lines (456, 514, 589, 820, 843, 881, 925, 967, 997, 1068) | CONFIRMED |
| `string == string` in .NET 4.8 | Standard equality operator | VALID |
| NT8-001 (`init;` ban) | No new properties | CLEAN |
| NT8-002 (`record` ban) | No new record types | CLEAN |
| NT8-003 (`volatile double` ban) | No volatile fields | CLEAN |
| NT8-004 (`ImmutableDictionary` ban) | No immutable collections | CLEAN |
| NT8-007 (`CreateOrder` arg 12 ban) | No `CreateOrder` in changed lines | CLEAN |
| `?.` null-conditional validity | C# 6+ / .NET 4.8 — confirmed valid | VALID |

---

## Section 8: 7-Scan Checklist (Pre-Filled)

| ID | Scan | Expected Result |
|----|------|-----------------|
| SCAN-01 | `grep -n "e\.Order\.Account ==" src/PropTraderTools/CopyEngine.cs` | Zero matches |
| SCAN-02 | `grep -n "\.Account\.Name ==" src/PropTraderTools/CopyEngine.cs` | Exactly 1 match |
| SCAN-03 | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | Zero matches |
| SCAN-04 | `grep -n "async void " src/PropTraderTools/CopyEngine.cs` | Zero matches |
| SCAN-05 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | Zero errors |
| SCAN-06 | `dotnet test --filter "Gate2"` | Both Gate2 tests pass |
| SCAN-07 | `dotnet test src/PropTraderTools/` | All 113 tests pass |

---

## Section 9: Deferred Work Identified This Block

| ID | Item | File | Line | Severity | Fix | Target |
|----|------|------|------|----------|-----|--------|
| DW-B19-02 | `PopulateOrderMap` dedup guard uses reference equality — duplicate bindings after reconnect | `CopyEngine.cs` | 659 | P2 | `.Name == .Name` comparison | B20+ |

---

## Component Summary

| Component | File | Change |
|-----------|------|--------|
| `CopyEngine.OnOrderUpdate` | `CopyEngine.cs:381` | Change `== rule.MasterAccount` to `.Account.Name == rule.MasterAccount?.Name` |
| `Gate2_UsesAccountName_SourceContractVerified` | `CopyEngineTests.cs` | New `[Fact]` — type-contract structural test |
| `Gate2_NullMasterAccount_NoCopyOrder` | `CopyEngineTests.cs` | New `[Fact]` — null-safety guard test |

**Total lines changed in source**: 1
**Total new test lines**: ~40
**Files modified**: 2
**Files added**: 0
