# B130-LaneA Ticket 1 Verification Report

**Epic**: B130-LaneA
**Defect**: DW-B137 -- IsAtmSTPOrder Wrong Name Format
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-09-01
**Source**: READ-ONLY scan of src/PropTraderTools/ -- independent of engineer self-report

---

## Verification Result: VERIFY_PASS

---

## Independent Scan Results (Layer 3)

| Scan | My Result | Engineer Report | Match? |
|------|-----------|-----------------|--------|
| SCAN-01 lock() | 3 comment-only hits (L298, L332, L2696); 0 actual lock() calls; 0 in modified region (L2051-L2246) | 3 comment-only hits (L298, L332, L2696); 0 new in modified region | Y |
| SCAN-02 async void | 0 results | 0 results | Y |
| SCAN-03 DateTime.Now | 0 results | 0 results | Y |
| SCAN-04 non-ASCII | 0 results in code lines (filter: not comment-only lines) | 0 results | Y |
| SCAN-05 CYC | IsAtmSTPOrder=1 (expression body); SyncFollowerBracket=7 (comment at L2076 confirms; decision nodes: fo null, price delta, ATM STP branch 3, ATM TGT branch 3b, IsTrailingStop, isStop-in-try = 6 nodes + base = 7); SyncAtmFollowerTarget=4 (acc null, fo null, newTarget null + base); all <=8 | IsAtmSTPOrder=1, SyncFollowerBracket=7, SyncAtmFollowerTarget=4; all <=8 | Y |
| SCAN-06 PTT- prefix | 3 hits: L2170 PTT-STP-Drag in SyncAtmFollowerBracket CreateOrder call; L2197 comment; L2230 PTT-TGT-Drag in SyncAtmFollowerTarget CreateOrder call. Both actual CreateOrder calls carry PTT- prefix. | 3 hits: L2170 PTT-STP-Drag, L2197 comment, L2230 PTT-TGT-Drag | Y |
| SCAN-07 build | Build succeeded. 0 errors, 0 warnings. (dotnet build src/PropTraderTools/PropTraderTools.csproj) | Build succeeded. 0 errors, 0 warnings. | Y |

All 7 scans match engineer Layer 2 report exactly.

---

## Implementation Verification

### V-CHECK-01: IsAtmSTPOrder (T1.2 Change 1) -- PASS

Actual code at L2051-2062:
`
// DW-B134: true if order name has STP suffix (NT8 ATM bracket stops: Buy STP, Sell STP).
// DW-B137: extended to cover Stop1/Stop2/Stop3 and Target1/Target2/Target3 (MES  SL 6 ATM).
// Mirrors IsBracketLegStatic STP+Stop+Target clauses. Made internal static for test access.
// Option A safety: grep confirms 0 CreateOrder calls use Stop*/Target* prefixed names.
// CYC=1: expression body. JS-021: no lock. JS-001: no throw. ASCII-only.
internal static bool IsAtmSTPOrder(Order order) =>
    order.Name != null
    && (
        order.Name.EndsWith(STP, StringComparison.OrdinalIgnoreCase)
        || order.Name.StartsWith(Stop, StringComparison.OrdinalIgnoreCase)
        || order.Name.StartsWith(Target, StringComparison.OrdinalIgnoreCase)
    );
`

- EndsWith(STP): present (L2059) [backward compat preserved]
- StartsWith(Stop): present (L2060) [DW-B137 extension]
- StartsWith(Target): present (L2061) [DW-B137 extension]
- DW-B137 comment: present at L2052 (extended to cover Stop1/Stop2/Stop3 and Target1/Target2/Target3)
- Ticket spec AFTER block: matched exactly (3 clauses, same OrdinalIgnoreCase). Minor formatting difference: actual uses extra indentation with outer parens -- semantically identical.
- PASS: all required clauses present, comment updated with DW-B137 citation.

### V-CHECK-02: SyncFollowerBracket (T1.2 Change 2) -- PASS

