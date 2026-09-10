# Implementation Tickets: PTT-REPAIRS-08-JS002
# JS-002 return-null repairs in CopyEngine.cs
# Generated from: 02-architecture-plan.md (REVIEW_PASS, Cycle 2)
# Plan review: 02-plan-review.md (REVIEW_PASS)

---

## Execution Order

T1 -> T2 -> T3. Each ticket must be implemented, built, and verified before the next begins.
All changes confined to `src/PropTraderTools/CopyEngine.cs`.
No external files require code edits (TradeCopierPanel.cs, PttBreakEvenSwap.cs are read-only in this epic).

---

## Baseline (confirm before starting T1)

```
dotnet build src/PropTraderTools/ -> 0 Error(s)
dotnet test                       -> 19 passed / 450 failed (NT8-runtime) / 32 skipped
Target file                       : src/PropTraderTools/CopyEngine.cs (only)
LangVersion                       : 9.0
NRT context                       : <Nullable> NOT set -- annotations are informational only
```

---

---

# T1 -- Struct-Nullable Cosmetic: `return null` -> `return default`

## Ticket Metadata

| Field | Value |
|-------|-------|
| Ticket ID | T1 |
| Title | Struct-Nullable Cosmetic: replace `return null` with `return default` in four methods |
| Epic | PTT-REPAIRS-08-JS002 |
| File | `src/PropTraderTools/CopyEngine.cs` |
| Prerequisite | Baseline build confirmed (0 errors) |
| Successor | T2 |

## Spec Requirement IDs Satisfied

- REQ-JS002: No `return null` in methods returning `Nullable<struct>` (T types: `CopyRule?`, `double?`)
- REQ-CYC-BUDGET: All changed methods <= 8 branches (annotation-only; no new branches)
- REQ-BUILD: dotnet build 0 Error(s) after changes
- REQ-TEST: dotnet test >= 19 passed, no new regressions

## JS Rule Constraints

| Rule | Constraint | Status After Fix |
|------|------------|-----------------|
| JS-002 | No `return null` | SATISFIED -- `return default` is lint-clean for Nullable<struct> |
| JS-021 | No lock() | SATISFIED -- no lock() added or touched |
| JS-001 | No new throw | SATISFIED -- no throw keyword added |
| JS-013 | CYC <= 8 per method | SATISFIED -- cosmetic change only, no branches added |

## Scope Lock

**Engineer must implement ONLY T1 changes (5 `return null` -> `return default` substitutions across 4 methods).**
Do NOT edit any other method, comment, whitespace, or formatting in the file.
Do NOT implement T2 or T3 work in this ticket.

---

## Methods Changed (Before and After)

### Method 1: FindMatchingRule

**Location**: `src/PropTraderTools/CopyEngine.cs` ~L1987
**Grep target**: `private CopyRule? FindMatchingRule(Order order)`

Before (current source):
```csharp
private CopyRule? FindMatchingRule(Order order)
{
    foreach (var rule in _rules)
    {
        if (
            order.Instrument.FullName == rule.Instrument
            && order.Account.Name == rule.MasterAccount?.Name
        )
            return rule;
    }
    return null;
}
```

After (target):
```csharp
private CopyRule? FindMatchingRule(Order order)
{
    foreach (var rule in _rules)
    {
        if (
            order.Instrument.FullName == rule.Instrument
            && order.Account.Name == rule.MasterAccount?.Name
        )
            return rule;
    }
    return default;
}
```

**Signature change**: NONE. Return type `CopyRule?` is unchanged. Body edit only.
**CYC**: 3 -> 3 (no change)

---

### Method 2: CaptureLinkedTargetPrice

**Location**: `src/PropTraderTools/CopyEngine.cs` ~L3027
**Grep target**: `private double? CaptureLinkedTargetPrice(Account acc, string stopName)`

Before:
```csharp
private double? CaptureLinkedTargetPrice(Account acc, string stopName)
{
    if (!TryParseStopSuffix(stopName, out string suffix)) // (1)
        return null;
    ...
}
```

After:
```csharp
private double? CaptureLinkedTargetPrice(Account acc, string stopName)
{
    if (!TryParseStopSuffix(stopName, out string suffix)) // (1)
        return default;
    ...
}
```

**Signature change**: NONE. Return type `double?` is unchanged. One body edit only (line ~L3030).
**CYC**: 5 -> 5 (no change)

---

### Method 3: FindFollowerRuleForOrder

**Location**: `src/PropTraderTools/CopyEngine.cs` ~L4425
**Grep target**: `private CopyRule? FindFollowerRuleForOrder(Order cancelledOrder, out int followerIndex)`

Before:
```csharp
private CopyRule? FindFollowerRuleForOrder(Order cancelledOrder, out int followerIndex)
{
    followerIndex = -1;
    foreach (var rule in _rules) // (1)
    {
        if (rule.Instrument != cancelledOrder.Instrument.FullName)
            continue; // (2)
        int idx = FindFollowerSlotIndex(rule, cancelledOrder.Account.Name); // (3)
        if (idx >= 0) // (4)
        {
            followerIndex = idx;
            return rule;
        }
    }
    return null;
}
```

