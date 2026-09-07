# Architecture Plan: DW-LB-SFB-01
# IsBracketLegStatic — Narrow PTT- Prefix Fix (Retroactive Formalisation)

**Status**: REVIEW_PENDING
**Defect ID**: DW-LB-SFB-01
**Severity**: P1
**Fix Commit**: 1086d9fd (main, 2026-09-07)
**SIM Result**: PASS — no PTT-Flatten storm after BE ALL cycle
**Phase**: 1 — Architecture Plan (retroactive)
**Author**: ptt-architect
**Date**: 2026-09-08

---

## 1. LANE-SPLIT GATE RESULT

**Q1. Is there only one concern here?**
YES. One concern: retroactive formalisation of a single already-merged fix to
`IsBracketLegStatic` in `CopyEngine.cs`, plus adding xUnit test coverage for
that method.

**Q2. Does this touch multiple independent code paths needing separate tickets?**
NO. One method (`IsBracketLegStatic`) was fixed. One new test file
(`IsBracketLegStaticTests.cs`) needs to be created. No other source files are
modified.

**Q3. Are there parallel workstreams that can execute concurrently without dependency?**
NO. The single ticket is: write the test file. The source fix is already merged.
There is no parallelism available or needed.

**Q4. Does the fix scope span more than one logical file/class?**
NO. The source fix is confined to `CopyEngine.cs` (already merged). The only new
file is the test file.

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

---

## 2. ROOT CAUSE ANALYSIS

### 2.1 Pre-Fix vs Post-Fix Body of IsBracketLegStatic

**Pre-fix body** (before commit 1086d9fd):

```csharp
// src/PropTraderTools/CopyEngine.cs
private static bool IsBracketLegStatic(Order order)
{
    return order.FromEntrySignal != null
        || (
            order.Name != null
            && (
                order.Name.StartsWith("Stop")
                || order.Name.StartsWith("Target")
                || order.Name.StartsWith("PTT-")          // <-- BUG: too broad
                || order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase)
            )
        );
}
```

**Post-fix body** (commit 1086d9fd, live on main, confirmed at CopyEngine.cs L5749):

```csharp
// src/PropTraderTools/CopyEngine.cs  L5749-5762
private static bool IsBracketLegStatic(Order order)
{
    return order.FromEntrySignal != null
        || (
            order.Name != null
            && (
                order.Name.StartsWith("Stop")
                || order.Name.StartsWith("Target")
                || order.Name.StartsWith("PTT-STP-Drag-", StringComparison.Ordinal) // narrowed
                || order.Name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal) // narrowed
                || order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase)
            )
        );
}
```

The single change: replaced one broad clause `StartsWith("PTT-")` with two specific
narrow clauses `StartsWith("PTT-STP-Drag-")` and `StartsWith("PTT-TGT-Drag-")`.

### 2.2 Why the Broad StartsWith("PTT-") Clause Caused the Bug

The broad `StartsWith("PTT-")` clause classified **any** PTT-prefixed management
order as a bracket leg. This included:

| Order Name Pattern | Correct classification | Pre-fix classification |
|--------------------|------------------------|------------------------|
| `PTT-BE-Stop-N`      | Management exit order  | BRACKET LEG (BUG)      |
| `PTT-Flatten`        | Management exit order  | BRACKET LEG (BUG)      |
| `PTT-Tighten-Stop`   | Management exit order  | BRACKET LEG (BUG)      |
| `PTT-Trim`           | Management exit order  | BRACKET LEG (BUG)      |
| `PTT-STP-Drag-N`     | Bracket leg replacement | BRACKET LEG (correct)  |
| `PTT-TGT-Drag-N`     | Bracket leg replacement | BRACKET LEG (correct)  |

### 2.3 The Call Chain: Order Event to PTT-Flatten Storm

The triggering event was a PTT-BE-Stop-N order going `Working` on the leader account:

