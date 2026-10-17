# B194 Ticket 1 -- Engineer Completion Report (Cycle 2)

**Epic**: B194 -- Fix 4 confirmed NakedPositionDetector + DrainThenDispatch bugs
**Ticket**: T1 -- F1 (TryNakedDetect PTT-Copy Guard) + F3 (DrainThenDispatch Submitted)
**Engineer**: ptt-engineer (Phase 4a, Cycle 2)
**Date**: 2026-10-07
**Cycle**: 2 (retry after VERIFY_FAIL scope violation in Cycle 1)

---

## 0. Scope Isolation (Cycle 2 Mandatory First Action)

### ResetGlobalQuickAll -- REMOVED

**Action taken**: `git checkout HEAD -- src/PropTraderTools/CopyEngine.cs`

Confirmed that `ResetGlobalQuickAll` was in the Cycle 1 working tree diff (Hunk 1 of 3).
Reverted the entire file to HEAD, then re-applied ONLY the two T1 changes (F1 + F3) from scratch.

**Additional scope isolation**: `CopyEngineTests.cs` was also reverted to HEAD.
The Cycle 1 working tree contained `B192T2ResetGlobalQuickAllTests` class (3 [Fact] tests calling
`ResetGlobalQuickAll`) added to CopyEngineTests.cs. After reverting CopyEngine.cs to HEAD
(which removed `ResetGlobalQuickAll`), the build failed with CS1061 on CopyEngineTests.cs.
`git checkout HEAD -- src/PropTraderTools/CopyEngineTests.cs` was also run to restore CopyEngineTests.cs.
This is correct: CopyEngineTests.cs changes were part of the same out-of-scope B192-T2 work.

**Post-revert verification**:
- `git diff HEAD -- src/PropTraderTools/CopyEngine.cs` = 0 hunks (clean)
- `git diff HEAD -- src/PropTraderTools/CopyEngineTests.cs` = 0 hunks (clean)
- `Select-String "ResetGlobalQuickAll" src/PropTraderTools/CopyEngine.cs` = 0 results

---

## 1. Changes Implemented

### CHANGE 1 -- F1: TryNakedDetect PTT-Copy Fill Guard

**File**: `src/PropTraderTools/CopyEngine.cs`
**Location**: TryNakedDetect method, line 6429 (header comment update) + line 6438 (guard insertion)

**Pre-change (lines 6429-6443 at HEAD)**:
```csharp
        // DW-NEW-08 Option E: thin dispatcher gate.
        // CYC=3: (1) terminal-state check, (2) follower-account check, (3) NakedPositionDetector call.
        // JS-021: no lock. JS-001: no throw. JS-033: synchronous void.
        private void TryNakedDetect(OrderEventArgs e)
        {
            if (
                e.Order.OrderState != OrderState.Filled
                && e.Order.OrderState != OrderState.Cancelled
                && e.Order.OrderState != OrderState.Rejected
            )
                return;
            if (!IsFollowerAccount(e.Order.Account))
                return;
            NakedPositionDetector(e.Order.Account);
        }
```

**Post-change (lines 6429-6449 after edit)**:
```csharp
        // DW-NEW-08 Option E: thin dispatcher gate.
        // CYC=4: (1) PTT-Copy fill guard (B194-F1), (2) terminal-state check,
        //        (3) follower-account check, (4) NakedPositionDetector call.
        // JS-021: no lock. JS-001: no throw. JS-033: synchronous void.
        private void TryNakedDetect(OrderEventArgs e)
        {
            // B194-F1: PTT-Copy fill guard -- follower fill has no brackets yet; brackets arrive
            // after leader Target1:Accepted. Never trigger NakedPositionDetector on own copy fill.
            // DW-B194-01: closed by this guard.
            if (e.Order.Name == "PTT-Copy" && e.Order.OrderState == OrderState.Filled)
                return;
            if (
                e.Order.OrderState != OrderState.Filled
                && e.Order.OrderState != OrderState.Cancelled
                && e.Order.OrderState != OrderState.Rejected
            )
                return;
            if (!IsFollowerAccount(e.Order.Account))
                return;
            NakedPositionDetector(e.Order.Account);
        }
```

**Line numbers**: Guard at line 6438, `return;` at line 6439 (post-edit).
**DW card closed**: DW-B194-01.

---

### CHANGE 2 -- F3: DrainThenDispatch entryCandidates Submitted Inclusion

**File**: `src/PropTraderTools/CopyEngine.cs`
**Location**: DrainThenDispatch method, entryCandidates LINQ Where predicate

**Pre-change (line 6540 at HEAD)**:
```csharp
                    && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted)
```

**Post-change (lines 6546-6548 after edit)**:
```csharp
                    // B194-F3: include Submitted -- PTT-Copy from completed drain-2 may be Submitted.
                    && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted
                        || o.OrderState == OrderState.Submitted)
```

**Line numbers**: Comment at line 6546, predicate at lines 6547-6548 (post-edit).
**DW card closed**: DW-B194-03.

---

## 2. Tests

**File**: `src/PropTraderTools/Tests/B194Tests.cs` (existed from Cycle 1 -- not modified)
**Tests present**:
- `TryNakedDetect_SkipsPTTCopyFill_NoNakedDetectorCalled` [Fact(Skip=...)] -- line 35
- `DrainThenDispatch_IncludesSubmittedInEntryCandidates` [Fact(Skip=...)] -- line 64