After:
```csharp
private CopyRule? FindFollowerRuleForOrder(Order cancelledOrder, out int followerIndex)
{
    followerIndex = -1;
    foreach (var rule in _rules) // (1)
    {
        if (rule.Instrument != cancelledOrder.Instrument.FullName)
            continue; // (2)
        int idx = FindFollowerSlotIndex(rule, cancelledOrder.Account.Name); // (3)
        if (idx >= 0) // (4)
        {
            followerIndex = idx;
            return rule;
        }
    }
    return default;
}
```

**Signature change**: NONE. Return type `CopyRule?` is unchanged. One body edit only (line ~L4439).
**CYC**: 5 -> 5 (no change)

---

### Method 4: FindRule (two `return null` sites)

**Location**: `src/PropTraderTools/CopyEngine.cs` ~L5987
**Grep target**: `internal CopyRule? FindRule(Instrument instrument)`

Before:
```csharp
internal CopyRule? FindRule(Instrument instrument)
{
    if (instrument == null)
        return null; // Change 8: null guard
    foreach (var rule in _rules)
    {
        if (rule.Instrument == instrument.FullName)
            return rule;
    }
    return null;
}
```

After:
```csharp
internal CopyRule? FindRule(Instrument instrument)
{
    if (instrument == null)
        return default; // Change 8: null guard
    foreach (var rule in _rules)
    {
        if (rule.Instrument == instrument.FullName)
            return rule;
    }
    return default;
}
```

**Signature change**: NONE. Return type `CopyRule?` is unchanged. Two body edits (lines ~L5990, ~L5996).
**CYC**: 3 -> 3 (no change)

---

## Callers (T1 -- no caller code changes required)

| Method | Caller Location | Caller Code | Null-Guard Status | Action |
|--------|----------------|-------------|-------------------|--------|
| FindMatchingRule | CopyEngine.cs ~L1522 | `CopyRule? matchedRule = FindMatchingRule(order); if (matchedRule == null)` | HasValue pattern -- already safe | None |
| CaptureLinkedTargetPrice | CopyEngine.cs ~L2838/L2842 | `double? capturedTargetPrice = CaptureLinkedTargetPrice(...); if (capturedTargetPrice.HasValue)` | HasValue pattern -- already safe | None |
| FindFollowerRuleForOrder | CopyEngine.cs ~L4390/L4391 | `var matchedRule = FindFollowerRuleForOrder(...); if (!matchedRule.HasValue \|\| followerIndex < 0) return;` | HasValue pattern -- already safe | None |
| FindRule | CopyEngine.cs multiple | Struct-nullable callers use `== null` or `.HasValue` patterns | Already safe for both `null` and `default` | None |

**`return default` for `Nullable<struct>` is semantically identical to `return null`.**
HasValue will be false in both cases. No caller behavior changes.

---

## Implementation Steps

1. Open `src/PropTraderTools/CopyEngine.cs`.
2. Grep for `private CopyRule? FindMatchingRule` to locate the method. Find the line `return null;` at the end of the method body (no other `return null` in this method). Change it to `return default;`.
3. Grep for `private double? CaptureLinkedTargetPrice` to locate the method. Find the line `return null;` inside the `if (!TryParseStopSuffix(...))` guard (first `return` in the method body). Change it to `return default;`. Do NOT touch the `return PickBestTargetPrice(...)` line.
4. Grep for `private CopyRule? FindFollowerRuleForOrder` to locate the method. Find the line `return null;` at the end of the method body (after the foreach). Change it to `return default;`.
5. Grep for `internal CopyRule? FindRule` to locate the method. There are TWO `return null;` lines in this method:
   - First: inside `if (instrument == null)` guard -- change to `return default;` (preserve the `// Change 8: null guard` inline comment verbatim).
   - Second: at the end of the method body after the foreach -- change to `return default;`.
6. Save the file. Do NOT edit any other code.

---

## CYC Budget Confirmation

| Method | Pre-fix CYC | Post-fix CYC | Delta | <= 8? |
|--------|------------|-------------|-------|-------|
| FindMatchingRule | 3 | 3 | 0 | YES |
| CaptureLinkedTargetPrice | 5 | 5 | 0 | YES |
| FindFollowerRuleForOrder | 5 | 5 | 0 | YES |
| FindRule | 3 | 3 | 0 | YES |

**All methods CYC <= 8. JS-013 satisfied.**

---

## 7-Scan Checklist (T1 -- embedded engineer contract)

```
SCAN-1: 0 lock() in changed files
  Verify: grep "lock(" src/PropTraderTools/CopyEngine.cs
  PASS criterion: 0 matches in the changed method bodies.
  Pre-assessment: PASS -- cosmetic body edits only; no lock() added or touched.

SCAN-2: 0 non-ASCII in changed files
  Verify: inspect all new text introduced by this ticket.
  PASS criterion: Every character in changed lines is ASCII (code point <= 127).
  Pre-assessment: PASS -- only `return default;` text substitutions; all ASCII.

SCAN-3: 0 CS error lines
  Verify: check diff for CS-prefixed error annotations.
  PASS criterion: no CS-prefixed error annotations in diff output.
  Pre-assessment: PASS -- `return default` compiles identically to `return null` for
  Nullable<struct> return types. LangVersion 9.0, NRT disabled.

SCAN-4: dotnet build 0 Error(s)
  Command: dotnet build src/PropTraderTools/
  PASS criterion: output line "0 Error(s)".
  Pre-assessment: PASS -- `return default` is syntactically and semantically valid for
  `CopyRule?` and `double?` return types. No signature changes. No caller changes.

SCAN-5: dotnet test counts (passed >= 19, no new regressions)
  Command: dotnet test
  PASS criterion: passed count >= 19, failed count <= 450, no test that passed at
  baseline now fails.
  Pre-assessment: PASS -- zero behavior change. `return default` == HasValue=false for
  Nullable<struct>. All existing tests pass unchanged.

SCAN-6: powershell -File .\deploy-sync.ps1 -> SYNC COMPLETE
  Command: powershell -File .\deploy-sync.ps1
  PASS criterion: output contains "SYNC COMPLETE".
  Pre-assessment: PASS (expected) -- CopyEngine.cs modified; deploy-sync.ps1 re-syncs
  NinjaTrader hard links.

SCAN-7: hardlink count = 1 for CopyEngine.cs
  Command: (Get-Item src/PropTraderTools/CopyEngine.cs).LinkCount
  PASS criterion: result equals 1.
  Pre-assessment: PASS (expected) -- no hard link manipulation performed by this ticket.
```

