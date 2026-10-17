# B196 Ticket 2 Verification Report

**SCOPE LOCK -- TICKET 2 ONLY**
**Epic**: B196
**Ticket**: T2 -- DW-B170-01: FindLeaderStopPrice Missing OrderState.Accepted
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-10-11
**Layer**: 3 (independent re-run -- never trusts Layer 2)

---

## SCOPE LOCK Confirmation

This verification covers Ticket 2 ONLY.
No completion files for T1, T3, T4, or T5 were read in this session.
Only the following files were read:
- src/PropTraderTools/CopyEngine.cs (lines 3698-3725 -- FindLeaderStopPrice region)
- docs/brain/B196/04-tickets.md (T2 section)
- docs/brain/B196/02-architecture-plan.md (section 3 T2)
- docs/brain/B196/ticket-2-completion.md (Layer 2 reference -- not trusted, compared only)
- src/PropTraderTools/Tests/B196Tests.cs (T2 tests)

---

## Independent 7-Scan Results (Layer 3)

### SCAN 1 -- JS-021 lock()

Command: Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\(" | Where-Object { $_.Line -notmatch "//.*lock" }
Result: 0 matches
Status: PASS

### SCAN 2 -- JS-033 async void

Command: Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "async void " | Where-Object { $_.Line -notmatch "//" }
Result: 0 matches
Status: PASS

### SCAN 3 -- JS-066 CYC: Independent Branch Count of FindLeaderStopPrice

Source lines read: 3703-3719 (verbatim from CopyEngine.cs)

    private static double FindLeaderStopPrice(Account? leaderAccount, int bracketIndex)
    {
        if (leaderAccount == null)                               // (1)
            return 0.0;
        if (bracketIndex <= 0)                                   // (2)
            return 0.0;
        var targetName = "Stop" + bracketIndex.ToString();
        foreach (var order in leaderAccount.Orders.ToList())     // (3)
        {
            if (
                order.Name == targetName                         // (4) &&
                && (order.OrderState == OrderState.Working ||    // (5)
                    order.OrderState == OrderState.Accepted)     // (6) ||
            ) // DW-B170-01
                return order.StopPrice;
        }
        return 0.0;
    }

Independent CYC count:
  Base:                          1
  (1) if (leaderAccount == null) +1 = 2
  (2) if (bracketIndex <= 0)     +1 = 3
  (3) foreach                    +1 = 4
  (4) && in compound condition   +1 = 5
  (5)+(6) || operator            +1 = 6
  TOTAL: CYC = 6

Engineer reported CYC: 6
Verifier independent count: 6
Cross-check: MATCH
Limit: 8
Status: PASS

### SCAN 4 -- JS-001 throw new

Command: Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "throw new "
Result: 0 matches in entire file
Status: PASS

### SCAN 5 -- JS-002 return null (T2 scope: FindLeaderStopPrice)

Command: Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "return null"
Result: return null occurrences found at lines 1271, 2022, 3158, 3239, 3247, 3891, 3941, 4116, 5471, 5477, 5556, 6358, 6373
Scope check: NONE are in FindLeaderStopPrice (lines 3703-3719). Method returns double -- null cannot be returned.
Status: PASS (no null return in T2 target method)

### SCAN 6 -- ASCII-only (modified region lines 3699-3720)

Command: Get-Content src/PropTraderTools/CopyEngine.cs | Select-Object -Index (3698..3719) | Where-Object { $_ -match '[^\x00-\x7F]' }
Result: 0 non-ASCII characters
Status: PASS

### SCAN 7 -- DateTime.Now ban

Command: Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "DateTime\.Now[^U]"
Result: 1 match -- line 4391: "// ASCII-only. No DateTime.Now. No lock()." -- COMMENT ONLY, not actual usage
Assessment: Not introduced by T2. No actual DateTime.Now call in file.
Status: PASS

---

## 7-Scan Summary

| Scan | Rule      | Check                      | Result                     | Status |
|------|-----------|----------------------------|----------------------------|--------|
| 1    | JS-021    | lock() calls               | 0 actual lock calls        | PASS   |
| 2    | JS-033    | async void                 | 0 async void               | PASS   |
| 3    | JS-066    | CYC of FindLeaderStopPrice | CYC=6 (limit 8)            | PASS   |
| 4    | JS-001    | throw new                  | 0 throw new                | PASS   |
| 5    | JS-002    | return null in T2 method   | 0 (method returns double)  | PASS   |
| 6    | ASCII     | non-ASCII in modified region | 0 non-ASCII chars          | PASS   |
| 7    | DateTime  | DateTime.Now actual usage  | 0 actual usages            | PASS   |

---

## Layer 2 Cross-Check (Engineer Self-Report vs Layer 3)

| Scan | Engineer (Layer 2)             | Verifier (Layer 3)             | Match? |
|------|-------------------------------|-------------------------------|--------|
| 1    | 0 matches                     | 0 matches                     | YES    |
| 2    | 0 matches                     | 0 matches                     | YES    |
| 3    | CYC=6                         | CYC=6 independent count       | YES    |
| 4    | 0 matches                     | 0 matches                     | YES    |
| 5    | 0 in region                   | 0 in method (returns double)  | YES    |
| 6    | 0 non-ASCII                   | 0 non-ASCII                   | YES    |
| 7    | 1 comment at 4391, 0 new usage| 1 comment at 4391, 0 new usage| YES    |

Layer 2 / Layer 3 discrepancy count: 0

---

## Correctness Checklist

[PASS] FindLeaderStopPrice predicate includes OrderState.Accepted
       Line 3714: && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)

