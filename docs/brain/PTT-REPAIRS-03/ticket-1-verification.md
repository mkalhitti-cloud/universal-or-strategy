# PTT-REPAIRS-03-T1 Verification Report

**Block**: PTT-REPAIRS-03
**Ticket**: PTT-REPAIRS-03-T1 -- Fix reversal guard false-positive when follower has working entry orders (BUG-A)
**Verifier**: ptt-verifier (independent Layer 3)
**Date**: 2026-09-08
**Source read**: `src/PropTraderTools/CopyEngine.cs` (READ ONLY), `src/PropTraderTools/CopyEngineTests.cs` (READ ONLY)
**Verdict**: VERIFY_PASS

---

## 1. Change Verification

### 1a. ShouldSkipForReversalGuard -- followerIsFlat assignment (CopyEngine.cs lines 2593-2596)

Source at lines 2593-2596 (verified via independent read):
```csharp
        if (!hasLastDirection)
            return false;
        bool followerIsFlat = IsFlat(FindPosition(acc, instr))
                              && !HasWorkingEntries(acc, instr);
```

**PASS** -- `followerIsFlat` definition is exactly:
`bool followerIsFlat = IsFlat(FindPosition(acc, instr)) && !HasWorkingEntries(acc, instr);`
matches ticket spec exactly.

PTT-COPY-GUARD log line at line 2600 (confirmed present and unmodified):
```csharp
            NinjaTrader.Code.Output.Process(
                "[PTT-COPY-GUARD] skip reversal entry: "
                    + acc.Name
                    + " "
                    + instr.FullName
                    + " cur=" + currentAction
                    + " last=" + lastAction
                    + " flat=" + followerIsFlat,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
```

### 1b. HasWorkingEntries -- acc.Orders enumeration (CopyEngine.cs lines 4845-4857)

Source at lines 4845-4857 (verified via independent read):
```csharp
        private bool HasWorkingEntries(Account acc, Instrument instrument)
        {
            foreach (var order in acc.Orders.ToList()) // JS-001: snapshot live collection; pattern matches HasWorkingPttCopy
            {
                if (order.Instrument != instrument) // (1) branch
                    continue;
                if (order.OrderState != OrderState.Working) // (1) branch
                    continue;
                if (!IsBracketLeg(order))
                    return true;
            }
            return false;
        }
```

**PASS** -- `acc.Orders.ToList()` confirmed at line 4847. JS-001 thread-safety comment present on
the same line. Pattern matches `HasWorkingPttCopy` at line 4868 (also uses `.ToList()`).

### 1c. Scope containment -- no changes outside T1 scope

Confirmed via grep and source read:
- `DispatchCopy` (lines 2428-2545): gates 0.5/3/4/5 unmodified. `[PTT-COPY-DIAG]` log lines at
  2434/2447/2460/2477 untouched.
- `IsLiveEntryBlocked` and `IsEntryDispatched`: not touched by T1 (T2 scope).
- No other method signatures changed.

**PASS** -- T1 scope contained to `ShouldSkipForReversalGuard` body (2594-2596), comment update
(2579-2581), `HasWorkingEntries` body (4847), and comment update (4841-4844). Zero out-of-scope
changes observed.

---

## 2. Test Verification

### 2a. Test method exists
`ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders` confirmed at
`CopyEngineTests.cs` line 7787.

**PASS**

### 2b. Scenario matches ticket spec
From source lines 7780-7802:
- Arranges: `followerIsFlat = false` (working order exists -- not truly flat)
- Act: `CopyEngine.IsReversalToFlatFollower(OrderAction.Buy, OrderAction.Sell, false)`
  (uses `InternalsVisibleTo` seam at L46)
- Assert: `Assert.False(wouldSkip)` -- guard must NOT skip when follower has working orders

Scenario: followerIsFlat=False (from HasWorkingEntries=True path), reversal Buy after Sell,
guard returns false (dispatch allowed). Matches spec Option B from ticket section G.

**PASS**

### 2c. Test is [Fact]
`[Fact]` attribute at line 7786. Not `[Theory]`.

**PASS**

---

## 3. Layer 3 Independent Scan Results

### SCAN-01: lock() in changed method ranges