---

## xUnit [Fact] Test Names (T1)

These tests verify the methods return the correct struct-nullable non-value state.
Tests live in `src/PropTraderTools/CopyEngineTests.cs` or the appropriate test file.
All assertions use `.HasValue` pattern (not `== null`) to correctly test Nullable<struct>.

| Test Name | Assert | Description |
|-----------|--------|-------------|
| `FindMatchingRule_NoMatch_ReturnsDefaultNotNull` | `Assert.False(result.HasValue)` | No rule matches -> result has no value |
| `CaptureLinkedTargetPrice_InvalidSuffix_ReturnsDefault` | `Assert.False(result.HasValue)` | Bad suffix -> early return default |
| `FindFollowerRuleForOrder_NoMatch_ReturnsDefault` | `Assert.False(result.HasValue)` | No rule match -> default (followerIndex = -1) |
| `FindRule_NullInstrument_ReturnsDefault` | `Assert.False(result.HasValue)` | Null instrument -> default from null guard |
| `FindRule_NoMatch_ReturnsDefault` | `Assert.False(result.HasValue)` | Valid instrument, no matching rule -> default from not-found exit path |

---

## Success Criteria (T1)

1. Exactly 5 text substitutions: `return null` -> `return default` in the 4 methods listed.
2. No other lines in `CopyEngine.cs` changed.
3. dotnet build: 0 Error(s).
4. dotnet test: >= 19 passed, 0 new failures.
5. deploy-sync.ps1: SYNC COMPLETE.
6. CopyEngine.cs LinkCount == 1.

---

---

# T2 -- Reference-Type Annotation: T -> T? plus propagation

## Ticket Metadata

| Field | Value |
|-------|-------|
| Ticket ID | T2 |
| Title | Reference-Type Annotation: annotate non-nullable return types as T? and propagate to callers |
| Epic | PTT-REPAIRS-08-JS002 |
| File | `src/PropTraderTools/CopyEngine.cs` |
| Prerequisite | T1 complete (SCAN-1 through SCAN-7 all PASS) |
| Successor | T3 |

## Spec Requirement IDs Satisfied

- REQ-JS002: Reference-type methods returning null must declare nullable return type T?
- REQ-NT8-CONTRACT: ResolveNullFollowerSlot preserves runtime null behavior; annotate return type only; both `return null` statements preserved verbatim with NT8-pattern inline comments
- REQ-CALLER-PROPAGATION: All callers of changed methods updated within CopyEngine.cs; no external file edits required
- REQ-CYC-BUDGET: All changed methods <= 8 branches (annotation-only; no new branches)
- REQ-BUILD: dotnet build 0 Error(s) after changes
- REQ-TEST: dotnet test >= 19 passed, no new regressions

## JS Rule Constraints

| Rule | Constraint | Status After Fix |
|------|------------|-----------------|
| JS-002 | No `return null` for non-nullable return type | SATISFIED -- all reference-type return types annotated T? |
| JS-021 | No lock() | SATISFIED -- annotation-only; no lock() added |
| JS-001 | No new throw | SATISFIED -- no throw keyword added |
| JS-013 | CYC <= 8 per method | SATISFIED -- annotation changes do not add branches |

## Scope Lock

**Engineer must implement ONLY T2 changes (7 return-type annotations + 1 variable annotation + 2 parameter annotations).**
Do NOT edit logic, comments unrelated to the change, or formatting.
Do NOT implement T3 work in this ticket.
Do NOT edit TradeCopierPanel.cs or PttBreakEvenSwap.cs -- external callers require no code changes.

---

## Methods Changed (Before and After)

### Method 1: FindBePosition

**Location**: `src/PropTraderTools/CopyEngine.cs` ~L1260
**Grep target**: `internal NinjaTrader.Cbi.Position FindBePosition(`

Before:
```csharp
internal NinjaTrader.Cbi.Position FindBePosition(
    Account acc,
    NinjaTrader.Cbi.Instrument instr
)
```

After:
```csharp
internal NinjaTrader.Cbi.Position? FindBePosition(
    Account acc,
    NinjaTrader.Cbi.Instrument instr
)
```

**Change**: Add `?` after `Position` on the return type declaration. Body unchanged.
**CYC**: 3 -> 3 (no change)

---

### Method 2: FindLeaderCollateralOrder (return type)

**Location**: `src/PropTraderTools/CopyEngine.cs` ~L3132
**Grep target**: `private static Order FindLeaderCollateralOrder(Order leaderOrder, string suffix)`