[PASS] OrderState.Working REMAINS in the predicate (regression guard)
       Line 3714: Working present in same compound expression

[PASS] CYC comment updated from CYC=5 to CYC=6
       Line 3701: // CYC=6: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working; (6) state==Accepted. DW-B170-01.

[PASS] Comment includes "DW-B170-01" tag
       Line 3701 (CYC comment) and line 3715 (// DW-B170-01 closing comment)

[PASS] No other methods modified
       Only lines 3701, 3714-3715 changed. Scope confirmed.

[PASS] [Fact] FindLeaderStopPrice_AcceptedStop_ReturnsPrice exists in B196Tests.cs
       Confirmed present in test file.

[PASS] [Fact] FindLeaderStopPrice_WorkingStop_StillReturnsPrice exists in B196Tests.cs
       Confirmed present in test file.

[PASS] Fix matches Ticket 2 contract from 04-tickets.md
       Change 1 (CYC comment) and Change 2 (predicate) match contract text verbatim.

[PASS] Fix matches B196 architecture plan section 3 T2 design
       Exact match to plan text in section 3.

All 9 checklist items: PASS

---

## Architecture Compliance

### Contract vs Actual Source (exact text)

CONTRACT predicate (04-tickets.md Change 2):
        if (
            order.Name == targetName // (4)
            && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted) // (5)(6)
        ) // DW-B170-01
            return order.StopPrice;

ACTUAL SOURCE lines 3712-3716:
                if (
                    order.Name == targetName // (4)
                    && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted) // (5)(6)
                ) // DW-B170-01
                    return order.StopPrice;

Match: EXACT (indentation only differs per source context). PASS.

CONTRACT CYC comment (04-tickets.md Change 1):
// CYC=6: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working; (6) state==Accepted. DW-B170-01.

ACTUAL SOURCE line 3701:
        // CYC=6: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working; (6) state==Accepted. DW-B170-01.

Match: EXACT. PASS.

### Test Coverage

| Test | Pattern | Coverage |
|------|---------|----------|
| FindLeaderStopPrice_AcceptedStop_ReturnsPrice | BCL File.ReadAllText + Assert.Contains("OrderState.Accepted") and full compound predicate | Verifies Accepted added |
| FindLeaderStopPrice_WorkingStop_StillReturnsPrice | BCL File.ReadAllText + compound predicate + CYC=6 DW-B170-01 comment check | Verifies Working retained; CYC comment updated |

Both tests use BCL source-text pattern (no NT8 runtime required).
Consistent with DwB170Tests precedent referenced in ticket contract.

### DW Item Coverage

| DW ID      | Description                                      | Closed? |
|-----------|--------------------------------------------------|---------|
| DW-B170-01 | FindLeaderStopPrice missing OrderState.Accepted | YES -- line 3714 confirmed |

---

## Additional DNA Rule Checks

| Rule | Check | Result |
|------|-------|--------|
| NT8: No sealed on TradeCopierWindow | T2 does not touch TradeCopierWindow | N/A |
| NT8: No FontFamily= (SCAN-03) | T2 adds no WPF elements | N/A |
| NT8: No #RRGGBB hex (SCAN-04) | T2 adds no color strings | N/A |
| NT8: CreateOrder with PTT- prefix (SCAN-05) | T2 adds no CreateOrder calls | N/A |
| NT8: DateTime.UtcNow ban (SCAN-07) | 0 new DateTime.Now calls | PASS |
| Concurrency: no shared mutable collection | FindLeaderStopPrice is static, reads Orders.ToList() snapshot only | PASS |
| Immutability: new SolidColorBrush Freeze | T2 adds no UI elements | N/A |
| Magic strings for mode discrimination | T2 adds no string mode flags | N/A |

---

## Verdict

**VERIFY_PASS**

- All 7 scans returned 0 violations (Layer 3 independent run)
- All 9 correctness checklist items: PASS
- Layer 2 / Layer 3 cross-check: 0 discrepancies
- Architecture plan and ticket contract: exact verbatim match
- Both required [Fact] tests present and correct in B196Tests.cs
- CYC=6 independently confirmed (matches engineer Layer 2 count)
- OrderState.Working regression guard confirmed present
- DW-B170-01 closed

---

*Verifier: ptt-verifier*
*Epic: B196*
*Phase: 4b (Verification)*
*Return: VERIFY_PASS*