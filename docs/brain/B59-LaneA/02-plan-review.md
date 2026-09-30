# B59-LaneA Plan Review — Phase 2 (Second Pass)

**Reviewer**: ptt-plan-reviewer  
**Epic**: B59-LaneA  
**Input**: `docs/brain/B59-LaneA/02-architecture-plan.md` (Ph1 revision — V-01 + V-02 applied)  
**Live code read**: `src/PropTraderTools/CopyEngine.cs` lines 716–773  
**Rules read**: `docs/standards/jane-street/RULES_CATALOG.md`  
**Date**: 2026-08-10  
**Pass**: SECOND PASS

---

## Violations from First Pass

| ID | Description | Status |
|----|-------------|--------|
| V-01 | Test insertion point said "after line 2699"; correct anchor is 2749 | **FIXED** — Plan §4 now reads "after line 2749" |
| V-02 | `deploy-sync.ps1` missing from T2 commit sequence | **FIXED** — Both T1 and T2 commit sequences include `powershell -File .\deploy-sync.ps1` |

---

## Checklist Results

### [1] IsExitSignalName is `internal static` (testable without reflection)

**PASS.** Plan §2.1 signature: `internal static bool IsExitSignalName(string name)`.  
Same testability pattern as `IsDispatchTriggerState` — directly callable from xUnit `[Fact]` without reflection.

---

### [2] IsExitSignalName CYC ≤ 8

**PASS.** CYC = 7.

| Decision point | Count |
|---|---|
| Base | 1 |
| `IsNullOrEmpty` guard | +1 |
| `== "Close"` | +1 |
| `== "Flatten"` | +1 |
| `StartsWith("Rev")` | +1 |
| `StartsWith("Exit")` | +1 |
| `StartsWith("PTT-")` | +1 |
| **Total** | **7** |

7 ≤ 8. No JS-CYC violation.

---

### [3] DispatchCopy CYC after change ≤ 8 (Gate 0.5 becomes a single call)

**PASS.** CYC = 7 (was 8).

Live code line 728 (confirmed by read):
```csharp
if (order.Name != null && order.Name.StartsWith("PTT-")) return;
```
Compound `&&` contributes 2 CYC points (`if` + short-circuit). Replacement:
```csharp
if (IsExitSignalName(order.Name)) return;
```
Single method call contributes 1 CYC point. Net change: 8 → 7. Still ≤ 8. No regression.

---

### [4] All 5 guard cases present in IsExitSignalName

**PASS.**

| Case | Present? |
|---|---|
| `PTT-` prefix | ✅ `StartsWith("PTT-", StringComparison.Ordinal)` |
| `"Close"` exact | ✅ `name == "Close"` |
| `"Flatten"` exact | ✅ `name == "Flatten"` |
| `"Rev"` prefix | ✅ `StartsWith("Rev", StringComparison.Ordinal)` |
| `"Exit"` prefix | ✅ `StartsWith("Exit", StringComparison.Ordinal)` |

---

### [5] Null input returns false (not true)

**PASS.** Plan line 96: `if (string.IsNullOrEmpty(name)) return false;`

Null → `false`. Null name does **not** block dispatch. Correct: a null name is not an exit signal;
the downstream gates (not this predicate) are responsible for null-order handling.

---

### [6] No `throw` in IsExitSignalName (JS-001)

**PASS.** Method body contains only `return true` / `return false` statements. No `throw` expression
anywhere in the new or modified code. JS-001 satisfied.

---

### [7] No `lock()` anywhere (JS-021)

**PASS.** Plan explicitly asserts no `lock` in new or modified code. Live code read of lines 716–773
confirms no `lock(` present in `DispatchCopy` or adjacent methods. JS-021 satisfied.

---

### [8] All string literals are ASCII-only

**PASS.** Five new string literals in `IsExitSignalName`: `"Close"`, `"Flatten"`, `"Rev"`, `"Exit"`,
`"PTT-"` — all pure ASCII. Test assertion message strings (plan §4) are ASCII-only. No
hardcoded hex colours, no Unicode, no curly quotes.

---

### [9] 7 test IDs match T_B59_01..T_B59_07 from mission brief

**PASS.** Plan §4 defines exactly seven `[Fact]` methods:

| ID | Method name |
|---|---|
| T_B59_01 | `IsExitSignalName_NullName_ReturnsFalse` |
| T_B59_02 | `IsExitSignalName_EmptyName_ReturnsFalse` |
| T_B59_03 | `IsExitSignalName_Close_ReturnsTrue` |
| T_B59_04 | `IsExitSignalName_Flatten_ReturnsTrue` |
| T_B59_05 | `IsExitSignalName_RevPrefix_ReturnsTrue` |
| T_B59_06 | `IsExitSignalName_ExitPrefix_ReturnsTrue` |
| T_B59_07 | `IsExitSignalName_PttPrefixBlockedAndNonMatchingPasses` |

---

### [10] Test placement is after line 2749 (before class closing brace)

**PASS.** Plan §4 "Test Placement": *"Append after `IsDispatchTriggerState_ReturnsTrueForSubmittedAndAccepted`
(after line 2749 — before the class closing brace; lines 2701–2749 contain the B55 LaneB test block
and must not be split)."* V-01 confirmed fixed.

---

### [11] deploy-sync.ps1 listed in commit steps for both T1 and T2

**PASS.** Plan §10:

- T1 commit sequence begins with `powershell -File .\deploy-sync.ps1` ✅  
- T2 commit sequence begins with `powershell -File .\deploy-sync.ps1` ✅  

V-02 confirmed fixed.

---

### [12] Diff estimate ≤ 10,000 characters

**PASS.** Plan §7: ~88 lines total across both files; estimated ~2,200 characters.  
2,200 << 10,000. PR diff limit satisfied.

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan section |
|---|---|---|
| Block NT8 `"Close"` order name from follower dispatch | ✅ | §2.1 (exact match guard) |
| Block NT8 `"Flatten"` order name | ✅ | §2.1 (exact match guard) |
| Block NT8 `"Rev..."` reversal order names | ✅ | §2.1 (StartsWith prefix guard) |
| Block NT8 `"Exit..."` exit signal names | ✅ | §2.1 (StartsWith prefix guard) |
| Preserve existing `"PTT-"` cascade-copy protection | ✅ | §2.1 (consolidated into IsExitSignalName) |
| `IsExitSignalName` testable without reflection | ✅ | §2.1 (`internal static`) |
| DispatchCopy CYC ≤ 8 after change | ✅ | §2.2 (CYC 8→7) |
| 7 xUnit `[Fact]` tests covering all branches | ✅ | §4 (T_B59_01–T_B59_07) |
| deploy-sync.ps1 in both commit sequences | ✅ | §10 (T1 + T2) |
| Diff ≤ 10,000 chars | ✅ | §7 (~2,200 chars) |

---

## Summary

No violations found in second pass. Both V-01 (test insertion line 2749) and V-02 (deploy-sync.ps1 in T2)
are confirmed fixed. All 12 checklist items pass. Spec coverage is complete.

---

REVIEW_PASS