Before:
```csharp
private static Order FindLeaderCollateralOrder(Order leaderOrder, string suffix)
```

After:
```csharp
private static Order? FindLeaderCollateralOrder(Order leaderOrder, string suffix)
```

**Change**: Add `?` after `Order` on the return type. Body unchanged (both `return null` preserved).
**CYC**: 3 -> 3 (no change)

---

### Method 2a: FindLeaderCollateralOrder caller variable annotation

**Location**: `src/PropTraderTools/CopyEngine.cs` ~L3301
**Grep target**: `Order leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);`

Before:
```csharp
Order leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
```

After:
```csharp
Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
```

**Change**: Add `?` to the local variable type. No logic change. The downstream call to
`ResubmitOneCollateralLeg(acc, fo, newPrice, ..., s, leaderLeg)` is unchanged.
`ResubmitOneCollateralLeg` internally null-guards `leaderLeg` with
`leaderLeg != null ? leaderLeg.Quantity : fo.Quantity` -- no edit required there.

---

### Method 3: ResolveNullFollowerSlot

**Location**: `src/PropTraderTools/CopyEngine.cs` ~L5950
**Grep target**: `private Account ResolveNullFollowerSlot(CopyRule rule, int i)`

Before:
```csharp
private Account ResolveNullFollowerSlot(CopyRule rule, int i)
```

After:
```csharp
private Account? ResolveNullFollowerSlot(CopyRule rule, int i)
```

**Change**: Add `?` after `Account` on the return type declaration. Body MUST NOT change.
Both `return null` lines in the body MUST be preserved verbatim:
```csharp
return null; // NT8 pattern: null = slot could not be resolved
```
Do NOT change these `return null` lines to `return default`. The NT8 runtime contract
depends on returning null to signal "slot could not be resolved" and the plan explicitly
prohibits changing behavior at these sites.
**CYC**: 3 -> 3 (no change)

---

### Method 4: FindPosition (return type)

**Location**: `src/PropTraderTools/CopyEngine.cs` ~L6075
**Grep target**: `private Position FindPosition(Account acc, Instrument instrument)`

Before:
```csharp
private Position FindPosition(Account acc, Instrument instrument)
```

After:
```csharp
private Position? FindPosition(Account acc, Instrument instrument)
```

**Change**: Add `?` after `Position` on the return type. Body unchanged.
**CYC**: 2 -> 2 (no change)

---

### Method 5: IsFlat (parameter annotation -- propagation)

**Location**: `src/PropTraderTools/CopyEngine.cs` ~L6008
**Grep target**: `private static bool IsFlat(NinjaTrader.Cbi.Position pos)`

Before:
```csharp
private static bool IsFlat(NinjaTrader.Cbi.Position pos)
```

After:
```csharp
private static bool IsFlat(NinjaTrader.Cbi.Position? pos)
```

**Change**: Add `?` after `Position` on the parameter type. Body unchanged.
`IsFlat` already implements `return pos == null || pos.Quantity == 0;` -- the null check is
already present. This annotation makes the parameter type match the reality that null is passed.
**CYC**: 1 -> 1 (no change)

---

### Method 6: SubmitMarketFlattenOrder (parameter annotation -- propagation)

**Location**: `src/PropTraderTools/CopyEngine.cs` ~L5501
**Grep target**: `private void SubmitMarketFlattenOrder(Account acc, Instrument instrument, Position pos)`

Before:
```csharp
private void SubmitMarketFlattenOrder(Account acc, Instrument instrument, Position pos)
```

After:
```csharp
private void SubmitMarketFlattenOrder(Account acc, Instrument instrument, Position? pos)
```

**Change**: Add `?` after `Position` on the `pos` parameter type. Body unchanged.
`SubmitMarketFlattenOrder` already begins with `if (pos == null || pos.Quantity == 0)` guard.
**CYC**: 3 -> 3 (no change)

---

### Method 7: FindPositionPublic (return type)

**Location**: `src/PropTraderTools/CopyEngine.cs` ~L6086
**Grep target**: `internal Position FindPositionPublic(`

Before:
```csharp
internal Position FindPositionPublic(Account acc, Instrument instrument) =>
    FindPosition(acc, instrument);
```

After:
```csharp
internal Position? FindPositionPublic(Account acc, Instrument instrument) =>
    FindPosition(acc, instrument);
```

**Change**: Add `?` after `Position` on the return type. Body unchanged (expression-body delegate).
**CYC**: 1 -> 1 (no change)

---

## Callers (T2 -- complete list)

### FindBePosition callers

| Location | Code | Null-Guard | Action Required |
|----------|------|------------|-----------------|
| CopyEngine.cs ~L1248 | `var pos = FindBePosition(acc, instr); if (pos == null \|\| pos.Quantity == 0) return;` | Explicit null check | None -- already null-guards |

### FindLeaderCollateralOrder callers

| Location | Code | Null-Guard | Action Required |
|----------|------|------------|-----------------|
| CopyEngine.cs ~L3301 | `Order leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);` | Downstream: `leaderLeg != null ? ... : fo.Quantity` | **REQUIRED**: change `Order leaderLeg` to `Order? leaderLeg` (included in Method 2a above) |

### ResolveNullFollowerSlot callers

