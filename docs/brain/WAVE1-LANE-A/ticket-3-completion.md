# Ticket 3 Completion Report -- OnOrderUpdate (Advisory)

## Scope: TICKET 3 ONLY

**Epic**: WAVE1-LANE-A
**Ticket**: T3 (A-09)
**Target method**: `OnOrderUpdate` in `src/PropTraderTools/CopyEngine.cs`
**Date**: 2026-09-07
**Engineer mode**: ptt-engineer (WAVE1-LANE-A)

---

## Advisory Tier Note

`OnOrderUpdate` had CCN=8 before this extraction -- fully compliant with JS-080.
This extraction was executed per the SCOPE LOCK directive.
The extraction is an improvement (CCN reduction from 8 to 5), not a compliance requirement.

---

## What Was Implemented

### 1. Added `TryResolveEnabledRule` helper (before `OnOrderUpdate`, lines 1499-1523 post-edit)

```csharp
private bool TryResolveEnabledRule(Order order, out CopyRule rule)
```

- **Access**: `private` (JS-007 compliant -- never widened)
- **Return**: `bool` (JS-002 compliant -- never null)
- **Out param**: `out CopyRule rule` -- value type (readonly struct), never null
- **Gate order** (correctness-critical, cannot be reordered):
  1. Gate 1: `if (!_isCopyEnabled)` -- enabled check first
  2. Gate 2: `CopyRule? matchedRule = FindMatchingRule(order); if (matchedRule == null)` -- null check second
  3. Gate 3: `if (!matchedRule.Value.Enabled)` -- rule.Enabled third (requires non-null from Gate 2)
- **CCN actual**: 5 (base=1 + gate1=1 + gate2=1 + gate3=1 + return true=1)

### 2. Refactored `OnOrderUpdate` gate block

Replaced 12 lines (three-gate block) with single call:
```csharp
if (!TryResolveEnabledRule(e.Order, out CopyRule matchedRule))
    return;
```

Updated all 5 uses of `matchedRule.Value` -> `matchedRule` (no `.Value` needed on unwrapped struct):
- `TryMirrorOrderUpdate(e.Order, matchedRule)`
- `TryCancelFollowerEntries(e.Order, matchedRule)`
- `TryDispatchLeaderFlat(... matchedRule ...)`
- `TryHandleDrag(e.Order, matchedRule)`
- `DispatchCopy(e.Order, matchedRule)`

---

## Build Result

**PASS** -- zero errors, zero warnings on modified lines.

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## CCN Scan Results

| Method | CCN Before | CCN After | <= 8? |
|--------|-----------|-----------|-------|
| `OnOrderUpdate` | 8 | **5** | YES |
| `TryResolveEnabledRule` | N/A (new) | **5** | YES |

Both methods are within the JS-080 CCN <= 8 limit.

---

## 7-Scan Results

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 | `lock(` in new/modified code | **0 hits** (all `lock` matches are comments) |
| SCAN-02 | `async void` in new/modified code | **0 hits** (only a comment mentioning it) |
| SCAN-03 | `return null;` in TryResolveEnabledRule / OnOrderUpdate | **0 hits** |
| SCAN-04 | CCN <= 8 for all affected methods | **PASS** (both CCN=5) |
| SCAN-05 | CreateOrder PTT- prefix | **N/A** (no CreateOrder in these methods) |
| SCAN-06 | ASCII-only in new code | **PASS** (no non-ASCII characters in lines 1499-1523) |
| SCAN-07 | `public.*TryResolveEnabledRule` | **0 hits** (method is `private`) |

`.Value` cleanup verified:
`Select-String -Pattern "matchedRule.Value"` returns zero hits inside `OnOrderUpdate` body.
Only remaining hits are inside `TryResolveEnabledRule` (lines 1516, 1521) where `matchedRule` is
the local `CopyRule?` nullable variable -- semantically correct.

---

## Tests Added

4 [Fact] tests added to `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`:

| # | Test Name | Gate Tested |
|---|-----------|-------------|
| T43 | `TryResolveEnabledRule_ReturnsFalse_WhenCopyDisabled` | Gate 1: `!_isCopyEnabled` |
| T44 | `TryResolveEnabledRule_ReturnsFalse_WhenNoMatchingRule` | Gate 2: `matchedRule == null` |
| T45 | `TryResolveEnabledRule_ReturnsFalse_WhenRuleIsDisabled` | Gate 3: `!rule.Enabled` |
| T46 | `TryResolveEnabledRule_ReturnsTrue_WhenAllGatesPass` | All 3 gates pass |

Pattern: Inline logic-mirror (NT8 Order/Account/Instrument not constructible outside NT8 runtime).

---

## Tests Run

**166 passing / 0 failing / 3 skipped** (up from 162 before T3 -- 4 new T3 tests added)

```
Passed!  - Failed: 0, Passed: 166, Skipped: 3, Total: 169, Duration: 37 ms
```

---

## BUILD_PASS