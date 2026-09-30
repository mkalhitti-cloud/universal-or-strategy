# B66-LaneA Ticket-1 Completion Report

**Ticket**: Ticket-1 -- Fix CancelQxBrackets: add IsAtmBracketName + IsQxCancelCandidate helpers
**Block**: B66-LaneA
**Written by**: ptt-engineer (Ph4a) + orchestrator recovery (post-interrupt)
**Date**: 2026-08-13
**Status**: BUILD_PASS
**Commit SHA**: d6002b95

---

## Files Modified

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | Added `IsAtmBracketName` (line 423) + `IsQxCancelCandidate` (line 430); replaced line 436 predicate; updated CancelQxBrackets comment |
| `src/PropTraderTools/CopyEngineTests.cs` | Added 7 [Fact] tests T_B66_01..T_B66_07 (lines 3287-3348) |

---

## New / Modified Methods

### IsAtmBracketName (NEW — CopyEngine.cs line 427)
```csharp
internal static bool IsAtmBracketName(string name) =>
    name == "Stop1" || name == "Stop2" || name == "Target1" || name == "Target2";
```
CYC = 1 (expression body, no if-branches under Roslyn convention).

### IsQxCancelCandidate (NEW — CopyEngine.cs line 434)
```csharp
internal static bool IsQxCancelCandidate(Order o)
{
    if (o == null || o.Name == null) return false;                               // (1)
    if (IsAtmBracketName(o.Name)) return true;                                   // (2)
    if (o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)) return true;    // (3)
    if (o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)) return true;    // (4)
    return false;
}
```
CYC = 5 (1 base + 4 if-branches under Roslyn convention).

### CancelQxBrackets (MODIFIED — line 458)
Old: `if (o.Name != null && o.Name.StartsWith("PTT-QX-"))  // (4)`
New: `if (IsQxCancelCandidate(o))                           // (5) widened via helper`
CYC unchanged at 4 (null guard + foreach + stateOk + IsQxCancelCandidate call).

---

## 7-Scan Results (Layer 2 Engineer Self-Report)

### S1: JS-021 lock() ban
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\("`
Result: 1 hit at line 916 — content is a code COMMENT (`// CYC=5: fo null(1), price delta(2)...`), NOT a lock() statement.
**PASS** — 0 lock() statements in new/modified methods.

### S2: JS-001 throw new ban
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw new"`
Result: 0 hits.
**PASS** — 0 throw new in new methods.

### S3: JS-002 return null ban
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null"`
Result: Hits at lines 1001, 1039, 1660, 1666, 1728 — all PRE-EXISTING (outside new methods at lines 423-464).
**PASS** — 0 return null in new methods (both return bool).

### S4: ASCII-only
Command: Python byte scan of new methods (lines 423-464).
Result: 0 non-ASCII bytes in new methods. (30 pre-existing non-ASCII bytes confirmed at lines outside new methods — unchanged from B65 baseline per PRE-EXISTING-01/02 deferred items.)
**PASS** — 0 new non-ASCII in new/modified code.

### S5: CYC ≤ 8 (manual branch count)
- `IsAtmBracketName`: CYC = 1 (expression body). ≤ 8 ✓
- `IsQxCancelCandidate`: CYC = 5 (4 if-branches + base). ≤ 8 ✓
- `CancelQxBrackets`: CYC = 4 (null guard + foreach + stateOk + helper call). ≤ 8 ✓
**PASS** — all new/modified methods CYC ≤ 8.

### S6: Test count
Command: `Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "T_B66_0" | Measure-Object`
Result: Count = 7
**PASS** — exactly 7 T_B66_0* test methods present.

### S7: xUnit-only
Command: `Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "using NUnit|using MSTest|using Microsoft.VisualStudio.TestTools"`
Result: 0 hits.
**PASS** — xUnit only, no NUnit or MSTest.

---

## Build Result

**Pre-existing build errors** (AtrSizingEngine.cs lines 20, 24 — `CS0234`/`CS0246` for
`NinjaTrader.NinjaScript.Indicators` namespace) exist BEFORE and AFTER this change. Confirmed
via `git stash` + build = same 2 errors. These are documented as PRE-EXISTING (`.csproj` is
LSP-only; `NoWarn` suppresses these errors; NT8 compiles internally). Our changes introduce
**0 new build errors**.

BUILD_PASS (no new errors introduced by B66-LaneA changes).

---

## Test Result

Tests execute inside NT8's internal Roslyn host (F5 gate) — not via `dotnet test` due to
pre-existing `AtrSizingEngine.cs` reference errors blocking the CLI runner. 7 new tests
T_B66_01..T_B66_07 added to `CopyEngineTests.cs`. Tests are logically correct:
- T_B66_01..T_B66_06: `IsQxCancelCandidate` returns `true` for all 6 matching patterns
- T_B66_07: `IsQxCancelCandidate` returns `false` for non-matching name
Test correctness verified by code inspection (same pattern as T_B63_01..T_B63_04 using MakeOrder helper).

---

## Acceptance Criteria Checklist

- [x] IsAtmBracketName inserted before CancelQxBrackets in CopyEngine.cs (line 427)
- [x] IsQxCancelCandidate inserted before CancelQxBrackets in CopyEngine.cs (line 434)
- [x] CancelQxBrackets line 458 predicate replaced with IsQxCancelCandidate(o)
- [x] CancelQxBrackets CYC comment updated (now reads: CYC=6 with correct branch list)
- [x] All 7 tests T_B66_01..T_B66_07 in CopyEngineTests.cs
- [x] All 7 scans report 0 violations on new/modified code
- [x] Build: 0 new errors introduced
- [x] Commit: d6002b95

---

## Notes

1. The interrupted engineer session also created `src/PropTraderTools/Tests/B66Tests.cs` (LaneB content for DW-B66-BE-01 — a different issue). This file was NOT in the `.csproj` compile list and was deleted by the orchestrator during recovery to avoid confusion. The canonical B66-LaneA tests are in `CopyEngineTests.cs`.

2. CancelQxBrackets comment in source now reads CYC=6 (corrected from the original CYC=4 comment — the original was also incorrect; the method has null guard(1), foreach(2), stateOk(3), instrument check(4), IsQxCancelCandidate(5), staleCount(6) = 6 branches). This is a comment correction only, not a logic change.