| Location | Code | Null-Guard | Action Required |
|----------|------|------------|-----------------|
| CopyEngine.cs ~L5938 | `var resolved = ResolveNullFollowerSlot(rule.Value, i); if (resolved != null) yield return resolved;` | Explicit null check | None -- already null-guards |

### FindPosition callers (all in CopyEngine.cs)

| Pattern | Locations | Null-Guard | Action Required |
|---------|-----------|------------|-----------------|
| `IsFlat(FindPosition(...))` | ~L1689, ~L1765, ~L1843, ~L1954, ~L2614, ~L4634, ~L6321, ~L6483, ~L6491, ~L6628, ~L6693, ~L6860, ~L6954 | IsFlat(null) returns true -- safe | None -- IsFlat param annotation (Method 5) is the propagation |
| Explicit null check | ~L2281: `if (pos == null \|\| pos.Quantity == 0) return;` | Explicit | None |
| Explicit null check | ~L4778: `if (pos == null) return false;` | Explicit | None |
| Explicit null check | ~L5287: `if (pos == null \|\| pos.Quantity == 0)` | Explicit | None |
| SubmitMarketFlattenOrder pass | ~L5397: `var posAfterCancel = FindPosition(...); SubmitMarketFlattenOrder(acc, instrument, posAfterCancel);` | SubmitMarketFlattenOrder null-guards | None -- param annotation (Method 6) is the propagation |
| Explicit null check | ~L5417, ~L5687, ~L5753: explicit null checks | Explicit | None |
| IsFlat guard then dereference | ~L6320, ~L6627, ~L6692, ~L6859, ~L6953 | IsFlat guard before dereference | None |

### IsFlat callers

All callers pass the result of `FindPosition()` or an `var pos` that is already nullable-compatible. After the parameter annotation to `Position?`, all call sites compile without code changes.

### SubmitMarketFlattenOrder callers

All callers pass `FindPosition()` result (now typed `Position?`) or a value already null-compatible. Parameter annotation to `Position?` matches the reality already in use.

### FindPositionPublic callers (external -- no code changes required)

| File | Location | Code | Null-Guard | Action Required |
|------|----------|------|------------|-----------------|
| TradeCopierPanel.cs | ~L1566 | `if (pos == null) return false;` | Explicit | None -- uses `var pos` inference; NRT disabled |
| TradeCopierPanel.cs | ~L2037 | `if (pos == null \|\| pos.Quantity == 0) return;` | Explicit | None -- uses `var pos` inference; NRT disabled |
| PttBreakEvenSwap.cs | ~L77 | `if (pos == null \|\| pos.Quantity == 0) return;` | Explicit | None -- uses `var pos` inference; NRT disabled |

With NRT disabled (no `<Nullable>enable</Nullable>`), changing `FindPositionPublic` return type from
`Position` to `Position?` introduces NO compiler error or warning in the calling files.
The `var pos = ...` variable at each call site will be inferred as `Position?` silently.
**Do NOT edit TradeCopierPanel.cs or PttBreakEvenSwap.cs.**

---

## Implementation Steps

1. Open `src/PropTraderTools/CopyEngine.cs`.
2. **FindBePosition** (~L1260): Grep for `internal NinjaTrader.Cbi.Position FindBePosition(`. On the return type `NinjaTrader.Cbi.Position`, add `?` to make it `NinjaTrader.Cbi.Position?`. The declaration spans lines 1260-1263 (multi-line). Edit only the return type token.
3. **FindLeaderCollateralOrder return type** (~L3132): Grep for `private static Order FindLeaderCollateralOrder(`. Change `Order` (return type only) to `Order?`. Do NOT change the `Order leaderOrder` parameter.
4. **FindLeaderCollateralOrder caller** (~L3301): Grep for `Order leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);`. Change `Order leaderLeg` to `Order? leaderLeg`. Do not touch the rest of the line.
5. **ResolveNullFollowerSlot** (~L5950): Grep for `private Account ResolveNullFollowerSlot(`. Change `Account` (return type) to `Account?`. Do NOT change the body. Both `return null; // NT8 pattern: null = slot could not be resolved` lines must remain exactly as they are.
6. **FindPosition** (~L6075): Grep for `private Position FindPosition(`. Change `Position` (return type) to `Position?`. Body unchanged.
7. **IsFlat parameter** (~L6008): Grep for `private static bool IsFlat(NinjaTrader.Cbi.Position pos)`. Change `NinjaTrader.Cbi.Position pos` to `NinjaTrader.Cbi.Position? pos`. Do NOT change the method body.
8. **SubmitMarketFlattenOrder parameter** (~L5501): Grep for `private void SubmitMarketFlattenOrder(Account acc, Instrument instrument, Position pos)`. Change the third parameter `Position pos` to `Position? pos`. Do NOT change the method body.
9. **FindPositionPublic** (~L6086): Grep for `internal Position FindPositionPublic(`. Change `Position` (return type) to `Position?`. Body (expression delegate) unchanged.
10. Save the file. Do NOT edit any other code. Do NOT edit TradeCopierPanel.cs or PttBreakEvenSwap.cs.

---

## CYC Budget Confirmation

| Method | Pre-fix CYC | Post-fix CYC | Delta | <= 8? |
|--------|------------|-------------|-------|-------|
| FindBePosition | 3 | 3 | 0 | YES |
| FindLeaderCollateralOrder | 3 | 3 | 0 | YES |
| ResolveNullFollowerSlot | 3 | 3 | 0 | YES |
| FindPosition | 2 | 2 | 0 | YES |
| IsFlat | 1 | 1 | 0 | YES |
| SubmitMarketFlattenOrder | 3 | 3 | 0 | YES |
| FindPositionPublic | 1 | 1 | 0 | YES |

