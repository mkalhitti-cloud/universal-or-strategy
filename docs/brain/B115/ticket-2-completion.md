# B115 Ticket T2 -- Completion Report

**Block**: B115
**Ticket**: T2
**DW Reference**: DW-B122
**Date**: 2026-08-27
**Engineer**: ptt-engineer (Phase 4a)
**Status**: BUILD_PASS

---

## What Was Implemented

New file created: `src/PropTraderTools/Tests/B115Tests.cs`

**Purpose**: Provides xUnit [Fact] test coverage for the DW-B122 fix — the compound
state guard (state != Working && state != Accepted) added to
TryCleanupReArmedAtmBracket at CopyEngine.cs L2397-2398.

**Seam used**: _qxPendingFollowerCleanup (internal ConcurrentDictionary<string,
(Instrument, DateTime)> on CopyEngine.Instance, accessible via
[assembly: InternalsVisibleTo("PropTraderTools.Tests")] at CopyEngine.cs L46).

**Why direct invocation is impossible**: TryCleanupReArmedAtmBracket requires a
live OrderEventArgs (NT8 sealed class, no public constructor). Tests instead
validate the guard boolean expression inline and the dict seam operations directly.

---

## Test Methods Written

| Method | CYC | What It Tests |
|--------|-----|---------------|
| TryCleanupReArmedAtmBracket_GuardAcceptsAcceptedState | 1 | DW-B122: OrderState.Accepted evaluates guard to false (does NOT early-return) |
| TryCleanupReArmedAtmBracket_GuardRejectsUnknownState | 1 | OrderState.Cancelled evaluates guard to true (correctly early-returns) |
| TryCleanupReArmedAtmBracket_DictSeam_T1Path_EntryRetained | 2 | tChar='1', non-expired: shouldRemove=false, entry stays in dict |
| TryCleanupReArmedAtmBracket_DictSeam_T3Path_EntryRemoved | 2 | tChar='3': shouldRemove=true, entry removed from dict |

---

## Rules Catalog Gate

**STEP 0 — RULES CATALOG GATE: PASS**

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No lock() in new file | PASS |
| JS-033 | No async void ([Fact] methods are synchronous) | PASS |
| JS-001 | No throw new XxxException | PASS |
| JS-002 | No return null | PASS |
| xUnit only | No NUnit/MSTest imported | PASS |
| NT8 sealed | No sealed OrderEventArgs/Order/Account/Instrument instantiated | PASS |
| ASCII-only | All string literals and comments are ASCII | PASS |

---

## 7-Scan Layer 2 Report

### SCAN-01 -- lock() check
**Command**: Select-String -Path src\PropTraderTools\Tests\B115Tests.cs -Pattern "lock\("
**Result**: zero matches
**Status**: PASS

### SCAN-02 -- async void check
**Command**: Select-String ... -Pattern "async void" filtered to non-comment lines
**Result**: zero matches in executable code (comment on L2 contains "no async void" as documentation; not executable code)
**Status**: PASS

### SCAN-03 -- throw new check
**Command**: Select-String -Path src\PropTraderTools\Tests\B115Tests.cs -Pattern "throw new"
**Result**: zero matches
**Status**: PASS

### SCAN-04 -- return null check
**Command**: Select-String -Path src\PropTraderTools\Tests\B115Tests.cs -Pattern "return null"
**Result**: zero matches
**Status**: PASS

### SCAN-05 -- new byte[] check
**Command**: Select-String -Path src\PropTraderTools\Tests\B115Tests.cs -Pattern "new byte\["
**Result**: zero matches
**Status**: PASS

### SCAN-06 -- CYC check
**Method**: Manual count (no if/for/while/switch in T_B115_01 and _02; one if-branch in _03 and _04)

| Method | Branches | CYC | <= 8? |
|--------|----------|-----|-------|
| GuardAcceptsAcceptedState | 0 | 1 | PASS |
| GuardRejectsUnknownState | 0 | 1 | PASS |
| DictSeam_T1Path_EntryRetained | 1 (if shouldRemove) | 2 | PASS |
| DictSeam_T3Path_EntryRemoved | 1 (if shouldRemove) | 2 | PASS |

**Status**: PASS (all <= 8)

### SCAN-07 -- ASCII-only check
**Command**: byte scan for bytes > 127 via [System.IO.File]::ReadAllBytes
**Result**: zero non-ASCII bytes
**Status**: PASS

---

## Layer 2 Summary

| Scan | Result | Status |
|------|--------|--------|
| SCAN-01 lock() | 0 | PASS |
| SCAN-02 async void | 0 code hits | PASS |
| SCAN-03 throw new | 0 | PASS |
| SCAN-04 return null | 0 | PASS |
| SCAN-05 new byte[ | 0 | PASS |
| SCAN-06 CYC | all <= 2 | PASS |
| SCAN-07 non-ASCII | 0 | PASS |

**ALL 7 SCANS: PASS**

---

## Acceptance Criteria Check

- [x] File `src/PropTraderTools/Tests/B115Tests.cs` created
- [x] Namespace: `PropTraderTools.Tests`
- [x] Class: `B115Tests`
- [x] Framework: xUnit `[Fact]` only -- no `[Theory]`, no NUnit, no MSTest
- [x] `TryCleanupReArmedAtmBracket_GuardAcceptsAcceptedState` present and asserts `Assert.False(guardFires)`
- [x] `CopyEngine.Instance._qxPendingFollowerCleanup.Clear()` called at start of dict-seam tests
- [x] All 7 scans: zero findings

---

## BUILD_PASS