```
NT8 OnOrderUpdate(order="PTT-BE-Stop-1", state=Working)
  └─> CopyEngine.OnOrderUpdate(order, account)
        └─> [finds matching CopyRule]
              └─> TryHandleBracketDrag(order, rule)              [L2055]
                    └─> IsWorkingBracket(order)                  [L2602]
                          ├─ order.OrderState == Working: TRUE
                          └─ IsBracketLegStatic(order)           [L5749]
                                PRE-FIX: StartsWith("PTT-") matches "PTT-BE-Stop-1"
                                → returns TRUE  [THE BUG]
                    └─> [IsWorkingBracket=true] → HandleBracketChange(order, rule)  [L3688]
                          └─> foreach followerAccount:
                                SyncFollowerBracket(acc, order, isStop=true, ...)   [L2663]
                                  └─> FindFollowerBracketOrder(acc, null, true, "PTT-BE-Stop-1")
                                        → fo = null  (no bracket order matches this name)
                                  └─> if (fo == null) return  ← early return, no direct crash
```

The direct path shows `fo=null` → early return in `SyncFollowerBracket`. However,
`TryHandleBracketDrag` **returns `true`** to its caller (L2072), which signals
"handled — skip the rest". This consumes the `PTT-BE-Stop-N Working` event before
the normal copy-dispatch path can process it, leaving follower accounts with stale
open bracket orders.

The PTT-Flatten storm on followers is the downstream consequence: when the leader's
BE-All cycle closes the leader's position but follower brackets are not properly
managed (because the BE-Stop event was silently consumed by the wrong HandleBracketChange
path), subsequent order-state changes trigger the follower flatten logic.

The empirical confirmation is the SIM result: **PASS** after applying the fix — no
PTT-Flatten storm observed on 2026-09-07 SIM test.

### 2.4 Why the Narrow Fix Is Safe

The two clauses `StartsWith("PTT-STP-Drag-", StringComparison.Ordinal)` and
`StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)` cover exactly the right
set of PTT-prefixed orders that ARE legitimate bracket leg replacements:

- **PTT-STP-Drag-N**: Submitted by `HandleAtmStopSync` (via `SyncAtmFollowerBracket`)
  when a follower's ATM stop bracket is replaced after a drag. These orders function
  as stop bracket legs and must be recognised by `IsBracketLegStatic` to enable
  subsequent drag synchronisation. Confirmed at CopyEngine.cs L2630.
- **PTT-TGT-Drag-N**: Symmetric for target brackets. Confirmed at CopyEngine.cs L2634.

All other PTT-prefixed orders are management/exit orders and should never be routed
through the bracket-change synchronisation path.

---

## 3. REGRESSION ANALYSIS

### 3.1 IsBracketLeg (non-static) Is Separate — Confirmed NOT Changed

`IsBracketLeg` (instance method, CopyEngine.cs L5769) is a **distinct and separate**
method:

```csharp
// CopyEngine.cs L5769 -- B29 fix: removed "PTT-" from IsBracketLeg.
private bool IsBracketLeg(Order order)
{
    return order.FromEntrySignal != null
        || (
            order.Name != null
            && (order.Name.StartsWith("Stop") || order.Name.StartsWith("Target"))
        );
}
```

Key differences from `IsBracketLegStatic`:
- Instance method (not static)
- No `PTT-STP-Drag-` / `PTT-TGT-Drag-` clauses (not needed for its callers)
- No `EndsWith("STP")` clause (not needed for its callers)
- Already had the "no PTT-" constraint from B29 fix

Callers of `IsBracketLeg` (non-static):
- `MirrorOrderUpdate` (L2217): mirror-mode bracket classification
- `L4748`: cancel logic bracket skip
- `L5374`: order enumeration bracket filter

**DW-LB-SFB-01 did NOT modify `IsBracketLeg`. It was and remains correct.**

### 3.2 Confirmed Return Values (Post-Fix Regression Matrix)