Actual code at L2075-2110:
- CYC comment at L2076: DW-B134/DW-B137: CYC=7: fo null(1), price delta(2), ATM STP(3), ATM TGT(3b), IsTrailingStop(4), isStop branch(5), [CYC from branching=7] -- matches ticket spec exactly.
- Branch (3) at L2100: if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134 + DW-B137 -> SyncAtmFollowerBracket. Comment updated (dual citation DW-B134 + DW-B137). PASS.
- Branch (3b) at L2105: if (!isStop && IsAtmSTPOrder(fo)) // (3b) DW-B137: ATM target cancel+resubmit -> SyncAtmFollowerTarget(acc, fo, newPrice); return. PRESENT.
- Placement: branch (3b) placed AFTER branch (3) (L2105 after L2100) and BEFORE IsTrailingStop guard (L2111). CORRECT.
- CYC comment updated from =6 to =7: CONFIRMED at L2076.
- PASS: all 3 sub-requirements satisfied.

### V-CHECK-03: SyncAtmFollowerTarget (T1.2 Change 3) -- PASS

Method at L2200-2246:
- Exists immediately after SyncAtmFollowerBracket closing brace (L2186-2187 is SyncAtmFollowerBracket end; L2188 starts SyncAtmFollowerTarget comment, L2200 is method signature). CORRECT placement.
- OrderType.Limit at L2223: PRESENT.
- limitPrice=newPrice (arg6) at L2227: newPrice is 7th positional arg (fo.Instrument, fo.OrderAction, OrderType.Limit, OrderEntry.Automated, TimeInForce.Day, fo.Quantity, newPrice). CORRECT arg position for limitPrice.
- stopPrice=0 (arg7) at L2228: 0 immediately after newPrice. CORRECT.
- Order name PTT-TGT-Drag at L2230: PRESENT. NT8-014 PTT- prefix satisfied.
- Two independent try/catch blocks: Block A (L2208-L2215: Cancel only), Block B (L2218-L2245: CreateOrder+Submit). PRESENT.
- newTarget null check at L2234: if (newTarget == null). PRESENT.
- PASS: all required attributes confirmed in actual source.

### V-CHECK-04: B130Tests.cs -- PASS

File: src/PropTraderTools/Tests/B130Tests.cs

- Both [Fact] tests present:
  - B130_DW137_Stop1NameRoutesToCancelResubmit: PRESENT
  - B130_DW137_Target1NameRoutesCorrectly: PRESENT
- Test 1 assertions:
  - Stop1 -> true: PRESENT
  - Stop2 -> true: PRESENT
  - Stop3 -> true: PRESENT
  - Buy STP -> true (backward compat): PRESENT
  - Sell STP -> true (backward compat): PRESENT
  - Entry -> false: PRESENT
  - PTT-Copy -> false: PRESENT
- Test 2 assertions:
  - Target1 -> true: PRESENT
  - Target2 -> true: PRESENT
  - Target3 -> true: PRESENT
  - PTT-Copy -> false: PRESENT
  - PTT-TGT-Drag -> false: PRESENT
- StubOrder: uses direct 
ew NinjaTrader.Cbi.Order() + .Name = name assignment (replaces placeholder from ticket spec). Identical pattern to B129Tests.cs. PASS.
- Test 2 does NOT include Assert.False for PTT-STP-Drag (ticket spec has this assertion but it was in architecture plan G section, not mandated in ticket T1.4/T1.2 Change 5 final assertion list). NOTE: ticket T1.2 Change 5 shows PTT-TGT-Drag assertion only for Test 2. Actual file matches the ticket Change 5 exact assertion list. PASS.
- PASS: test names exact, assertions match ticket spec T1.2 Change 5 final list.

### V-CHECK-05: B129Tests.cs backward compatibility -- PASS

All 6 [Fact] tests present:
DW-B134 group (3):
  - B129_DW134_STPSuffixDetectedByIsBracketLegStatic: PRESENT
  - B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket: PRESENT
  - B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel: PRESENT