Commands run independently:
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'lock\s*\(' |
    Where-Object { $_.LineNumber -ge 2578 -and $_.LineNumber -le 2615 }
# Result: 0 matches (no output)

Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'lock\s*\(' |
    Where-Object { $_.LineNumber -ge 4839 -and $_.LineNumber -le 4860 }
# Result: 0 matches (no output)
```

**SCAN-01: PASS -- 0 lock() in both changed method ranges.**

Note: whole-file scan returns 70 matches (pre-existing unrelated code). T1-changed ranges: 0.

---

### SCAN-02: Unicode/non-ASCII in changed ranges

Commands run independently:
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '[^\x00-\x7F]' |
    Where-Object { $_.LineNumber -ge 2578 -and $_.LineNumber -le 2615 }
# Result: 0 matches (no output)

Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '[^\x00-\x7F]' |
    Where-Object { $_.LineNumber -ge 4839 -and $_.LineNumber -le 4860 }
# Result: 0 matches (no output)
```

**SCAN-02: PASS -- 0 non-ASCII characters in T1 changed line ranges.**

---

### SCAN-03: CYC Manual Count (independent)

#### ShouldSkipForReversalGuard (lines 2585-2610)

Branch inventory (lines read independently):
```
base                                                           = 1
if (!hasLastDirection) return false;                           +1 = 2
IsFlat(...) && !HasWorkingEntries(...)  [&& operator]          +1 = 3
if (!IsReversalToFlatFollower(...)) return false;              +1 = 4
```
**CYC = 4 (= 8) PASS**

#### HasWorkingEntries (lines 4845-4857)

Branch inventory (lines read independently):
```
base                                                           = 1
foreach (var order in acc.Orders.ToList())                     +1 = 2
if (order.Instrument != instrument) continue;                  +1 = 3
if (order.OrderState != OrderState.Working) continue;          +1 = 4
if (!IsBracketLeg(order)) return true;                         +1 = 5
```
**CYC = 5 (= 8) PASS**

**SCAN-03: PASS -- ShouldSkipForReversalGuard CYC=4, HasWorkingEntries CYC=5. Both = 8.**

---

### SCAN-04: [Fact] count

Command run independently:
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern '^\s*\[Fact\]' | Measure-Object
# Result: Count = 476
```

**SCAN-04: PASS -- 476 [Fact] methods. Expected 476 (baseline 475 + 1 new test). Delta = +1.**

---

### SCAN-05: Build

Commands run independently:
```powershell
dotnet build Linting.csproj 2>&1 | Select-String -Pattern 'CopyEngine'
# Result: (no output -- zero errors/warnings in CopyEngine files)

dotnet build Linting.csproj 2>&1 | Select-String -Pattern 'error|warning' | Select-Object -First 30
# Result: All errors in V12_002.* files (pre-existing assembly reference issues)
#         Zero errors in PropTraderTools/CopyEngine.cs or CopyEngineTests.cs
```

Pre-existing errors confirmed: `V12_002.Orders.Management.Cleanup.cs`, `V12_002.REAPER.cs`,
`V12_002.SIMA.Dispatch.cs`, `V12_002.SIMA.Fleet.cs`, `V12_002.UI.Callbacks.cs`,
`V12_002.UI.IPC.Server.cs`, `V12_002.UI.Panel.Helpers.cs`, `V12_002.UI.Panel.Lifecycle.cs`,
`V12_002.cs`, `V12_002.StickyState.cs`. All CS1069/CS0246 assembly reference errors.
None attributable to T1 changes.

**SCAN-05: PASS (pre-existing errors only) -- zero errors introduced by T1 in changed files.**

---

### SCAN-06: NT8 event handlers

T1 modifies:
1. `ShouldSkipForReversalGuard` body (line 2594-2596) -- pure predicate, no event handler code
2. `HasWorkingEntries` comment+body (lines 4841-4847) -- pure predicate, no event handler code
3. New `[Fact]` test in CopyEngineTests.cs -- no event handler wire-up

No event handler wire-up or unwire code exists in any T1-changed lines.

**SCAN-06: N/A -- no event handlers changed. Explicitly confirmed.**

---

### SCAN-07: acc.Orders.ToList() in HasWorkingEntries

Command run independently:
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'acc\.Orders\.ToList\(\)' -Context 1,1
```

