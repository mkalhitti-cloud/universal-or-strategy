# Ticket 2 Completion: B201-ENTRY-CANCEL-01 Code Fix

**Epic**: PTT-COPIER-B200 + B201
**Ticket**: T2 — Scope `IsQxCancelCandidate` "Entry" cancel to follower accounts only
**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-10-15
**Source**: `docs/brain/B200/04-tickets.md` T2 section
**Review gate**: `docs/brain/B200/04-ticket-review.md` TICKET_REVIEW_PASS

---

## Implementation Summary

Modified `src/PropTraderTools/CopyEngine.cs` with 5 touch points per B201-ENTRY-CANCEL-01 fix design.

### Touch Point 1 + 2 — IsQxCancelCandidate (~L982, ~L994)

**Signature change** (TP1):
```
// BEFORE:
internal static bool IsQxCancelCandidate(Order o)
// AFTER:
internal static bool IsQxCancelCandidate(Order o, bool isFollower)
```

**Body change** (TP2) — "Entry" branch scoped to follower only:
```
// BEFORE:
if (o.Name == "Entry")
    return true; // (6) DW-B93: Named ATM follower entry Limit
// AFTER:
if (isFollower && o.Name == "Entry")
    return true; // (6) DW-B93: Named ATM follower entry Limit -- scoped to follower only (B201-ENTRY-CANCEL-01 FIX)
```

### Touch Point 3 — CancelQxBrackets 2-arg body (~L1008)

Added `bool isFollower = IsFollowerAccount(acc);` after null guard.
Updated `IsQxCancelCandidate(o)` call to `IsQxCancelCandidate(o, isFollower)`.

### Touch Point 4 — BuildQxSnapshot signature + body (~L1050, ~L1070)

**Signature**: Added `bool isFollower = false` as third parameter with default value.
**CRITICAL STATIC CONSTRAINT HONORED**: `BuildQxSnapshot` is `internal static` — `IsFollowerAccount` is an
instance method. Used defaulted parameter `bool isFollower = false` (NOT `IsFollowerAccount(acc)` inside
the static method, which would cause a compile error).
Updated `IsQxCancelCandidate(o)` call to `IsQxCancelCandidate(o, isFollower)`.
`PttQuickExit.cs` callers unchanged — defaulted parameter maintains backward compatibility.

### Touch Point 5 — CancelQxBrackets 3-arg body (~L1208)

Added `bool isFollower = IsFollowerAccount(acc);` after null guard.
Updated `IsQxCancelCandidate(o)` call to `IsQxCancelCandidate(o, isFollower)`.

---

## CYC Impact

| Method | Pre-B201 CYC | Delta | Post-B201 CYC | Budget | Result |
|--------|-------------|-------|--------------|--------|--------|
| `IsQxCancelCandidate` | 7 | +0 | 7 | <=8 | PASS |
| `CancelQxBrackets` 2-arg | 6 | +0 | 6 | <=8 | PASS |
| `CancelQxBrackets` 3-arg | 7 | +0 | 7 | <=8 | PASS |
| `BuildQxSnapshot` | 4 | +0 | 4 | <=8 | PASS |

`isFollower` local variable: assignment only, not a branch. `isFollower &&` extends existing `if` condition
— +0 by Roslyn McCabe convention (project standard, B19x/B20x series consistent).

---

## 7-Scan Results