**All methods CYC <= 8. JS-013 satisfied.**

---

## 7-Scan Checklist (T2 -- embedded engineer contract)

```
SCAN-1: 0 lock() in changed files
  Verify: grep "lock(" src/PropTraderTools/CopyEngine.cs
  PASS criterion: 0 matches in changed method bodies/signatures.
  Pre-assessment: PASS -- annotation-only changes; no lock() added or touched.

SCAN-2: 0 non-ASCII in changed files
  Verify: inspect all changed lines for non-ASCII characters.
  PASS criterion: every character on changed lines is ASCII (code point <= 127).
  Pre-assessment: PASS -- only `?` additions and one `Order?` variable annotation;
  all ASCII. NT8-pattern inline comments preserved verbatim (ASCII-only).

SCAN-3: 0 CS error lines
  Verify: check diff for CS-prefixed error annotations.
  PASS criterion: no CS-prefixed error annotations.
  Pre-assessment: PASS -- NRT disabled (no <Nullable>enable); `T?` on reference types
  is purely informational under C# 9.0 without NRT enabled. No CS8600/CS8603/CS8604
  enforcement. No new errors introduced.

SCAN-4: dotnet build 0 Error(s)
  Command: dotnet build src/PropTraderTools/
  PASS criterion: output line "0 Error(s)".
  Pre-assessment: PASS -- all `T?` annotations are informational under NRT-off.
  `Order? leaderLeg` variable at L3301 compiles cleanly. External callers use var
  inference and require no edits. NRT disabled; no new warnings expected.

SCAN-5: dotnet test counts (passed >= 19, no new regressions)
  Command: dotnet test
  PASS criterion: passed count >= 19, failed count <= 450, no test that passed at
  baseline now fails.
  Pre-assessment: PASS -- annotation-only; runtime behavior unchanged. ResolveNullFollowerSlot
  `return null` paths preserved verbatim (NT8 pattern). IsFlat null-guard body unchanged.
  SubmitMarketFlattenOrder null-guard body unchanged.

SCAN-6: powershell -File .\deploy-sync.ps1 -> SYNC COMPLETE
  Command: powershell -File .\deploy-sync.ps1
  PASS criterion: output contains "SYNC COMPLETE".
  Pre-assessment: PASS (expected) -- CopyEngine.cs modified; deploy-sync.ps1 re-syncs
  NinjaTrader hard links.

SCAN-7: hardlink count = 1 for CopyEngine.cs
  Command: (Get-Item src/PropTraderTools/CopyEngine.cs).LinkCount
  PASS criterion: result equals 1.
  Pre-assessment: PASS (expected) -- no hard link manipulation by this ticket.
```

**Additional NT8-contract verification (required for T2):**
After completing T2, confirm both lines in ResolveNullFollowerSlot remain unchanged:
- Line at ~L5955: must read exactly `return null; // NT8 pattern: null = slot could not be resolved`
- Line at ~L5977: must read exactly `return null; // NT8 pattern: null = slot could not be resolved`
If either was changed to `return default`, the ticket is not complete. Revert those lines.

---

## xUnit [Fact] Test Names (T2)

These tests verify the methods return null (not throw) when no match is found.

| Test Name | Assert | Description |
|-----------|--------|-------------|
| `FindBePosition_NoMatch_ReturnsNull` | `Assert.Null(result)` | No position for instrument -> null |
| `FindLeaderCollateralOrder_NullAccount_ReturnsNull` | `Assert.Null(result)` | Null account on leaderOrder -> null |
| `FindPosition_NoMatch_ReturnsNull` | `Assert.Null(result)` | No position in account -> null |
| `IsFlat_NullPosition_ReturnsTrue` | `Assert.True(result)` | IsFlat(null) must return true |
| `FindPositionPublic_NoMatch_ReturnsNull` | `Assert.Null(result)` | No position match -> returns null (delegates to FindPosition which returns null) |

---

## Success Criteria (T2)

1. Exactly 7 return-type annotations (add `?`) on method declarations.
2. Exactly 1 variable annotation: `Order? leaderLeg` at ~L3301.
3. Exactly 2 parameter annotations: `Position?` in IsFlat and SubmitMarketFlattenOrder.
4. Both `return null; // NT8 pattern: null = slot could not be resolved` lines in ResolveNullFollowerSlot unchanged.
5. No edits to TradeCopierPanel.cs or PttBreakEvenSwap.cs.
6. dotnet build: 0 Error(s).
7. dotnet test: >= 19 passed, 0 new failures.
8. deploy-sync.ps1: SYNC COMPLETE.
9. CopyEngine.cs LinkCount == 1.

---

---

# T3 -- Array Return-Type Annotation

## Ticket Metadata

| Field | Value |
|-------|-------|
| Ticket ID | T3 |
| Title | Array Return-Type Annotation: `int[]` -> `int[]?` in ResolveMultipliers plus caller annotation |
| Epic | PTT-REPAIRS-08-JS002 |
| File | `src/PropTraderTools/CopyEngine.cs` |
| Prerequisite | T2 complete (SCAN-1 through SCAN-7 all PASS) |
| Successor | None (final ticket in epic) |