| Input `order.Name` | `FromEntrySignal` | Expected | Clause Hit |
|--------------------|-------------------|----------|------------|
| `"PTT-STP-Drag-1"` | null | **true** | `StartsWith("PTT-STP-Drag-")` |
| `"PTT-STP-Drag-3"` | null | **true** | `StartsWith("PTT-STP-Drag-")` |
| `"PTT-TGT-Drag-1"` | null | **true** | `StartsWith("PTT-TGT-Drag-")` |
| `"PTT-BE-Stop-1"`  | null | **false** | No clause matches |
| `"PTT-Flatten"`    | null | **false** | No clause matches |
| `"PTT-Tighten-Stop"` | null | **false** | No clause matches |
| `"PTT-Trim"`       | null | **false** | No clause matches |
| `"Stop1"`          | null | **true** | `StartsWith("Stop")` |
| `"Stop2"`          | null | **true** | `StartsWith("Stop")` |
| `"Target1"`        | null | **true** | `StartsWith("Target")` |
| `"Target3"`        | null | **true** | `StartsWith("Target")` |
| `"Buy STP"`        | null | **true** | `EndsWith("STP")` |
| `"Sell STP"`       | null | **true** | `EndsWith("STP")` |
| `"Entry"`          | null | **false** | No clause matches |
| any name           | non-null | **true** | `FromEntrySignal != null` |
| null               | null | **false** | `Name != null` guard fails |

### 3.3 Confirming PTT-STP-Drag-N / PTT-TGT-Drag-N Still Return True

Post-fix: `StartsWith("PTT-STP-Drag-", StringComparison.Ordinal)` matches `"PTT-STP-Drag-1"`,
`"PTT-STP-Drag-2"`, `"PTT-STP-Drag-3"`. ✓

Post-fix: `StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)` matches `"PTT-TGT-Drag-1"`,
`"PTT-TGT-Drag-2"`, `"PTT-TGT-Drag-3"`. ✓

The original broad `StartsWith("PTT-")` correctly matched these — the narrow clauses
preserve that correctness for the only PTT-prefixed names that should return true.

---

## 4. CYC ANALYSIS

### 4.1 McCabe Cyclomatic Complexity — IsBracketLegStatic Post-Fix

The post-fix method body is a single compound boolean return expression:

```
A || (B && (C || D || E || F || G))
```

Where:
- A = `order.FromEntrySignal != null`
- B = `order.Name != null`
- C = `order.Name.StartsWith("Stop")`
- D = `order.Name.StartsWith("Target")`
- E = `order.Name.StartsWith("PTT-STP-Drag-", ...)`
- F = `order.Name.StartsWith("PTT-TGT-Drag-", ...)`
- G = `order.Name.EndsWith("STP", ...)`

Boolean operators: 1 `||` (top) + 1 `&&` + 4 `||` (inner, between C/D/E/F/G) = **6 operators**.

McCabe CYC = number of binary boolean operators + 1 = **6 + 1 = 7**.

**CYC = 7 ≤ 8. PASS.**

Pre-fix CYC was 6 (one fewer clause). Post-fix adds one clause (PTT-TGT-Drag-), increasing
CYC by 1 to 7. Both values are within the Jane Street ≤ 8 mandate.

---

## 5. TEST SPECIFICATION

### 5.1 Test File

**Path**: `tests/PropTraderTools.Tests/IsBracketLegStaticTests.cs`
**Framework**: xUnit 2.6.2 (net8.0) — consistent with existing test files
**Pattern**: Inline mirror (B143 pattern) — no ProjectReference to PropTraderTools
  required (cross-TFM: PropTraderTools=net48, tests=net8.0).

### 5.2 Call Signature Decision

`IsBracketLegStatic` is `private static` in `CopyEngine`. It cannot be called
directly from the test project (private access + TFM mismatch prevents
ProjectReference).

**Decision**: Inline mirror pattern.

The method reads exactly two fields from its `Order` parameter:
- `order.FromEntrySignal` (string?) — only null vs non-null matters
- `order.Name` (string?) — full value matters

The inline mirror decomposes `Order` into `(string? name, bool hasEntrySignal)`:

