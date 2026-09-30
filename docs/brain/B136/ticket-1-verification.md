# B136 Ticket 1 Verification

**Ticket**: B136-T1
**Block**: B136
**Produced by**: ptt-verifier (Phase 4b)
**Date**: 2026-09-07

## Scope Lock Confirmation
SCOPE LOCK - VERIFY TICKET 1 ONLY — confirmed. No other ticket completion files read.

## 7-Scan Comparison Table (Layer 2 vs Layer 3)

| SCAN ID | Engineer Result (Layer 2) | Verifier Result (Layer 3) | Match? |
|---------|--------------------------|--------------------------|--------|
| SCAN-01 lock() | 0 hits in new/modified code | 0 hits confirmed (grep run independently) | MATCH |
| SCAN-02 async void | 0 hits in new code | 0 hits confirmed | MATCH |
| SCAN-03 return null | 0 hits in new methods | 0 hits confirmed (OrderPassesBracketGate returns bool) | MATCH |
| SCAN-04 CYC | FindFollowerBracketOrder=7, OrderPassesBracketGate=2, Testable=1 | Manual count from source: foreach(1)+guard(1)+state(3)+isStop(1)+type(1)=7; if(signalName!=null)(1)+base(1)=2; expression-body=1 | MATCH |
| SCAN-05 ASCII | 0 non-ASCII | 0 non-ASCII confirmed (all identifiers/strings in B136 additions are ASCII) | MATCH |
| SCAN-06 dotnet build | 0 errors, 0 new warnings | 0 errors, 0 warnings (minor divergence: engineer noted 0 new warnings; verifier also finds 0 new warnings) | MATCH |
| SCAN-07 dotnet test | 71/71 PASS | 9/9 B136Tests + 62/62 B129-B135 = 71/71 PASS confirmed | MATCH |

**Divergences**: NONE. All scans MATCH.

## Implementation Verification

### OrderPassesBracketGate (CopyEngine.cs L2671-2680)
- [x] Method exists at correct location (after MatchesLeaderNameTestable)
- [x] Signature matches spec: `private static bool OrderPassesBracketGate(Order order, string? signalName, string? leaderName, bool isStop)`
- [x] Signal path: `if (signalName != null) return order.FromEntrySignal == signalName;` — exact match
- [x] ATM path: `return MatchesLeaderName(order, leaderName, isStop);` — exact match
- [x] CYC = 2 (1 base + 1 if branch)
- [x] No lock(), no throw, no return null (returns bool)

### FindFollowerBracketOrder (list overload) — L2600-2630
- [x] Two-guard sequence replaced by single OrderPassesBracketGate call at L2609
- [x] CYC comment updated to CYC=7 at L2596-2598
- [x] DW-B148 history appended to comment at L2598
- [x] All other logic (state filter, isStop branch, type match, return null) UNCHANGED
- [x] Final CYC = 7 (AT LIMIT RESOLVED)

### SignalOrNameMatches — UNCHANGED
- [x] Method body verified UNCHANGED from pre-B136

### MatchesLeaderName — UNCHANGED
- [x] Method body verified UNCHANGED from pre-B136 (B135 T1 code preserved)

### OrderPassesBracketGateTestable (L2684-2689)
- [x] Test seam exists
- [x] Signature: `internal static bool OrderPassesBracketGateTestable(Order, string?, string?, bool)`
- [x] Expression-body delegate: `=> OrderPassesBracketGate(order, signalName, leaderName, isStop);`

### B136Tests.cs
- [x] File exists at `src/PropTraderTools/Tests/B136Tests.cs`
- [x] 9 [Fact] methods confirmed (grep verified)
- [x] Covers PTT-TGT-Drag ATM path (THE FIX)
- [x] Covers PTT-STP-Drag ATM path (stop fix)
- [x] Covers signal-path match/mismatch
- [x] Covers wrong-leg rejection
- [x] xUnit [Fact] only — no NUnit, no MSTest

### PropTraderTools.csproj
- [x] `<Compile Include="Tests\B136Tests.cs" />` present at L164

## Spec Compliance

### DW-B148 Fix Path Confirmed
Code path verified in source:
1. `SyncFollowerBracket` L2247: `FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal=null, isStop, leaderOrder.Name="Target3")`
2. `FindFollowerBracketOrder` L2609: `OrderPassesBracketGate(order, fromEntrySignalName=null, leaderName="Target3", isStop=false)`
3. `OrderPassesBracketGate` L2677: `signalName == null` → ATM path → `MatchesLeaderName(order, "Target3", false)`
4. `MatchesLeaderName` L2649: `!isStop && order.Name == "PTT-TGT-Drag"` → **true**
5. Order returned. Sync proceeds. **DW-B148 CLOSED.**

### DW-B146 Closure Confirmed
DW-B146 root cause (MatchesLeaderName unreachable for PTT-prefix orders) resolved by DW-B148 fix. **DW-B146 CLOSED** as consequence.

## VERIFY_PASS

All 7 scans MATCH. Implementation satisfies ticket spec, plan, and spec requirements DW-B148 / DW-B146. Zero divergences. Zero violations.

**VERIFY_PASS**
