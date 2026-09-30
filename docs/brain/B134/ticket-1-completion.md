# B134 Ticket 1 — Completion Report (Retry 2)

**Epic**: B134 — DW-B144 (Submitted-state gap) + DW-B145 (wrong bracket index)
**Ticket**: Ticket 1 — DW-B144: Extend FindFollowerBracketOrder to accept OrderState.Submitted
**Retry**: 2 (authorized B133 test amendment per orchestrator)
**Status**: BUILD_PASS

---

## What Was Implemented

### CopyEngine.cs (src/PropTraderTools/CopyEngine.cs L2529-2572)

**T1 (DW-B144)**: State filter in `FindFollowerBracketOrder` extended to include `OrderState.Submitted`.
  - Before: `Working | Accepted` only
  - After: `Working | Accepted | Submitted`
  - Drag-move operations on Submitted brackets no longer silently drop.

**T2 (DW-B145)**: `leaderName` exact-match guard added as a combined form (pre-existing from B134 architecture plan):
  - `if (leaderName != null && order.Name != leaderName) continue;`
  - Prevents wrong-index bracket selection when multiple brackets share the same signal.

**CYC count**: 8 — AT LIMIT; PASS.
  - foreach(1) + SignalOrNameMatches guard(1) + leaderName exact guard(1) + state filter(3) + isStop(1) + type match(1) = 8

### B134Tests.cs (src/PropTraderTools/Tests/B134Tests.cs) — NEW FILE

5 [Fact] tests in `B134FindFollowerBracketOrderTests.B134Ticket1Tests`:
  1. `T1_SubmittedState_StopOrder_Found_And_Returned` — primary DW-B144 fix (stop path)
  2. `T1_SubmittedState_TargetOrder_Found_And_Returned` — primary DW-B144 fix (target path)
  3. `T1_WorkingState_StillFound_Regression` — guards Working branch not broken
  4. `T1_AcceptedState_StillFound_Regression` — guards Accepted branch not broken
  5. `T1_NullOrder_NotMatched_Guard` — Initialized state still rejected

### PropTraderTools.csproj (L162) — B134Tests.cs registered

```xml
<Compile Include="Tests\B134Tests.cs" />
```

### B133Tests.cs — AUTHORIZED AMENDMENT (Retry 2)

**Authorization**: Orchestrator explicitly authorized ONE targeted amendment.
**Rationale**: `FindFollowerBracketOrder_SubmittedState_IsNotFound` (L155) was written pre-B134
to assert `Assert.Null(result)` for Submitted state. DW-B144 intentionally reverses this behavior.
The "DO NOT MODIFY B133Tests.cs" directive was intended to prevent scope creep, NOT to lock
in superseded behavior. The amendment reflects the correct post-B134 behavior.

**Change at L167-168**:
  - FROM: `// Assert: Submitted remains excluded (unreliable cancel)` + `Assert.Null(result);`
  - TO:   `// Assert: Post-B134: Submitted orders now accepted (DW-B144 fix)` + `Assert.NotNull(result);`

No other change made to B133Tests.cs.

---

## 7-Scan Results

### SCAN-01: lock() in CopyEngine.cs
```
Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" }
Result: (no output)
```
**RESULT: 0 hits. PASS.**

### SCAN-02: throw new in CopyEngine.cs
```
Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw\s+new"
Result: (no output)
```
**RESULT: 0 hits. PASS.**

### SCAN-03: Non-ASCII bytes in CopyEngine.cs
```
Command: $bytes = [System.IO.File]::ReadAllBytes("src/PropTraderTools/CopyEngine.cs"); ($bytes | Where-Object { $_ -gt 127 } | Measure-Object).Count
Result: 0
```
**RESULT: 0 non-ASCII bytes. PASS.**

### SCAN-04: CYC manual count
```
FindFollowerBracketOrder (L2540-2572):
  foreach(1) + SignalOrNameMatches guard(1) + leaderName exact guard(1) + state filter(3) + isStop(1) + type match(1) = 8
CYC=8 documented at L2536-2537.
```
**RESULT: CYC=8. AT LIMIT; PASS.**

### SCAN-05: return null in new code (L>2560)
```
Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null" | Where-Object { $_.LineNumber -gt 2560 }
Result: src\PropTraderTools\CopyEngine.cs:2571:            return null;
```
**RESULT: L2571 is the terminal Order? null contract (JS-002 compliant — method returns Order?, null = "not found"). PASS.**

### SCAN-06: dotnet build
```
Command: dotnet build "src/PropTraderTools/PropTraderTools.csproj"
Result: Build succeeded. 0 Error(s). 1 Warning (xUnit2004 in B131Tests.cs — pre-existing, not in scope).
```
**RESULT: 0 errors. PASS.**

### SCAN-07: dotnet test (B129+B130+B131+B132+B133+B134)
```
Command: dotnet test --filter "FullyQualifiedName~B129|FullyQualifiedName~B130|FullyQualifiedName~B131|FullyQualifiedName~B132|FullyQualifiedName~B133|FullyQualifiedName~B134"
Result: Passed! - Failed: 0, Passed: 47, Skipped: 0, Total: 47
```
Breakdown:
  - B129: 13 PASS
  - B130: 8 PASS
  - B131: 7 PASS
  - B132: 6 PASS
  - B133: 10 PASS (includes amended FindFollowerBracketOrder_SubmittedState_IsNotFound)
  - B134Ticket1Tests: 5 PASS

Pre-existing failures (15 tests) in B44/B68/B70/B71/B72/B74/B76/B77/B79/B118 — out of B134 scope, pre-existing before this ticket.

**RESULT: 47/47 PASS. PASS.**

---

## Jane Street DNA Compliance

| Rule | Status |
|------|--------|
| JS-021 no lock() | PASS — 0 lock() in CopyEngine.cs |
| JS-001 no throw in hot path | PASS — 0 throw new in CopyEngine.cs |
| JS-002 Order? null contract unchanged | PASS — L2571 return null is the correct null contract |
| JS-008 immutability | PASS — no mutable statics added |
| ASCII-only | PASS — 0 non-ASCII bytes |
| CYC<=8 | PASS — CYC=8 exactly |
| CreateOrder name "PTT-" prefix | N/A — no CreateOrder calls in T1 scope |
| DateTime.UtcNow only | N/A — no DateTime in T1 scope |

---

## Files Modified

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | DW-B144: Submitted added to state filter; DW-B145: leaderName exact guard added (T1+T2 combined form) |
| `src/PropTraderTools/Tests/B134Tests.cs` | NEW: 5 [Fact] tests for DW-B144 fix |
| `src/PropTraderTools/PropTraderTools.csproj` | B134Tests.cs registered at L162 |
| `src/PropTraderTools/Tests/B133Tests.cs` | AMENDED: Assert.Null→Assert.NotNull in FindFollowerBracketOrder_SubmittedState_IsNotFound (authorized by orchestrator, retry 2) |

---

## BUILD_PASS