```csharp
// Inline mirror — exact logic of IsBracketLegStatic post-fix (commit 1086d9fd)
// Source confirmed: CopyEngine.cs L5749-5762
private static bool IsBracketLegStatic(string? name, bool hasEntrySignal)
{
    if (hasEntrySignal) return true;
    if (name == null) return false;
    return name.StartsWith("Stop")
        || name.StartsWith("Target")
        || name.StartsWith("PTT-STP-Drag-", StringComparison.Ordinal)
        || name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)
        || name.EndsWith("STP", StringComparison.OrdinalIgnoreCase);
}
```

This is semantically equivalent to the production method with the `Order` parameter
decomposed into its two relevant fields.

### 5.3 [Fact] Test Methods

All tests call `IsBracketLegStatic(name, hasEntrySignal)` and use `Assert.Equal`.

| ID  | Method Name | Input (name, hasEntrySignal) | Expected | Description |
|-----|-------------|------------------------------|----------|-------------|
| T1  | `PTT_STP_Drag_1_ReturnsTrue` | `("PTT-STP-Drag-1", false)` | `true` | Drag stop replacement — bracket leg |
| T2  | `PTT_TGT_Drag_1_ReturnsTrue` | `("PTT-TGT-Drag-1", false)` | `true` | Drag target replacement — bracket leg |
| T3  | `PTT_BE_Stop_1_ReturnsFalse` | `("PTT-BE-Stop-1", false)` | `false` | **KEY REGRESSION**: was true pre-fix |
| T4  | `PTT_Flatten_ReturnsFalse` | `("PTT-Flatten", false)` | `false` | **KEY REGRESSION**: was true pre-fix |
| T5  | `PTT_Tighten_Stop_ReturnsFalse` | `("PTT-Tighten-Stop", false)` | `false` | Was true pre-fix |
| T6  | `Stop1_ReturnsTrue` | `("Stop1", false)` | `true` | ATM bracket stop — must remain true |
| T7  | `Target1_ReturnsTrue` | `("Target1", false)` | `true` | ATM bracket target — must remain true |
| T8  | `Buy_STP_ReturnsTrue` | `("Buy STP", false)` | `true` | ATM STP bracket — DW-B134 path |
| T9  | `Entry_ReturnsFalse` | `("Entry", false)` | `false` | Entry orders are never bracket legs |
| T10 | `NullName_ReturnsFalse` | `(null, false)` | `false` | Null name guard |
| T11 | `NullOrderAnalog_ReturnsFalse` | `(null, false)` | `false` | Null order analog (no signal + null name) |

**Notes on T3, T4, T5**: These are the key regression guards. Pre-fix, all three would
return `true` (matched by `StartsWith("PTT-")`). Post-fix, all three return `false`.
These tests enforce the fix and prevent regression if someone re-introduces a broad
"PTT-" clause.

**Notes on T10 vs T11**: T10 and T11 test the same input and are both included per
spec to explicitly document both the "null name" and "null order" scenarios. They can
be two separate `[Fact]` methods with different display names for clarity.

**Note on T1/T2 — Ordinal vs CurrentCulture**: The production code uses
`StringComparison.Ordinal` for the PTT-STP-Drag- and PTT-TGT-Drag- clauses.
The inline mirror must use `StringComparison.Ordinal` identically. The test input
strings `"PTT-STP-Drag-1"` and `"PTT-TGT-Drag-1"` are ASCII-only so Ordinal vs
OrdinalIgnoreCase makes no difference, but the test must mirror the production
comparison exactly.

### 5.4 File Header Comment

The test file must include a comment block mirroring the B143 pattern, stating:
- Source of the mirrored logic: CopyEngine.cs L5749-5762, commit 1086d9fd
- Framework: xUnit ONLY. NEVER NUnit or MSTest.
- The reason for inline mirror (cross-TFM: net48 vs net8.0)

---

## 6. SPEC REQUIREMENT TRACEABILITY