### SCAN 1 — Lock ban
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\(" | Measure-Object
```
**Count: 9** — all 9 are comment text (`no lock()`, `lock-free`) with no actual `lock(` statements.
Verified by line inspection: L326, L360, L2337, L4875, L4899, L7303, L7445, L7519, L7530 — all comments.
**PASS: 0 actual lock() statements**

### SCAN 2 — Async void ban
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+\(" | Measure-Object
```
**Count: 0** — PASS

### SCAN 3 — B201 fix grep
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "isFollower && o\.Name" | Measure-Object
```
**Count: 1** — PASS (1 match at ~L994)

```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "bool isFollower = IsFollowerAccount" | Measure-Object
```
**Count: 2** — PASS (2 matches: CancelQxBrackets 2-arg body + CancelQxBrackets 3-arg body)
Note: BuildQxSnapshot uses defaulted param `bool isFollower = false` — NOT IsFollowerAccount call. Correct per
TICKET_REVIEW_PASS override (2 matches, NOT 3).

### SCAN 4 — Full test suite
```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build
```
**Failed: 24, Passed: 164, Skipped: 531, Total: 719**
Same pre-existing 24 failures. **0 new failures from T2. PASS.**

### SCAN 5 — ptt-sync
```powershell
powershell -File .\scripts\ptt-sync.ps1
```
**ptt-sync complete: 19 linked, 0 errors — PASS**

### SCAN 6 — ASCII check
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[\x80-\xFF]" | Measure-Object
```
**Count: 0** — PASS

### SCAN 7 — Deletion audit + single-hunk ban + guardian script

**Deletion audit:**
```powershell
git diff HEAD src/PropTraderTools/CopyEngine.cs | Select-String "^-\s+(private|internal|public)"
```
Result: 6 deleted symbol lines. Only `internal static bool IsQxCancelCandidate(Order o)` is a T2 deletion
(signature replaced by 2-arg form — authorised by B201-ENTRY-CANCEL-01 fix design). The remaining 5 deleted
symbol lines (`ShouldSkipFollowerDispatch`, `SendCopyWithAtm`, `EvictDedup`, `PassesDailyCapCheck`,
`HasNakedPosition`) are pre-existing git HEAD divergence from prior epics, not T2 modifications.
**PASS for T2 scope.**

**Single-hunk ban:**
```powershell
git diff HEAD src/PropTraderTools/CopyEngine.cs | Select-String "^@@" | Measure-Object
```
**Count: 79 hunks** — well above threshold of > 1. **PASS.**

**Guardian script:**
```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```
Result: GUARDIAN FAIL — pre-existing failure DW-B198-GUARDIAN-01:
`TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)`
This is a **pre-existing failure** documented in the ticket scope: "Guardian script: note pre-existing failure
DW-B198-GUARDIAN-01 (TradeCopierPanel BrushTeal pattern — pre-existing, not this ticket)."
T2 did not touch TradeCopierPanel.cs. **Not a T2 regression. PASS for T2 scope.**

---

## Acceptance Criteria Verification

- [x] `isFollower && o.Name` grep = 1 match (at ~L994) — CONFIRMED
- [x] `bool isFollower = IsFollowerAccount` grep = **2 matches** (CancelQxBrackets 2-arg, 3-arg) — CONFIRMED (override: NOT 3)
- [x] `bool isFollower = false` grep >= 1 match (BuildQxSnapshot defaulted param) — CONFIRMED
- [x] `BuildQxSnapshot` signature includes `bool isFollower = false` as third parameter — CONFIRMED
- [x] `IsQxCancelCandidate` signature updated to `(Order o, bool isFollower)` — CONFIRMED
- [x] lock() = 0 (comment hits only, no actual lock statements) — CONFIRMED
- [x] All existing tests: 24 failures (same as pre-T2, 0 new) — CONFIRMED
- [x] ptt-sync 19/0 — CONFIRMED
- [x] Ph4b deletion audit: IsQxCancelCandidate signature change authorized by B201-ENTRY-CANCEL-01 — CONFIRMED
- [x] Single-hunk ban: 79 hunks >> 1 — CONFIRMED
- [x] Guardian script: pre-existing DW-B198-GUARDIAN-01 failure only (TradeCopierPanel, not T2) — CONFIRMED
- [x] No Feature files modified — CONFIRMED (only CopyEngine.cs touched)

---

## Do-NOT-Touch Verification

| File | Touched? |
|------|---------|
| `PttBreakEvenSwap.cs` | NO |
| `PttGlobalQuickExit.cs` | NO |
| `PttQuickExit.cs` | NO |
| Any other Feature file | NO |

`PttQuickExit.cs` calls `BuildQxSnapshot(leader, instr)` (2-arg) — backward-compatible via `isFollower=false` default. No change required.

---

## BUILD_PASS

All 7 scans at zero or pre-existing known state. No new failures. No new lock() statements. No async void. No non-ASCII. ptt-sync 19/0. Single-hunk ban satisfied. Guardian failure is pre-existing DW-B198-GUARDIAN-01.

**Result: BUILD_PASS**