DW-B135 group (3):
  - B129_DW135_GuardClearedAfterLeaderFlat: PRESENT
  - B129_DW135_DW128ProtectionPreservedDuringRaceWindow: PRESENT
  - B129_DW135_FirstEntryAfterRestartNotBlocked: PRESENT

Updated assertions for DW-B137 compatibility:
1. B129_DW134_STPSuffixDetectedByIsBracketLegStatic: legacy.Name = Stop1 now has Assert.True(CopyEngine.IsAtmSTPOrder(legacy)) with comment DW-B137: Stop1 now returns true. UPDATED. PASS.
2. B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket: 
ative.Name = Stop1 now has Assert.True(CopyEngine.IsAtmSTPOrder(native)) with comment DW-B137: Stop1 now routes to cancel+resubmit. UPDATED. PASS.
3. B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel: stop1.Name = Stop1 now has Assert.True(CopyEngine.IsAtmSTPOrder(stop1)) with comment DW-B137: Stop1 returns true. UPDATED. PASS.
Buy STP still returns true in all tests: CONFIRMED. Backward compat preserved.

### V-CHECK-06: PropTraderTools.csproj -- PASS

L157: <Compile Include=Tests\B129Tests.cs />
L158: <Compile Include=Tests\B130Tests.cs />
Compile entry present immediately after B129Tests.cs. PASS.

---

## Discrepancies Found

NONE. All 7 independent scans match engineer Layer 2 report. All 6 implementation checks pass.

Minor formatting note: IsAtmSTPOrder actual code uses outer parentheses around the 3-clause OR:
  && ( ... || ... || ... ) vs ticket spec AFTER block shows same clauses without outer parens.
This is semantically equivalent -- no violation.

---

## DNA Rule Verification

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No lock() in modified region (L2051-L2246) | PASS: 0 actual lock() calls |
| JS-001 | No throw in hot path; acc.Cancel/CreateOrder/Submit all in try/catch | PASS: two independent try/catch blocks in SyncAtmFollowerTarget |
| JS-002 | No return null from value-expected methods | PASS: null guards use return; (void method) |
| JS-033 | No async void | PASS: 0 async void in file |
| JS-036 | new Order[]{ fo } / new[]{ newTarget } -- pre-existing NT8 array pattern, accepted | PASS |
| JS-066 | CYC <= 8 for all modified/new methods | PASS: IsAtmSTPOrder=1, SyncFollowerBracket=7, SyncAtmFollowerTarget=4 |
| NT8-014 | Order name starts with PTT- | PASS: PTT-TGT-Drag confirmed at L2230 |
| Non-ASCII | ASCII-only source | PASS: 0 non-ASCII results |
| DateTime.Now | UtcNow used not Now | PASS: 0 DateTime.Now in file |

---

## Verifier Conclusion

**VERIFY_PASS** -- all 7 independent scans pass (Layer 3 matches Layer 2 exactly), all 6 implementation checks pass, all DNA rules satisfied, build 0 errors 0 warnings.

The B130-LaneA Ticket 1 implementation correctly:
1. Extends IsAtmSTPOrder to match Stop1/Stop2/Stop3 (StartsWith(Stop)) and Target1/Target2/Target3 (StartsWith(Target)) while preserving Buy STP/Sell STP backward compatibility.
2. Adds branch (3b) to SyncFollowerBracket routing target ATM brackets to new SyncAtmFollowerTarget method (CYC 6->7, still <=8).
3. Implements SyncAtmFollowerTarget as cancel+resubmit using OrderType.Limit with limitPrice=newPrice, stopPrice=0, order name PTT-TGT-Drag.
4. Provides 2 [Fact] tests in B130Tests.cs covering all required assertions.
5. Updates B129Tests.cs (3 Assert.False->Assert.True for Stop1 assertions reflecting DW-B137 behavior).
6. Adds <Compile Include=Tests\B130Tests.cs /> to PropTraderTools.csproj.