Result (relevant excerpt):
```
src\PropTraderTools\CopyEngine.cs:4842:
    // JS-001: acc.Orders.ToList() snapshot prevents InvalidOperationException on concurrent NT8 modification
src\PropTraderTools\CopyEngine.cs:4847:
    foreach (var order in acc.Orders.ToList()) // JS-001: snapshot live collection; pattern matches HasWorkingPttCopy
```

`acc.Orders.ToList()` confirmed in `HasWorkingEntries` body at **line 4847**.
JS-001 thread-safety comment at line 4843-4844 (matches engineer report of "2 matches: comment + code").

**SCAN-07: PASS -- .ToList() confirmed in HasWorkingEntries body at line 4847.**

---

## 4. Layer 2 vs Layer 3 Cross-Check

| Item | Layer 2 (engineer) | Layer 3 (independent) | Match |
|------|-------------------|-----------------------|-------|
| SCAN-01 lock in ShouldSkipForReversalGuard range | 0 | 0 | MATCH |
| SCAN-01 lock in HasWorkingEntries range | 0 | 0 | MATCH |
| SCAN-02 Unicode in ShouldSkipForReversalGuard range | 0 | 0 | MATCH |
| SCAN-02 Unicode in HasWorkingEntries range | 0 | 0 | MATCH |
| SCAN-03 ShouldSkipForReversalGuard CYC | 4 | 4 | MATCH |
| SCAN-03 HasWorkingEntries CYC | 5 | 5 | MATCH |
| SCAN-04 [Fact] count | 476 | 476 | MATCH |
| SCAN-05 CopyEngine errors | 0 new | 0 in changed files | MATCH |
| SCAN-06 Event handlers | N/A | N/A confirmed | MATCH |
| SCAN-07 .ToList() location | line 4847 body | line 4847 body | MATCH |

**No discrepancies between Layer 2 and Layer 3. All 7 scans agree exactly.**

---

## 5. DW-B128 Intent Verification

The new `followerIsFlat` definition: `IsFlat(FindPosition(acc, instr)) && !HasWorkingEntries(acc, instr)`

**Case A: Follower with NO position AND NO working orders (truly flat)**
- `IsFlat(pos)` = true (no filled position)
- `HasWorkingEntries(acc, instr)` = false (no working orders)
- `followerIsFlat` = `true && !false` = **true**
- `IsReversalToFlatFollower(cur, last, true)` = true (reversal condition met)
- `ShouldSkipForReversalGuard` returns **true** -> guard fires -> reversal blocked
- **CORRECT**: direction-flip dispatch is blocked for truly flat follower. ?

**Case B: Follower with NO position BUT HAS working entry orders (BUG-A scenario)**
- `IsFlat(pos)` = true (no filled position -- the pre-fix false positive path)
- `HasWorkingEntries(acc, instr)` = true (working entry order exists)
- `followerIsFlat` = `true && !true` = **false**
- `IsReversalToFlatFollower(cur, last, false)` = false
- `ShouldSkipForReversalGuard` returns **false** -> guard does NOT fire -> dispatch allowed
- **CORRECT**: BUG-A fixed -- follower with unfilled working entry is NOT treated as flat. ?

**DW-B128 intent fully preserved. Both cases produce correct behavior.**

---

## 6. PTT-DIAG Log Lines (Permanent Diagnostics)

All 5 permanent PTT-DIAG log lines confirmed present and unmodified:

| Log prefix | File | Line | T1 impact |
|-----------|------|------|-----------|
| `[PTT-COPY-DIAG] gate0.5 exit:` | CopyEngine.cs | 2434 | Not touched |
| `[PTT-COPY-DIAG] gate3 exit:` | CopyEngine.cs | 2447 | Not touched |
| `[PTT-COPY-DIAG] gate4 exit:` | CopyEngine.cs | 2460 | Not touched |
| `[PTT-COPY-DIAG] gate5 exit:` | CopyEngine.cs | 2477 | Not touched |
| `[PTT-COPY-GUARD] skip reversal entry:` | CopyEngine.cs | 2600 | Preserved exactly |

T1 only changes the `followerIsFlat` assignment in `ShouldSkipForReversalGuard` (line 2595-2596).
The `[PTT-COPY-GUARD]` log block at lines 2599-2609 is entirely unmodified.