## Spec Requirement IDs Satisfied

- REQ-JS002: Reference-type method returning null must declare nullable return type `int[]?`
- REQ-CALLER-PROPAGATION: One caller at L7295 updated within CopyEngine.cs
- REQ-CYC-BUDGET: Changed method CYC = 2 (unchanged)
- REQ-BUILD: dotnet build 0 Error(s) after changes
- REQ-TEST: dotnet test >= 19 passed, no new regressions

## JS Rule Constraints

| Rule | Constraint | Status After Fix |
|------|------------|-----------------|
| JS-002 | No `return null` for non-nullable array return | SATISFIED -- `int[]?` annotation declares nullable contract |
| JS-021 | No lock() | SATISFIED -- no lock() added |
| JS-001 | No new throw | SATISFIED -- no throw added |
| JS-013 | CYC <= 8 | SATISFIED -- annotation change only; CYC stays at 2 |

## Scope Lock

**Engineer must implement ONLY T3 changes: 1 return-type annotation and 1 variable annotation.**
Do NOT change the `return null;` in the method body to anything else.
Do NOT change `return dto.FollowerMultipliers;`.
Do NOT change CopyRule.Create or any downstream caller.
This is the final ticket. After T3 SCAN-7 passes, the epic is complete.

---

## Methods Changed (Before and After)

### Method 1: ResolveMultipliers (return type)

**Location**: `src/PropTraderTools/CopyEngine.cs` ~L7352
**Grep target**: `internal static int[] ResolveMultipliers(CopyRuleDto dto)`

Before:
```csharp
internal static int[] ResolveMultipliers(CopyRuleDto dto)
{
    if (dto.FollowerMultipliers == null || dto.FollowerMultipliers.Length == 0) // (1)(2)
        return null;
    return dto.FollowerMultipliers;
}
```

After:
```csharp
internal static int[]? ResolveMultipliers(CopyRuleDto dto)
{
    if (dto.FollowerMultipliers == null || dto.FollowerMultipliers.Length == 0) // (1)(2)
        return null;
    return dto.FollowerMultipliers;
}
```

**Change**: Add `?` after `int[]` on the return type declaration only. Body unchanged.
`return null;` in the body is kept as-is. Do NOT change to `return Array.Empty<int>()`.
The CopyRule.Create contract distinguishes null (use all-1s default multipliers) from empty
array (use 0 multipliers). Converting to empty array would silently change behavior.
**CYC**: 2 -> 2 (no change)

---

### Method 1a: DtoToRule caller variable annotation

**Location**: `src/PropTraderTools/CopyEngine.cs` ~L7295
**Grep target**: `int[] multipliers = ResolveMultipliers(dto);`

Before:
```csharp
int[] multipliers = ResolveMultipliers(dto);
```

After:
```csharp
int[]? multipliers = ResolveMultipliers(dto);
```

**Change**: Add `?` after `int[]` on the local variable type. No logic change.
The `multipliers` variable is passed to `CopyRule.Create(multipliers: multipliers)`.
`CopyRule.Create` declares `int[] multipliers = null` as default-null parameter.
With NRT disabled, passing `int[]?` to `int[]` parameter compiles without error.
No change to `CopyRule.Create` or any downstream code.

---

## Callers (T3 -- complete list)

| Method | Caller Location | Code | Null-Guard / Handling | Action Required |
|--------|----------------|------|-----------------------|-----------------|
| ResolveMultipliers | CopyEngine.cs ~L7295 (in DtoToRule) | `int[] multipliers = ResolveMultipliers(dto);` | Passed to `CopyRule.Create(multipliers: multipliers)` which accepts null | **REQUIRED**: change `int[] multipliers` to `int[]? multipliers` |

**No other callers of ResolveMultipliers exist in the codebase.**

---

## Implementation Steps

1. Open `src/PropTraderTools/CopyEngine.cs`.
2. **ResolveMultipliers return type** (~L7352): Grep for `internal static int[] ResolveMultipliers(CopyRuleDto dto)`. Change `int[]` (return type only) to `int[]?`. Do NOT change the method body.
3. **DtoToRule caller** (~L7295): Grep for `int[] multipliers = ResolveMultipliers(dto);`. Change `int[] multipliers` to `int[]? multipliers`. Do not touch the rest of the line or any surrounding code.
4. Save the file. Do NOT edit any other code.

---

## CYC Budget Confirmation

| Method | Pre-fix CYC | Post-fix CYC | Delta | <= 8? |
|--------|------------|-------------|-------|-------|
| ResolveMultipliers | 2 | 2 | 0 | YES |

**All methods CYC <= 8. JS-013 satisfied.**

---

## 7-Scan Checklist (T3 -- embedded engineer contract)