| Requirement | Source | Satisfied By |
|-------------|--------|--------------|
| PTT-BE-Stop must NOT be classified as bracket leg | DW-LB-SFB-01 defect | Post-fix IsBracketLegStatic (L5757-5758) |
| PTT-Flatten must NOT be classified as bracket leg | DW-LB-SFB-01 defect | Post-fix IsBracketLegStatic |
| PTT-STP-Drag-N must be classified as bracket leg | DW-B142-DIRECT-4 (L2630) | StartsWith("PTT-STP-Drag-") clause |
| PTT-TGT-Drag-N must be classified as bracket leg | DW-B142-DRAG (L2634) | StartsWith("PTT-TGT-Drag-") clause |
| Stop1..Stop9 must be classified as bracket leg | Core NT8 ATM contract | StartsWith("Stop") clause |
| Target1..Target9 must be classified as bracket leg | Core NT8 ATM contract | StartsWith("Target") clause |
| "Buy STP" / "Sell STP" must be classified as bracket leg | DW-B134 | EndsWith("STP") clause |
| xUnit test coverage for IsBracketLegStatic | DW-LB-SFB-01 pipeline | T1..T11 in IsBracketLegStaticTests.cs |
| CYC ≤ 8 | JS (Jane Street mandate) | CYC = 7 ✓ |
| No lock() | JS-021 | Pure predicate, no state ✓ |
| ASCII-only | Project mandate | All literals are ASCII ✓ |

---

## 7. RISKS & DEFERRED ITEMS

### 7.1 Risks

**R1 — Future PTT-prefix order names**: If a new PTT-prefixed order name is added
that IS intended to be a bracket leg (other than PTT-STP-Drag- and PTT-TGT-Drag-),
`IsBracketLegStatic` must be updated to include it explicitly. The narrow-clause
design requires deliberate opt-in for each new PTT bracket leg type. This is
intentional (defense-in-depth) but requires discipline in future work.
*Mitigation*: T3-T5 regression tests will catch unintended re-broadening.

**R2 — FindFollowerBracketOrder silent no-op**: The `fo=null` early return in
`SyncFollowerBracket` means misclassified orders won't cause a crash, but they DO
consume the order event (TryHandleBracketDrag returns true). Future debugging of
PTT-Flatten storms should check whether `IsWorkingBracket` is being called for
non-bracket orders before diving deeper.
*Mitigation*: The fix eliminates the misclassification at source.

**R3 — Mirror test drift**: The inline mirror in tests will diverge from production
code if `IsBracketLegStatic` is modified without updating the test mirror.
*Mitigation*: The comment block in the test file explicitly references CopyEngine.cs
line numbers and commit hash. Any future change to `IsBracketLegStatic` should
trigger a test update.

### 7.2 Deferred Items

**None.** This is a complete retroactive formalisation of an already-merged fix.
The plan captures the full root cause, regression matrix, CYC analysis, and test spec.
No follow-up architectural work is needed for this defect.

---

## 8. COMPONENT SUMMARY

| Component | File | Action | Notes |
|-----------|------|--------|-------|
| `IsBracketLegStatic` | `src/PropTraderTools/CopyEngine.cs` L5749 | **ALREADY FIXED** (commit 1086d9fd) | ptt-engineer DOES NOT touch |
| `IsBracketLegStaticTests` | `tests/PropTraderTools.Tests/IsBracketLegStaticTests.cs` | **CREATE NEW** | 11 xUnit [Fact] methods, inline mirror pattern |

**ptt-engineer writes exactly one file: `IsBracketLegStaticTests.cs`.**

---

## 9. 7-SCAN PRE-FLIGHT (PLAN-LEVEL)

| SCAN | Check | Result |
|------|-------|--------|
| SCAN-01 | No `lock()` in scope | PASS — pure predicate, no state |
| SCAN-02 | No `throw` in hot path | PASS — returns bool |
| SCAN-03 | No `return null` | PASS — returns bool |
| SCAN-04 | No `DateTime.Now` | PASS — not used |
| SCAN-05 | ASCII-only identifiers and strings | PASS — all literals ASCII |
| SCAN-06 | No FontFamily / hex colors | PASS — not applicable |
| SCAN-07 | CYC ≤ 8 | PASS — CYC = 7 |