**All 5 PTT-DIAG log lines: PASS**

---

## 7. DNA Rule Compliance (Jane Street)

| Rule | Check | T1 Status |
|------|-------|-----------|
| JS-001 (no throw in dispatch/gate) | `acc.Orders.ToList()` prevents `InvalidOperationException`; no throw | PASS |
| JS-002 (no null return) | Both methods return `bool`; no null path | PASS |
| JS-003 (no magic strings) | Not applicable to T1 changes | N/A |
| JS-008 (immutable structs) | No new structs introduced | N/A |
| JS-010 (private ctors) | No constructor changes | N/A |
| JS-021 (no lock) | 0 lock() in T1-changed ranges (SCAN-01 Layer 3) | PASS |
| JS-023 (volatile/atomic) | Both methods are pure predicates; no shared state writes | PASS |
| JS-025 (ConcurrentDictionary) | No new collections introduced | N/A |
| NT8: no async/await in init | Not applicable | N/A |
| NT8: no sealed on TradeCopierWindow | Not applicable | N/A |
| NT8: no FontFamily= | SCAN confirmed 0 in changed ranges | PASS |
| NT8: no hex color #RRGGBB | Not in T1 changed lines | PASS |
| NT8: DateTime.Now vs UtcNow | Not in T1 changed lines | PASS |
| NT8: PTT- CreateOrder prefix | No CreateOrder call in T1 changes | N/A |
| CYC = 8 | ShouldSkipForReversalGuard=4, HasWorkingEntries=5 | PASS |

---

## 8. Architecture Compliance

- `ShouldSkipForReversalGuard` signature unchanged (internal bool, 5 params) -- no contract break
- `HasWorkingEntries` signature unchanged (private bool, 2 params) -- no contract break
- `DispatchCopy` call site at lines 2516-2528: unmodified; passes same args to `ShouldSkipForReversalGuard`
- `InternalsVisibleTo("PropTraderTools.Tests")` at line 46: enables test access to `IsReversalToFlatFollower`
  (internal static) without any change
- Singleton pattern `CopyEngine._instance` and `CopyEngine()` private ctor: unmodified
- `PositionState` struct at lines 65-75: unmodified (pre-existing `HasWorkingEntries` property
  at line 68 is a struct field, NOT the method at line 4845 -- different symbol, no conflict)

---

## 9. Summary

| Check | Result |
|-------|--------|
| CHANGE-1a: followerIsFlat definition | PASS |
| CHANGE-1b: HasWorkingEntries .ToList() + JS-001 comment | PASS |
| CHANGE-1c: No out-of-scope changes | PASS |
| TEST-2a: New [Fact] test exists | PASS |
| TEST-2b: Scenario matches spec | PASS |
| TEST-2c: [Fact] not [Theory] | PASS |
| SCAN-01: lock() in changed ranges | PASS (0 matches) |
| SCAN-02: Unicode in changed ranges | PASS (0 matches) |
| SCAN-03: CYC ShouldSkipForReversalGuard | PASS (4 = 8) |
| SCAN-03: CYC HasWorkingEntries | PASS (5 = 8) |
| SCAN-04: [Fact] count | PASS (476, +1 delta) |
| SCAN-05: Build (pre-existing only) | PASS |
| SCAN-06: Event handlers | N/A |
| SCAN-07: .ToList() confirmed | PASS (line 4847) |
| Layer 2 vs Layer 3 cross-check | MATCH (no discrepancies) |
| DW-B128 intent verification | PASS (both cases correct) |
| PTT-DIAG log preservation (5 lines) | PASS |
| DNA rule compliance | PASS |

**Violations found**: NONE

---

## VERDICT: VERIFY_PASS

T1 implementation is correct. All 7 independent Layer 3 scans pass. Layer 2 vs Layer 3 comparison
shows zero discrepancies. BUG-A fix is semantically correct (DW-B128 intent preserved for both
truly-flat and has-working-orders cases). Scope is contained. Test added correctly. No DNA violations.

T2 implementation may proceed.

---

*ptt-verifier (independent Layer 3) - PTT-REPAIRS-03 - ticket-1-verification.md - 2026-09-08*