Both tests are `[Fact(Skip=...)]` with "integration-observable: SIM gate criterion".
Rationale: private void methods requiring NT8 host runtime for OrderEventArgs/Account/Instrument construction.
**No T2 or T3 tests added** (scope lock honored).

---

## 3. 7-Scan Results

### SCAN-01 [JS-021]: lock() in executable code
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\(" | Where-Object { $_.Line -notmatch "^\s*//" }`
**Output**: (no output)
**Result**: PASS -- zero lock() in executable code

### SCAN-02 [JS-033]: async void in method signatures
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async void" | Where-Object { $_.Line -notmatch "^\s*//" }`
**Output**: (no output)
**Result**: PASS -- zero async void method signatures

### SCAN-03 [JS-066]: CYC count
**TryNakedDetect post-F1**:
- base=1, PTT-Copy guard=+1, terminal-state check=+1, follower-account check=+1
- **CYC = 4** (was 3). JS-066 limit = 8. PASS

**DrainThenDispatch post-F3**:
- LINQ predicate extension does not add McCabe branches to enclosing method
- **CYC unchanged**. PASS

**Result**: PASS -- TryNakedDetect CYC=4, DrainThenDispatch CYC unchanged

### SCAN-04 [JS-001]: throw new in executable code
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw new" | Where-Object { $_.Line -notmatch "^\s*//" }`
**Output**: (no output)
**Result**: PASS -- zero throw new in CopyEngine.cs

### SCAN-05 [JS-002]: return null in changed methods
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null;"`
**Output**: 13 pre-existing matches (lines 1262, 2013, 3149, 3230, 3238, 3882, 3932, 4107, 5448, 5454, 5533, 6335, 6350)
**Verification**: None are in TryNakedDetect (lines 6433-6449) or DrainThenDispatch (~6510-6620).
Both changed methods are void -- use bare `return;` only.
**Result**: PASS -- zero return null in T1 change methods

### SCAN-06 [ASCII-only + DateTime.UtcNow]
**Unicode check**:
- Lines 6428-6448 (F1 area): zero non-ASCII characters
- Lines 6540-6553 (F3 area): zero non-ASCII characters
**DateTime.Now check**:
- `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "DateTime\.Now[^U]" | Where-Object { $_.Line -notmatch "^\s*//" }` = no output
- Only hit in file is a comment at line 4400 (pre-existing, not in change area)
**String "PTT-Copy"**: All chars ASCII (0x50..0x79). PASS
**"OrderState.Submitted"**: All chars ASCII. PASS
**Result**: PASS -- all new strings/identifiers ASCII-only, no DateTime.Now in code

### SCAN-07 [Test coverage]
**Command**: `Select-String -Path "src/PropTraderTools/Tests/B194Tests.cs" -Pattern "\[Fact"`
**Output**: 2 matches (lines 35 and 64)
- Line 35: `TryNakedDetect_SkipsPTTCopyFill_NoNakedDetectorCalled` [Fact(Skip=...)]
- Line 64: `DrainThenDispatch_IncludesSubmittedInEntryCandidates` [Fact(Skip=...)]
**Result**: PASS -- 2 [Fact] T1 tests present in B194Tests.cs

---

## 4. Diff Gate

**Command**: `(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count`
**Output**: `2`

**Hunk 1**: `@@ -6427,10 +6427,16 @@` -- TryNakedDetect F1 guard insertion (+6 lines)
**Hunk 2**: `@@ -6537,7 +6543,9 @@` -- DrainThenDispatch F3 Submitted extension (+2 lines)

**Result**: PASS -- exactly 2 hunks (F1 + F3 only)

**CopyEngineTests.cs diff**: 0 hunks (reverted to HEAD)

---

## 5. ptt-sync Result

**Command**: `powershell -File .\scripts\ptt-sync.ps1`
**Result**: 19 linked, 0 errors. PASS

---

## 6. Build Result

**Command**: `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj`
**Exit code**: 0
**Errors**: 0
**Output**: "Build succeeded. 0 Error(s)"

**Note**: Pre-existing CS0436 warnings (NinjaTrader.Custom type conflicts) are present but unchanged from HEAD.
These are structural to the test project and were pre-existing before T1 work.

---

## 7. DW Cards Closed

| DW Card | Fixed By | Status |
|---------|----------|--------|
| DW-B194-01 | F1 guard in TryNakedDetect (line 6438) | CLOSED |
| DW-B194-03 | F3 LINQ Submitted extension in DrainThenDispatch (line 6547) | CLOSED |

---

## 8. Scope Isolation Summary

**T1 SCOPE LOCK**: `TryNakedDetect` and `DrainThenDispatch` only. No other methods. No other files.

| Check | Result |
|-------|--------|
| ResetGlobalQuickAll in CopyEngine.cs | NOT PRESENT (reverted) |
| CopyEngineTests.cs B192T2 class | NOT PRESENT (reverted to HEAD) |
| CopyEngine.cs diff hunk count | 2 (F1 + F3 only) |
| CopyEngineTests.cs diff hunk count | 0 (clean at HEAD) |
| Any other method touched | NO |
| Any other file modified | NO |

**SCOPE LOCK: HONORED**

---

## 9. Return Status

BUILD_PASS