```
SCAN-1: 0 lock() in changed files
  Verify: grep "lock(" src/PropTraderTools/CopyEngine.cs
  PASS criterion: 0 matches in changed method bodies/signatures.
  Pre-assessment: PASS -- annotation-only change; no lock() added or touched.

SCAN-2: 0 non-ASCII in changed files
  Verify: inspect all changed lines for non-ASCII characters.
  PASS criterion: every character on changed lines is ASCII (code point <= 127).
  Pre-assessment: PASS -- only `int[]?` return-type annotation and `int[]?` variable
  annotation; all ASCII.

SCAN-3: 0 CS error lines
  Verify: check diff for CS-prefixed error annotations.
  PASS criterion: no CS-prefixed error annotations.
  Pre-assessment: PASS -- NRT disabled; `int[]?` annotation is informational.
  `return null` for `int[]?` return type compiles identically under NRT-off.
  `int[]? multipliers =` at L7295 compiles cleanly. No new CS errors.

SCAN-4: dotnet build 0 Error(s)
  Command: dotnet build src/PropTraderTools/
  PASS criterion: output line "0 Error(s)".
  Pre-assessment: PASS -- 2 annotation changes (return type + variable), zero logic delta.
  NRT disabled; no new compiler enforcement. CopyRule.Create accepts null int[] parameter
  as-is (declared `int[] multipliers = null`).

SCAN-5: dotnet test counts (passed >= 19, no new regressions)
  Command: dotnet test
  PASS criterion: passed count >= 19, failed count <= 450, no test that passed at
  baseline now fails.
  Pre-assessment: PASS -- zero behavior change; `return null` preserved in body.
  ResolveMultipliers returns the same null value. CopyRule.Create behavior unchanged.

SCAN-6: powershell -File .\deploy-sync.ps1 -> SYNC COMPLETE
  Command: powershell -File .\deploy-sync.ps1
  PASS criterion: output contains "SYNC COMPLETE".
  Pre-assessment: PASS (expected) -- CopyEngine.cs modified; deploy-sync.ps1 re-syncs
  NinjaTrader hard links.

SCAN-7: hardlink count = 1 for CopyEngine.cs
  Command: (Get-Item src/PropTraderTools/CopyEngine.cs).LinkCount
  PASS criterion: result equals 1.
  Pre-assessment: PASS (expected) -- no hard link manipulation by this ticket.
```

---

## xUnit [Fact] Test Names (T3)

These tests verify the ResolveMultipliers contract: null and empty input -> null output, valid input -> array.

| Test Name | Assert | Description |
|-----------|--------|-------------|
| `ResolveMultipliers_NullDto_ReturnsNull` | `Assert.Null(result)` | Null FollowerMultipliers -> null (not empty array) |
| `ResolveMultipliers_EmptyMultipliers_ReturnsNull` | `Assert.Null(result)` | Empty FollowerMultipliers -> null (not empty array; CopyRule.Create uses all-1s default) |
| `ResolveMultipliers_ValidMultipliers_ReturnsArray` | `Assert.NotNull(result); Assert.Equal(N, result.Length)` | Valid non-empty multipliers -> returns the array |

---

## Success Criteria (T3)

1. Exactly 1 return-type annotation: `int[]?` on ResolveMultipliers declaration.
2. Exactly 1 variable annotation: `int[]? multipliers` at ~L7295 in DtoToRule.
3. Body of ResolveMultipliers unchanged: `return null;` inside the guard preserved as-is.
4. No edits to CopyRule.Create or any other method.
5. dotnet build: 0 Error(s).
6. dotnet test: >= 19 passed, 0 new failures.
7. deploy-sync.ps1: SYNC COMPLETE.
8. CopyEngine.cs LinkCount == 1.

---

---

## Complete Change Inventory (all tickets)

| Ticket | Method | Change | Lines (approx) |
|--------|--------|--------|----------------|
| T1 | FindMatchingRule | `return null` -> `return default` | ~L1997 |
| T1 | CaptureLinkedTargetPrice | `return null` -> `return default` | ~L3030 |
| T1 | FindFollowerRuleForOrder | `return null` -> `return default` | ~L4439 |
| T1 | FindRule (x2) | `return null` -> `return default` | ~L5990, ~L5996 |
| T2 | FindBePosition | Return type `Position` -> `Position?` | ~L1260 |
| T2 | FindLeaderCollateralOrder | Return type `Order` -> `Order?` | ~L3132 |
| T2 | FindLeaderCollateralOrder caller | `Order leaderLeg` -> `Order? leaderLeg` | ~L3301 |
| T2 | ResolveNullFollowerSlot | Return type `Account` -> `Account?` | ~L5950 |
| T2 | FindPosition | Return type `Position` -> `Position?` | ~L6075 |
| T2 | IsFlat | Parameter `Position pos` -> `Position? pos` | ~L6008 |
| T2 | SubmitMarketFlattenOrder | Parameter `Position pos` -> `Position? pos` | ~L5501 |
| T2 | FindPositionPublic | Return type `Position` -> `Position?` | ~L6086 |
| T3 | ResolveMultipliers | Return type `int[]` -> `int[]?` | ~L7352 |
| T3 | DtoToRule caller | `int[] multipliers` -> `int[]? multipliers` | ~L7295 |

**Total code edits: 14 surgical changes, all in `src/PropTraderTools/CopyEngine.cs`.**
**Already-compliant (no change): FindFollowerBracketOrder (Order?), FindFollowerEntryOrder (Order?), FindFollowerAccount (Account?).**

---

## No-Change Confirmation (already compliant)

| Method | Location | Status |
|--------|----------|--------|
| FindFollowerBracketOrder (IEnumerable overload) | ~L3929 | ALREADY `Order?` -- no code change needed |
| FindFollowerEntryOrder | ~L4132 | ALREADY `Order?` -- no code change needed |
| FindFollowerAccount | ~L7363 | ALREADY `Account?` -- no code change needed |

---

*ptt-architect -- PTT-REPAIRS-08-JS002 -- Phase 3 -- TICKETS_COMPLETE*
